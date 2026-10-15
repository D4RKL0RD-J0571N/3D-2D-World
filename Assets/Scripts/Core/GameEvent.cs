namespace Core
{
    /// <summary>
    /// Base class for all game events published through <see cref="GameEventManager"/>.
    /// </summary>
    /// <remarks>
    /// Derive from this class to define a strongly-typed event.
    /// Events carry data as public fields or properties on the derived class.
    /// </remarks>
    /// <example>
    /// <code>
    /// public class PlayerDiedEvent : GameEvent
    /// {
    ///     public int FinalScore;
    /// }
    /// </code>
    /// </example>
    public abstract class GameEvent { }
}