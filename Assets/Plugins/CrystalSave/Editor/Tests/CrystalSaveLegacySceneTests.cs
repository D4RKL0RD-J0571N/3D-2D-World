#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator SceneLoadRemovesOnlyTheIncomingBakedPersistentDuplicate()
        {
            // Author actual scene instances while outside Play Mode. The common
            // fixture owns/restores these scenes, assets and build-list entries.
            yield return new ExitPlayMode();
            var settings = AssetDatabase.LoadAssetAtPath<SaveSettings>(AssetsRoot + "/Settings.asset");
            settings.usePrefabPooling = false;
            EditorUtility.SetDirty(settings);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsRoot + "/Resources/CrystalSaveLifecyclePrefab.prefab");
            var sceneA = EditorSceneManager.OpenScene(AssetsRoot + "/A.unity", OpenSceneMode.Single);
            AddLegacyScenePrefab(asset, sceneA, "PersistentCarrierA", "persistent-carrier-a", true);
            EditorSceneManager.SaveScene(sceneA);
            var sceneB = EditorSceneManager.OpenScene(AssetsRoot + "/B.unity", OpenSceneMode.Single);
            AddLegacyScenePrefab(asset, sceneB, "IncomingPersistentB", "incoming-persistent-b", true);
            AddLegacyScenePrefab(asset, sceneB, "OrdinarySceneB", "ordinary-scene-b", false);
            EditorSceneManager.SaveScene(sceneB);
            EditorSceneManager.OpenScene(AssetsRoot + "/A.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            yield return new RecompileScripts(expectScriptCompilation: false);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True);
            yield return Until(() => SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady, "authored-scene initialization");

            var manager = SaveManager.Instance;
            var carrier = manager.GetPrefabManager.GetSaveablePrefabs().Single(p => p && p.name == "PersistentCarrierA");
            Assert.That(carrier.IsAddedAtRuntime, Is.False);
            Assert.That(Get<string>(carrier, "runtimeOriginSceneName"), Is.EqualTo("A"));
            Assert.That(carrier.gameObject.scene.name, Is.EqualTo("DontDestroyOnLoad"),
                "Origin must be recorded before the persistent-scene move.");
            var runtime = Spawn(true);
            runtime.MarkAsAddedAtRuntime();
            string runtimeId = runtime.UniqueID;
            Assert.That(runtime.PrefabAssetID, Is.EqualTo(carrier.PrefabAssetID));

            yield return LoadScene("B");
            yield return Until(() => !manager.GetPrefabManager.GetSaveablePrefabs()
                .Any(p => p && p.UniqueID == "incoming-persistent-b"), "incoming baked duplicate removal", 5);
            Assert.That(carrier != null && carrier.gameObject.activeSelf, Is.True);
            Assert.That(runtime != null && runtime.gameObject.activeSelf, Is.True);
            Assert.That(runtime.UniqueID, Is.EqualTo(runtimeId));
            Assert.That(runtime.IsAddedAtRuntime, Is.True);
            var remaining = manager.GetPrefabManager.GetSaveablePrefabs();
            var ordinary = remaining.Single(p => p && p.UniqueID == "ordinary-scene-b");
            Assert.That(ordinary.KeepAcrossScenes, Is.False);
            Assert.That(ordinary.gameObject.scene.name, Is.EqualTo("B"));
            Assert.That(remaining.Count(p => p && p.KeepAcrossScenes && !p.IsAddedAtRuntime &&
                p.PrefabAssetID == carrier.PrefabAssetID), Is.EqualTo(1));
            Assert.That(manager.GetPrefabManager.GetInstantiatedPrefabs().ContainsKey("incoming-persistent-b"), Is.False);
        }

        private static void AddLegacyScenePrefab(GameObject asset, Scene scene, string name, string id, bool persistent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            instance.name = name;
            var prefab = instance.GetComponent<SaveablePrefab>();
            Set(prefab, "uniqueID", id);
            prefab.KeepAcrossScenes = persistent;
            instance.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(prefab);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }
    }
}
#endif
