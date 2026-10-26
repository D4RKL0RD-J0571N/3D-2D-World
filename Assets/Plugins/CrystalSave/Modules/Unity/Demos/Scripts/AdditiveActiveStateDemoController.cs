#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Arawn.CrystalSave.Runtime;

namespace Arawn.CrystalSave.Demo
{
    /// <summary>
    /// Runtime controller for the "Additive Active State" demo.
    ///
    /// The boot/main scene stays loaded for the entire session. An area scene
    /// is loaded additively and unloaded again on demand. The cubes inside the
    /// area scene each carry a <see cref="RememberComposite"/> wrapping a
    /// <see cref="RememberGameObject"/> configured with <c>RememberHomeScene =
    /// true</c> (DesignScene mode, home = the area scene) and
    /// <c>ShareStateAcrossScenes = true</c>. Crystal Save snapshots their
    /// active state when the area is unloaded and re-applies it when the area
    /// is re-loaded — without a slot save. Pressing Save / Load also
    /// demonstrates disk persistence: the area scene itself is recorded in the
    /// slot and re-loaded by <see cref="SaveManager.Load"/>.
    ///
    /// Implements <see cref="ISceneLoadOrchestrator"/> to mirror the pattern
    /// used by <c>AdditiveScenesDemoController</c>: every additive transition
    /// runs through OnScenePreLoad → OnSceneWillActivate → OnSceneActivated
    /// → OnSceneUnloaded. Note that Crystal Save's internal scene-loading
    /// paths (e.g. SaveManager.Load with RememberScenes) do NOT invoke these
    /// hooks — they fire only for additive load/unload operations driven by
    /// this controller, so the slot-load post-refresh is handled separately
    /// via SaveManager.OnLoadCompleted.
    /// </summary>
    public class AdditiveActiveStateDemoController : MonoBehaviour, ISceneLoadOrchestrator
    {
        [Header("Save slot")]
        public int slot = 1;

        [Header("Additive area")]
        public string areaSceneName = "CrystalSaveDemo_AdditiveActiveState_Area";

        [Header("Boot/main scene (the one that stays loaded)")]
        [Tooltip("Captured automatically in Awake before DontDestroyOnLoad. Set explicitly if you need to override it.")]
        public string mainSceneName = "CrystalSaveDemo_AdditiveActiveState_Main";

        [Header("Cube names to toggle (resolved by name in the area scene)")]
        public List<string> cubeNames = new List<string>
        {
            "CubeA_StartsEnabled",
            "CubeB_StartsDisabled",
            "CubeC_StartsEnabled",
        };

        [Header("UI")]
        public Text statusText;
        public Button enterButton;
        public Button exitButton;
        public Button saveButton;
        public Button loadButton;
        public Button deleteButton;
        public List<Button> toggleButtons = new List<Button>();
        public List<Text>   toggleLabels  = new List<Text>();

        private bool _busy;
        private SaveManager _subscribedSaveManager;
        private bool _orchestratorRegistered;

