namespace Domain.Museum
{
    using Core;

    /// <summary>
    /// Published when the player character inspects or approaches a museum exhibit.
    /// </summary>
    public sealed class ExhibitInspectedEvent : GameEvent
    {
        #region Properties

        public MuseumExhibitDefinitionSO Exhibit { get; }
        public CuratorEvaluationRecord CurrentEvaluation { get; }

        #endregion

        #region Constructors

        public ExhibitInspectedEvent(MuseumExhibitDefinitionSO exhibit, CuratorEvaluationRecord currentEvaluation)
        {
            Exhibit = exhibit;
            CurrentEvaluation = currentEvaluation;
        }

        #endregion
    }

    /// <summary>
    /// Published when the player updates the evaluation rating or notes for an exhibit.
    /// </summary>
    public sealed class ExhibitEvaluatedEvent : GameEvent
    {
        #region Properties

        public string ExhibitId { get; }
        public CuratorRating Rating { get; }
        public string Notes { get; }

        #endregion

        #region Constructors

        public ExhibitEvaluatedEvent(string exhibitId, CuratorRating rating, string notes)
        {
            ExhibitId = exhibitId;
            Rating = rating;
            Notes = notes;
        }

        #endregion
    }

    /// <summary>
    /// Published when the player exits the inspection radius of an exhibit or closes the panel.
    /// </summary>
    public sealed class ExhibitInspectionClosedEvent : GameEvent
    {
    }

    /// <summary>
    /// Published to toggle the Curator evaluation HUD overlay.
    /// </summary>
    public sealed class ToggleCuratorHudEvent : GameEvent
    {
    }

    /// <summary>
    /// Published when the curator evaluation manifest is written to disk.
    /// </summary>
    public sealed class MuseumReportExportedEvent : GameEvent
    {
        #region Properties

        public string FilePath { get; }
        public int TotalEvaluations { get; }

        #endregion

        #region Constructors

        public MuseumReportExportedEvent(string filePath, int totalEvaluations)
        {
            FilePath = filePath;
            TotalEvaluations = totalEvaluations;
        }

        #endregion
    }
}
