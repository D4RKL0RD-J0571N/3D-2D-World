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
            ServiceLocator.Register(new Systems.Persistence.FileSaveService());

            ServiceLocator.Register(new Systems.Player.PlayerSpawnService()); // Also making sure the spawn service is registered!

            var worldMapService = new Systems.WorldMap.WorldMapService();
            ServiceLocator.Register(worldMapService);
            TrackTickable(worldMapService);

            if (Object.FindFirstObjectByType<Gameplay.UI.HotbarHUD>() == null)
            {
                var hudGo = new GameObject("HotbarHUD");
                hudGo.AddComponent<Gameplay.UI.HotbarHUD>();
            }

            if (Object.FindFirstObjectByType<Gameplay.UI.PlayerHUD>() == null)
            {
                var playerHudGo = new GameObject("PlayerHUD");
                playerHudGo.AddComponent<Gameplay.UI.PlayerHUD>();
            }

            if (Object.FindFirstObjectByType<Gameplay.UI.WorldMapHUD>() == null)
            {
                var mapGo = new GameObject("WorldMapHUD");
                mapGo.AddComponent<Gameplay.UI.WorldMapHUD>();
            }

            _ = _stateMachine.TransitionToAsync(new Playing());
        }

        private void TrackTickable(ITickable tickable)
        {
            if (tickable != null && !_tickables.Contains(tickable))
                _tickables.Add(tickable);
        }
    }
}
