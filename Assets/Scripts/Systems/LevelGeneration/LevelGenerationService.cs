namespace Systems.LevelGeneration
{
    using Core;
    using Domain.LevelGeneration;
    using Gameplay.World;

    public sealed class LevelGenerationService : IGameService
    {
        private IRoomGraphGenerator _generator;
        private LevelLayout _currentLayout;

        public LevelLayout CurrentLayout => _currentLayout;

        public LevelGenerationService()
        {
            // By default use the basic generator, or could be injected
            _generator = new RoomGraphGenerator();
        }

        public void Initialize()
        {
            GameEventManager.AddListener<LevelGenerationRequestedEvent>(OnLevelGenerationRequested);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<LevelGenerationRequestedEvent>(OnLevelGenerationRequested);
            _currentLayout = null;
        }

        private void OnLevelGenerationRequested(LevelGenerationRequestedEvent gameEvent)
        {
            if (ServiceLocator.TryGet<Systems.Persistence.FileSaveService>(out var saveService) && saveService.HasSaveFile())
            {
                var saveData = saveService.ReadData();
                if (saveData != null)
                {
                    _currentLayout = saveData.OriginalLayout;
                    GameEventManager.Publish(new Domain.Persistence.GameDataLoadedEvent { SaveData = saveData });
                    return; // Skip procedural generation
                }
            }

            var parameters = gameEvent.Parameters ?? new LevelGenerationParameters();
            var layout = _generator.Generate(parameters);

            if (layout == null || layout.Rooms.Count == 0)
            {
                GameEventManager.Publish(new LevelGenerationFailedEvent
                {
                    Reason = "Level generation produced no rooms."
                });
                return;
            }

            _currentLayout = layout;
            GameEventManager.Publish(new LevelGenerationCompletedEvent
            {
                Layout = layout
            });
        }
    }
}
