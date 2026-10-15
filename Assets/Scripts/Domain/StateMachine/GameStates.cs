using System.Threading.Tasks;
using Core;

namespace Domain.Entities.StateMachine
{
    public class Playing : IGameState
    {
        public GameStateType StateType => GameStateType.Playing;

        public Task OnEnterAsync()
        {
            GameEventManager.AddListener<Gameplay.World.LevelCompletedEvent>(OnLevelCompleted);
            return Task.CompletedTask;
        }

        public Task OnExitAsync()
        {
            GameEventManager.RemoveListener<Gameplay.World.LevelCompletedEvent>(OnLevelCompleted);
            return Task.CompletedTask;
        }

        public void OnTick(float deltaTime) { }

        private void OnLevelCompleted(Gameplay.World.LevelCompletedEvent gameEvent)
        {
            // Seed = -1 means random seed
            GameEventManager.Publish(new Gameplay.World.LevelGenerationRequestedEvent
            {
                Parameters = new Domain.LevelGeneration.LevelGenerationParameters { Seed = -1 }
            });
        }
    }

    public class MainMenu : IGameState
    {
        public GameStateType StateType => GameStateType.MainMenu;

        public Task OnEnterAsync() => Task.CompletedTask;

        public Task OnExitAsync() => Task.CompletedTask;

        public void OnTick(float deltaTime) { }
    }

    public class GameOver : IGameState
    {
        public GameStateType StateType => GameStateType.GameOver;

        public Task OnEnterAsync() => Task.CompletedTask;

        public Task OnExitAsync() => Task.CompletedTask;

        public void OnTick(float deltaTime) { }
    }

    public class Paused : IGameState
    {
        public GameStateType StateType => GameStateType.Paused;

        public Task OnEnterAsync() => Task.CompletedTask;

        public Task OnExitAsync() => Task.CompletedTask;

        public void OnTick(float deltaTime) { }
    }

    public class Loading : IGameState
    {
        public GameStateType StateType => GameStateType.Loading;

        public Task OnEnterAsync() => Task.CompletedTask;

        public Task OnExitAsync() => Task.CompletedTask;

        public void OnTick(float deltaTime) { }
    }
}