        private void Awake()
        {
            // CRITICAL: capture the boot scene name BEFORE DontDestroyOnLoad.
            // After DontDestroyOnLoad runs, gameObject.scene is the internal
            // "DontDestroyOnLoad" scene, which Unity refuses to make active
            // (SetActiveScene throws ArgumentException for it).
            if (gameObject.scene.IsValid() && !string.IsNullOrEmpty(gameObject.scene.name))
                mainSceneName = gameObject.scene.name;

            DontDestroyOnLoad(gameObject);
            WireButtons();
            TryRegisterOrchestrator();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded   += OnSceneLoaded;
            SceneManager.sceneUnloaded += UnitySceneUnloaded;
            TryRegisterOrchestrator();
            TrySubscribeSaveManager();
            RefreshUI();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= UnitySceneUnloaded;
            UnsubscribeSaveManager();
            if (_orchestratorRegistered && SaveManager.Instance != null)
            {
                SaveManager.Instance.UnregisterSceneLoadOrchestrator(this);
                _orchestratorRegistered = false;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            // SaveManager may not have been around in Awake (RuntimeInit order),
            // and slot-loads spawn one when needed. Try again on every scene load.
            TryRegisterOrchestrator();
            TrySubscribeSaveManager();
            if (scene.name == areaSceneName) RefreshUI();
        }

        // Renamed to avoid collision with ISceneLoadOrchestrator.OnSceneUnloaded.
        private void UnitySceneUnloaded(Scene scene) { if (scene.name == areaSceneName) RefreshUI(); }

        private void TryRegisterOrchestrator()
        {
            if (_orchestratorRegistered) return;
            if (SaveManager.Instance == null) return;
            SaveManager.Instance.RegisterSceneLoadOrchestrator(this);
            _orchestratorRegistered = true;
        }

        private void TrySubscribeSaveManager()
        {
            var sm = SaveManager.Instance;
            if (sm == null || sm == _subscribedSaveManager) return;
            UnsubscribeSaveManager();
            sm.OnLoadCompleted += OnSaveManagerLoadCompleted;
            _subscribedSaveManager = sm;
        }

        private void UnsubscribeSaveManager()
        {
            if (_subscribedSaveManager == null) return;
            _subscribedSaveManager.OnLoadCompleted -= OnSaveManagerLoadCompleted;
            _subscribedSaveManager = null;
        }

        private void OnSaveManagerLoadCompleted(object sender, SaveLoadEventArgs e)
        {
            // SaveManager.Load restores the area scene (RememberScenes) and
            // then applies GameObject active states. Refresh on the next frame
            // so all post-load state-application coroutines have run.
            StartCoroutine(RefreshNextFrame());
        }

        private System.Collections.IEnumerator RefreshNextFrame()
        {
            yield return null;
            yield return null; // double-yield: state-apply coroutines may chain one frame
            RefreshUI();
        }

        private void WireButtons()
        {
            if (enterButton  != null) enterButton.onClick.AddListener(OnClickEnter);
            if (exitButton   != null) exitButton.onClick.AddListener(OnClickExit);
            if (saveButton   != null) saveButton.onClick.AddListener(OnClickSave);
            if (loadButton   != null) loadButton.onClick.AddListener(OnClickLoad);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnClickDelete);

            for (int i = 0; i < toggleButtons.Count; i++)
            {
                int captured = i;
                if (toggleButtons[i] != null)
                    toggleButtons[i].onClick.AddListener(() => OnClickToggle(captured));
            }
        }

        private async void OnClickEnter()  { if (_busy) return; _busy = true;  try { await EnterAreaAsync();  } finally { _busy = false; RefreshUI(); } }
        private async void OnClickExit()   { if (_busy) return; _busy = true;  try { await ExitAreaAsync();   } finally { _busy = false; RefreshUI(); } }
        private async void OnClickSave()   { if (_busy) return; _busy = true;  try { await SaveAsync();       } finally { _busy = false; RefreshUI(); } }
        private        void OnClickLoad()  { if (_busy) return; LoadSlot();    RefreshUI(); }
        private        void OnClickDelete(){ if (_busy) return; DeleteSlot();  RefreshUI(); }

        private void OnClickToggle(int index)
        {
            if (_busy) return;
            if (index < 0 || index >= cubeNames.Count) return;

            var cube = FindCubeInArea(cubeNames[index]);
            if (cube == null)
            {
                Log($"Cube '{cubeNames[index]}' not found — is the area scene loaded?");
                return;
            }
            cube.SetActive(!cube.activeSelf);
            RefreshUI();
        }

        // ──────────────────────────────────────────────────────────
        // ISceneLoadOrchestrator — area additive load/unload
        // ──────────────────────────────────────────────────────────
        // The hooks below mirror AdditiveScenesDemoController and are invoked
        // by EnterAreaAsync / ExitAreaAsync. They are NOT invoked by
        // SaveManager.Load — Crystal Save's internal scene-loading paths do
        // not dispatch orchestrator events. The hooks exist here purely to
        // share a consistent additive-area pattern across the two demos.
        public async Task OnScenePreLoad(string sceneName)
        {
            // Mirror AdditiveScenesDemoController: prime Crystal Save with the
            // persisted slot data BEFORE the area scene loads. This:
            //   • sets SaveManager.CurrentSaveData,
            //   • imports HomeSceneSnapshots into ComponentManager so
            //     SaveableComponent registrations can find a snapshot to apply,
            //   • re-queues any saved prefab spawns for this scene.
            //
            // We need this for the Exit → Enter cycle: the in-memory home-scene
            // snapshot path alone is not enough because the area scene becomes
            // active only AFTER its components have already registered, so
            // TryApplyRememberedSnapshot's active-scene guard rejects them at
            // registration time. Loading the slot back in primes the apply
            // pipeline so we can fall back to ApplyGameObjectActiveStates after
            // the additive load completes.
            if (SaveManager.Instance == null) return;
            // PopulatePendingPrefabsFromSlotAsync logs and no-ops if the slot is empty.
            await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(slot);
            Log($"[Hook] OnScenePreLoad('{sceneName}') — populated slot {slot}.");
        }
        public Task OnSceneWillActivate(Scene scene) => Task.CompletedTask;

