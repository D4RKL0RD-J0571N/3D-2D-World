#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
#if REMEMBERME_AUTHENTICATION_PRESENT
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
#endif
#if REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Core;
#endif

namespace Arawn.CrystalSave.Runtime
{
    internal static class SaveManagerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SaveManager.Bootstrap();
        }
    }

    /// <summary>
    /// Manages saving and loading of game data, including scene management, prefab handling, and component state.
    /// Implements a state machine for the loading process to ensure robustness and maintainability.
    /// </summary>
    [RequireComponent(typeof(UnityMainThreadDispatcher))]
    [RequireComponent(typeof(PrefabManager))]
    [RequireComponent(typeof(GameObjectTracker))]
    [RequireComponent(typeof(LiveConflictResolver))]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("")]
        [DisallowMultipleComponent]
        public partial class SaveManager : MonoBehaviour
        {
#if UNITY_EDITOR
        private const string DebugLogTag = "[CrystalSaveDebug]";
#endif
        #region Singleton and Awake

        /// <summary>
        /// Raised once the SaveManager has finished its asynchronous
        /// initialisation sequence.
        /// </summary>
        public static event Action<SaveManager> Initialized;

        /// <summary>
        /// Indicates whether the SaveManager completed initialisation.
        /// </summary>
        public static bool IsInitialized { get; private set; }
        private bool initializationCompleted;

        /// <summary>
        /// Singleton instance of the SaveManager.
        /// </summary>
        public static SaveManager Instance { get; private set; }

        /// <summary>
        /// Indicates whether the manager is in the process of shutting down.
        /// This is set when Unity begins to destroy the component so that
        /// asynchronous initialisation routines can abort without logging
        /// spurious errors.
        /// </summary>
        internal bool IsShuttingDown { get; private set; }

        // Queue for operations invoked before the manager has finished
        // initialisation. These will be executed once initialisation
        // completes.
        private readonly Queue<Action> queuedOperations = new();
        private readonly object queueLock = new();

        internal static void Bootstrap()
        {
            if (Instance == null)
            {
                GameObject saveManagerGO = new GameObject("SaveManager");
                Instance = saveManagerGO.AddComponent<SaveManager>();
#if NANINOVEL && MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_NANINOVEL_PRESENT
                var settings = AssetProvider.Load<SaveSettings>("SaveSettings");
                if (settings != null &&
                    settings.screenshotProvider == ScreenshotProvider.Naninovel)
                {
                    saveManagerGO.AddComponent<Arawn.CrystalSave.NaninovelIntegration.NaninovelScreenshotBridge>();
                }
#endif
                DontDestroyOnLoad(saveManagerGO);
            }
        }

        // When domain reload is disabled, Unity calls SubsystemRegistration at the start of Play Mode.
        // Reset static fields here to avoid stale references to destroyed objects after an error aborts play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Clear singleton and init flags
            Instance = null;
            IsInitialized = false;
            AreSaveSlotsReady = false;
            AreQuickSlotsReady = false;
            AreAutoSlotsReady = false;
            // Clear static event to drop old subscribers captured from previous play sessions
            Initialized = null;
            SaveSlotsInitialized = null;
            QuickSlotsInitialized = null;
            AutoSlotsInitialized = null;
        }

        private async void Awake()
        {
            operationCoordinator = new SaveOperationCoordinator(this);
            if (Instance != null && Instance != this)
            {
                DestroyHelper.DestroyWithLogging(this.gameObject, "SaveManager.Awake: Duplicate Instance");
                return;
            }

            // Ensure Instance is set to this if it's null (edge cases)
            if (Instance == null)
            {
                Instance = this;
                IsInitialized = false;
                AreSaveSlotsReady = false;
                AreQuickSlotsReady = false;
                AreAutoSlotsReady = false;
                DontDestroyOnLoad(this.gameObject);
            }

            // Ensure SaveSettings is loaded before any access
            LoadSaveSettings();

            SetLookupCacheEnabled(saveSettings != null && saveSettings.enableLookupCache);

            if (saveSettings != null && saveSettings.enableSync && saveSettings.syncSettings != null)
            {
                syncManager = new SyncManager(this, saveSettings.syncSettings);
            }


            var conflictResolver = GetComponent<LiveConflictResolver>();
            if (conflictResolver != null)
            {
                conflictResolver.OverlayCanvas = saveSettings != null ? saveSettings.conflictOverlayCanvas : null;
            }

            _pathProvider = saveSettings?.CreatePathProvider() ?? new DefaultStoragePathProvider();
            _rootPath = _pathProvider.GetRootPath();
            Directory.CreateDirectory(_rootPath);

            if (saveSettings != null &&
                saveSettings.runPersistentPathMigrationOnStartup &&
                saveSettings.persistentPath.mode == PersistentPathMode.Custom)
            {
                if (!await PersistentPathMigration.TryMigrateAsync(Application.persistentDataPath, _rootPath))
                    throw new IOException("Persistent-path migration could not complete; original saves were retained where needed for recovery.");
            }

            if (saveSettings != null &&
                !saveSettings.enableCloudSave &&
                (saveSettings.backend == SaveBackend.Supabase ||
                 saveSettings.backend == SaveBackend.MySQL ||
                 saveSettings.backend == SaveBackend.Firebase))
            {
                Logger.Log(
                    "Cloud Save is required when using remote backends like Supabase, Firebase or MySQL. " +
                    "Select UnityCloudSave for local-only saves.",
                    LogCategory.SaveManager,
                    LogLevel.Warning);
            }

            try
            {
                await InitializeDependenciesAsync();

                if (this == null || IsShuttingDown)
                {
                    return;
                }
                gameObjectTracker?.Initialize(this, prefabManager, componentManager);
                prefabRestoreService = new PrefabRestoreService(this, prefabManager, gameObjectTracker);
                PrefabRestoreService = prefabRestoreService;
                sceneLoadManager = new SceneLoadManager(this);
                SceneLoadManager = sceneLoadManager;
                saveOperationService = new SaveOperationService(this, screenshotManager, serializer, SlotManager, GetComponent<LiveConflictResolver>());
                SaveOperations = saveOperationService;
                if (saveSettings.enableCloudSave)
                {
                    cloudSaveService = new CloudSaveService(this);
                    CloudSaveService = cloudSaveService;

                    if (saveSettings.backend == SaveBackend.Supabase)
                        SubscribeSupabaseLoginRelay();

                    // When using Supabase with a custom user strategy, the
                    // credentials may already be loaded from the resolver
                    // before the SaveManager subscribes to login events.
                    // Ensure the login state is initialised here so cloud
                    // operations work even if the event was fired earlier.
                    if (saveSettings.backend == SaveBackend.Supabase &&
                        saveSettings.userFolderStrategy == UserFolderStrategy.Custom &&
                        saveSettings.customUserFolderResolver is IUserAuthorizationResolver authRes)
                    {
                        string token = authRes.ResolveAccessKey();
                        if (!string.IsNullOrEmpty(token))
                        {
                            string uid = null;
                            if (saveSettings.customUserFolderResolver is IUserFolderResolver folderRes)
                            {
                                string folder = folderRes.ResolveUserFolder();
                                if (!string.IsNullOrEmpty(folder))
                                {
                                    int slash = folder.LastIndexOf('/');
                                    uid = slash >= 0 ? folder[(slash + 1)..] : folder;
                                }
                            }
                            cloudSaveService.OnSupabaseLoggedIn(uid);
                        }
                    }
                }

                if (syncManager != null)
                {
                    try
                    {
                        await syncManager.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"SyncManager initialization failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Warning);
                    }
                }
#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log("[SaveManager] WebGL: Setting IsInitialized = true", LogCategory.SaveManager, LogLevel.Info);
#endif
                if (this == null || IsShuttingDown || Instance != this) return;
                initializationCompleted = true;
                IsInitialized = true;
#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log("[SaveManager] WebGL: Executing queued operations", LogCategory.SaveManager, LogLevel.Info);
#endif
                ExecuteQueuedOperations();
#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log("[SaveManager] WebGL: Invoking Initialized event", LogCategory.SaveManager, LogLevel.Info);
#endif
                SaveEventDispatch.Raise(Initialized, this);
#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log("[SaveManager] WebGL: Registering existing GameObjects", LogCategory.SaveManager, LogLevel.Info);
#endif
                if (saveSettings != null && saveSettings.scanForExistingGameObjects)
                {
                    RegisterExistingGameObjects();
                }

                // Ensure quick-save slots are ready for immediate use
                if (!AreQuickSlotsReady && saveSettings != null && saveSettings.numberOfQuickSaveSlots > 0)
                {
                    try
                    {
                        await InitializeQuickSaveSlotsAsync(saveSettings.numberOfQuickSaveSlots);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"InitializeQuickSaveSlotsAsync failed during Awake: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
                    }
                }
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                if (saveSettings.enableCloudSave &&
                    saveSettings.backend != SaveBackend.Supabase &&
                    saveSettings.backend != SaveBackend.Firebase &&
                    saveSettings.autoCloudSignIn && cloudSaveService != null)
                {
                    await cloudSaveService.InitializeCloudServicesAsync();
                }
#endif
            }
            catch (Exception ex)
            {
                if (this == null || IsShuttingDown)
                {
                    // Unity is destroying the object (e.g. exiting Play Mode).
                    // Silently abort initialisation without logging errors that
                    // would be misleading in the editor console.
                    return;
                }

                Logger.Log($"SaveManager initialisation failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);

                if (this != null)
                {
                    enabled = false;
                }
            }
        }

        private void OnDestroy()
        {
            bool applicationShutdown = IsShuttingDown;
            IsShuttingDown = true;
            initializationCompleted = false;
            // Delayed helpers and public batches belong to this registry's lifetime.
            // Surviving scene objects must not apply the retired owner's pending
            // snapshot after a replacement manager takes responsibility for them.
            componentManager?.BeginFullLoadGeneration();
            gameObjectTracker?.RetireManagerWork();
            operationCoordinator?.Dispose();
            syncManager?.Retire();
            UnsubscribeRuntimeEvents();
            // If this instance is being torn down, release the singleton so the next Play session can recreate cleanly
            if (Instance == this)
            {
                if (!applicationShutdown)
                    componentManager?.PrepareRegistrationsForManagerReplacement();
                Instance = null;
                IsInitialized = false;
                AreSaveSlotsReady = false;
                AreQuickSlotsReady = false;
                AreAutoSlotsReady = false;
            }
        }

        private void OnApplicationQuit()
        {
            IsShuttingDown = true;
        }

        #endregion

        #region Unity Lifecycle
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        private void Start()
        {
            // Auto sign-in is handled during Awake after initialization
        }
#endif
        #endregion

        #region Fields
        const string DEVICE_GUID_PREF = "CrystalSave_DeviceGuid";
        private byte[] cryptoKey;
        private bool   useEncryption;
        private bool   useCompression;
        IMasterSecretProvider secretProvider;
        byte[] masterSecret;
        internal static string ResolveUserIdentifier()
        {
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            // If the player is signed in, use the same ID Supabase & Cloud-Save use
            var auth = Unity.Services.Authentication.AuthenticationService.Instance;
            if (auth.IsSignedIn && !string.IsNullOrEmpty(auth.PlayerId))
                return auth.PlayerId;
#endif
            // Fallback: one persistent GUID per installation
            if (!PlayerPrefs.HasKey(DEVICE_GUID_PREF))
                PlayerPrefs.SetString(DEVICE_GUID_PREF, Guid.NewGuid().ToString("N"));
            return PlayerPrefs.GetString(DEVICE_GUID_PREF);
        }

#if REMEMBERME_CORESERVICES_PRESENT
        internal static async Task EnsureUnityServicesInitializedAsync ()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();          // no-op if already running
        }
