namespace Core
{
    using System.Collections.Generic;
    using UnityEngine;
    using Systems.Input;
    using Systems.LevelGeneration;
    using Domain.Entities.StateMachine;

    /// <summary>
    /// Global entry point for the game. Attached to a persistent GameObject in the bootstrap scene.
    /// Responsible for initializing the <see cref="ServiceLocator"/> and all <see cref="IGameService"/> instances,
    /// and for driving the per-frame tick on services that implement <see cref="ITickable"/>.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrapper : MonoBehaviour
    {
        public static GameBootstrapper Instance { get; private set; }

        private GameStateMachine _stateMachine;
        private readonly List<ITickable> _tickables = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeServices();
        }

        private void Update()
        {
            foreach (var tickable in _tickables)
                tickable?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            ServiceLocator.Clear();
            GameEventManager.Clear();
            Instance = null;
        }

        private void InitializeServices()
        {
            GameEventManager.Clear();
            _tickables.Clear();

            _stateMachine = new GameStateMachine();
            ServiceLocator.Register(_stateMachine);
            TrackTickable(_stateMachine);

            ServiceLocator.Register(new InputManager());
            ServiceLocator.Register(new Systems.Pooling.PoolingManager());

            var areaScheduler = new Systems.Scheduler.AreaSchedulerService();
            ServiceLocator.Register(areaScheduler);
            TrackTickable(areaScheduler);

            ServiceLocator.Register(new LevelGenerationService());
            ServiceLocator.Register(new Systems.Interaction.GridInteractionService());
            ServiceLocator.Register(new Systems.Interaction.HotbarService());
            ServiceLocator.Register(new Systems.Interaction.BlockPlacementService());
            ServiceLocator.Register(new Systems.Crafting.CraftingService());
            ServiceLocator.Register(new Systems.Persistence.FileSaveService());

            ServiceLocator.Register(new Systems.Player.PlayerSpawnService());

            var worldMapService = new Systems.WorldMap.WorldMapService();
            ServiceLocator.Register(worldMapService);
            TrackTickable(worldMapService);

            ServiceLocator.Register(new Systems.Lab.LabService());

            EnsureHUD<Gameplay.UI.HotbarHUD>("HotbarHUD");
            EnsureHUD<Gameplay.UI.PlayerHUD>("PlayerHUD");
            EnsureHUD<Gameplay.UI.WorldMapHUD>("WorldMapHUD");
            EnsureHUD<Gameplay.UI.LabHUD>("LabHUD");

            _ = _stateMachine.TransitionToAsync(new Playing());
        }

        /// <summary>
        /// Creates a HUD GameObject with the given component if none exists in the scene.
        /// </summary>
        private static void EnsureHUD<T>(string goName) where T : Component
        {
            if (Object.FindFirstObjectByType<T>() == null)
            {
                var go = new GameObject(goName);
                go.AddComponent<T>();
            }
        }

        private void TrackTickable(ITickable tickable)
        {
            if (tickable != null && !_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }
    }
}
