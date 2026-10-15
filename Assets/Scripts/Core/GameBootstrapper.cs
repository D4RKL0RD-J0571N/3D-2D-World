namespace Core
{
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
            _stateMachine?.Tick(Time.deltaTime);
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

            _stateMachine = new GameStateMachine();
            ServiceLocator.Register(_stateMachine);

            ServiceLocator.Register(new InputManager());
            ServiceLocator.Register(new Systems.Pooling.PoolingManager());
            ServiceLocator.Register(new Systems.Scheduler.AreaSchedulerService());
            ServiceLocator.Register(new LevelGenerationService());
            ServiceLocator.Register(new Systems.Interaction.GridInteractionService());
            ServiceLocator.Register(new Systems.Interaction.HotbarService());
            ServiceLocator.Register(new Systems.Interaction.BlockPlacementService());
            ServiceLocator.Register(new Systems.Persistence.FileSaveService());

            ServiceLocator.Register(new Systems.Player.PlayerSpawnService()); // Also making sure the spawn service is registered!

            if (Object.FindFirstObjectByType<Gameplay.UI.HotbarHUD>() == null)
            {
                var hudGo = new GameObject("HotbarHUD");
                hudGo.AddComponent<Gameplay.UI.HotbarHUD>();
            }

            _ = _stateMachine.TransitionToAsync(new Playing());
        }
    }
}
