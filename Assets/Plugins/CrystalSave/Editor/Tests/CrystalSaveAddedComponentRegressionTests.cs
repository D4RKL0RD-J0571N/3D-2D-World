#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    internal sealed class AddedComponentFieldsProbe : MonoBehaviour
    {
        public int Score;
        public Vector3 Direction;
        public Quaternion Rotation;
    }

    public sealed class CrystalSaveAddedComponentRegressionTests
    {
        [TestCase("Score", "en-US")]
        [TestCase("Direction", "en-US")]
        [TestCase("Rotation", "en-US")]
        [TestCase("Direction", "de-DE")]
        [TestCase("Rotation", "de-DE")]
        public void AddedComponent_SupportedFieldsRoundTripWithoutDisplayRounding(string field, string culture)
        {
            using var settings = new FeatureSettingsScope();
            var previousCulture = CultureInfo.CurrentCulture;
            var root = new GameObject("Added field round trip");
            root.SetActive(false);
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var prefab = root.AddComponent<SaveablePrefab>();
                Set(prefab, "uniqueID", "field-instance");
                Set(prefab, "prefabAssetID", "field-asset");
                prefab.TrackAddedComponents = true;
                prefab.TrackComponentBlobs = false;
                var original = root.AddComponent<AddedComponentFieldsProbe>();
                original.Score = 75;
                original.Direction = new Vector3(0.00001234567f, -1234567.25f, float.Epsilon);
                original.Rotation = Quaternion.Euler(15.123456f, -30.234567f, 45.345678f);
                var reflected = typeof(AddedComponentFieldsProbe).GetField(field);
                var expected = reflected.GetValue(original);
                var bytes = prefab.CaptureRuntimeModifications();
                Object.DestroyImmediate(original);
                prefab.ApplyRuntimeModifications(bytes);
                var restored = root.GetComponent<AddedComponentFieldsProbe>();
                Assert.That(restored, Is.Not.Null);
                Assert.That(reflected.GetValue(restored), Is.EqualTo(expected));
            }
            finally { CultureInfo.CurrentCulture = previousCulture; Object.DestroyImmediate(root); }
        }

        [TestCase("en-US")]
        [TestCase("de-DE")]
        public void AddedComponent_LegacyDisplayStringsRemainReadable(string culture)
        {
            using var settings = new FeatureSettingsScope();
            var previousCulture = CultureInfo.CurrentCulture;
            var root = new GameObject("Legacy added fields");
            root.SetActive(false);
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var prefab = root.AddComponent<SaveablePrefab>();
                prefab.TrackAddedComponents = true;
                prefab.TrackComponentBlobs = false;
                var modifications = AddedFieldsData(new Dictionary<string, string>
                {
                    ["Score"] = "123",
                    ["Direction"] = "(2.25, -3.50, 4.75)",
                    ["Rotation"] = "(0.10000, -0.20000, 0.30000, 0.90000)"
                });
                prefab.ApplyRuntimeModifications(SaveDataSerializer.Instance.Serialize(modifications));
                var restored = root.GetComponent<AddedComponentFieldsProbe>();
                Assert.That(restored.Score, Is.EqualTo(123));
                Assert.That(restored.Direction, Is.EqualTo(new Vector3(2.25f, -3.5f, 4.75f)));
                Assert.That(restored.Rotation, Is.EqualTo(new Quaternion(.1f, -.2f, .3f, .9f)));
            }
            finally { CultureInfo.CurrentCulture = previousCulture; Object.DestroyImmediate(root); }
        }

        internal static RuntimeModificationData AddedFieldsData(Dictionary<string, string> fields)
            => new RuntimeModificationData
            {
                AddedComponents = new List<ComponentModification>
                {
                    new ComponentModification
                    {
                        ComponentTypeName = nameof(AddedComponentFieldsProbe),
                        SerializedData = SaveDataSerializer.Instance.Serialize(fields)
                    }
                }
            };
    }

    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator AddedComponent_InvalidFieldFailsRestoreAndHealthyFieldsContinue()
        {
            var manager = SaveManager.Instance;
            var target = new GameObject("Added field restore failure");
            target.SetActive(false);
            try
            {
                var prefab = target.AddComponent<SaveablePrefab>();
                Set(prefab, "uniqueID", "bad-field-instance");
                Set(prefab, "prefabAssetID", "bad-field-asset");
                prefab.TrackAddedComponents = true;
                prefab.TrackComponentBlobs = false;
                var healthy = target.AddComponent<FeatureProbe>();
                Set(healthy, "uniqueID", prefab.UniqueID);
                healthy.ComponentID = "healthy-field";
                var prefabData = manager.GetPrefabManager.BuildPrefabData(prefab);
                prefabData.RuntimeModificationData = manager.Serializer.Serialize(
                    CrystalSaveAddedComponentRegressionTests.AddedFieldsData(new Dictionary<string, string>
                    {
                        ["Score"] = "91",
                        ["Direction"] = "csav:v3:1,broken,3",
                        ["Rotation"] = "csav:q4:0,0,0,1"
                    }));
                var data = new SaveData(manager.SaveSettings.version);
                data.Prefabs.Add(prefabData);
                data.ComponentsData[healthy.UniqueIdentifier] = BitConverter.GetBytes(99);
                var restore = manager.RestoreSingleGameObjectAsync(target, data);
                yield return Await(restore);
                Assert.That(restore.Result, Is.False);
                var fields = target.GetComponent<AddedComponentFieldsProbe>();
                Assert.That(fields.Score, Is.EqualTo(91));
                Assert.That(fields.Rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(healthy.Value, Is.EqualTo(99));
                Assert.That(fields.Direction, Is.EqualTo(Vector3.zero));
                prefabData.RuntimeModificationData = manager.Serializer.Serialize(
                    CrystalSaveAddedComponentRegressionTests.AddedFieldsData(new Dictionary<string, string>
                    {
                        ["Direction"] = "csav:v3:1.25,2.5,3.75"
                    }));
                var retry = manager.RestoreSingleGameObjectAsync(target, data);
                yield return Await(retry);
                Assert.That(retry.Result, Is.True);
                Assert.That(fields.Direction, Is.EqualTo(new Vector3(1.25f, 2.5f, 3.75f)));
            }
            finally { if (target) Object.Destroy(target); }
        }
    }
}
#endif
