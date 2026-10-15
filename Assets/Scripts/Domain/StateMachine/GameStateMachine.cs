namespace Domain.Entities.StateMachine
{
    using System.Threading.Tasks;
    using Core;

    /// <summary>
    /// Manages game flow by maintaining a single active <see cref="IGameState"/>
    /// and handling async transitions between states.
    /// </summary>
    /// <remarks>
    /// <para>Registered with <see cref="ServiceLocator"/> in <see cref="GameBootstrapper"/>
    /// and ticked each frame via <see cref="ITickable.Tick"/>.</para>
    /// <para>Transitioning to <c>null</c> is valid and clears the active state silently.</para>
    /// <para>Transitions are sequential — the outgoing state fully exits before the
    /// incoming state enters. Calling <see cref="TransitionToAsync"/> while a transition
    /// is already in progress is not thread-safe; avoid overlapping calls.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var stateMachine = ServiceLocator.Get&lt;GameStateMachine&gt;();
    /// await stateMachine.TransitionToAsync(new MainMenuState());
    /// </code>
    /// </example>
    public class GameStateMachine : IGameService, ITickable
    {
        private IGameState _currentState;

        /// <summary>
        /// The currently active state, or <c>null</c> if no state is active.
        /// </summary>
        public IGameState CurrentState => _currentState;

        // New property for easy comparison by external systems (e.g., InputManager)
        public GameStateType CurrentStateType => _currentState != null ? _currentState.StateType : GameStateType.None;

        /// <summary>
        /// Transitions from the current state to <paramref name="newState"/>.
        /// Calls <see cref="IGameState.OnExitAsync"/> on the outgoing state,
        /// then <see cref="IGameState.OnEnterAsync"/> on the incoming state.
        /// Either state may be <c>null</c>.
        /// </summary>
        /// <param name="newState">The state to transition into, or <c>null</c> to clear the active state.</param>
        public async Task TransitionToAsync(IGameState newState)
        {
            if (_currentState != null)
                await _currentState.OnExitAsync();

            _currentState = newState;

            if (_currentState != null)
                await _currentState.OnEnterAsync();
        }

        /// <summary>
        /// Forwards the frame tick to the active state's <see cref="IGameState.OnTick"/>.
        /// Called by <see cref="GameBootstrapper"/> each frame. No-op if no state is active.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds since the last frame.</param>
        public void Tick(float deltaTime)
        {
            _currentState?.OnTick(deltaTime);
        }

        /// <inheritdoc/>
        public void Initialize() { }

        /// <inheritdoc/>
        public void Shutdown() { }
    }
}