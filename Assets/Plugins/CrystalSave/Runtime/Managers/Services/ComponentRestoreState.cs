#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Arawn.CrystalSave.Runtime
{
    // Owns pending bytes, application identities and deserialization markers together.
    // ComponentManager supplies routing; this state never resolves the global singleton.
    internal sealed class ComponentRestoreState
    {
        readonly IEqualityComparer<ISaveable> componentComparer;
        readonly Dictionary<string, byte[]> pendingComponentData = new();
        readonly Dictionary<string, RestoreApplication> restoreApplications = new();
        readonly HashSet<string> deserializedComponents = new();
        readonly Dictionary<string, HashSet<ISaveable>> deserializedComponentOwners = new();
        internal object Generation { get; private set; } = new object ();
        internal IReadOnlyDictionary<string, byte[]> Pending { get; }

        sealed class RestoreApplication
        {
            internal readonly ISaveable Component;
            internal readonly byte[] Data;

            internal RestoreApplication(ISaveable component, byte[] data)
            {
                Component = component;
                Data = data;
            }
        }

        internal ComponentRestoreState(IEqualityComparer<ISaveable> componentComparer)
        {
            this.componentComparer = componentComparer;
            Pending = new ReadOnlyDictionary<string, byte[]>(pendingComponentData);
        }

        internal void BeginGeneration()
        {
            Generation = new object ();
            restoreApplications.Clear();
            pendingComponentData.Clear();
            ResetDeserializedComponents();
        }

        internal void RemoveIdentifierMarker(string identifier) => deserializedComponents.Remove(identifier);
        internal void ResetDeserializedComponents()
        {
            deserializedComponents.Clear();
            deserializedComponentOwners.Clear();
        }

        internal void MarkComponentDeserialized(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                bool wasAlreadyMarked = deserializedComponents.Contains(id);
                deserializedComponents.Add(id);
                if (Logger.ShouldLog(LogCategory.ComponentManager))
                {
                    var builder = new StringBuilder(96);
                    builder.Append("ComponentManager: MarkComponentDeserialized '").Append(id).Append("' (wasAlreadyMarked: ").Append(wasAlreadyMarked).Append(')');
                    Logger.Log(builder.ToString(), LogCategory.ComponentManager, LogLevel.Info);
                }
            }
        }

        internal bool HasComponentDeserialized(string id)
        {
            bool result = !string.IsNullOrEmpty(id) && (deserializedComponents.Contains(id) || (deserializedComponentOwners.TryGetValue(id, out var owners) && owners.Count > 0));
            if (Logger.ShouldLog(LogCategory.ComponentManager))
            {
                var builder = new StringBuilder(96);
                builder.Append("ComponentManager: HasComponentDeserialized '").Append(id).Append("' = ").Append(result);
                Logger.Log(builder.ToString(), LogCategory.ComponentManager, LogLevel.Info);
            }

            return result;
        }

        internal void MarkComponentDeserialized(ISaveable component, string id)
        {
            if (component == null)
            {
                MarkComponentDeserialized(id);
                return;
            }

            if (string.IsNullOrEmpty(id))
                return;
            if (!deserializedComponentOwners.TryGetValue(id, out var owners))
            {
                owners = new HashSet<ISaveable>(componentComparer);
                deserializedComponentOwners[id] = owners;
            }

            bool wasAlreadyMarked = deserializedComponents.Contains(id) || owners.Contains(component);
            owners.Add(component);
            if (Logger.ShouldLog(LogCategory.ComponentManager))
            {
                var builder = new StringBuilder(112);
                builder.Append("ComponentManager: MarkComponentDeserialized '").Append(id).Append("' for component instance (wasAlreadyMarked: ").Append(wasAlreadyMarked).Append(')');
                Logger.Log(builder.ToString(), LogCategory.ComponentManager, LogLevel.Info);
            }
        }

        internal bool HasComponentDeserialized(ISaveable component, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            // An identifier-only marker created through the public API deliberately
            // blocks every matching instance, preserving its historical semantics.
            if (deserializedComponents.Contains(id))
                return true;
            return component != null && deserializedComponentOwners.TryGetValue(id, out var owners) && owners.Contains(component);
        }

        // Retained bytes still own restore precedence while application is delayed or
        // failed. A cached home/shared snapshot must not replace that retry payload.
        internal bool HasRestoreData(ISaveable component, string id)
            => HasComponentDeserialized(component, id) ||
               (!string.IsNullOrEmpty(id) &&
                (pendingComponentData.ContainsKey(id) || restoreApplications.ContainsKey(id)));

        internal bool IsApplying(ISaveable component, string id, byte[] data)
            => !string.IsNullOrEmpty(id) && restoreApplications.TryGetValue(id, out var application) &&
               ReferenceEquals(application.Component, component) && ReferenceEquals(application.Data, data);

        internal void RemoveDeserializedComponentOwner(ISaveable component, string identifier)
        {
            if (component == null || string.IsNullOrEmpty(identifier) || !deserializedComponentOwners.TryGetValue(identifier, out var owners))
            {
                return;
            }

            owners.Remove(component);
            if (owners.Count == 0)
                deserializedComponentOwners.Remove(identifier);
        }

        internal bool RestoreComponent(ISaveable component, byte[] data, string identifier = null)
        {
            identifier ??= component.UniqueIdentifier;
            string key = identifier;
            var application = new RestoreApplication(component, data);
            restoreApplications[key] = application;
            bool applied = false;
            bool OwnsApplication() => restoreApplications.TryGetValue(key, out var current) && ReferenceEquals(current, application);
            bool completed = ComponentOperationScope.Restore(component, data, () =>
            {
                if (!OwnsApplication())
                    return;
                MarkComponentDeserialized(component, key);
                RemovePendingComponentData(key);
                applied = true;
            }, () =>
            {
                if (OwnsApplication())
                    pendingComponentData[key] = data;
            }, this, () =>
            {
                // A failed application retains its bytes, but registration must be
                // allowed to retry it. A late completion cannot retire a replacement.
                if (OwnsApplication())
                    restoreApplications.Remove(key);
            });
            return completed && applied;
        }

        internal void QueuePendingComponentData(string identifier, byte[] data)
        {
            restoreApplications.Remove(identifier);
            pendingComponentData[identifier] = data;
        }

        internal void RemovePendingComponentData(string uniqueIdentifier)
        {
            if (string.IsNullOrEmpty(uniqueIdentifier))
                return;
            restoreApplications.Remove(uniqueIdentifier);
            pendingComponentData.Remove(uniqueIdentifier);
        }

        internal void Reindex(ISaveable component, string previousIdentifier, string currentIdentifier)
        {
            // Preserve pending data that may have been queued under the old key.
            if (pendingComponentData.TryGetValue(previousIdentifier, out var pendingBytes))
            {
                if (!pendingComponentData.ContainsKey(currentIdentifier))
                    QueuePendingComponentData(currentIdentifier, pendingBytes);
                RemovePendingComponentData(previousIdentifier);
            }

            // Preserve deserialized marker if it was recorded under the old key.
            if (deserializedComponents.Remove(previousIdentifier))
                deserializedComponents.Add(currentIdentifier);
            if (deserializedComponentOwners.TryGetValue(previousIdentifier, out var previousOwners) && previousOwners.Remove(component))
            {
                if (previousOwners.Count == 0)
                    deserializedComponentOwners.Remove(previousIdentifier);
                if (!deserializedComponentOwners.TryGetValue(currentIdentifier, out var currentOwners))
                {
                    currentOwners = new HashSet<ISaveable>(componentComparer);
                    deserializedComponentOwners[currentIdentifier] = currentOwners;
                }

                currentOwners.Add(component);
            }
        }
    }
}
#endif
