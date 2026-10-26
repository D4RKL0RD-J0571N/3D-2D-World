#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object=UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    // These Editor tests enter real Play Mode. Keeping them in the existing predefined
    // Editor assembly avoids changing assembly identities in shipped scenes/prefabs.
    [Category("CrystalSaveLifecycle")]
    public sealed partial class CrystalSaveLifecycleTests
    {
        internal const string AssetsRoot="Assets/__CrystalSaveLifecycleTests";
        internal const string SessionKey="CrystalSave.Tests.LifecycleActive";
        private const string DemoRoot="Assets/Plugins/CrystalSave/Modules/Unity/Demos/Scenes/";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Exiting Play Mode can queue a settings-driven script reload. Drain it
            // before creating fixture assets so it cannot interrupt EnterPlayMode.
            yield return new RecompileScripts(expectScriptCompilation: false);
            Assert.That(AssetDatabase.IsValidFolder(AssetsRoot), Is.False,"Stale fixture directory: inspect it before removing.");
            SessionState.SetString(SessionKey+".buildScenes",JsonUtility.ToJson(new BuildScenesBackup
            {
                Paths=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),
                Enabled=EditorBuildSettings.scenes.Select(s=>s.enabled).ToArray()
            }));
            Directory.CreateDirectory(AssetsRoot+"/Resources"); AssetDatabase.Refresh();
            var settings=ScriptableObject.CreateInstance<SaveSettings>();
            settings.onboardingCompleted=true; settings.editorAutomationSettingsInitialized=true;
            settings.autoRegisterPrefabs=false; settings.enablePrefabRegistryAutoValidation=false;
            settings.enableCloudSave=false; settings.enableScreenshots=false; settings.runPersistentPathMigrationOnStartup=false;
            settings.numberOfSaveSlots=3; settings.numberOfQuickSaveSlots=2; settings.quickSaveSlotOffset=10;
            settings.numberOfAutoSaveSlots=2; settings.autoSaveSlotOffset=20;
            settings.persistentPath.mode=PersistentPathMode.Custom;
            settings.persistentPath.nonWebGLOutputRoot=Path.Combine(Path.GetTempPath(),"CrystalSaveTests",Guid.NewGuid().ToString("N"));
            settings.persistentPath.customFolderName="Saves";
            SessionState.SetString(SessionKey+".root",settings.persistentPath.nonWebGLOutputRoot);
            AssetDatabase.CreateAsset(settings,AssetsRoot+"/Settings.asset");
            var texture=new Texture2D(2,2) { name="CrystalSaveLifecycleTexture" };
            AssetDatabase.CreateAsset(texture,AssetsRoot+"/Resources/CrystalSaveLifecycleTexture.asset");
            var registry=ScriptableObject.CreateInstance<PrefabRegistry>();
            AssetDatabase.CreateAsset(registry,AssetsRoot+"/PrefabRegistry.asset");
            var overrides=ScriptableObject.CreateInstance<CrystalSaveAssetOverrides>(); overrides.saveSettings=settings; overrides.prefabRegistry=registry;
            overrides.sceneObjectRegistry=ScriptableObject.CreateInstance<SceneObjectRegistry>();
            overrides.migrationManager=ScriptableObject.CreateInstance<MigrationManager>();
            overrides.tagRegistry=ScriptableObject.CreateInstance<TagRegistry>();
            AssetDatabase.CreateAsset(overrides.tagRegistry,AssetsRoot+"/TagRegistry.asset");
            AssetDatabase.CreateAsset(overrides.sceneObjectRegistry,AssetsRoot+"/SceneObjectRegistry.asset");
            AssetDatabase.CreateAsset(overrides.migrationManager,AssetsRoot+"/MigrationManager.asset");
            AssetDatabase.CreateAsset(overrides,AssetsRoot+"/Resources/CrystalSaveLifecycleOverrides.asset");
            var temp=new GameObject("LifecyclePrefab"); temp.SetActive(false);
            var sp=temp.AddComponent<SaveablePrefab>(); Set(sp,"prefabAssetID","lifecycle-prefab"); sp.TrackComponentBlobs=true;
            var child=new GameObject("State"); child.transform.SetParent(temp.transform);
            Composite(child,typeof(RememberTransform)); ConfigureTransform(child);
            var prefab=PrefabUtility.SaveAsPrefabAsset(temp,AssetsRoot+"/Resources/CrystalSaveLifecyclePrefab.prefab");
            Object.DestroyImmediate(temp);
            registry.TryAddPrefab(prefab.GetComponent<SaveablePrefab>().PrefabAssetID,prefab,out _); registry.RebuildLookup();
            // Isolated, empty scenes exercise real scene unload/activation with a temporary, restored build list.
            for(int i=0;i<2;i++)
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene,AssetsRoot+(i==0 ? "/A.unity" : "/B.unity"));
            }
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]
            {
                new EditorBuildSettingsScene(AssetsRoot+"/A.unity",true),
                new EditorBuildSettingsScene(AssetsRoot+"/B.unity",true)
            }).ToArray();
            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey,true);
            yield return new EnterPlayMode();
            Assert.That(Application.isPlaying, Is.True, "Lifecycle fixture must enter Play Mode before manager initialization.");
            yield return Until(()=>SaveManager.IsInitialized && SaveManager.AreSaveSlotsReady,"SaveManager initialization");
            Assert.That(SaveManager.Instance.SaveSettings.persistentPath.mode, Is.EqualTo(PersistentPathMode.Custom));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(Application.isPlaying) yield return new ExitPlayMode();
            SessionState.SetBool(SessionKey,false);
            string buildScenes=SessionState.GetString(SessionKey+".buildScenes","");
            if(!string.IsNullOrEmpty(buildScenes))
            {
                var backup=JsonUtility.FromJson<BuildScenesBackup>(buildScenes);
                EditorBuildSettings.scenes=backup.Paths.Select((p,i)=>new EditorBuildSettingsScene(p,backup.Enabled[i])).ToArray();
                SessionState.EraseString(SessionKey+".buildScenes");
            }
            string root=SessionState.GetString(SessionKey+".root","");
            if(!string.IsNullOrEmpty(root) && Directory.Exists(root)) Directory.Delete(root,true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            AssetDatabase.DeleteAsset(AssetsRoot);
            typeof(CrystalSaveOverrides).GetField("cached",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            CrystalSaveEditorAutomationSettings.ClearCache();
        }

        [UnityTest]
        public IEnumerator CloudOnlyLegacyDeleteDoesNotBlockDispatcherAndAsyncApiCompletes()
        {
            var settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = true; settings.keepLocalMirror = false; settings.enableScreenshots = false;
            settings.saveKey = "CrystalSaveDeleteTest_" + Guid.NewGuid().ToString("N") + "_{n}";
            string key = settings.saveKey.Replace("{n}", "3");
            string root = SessionState.GetString(SessionKey + ".root", "");
            try
            {
                var storage = new SaveSystem(settings, root);
                // Empty metadata skips remote requests, but still exercises the same dispatcher cleanup.
                var slot = new SaveSlot(3, "Empty", default, "", "");
                PlayerPrefs.SetString(key, "AQ==");
                storage.Delete(slot);
                yield return Until(() => !PlayerPrefs.HasKey(key), "scheduled cloud-only deletion", 5);
                PlayerPrefs.SetString(key, "Ag==");
                yield return Await(storage.DeleteAsync(slot));
                Assert.That(PlayerPrefs.HasKey(key), Is.False);
            }
            finally { PlayerPrefs.DeleteKey(key); Object.Destroy(settings); }
        }

        [UnityTest]
        public IEnumerator VerificationBackupFailurePreservesSaveAcrossAllSaveEntryPoints()
        {
            var sm = SaveManager.Instance;
            var prefab = Spawn();
            yield return Await(sm.SaveAsync(1));
            string directory = SessionState.GetString(SessionKey + ".root", "");
            string path = Directory.GetFiles(directory, "*.sav", SearchOption.AllDirectories).Single();
            byte[] original = File.ReadAllBytes(path);
            prefab.transform.position = new Vector3(88, 77, 66);
            sm.SaveSettings.enableSaveFileVerification = true;
            Directory.CreateDirectory(path + ".bak"); // Force backup commit failure without changing permissions.
            int backupFailures = 0, saveFailures = 0;
            EventHandler<OperationFailedEventArgs> onBackup = (_, __) => backupFailures++;
            EventHandler<OperationFailedEventArgs> onFailure = (_, __) => saveFailures++;
            sm.OnBackupFailed += onBackup; sm.OnSaveFailed += onFailure;
            try
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("HandleSaveCompletion: Save operation for slot 1 failed with error: Save backup failed:"));
                    Task save = mode == 0 ? sm.SaveAsync(1) : mode == 1 ? sm.SaveImmediateAsync(1) : sm.SaveSlotForTagAsync(1, "Untagged");
                    yield return Until(() => save.IsCompleted && saveFailures == mode + 1, "failed backup completion");
                    if (save.IsFaulted) _ = save.Exception;
                    Assert.That(backupFailures, Is.EqualTo(mode + 1));
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
                }
            }
            finally
            {
                sm.OnBackupFailed -= onBackup; sm.OnSaveFailed -= onFailure;
                Directory.Delete(path + ".bak");
            }
        }

        [UnityTest]
        public IEnumerator AwaitableManagerDeleteCompletesAfterDataAndEvents()
        {
            var sm = SaveManager.Instance;
            yield return Await(sm.SaveAsync(1));
            int completions = 0;
            EventHandler<SaveManagerEventArgs> handler = (_, __) => completions++;
            sm.OnDeleteCompleted += handler;
            try
            {
                yield return Await(sm.DeleteAsync(1));
                Assert.That(completions, Is.EqualTo(1));
                var exists = sm.HasSaveAtAsync(1, false);
                yield return Await(exists);
                Assert.That(exists.Result, Is.False);
            }
            finally { sm.OnDeleteCompleted -= handler; }
        }

        [Serializable] private sealed class BuildScenesBackup { public string[] Paths; public bool[] Enabled; }

        internal static IEnumerator Until(Func<bool> condition,string description,float seconds=30)
        {
            float deadline=Time.realtimeSinceStartup+seconds;
            while(!condition() && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(condition(),Is.True,"Timed out: "+description);
        }
        private static IEnumerator Await(Task task)
        {
            yield return Until(()=>task.IsCompleted,"async save/load");
            if(task.IsFaulted) throw task.Exception;
            Assert.That(task.IsCanceled,Is.False);
        }
        private static IEnumerator LoadScene(string name,LoadSceneMode mode=LoadSceneMode.Single)
        {
            var operation=EditorSceneManager.LoadSceneAsyncInPlayMode(AssetsRoot+"/"+name+".unity",new LoadSceneParameters(mode));
            yield return Until(()=>operation.isDone,"scene load "+name);
            Assert.That(SceneManager.GetSceneByPath(AssetsRoot+"/"+name+".unity").isLoaded,Is.True);
            yield return null;
        }
        private static SaveablePrefab Spawn(bool keepAcrossScenes=false)
        {
            var go=Object.Instantiate(Resources.Load<GameObject>("CrystalSaveLifecyclePrefab"));
            go.GetComponent<SaveablePrefab>().KeepAcrossScenes=keepAcrossScenes; go.SetActive(true);
            return go.GetComponent<SaveablePrefab>();
        }
        private static RememberTransform SceneComponent(string name,bool unregister=false,bool keepAcrossScenes=false)
        {
            var go=new GameObject(name); go.SetActive(false);
            Composite(go,typeof(RememberTransform)); var c=ConfigureTransform(go); Set(c,"unregisterOnDisable",unregister); c.KeepAcrossScenes=keepAcrossScenes;
            go.SetActive(true); return c;
        }

        [UnityTest]
        public IEnumerator DiskSaveLoadRestoresComponentAndPrefabBlobThroughAllManagers()
        {
            yield return LoadScene("A"); var sm=SaveManager.Instance;
            var scene=SceneComponent("SceneState"); scene.transform.position=new Vector3(4,5,6);
            var prefab=Spawn(); prefab.transform.position=new Vector3(10,0,0);
            var helper=prefab.GetComponentInChildren<RememberTransform>(); helper.transform.localPosition=new Vector3(2,3,4);
            yield return null;
            string id=prefab.UniqueID;
            yield return Await(sm.SaveAsync(1));
            var stored=sm.LoadSaveDataForSlotAsync(1); yield return Await(stored);
            Assert.That(stored.Result.ComponentsData.ContainsKey(scene.UniqueIdentifier),Is.True);
            Assert.That(stored.Result.ComponentsData.ContainsKey(helper.UniqueIdentifier),Is.False,"Prefab blobs must have one save route.");
            Assert.That(stored.Result.Prefabs.Any(p=>p.InstanceID==id),Is.True);
            scene.transform.position=Vector3.zero; helper.transform.localPosition=Vector3.zero; prefab.transform.position=Vector3.zero;
            var load=sm.LoadSaveSlotAsync(1,restoreLastActiveScene:false); yield return Await(load);
            Assert.That(load.Result.Success,Is.True,load.Result.ErrorMessage); yield return null;
            Assert.That(scene.transform.position,Is.EqualTo(new Vector3(4,5,6)));
            var restored=sm.FindGameObjectByUniqueID(id);
            Assert.That(restored,Is.Not.Null); Assert.That(restored.transform.position,Is.EqualTo(new Vector3(10,0,0)));
            Assert.That(restored.GetComponentInChildren<RememberTransform>(true).transform.localPosition,Is.EqualTo(new Vector3(2,3,4)));
        }

        [UnityTest]
        public IEnumerator UnregisterOnDisableControlsWhetherInactiveChangesAreSaved()
        {
            yield return LoadScene("A"); var sm=SaveManager.Instance;
            var keep=SceneComponent("KeepSaving",false); var remove=SceneComponent("Unregister",true); yield return null;
            keep.gameObject.SetActive(false); remove.gameObject.SetActive(false);
            keep.transform.position=Vector3.one*7; remove.transform.position=Vector3.one*8;
            Assert.That(sm.ComponentManager.Contains(keep),Is.True); Assert.That(sm.ComponentManager.Contains(remove),Is.False);
            var data=new SaveData(); sm.ComponentManager.CollectComponentData(data);
            Assert.That(data.ComponentsData.ContainsKey(keep.UniqueIdentifier),Is.True);
            Assert.That(data.ComponentsData.ContainsKey(remove.UniqueIdentifier),Is.False);
            keep.gameObject.SetActive(true); remove.gameObject.SetActive(true); yield return null;
            Assert.That(sm.ComponentManager.Contains(remove),Is.True);
        }

        [UnityTest]
        public IEnumerator DeferredComponentDataWaitsForExplicitStreamingRequest()
        {
            yield return LoadScene("A"); var c=SceneComponent("Streamed"); c.DeferLowPriorityUntilRequested=true;
            c.transform.position=Vector3.one*6; yield return null;
            var sm=SaveManager.Instance; yield return Await(sm.SaveAsync(1)); c.transform.position=Vector3.zero;
            var load=sm.LoadSaveSlotAsync(1,restoreLastActiveScene:false); yield return Await(load); yield return null;
            Assert.That(load.Result.Success,Is.True,load.Result.ErrorMessage); Assert.That(c.transform.position,Is.EqualTo(Vector3.zero));
            sm.ComponentManager.ProcessDeferredComponent(c.UniqueIdentifier); yield return null;
            Assert.That(c.transform.position,Is.EqualTo(Vector3.one*6));
        }

        [UnityTest]
        public IEnumerator DeferredPrefabIsInstantiatedOnlyAfterRequest()
        {
            yield return LoadScene("A"); var sm=SaveManager.Instance; var p=Spawn(); p.DeferLowPriorityUntilRequested=true;
            p.transform.position=Vector3.one*5; string id=p.UniqueID; yield return null;
            yield return Await(sm.SaveAsync(1)); Object.Destroy(p.gameObject); yield return null;
            var load=sm.LoadSaveSlotAsync(1,restoreLastActiveScene:false); yield return Await(load);
            Assert.That(load.Result.Success,Is.True,load.Result.ErrorMessage); Assert.That(sm.GetPrefabManager.HasDeferredPrefabs,Is.True);
            Assert.That(sm.FindGameObjectByUniqueID(id),Is.Null);
            yield return sm.GetPrefabManager.ProcessDeferredPrefabByUniqueID(id);
            Assert.That(sm.FindGameObjectByUniqueID(id),Is.Not.Null);
            Assert.That(sm.FindGameObjectByUniqueID(id).transform.position,Is.EqualTo(Vector3.one*5));
        }

        [UnityTest]
        public IEnumerator KeepAcrossScenesPreservesPrefabAndSceneComponentOnSingleSceneLoad()
        {
            yield return LoadScene("A"); var p=Spawn(true);
            var c=SceneComponent("Persistent",keepAcrossScenes:true);
            string id=p.UniqueID;
            yield return LoadScene("B");
            Assert.That(p,Is.Not.Null); Assert.That(p.UniqueID,Is.EqualTo(id));
            Assert.That(c,Is.Not.Null); Assert.That(SaveManager.Instance,Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator AdditiveDemoActiveStatesSurviveUnloadReloadAndDiskRestore()
        {
            yield return LoadScene("A");
            string path=DemoRoot+"CrystalSaveDemo_AdditiveActiveState_Area.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Additive));
            var area=SceneManager.GetSceneByPath(path);
            var objects=area.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RememberGameObject>(true)).ToArray();
            Assert.That(objects.Length,Is.GreaterThanOrEqualTo(3));
            var states=new System.Collections.Generic.Dictionary<string,bool>();
            for(int i=0;i<objects.Length;i++)
            {
                bool active=i%2==0; objects[i].gameObject.SetActive(active);
                states[objects[i].GameObjectUniqueID]=active;
            }
            yield return null;
            var sm=SaveManager.Instance; yield return Await(sm.SaveAsync(1));
            yield return SceneManager.UnloadSceneAsync(area);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Additive));
            var load=sm.LoadSaveSlotAsync(1,restoreLastActiveScene:false); yield return Await(load); yield return null;
            Assert.That(load.Result.Success,Is.True,load.Result.ErrorMessage);
            area=SceneManager.GetSceneByPath(path);
            foreach(var c in area.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RememberGameObject>(true)))
                Assert.That(c.gameObject.activeSelf,Is.EqualTo(states[c.GameObjectUniqueID]),c.name);
        }

        [UnityTest]
        public IEnumerator HomeSceneSnapshotAndSharedStateRestoreMatchingReplacement()
        {
            yield return LoadScene("A");
            var source=SceneComponent("HomeState"); source.RememberHomeScene=true; source.HomeScene="A";
            source.ShareStateAcrossScenes=true; source.transform.position=Vector3.one*13;
            string objectID=source.GameObjectUniqueID; string componentID=source.ComponentID;
            SaveManager.Instance.ComponentManager.SnapshotObjectToSceneCache(source.gameObject);
            Object.Destroy(source.gameObject); yield return null;
            var go=new GameObject("Replacement"); go.SetActive(false);
            var uid=go.AddComponent<UniqueID>(); uid.ID=objectID;
            Composite(go,typeof(RememberTransform)); var replacement=ConfigureTransform(go);
            replacement.ComponentID=componentID; replacement.RememberHomeScene=true; replacement.HomeScene="A";
            replacement.ShareStateAcrossScenes=true; go.SetActive(true); yield return null;
            Assert.That(replacement.transform.position,Is.EqualTo(Vector3.one*13));
        }

        [UnityTest]
        public IEnumerator HomeSceneModesKeepOriginalOrFollowSnapshotScene()
        {
            yield return LoadScene("A");
            var manager=SaveManager.Instance;
            foreach(var mode in new[]{HomeSceneMode.DesignScene,HomeSceneMode.LastSnapshotScene})
            {
                var component=SceneComponent("HomeMode"+mode);
                component.HomeSceneMode=mode; component.HomeScene="Original"; component.RememberHomeScene=true;
                manager.ComponentManager.SnapshotObjectToSceneCache(component.gameObject);
                Assert.That(component.HomeScene,Is.EqualTo(mode==HomeSceneMode.DesignScene ? "Original" : "A"));
            }
            foreach(var mode in new[]{SaveablePrefab.HomeSceneMode.InstantiationScene,SaveablePrefab.HomeSceneMode.LastSnapshotScene})
            {
                var prefab=Spawn(); prefab.HomeSceneCaptureMode=mode;
                prefab.SetHomeScene("Original"); prefab.RememberHomeScene=true;
                var snapshot=manager.GetPrefabManager.BuildPrefabData(prefab);
                Assert.That(snapshot.HomeScene,Is.EqualTo(mode==SaveablePrefab.HomeSceneMode.InstantiationScene ? "Original" : "A"));
                Assert.That(prefab.HomeScene,Is.EqualTo(snapshot.HomeScene));
            }
        }

        [UnityTest]
        public IEnumerator TextureAssetOverridesRoundTripRuntimeMaterialInstances()
        {
            yield return LoadScene("A");
            foreach(bool enabled in new[]{false,true})
            {
                var prefab=Spawn(); prefab.TrackTextureOverrides=enabled;
                var renderer=prefab.gameObject.AddComponent<MeshRenderer>();
                var material=new Material(Shader.Find("UI/Default"));
                var texture=Resources.Load<Texture2D>("CrystalSaveLifecycleTexture");
                Assert.That(texture,Is.Not.Null); material.SetTexture("_MainTex",texture); renderer.sharedMaterial=material;
                try
                {
                    var bytes=prefab.CaptureRuntimeModifications();
                    renderer.material.SetTexture("_MainTex",null);
                    prefab.ApplyRuntimeModifications(bytes);
                    Assert.That(renderer.material.GetTexture("_MainTex"),Is.EqualTo(enabled ? texture : null));
                }
                finally
                {
                    if(renderer.sharedMaterial!=material) Object.Destroy(renderer.sharedMaterial);
                    Object.Destroy(material); Object.Destroy(prefab.gameObject);
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PlayerPrefsSyncAndAsyncApisShareKeysAndReadLegacySaves()
        {
            var settings=Object.Instantiate(SaveManager.Instance.SaveSettings);
            settings.saveMethod=SaveMethod.PlayerPrefs;
            settings.saveKey="CrystalSaveTests_"+Guid.NewGuid().ToString("N")+"_{n}";
            string root=Path.Combine(Path.GetTempPath(),"CrystalSaveTests",Guid.NewGuid().ToString("N"));
            string current=settings.saveKey.Replace("{n}","1"), legacy=settings.saveKey+"1";
            var slot=new SaveSlot(1,"Interop",DateTime.UtcNow,"","");
            var storage=new SaveSystem(settings,root);
            try
            {
                storage.Save(new byte[]{1,2},slot);
                var read=storage.LoadAsync(slot); yield return Await(read);
                Assert.That(read.Result,Is.EqualTo(new byte[]{1,2}));
                yield return Await(storage.SaveAsync(new byte[]{3,4},slot));
                Assert.That(storage.Load(slot),Is.EqualTo(new byte[]{3,4}));
                PlayerPrefs.DeleteKey(current); PlayerPrefs.SetString(legacy,Convert.ToBase64String(new byte[]{5}));
                read=storage.LoadAsync(slot); yield return Await(read);
                Assert.That(read.Result,Is.EqualTo(new byte[]{5}));
                yield return Await(storage.DeleteAsync(slot));
                Assert.That(PlayerPrefs.HasKey(current) || PlayerPrefs.HasKey(legacy),Is.False);
            }
            finally
            {
                PlayerPrefs.DeleteKey(current); PlayerPrefs.DeleteKey(legacy);
                PlayerPrefs.DeleteKey(settings.saveKey+"_Metadata_1"); PlayerPrefs.Save();
                Object.Destroy(settings); if(Directory.Exists(root)) Directory.Delete(root,true);
            }
        }

        [UnityTest]
        public IEnumerator QuickAndAutoSaveSlotsRotateWithoutOverwritingManualSlot()
        {
            yield return LoadScene("A"); var sm=SaveManager.Instance; var c=SceneComponent("Slots"); yield return null;
            var original=c.SaveData();
            yield return Await(sm.SaveAsync(1));
            yield return Await(sm.QuickSaveAsync()); c.transform.position=Vector3.one;
            var second=c.SaveData();
            yield return Await(sm.QuickSaveAsync()); yield return Await(sm.AutoSaveAsync());
            c.transform.position=Vector3.one*2; var third=c.SaveData();
            yield return Await(sm.AutoSaveAsync());
            yield return Await(sm.ForceRefreshSlotsAsync());
            Assert.That(sm.GetSaveSlots().Select(s=>s.SlotNumber).OrderBy(n=>n),
                Is.EqualTo(new[]{1,2,3,11,12,21,22}),"Refresh preserves sparse slot numbers.");
            foreach(var expected in new[]{(1,original),(12,original),(11,second),(22,second),(21,third)})
            {
                Assert.That(sm.HasSaveAt(expected.Item1,false),Is.True);
                var saved=sm.LoadSaveDataForSlotAsync(expected.Item1); yield return Await(saved);
                Assert.That(saved.Result.ComponentsData[c.UniqueIdentifier],Is.EqualTo(expected.Item2),"Slot "+expected.Item1);
            }
        }
    }

    internal static class LifecycleTestBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void ConfigureBeforeSaveManager()
        {
            if(!SessionState.GetBool(CrystalSaveLifecycleTests.SessionKey,false)) return;
            var asset=Resources.Load<CrystalSaveAssetOverrides>("CrystalSaveLifecycleOverrides");
            typeof(CrystalSaveOverrides).GetField("cached",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,asset);
        }
    }
}
#endif