#endif

        private bool isLoading = false;
        public bool IsLoading => isLoading;
        
        private bool isInSceneTransition = false;
        public bool IsInSceneTransition => isInSceneTransition;

        [Header("Save Settings")]
        [SerializeField]
        private SaveSettings saveSettings;
        public SaveSettings SaveSettings => saveSettings;

        internal const string DefaultTagRegistryAssetKey = "TagRegistry";

        internal static string GetTagRegistryAssetKey()
        {
            var settings = Instance != null ? Instance.saveSettings : null;
            var configuredKey = settings != null ? settings.tagRegistryKey : null;

            return string.IsNullOrWhiteSpace(configuredKey)
                ? DefaultTagRegistryAssetKey
                : configuredKey.Trim();
        }
        private IStoragePathProvider _pathProvider;
        private string _rootPath;
        public string RootPath => _rootPath;
        private MigrationManager migrationManager;
        public MigrationManager MigrationManager => migrationManager;
        private VersionManager versionManager;
        public VersionManager VersionManager => versionManager;
        private SyncManager syncManager;
        public SyncManager SyncManager => syncManager;
        private SaveDataSerializer serializer;
        private ISaveSystem saveSystem;
        private ScreenshotManager screenshotManager;
        private SaveOperationService saveOperationService;
        private PrefabManager prefabManager;
        public PrefabManager GetPrefabManager => prefabManager;
        private ComponentManager componentManager;
        private GameObjectTracker gameObjectTracker;
        public GameObjectTracker GameObjectTracker => gameObjectTracker;

        private readonly SaveObjectLookup objectLookup = new();
        public bool LookupCacheEnabled => objectLookup.Enabled;
        internal IReadOnlyDictionary<string, GameObject> CachedObjects => objectLookup.CachedObjects;
        /// <summary>
        /// The currently loaded <see cref="SaveData"/> instance. The setter is
        /// marked as <c>internal</c> so that other runtime components such as
        /// <see cref="GameObjectTracker"/> can update the reference while
        /// keeping the API surface of <see cref="SaveManager"/> clean for
        /// external consumers.
        /// </summary>
        public SaveData CurrentSaveData { get; internal set; }
        public SaveStateMachine StateMachine { get; private set; } = new SaveStateMachine();

        private SaveSlot currentSaveSlot;
        /// <summary>
        /// The save slot that was most recently saved to or loaded from.
        /// </summary>
        public SaveSlot CurrentSaveSlot => currentSaveSlot;

        internal bool UseLocalMirror
        {
            get
            {
                if (saveSettings.enableCloudSave &&
                    saveSettings.cloudCryptoMode == CloudCryptoMode.ServerSide)
                    return false;

                return saveSettings.backend == SaveBackend.UnityCloudSave      // classic offline mode
                       || (saveSettings.backend != SaveBackend.UnityCloudSave  // Supabase / Unity Cloud
                           && saveSettings.keepLocalMirror);
            }
        }

        private bool isSupabaseCustomLoggedIn = false;
        public bool IsSupabaseCustomLoggedIn => isSupabaseCustomLoggedIn;

        private CloudSaveService cloudSaveService;
        private PrefabRestoreService prefabRestoreService;
        internal SceneLoadManager sceneLoadManager;

        /// <summary>
        /// Registered scene load orchestrators for custom scene loading integration.
        /// </summary>
        private readonly List<ISceneLoadOrchestrator> sceneLoadOrchestrators = new List<ISceneLoadOrchestrator>();

        private readonly HashSet<string> buildSceneNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> destroyedIdAliases = new(StringComparer.Ordinal);

        // Number of frames to wait before enforcing GameObject active state
        // restoration as a fallback. Allows tuning to handle edge cases
        // where SetActive may not immediately stick.
        [SerializeField]
        private int activeStateEnforceDelayFrames = 1;
        private int ActiveStateEnforceDelayFrames => gameObjectTracker != null ? gameObjectTracker.ActiveStateEnforceDelayFrames : activeStateEnforceDelayFrames;

        [SerializeField]
        private bool enforceActiveState = false;
        public bool EnforceActiveState => gameObjectTracker != null ? gameObjectTracker.EnforceActiveState : enforceActiveState;

        [SerializeField]
        private float activeStateWatchDuration = 0f;
        public float ActiveStateWatchDuration => gameObjectTracker != null ? gameObjectTracker.ActiveStateWatchDuration : activeStateWatchDuration;
        private readonly Dictionary<int, TaskCompletionSource<LoadResult>> loadCompletionSources = new Dictionary<int, TaskCompletionSource<LoadResult>>();
        private readonly object loadCompletionLock = new object();
        private readonly List<SaveSlot> saveSlots = new List<SaveSlot>();
        private readonly List<SaveSlot> quickSaveSlots = new List<SaveSlot>();
        private readonly List<SaveSlot> autoSaveSlots = new List<SaveSlot>();

        /// <summary>
        /// Dictionary to hold tracked GameObjects by their UniqueID.
        /// </summary>

        /// <summary>
        /// Load Locks: Tracks if a specific slot is currently being loaded
        /// </summary>
        private readonly Dictionary<int, bool> loadLocks = new Dictionary<int, bool>();

        /// <summary>
        /// Lock object for thread-safe access to loadLocks
        /// </summary>
        private readonly object loadLocksLock = new object();

        /// <summary>
        /// Exposes ISaveable components via ComponentManager.
        /// </summary>
        public IReadOnlyList<ISaveable> SaveableComponents => componentManager.GetSaveableComponents();

        /// <summary>
        /// Exposes the SaveDataSerializer.
        /// </summary>
        public SaveDataSerializer Serializer => serializer;

        /// <summary>
        /// Exposes the SaveSystem
        /// </summary>
        public ISaveSystem SaveSystem => saveSystem;

        // Expose ComponentManager
        public ComponentManager ComponentManager => componentManager;

        internal async Task<bool> WaitForSupabaseLoginAsync(float timeoutSeconds = 30f)
        {
            if (!saveSettings.enableCloudSave ||
                  saveSettings.backend != SaveBackend.Supabase ||
                  saveSettings.userFolderStrategy != UserFolderStrategy.Custom ||
                  IsSupabaseCustomLoggedIn)
                return true;
            Logger.Log("Waiting for connection to Supabase...", LogCategory.SaveManager, LogLevel.Info);
            float start = Time.realtimeSinceStartup;
            while (!IsSupabaseCustomLoggedIn && Time.realtimeSinceStartup - start < timeoutSeconds)
                await Task.Yield();

            if (!IsSupabaseCustomLoggedIn)
                Logger.Log("Supabase connection could not be established in time.", LogCategory.SaveManager, LogLevel.Warning);
            return IsSupabaseCustomLoggedIn;
        }

        internal async Task<bool> WaitForCloudConnectionAsync(float timeoutSeconds = 30f)
        {
            if (!saveSettings.enableCloudSave)
                return true;

            if (saveSettings.backend == SaveBackend.Supabase &&
                saveSettings.userFolderStrategy == UserFolderStrategy.Custom)
            {
                return await WaitForSupabaseLoginAsync(timeoutSeconds);
            }
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            if (saveSettings.backend == SaveBackend.UnityCloudSave)
            {
                float start = Time.realtimeSinceStartup;
                while (!AuthenticationService.Instance.IsSignedIn &&
                       Time.realtimeSinceStartup - start < timeoutSeconds)
                {
                    await Task.Yield();
                }
                return AuthenticationService.Instance.IsSignedIn;
            }
#endif
            return true;
        }

        /// <summary>
        /// Waits until save slots have finished initialisation.
        /// </summary>
        public async Task WaitForSaveSlotsAsync(float timeoutSeconds = 10f)
        {
            if (AreSaveSlotsReady && SlotManager != null)
                return;

            float start = Time.realtimeSinceStartup;
            while ((!AreSaveSlotsReady || SlotManager == null) && Time.realtimeSinceStartup - start < timeoutSeconds)
                await Task.Yield();

            if (AreSaveSlotsReady && SlotManager != null)
                return;

            if (saveSettings != null && saveSettings.numberOfSaveSlots > 0)
            {
                Logger.Log("[SaveManager] WaitForSaveSlotsAsync timed out or found stale state; reinitializing save slots.", LogCategory.SaveManager, LogLevel.Warning);
                await InitializeSaveSlotsAsync(saveSettings.numberOfSaveSlots);
            }
        }

        public async Task WaitForQuickSlotsAsync(float timeoutSeconds = 10f)
        {
            if (AreQuickSlotsReady && QuickSlotManager != null)
                return;

            float start = Time.realtimeSinceStartup;
            while ((!AreQuickSlotsReady || QuickSlotManager == null) && Time.realtimeSinceStartup - start < timeoutSeconds)
                await Task.Yield();

            if (AreQuickSlotsReady && QuickSlotManager != null)
                return;

            if (saveSettings != null && saveSettings.numberOfQuickSaveSlots > 0)
            {
                Logger.Log("[SaveManager] WaitForQuickSlotsAsync timed out or found stale state; reinitializing quick slots.", LogCategory.SaveManager, LogLevel.Warning);
                await InitializeQuickSaveSlotsAsync(saveSettings.numberOfQuickSaveSlots);
            }
        }

        public async Task WaitForAutoSlotsAsync(float timeoutSeconds = 10f)
        {
            if (AreAutoSlotsReady && AutoSlotManager != null)
                return;

            float start = Time.realtimeSinceStartup;
            while ((!AreAutoSlotsReady || AutoSlotManager == null) && Time.realtimeSinceStartup - start < timeoutSeconds)
                await Task.Yield();

            if (AreAutoSlotsReady && AutoSlotManager != null)
                return;

            if (saveSettings != null && saveSettings.numberOfAutoSaveSlots > 0)
            {
                Logger.Log("[SaveManager] WaitForAutoSlotsAsync timed out or found stale state; reinitializing auto slots.", LogCategory.SaveManager, LogLevel.Warning);
                await InitializeAutoSaveSlotsAsync(saveSettings.numberOfAutoSaveSlots);
            }
        }

        [Header("Scene Object Restoration")]
        [SerializeField]
        private SceneObjectRegistry sceneObjectRegistry; // Assign in Inspector or load from Resources

        /// <summary>
        /// Checks if a GameObject with the specified UniqueID is tracked by the SaveManager.
        /// </summary>
        /// <param name="uniqueID">The UniqueID of the GameObject.</param>
        /// <returns>True if the GameObject is tracked; otherwise, false.</returns>
        public bool IsTracked(string uniqueID)
        {
            if (gameObjectTracker != null)
                return gameObjectTracker.IsTracked(uniqueID);
            return false;
        }

        #endregion

        #region Events

        // Public events for operation completions
        public event Action OnSaveSlotsUpdated;
        /// <summary>
        /// Raised once save slots have been fully initialised.
        /// </summary>
        public static event Action SaveSlotsInitialized;
        public static bool AreSaveSlotsReady { get; private set; }

        public event Action OnQuickSlotsUpdated;
        public static event Action QuickSlotsInitialized;
        public static bool AreQuickSlotsReady { get; private set; }

        public event Action OnAutoSlotsUpdated;
        public static event Action AutoSlotsInitialized;
        public static bool AreAutoSlotsReady { get; private set; }
        public event EventHandler<SaveLoadEventArgs> OnSaveCompleted;
        /// <summary>
        /// Raised after a load request is accepted and <see cref="IsLoading"/> is set, before restore work begins.
        /// </summary>
        public event EventHandler<SaveManagerEventArgs> OnLoadStarted;
        public event EventHandler<SaveLoadEventArgs> OnLoadCompleted;
        public event EventHandler<SaveManagerEventArgs> OnDeleteCompleted;
        public event EventHandler<RenameSlotEventArgs> OnRenameSlotCompleted;

        /// <summary>
        /// Fired right before the ScreenshotManager captures a screenshot.
        /// </summary>
        public event Action OnScreenshotCaptureStarted;

        /// <summary>
        /// Fired once the ScreenshotManager has finished capturing a screenshot.
        /// </summary>
        public event Action OnScreenshotCaptureFinished;

        // Public events for operation failures
        public event EventHandler<OperationFailedEventArgs> OnSaveFailed;
        public event EventHandler<OperationFailedEventArgs> OnLoadFailed;
        public event EventHandler<OperationFailedEventArgs> OnDeleteFailed;
        public event EventHandler<OperationFailedEventArgs> OnRenameSlotFailed;
        public event EventHandler<OperationFailedEventArgs> OnBackupFailed;
        public event EventHandler<OperationFailedEventArgs> OnVerificationFailed;

        // Events related to scene loading
        public event Action<string> OnSceneLoadStarted; // Parameter: Scene Name
        public event Action<float> OnSceneLoadProgress; // Parameter: Progress (0 to 1)
        public event Action<string> OnSceneLoadCompleted; // Parameter: Scene Name
        public event Action<string> OnSceneActivationRequested; // Parameter: Scene Name

        public event Action<string> OnGameObjectRestored; // Fires when a single GameObject is restored; passes its UniqueID.
        public event Action OnAllGameObjectsRestored; // Fires when all destroyed GameObjects have been restored.
        public event Action<GameObject> OnSingleGameObjectRestored; // Fires whenever RestoreSingleGameObject succeeds on a live GameObject.

        /// <summary>
        /// Hook system for controlling when prefabs spawn during scene activation.
        /// Subscribe to this delegate to delay prefab spawning until your custom loader signals "ready".
        /// The delegate receives the scene name and should return true when prefabs are allowed to spawn.
        /// </summary>
        /// <remarks>
        /// Example usage for custom loading screens:
        /// <code>
        /// SaveManager.Instance.SceneActivationPipeline = (sceneName) =>
        /// {
        ///     // Only spawn prefabs if loading screen is hidden
        ///     return !IsLoadingScreenVisible();
        /// };
        /// </code>
        /// 
        /// Example for delayed spawning:
        /// <code>
        /// private bool allowPrefabSpawn = false;
        /// 
        /// SaveManager.Instance.SceneActivationPipeline = (sceneName) => allowPrefabSpawn;
        /// 
        /// // Later, when ready:
        /// allowPrefabSpawn = true;
        /// </code>
        /// </remarks>
        public Func<string, bool> SceneActivationPipeline { get; set; }

        #endregion

        internal void NotifySaveSlotsUpdated() => SaveEventDispatch.Raise(OnSaveSlotsUpdated);

        internal void RaiseSceneLoadStarted(string sceneName)
            => SaveEventDispatch.Raise(OnSceneLoadStarted, sceneName);

        internal void RaiseSceneLoadProgress(float progress)
            => SaveEventDispatch.Raise(OnSceneLoadProgress, progress);

        internal void RaiseSceneActivationRequested(string sceneName)
            => SaveEventDispatch.Raise(OnSceneActivationRequested, sceneName);

        internal void RaiseSceneLoadCompleted(string sceneName)
            => SaveEventDispatch.Raise(OnSceneLoadCompleted, sceneName);

        internal void RaiseScreenshotCaptureStarted()
            => SaveEventDispatch.Raise(OnScreenshotCaptureStarted);

        internal void RaiseScreenshotCaptureFinished()
            => SaveEventDispatch.Raise(OnScreenshotCaptureFinished);

        internal void RaiseLoadStarted(SaveSlot slot)
            => SaveEventDispatch.Raise(OnLoadStarted, this, new SaveManagerEventArgs(slot));

        internal void RaiseLoadFailed(OperationFailedEventArgs args)
            => SaveEventDispatch.Raise(OnLoadFailed, this, args);

        #region Initialization and Dependencies

        /// <summary>
        /// Creates the concrete ISaveSystem that backs all slot operations,
        /// based on <see cref="SaveSettings.backend"/>.
        /// </summary>
        internal ISaveSystem CreateSaveSystem()
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Logger.Log($"[SaveManager] WebGL: CreateSaveSystem called with backend: {saveSettings.backend}", LogCategory.SaveManager, LogLevel.Info);
#endif
                switch (saveSettings.backend)
                {
                    /* ───────── Supabase backend ───────── */
                    case SaveBackend.Supabase:
                    {
#if UNITY_WEBGL && !UNITY_EDITOR
                        Logger.Log("[SaveManager] WebGL: Creating SupabaseSaveSystem...", LogCategory.SaveManager, LogLevel.Info);
#endif
                        // Cloud save systems such as SupabaseSaveSystem and FirebaseSaveSystem
                        // handle the User-Id Folder Strategy (Shared, PublicPerBuild,
                        // GuidPerDevice, UnityAuthentication, Custom) on their own,
                        // based on the fields in saveSettings.
                        var system = new SupabaseSaveSystem(saveSettings, _rootPath);
#if UNITY_WEBGL && !UNITY_EDITOR
                        Logger.Log("[SaveManager] WebGL: SupabaseSaveSystem created successfully", LogCategory.SaveManager, LogLevel.Info);
#endif
                        return system;
                    }

                    /* ───────── Firebase backend ───────── */
                    case SaveBackend.Firebase:
#if UNITY_WEBGL && !UNITY_EDITOR
                        Logger.Log("[SaveManager] WebGL: Creating FirebaseSaveSystem...", LogCategory.SaveManager, LogLevel.Info);
#endif
                        return new FirebaseSaveSystem(saveSettings, _rootPath);

                    /* ───────── MySQL backend ───────── */
                    case SaveBackend.MySQL:
#if UNITY_WEBGL && !UNITY_EDITOR
                        Logger.Log("[SaveManager] WebGL: Creating MySqlSaveSystem...", LogCategory.SaveManager, LogLevel.Info);
#endif
                        return new MySqlSaveSystem(saveSettings, _rootPath);

                    /* ───────── Local mirror (file / PlayerPrefs) ───────── */
                    case SaveBackend.UnityCloudSave:
                    default:
#if UNITY_WEBGL && !UNITY_EDITOR
            Logger.Log("[SaveManager] WebGL: Creating default SaveSystem...", LogCategory.SaveManager, LogLevel.Info);
#endif
                        return new SaveSystem(saveSettings, _rootPath);
                }
            }
            catch (System.Exception ex)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
        Logger.Log($"[SaveManager] WebGL: CreateSaveSystem failed with exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
        Logger.Log($"[SaveManager] WebGL: Exception type: {ex.GetType().Name}", LogCategory.SaveManager, LogLevel.Error);
        Logger.Log($"[SaveManager] WebGL: Stack trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
#else
        Logger.Log($"[SaveManager] CreateSaveSystem failed with exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
#endif
                throw;
            }
        }

        async Task InitializeDependenciesAsync()
        {
            if (InitializationHandler == null)
                InitializationHandler = new InitializationHandler();
            await InitializationHandler.InitializeAsync(this);
        }



        /* ───────────────────────────────────────────────────────────── */
        /*  InitializeSaveSlots                                          */
        /* ───────────────────────────────────────────────────────────── */
        // Add initialization protection at SaveManager level
        private bool slotsInitializing = false;
        
        public Task InitializeSaveSlotsAsync(int numberOfSlots)
        {
            return Operations.RunAsync(async context => { await AwaitOperationReady(context); await InitializeSaveSlotsCoreAsync(numberOfSlots); });
        }

        internal async Task InitializeSaveSlotsCoreAsync(int numberOfSlots)
        {
            if (numberOfSlots <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfSlots));
            if (slotsInitializing) throw new InvalidOperationException("Slot initialization is already running.");
            slotsInitializing = true;
            var context = SaveIoScope.Current.Value;
            try
            {
                var candidate = new SaveSlotManager(saveSystem, saveSettings, screenshotManager, _rootPath, this);
                var initialize = candidate.InitializeAsync(numberOfSlots);
                if (context != null) await context.WaitAsync(initialize); else await initialize;
                context?.CheckCancellation();
                if (IsShuttingDown || this == null) throw new InvalidOperationException("SaveManager is unavailable.");
                SlotManager = candidate;
                saveSlots.Clear(); saveSlots.AddRange(candidate.ManagedSlots);
                lock (loadLocksLock)
                {
                    loadLocks.Clear();
                    for (int i = 1; i <= numberOfSlots; i++) loadLocks.Add(i, false);
                }
                AreSaveSlotsReady = true;
                NotifyOperation(() => SaveEventDispatch.Raise(OnSaveSlotsUpdated));
                NotifyOperation(() => SaveEventDispatch.Raise(SaveSlotsInitialized));
            }
            finally { slotsInitializing = false; }
        }

        public Task InitializeQuickSaveSlotsAsync(int numberOfSlots)
            => QueueRotatingSlotInitializationAsync(numberOfSlots, false);

        public Task InitializeAutoSaveSlotsAsync(int numberOfSlots)
            => QueueRotatingSlotInitializationAsync(numberOfSlots, true);

        private Task QueueRotatingSlotInitializationAsync(int numberOfSlots, bool automatic)
            => Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                await InitializeRotatingSlotsCoreAsync(numberOfSlots, automatic);
            });

        private async Task InitializeRotatingSlotsCoreAsync(int numberOfSlots, bool automatic)
        {
            if (numberOfSlots < 0) throw new ArgumentOutOfRangeException(nameof(numberOfSlots));
            var candidate = new SaveSlotManager(saveSystem, saveSettings, screenshotManager, _rootPath, this);
            int start = (automatic ? saveSettings.autoSaveSlotOffset : saveSettings.quickSaveSlotOffset) + 1;
            await AwaitOperationTask(candidate.InitializeStrictAsync(numberOfSlots, start));
            CheckOperationCancellation();
            if (IsShuttingDown || this == null) throw new InvalidOperationException("SaveManager is unavailable.");

            // Publish the complete candidate without yielding. Until this point the
            // previous history and readiness remain authoritative for every caller.
            if (automatic)
            {
                AutoSlotManager = candidate;
                autoSaveSlots.Clear();
                autoSaveSlots.AddRange(candidate.ManagedSlots);
                AreAutoSlotsReady = true;
                NotifyOperation(() => SaveEventDispatch.Raise(OnAutoSlotsUpdated));
                NotifyOperation(() => SaveEventDispatch.Raise(AutoSlotsInitialized));
            }
            else
            {
                QuickSlotManager = candidate;
                quickSaveSlots.Clear();
                quickSaveSlots.AddRange(candidate.ManagedSlots);
                AreQuickSlotsReady = true;
                NotifyOperation(() => SaveEventDispatch.Raise(OnQuickSlotsUpdated));
                NotifyOperation(() => SaveEventDispatch.Raise(QuickSlotsInitialized));
            }
        }

        private Task EnsureRotatingSlotsReadyAsync(bool automatic)
        {
            if (automatic ? AreAutoSlotsReady && AutoSlotManager != null : AreQuickSlotsReady && QuickSlotManager != null)
                return Task.CompletedTask;
            int count = automatic ? saveSettings.numberOfAutoSaveSlots : saveSettings.numberOfQuickSaveSlots;
            return InitializeRotatingSlotsCoreAsync(count, automatic);
        }

        private void PublishSystemSlot(SaveSlot slot)
        {
            EnsurePrimarySlotExists(slot.SlotNumber);
            SlotManager.PublishSlot(slot);
            SyncSlotToInternalList(slot);
        }

        private async Task<List<PreparedSaveWrite>> PrepareHistoryAsync(SaveOperationContext context, SaveSlotManager history, int offset, int limit)
        {
            var writes = new List<PreparedSaveWrite>();
            for (int i = limit; i > 1; i--)
            {
                var source = history.GetByNumber(offset + i - 1);
                if (source == null || source.LastSaved == DateTime.MinValue) continue;
                source = CopySlot(source);
                byte[] data;
                if (saveSettings.enableCloudSave)
                {
                    var candidate = await SaveOperations.ResolveHistoryCandidateAsync(context, source);
                    if (candidate == null) continue;
                    data = candidate.Payload;
                    if (candidate.Metadata != null) source = CopySlot(candidate.Metadata);
                }
                else data = saveSystem.Load(source);
                if (data == null || data.Length == 0) continue;
                context.CheckCancellation();
                var destination = CopySlot(source);
                destination.SlotNumber = offset + i;
                if (saveSettings.enableScreenshots && !string.IsNullOrEmpty(source.ScreenshotFileName))
                    destination.ScreenshotFileName = await context.WaitAsync(screenshotManager.PrepareScreenshotCopyAsync(source.ScreenshotFileName, destination.SlotNumber));
                var plain = await context.WaitAsync(SaveOperations.DecryptFromCloudAsync(data));
                var snapshot = serializer.DeserializeSaveDataExact(SaveOperations.MaybeDecompress(plain));
                ValidateCandidate(snapshot);
                // Screenshot paths are part of the saved generation as well as its header.
                if (destination.ScreenshotFileName != source.ScreenshotFileName)
                {
                    snapshot.ScreenshotFileName = destination.ScreenshotFileName;
                    data = await context.WaitAsync(SaveOperations.EncryptForSaveAsync(SaveOperations.MaybeCompress(serializer.Serialize(snapshot))));
                }
                writes.Add(new PreparedSaveWrite(data, destination, () =>
                {
                    var existing = history.GetByNumber(destination.SlotNumber);
                    string previousScreenshot = existing?.ScreenshotFileName;
                    history.AppendHistorySlot(destination);
                    PublishSystemSlot(destination);
                    CleanupCommittedScreenshot(previousScreenshot, destination.ScreenshotFileName);
                }));
            }
            return writes;
        }

        /// <summary>
        /// Dynamically changes the number of save slots at runtime.
        /// This reinitializes the save slot system and all dependent managers.
        /// Existing save data on disk is preserved and will be reloaded.
        /// </summary>
        /// <param name="numberOfSlots">The new number of save slots. Must be greater than 0.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task SetSaveSlotCountAsync(int numberOfSlots) => Operations.RunAsync(async context => { await AwaitOperationReady(context); await SetSaveSlotCountCoreAsync(numberOfSlots); });

        internal Task RestoreSaveSlotCountAsync(int count) => Operations.Active != null ? SetSaveSlotCountCoreAsync(count) : SetSaveSlotCountAsync(count);

        private async Task SetSaveSlotCountCoreAsync(int numberOfSlots)
        {
            if (numberOfSlots <= 0)
            {
                Logger.Log($"[SaveManager] SetSaveSlotCountAsync: Number of slots must be greater than 0. Provided: {numberOfSlots}", LogCategory.SaveManager, LogLevel.Warning);
                return;
            }

            Logger.Log($"[SaveManager] SetSaveSlotCountAsync: Changing slot count to {numberOfSlots}", LogCategory.SaveManager, LogLevel.Info);

            // Reinitialize the slots
            await InitializeSaveSlotsCoreAsync(numberOfSlots);

            // Reinitialize SaveOperationService with the new SlotManager
            if (saveOperationService != null && SlotManager != null)
            {
                saveOperationService = new SaveOperationService(this, screenshotManager, serializer, SlotManager, GetComponent<LiveConflictResolver>());
                SaveOperations = saveOperationService;
                Logger.Log($"[SaveManager] SetSaveSlotCountAsync: SaveOperationService reinitialized", LogCategory.SaveManager, LogLevel.Info);
            }

            Logger.Log($"[SaveManager] SetSaveSlotCountAsync: Successfully changed slot count to {numberOfSlots}", LogCategory.SaveManager, LogLevel.Info);
        }

        /// <summary>
        /// Gets the current number of save slots at runtime.
        /// This reflects the actual slot count, which may differ from SaveSettings
        /// if SetSaveSlotCountAsync has been called.
        /// </summary>
        public int CurrentSaveSlotCount => saveSlots?.Count ?? 0;

        /// <summary>
        /// Increases the number of save slots by one.
        /// If there are currently 2 slots, after calling this method there will be 3.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task AddSaveSlotAsync()
        {
            await Operations.RunAsync(async context => { await AwaitOperationReady(context); await SetSaveSlotCountCoreAsync(CurrentSaveSlotCount + 1); });
        }

        /// <summary>
        /// Decreases the number of save slots by one.
        /// If there are currently 5 slots, after calling this method there will be 4.
        /// Ensures at least 1 slot remains.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RemoveSaveSlotAsync()
        {
            await Operations.RunAsync(async context => { await AwaitOperationReady(context); if (CurrentSaveSlotCount > 1) await SetSaveSlotCountCoreAsync(CurrentSaveSlotCount - 1); });
        }

        /// <summary>
        /// Updates a slot in the internal saveSlots list to keep it synchronized with SlotManager.
        /// Call this after any operation that modifies slot metadata (save, rename, delete, etc.)
        /// </summary>
        internal void SyncSlotToInternalList(SaveSlot updatedSlot)
        {
            if (updatedSlot == null) return;
            void Update(List<SaveSlot> list, bool add)
            {
                if (list == null) return;
                var existing = list.FirstOrDefault(slot => slot.SlotNumber == updatedSlot.SlotNumber);
                if (existing != null)
                {
                    SaveSlotManager.CopyPublishedMetadata(updatedSlot, existing);
                }
                else if (add) { list.Add(updatedSlot); list.Sort((a, b) => a.SlotNumber.CompareTo(b.SlotNumber)); }
            }
            Update(saveSlots, true);
            SlotManager?.SynchronizeSlot(updatedSlot, true);
            QuickSlotManager?.SynchronizeSlot(updatedSlot, false);
            AutoSlotManager?.SynchronizeSlot(updatedSlot, false);
        }

        #region Public Accessors

        /// <summary>
        /// Returns the list of Build Scene Names.
        /// </summary>
        public List<string> GetBuildSceneNames()
        {
            return buildSceneNames.ToList();
        }

        /// <summary>
        /// Returns the list of Destroyed GameObject IDs.
        /// </summary>
        public List<string> GetDestroyedGameObjectIDs()
        {
            return gameObjectTracker != null ? gameObjectTracker.GetDestroyedGameObjectIDs() : new List<string>();
        }

        /// <summary>
        /// Returns the dictionary of Tracked GameObjects.
        /// </summary>
        public Dictionary<string, TrackedGameObject> GetTrackedGameObjects()
        {
            return gameObjectTracker != null ? gameObjectTracker.GetTrackedGameObjects() : new Dictionary<string, TrackedGameObject>();
        }

        #endregion

        #region Supabase Integration

        public Task RefreshRemoteSlotsAsync() => QueueSlotRefreshAsync(false, CancellationToken.None, null);

        public Task RefreshRemoteSlotsAsync(CancellationToken cancellationToken, TimeSpan? timeout = null)
            => QueueSlotRefreshAsync(false, cancellationToken, timeout);

        void OnSupabaseLoggedIn(string uid)
        {
            if (cloudSaveService != null)
                cloudSaveService.OnSupabaseLoggedIn(uid);
        }

        public void LogoutFromSupabase()
        {
            cloudSaveService?.LogoutFromSupabase();
        }

        #endregion // Supabase Integration

        #region Unity Cloud Sign-In
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT

        private string _cachedEmail;
        private string _cachedPassword;
        public Task<bool> SignInUsernamePasswordAsync(
        string email, string password, bool createAccount = true)
        {
            return cloudSaveService != null
                ? cloudSaveService.SignInUsernamePasswordAsync(email, password, createAccount)
                : Task.FromResult(false);
        }
        private void OnCloudSignedIn()
        {
            if (cloudSaveService != null)
                _ = cloudSaveService.OnCloudSignedIn();
        }

        /// <summary>
        /// Manually signs the player in to Unity Cloud Save.
        /// Call this when <c>SaveSettings.autoCloudSignIn</c> is <b>false</b>
        /// and you want to trigger the login from a custom UI.
        /// Returns <c>true</c> if the player ends up signed-in; <c>false</c> otherwise.
        /// </summary>
        /// <param name="overrideProvider">
        /// Optional provider to use just for this call, ignoring the
        /// <c>defaultAuthProvider</c> that is stored in <c>SaveSettings</c>.
        /// Pass <c>null</c> to respect the default setting.
        /// </param>
        public Task<bool> SignInToCloudAsync(AuthProvider? overrideProvider = null)
        {
            return cloudSaveService != null
                ? cloudSaveService.SignInToCloudAsync(overrideProvider)
                : Task.FromResult(false);
        }

        public Task<bool> UseEmailLoginAsync(string email, string password)
        {
            return cloudSaveService != null
                ? cloudSaveService.UseEmailLoginAsync(email, password)
                : Task.FromResult(false);
        }

        public Task<bool> LinkIdentityAsync(AuthProvider provider)
        {
            return cloudSaveService != null
                ? cloudSaveService.LinkIdentityAsync(provider)
                : Task.FromResult(false);
        }

        private Task InitializeCloudServicesAsync()
        {
            return cloudSaveService != null
                ? cloudSaveService.InitializeCloudServicesAsync()
                : Task.CompletedTask;
        }
