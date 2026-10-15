namespace Core
{
    /// <summary>
    /// Contract for services that require a per-frame update.
    /// </summary>
    /// <remarks>
    /// <para>Implement alongside <see cref="IGameService"/> on any service that needs to run
    /// logic every frame — input polling, state machine ticking, animation updates, and similar.</para>
    /// <para>Tickable services are driven explicitly by <see cref="GameBootstrapper.Update"/>,
    /// not by Unity's MonoBehaviour lifecycle. This keeps update order deterministic
    /// and avoids hidden per-frame dependencies between systems.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class InputManager : IGameService, ITickable
    /// {
    ///     public void Initialize() { }
    ///     public void Shutdown()   { }
    ///
    ///     public void Tick(float deltaTime)
    ///     {
    ///         // poll input and publish events
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface ITickable
    {
        /// <summary>
        /// Called once per frame by <see cref="GameBootstrapper"/>.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds since the last frame (<c>Time.deltaTime</c>).</param>
        void Tick(float deltaTime);
    }
}