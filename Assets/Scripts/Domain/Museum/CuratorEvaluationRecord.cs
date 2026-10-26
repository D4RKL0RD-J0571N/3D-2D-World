namespace Domain.Museum
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Persistent evaluation record for an exhibit in the Museum Showroom.
    /// </summary>
    [Serializable]
    public sealed class CuratorEvaluationRecord
    {
        #region Fields

        public string ExhibitId;
        public CuratorRating Rating;
        public string Notes;
        public string EvaluatedAtUtc;

        #endregion

        #region Constructors

        public CuratorEvaluationRecord()
        {
            Rating = CuratorRating.Unrated;
            Notes = string.Empty;
            EvaluatedAtUtc = string.Empty;
        }

        public CuratorEvaluationRecord(string exhibitId, CuratorRating rating, string notes)
        {
            ExhibitId = exhibitId;
            Rating = rating;
            Notes = notes ?? string.Empty;
            EvaluatedAtUtc = DateTime.UtcNow.ToString("o");
        }

        #endregion
    }

    /// <summary>
    /// Container for serializing all museum evaluations into JSON.
    /// </summary>
    [Serializable]
    public sealed class MuseumEvaluationReport
    {
        #region Fields

        public string LastUpdatedUtc;
        public List<CuratorEvaluationRecord> Records = new List<CuratorEvaluationRecord>();

        #endregion
    }
}