#endif
        #endregion // Unity Cloud Sign-In

        #region MySQL Authentication

        public Task<bool> MySqlSignUpAsync(string username, string password)
        {
            return saveSystem is MySqlSaveSystem mysql
                ? mysql.SignUpAsync(username, password)
                : Task.FromResult(false);
        }

        public Task<bool> MySqlSignInAsync(string username, string password)
        {
            return saveSystem is MySqlSaveSystem mysql
                ? mysql.LoginAsync(username, password)
                : Task.FromResult(false);
        }

        #endregion // MySQL Authentication

        /// <summary>
        /// Loads SaveSettings using the AssetProvider.
        /// </summary>
        internal void LoadSaveSettings()
        {
            saveSettings = AssetProvider.Load<SaveSettings>("SaveSettings");

            if (saveSettings == null)
            {
                Logger.Log("SaveManager: Failed to load SaveSettings. Please complete the Settings Wizard via Tools > Crystal Save > Settings Wizard, or import demo settings via Tools > Crystal Save > Settings > Install Demo Settings.", LogCategory.SaveManager, LogLevel.Error);
            }
            else
            {
#if MEMORYPACK && ARAWN_REMEMBERME
#if REMEMBERME_ADDRESSABLES_PRESENT
                AssetProvider.UseAddressables = saveSettings.useAddressables;
#else
                if (saveSettings.useAddressables)
                    Logger.Log(
                        "Unity Addressables package not installed. Falling back to Resources.",
                        LogCategory.SaveManager,
                        LogLevel.Warning);
                AssetProvider.UseAddressables = false;
#endif
#endif
            }

        }

        /// <summary>
        /// Initializes save slots based on the number specified in SaveSettings.
        /// </summary>
        /// <param name="numberOfSlots">Number of save slots to initialize.</param>
        private async void InitializeSaveSlots(int numberOfSlots)
        {
            saveSlots.Clear();
            lock (loadLocksLock)
            {
                loadLocks.Clear();
                for (int i = 1; i <= numberOfSlots; i++)
                {
                    loadLocks.Add(i, false); // Initialize all slots as not loading
                }
            }

            for (int i = 1; i <= numberOfSlots; i++)
            {
                // Load the slot from disk/PlayerPrefs if it exists
                SaveSlot loadedSlot = await saveSystem.LoadSlotMetadataAsync(i);

                if (loadedSlot != null)
                {
                    saveSlots.Add(loadedSlot);
                }
                else
                {
                    // Create an empty slot if no file/PlayerPrefs data is found
                    saveSlots.Add(new SaveSlot(
                        slotNumber: i,
                        slotName: $"Slot {i}",
                        lastSaved: DateTime.MinValue,
                        screenshotFileName: "",
                        lastActiveScene: ""
                    ));
                }
            }

            OnSaveSlotsUpdated?.Invoke();
        }

        /// <summary>
        /// Caches scene names from build settings for validation purposes.
        /// </summary>
        internal void InitializeBuildSceneNames()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                buildSceneNames.Add(sceneName);
            }

            Logger.Log($"SaveManager: Cached {buildSceneNames.Count} scenes from build settings.", LogCategory.SaveManager, LogLevel.Info);
        }

        #endregion

        #region Scene Management

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        private async void OnEnable()
        {
            SubscribeSceneEvents();
            SubscribeSupabaseRuntimeHooks();

            /* ─── Unity Cloud Save (UGS) ───────────────────── */
            await EnsureUnityServicesInitializedAsync();
            if (!this || !isActiveAndEnabled)
                return;

            SubscribeCloudSignedIn();
        }
