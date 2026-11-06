namespace Core
{
    using Core.Modules;
    using UnityEngine;

    /// <summary>
    /// Global entry point for the Fantasy Engine. Attached to a persistent GameObject in the bootstrap scene.
    /// Delegates module discovery, topological dependency sorting, two-phase service initialization,
    /// and deterministic per-frame ticking to <see cref="ModuleHost"/>.
    /// Contains zero compile-time or runtime references to specific RPG/Skyrim gameplay implementations.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
        #region Singleton & Inspector Fields

        public static GameBootstrapper Instance { get; private set; }

        [Header("Engine Bootstrap Configuration")]
        [Tooltip("Optional profile controlling which engine modules load. If null, all discovered built-in modules load.")]
        [SerializeField]
        private EngineBootstrapProfile _bootstrapProfile;

        #endregion

        #region Runtime State

        private ModuleHost _moduleHost;

        public ModuleHost Host => _moduleHost;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeEngine();
        }

        private void Update()
        {
            _moduleHost?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            _moduleHost?.Shutdown();
            _moduleHost = null;
            Instance = null;
        }

        #endregion

        #region Initialization

        private void InitializeEngine()
        {
            GameEventManager.Clear();

            _moduleHost = new ModuleHost();
            _moduleHost.DiscoverAndAddModules(_bootstrapProfile);
            _moduleHost.Boot(hydrateProjectAssets: true);
        }

        #endregion
    }
}
