namespace Systems.Museum
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Core;
    using Domain.Museum;
    using UnityEngine;

    /// <summary>
    /// Headless system service managing exhibit evaluation ratings, notes, and report export.
    /// Registered into <see cref="ServiceLocator"/> during application bootstrap.
    /// </summary>
    public sealed class CuratorService : IGameService
    {
        #region Fields

        private readonly Dictionary<string, CuratorEvaluationRecord> _evaluations = new Dictionary<string, CuratorEvaluationRecord>();
        private string _reportFilePath;

        #endregion

        #region IGameService Lifecycle

        public void Initialize()
        {
            _reportFilePath = Path.Combine(Application.dataPath, "Data", "MuseumEvaluationReport.json");
            LoadReport();

            GameEventManager.AddListener<ExhibitEvaluatedEvent>(OnExhibitEvaluated);
            Debug.Log($"[CuratorService] Initialized. Loaded {_evaluations.Count} evaluation records from {_reportFilePath}");
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<ExhibitEvaluatedEvent>(OnExhibitEvaluated);
            SaveReport();
            _evaluations.Clear();
            Debug.Log("[CuratorService] Shutdown complete.");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Retrieves the current evaluation record for the specified exhibit, or creates a default unrated one.
        /// </summary>
        public CuratorEvaluationRecord GetEvaluation(string exhibitId)
        {
            if (string.IsNullOrEmpty(exhibitId))
            {
                return new CuratorEvaluationRecord(string.Empty, CuratorRating.Unrated, string.Empty);
            }

            if (_evaluations.TryGetValue(exhibitId, out var record))
            {
                return record;
            }

            var newRecord = new CuratorEvaluationRecord(exhibitId, CuratorRating.Unrated, string.Empty);
            _evaluations[exhibitId] = newRecord;
            return newRecord;
        }

        /// <summary>
        /// Explicitly forces serialization of all records to disk and publishes an export event.
        /// </summary>
        public void ExportReport()
        {
            SaveReport();
            GameEventManager.Publish(new MuseumReportExportedEvent(_reportFilePath, _evaluations.Count));
            Debug.Log($"[CuratorService] Exported museum evaluation report to {_reportFilePath}");
        }

        #endregion

        #region Event Handlers

        private void OnExhibitEvaluated(ExhibitEvaluatedEvent evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.ExhibitId)) return;

            var record = new CuratorEvaluationRecord(evt.ExhibitId, evt.Rating, evt.Notes);
            _evaluations[evt.ExhibitId] = record;
            SaveReport();
        }

        #endregion

        #region Persistence Helpers

        private void LoadReport()
        {
            try
            {
                if (!File.Exists(_reportFilePath))
                {
                    return;
                }

                string json = File.ReadAllText(_reportFilePath);
                var report = JsonUtility.FromJson<MuseumEvaluationReport>(json);
                if (report != null && report.Records != null)
                {
                    _evaluations.Clear();
                    foreach (var record in report.Records)
                    {
                        if (record != null && !string.IsNullOrEmpty(record.ExhibitId))
                        {
                            _evaluations[record.ExhibitId] = record;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CuratorService] Failed to load museum report: {ex.Message}");
            }
        }

        private void SaveReport()
        {
            try
            {
                var dir = Path.GetDirectoryName(_reportFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var report = new MuseumEvaluationReport
                {
                    LastUpdatedUtc = DateTime.UtcNow.ToString("o"),
                    Records = new List<CuratorEvaluationRecord>(_evaluations.Values)
                };

                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(_reportFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CuratorService] Failed to save museum report: {ex.Message}");
            }
        }

        #endregion
    }
}