#else
        private void OnEnable()
        {
            SubscribeSceneEvents();
            SubscribeSupabaseRuntimeHooks();
        }
#endif

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        private void OnDisable()
        {
            UnsubscribeRuntimeEvents();
        }
#else
        private void OnDisable()
        {
            UnsubscribeRuntimeEvents();
        }
#endif

        private void SubscribeSceneEvents()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void UnsubscribeSceneEvents()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void SubscribeSupabaseRuntimeHooks()
        {
            UnsubscribeSupabaseRuntimeHooks();

            if (saveSettings == null || saveSettings.backend != SaveBackend.Supabase)
                return;

            if (cloudSaveService != null)
            {
                SubscribeSupabaseLoginRelay();
            }
            else
            {
                SubscribeInitializedForSupabase();
            }
        }

        private void SubscribeSupabaseLoginRelay()
        {
            SupabaseAuthRelay.LoggedIn -= OnSupabaseLoggedIn;
            SupabaseAuthRelay.LoggedIn += OnSupabaseLoggedIn;
        }

        private void SubscribeInitializedForSupabase()
        {
            Initialized -= SubscribeToSupabaseLogin;
            Initialized += SubscribeToSupabaseLogin;
        }

        private void UnsubscribeInitializedForSupabase()
        {
            Initialized -= SubscribeToSupabaseLogin;
        }

        private void UnsubscribeSupabaseRuntimeHooks()
        {
            SupabaseAuthRelay.LoggedIn -= OnSupabaseLoggedIn;
            UnsubscribeInitializedForSupabase();
        }

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
        private void SubscribeCloudSignedIn()
        {
            if (cloudSaveService == null || UnityServices.State != ServicesInitializationState.Initialized)
                return;

            Unity.Services.Authentication.AuthenticationService.Instance.SignedIn -= OnCloudSignedIn;
            Unity.Services.Authentication.AuthenticationService.Instance.SignedIn += OnCloudSignedIn;
        }

        private void UnsubscribeCloudSignedIn()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
                Unity.Services.Authentication.AuthenticationService.Instance.SignedIn -= OnCloudSignedIn;
        }
