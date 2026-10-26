#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demos
{
    /// <summary>
    /// Runtime controller for the "Additive Scenes &amp; Runtime Prefabs" one-click demo.
    /// Implements <see cref="ISceneLoadOrchestrator"/> so every additive transition runs
    /// through the documented lifecycle:
    /// OnScenePreLoad -> OnSceneWillActivate -> OnSceneActivated -> OnSceneUnloaded.
    /// Mirrors the AdditiveAreaLoader pattern in the GitBook guide
    /// "additive-scenes-and-runtime-prefabs.md".
    /// </summary>
    public class AdditiveScenesDemoController : MonoBehaviour, ISceneLoadOrchestrator
    {
        [Header("Save slot")]
        public int slot = 1;

        [Header("Runtime prefab")]
        public GameObject runtimePrefab;
        public Transform spawnAnchor;
        public float spawnRadius = 2.5f;

        [Header("UI")]
        public Text statusText;

        [Header("Additive area")]
        public string areaSceneName = "Island";
        public float spawnYOffset = 0.5f;

        private Scene _areaScene;
        private bool _registered;

        private void Awake() => TryRegisterOrchestrator();
        private void OnEnable() => TryRegisterOrchestrator();

        private void OnDisable()
        {
            if (_registered && SaveManager.Instance != null)
            {
                SaveManager.Instance.UnregisterSceneLoadOrchestrator(this);
                _registered = false;
            }
        }

        private void Update() => RefreshStatus();

        private void TryRegisterOrchestrator()
        {
            if (_registered) return;
            if (SaveManager.Instance == null) return;
            SaveManager.Instance.RegisterSceneLoadOrchestrator(this);
            _registered = true;
        }

        public async Task OnScenePreLoad(string sceneName)
        {
            if (SaveManager.Instance == null) return;
            Log($"[Hook] OnScenePreLoad('{sceneName}') - populating pending prefabs from slot {slot}.");
            await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(slot);
        }

        public Task OnSceneWillActivate(Scene scene) => Task.CompletedTask;

        public void OnSceneActivated(Scene scene)
        {
            if (scene.name != areaSceneName) return;
            _areaScene = scene;
            SetActiveSceneWithoutPoolDisposal(scene);
            Log($"[Hook] OnSceneActivated('{scene.name}').");
        }

        public void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == areaSceneName)
                Log($"[Hook] OnSceneUnloaded('{scene.name}').");
        }

        public async void OnClickSave()         => await SaveAsync();
        public async void OnClickLoad()         => await LoadAsync();
        public async void OnClickEnterArea()    => await LoadAreaAsync(areaSceneName);
        public async void OnClickExitArea()     => await UnloadAreaAsync(areaSceneName);
        public async void OnClickReturnToArea() => await LoadAreaAsync(areaSceneName);
        public void      OnClickSpawnCube()     => SpawnRuntimePrefab();
        public void      OnClickDeleteSave()    => DeleteSave();

        public async Task LoadAreaAsync(string sceneName)
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }

            // NOTE: We intentionally do NOT SaveAsync here. On "Return To Area"
            // the area scene is already unloaded, so saving would overwrite the
            // slot with empty state and the runtime cube would be lost.
            // Use the Exit button (which saves before unloading) or the Save
            // button to explicitly capture state before entering.

            var existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                OnSceneActivated(existing);
                return;
            }

            await OnScenePreLoad(sceneName);

            Scene loaded = SceneManager.GetSceneByName(sceneName);
            if (!loaded.IsValid())
            {
                for (int i = 0; i < 10; i++)
                {
                    loaded = SceneManager.GetSceneByName(sceneName);
                    if (loaded.IsValid()) break;
                    await Task.Yield();
                }
            }

            if (!loaded.IsValid())
            {
                Log($"LoadArea: loading '{sceneName}' additively...");
                var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Log($"Scene '{sceneName}' is not in Build Settings.");
                    return;
                }
                while (!op.isDone) await Task.Yield();
                loaded = SceneManager.GetSceneByName(sceneName);
            }
            else
            {
                while (!loaded.isLoaded) await Task.Yield();
            }

            await OnSceneWillActivate(loaded);
            OnSceneActivated(loaded);

            // Yield a frame and re-assert active scene. On sessions with an existing
            // save, populate's async RememberScenes work can briefly leave the boot
            // scene active after our hook returns — re-asserting here guarantees
            // Spawn Cube lands in Island.
            await Task.Yield();
            if (loaded.IsValid() && loaded.isLoaded && SceneManager.GetActiveScene() != loaded)
                SetActiveSceneWithoutPoolDisposal(loaded);

            Log($"Entered '{sceneName}'. Runtime prefabs should be restored.");
        }

        public async Task UnloadAreaAsync(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Log($"'{sceneName}' is not loaded - nothing to exit.");
                return;
            }

            Log($"UnloadArea('{sceneName}'): saving slot {slot} so prefabs survive...");
            await SaveManager.Instance.SaveAsync(slot);

            Log($"UnloadArea: unloading '{sceneName}'...");
            var op = SceneManager.UnloadSceneAsync(scene);
            while (op != null && !op.isDone) await Task.Yield();

            OnSceneUnloaded(scene);
            Log($"Exited '{sceneName}'. Runtime prefabs are preserved in slot {slot}.");
        }

        private async Task SaveAsync()
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }
            Log($"Saving slot {slot}...");
            await SaveManager.Instance.SaveAsync(slot);
            Log($"Saved slot {slot}.");
        }

        private Task LoadAsync()
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return Task.CompletedTask; }
            Log($"Loading slot {slot}...");
            SaveManager.Instance.Load(slot);
            Log($"Load requested for slot {slot}.");
            return Task.CompletedTask;
        }

        private void DeleteSave()
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }
            Log($"Deleting slot {slot}...");
            SaveManager.Instance.Delete(slot);
            Log($"Delete requested for slot {slot}. Enter Area will now start empty.");
        }

        private void SpawnRuntimePrefab()
        {
            if (runtimePrefab == null) { Log("No runtimePrefab assigned."); return; }
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }

            // Prefer the additive area scene so cubes always land in Island — even on
            // the second play session where populate's async load can race our
            // OnSceneActivated hook and leave the boot scene as 'active'.
            var target = SceneManager.GetSceneByName(areaSceneName);
            if (target.IsValid() && target.isLoaded)
            {
                if (SceneManager.GetActiveScene() != target)
                    SetActiveSceneWithoutPoolDisposal(target);
            }
            else
            {
                Log($"'{areaSceneName}' is not loaded — click Enter Area first.");
                return;
            }

            var origin = spawnAnchor ? spawnAnchor.position : Vector3.zero;
            var offset = new Vector3(Random.Range(-spawnRadius, spawnRadius), spawnYOffset,
                                     Random.Range(-spawnRadius, spawnRadius));

            var spawned = SaveablePrefabFactory.Instantiate(
                runtimePrefab,
                origin + offset,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                sceneName: target.name);

            Log(spawned != null
                ? $"Spawned '{spawned.name}' into '{target.name}'."
                : "Spawn failed - see Console.");
        }

        private void RefreshStatus()
        {
            if (statusText == null) return;
            var active = SceneManager.GetActiveScene();
            var area = SceneManager.GetSceneByName(areaSceneName);
            statusText.text =
                $"Active scene : {active.name}\n" +
                $"Loaded scenes: {SceneManager.sceneCount}\n" +
                $"'{areaSceneName}' loaded: {(area.IsValid() && area.isLoaded ? "yes" : "no")}\n" +
                $"Slot         : {slot}\n" +
                $"Orchestrator : {(_registered ? "registered" : "pending")}";
        }

        private void Log(string msg)
        {
            Debug.Log($"[AdditiveScenesDemo] {msg}");
            if (statusText != null) statusText.text = msg;
        }

        private static void SetActiveSceneWithoutPoolDisposal(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            using (SaveablePrefabPoolCache.SuppressDisposal())
                SceneManager.SetActiveScene(scene);
        }
    }
}
#endif
