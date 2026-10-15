namespace Core
{
    /// <summary>
    /// Contract for all runtime services registered with <see cref="ServiceLocator"/>.
    /// </summary>
    /// <remarks>
    /// <para>Implement this interface on any class that acts as a shared runtime system —
    /// audio, input, save, UI, state machine, and similar managers.</para>
    /// <para><see cref="Initialize"/> is called automatically by <see cref="ServiceLocator.Register{T}"/>
    /// immediately after registration. <see cref="Shutdown"/> is called by
    /// <see cref="ServiceLocator.Unregister{T}"/> or <see cref="ServiceLocator.Clear"/>.</para>
    /// <para>Services that require a per-frame update should also implement <see cref="ITickable"/>
    /// and be ticked explicitly from <see cref="GameBootstrapper"/>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class AudioManager : IGameService
    /// {
    ///     public void Initialize() { /* load audio banks, set initial volume */ }
    ///     public void Shutdown()   { /* stop all sounds, release resources  */ }
    /// }
    /// </code>
    /// </example>
    public interface IGameService
    {
        /// <summary>
        /// Called once immediately after the service is registered with <see cref="ServiceLocator"/>.
        /// Use for setup logic that requires all constructor dependencies to be in place.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called when the service is unregistered or when <see cref="ServiceLocator.Clear"/> is invoked.
        /// Use for cleanup: unsubscribing events, releasing resources, stopping coroutines.
        /// </summary>
        void Shutdown();
    }
}