        public void OnSceneActivated(Scene scene)
        {
            if (scene.name != areaSceneName) return;

            // CRITICAL for RememberHomeScene with additive scenes:
            //
            // Crystal Save's home-scene snapshot/restore pipeline is driven by
            // SceneManager.activeSceneChanged. Without this call the area
            // scene would never become "active", so the area's
            // RememberHomeScene snapshot would never be captured / re-applied,
            // and CubeB_StartsDisabled (disabled at design time) would never
            // be re-enabled across an unload/reload cycle.
            //
            // Side effect users MUST be aware of: while the area is the
            // active scene, naive Object.Instantiate(prefab) calls without a
            // parent land in the area scene and are destroyed when the area
            // unloads. See the documentation page "Additive scenes and the
            // active-scene caveat" for recommended patterns.
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.SetActiveScene(scene);

            Log($"[Hook] OnSceneActivated('{scene.name}'). Active scene = area.");
        }

        public void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == areaSceneName)
                Log($"[Hook] OnSceneUnloaded('{scene.name}').");
        }

        private async Task EnterAreaAsync()
        {
            var existing = SceneManager.GetSceneByName(areaSceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                OnSceneActivated(existing);
                return;
            }

            // Populate first so the slot's RememberScenes / pending component
            // data are available. NOTE: RememberScenes.DeserializeComponentData
            // itself calls SceneManager.LoadSceneAsync(area, Additive) for any
            // scenes recorded in the slot, so by the time this returns the
            // area scene may ALREADY be loading. We must not kick off a second
            // load — that would result in two copies of the area scene.
            await OnScenePreLoad(areaSceneName);

            // If RememberScenes (or anything else) already started an additive
            // load for the area scene, await it instead of starting our own.
            // GetSceneByName returns a valid scene as soon as a load has been
            // requested; isLoaded only becomes true once the load completes.
            Scene loaded = SceneManager.GetSceneByName(areaSceneName);
            if (!loaded.IsValid())
            {
                var op = SceneManager.LoadSceneAsync(areaSceneName, LoadSceneMode.Additive);
                if (op == null)
                {
                    Log($"Area scene '{areaSceneName}' is not in Build Settings.");
                    return;
                }
                while (!op.isDone) await Task.Yield();
                loaded = SceneManager.GetSceneByName(areaSceneName);
            }
            else
            {
                while (!loaded.isLoaded) await Task.Yield();
            }

            await OnSceneWillActivate(loaded);
            OnSceneActivated(loaded);

            // Now that the area scene is loaded, its cubes are registered, and
            // active scene = area, re-apply the persisted active states from
            // CurrentSaveData. ApplyGameObjectActiveStates uses the
            // GameObjectTracker's cached references and works for cubes that
            // were disabled at design time — SetActive(true) is called on the
            // cached reference even if the cube is currently inactive.
            if (SaveManager.Instance != null && SaveManager.Instance.CurrentSaveData != null)
            {
                // One-frame yield so any registration coroutines from the
                // additive load have completed before we re-apply.
                await Task.Yield();
                SaveManager.Instance.ApplyGameObjectActiveStates();
            }

            Log($"Entered area '{areaSceneName}'.");
        }

        private async Task ExitAreaAsync()
        {
            var scene = SceneManager.GetSceneByName(areaSceneName);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Log("Area is not loaded — nothing to exit.");
                return;
            }

            // Persist the current area state to disk BEFORE unloading, so
            // Exit → Enter survives across the additive cycle just like the
            // prefab demo. Without this, the in-memory home-scene snapshot is
            // present but the active-scene timing prevents it from re-applying
            // when the area is loaded again.
            if (SaveManager.Instance != null)
            {
                Log($"Saving slot {slot} so cube states survive Exit→Enter…");
                await SaveManager.Instance.SaveAsync(slot);
            }

            // Switch the active scene BACK to the boot/main scene before
            // unloading. This fires SceneManager.activeSceneChanged with
            // oldScene = area, which Crystal Save uses to call
            // ComponentManager.SnapshotSceneAll(area). The snapshot captures
            // every cube's active state (including inactive ones — the
            // snapshot walker uses includeInactive: true).
            //
            // NOTE: gameObject.scene is the internal "DontDestroyOnLoad"
            // scene (because of DontDestroyOnLoad in Awake) and Unity's
            // SetActiveScene throws ArgumentException on it. We resolve the
            // real boot scene by the name we captured in Awake instead.
            var bootScene = SceneManager.GetSceneByName(mainSceneName);
            if (bootScene.IsValid() && bootScene.isLoaded)
                SceneManager.SetActiveScene(bootScene);

            var op = SceneManager.UnloadSceneAsync(scene);
            while (op != null && !op.isDone) await Task.Yield();

            OnSceneUnloaded(scene);
            Log($"Exited area. Slot {slot} holds the cube states; Enter Area to verify.");
        }

        // ──────────────────────────────────────────────────────────
        // Slot operations (disk persistence)
        // ──────────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }
            await SaveManager.Instance.SaveAsync(slot);
            Log($"Saved slot {slot}. (Includes additive area state.)");
        }

        private void LoadSlot()
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }
            SaveManager.Instance.Load(slot);
            Log($"Loading slot {slot}…");
        }

        private void DeleteSlot()
        {
            if (SaveManager.Instance == null) { Log("SaveManager not ready."); return; }
            SaveManager.Instance.Delete(slot);
            Log($"Deleted slot {slot}.");
        }

        // ──────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────
        private GameObject FindCubeInArea(string cubeName)
        {
            var area = SceneManager.GetSceneByName(areaSceneName);
            if (!area.IsValid() || !area.isLoaded) return null;

            foreach (var root in area.GetRootGameObjects())
            {
                if (root.name == cubeName) return root;
                var t = root.transform.Find(cubeName);
                if (t != null) return t.gameObject;
            }
            return null;
        }

        private void RefreshUI()
        {
            var area = SceneManager.GetSceneByName(areaSceneName);
            bool areaLoaded = area.IsValid() && area.isLoaded;

            if (enterButton != null) enterButton.interactable = !areaLoaded && !_busy;
            if (exitButton  != null) exitButton.interactable  =  areaLoaded && !_busy;

            for (int i = 0; i < toggleButtons.Count; i++)
            {
                if (toggleButtons[i] == null) continue;
                bool canToggle = areaLoaded && !_busy && i < cubeNames.Count;
                toggleButtons[i].interactable = canToggle;

                if (i < toggleLabels.Count && toggleLabels[i] != null)
                {
                    var cubeName = i < cubeNames.Count ? cubeNames[i] : $"Cube {i}";
                    var go = canToggle ? FindCubeInArea(cubeName) : null;
                    string state = go != null ? (go.activeSelf ? "ON"  : "OFF") : "—";
                    toggleLabels[i].text = $"Toggle {cubeName}\n<b>[{state}]</b>";
                }
            }

            if (statusText == null) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>Main scene:</b> {mainSceneName}");
            sb.AppendLine($"<b>Area '{areaSceneName}':</b> {(areaLoaded ? "loaded" : "not loaded")}");
            sb.AppendLine($"<b>Save slot:</b> {slot}");
            if (areaLoaded)
            {
                sb.AppendLine();
                foreach (var n in cubeNames)
                {
                    var go = FindCubeInArea(n);
                    if (go != null) sb.AppendLine($"  • {n}: <b>{(go.activeSelf ? "ENABLED" : "DISABLED")}</b>");
                    else            sb.AppendLine($"  • {n}: (missing)");
                }
            }
            statusText.text = sb.ToString();
        }

        private void Log(string msg)
        {
            Debug.Log($"[AdditiveActiveStateDemo] {msg}");
        }
    }
}
#endif
