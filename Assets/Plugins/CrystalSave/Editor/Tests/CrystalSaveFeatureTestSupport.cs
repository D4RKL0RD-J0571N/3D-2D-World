#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    internal static class FeatureTestSupport
    {
        internal static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }
        internal static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        internal static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
        internal static IDisposable CreateOperationContext()
            => (IDisposable)Activator.CreateInstance(
                typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveOperationContext", true),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { System.Threading.CancellationToken.None, System.Threading.CancellationToken.None }, null);
        internal static IReadOnlyDictionary<string, byte[]> PendingBytes(ComponentManager manager)
            => (IReadOnlyDictionary<string, byte[]>)typeof(ComponentManager)
                .GetProperty("PendingComponentData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

        internal static IReadOnlyDictionary<string, GameObject> CachedObjects(SaveManager manager)
            => (IReadOnlyDictionary<string, GameObject>)typeof(SaveManager)
                .GetProperty("CachedObjects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

        internal static IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, byte[]>>> HomeSnapshots(ComponentManager manager)
            => (IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, byte[]>>>)typeof(ComponentManager)
                .GetProperty("HomeSceneSnapshots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);

        internal static object Call(object target, string name, params object[] args)
        {
            for (var type = target as Type ?? target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null) return method.Invoke(target is Type ? null : target, args);
            }
            throw new MissingMethodException(name);
        }
        internal static RememberTransform ConfigureTransform(GameObject go, SpaceMode space = SpaceMode.Local)
        {
            var c = go.GetComponent<RememberTransform>() ?? go.AddComponent<RememberTransform>();
            foreach (string field in new[] { "rememberPositionSettings", "rememberRotationSettings", "rememberScaleSettings" })
                Set(c, field, new TransformPropertySettings { Save = true, SpaceMode = space });
            return c;
        }
        internal static RememberComposite Composite(GameObject go, params Type[] types)
        {
            var c = go.AddComponent<RememberComposite>();
            var so = new SerializedObject(c);
            var list = so.FindProperty("_rememberTypes");
            list.arraySize = types.Length;
            for (int i = 0; i < types.Length; i++) list.GetArrayElementAtIndex(i).stringValue = types[i].AssemblyQualifiedName;
            so.ApplyModifiedPropertiesWithoutUndo();
            c.RefreshComponents();
            return c;
        }
        internal static void Drain(IEnumerator routine)
        {
            while (routine.MoveNext()) if (routine.Current is IEnumerator child) Drain(child);
        }
    }

    internal sealed class FeatureSettingsScope : IDisposable
    {
        private readonly FieldInfo cache = typeof(CrystalSaveOverrides).GetField("cached", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object previous;
        internal readonly SaveSettings Settings;
        internal readonly CrystalSaveAssetOverrides Overrides;
        private readonly LogLevel logLevel;
        internal FeatureSettingsScope()
        {
            previous = cache.GetValue(null);
            logLevel = Arawn.CrystalSave.Runtime.Logger.LogThreshold;
            Settings = ScriptableObject.CreateInstance<SaveSettings>();
            Settings.editorAutomationSettingsInitialized = true;
            Settings.autoRegisterPrefabs = false;
            Settings.enablePrefabRegistryAutoValidation = false;
            Settings.enableCloudSave = false;
            Settings.enableScreenshots = false;
            Settings.runPersistentPathMigrationOnStartup = false;
            Overrides = ScriptableObject.CreateInstance<CrystalSaveAssetOverrides>();
            Overrides.saveSettings = Settings;
            Overrides.prefabRegistry = ScriptableObject.CreateInstance<PrefabRegistry>();
            Overrides.sceneObjectRegistry = ScriptableObject.CreateInstance<SceneObjectRegistry>();
            Overrides.tagRegistry = ScriptableObject.CreateInstance<TagRegistry>();
            Overrides.tagRegistry.Tags.AddRange(UnityEditorInternal.InternalEditorUtility.tags);
            cache.SetValue(null, Overrides);
            CrystalSaveEditorAutomationSettings.ClearCache();
        }
        public void Dispose()
        {
            cache.SetValue(null, previous);
            CrystalSaveEditorAutomationSettings.ClearCache();
            Arawn.CrystalSave.Runtime.Logger.LogThreshold = logLevel;
            Object.DestroyImmediate(Overrides.prefabRegistry);
            Object.DestroyImmediate(Overrides.sceneObjectRegistry);
            Object.DestroyImmediate(Overrides.tagRegistry);
            Object.DestroyImmediate(Overrides);
            Object.DestroyImmediate(Settings);
        }
    }

    internal sealed class FeatureProbe : SaveableComponent
    {
        internal int Value;
        internal int Loads;
        internal Action Loaded;
        protected override byte[] SerializeComponentData() => BitConverter.GetBytes(Value);
        protected override void DeserializeComponentData(byte[] data)
        {
            Value = BitConverter.ToInt32(data, 0);
            Loads++;
            Loaded?.Invoke();
        }
    }
}
#endif