#endif

        private void UnsubscribeRuntimeEvents()
        {
            UnsubscribeSceneEvents();
            UnsubscribeSupabaseRuntimeHooks();
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
            UnsubscribeCloudSignedIn();
#endif
        }

        private void SubscribeToSupabaseLogin(SaveManager _)
        {
            if (saveSettings != null && saveSettings.backend == SaveBackend.Supabase && cloudSaveService != null)
            {
                SubscribeSupabaseLoginRelay();
                UnsubscribeInitializedForSupabase();
            }
        }

        /// <summary>
        /// Handles registration when new scenes are loaded.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsInitialized)
                return;

            if (gameObjectTracker == null)
            {
                Logger.Log("SaveManager: Scene loaded but GameObjectTracker is null. Skipping registration.", LogCategory.SaveManager, LogLevel.Warning);
                return;
            }

            foreach (var go in scene.GetRootGameObjects())
            {
                RegisterGameObjectRecursive(go);
            }

            // Register SaveableComponents on disabled GameObjects if enabled
            if (saveSettings != null && 
                saveSettings.registerDisabledComponents && 
                saveSettings.disabledComponentScanMode == DisabledComponentScanMode.OnSceneLoad)
            {
                if (saveSettings.scanOnlyActiveScene)
                {
                    RegisterExistingSaveableComponentsInScene(scene);
                }
                else
                {
                    RegisterExistingSaveableComponents();
                }
	            }

	            prefabManager?.ResolveKeepAcrossSceneDuplicatesForLoadedScene(scene.name);
	            prefabManager?.ProcessPendingPrefabs(scene.name, gameObjectTracker.GetDestroyedGameObjectIDs());
	        }

        private void OnSceneUnloaded(Scene scene)
        {
            // Nothing to do here; we snapshot the old scene during activeSceneChanged
            // to ensure objects still exist during capture.
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            try
            {
                // When a managed scene transition is in progress (snapshot-based switch),
                // PopulatePendingPrefabsFromSnapshotAsync has already captured the scene
                // snapshot.  Re-snapshotting here would ClearSceneSnapshot first and then
                // try to capture from root objects that are already destroyed, wiping the
                // correct data.
                if (isInSceneTransition)
                    return;

                if (componentManager != null && oldScene.IsValid())
                    componentManager.SnapshotSceneAll(oldScene);
            }
            catch (Exception ex)
            {
                Logger.Log($"OnActiveSceneChanged snapshot failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Warning);
            }
        }

        /// <summary>
        /// Registers all existing GameObjects with RememberGameObject component.
        /// </summary>
        private void RegisterExistingGameObjects()
        {
            var rememberObjects = UnityObjectSearch.FindIncludingInactive<RememberGameObject>();
            int batchSize = saveSettings != null ? saveSettings.existingObjectScanBatchSize : 0;

            if (batchSize <= 0)
            {
                foreach (var rememberGO in rememberObjects)
                {
                    var go = rememberGO.gameObject;
                    string uniqueID = GetUniqueID(go);
                    if (!gameObjectTracker.IsGameObjectDestroyed(uniqueID))
                    {
                        RegisterGameObject(go, rememberGO.PropertySettings);
                        // Ensure cache is populated for all objects during initial scan
                        // This is critical when enableLookupCache is true to prevent first-load issues
                        CacheGameObject(uniqueID, go);
                    }
                    else
                    {
                        Logger.Log($"SaveManager: Skipping registration of '{go.name}' as it is marked as destroyed.", LogCategory.SaveManager, LogLevel.Info);
                    }
                }
            }
            else
            {
                StartCoroutine(RegisterExistingGameObjectsCoroutine(rememberObjects, batchSize));
            }

        }

        private IEnumerator RegisterExistingGameObjectsCoroutine(RememberGameObject[] rememberObjects, int batchSize)
        {
            int total = rememberObjects.Length;
            int index = 0;

            while (index < total)
            {
                while (StateMachine.CurrentState != SaveState.Idle)
                {
                    yield return null;
                }

                int end = Math.Min(index + batchSize, total);
                for (; index < end; index++)
                {
                    var rememberGO = rememberObjects[index];
                    var go = rememberGO.gameObject;
                    string uniqueID = GetUniqueID(go);
                    if (!gameObjectTracker.IsGameObjectDestroyed(uniqueID))
                    {
                        RegisterGameObject(go, rememberGO.PropertySettings);
                        // Ensure cache is populated for all objects during initial scan
                        // This is critical when enableLookupCache is true to prevent first-load issues
                        CacheGameObject(uniqueID, go);
                    }
                    else
                    {
                        Logger.Log($"SaveManager: Skipping registration of '{go.name}' as it is marked as destroyed.", LogCategory.SaveManager, LogLevel.Info);
                    }
                }

                yield return null;
            }
        }

        /// <summary>
        /// Registers all existing <see cref="SaveableComponent"/> instances,
        /// including those on inactive GameObjects.
        /// </summary>
        internal void RegisterExistingSaveableComponents()
        {
            var allComponents = UnityObjectSearch.FindIncludingInactive<SaveableComponent>();
            int registeredCount = 0;
            foreach (var comp in allComponents)
            {
                if (componentManager.EnsureSaveableComponentRegistered(comp, "ExistingSceneScan"))
                    registeredCount++;
            }
            if (registeredCount > 0)
            {
                Logger.Log($"[SaveManager] Registered {registeredCount} SaveableComponent(s) on inactive GameObjects.", LogCategory.SaveManager, LogLevel.Info);
            }
        }

        /// <summary>
        /// Registers all <see cref="SaveableComponent"/> instances in a specific scene,
        /// including those on inactive GameObjects.
        /// </summary>
        /// <param name="scene">The scene to scan for components.</param>
        internal void RegisterExistingSaveableComponentsInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            int registeredCount = 0;
            var components = new List<SaveableComponent>();
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                components.Clear();
                rootGO.GetComponentsInChildren(true, components);
                foreach (var comp in components)
                {
                    if (componentManager.EnsureSaveableComponentRegistered(comp, "ExistingSceneScan"))
                        registeredCount++;
                }
            }
            if (registeredCount > 0)
            {
                Logger.Log($"[SaveManager] Registered {registeredCount} SaveableComponent(s) on inactive GameObjects in scene '{scene.name}'.", LogCategory.SaveManager, LogLevel.Info);
            }
        }

        /// <summary>
        /// Recursively registers GameObjects and their children.
        /// </summary>
        /// <param name="go">The root GameObject to register.</param>
        private void RegisterGameObjectRecursive(GameObject go)
        {
            if (gameObjectTracker == null)
            {
                return;
            }
            var rememberGO = go.GetComponent<RememberGameObject>();
            var rememberTF = go.GetComponent<RememberTransform>();
            var saveablePrefab = go.GetComponent<SaveablePrefab>();
            // If this subtree opts into batched registration, do a single sweep and spread work over frames
            if ((rememberGO != null && rememberGO.EnableBatchRegistration) ||
                (rememberTF != null && rememberTF.EnableBatchRegistration) ||
                (saveablePrefab != null && saveablePrefab.EnableBatchRegistration))
            {
                StartCoroutine(BatchRegisterCoroutine(go));
                return;
            }
            if (rememberGO != null)
            {
                string uniqueID = GetUniqueID(go);
                if (!string.IsNullOrEmpty(uniqueID) && !gameObjectTracker.IsTracked(uniqueID))
                {
                    RegisterGameObject(go, rememberGO.PropertySettings);
                }
            }

            foreach (Transform child in go.transform)
            {
                RegisterGameObjectRecursive(child.gameObject);
            }
        }

        private IEnumerator BatchRegisterCoroutine(GameObject root)
        {
            // Gather all RememberGameObject components in one native call
            var rememberAll = root.GetComponentsInChildren<RememberGameObject>(includeInactive: true);
            const int batchSize = 64; // spread registration across frames to avoid spikes
            int count = 0;
            foreach (var rg in rememberAll)
            {
                if (rg == null) continue;
                var go = rg.gameObject;
                string uniqueID = GetUniqueID(go);
                if (!string.IsNullOrEmpty(uniqueID) && !gameObjectTracker.IsTracked(uniqueID))
                {
                    RegisterGameObject(go, rg.PropertySettings);
                }
                if (++count % batchSize == 0)
                    yield return null; // yield to next frame
            }
        }

        #endregion

        #region GameObject Tracking and Restoration
        /// <summary>
        /// Legacy: restore from CurrentSaveData (fire-and-forget).
        /// </summary>
        public void RestoreDestroyedGameObject(string uniqueID)
        {
            RestoreDestroyedGameObject(uniqueID, null);
        }

        /// <summary>
        /// Legacy: restore all from CurrentSaveData.
        /// </summary>
        public void RestoreAllDestroyedGameObjects()
        {
            RestoreAllDestroyedGameObjects(null);
        }

        /// <summary>
        /// Core: restore the given destroyed object from the provided SaveData (or CurrentSaveData if null).
        /// For scene objects (with UniqueID component), reactivates the existing object.
        /// For instantiated prefabs (with SaveablePrefab component), instantiates from prefab mapping.
        /// </summary>
        public void RestoreDestroyedGameObject(string uniqueID, SaveData data = null)
            => ObserveTargetedRestore(RestoreDestroyedGameObjectAsync(uniqueID, data));

        /// <summary>Restores a destroyed object and reports completed component application.</summary>
        public Task<bool> RestoreDestroyedGameObjectAsync(string uniqueID, SaveData data = null)
            => QueueDestroyedRestoreAsync(uniqueID, data, null, 1, 0);

        public Task<bool> RestoreDestroyedGameObjectWithRetryAsync(string uniqueID, int slotNumber,
            int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(uniqueID, null, slotNumber, maxRetries, retryDelayMs);

        public void RestoreDestroyedGameObject(string uniqueID, int slotNumber)
            => ObserveTargetedRestore(RestoreDestroyedGameObjectWithRetryAsync(uniqueID, slotNumber));

        public void RestoreAllDestroyedGameObjects(SaveData data = null)
            => ObserveTargetedRestore(QueueDestroyedRestoreAsync(null, data, null, 1, 0, all: true));

        /// <summary>Returns whether the request was queued. Use the async counterpart to await its outcome.</summary>
        public bool RestoreAllDestroyedGameObjectsFromCurrentData()
        {
            if (CurrentSaveData == null) return false;
            RestoreAllDestroyedGameObjects(CurrentSaveData);
            return true;
        }

        public Task<bool> RestoreAllDestroyedGameObjectsFromCurrentDataWithRetryAsync(int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(null, null, null, maxRetries, retryDelayMs, all: true);

        public Task<bool> RestoreAllDestroyedGameObjectsWithRetryAsync(int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(null, null, slotNumber, maxRetries, retryDelayMs, all: true);

        public void RestoreAllDestroyedGameObjects(int slotNumber)
            => ObserveTargetedRestore(RestoreAllDestroyedGameObjectsWithRetryAsync(slotNumber));

        public void RestoreDestroyedPrefab(string uniqueID, SaveData data = null)
            => ObserveTargetedRestore(RestoreDestroyedPrefabAsync(uniqueID, data));

        public Task<bool> RestoreDestroyedPrefabAsync(string uniqueID, SaveData data = null)
            => QueueDestroyedRestoreAsync(uniqueID, data, null, 1, 0, prefabsOnly: true);

        public void RestoreDestroyedPrefabByAssetID(string prefabAssetID, SaveData data = null)
            => ObserveTargetedRestore(QueueDestroyedRestoreAsync(prefabAssetID, data, null, 1, 0, prefabsOnly: true, byAsset: true));

        public void RestoreAllDestroyedPrefabs(SaveData data = null)
            => ObserveTargetedRestore(QueueDestroyedRestoreAsync(null, data, null, 1, 0, prefabsOnly: true, all: true));

        /// <summary>Returns whether the request was queued. Use the async counterpart to await its outcome.</summary>
        public bool RestoreAllDestroyedPrefabsFromCurrentData()
        {
            if (CurrentSaveData == null) return false;
            RestoreAllDestroyedPrefabs(CurrentSaveData);
            return true;
        }

        public Task<bool> RestoreAllDestroyedPrefabsFromCurrentDataWithRetryAsync(int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(null, null, null, maxRetries, retryDelayMs, prefabsOnly: true, all: true);

        public Task<bool> RestoreDestroyedPrefabWithRetryAsync(string uniqueID, int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(uniqueID, null, slotNumber, maxRetries, retryDelayMs, prefabsOnly: true);

        public Task<bool> RestoreDestroyedPrefabByAssetIDWithRetryAsync(string prefabAssetID, int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(prefabAssetID, null, slotNumber, maxRetries, retryDelayMs, prefabsOnly: true, byAsset: true);

        public void RestoreDestroyedPrefab(string uniqueID, int slotNumber)
            => ObserveTargetedRestore(RestoreDestroyedPrefabWithRetryAsync(uniqueID, slotNumber));

        public void RestoreDestroyedPrefabByAssetID(string prefabAssetID, int slotNumber)
            => ObserveTargetedRestore(RestoreDestroyedPrefabByAssetIDWithRetryAsync(prefabAssetID, slotNumber));

        public Task<bool> RestoreAllDestroyedPrefabsWithRetryAsync(int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => QueueDestroyedRestoreAsync(null, null, slotNumber, maxRetries, retryDelayMs, prefabsOnly: true, all: true);

        public void RestoreAllDestroyedPrefabs(int slotNumber)
            => ObserveTargetedRestore(RestoreAllDestroyedPrefabsWithRetryAsync(slotNumber));

        /// <summary>
        /// Registers a GameObject for tracking based on its UniqueID and settings.
        /// <param name="obj">The GameObject to track.</param>
        /// <param name="settings">Settings indicating which properties to save.</param>
        /// </summary>
        public void RegisterGameObject(GameObject obj, GameObjectPropertySettings settings)
        {
            gameObjectTracker?.RegisterGameObject(obj, settings);
        }

        /// <summary>
        /// Unregisters a GameObject from the save system based on its UniqueID.
        /// </summary>
        /// <param name="obj">The GameObject to unregister.</param>
        public void UnregisterGameObject(GameObject obj)
        {
            gameObjectTracker?.UnregisterGameObject(obj);
        }

        /// destroying the object itself, so pooled instances survive a despawn.
        /// Safe to call multiple times on the same instance.
        ///
        ///  • Skips helper clones that were never assigned a runtime UniqueID.
        ///  • Clears all SaveManager dictionaries.
        ///  • Removes the TrackedGameObjectProxy (but keeps the GameObject).
        ///  • Updates SaveablePrefab’s internal “registered” latch.
        ///  • Drops the prefab entry from PrefabManager so OnDestroy() stays silent.
        ///  • Resets the prefab for pooling (UniqueID = "", flags = false).
        public void SoftUnregisterGameObject(GameObject obj)
        {
            gameObjectTracker?.SoftUnregisterGameObject(obj);
        }

        internal void SoftUnregisterGameObjectForPrefabBatch(GameObject obj)
        {
            gameObjectTracker?.SoftUnregisterGameObjectForPrefabBatch(obj);
        }

        /// <summary>
        /// Captures component data and registers a GameObject as destroyed,
        /// then optionally destroys or deactivates the object.
        /// </summary>
        /// <param name="obj">The GameObject to destroy or deactivate.</param>
        /// <param name="destroy">If true, the object is destroyed; otherwise it is deactivated.</param>
        /// <param name="allowPooling">
        /// When true (default) the method will return eligible SaveablePrefabs to their pool
        /// instead of destroying them outright when global prefab pooling is enabled and the
        /// prefab has not opted out via the registry.
        /// </param>
        public void DestroyWithSnapshot(GameObject obj, bool destroy = true, bool allowPooling = true)
        {
            if (obj == null) return;

            string uniqueID = GetUniqueID(obj);
            string resolvedID = uniqueID;

            resolvedID = gameObjectTracker?.GetTrackedIDForObject(obj) ?? resolvedID;

            if (!string.IsNullOrEmpty(resolvedID))
            {
                CaptureDestroyedDataIfPossible(resolvedID);
                RegisterDestroyedGameObject(resolvedID);
            }

            if (destroy)
            {
                if (allowPooling && TryReturnPrefabToPool(obj))
                    return;

                DestroyHelper.DestroyWithLogging(obj, "SaveManager.DestroyWithSnapshot");
            }
            else
            {
                obj.SetActive(false);
            }
        }

        private bool TryReturnPrefabToPool(GameObject obj)
        {
            if (obj == null)
                return false;

            var settings = SaveSettings;
            if (settings == null || !settings.usePrefabPooling)
                return false;

            if (!obj.TryGetComponent(out SaveablePrefab saveablePrefab) || saveablePrefab == null)
                return false;

            if (string.IsNullOrEmpty(saveablePrefab.PrefabAssetID))
                return false;

            PrefabRegistry prefabRegistry = null;

            try
            {
                prefabRegistry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
            }
            catch (Exception ex)
            {
                Logger.Log($"SaveManager.DestroyWithSnapshot: failed to load PrefabRegistry – {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Warning);
            }

            // Check if pooling is disabled directly on the SaveablePrefab component first
            if (saveablePrefab.DisablePooling)
                return false;

            // If not disabled on component, check the PrefabRegistry setting
            if (prefabRegistry != null && prefabRegistry.IsPoolingDisabled(saveablePrefab.PrefabAssetID))
                return false;

            int defaultPoolSize = settings.defaultPrefabPoolSize;
            int poolSize = prefabRegistry?.ResolvePoolSize(saveablePrefab.PrefabAssetID, defaultPoolSize)
                            ?? defaultPoolSize;

            return SaveablePrefabPoolCache.TryDespawn(
                saveablePrefab,
                poolSize,
                saveablePrefab.RegisterWithSaveSystem);
        }

        /// <summary>
        /// Registers the destruction of a GameObject.
        /// </summary>
        /// <param name="uniqueID">The UniqueID of the destroyed GameObject.</param>
        internal bool TryRegisterDestroyedGameObjectFromOwner(
            string uniqueID,
            GameObject destroyedOwner)
        {
            if (string.IsNullOrEmpty(uniqueID))
                return false;

            // Object destruction is deferred until the end of the frame. A clearing load
            // can install a replacement under the same ID before the old components receive
            // OnDestroy. Never let those stale callbacks destroy the replacement's identity.
            bool hasDifferentOwner = gameObjectTracker != null &&
                                     gameObjectTracker.HasDifferentLiveOwner(
                                         uniqueID,
                                         destroyedOwner);

            if (!hasDifferentOwner &&
                prefabManager != null &&
                prefabManager.TryGetInstantiatedPrefab(
                    uniqueID,
                    out GameObject prefabOwner) &&
                !IsSameGameObjectInstance(prefabOwner, destroyedOwner))
            {
                hasDifferentOwner = true;
            }

            if (!hasDifferentOwner &&
                TryGetCachedGameObject(uniqueID, out GameObject cachedOwner) &&
                !IsSameGameObjectInstance(cachedOwner, destroyedOwner))
            {
                hasDifferentOwner = true;
            }

            if (hasDifferentOwner)
                return false;

            RegisterDestroyedGameObject(uniqueID);
            return gameObjectTracker == null ||
                   gameObjectTracker.IsGameObjectDestroyed(uniqueID);
        }

        public void RegisterDestroyedGameObject(string uniqueID)
        {
            string incomingId = uniqueID;
            bool hasValidIncomingId = !string.IsNullOrEmpty(incomingId);
            string incomingLogId = hasValidIncomingId ? incomingId : "<null or empty>";

            if (!hasValidIncomingId)
            {
                Logger.Log($"SaveManager: Attempted to register an invalid destroyed ID '{incomingLogId}' (hasValidId: {hasValidIncomingId}).", LogCategory.SaveManager, LogLevel.Warning);
                return;
            }

            if (CurrentSaveData == null)
            {
                CurrentSaveData = new SaveData();
            }

            string trackerKey;
            TrackedGameObject trackedGameObject;
            bool foundInTracker = TryGetTrackedGameObjectForIdentifier(incomingId, out trackerKey, out trackedGameObject);
            GameObject trackedObject = foundInTracker && trackedGameObject?.GameObject != null ? trackedGameObject.GameObject : null;

            string canonicalId = DeriveCanonicalDestroyedIdentifier(trackedObject, trackerKey, incomingId);
            bool idsDiffer = !string.Equals(canonicalId, incomingId, StringComparison.Ordinal);
            string canonicalLogId = string.IsNullOrEmpty(canonicalId) ? "<null or empty>" : canonicalId;

            if (idsDiffer)
            {
                NormalizeDestroyedDataAliases(incomingId, canonicalId);
            }

            if (idsDiffer) gameObjectTracker?.RemoveDestroyedID(incomingId);

            bool alreadyRegistered = gameObjectTracker != null && gameObjectTracker.IsGameObjectDestroyed(canonicalId);
            if (alreadyRegistered)
            {
                Logger.Log($"SaveManager: Attempted to register an invalid or already destroyed ID (incoming: '{incomingLogId}', canonical: '{canonicalLogId}', hasValidId: {hasValidIncomingId}, alreadyRegistered: {alreadyRegistered}).", LogCategory.SaveManager, LogLevel.Warning);
                return;
            }

            if (foundInTracker && trackedObject != null)
            {
                Dictionary<string, byte[]> compData = componentManager.CollectComponentDataForObject(trackedObject);
                CurrentSaveData.DestroyedObjectData[canonicalId] = compData;
            }

            gameObjectTracker?.AddDestroyedID(canonicalId);

            if (gameObjectTracker != null)
            {
                gameObjectTracker.TryGetTrackedGameObject(canonicalId, out var _);
                GameObject objectToUnregister = trackedObject ?? FindGameObjectByUniqueID(canonicalId, IdentifierType.UniqueID);
                gameObjectTracker.UnregisterGameObject(objectToUnregister);
            }
        }

        public void CaptureDestroyedDataIfPossible(string uniqueID)
        {
            // Make sure we have a SaveData to store it in
            if (CurrentSaveData == null)
            {
                CurrentSaveData = new SaveData();
                Logger.Log("Created new SaveData for immediate destroyed-object capture.", LogCategory.SaveManager);
            }

            string incomingId = uniqueID;
            string trackerKey;
            TrackedGameObject trackedGameObject;
            bool foundInTracker = TryGetTrackedGameObjectForIdentifier(incomingId, out trackerKey, out trackedGameObject);
            GameObject obj = foundInTracker && trackedGameObject?.GameObject != null ? trackedGameObject.GameObject : null;

            string canonicalId = DeriveCanonicalDestroyedIdentifier(obj, trackerKey, incomingId);
            bool idsDiffer = !string.Equals(canonicalId, incomingId, StringComparison.Ordinal);

            if (idsDiffer)
            {
                NormalizeDestroyedDataAliases(incomingId, canonicalId);
            }

            // If the object is still tracked, gather data
            if (foundInTracker && obj != null)
            {
                // Collect the transform + other "remember" data
                var compData = componentManager.CollectComponentDataForObject(obj);

                // Store it in DestroyedObjectData (so that future calls to `RestoreDestroyedGameObject` have it)
                CurrentSaveData.DestroyedObjectData[canonicalId] = compData;
#if UNITY_EDITOR
                string incomingLogId = string.IsNullOrEmpty(incomingId) ? "<null or empty>" : incomingId;
                string canonicalLogId = string.IsNullOrEmpty(canonicalId) ? "<null or empty>" : canonicalId;
                string idSummary = idsDiffer
                    ? $"incoming ID: {incomingLogId}, canonical ID: {canonicalLogId}"
                    : $"canonical ID: {canonicalLogId}";
                bool componentDataStored = compData != null && compData.Count > 0;
                Logger.Log($"{DebugLogTag} [SaveManager] CaptureDestroyedDataIfPossible captured '{obj.name}' ({idSummary}) at position {obj.transform.position}; component data stored: {componentDataStored} (entries: {compData?.Count ?? 0})", LogCategory.SaveManager, LogLevel.Info);
#endif

                // Additional: if this was a SaveablePrefab, capture its prefab data for later restoration
                var sp = SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedSp) ? cachedSp : obj.GetComponent<SaveablePrefab>();
                if (sp != null && prefabManager != null)
                {
                    var pd = prefabManager.BuildPrefabData(sp);
                    if (pd != null)
                    {
                        CurrentSaveData.Prefabs.RemoveAll(p => p.InstanceID == canonicalId);
                        pd.InstanceID = canonicalId;
                        CurrentSaveData.Prefabs.Add(pd);
#if UNITY_EDITOR
                        Logger.Log($"{DebugLogTag} [SaveManager] CaptureDestroyedDataIfPossible stored SaveablePrefabData for '{obj.name}' (ID: {canonicalId}) pos={pd.Position}, rot={pd.Rotation.eulerAngles}, scale={pd.Scale}.", LogCategory.SaveManager, LogLevel.Info);
#endif
                    }
                }
                else
                {
                    // Scene object without SaveablePrefab: synthesize a transform record so we restore at last position.
                    var tr = obj.transform;
                    string parentId = null;
                    bool isParentSceneObject = false;
                    if (tr != null && tr.parent != null)
                    {
                        var parentGO = tr.parent.gameObject;
                        parentId = GetUniqueID(parentGO);
                        isParentSceneObject = parentGO.GetComponent<SceneObjectID>() != null;
                    }

                    var synthesized = new SaveablePrefabData(
                        instanceID: canonicalId,
                        prefabID: string.Empty,
                        gameObjectName: obj.name,
                        position: tr != null ? tr.position : Vector3.zero,
                        rotation: tr != null ? tr.rotation : Quaternion.identity,
                        scale: tr != null ? tr.localScale : Vector3.one,
                        parentID: parentId,
                        isParentSceneObject: isParentSceneObject,
                        visibilitySettingsData: null,
                        homeScene: null,
                        disablePooling: false
                    );
                    synthesized.HasTransformOverride = true;
                    synthesized.HasParentOverride = !string.IsNullOrEmpty(parentId);

                    CurrentSaveData.Prefabs.RemoveAll(p => p.InstanceID == canonicalId);
                    CurrentSaveData.Prefabs.Add(synthesized);
#if UNITY_EDITOR
                    Logger.Log($"{DebugLogTag} [SaveManager] CaptureDestroyedDataIfPossible stored synthesized transform for '{obj.name}' (ID: {canonicalId}) pos={synthesized.Position}, rot={synthesized.Rotation.eulerAngles}, scale={synthesized.Scale}, parentID='{parentId}'.", LogCategory.SaveManager, LogLevel.Info);
#endif
                }

                Logger.Log($"Captured transform/component data for object '{canonicalId}' before destruction.", LogCategory.SaveManager);
            }
            else
            {
                string incomingLogId = string.IsNullOrEmpty(incomingId) ? "<null or empty>" : incomingId;
                string canonicalLogId = string.IsNullOrEmpty(canonicalId) ? "<null or empty>" : canonicalId;
                string message = idsDiffer
                    ? $"CaptureDestroyedDataIfPossible: incoming '{incomingLogId}' resolved to canonical '{canonicalLogId}', but the object is not tracked or already destroyed."
                    : $"CaptureDestroyedDataIfPossible: '{incomingLogId}' not tracked or object already destroyed.";
                Logger.Log(message, LogCategory.SaveManager, LogLevel.Info);
            }
        }

        /// <summary>
        /// Attempts to retrieve a tracked GameObject by its UniqueID.
        /// </summary>
        /// <param name="uniqueID">The UniqueID of the GameObject.</param>
        /// <param name="tracked">Out parameter for the tracked GameObject.</param>
        /// <returns>True if found; otherwise, false.</returns>
        public bool TryGetTrackedGameObject(string uniqueID, out TrackedGameObject tracked)
        {
            if (gameObjectTracker != null)
                return gameObjectTracker.TryGetTrackedGameObject(uniqueID, out tracked);
            tracked = null;
            return false;
        }

        /// <summary>
        /// Updates the active state of a GameObject.
        /// </summary>
        /// <param name="uniqueID">UniqueID of the GameObject.</param>
        /// <param name="isActive">Current active state.</param>
        public void UpdateActiveState(string uniqueID, bool isActive)
        {
            gameObjectTracker?.UpdateActiveState(uniqueID, isActive);
        }

        /// <summary>
        /// Retrieves the UniqueID of a GameObject by checking specific components.
        /// </summary>
        /// <param name="obj">The GameObject to retrieve the UniqueID from.</param>
        /// <returns>The UniqueID as a string, or null if not found.</returns>
        private string GetUniqueID(GameObject obj)
        {
            // GameObject-level identity must remain consistent regardless of whether any
            // component's Awake has executed yet. RememberGameObject.UniqueIdentifier carries
            // a per-component suffix (`..._<componentGuid>`) and is therefore unsuitable for
            // tracking GameObject-level state (active state, transform, etc.). Prefer the
            // bare UniqueID component value first so a GameObject that starts inactive
            // (Awake never fires before scene-load registration) and one that starts active
            // resolve to the same tracker/active-state key.
            var uidComp = obj.GetComponent<UniqueID>();
            if (uidComp != null && !string.IsNullOrEmpty(uidComp.ID))
                return uidComp.ID;

            // Fall back to RememberGameObject's GameObject-level ID (also bare, no component suffix).
            var rememberGO = obj.GetComponent<RememberGameObject>();
            if (rememberGO != null && !string.IsNullOrEmpty(rememberGO.GameObjectUniqueID))
                return rememberGO.GameObjectUniqueID;

            // Fallback to other identifiers following the priority:
            // 1) SceneObjectID
            // 2) SaveablePrefab (instance ID)
            var sceneId = obj.GetComponent<SceneObjectID>()?.UniqueID;
            if (!string.IsNullOrEmpty(sceneId)) return sceneId;

            var sp = obj.GetComponent<SaveablePrefab>();
            if (sp != null)
            {
                if (!string.IsNullOrEmpty(sp.UniqueID)) return sp.UniqueID;
                if (!string.IsNullOrEmpty(sp.PrefabAssetID)) return sp.PrefabAssetID;
            }

            return null;
        }

        private bool TryGetTrackedGameObjectForIdentifier(string identifier, out string trackerKey, out TrackedGameObject trackedGameObject)
        {
            trackerKey = null;
            trackedGameObject = null;

            if (gameObjectTracker == null || string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            if (gameObjectTracker.TryPeekLiveTrackedGameObject(identifier, out trackedGameObject))
            {
                trackerKey = identifier;
                return true;
            }

            foreach (var kvp in gameObjectTracker.GetTrackedGameObjects())
            {
                var candidate = kvp.Value?.GameObject;
                if (candidate == null)
                    continue;

                if (IdentifierMatches(candidate, identifier))
                {
                    trackerKey = kvp.Key;
                    trackedGameObject = kvp.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool IdentifierMatches(GameObject obj, string identifier)
        {
            if (obj == null || string.IsNullOrEmpty(identifier))
                return false;

            if (RememberGameObject.TryGetCachedUniqueIdentifier(obj, out var cachedUniqueIdentifier) &&
                string.Equals(cachedUniqueIdentifier, identifier, StringComparison.Ordinal))
            {
                return true;
            }

            if (RememberGameObject.TryGetCachedGameObjectUniqueID(obj, out var cachedGameObjectId) &&
                string.Equals(cachedGameObjectId, identifier, StringComparison.Ordinal))
            {
                return true;
            }

            RememberGameObject rememberComponent = null;
            if (RememberGameObject.TryGetCachedRemember(obj, out var cachedRemember) && cachedRemember != null)
            {
                rememberComponent = cachedRemember;
            }
            else
            {
                rememberComponent = obj.GetComponent<RememberGameObject>();
            }

            if (rememberComponent != null)
            {
                if (!string.IsNullOrEmpty(rememberComponent.UniqueIdentifier) &&
                    string.Equals(rememberComponent.UniqueIdentifier, identifier, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(rememberComponent.GameObjectUniqueID) &&
                    string.Equals(rememberComponent.GameObjectUniqueID, identifier, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (obj.TryGetComponent<UniqueID>(out var uniqueIdComponent) &&
                !string.IsNullOrEmpty(uniqueIdComponent.ID) &&
                string.Equals(uniqueIdComponent.ID, identifier, StringComparison.Ordinal))
            {
                return true;
            }

            if (SaveablePrefab.TryGetCachedUniqueID(obj, out var cachedPrefabInstanceId) &&
                string.Equals(cachedPrefabInstanceId, identifier, StringComparison.Ordinal))
            {
                return true;
            }

            if (SaveablePrefab.TryGetCachedPrefabAssetID(obj, out var cachedPrefabAssetId) &&
                string.Equals(cachedPrefabAssetId, identifier, StringComparison.Ordinal))
            {
                return true;
            }

            if (SaveablePrefab.TryGetCachedSaveablePrefab(obj, out var cachedPrefab) && cachedPrefab != null)
            {
                if (!string.IsNullOrEmpty(cachedPrefab.UniqueID) &&
                    string.Equals(cachedPrefab.UniqueID, identifier, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(cachedPrefab.PrefabAssetID) &&
                    string.Equals(cachedPrefab.PrefabAssetID, identifier, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (obj.TryGetComponent<SaveablePrefab>(out var prefabComponent) && prefabComponent != null)
            {
                if (!string.IsNullOrEmpty(prefabComponent.UniqueID) &&
                    string.Equals(prefabComponent.UniqueID, identifier, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(prefabComponent.PrefabAssetID) &&
                    string.Equals(prefabComponent.PrefabAssetID, identifier, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string DeriveCanonicalDestroyedIdentifier(GameObject trackedObject, string trackerKey, string fallbackId)
        {
            if (!string.IsNullOrEmpty(trackerKey))
                return trackerKey;

            if (!string.IsNullOrEmpty(fallbackId) &&
                destroyedIdAliases.TryGetValue(fallbackId, out var mappedId) &&
                !string.IsNullOrEmpty(mappedId))
            {
                bool mappingValid = (CurrentSaveData?.DestroyedObjectData != null &&
                                     CurrentSaveData.DestroyedObjectData.ContainsKey(mappedId)) ||
                                    (gameObjectTracker != null && gameObjectTracker.IsGameObjectDestroyed(mappedId));

                if (mappingValid)
                {
                    return mappedId;
                }

                destroyedIdAliases.Remove(fallbackId);
            }

            if (trackedObject != null)
            {
                if (RememberGameObject.TryGetCachedUniqueIdentifier(trackedObject, out var cachedRememberId) &&
                    !string.IsNullOrEmpty(cachedRememberId))
                {
                    return cachedRememberId;
                }

                RememberGameObject rememberComponent = null;
                if (RememberGameObject.TryGetCachedRemember(trackedObject, out var cachedRemember) && cachedRemember != null)
                {
                    rememberComponent = cachedRemember;
                }
                else
                {
                    rememberComponent = trackedObject.GetComponent<RememberGameObject>();
                }

                if (rememberComponent != null && !string.IsNullOrEmpty(rememberComponent.UniqueIdentifier))
                {
                    return rememberComponent.UniqueIdentifier;
                }

                if (trackedObject.TryGetComponent<UniqueID>(out var uniqueIdComponent) &&
                    !string.IsNullOrEmpty(uniqueIdComponent.ID))
                {
                    return uniqueIdComponent.ID;
                }

                if (SaveablePrefab.TryGetCachedUniqueID(trackedObject, out var cachedPrefabId) &&
                    !string.IsNullOrEmpty(cachedPrefabId))
                {
                    return cachedPrefabId;
                }

                if (SaveablePrefab.TryGetCachedSaveablePrefab(trackedObject, out var cachedPrefab) &&
                    cachedPrefab != null &&
                    !string.IsNullOrEmpty(cachedPrefab.UniqueID))
                {
                    return cachedPrefab.UniqueID;
                }

                if (trackedObject.TryGetComponent<SaveablePrefab>(out var prefabComponent) &&
                    !string.IsNullOrEmpty(prefabComponent.UniqueID))
                {
                    return prefabComponent.UniqueID;
                }
            }

            return fallbackId;
        }

        private void NormalizeDestroyedDataAliases(string incomingId, string canonicalId)
        {
            if (CurrentSaveData == null) return;
            if (string.IsNullOrEmpty(incomingId) || string.IsNullOrEmpty(canonicalId)) return;
            if (string.Equals(incomingId, canonicalId, StringComparison.Ordinal)) return;

            if (CurrentSaveData.DestroyedObjectData != null &&
                CurrentSaveData.DestroyedObjectData.TryGetValue(incomingId, out var aliasData))
            {
                CurrentSaveData.DestroyedObjectData.Remove(incomingId);
                if (!CurrentSaveData.DestroyedObjectData.ContainsKey(canonicalId))
                {
                    CurrentSaveData.DestroyedObjectData[canonicalId] = aliasData;
                }
            }

            destroyedIdAliases[incomingId] = canonicalId;

            if (CurrentSaveData.Prefabs != null && CurrentSaveData.Prefabs.Count > 0)
            {
                bool canonicalExists = CurrentSaveData.Prefabs.Any(p => p.InstanceID == canonicalId);
                for (int i = CurrentSaveData.Prefabs.Count - 1; i >= 0; i--)
                {
                    var prefabData = CurrentSaveData.Prefabs[i];
                    if (prefabData.InstanceID != incomingId)
                        continue;

                    if (!canonicalExists)
                    {
                        prefabData.InstanceID = canonicalId;
                        canonicalExists = true;
                    }
                    else
                    {
                        CurrentSaveData.Prefabs.RemoveAt(i);
                    }
                }
            }
        }

        internal readonly struct DestroyedIdentifierInfo
        {
            public DestroyedIdentifierInfo(string incomingId, string canonicalId, IReadOnlyList<string> variants)
            {
                IncomingId = incomingId;
                CanonicalId = canonicalId;
                Variants = variants;
            }

            public string IncomingId { get; }
            public string CanonicalId { get; }
            public IReadOnlyList<string> Variants { get; }
        }

        internal DestroyedIdentifierInfo ResolveDestroyedIdentifierInfo(string incomingId)
        {
            string trackerKey;
            TrackedGameObject trackedGameObject;
            GameObject trackedObject = null;
            if (TryGetTrackedGameObjectForIdentifier(incomingId, out trackerKey, out trackedGameObject) &&
                trackedGameObject?.GameObject != null)
            {
                trackedObject = trackedGameObject.GameObject;
            }

            string canonicalId = DeriveCanonicalDestroyedIdentifier(trackedObject, trackerKey, incomingId);
            var variants = GatherDestroyedIdVariantsOrdered(incomingId, canonicalId);
            return new DestroyedIdentifierInfo(incomingId, canonicalId, variants);
        }

        private List<string> GatherDestroyedIdVariantsOrdered(string incomingId, string canonicalId)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddVariant(string id)
            {
                if (string.IsNullOrEmpty(id)) return;
                if (seen.Add(id)) ordered.Add(id);
            }

            AddVariant(canonicalId);
            AddVariant(incomingId);

            if (destroyedIdAliases.Count == 0)
                return ordered;

            bool added;
            do
            {
                added = false;
                foreach (var kvp in destroyedIdAliases)
                {
                    if (string.IsNullOrEmpty(kvp.Key) && string.IsNullOrEmpty(kvp.Value))
                        continue;

                    if ((!string.IsNullOrEmpty(kvp.Key) && seen.Contains(kvp.Key)) ||
                        (!string.IsNullOrEmpty(kvp.Value) && seen.Contains(kvp.Value)))
                    {
                        int before = ordered.Count;
                        AddVariant(kvp.Key);
                        AddVariant(kvp.Value);
                        if (ordered.Count > before)
                        {
                            added = true;
                        }
                    }
                }
            }
            while (added);

            return ordered;
        }

        private static bool VariantIdsContainMatch(IReadOnlyList<string> variantIds, string candidate)
        {
            if (variantIds == null || string.IsNullOrEmpty(candidate))
                return false;

            foreach (var variant in variantIds)
            {
                if (string.IsNullOrEmpty(variant))
                    continue;

                if (string.Equals(candidate, variant, StringComparison.Ordinal))
                    return true;

                if (candidate.StartsWith(variant + "_", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool TryGetComponentDataFrom(
            SaveData data,
            IReadOnlyList<string> variantIds,
            out string matchedId,
            out Dictionary<string, byte[]> componentData)
        {
            matchedId = null;
            componentData = null;

            if (data?.DestroyedObjectData == null || variantIds == null)
                return false;

            foreach (var id in variantIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                if (data.DestroyedObjectData.TryGetValue(id, out componentData))
                {
                    matchedId = id;
                    return true;
                }
            }

            return false;
        }

        private static SaveablePrefabData FindPrefabDataInList(List<SaveablePrefabData> list, IReadOnlyList<string> variantIds)
        {
            if (list == null || list.Count == 0 || variantIds == null)
                return null;

            foreach (var id in variantIds)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                // Prefer exact match on InstanceID
                var record = list.FirstOrDefault(p => p.InstanceID == id);
                if (record != null)
                    return record;

                // Fallback: allow canonical base ID to match extended IDs (e.g., baseID_componentID)
                // This mirrors component-data variant matching so restores can find synthesized
                // transform records captured at destruction time.
                string prefix = id + "_";
                record = list.FirstOrDefault(p => !string.IsNullOrEmpty(p.InstanceID) && p.InstanceID.StartsWith(prefix, StringComparison.Ordinal));
                if (record != null)
                    return record;
            }

            return null;
        }

        private int RemoveDestroyedObjectDataEntries(SaveData targetData, IReadOnlyCollection<string> ids)
        {
            if (targetData?.DestroyedObjectData == null || ids == null || ids.Count == 0)
                return 0;

            int removed = 0;
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id))
                    continue;

                if (targetData.DestroyedObjectData.Remove(id))
                    removed++;
            }

            return removed;
        }

        private int RemoveDestroyedListEntries(SaveData targetData, IReadOnlyCollection<string> variants)
        {
            if (targetData?.DestroyedGameObjects == null ||
                targetData.DestroyedGameObjects.Count == 0 ||
                variants == null ||
                variants.Count == 0)
            {
                return 0;
            }

            var variantList = variants as IReadOnlyList<string> ?? variants.ToList();
            bool hasValidVariant = false;
            foreach (var id in variantList)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    hasValidVariant = true;
                    break;
                }
            }

            if (!hasValidVariant)
                return 0;

            return targetData.DestroyedGameObjects.RemoveAll(id => VariantIdsContainMatch(variantList, id));
        }

        private int RemovePrefabEntries(SaveData targetData, IReadOnlyCollection<string> ids)
        {
            if (targetData?.Prefabs == null || targetData.Prefabs.Count == 0 || ids == null || ids.Count == 0)
                return 0;

            var idSet = new HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
            if (idSet.Count == 0)
                return 0;

            int before = targetData.Prefabs.Count;
            targetData.Prefabs.RemoveAll(p =>
                p != null &&
                !string.IsNullOrEmpty(p.InstanceID) &&
                (idSet.Contains(p.InstanceID) || idSet.Any(baseId => p.InstanceID.StartsWith(baseId + "_", StringComparison.Ordinal)))
            );
            return before - targetData.Prefabs.Count;
        }

        private int RemoveDestroyedAliasMappings(IReadOnlyCollection<string> ids)
        {
            if (destroyedIdAliases.Count == 0 || ids == null || ids.Count == 0)
                return 0;

            var idSet = new HashSet<string>(ids.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);
            if (idSet.Count == 0)
                return 0;

            var keysToRemove = destroyedIdAliases
                .Where(kvp => idSet.Contains(kvp.Key) || (!string.IsNullOrEmpty(kvp.Value) && idSet.Contains(kvp.Value)))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                destroyedIdAliases.Remove(key);
            }

            return keysToRemove.Count;
        }

        private void LogDestroyedIdCleanup(
            string incomingId,
            string canonicalId,
            int destroyedRemoved,
            int destroyedListRemoved,
            int dataRemoved,
            int prefabRemoved,
            int aliasRemoved)
        {
            string incomingLogId = string.IsNullOrEmpty(incomingId) ? "<null or empty>" : incomingId;
            string canonicalLogId = string.IsNullOrEmpty(canonicalId) ? "<null or empty>" : canonicalId;

            Logger.Log(
                $"RestoreDestroyedGameObject: cleared destroyed records for incoming '{incomingLogId}' (canonical '{canonicalLogId}') [DestroyedIDsRemoved={destroyedRemoved}, DestroyedListEntriesRemoved={destroyedListRemoved}, DestroyedDataRemoved={dataRemoved}, PrefabEntriesRemoved={prefabRemoved}, AliasMappingsRemoved={aliasRemoved}].",
                LogCategory.SaveManager,
                LogLevel.Info);
        }

        internal void ReMapTrackedUniqueIDs(SaveSlot slot)
        {
            try
            {
                // 1️⃣  Null-guards ─────────────────────────────────────
                if (CurrentSaveData == null)
                {
                    Logger.Log("ReMapTrackedUniqueIDs: CurrentSaveData is null – nothing to remap.",
                            LogLevel.Warning);
                    return;
                }

                SaveData data = CurrentSaveData;

                if (data.TrackedUniqueIDs == null || data.TrackedUniqueIDs.Count == 0)
                {
                    Logger.Log("ReMapTrackedUniqueIDs: TrackedUniqueIDs list is empty or null.",
                            LogCategory.SaveManager,
                            LogLevel.Info);
                    return;
                }

                // 2️⃣  Re-register every still-present object ─────────
                foreach (string uniqueID in data.TrackedUniqueIDs)
                {
                    if (string.IsNullOrEmpty(uniqueID)) continue;

	                    GameObject go = FindGameObjectByUniqueID(uniqueID, IdentifierType.UniqueID);
	                    if (go != null)
	                    {
	                        var matchingPrefabData = data.Prefabs?.FirstOrDefault(p => p != null && string.Equals(p.InstanceID, uniqueID, StringComparison.Ordinal));
	                        var saveablePrefab = go.GetComponent<SaveablePrefab>();
	                        Logger.Log(
	                            $"[CrystalSave][SaveFlowDiag] ReMapTrackedUniqueIDs: trackedID='{uniqueID}' resolved to go='{go.name}', " +
	                            $"path='{GetGameObjectPath(go)}', scene='{(go.scene.IsValid() ? go.scene.name : "<invalid>")}', " +
	                            $"goSaveablePrefabAssetID='{(saveablePrefab != null ? EmptyDiagLabel(saveablePrefab.PrefabAssetID) : "<none>")}', " +
	                            $"goSaveableUniqueID='{(saveablePrefab != null ? EmptyDiagLabel(saveablePrefab.UniqueID) : "<none>")}', " +
	                            $"matchingPrefabDataName='{(matchingPrefabData != null ? EmptyDiagLabel(matchingPrefabData.GameObjectName) : "<none>")}', " +
	                            $"matchingPrefabDataPrefabID='{(matchingPrefabData != null ? EmptyDiagLabel(matchingPrefabData.PrefabID) : "<none>")}'.",
	                            LogCategory.SaveManager,
	                            LogLevel.Info);
	                        RegisterGameObject(go, GetPropertySettings(go));
	                        Logger.Log($"Re-registered GameObject '{go.name}' with ID '{uniqueID}'.",
	                                LogCategory.SaveManager, LogLevel.Info);
                    }
                    else
                    {
                        Logger.Log(
                            $"ReMapTrackedUniqueIDs: GameObject with ID '{uniqueID}' not found – probably expected if it was destroyed.", LogCategory.SaveManager, LogLevel.Info);
                    }
                }

	                // 3️⃣  Clean up destroyed-IDs list — only if both lists exist
                if (data.DestroyedGameObjects == null)
                    data.DestroyedGameObjects = new List<string>();   // ensure not null

                foreach (string uniqueID in data.TrackedUniqueIDs)
                {
                    if (string.IsNullOrEmpty(uniqueID)) continue;

                    if (gameObjectTracker.IsGameObjectDestroyed(uniqueID))
                    {
                        // keep entry only when the object is *still* destroyed in the save
                        if (!data.DestroyedGameObjects.Contains(uniqueID))
                        {
                            gameObjectTracker.RemoveDestroyedID(uniqueID);
                            Logger.Log($"ReMapTrackedUniqueIDs: Removed '{uniqueID}' from destroyedGameObjectIDs – object exists again.", LogCategory.SaveManager, LogLevel.Info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ReMapTrackedUniqueIDs error: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
                // don’t re-throw – we just want to prevent a crash
            }
	        }

	        private static string EmptyDiagLabel(string value)
	        {
	            return string.IsNullOrEmpty(value) ? "<empty>" : value;
	        }

	        private static string GetGameObjectPath(GameObject go)
	        {
	            if (go == null)
	                return "<null>";

	            Transform transform = go.transform;
	            string path = transform.name;
	            Transform current = transform.parent;
	            while (current != null)
	            {
	                path = current.name + "/" + path;
	                current = current.parent;
	            }

	            return path;
	        }

	        GameObjectPropertySettings GetPropertySettings(GameObject go)
	        {
            var remember = go.GetComponent<RememberGameObject>();
            if (remember != null)
                return remember.PropertySettings;

            var prefab = go.GetComponent<SaveablePrefab>();
            if (prefab != null)
                return prefab.PropertySettings;

            return new GameObjectPropertySettings { RememberActive = true };
        }

        #endregion

        #region Async Helpers
        /// <summary>
        /// Wraps a Task so it can be yielded from a coroutine.
        /// </summary>
        internal IEnumerator AwaitTask(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                // Optionally, rethrow or handle exceptions here.
                throw task.Exception;
            }
        }
        #endregion



        #region Utility Methods
        /// <summary>
        /// Collects all necessary save data including prefabs, components, and GameObject states.
        /// </summary>
        /// <param name="lastActiveScene">Optional: The name of the last active scene.</param>
        /// <returns>A SaveData object containing all collected data.</returns>
        internal SaveData CollectSaveData(string lastActiveScene = null)
        {
            using var captureOutcome = ComponentOperationScope.Capture();
            using ComponentManager.SaveDataCollectionOwnershipScope ownershipScope =
                componentManager.BeginSaveDataCollectionOwnershipScope();
            SaveData data = new SaveData(VersioningService.VersionManager.CurrentVersion);

            // Determine the active scene
            if (!string.IsNullOrEmpty(lastActiveScene))
            {
                data.LastActiveScene = lastActiveScene;
                Logger.Log($"SaveManager: Using provided LastActiveScene '{data.LastActiveScene}'.", LogCategory.SaveManager, LogLevel.Info);
            }
            else
            {
                data.LastActiveScene = SceneManager.GetActiveScene().name;
                Logger.Log($"SaveManager: Captured active scene '{data.LastActiveScene}'.", LogCategory.SaveManager, LogLevel.Info);
            }

            // IMPORTANT: Add the destroyed IDs
            data.DestroyedGameObjects = new List<string>(gameObjectTracker.GetDestroyedGameObjectIDs());

            // Collect prefab data
            data.Prefabs = prefabManager.CollectPrefabData();

            // Merge any prefab records captured from destroyed instances
            // IMPORTANT: We must also include synthesized transform snapshots (entries with empty PrefabID)
            // for destroyed scene objects so their last known transform survives across save/load cycles.
            // Prefab instantiation on load already skips these placeholders, but RestoreDestroyedGameObject
            // will read them to position the restored prefab correctly.
            int mergedFromPrevious = 0;
            int droppedDestroyedAtRuntime = 0;
            if (CurrentSaveData != null && CurrentSaveData.Prefabs != null)
            {
                // Build a quick lookup of destroyed IDs in this collection pass
                HashSet<string> destroyedIdSet = null;
                if (data.DestroyedGameObjects != null && data.DestroyedGameObjects.Count > 0)
                    destroyedIdSet = new HashSet<string>(data.DestroyedGameObjects);

                // Snapshot of PrefabManager's instantiated prefabs — entries that are
                // still alive have a mapping here. Entries that were loaded then destroyed
                // at runtime have been removed by UnregisterPrefab.
                var liveInstances = prefabManager.GetInstantiatedPrefabs();

                // Build a set of live InstanceIDs for fast lookup
                var liveInstanceIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in data.Prefabs)
                {
                    if (p != null && !string.IsNullOrEmpty(p.InstanceID))
                        liveInstanceIds.Add(p.InstanceID);
                }

                foreach (var pd in CurrentSaveData.Prefabs)
                {
                    if (pd == null) continue;

                    // Already in the live set — skip (no duplicate)
                    if (liveInstanceIds.Contains(pd.InstanceID))
                        continue;

                    bool isPlaceholder = string.IsNullOrEmpty(pd.PrefabID);
                    if (isPlaceholder)
                    {
                        // Only carry placeholders if they correspond to a destroyed object in this snapshot
                        // or if they explicitly carry a transform override flag (synthesized records do).
                        bool keepPlaceholder = (destroyedIdSet != null && destroyedIdSet.Contains(pd.InstanceID))
                                               || pd.HasTransformOverride;
                        if (!keepPlaceholder)
                            continue;
                    }
                    else
                    {
                        // Non-placeholder entry not in live set.
                        // Check if PrefabManager still tracks this instance.
                        // If it doesn't, the instance was loaded and then destroyed
                        // at runtime — drop it to prevent zombie accumulation.
                        //
                        // EXCEPTION: If the entry has a HomeScene that is NOT currently
                        // loaded, it belongs to an unloaded scene (Remember Home Scene).
                        // These entries were never instantiated this session, so they
                        // won't be in liveInstances — but they must be preserved so
                        // they can be restored when their scene loads.
                        bool stillTracked = liveInstances.ContainsKey(pd.InstanceID);
                        if (!stillTracked)
                        {
                            // Check if the entry belongs to an unloaded scene
                            bool belongsToUnloadedScene = false;
                            if (!string.IsNullOrEmpty(pd.HomeScene))
                            {
                                var scene = SceneManager.GetSceneByName(pd.HomeScene);
                                belongsToUnloadedScene = !scene.IsValid() || !scene.isLoaded;
                            }

                            if (!belongsToUnloadedScene)
                            {
                                droppedDestroyedAtRuntime++;
                                continue;
                            }
                        }
                    }

                    data.Prefabs.Add(pd);
                    mergedFromPrevious++;
                }
            }

            if (droppedDestroyedAtRuntime > 0)
            {
                Logger.Log($"[SaveManager.CollectSaveData] Dropped {droppedDestroyedAtRuntime} zombie prefab entries (destroyed at runtime). Merged {mergedFromPrevious} entries from previous save.", LogCategory.SaveManager, LogLevel.Info);
            }

            // Collect saveable component data via ComponentManager
            componentManager.CollectComponentData(data);

            // Collect GameObject active states
            foreach (var state in gameObjectTracker.CollectActiveStates())
            {
                data.GameObjectStates.Add(state);

                Logger.Log($"SaveManager: Collected active state for GameObject with ID '{state.UniqueID}': {state.IsActive}", LogCategory.SaveManager, LogLevel.Info);
            }

            Logger.Log("SaveManager: Data collection completed successfully.", LogCategory.SaveManager, LogLevel.Info);

            // Update CurrentSaveData to the freshly collected data.
            // This prevents stale entries from being re-merged on subsequent saves
            // within the same session.
            captureOutcome.ThrowIfFailed();
            CurrentSaveData = data;

            return data;
        }

        /// <summary>
        /// Applies the active states to the tracked GameObjects.
        /// </summary>
        /// <param name="states">List of GameObjectState containing active state information.</param>
        internal void ApplyGameObjectActiveStates(List<GameObjectState> states)
        {
            gameObjectTracker?.ApplyGameObjectActiveStates(states);
        }

        /// <summary>
        /// Applies GameObject active states from the currently loaded save data.
        /// </summary>
        public void ApplyGameObjectActiveStates()
        {
            if (CurrentSaveData == null)
            {
                Logger.Log(
                    "ApplyGameObjectActiveStates: no loaded data available.",
                    LogCategory.SaveManager,
                    LogLevel.Warning
                );
                return;
            }

            ApplyGameObjectActiveStates(CurrentSaveData.GameObjectStates);
        }

        public bool IsGameObjectDestroyed(string uniqueID)
        {
            return gameObjectTracker != null && gameObjectTracker.IsGameObjectDestroyed(uniqueID);
        }

        /// <summary>
        /// Determines whether the specified <see cref="GameObject"/> is
        /// currently tracked by the <see cref="SaveManager"/>.
        /// </summary>
        /// <param name="obj">The <see cref="GameObject"/> to check.</param>
        /// <returns>
        /// True if the object's UniqueID is present in <c>trackedGameObjects</c>;
        /// otherwise, false.
        /// </returns>
        public bool IsGameObjectTracked(GameObject obj)
        {
            if (obj == null)
            {
                return false;
            }

            string uniqueID = GetUniqueID(obj);
            if (string.IsNullOrEmpty(uniqueID))
            {
                return false;
            }

            if (gameObjectTracker == null)
                return false;

            return gameObjectTracker.IsTracked(uniqueID);
        }

        /// <summary>
        /// Finds a GameObject by its UniqueID.
        /// </summary>
        /// <param name="uniqueID">The UniqueID to search for.</param>
        /// <returns>The corresponding GameObject if found; otherwise, null.</returns>
        public enum IdentifierType
        {
            Auto,
            UniqueID,
            SceneObjectID,
            PrefabAssetID
        }

        public GameObject FindGameObjectByUniqueID(string uniqueID, IdentifierType identifierType = IdentifierType.Auto)
        {
            PrefabLoadDiagnosticCounters.RecordPublicLookupCall();
            return objectLookup.Find(uniqueID, identifierType, true, gameObjectTracker, prefabManager);
        }

        internal GameObject FindGameObjectByUniqueIDWithoutMissingLog(string uniqueID, IdentifierType identifierType = IdentifierType.Auto)
            => objectLookup.Find(uniqueID, identifierType, false, gameObjectTracker, prefabManager);

        public void SetLookupCacheEnabled(bool enabled) => objectLookup.SetLookupCacheEnabled(enabled);
        public void ClearLookupCache() => objectLookup.ClearLookupCache();
        public void EnsureCacheConsistency() => objectLookup.EnsureCacheConsistency(gameObjectTracker);
        internal void CacheGameObject(string id, GameObject obj) => objectLookup.CacheGameObject(id, obj);
        internal void UncacheGameObject(string id) => objectLookup.UncacheGameObject(id);
        internal void UncacheGameObjectIfOwned(string id, GameObject owner) => objectLookup.UncacheGameObjectIfOwned(id, owner);
        internal bool TryGetCachedGameObject(string id, out GameObject obj) => objectLookup.TryGetCachedGameObject(id, out obj);
        private static bool IsSameGameObjectInstance(GameObject left, GameObject right) => SaveObjectLookup.IsSameGameObjectInstance(left, right);

        #endregion

        #region Version Handling

        /// <summary>
        /// Handles version comparison and compatibility.
        /// </summary>
        /// <param name="result">The result of the version comparison.</param>
        /// <param name="data">The loaded save data.</param>
        internal void HandleVersionResult(VersionComparisonResult result, SaveData data)
        {
            switch (result)
            {
                case VersionComparisonResult.Newer:
                    Logger.Log($"Save data version {data.Version} is newer than current version {VersioningService.VersionManager.CurrentVersion}. Cannot load saved data.", LogCategory.SaveManager, LogLevel.Warning);
                    throw new InvalidOperationException("Incompatible save data version.");

                case VersionComparisonResult.Older:
                    Logger.Log($"Save data version {data.Version} is older than current version {VersioningService.VersionManager.CurrentVersion}. Attempting to load with compatibility mode.", LogCategory.SaveManager, LogLevel.Warning);
                    VersioningService?.Migrate(data);
                    break;

                case VersionComparisonResult.Equal:
                    Logger.Log("Save data version matches current version. Proceeding with load.", LogCategory.SaveManager, LogLevel.Info);
                    // Backward-compat: same-version saves written by older builds may still
                    // contain legacy suffixed GameObject identifiers. Normalize them so
                    // GameObjectTracker (which now always emits bare IDs) can resolve them.
                    LegacyIdMigration.NormalizeSaveData(data);
                    break;

                case VersionComparisonResult.Incompatible:
                    Logger.Log("Save data version is incompatible.", LogCategory.SaveManager, LogLevel.Error);
                    throw new InvalidOperationException("Incompatible save data version.");

                default:
                    Logger.Log("Unknown version comparison result.", LogCategory.SaveManager, LogLevel.Error);
                    throw new InvalidOperationException("Unknown version comparison result.");
            }
        }

        public void ConfigureSerializer(Action<SaveDataSerializer> configure)
            => VersioningService?.ConfigureSerializer(configure);

        public void MigrateSaveData(SaveData data)
            => VersioningService?.Migrate(data);

        /// Invokes the OnSaveFailed event.
        /// </summary>
        /// <param name="slot">The save slot that failed.</param>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="message">The error message.</param>
        private void InvokeSaveFailed(SaveSlot slot, string operation, string message)
        {
            OnSaveFailed?.Invoke(this, new OperationFailedEventArgs(slot, operation, message));
        }

        /// <summary>
        /// Invokes the OnDeleteFailed event.
        /// </summary>
        /// <param name="slot">The save slot that failed.</param>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="message">The error message.</param>
        private void InvokeDeleteFailed(SaveSlot slot, string operation, string message)
        {
            SaveEventDispatch.Raise(OnDeleteFailed, this, new OperationFailedEventArgs(slot, operation, message));
        }

        /// <summary>
        /// Invokes the OnRenameSlotFailed event.
        /// </summary>
        /// <param name="slot">The save slot that failed.</param>
        /// <param name="operation">The operation that failed.</param>
        /// <param name="message">The error message.</param>
        private void InvokeRenameSlotFailed(SaveSlot slot, string operation, string message)
        {
            SaveEventDispatch.Raise(OnRenameSlotFailed, this, new OperationFailedEventArgs(slot, operation, message));
        }

        internal void InvokeBackupFailed(SaveSlot slot, string operation, string message)
        {
            SaveEventDispatch.Raise(OnBackupFailed, this, new OperationFailedEventArgs(slot, operation, message));
        }

        internal void InvokeVerificationFailed(SaveSlot slot, string operation, string message)
        {
            SaveEventDispatch.Raise(OnVerificationFailed, this, new OperationFailedEventArgs(slot, operation, message));
        }

        /// <summary>
        /// Copies key/value pairs from <see cref="SaveSettings.defaultSlotMetadata"/> into the slot.
        /// </summary>
        /// <param name="slot">Target slot.</param>
        internal void ApplyDefaultSlotMetadata(SaveSlot slot, bool overwrite = true)
        {
            if (slot == null)
                return;

            if (slot.CustomMetadata == null)
                slot.CustomMetadata = new Dictionary<string, string>();

            if (saveSettings != null && saveSettings.defaultSlotMetadata != null)
            {
                // Overwrite existing keys with the current defaults so metadata like level name
                // is refreshed on every save when driven by SaveSlotMetadataSO.
                // Only applies to keys defined in defaults; other custom keys remain untouched.
                var defaults = saveSettings.defaultSlotMetadata.ToDictionary();
                foreach (var kvp in defaults)
                {
                    if (string.IsNullOrEmpty(kvp.Key))
                        continue;
                    if (!overwrite && slot.CustomMetadata.ContainsKey(kvp.Key)) continue;
                    slot.CustomMetadata[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }

        }

        /// <summary>
        /// Coroutine that re-applies GameObject active states after a short delay.
        internal void StartActiveStateWatch(List<GameObjectState> states)
        {
            gameObjectTracker?.StartActiveStateWatch(states);
        }

        internal void StopActiveStateWatch()
        {
            gameObjectTracker?.StopActiveStateWatch();
        }
        /// <summary>
        /// Checks if a scene exists in the build settings.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check.</param>
        /// <returns>True if the scene exists; otherwise, false.</returns>
        internal bool IsSceneInBuild(string sceneName)
        {
            return buildSceneNames.Contains(sceneName);
        }

        /* ================================================================== */
        #region Queued Operations
        /* ================================================================== */

        /// <summary>
        /// Enqueue an operation to be executed once the manager has completed
        /// its initialisation. Can also be used by Remember components to defer
        /// operations until after loading completes.
        /// </summary>
        /// <param name="operation">The action to execute.</param>
        public void QueueOperation(Action operation)
        {
            lock (queueLock)
                queuedOperations.Enqueue(operation);
        }

        /// <summary>
        /// Execute all queued operations.
        /// </summary>
        internal void ExecuteQueuedOperations()
        {
            lock (queueLock)
            {
                if (queuedOperations.Count > 0)
                    Logger.Log($"Executing {queuedOperations.Count} queued SaveManager operation(s).", LogCategory.SaveManager, LogLevel.Info);
                while (queuedOperations.Count > 0)
                {
                    try { queuedOperations.Dequeue()?.Invoke(); }
                    catch (Exception ex)
                    {
                        Logger.Log($"Queued operation threw an exception: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.SaveManager, LogLevel.Error);
                    }
                }
            }
        }

        #endregion
        #endregion

        #region Documentation

        // The class is organized into regions for better readability and maintainability.
        // Each region encapsulates related functionalities, making the class easier to navigate.

        #endregion
    }
}
#endif
