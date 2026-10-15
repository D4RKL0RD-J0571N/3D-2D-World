namespace Core
{
    using System.Threading.Tasks;
    using Domain.Entities.StateMachine;

    /// <summary>
    /// Contract for a single state in a <see cref="GameStateMachine"/>.
    /// </summary>
    /// <remarks>
    /// <para>Each state represents a discrete phase of the game — main menu, gameplay,
    /// pause, game over, and similar. Transitions between states are managed by
    /// <see cref="GameStateMachine.TransitionToAsync"/>.</para>
    /// <para>Enter and exit are async to support loading screens, fade transitions,
    /// and other operations that span multiple frames before a state is fully active
    /// or fully exited.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class GameplayState : IGameState
    /// {
    ///     public async Task OnEnterAsync()
    ///     {
    ///         await SceneLoader.LoadAsync("Gameplay");
    ///         GameEventManager.Publish(new GameplayStartedEvent());
    ///     }
    ///
    ///     public Task OnExitAsync()
    ///     {
    ///         GameEventManager.Publish(new GameplayEndedEvent());
    ///         return Task.CompletedTask;
    ///     }
    ///
    ///     public void OnTick(float deltaTime) { }
    /// }
    /// </code>
    /// </example>
    public interface IGameState
    {
        /// <summary>
        /// Used to define the type of state this is.
        /// For boolean comparisions.
        /// </summary>
        GameStateType StateType { get; }

        /// <summary>
        /// Called when the state machine transitions into this state.
        /// Awaited before the state begins ticking.
        /// </summary>
        Task OnEnterAsync();

        /// <summary>
        /// Called when the state machine transitions out of this state.
        /// Awaited before the incoming state's <see cref="OnEnterAsync"/> is called.
        /// </summary>
        Task OnExitAsync();

        /// <summary>
        /// Called every frame while this state is active, driven by <see cref="GameBootstrapper"/>.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds since the last frame.</param>
        void OnTick(float deltaTime);
    }
}