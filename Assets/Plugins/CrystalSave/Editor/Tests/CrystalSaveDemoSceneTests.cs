#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System.Linq;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveDemoSceneTests
    {
        [TestCase("PersistentPrefabsAcrossScenes1")]
        [TestCase("PersistentPrefabsAcrossScenes2")]
        [TestCase("StreamingDeferredPrefabs")]
        [TestCase("DeferredComponentStreaming")]
        [TestCase("CrystalSaveDemo_GameObjectActiveState_SceneA")]
        [TestCase("CrystalSaveDemo_GameObjectActiveState_SceneB")]
        [TestCase("CrystalSaveDemo_AdditiveActiveState_Area")]
        [TestCase("CrystalSaveDemo_AdditiveActiveState_Main")]
        public void DemoSceneImportsWithResolvedScriptsAndStableCompositeIdentity(string name)
        {
            string path="Assets/Plugins/CrystalSave/Modules/Unity/Demos/Scenes/"+name+".unity";
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null);
            var scene=EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var objects=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
                Assert.That(objects,Is.Not.Empty);
                foreach(var go in objects)
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go),Is.Zero,go.name);
                    if(!go.GetComponent<RememberComposite>() || go.GetComponentInParent<SaveablePrefab>(true)) continue;
                    var uid=go.GetComponent<UniqueID>();
                    Assert.That(uid,Is.Not.Null,go.name); Assert.That(uid.ID,Is.Not.Empty,go.name);
                    var helpers=go.GetComponents<SaveableComponent>();
                    Assert.That(helpers.All(c=>!string.IsNullOrEmpty(c.ComponentID)),Is.True,go.name);
                    Assert.That(helpers.Select(c=>c.ComponentID).Distinct().Count(),Is.EqualTo(helpers.Length),go.name);
                }
                if(name=="DeferredComponentStreaming")
                    Assert.That(objects.SelectMany(g=>g.GetComponents<SaveableComponent>()).Any(c=>c.DeferLowPriorityUntilRequested),Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
