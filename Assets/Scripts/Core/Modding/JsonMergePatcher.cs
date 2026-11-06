namespace Core.Modding
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using Core.Content;
    using Core.Ids;
    using Data.Config;
    using Domain.Grid;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEngine;

    /// <summary>
    /// Implements RFC 7396 JSON Merge Patch semantics with property-level provenance tracking (<c>ARCH-04</c>).
    /// Keeps Layer A Engine Core 100% independent of Layer B/C Fantasy/Skyrim namespaces while supporting
    /// both <see cref="ScriptableObject"/> definitions (<see cref="ItemData"/>, <see cref="BlockDefinition"/>)
    /// and any POCO <see cref="IContentDefinition"/> registered by framework or content modules.
    /// </summary>
    public static class JsonMergePatcher
    {
        #region RFC 7396 Merge Patch

        /// <summary>
        /// Applies an RFC 7396 JSON Merge Patch (<paramref name="patch"/>) onto <paramref name="target"/> in place,
        /// recording per-property modification provenance in <paramref name="propertyProvenance"/>.
        /// </summary>
        public static void ApplyMergePatch(
            JObject target,
            JObject patch,
            ResourceId contentId,
            string modId,
            string baseModId,
            Dictionary<string, string> propertyProvenance,
            ModDiagnosticReport report = null)
        {
            if (target == null || patch == null) return;
            ApplyMergePatchRecursive(target, patch, contentId, modId, baseModId, string.Empty, propertyProvenance, report);
        }

        private static void ApplyMergePatchRecursive(
            JObject target,
            JObject patch,
            ResourceId contentId,
            string modId,
            string baseModId,
            string currentPath,
            Dictionary<string, string> propertyProvenance,
            ModDiagnosticReport report)
        {
            foreach (JProperty prop in patch.Properties())
            {
                if (string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "contentId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "targetId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "targetRegistry", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "operation", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(prop.Name, "conflictPolicy", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string propNameLower = prop.Name.ToLowerInvariant();
                string nextPath = string.IsNullOrEmpty(currentPath) ? propNameLower : $"{currentPath}.{propNameLower}";
                string provenanceKey = $"{contentId.Value}::{nextPath}";

                JProperty existingProp = FindPropertyCaseInsensitive(target, prop.Name);

                if (prop.Value.Type == JTokenType.Null)
                {
                    existingProp?.Remove();
                    if (propertyProvenance != null)
                    {
                        propertyProvenance[provenanceKey] = modId;
                    }
                    continue;
                }

                if (prop.Value is JObject patchChildObj)
                {
                    if (existingProp?.Value is JObject targetChildObj)
                    {
                        ApplyMergePatchRecursive(targetChildObj, patchChildObj, contentId, modId, baseModId, nextPath, propertyProvenance, report);
                    }
                    else
                    {
                        var createdChild = new JObject();
                        target[prop.Name] = createdChild;
                        ApplyMergePatchRecursive(createdChild, patchChildObj, contentId, modId, baseModId, nextPath, propertyProvenance, report);
                    }
                    continue;
                }

                // Leaf scalar or array replacement
                if (propertyProvenance != null &&
                    propertyProvenance.TryGetValue(provenanceKey, out string previousModId) &&
                    !string.IsNullOrEmpty(previousModId) &&
                    !string.Equals(previousModId, baseModId, StringComparison.Ordinal) &&
                    !string.Equals(previousModId, modId, StringComparison.Ordinal))
                {
                    report?.Add(
                        modId,
                        ModDiagnosticSeverity.Warning,
                        "MOD-PATCH-CONFLICT",
                        $"Property override conflict on '{contentId}' field '{nextPath}': mod '{modId}' overwrote change from earlier mod '{previousModId}'.");
                }

                if (existingProp != null)
                {
                    existingProp.Value = prop.Value.DeepClone();
                }
                else
                {
                    target[prop.Name] = prop.Value.DeepClone();
                }

                if (propertyProvenance != null)
                {
                    propertyProvenance[provenanceKey] = modId;
                }
            }
        }

        private static JProperty FindPropertyCaseInsensitive(JObject obj, string name)
        {
            foreach (JProperty p in obj.Properties())
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            return null;
        }

        #endregion

        #region Definition <-> JObject Snapshot & Hydration

        /// <summary>
        /// Serializes the mutable data fields of <paramref name="definition"/> into a canonical <see cref="JObject"/>
        /// suitable for RFC 7396 merge patching.
        /// </summary>
        public static JObject SnapshotToJObject(IContentDefinition definition)
        {
            if (definition == null) return new JObject();

            var obj = new JObject
            {
                ["id"] = definition.ContentId.Value,
                ["displayName"] = definition.DisplayName ?? string.Empty
            };

            switch (definition)
            {
                case ItemData item:
                    obj["damage"] = item.Damage;
                    obj["weight"] = item.Weight;
                    obj["value"] = item.Value;
                    obj["isStackable"] = item.IsStackable;
                    obj["maxStackSize"] = item.MaxStackSize;
                    obj["description"] = item.Description ?? string.Empty;
                    if (!item.PlaceableBlock.Id.IsEmpty)
                    {
                        obj["placeableBlock"] = item.PlaceableBlock.Id.Value;
                    }
                    break;

                case BlockDefinition block:
                    obj["runtimeId"] = block.RuntimeId;
                    obj["renderType"] = block.RenderType.ToString();
                    obj["isSolid"] = block.IsSolid;
                    obj["isLiquid"] = block.IsLiquid;
                    obj["surface"] = block.Surface.ToString();
                    break;

                default:
                    // Generic POCO IContentDefinition snapshot via Newtonsoft
                    JObject generic = JObject.FromObject(definition, JsonSerializer.Create(FantasyEngineJsonSettings.CreateDefault(indented: false)));
                    foreach (JProperty p in generic.Properties())
                    {
                        obj[p.Name] = p.Value;
                    }
                    break;
            }

            return obj;
        }

        /// <summary>
        /// Applies fields from a merged or newly parsed <see cref="JObject"/> onto an <see cref="IContentDefinition"/> instance.
        /// Supports both flat properties (<c>"damage": 18</c>) and nested <c>fragments.weapon.baseDamage</c> syntax (<c>ARCH-04</c>).
        /// </summary>
        public static void ApplyJObjectToDefinition(IContentDefinition definition, JObject json)
        {
            if (definition == null || json == null) return;

            string displayName = ReadString(json, "displayName", "name", "title");

            switch (definition)
            {
                case ItemData item:
                    if (!string.IsNullOrWhiteSpace(displayName)) item.DisplayName = displayName;
                    if (TryReadFloat(json, out float dmg, "damage", "baseDamage") ||
                        TryReadNestedFloat(json, new[] { "fragments", "weapon", "baseDamage" }, out dmg) ||
                        TryReadNestedFloat(json, new[] { "fragments", "weapon", "damage" }, out dmg))
                    {
                        item.Damage = dmg;
                    }
                    if (TryReadFloat(json, out float weight, "weight", "baseWeight"))
                    {
                        item.Weight = weight;
                    }
                    if (TryReadInt(json, out int val, "value", "baseValue", "goldValue"))
                    {
                        item.Value = val;
                    }
                    if (TryReadBool(json, out bool stackable, "isStackable", "stackable"))
                    {
                        item.IsStackable = stackable;
                    }
                    if (TryReadInt(json, out int maxStack, "maxStackSize", "maxStack"))
                    {
                        item.MaxStackSize = maxStack;
                    }
                    string desc = ReadString(json, "description");
                    if (desc != null)
                    {
                        item.Description = desc;
                    }
                    string blockRef = ReadString(json, "placeableBlock", "blockId");
                    if (!string.IsNullOrWhiteSpace(blockRef) && ResourceId.TryParse(blockRef, out ResourceId blockId))
                    {
                        item.PlaceableBlock = new DefinitionRef<BlockDefinition>(blockId);
                    }
                    break;

                case BlockDefinition block:
                    if (!string.IsNullOrWhiteSpace(displayName)) block.DisplayName = displayName;
                    if (TryReadInt(json, out int runtimeId, "runtimeId") && runtimeId >= 0 && runtimeId <= ushort.MaxValue)
                    {
                        block.RuntimeId = (ushort)runtimeId;
                    }
                    if (TryReadBool(json, out bool isSolid, "isSolid"))
                    {
                        block.IsSolid = isSolid;
                    }
                    if (TryReadBool(json, out bool isLiquid, "isLiquid"))
                    {
                        block.IsLiquid = isLiquid;
                    }
                    string renderTypeStr = ReadString(json, "renderType", "shape");
                    if (!string.IsNullOrWhiteSpace(renderTypeStr) && Enum.TryParse(renderTypeStr, true, out BlockRenderType parsedRenderType))
                    {
                        block.RenderType = parsedRenderType;
                    }
                    string surfaceStr = ReadString(json, "surface");
                    if (!string.IsNullOrWhiteSpace(surfaceStr) && Enum.TryParse(surfaceStr, true, out SurfaceType parsedSurface))
                    {
                        block.Surface = parsedSurface;
                    }
                    break;

                default:
                    ApplyReflectionProperties(definition, json, displayName);
                    break;
            }
        }

        private static void ApplyReflectionProperties(IContentDefinition target, JObject json, string displayName)
        {
            Type type = target.GetType();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                PropertyInfo dispProp = type.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (dispProp != null && dispProp.CanWrite)
                {
                    dispProp.SetValue(target, displayName);
                }
            }

            foreach (JProperty jProp in json.Properties())
            {
                if (string.Equals(jProp.Name, "id", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(jProp.Name, "contentId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(jProp.Name, "sourceModId", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                PropertyInfo prop = type.GetProperty(jProp.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                try
                {
                    Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    if (propType.IsEnum)
                    {
                        if (Enum.TryParse(propType, jProp.Value.ToString(), true, out object enumVal))
                        {
                            prop.SetValue(target, enumVal);
                        }
                    }
                    else if (propType == typeof(string))
                    {
                        prop.SetValue(target, jProp.Value.ToString());
                    }
                    else if (propType == typeof(int) && int.TryParse(jProp.Value.ToString(), out int intVal))
                    {
                        prop.SetValue(target, intVal);
                    }
                    else if (propType == typeof(float) && float.TryParse(jProp.Value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
                    {
                        prop.SetValue(target, floatVal);
                    }
                    else if (propType == typeof(bool) && bool.TryParse(jProp.Value.ToString(), out bool boolVal))
                    {
                        prop.SetValue(target, boolVal);
                    }
                    else if (jProp.Value is JObject dictObj && typeof(IDictionary).IsAssignableFrom(propType))
                    {
                        object converted = dictObj.ToObject(prop.PropertyType);
                        if (converted != null) prop.SetValue(target, converted);
                    }
                }
                catch
                {
                    // Ignore non-convertible optional property
                }
            }
        }

        /// <summary>
        /// Creates a clone of <paramref name="source"/> so base definitions are never permanently mutated
        /// when applying profile-scoped mod patches.
        /// </summary>
        public static T CloneDefinition<T>(T source) where T : class, IContentDefinition
        {
            if (source == null) return null;

            if (source is ScriptableObject so)
            {
                ScriptableObject copy = UnityEngine.Object.Instantiate(so);
                copy.name = so.name;
                return copy as T;
            }

            string json = JsonConvert.SerializeObject(source, FantasyEngineJsonSettings.CreateDefault(indented: false));
            return (T)JsonConvert.DeserializeObject(json, source.GetType(), FantasyEngineJsonSettings.CreateDefault(indented: false));
        }

        #endregion

        #region JSON Token Readers

        public static string ReadString(JObject json, params string[] keys)
        {
            foreach (string key in keys)
            {
                JProperty prop = FindPropertyCaseInsensitive(json, key);
                if (prop != null && prop.Value.Type != JTokenType.Null)
                {
                    return prop.Value.ToString();
                }
            }
            return null;
        }

        public static bool TryReadFloat(JObject json, out float value, params string[] keys)
        {
            foreach (string key in keys)
            {
                JProperty prop = FindPropertyCaseInsensitive(json, key);
                if (prop != null && float.TryParse(prop.Value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
            value = 0f;
            return false;
        }

        public static bool TryReadNestedFloat(JObject json, string[] path, out float value)
        {
            value = 0f;
            JToken current = json;
            foreach (string segment in path)
            {
                if (current is JObject obj)
                {
                    JProperty prop = FindPropertyCaseInsensitive(obj, segment);
                    if (prop == null) return false;
                    current = prop.Value;
                }
                else
                {
                    return false;
                }
            }

            return current != null && float.TryParse(current.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        public static bool TryReadInt(JObject json, out int value, params string[] keys)
        {
            foreach (string key in keys)
            {
                JProperty prop = FindPropertyCaseInsensitive(json, key);
                if (prop != null && int.TryParse(prop.Value.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
            value = 0;
            return false;
        }

        public static bool TryReadBool(JObject json, out bool value, params string[] keys)
        {
            foreach (string key in keys)
            {
                JProperty prop = FindPropertyCaseInsensitive(json, key);
                if (prop != null && bool.TryParse(prop.Value.ToString(), out value))
                {
                    return true;
                }
            }
            value = false;
            return false;
        }

        #endregion
    }
}
