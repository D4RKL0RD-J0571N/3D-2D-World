namespace Core.Persistence
{
    using Core.Ids;

    /// <summary>
    /// Implemented by any <see cref="IGameService"/> or <see cref="Core.Modules.IEngineModule"/> that owns
    /// session-wide state persisted inside <see cref="SaveEnvelope.ModuleStates"/>
    /// (e.g., <c>SkillProgressionService</c>, <c>FactionService</c>, <c>RadiantQuestService</c>).
    /// </summary>
    public interface ISaveableService
    {
        /// <summary>Canonical namespaced key for this service's save block (e.g., <c>framework:state/progression</c>).</summary>
        ResourceId SaveStateKey { get; }

        /// <summary>Schema version of the state object returned by <see cref="CaptureState"/>.</summary>
        int CurrentStateSchemaVersion { get; }

        /// <summary>Captures a serializable DTO representing the service's current runtime state.</summary>
        object CaptureState();

        /// <summary>Restores the service's runtime state from a JSON string and schema version.</summary>
        void RestoreState(string serializedJsonState, int savedSchemaVersion);
    }
}
