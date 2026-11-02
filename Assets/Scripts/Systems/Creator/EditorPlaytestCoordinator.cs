namespace Systems.Creator
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;
    using Core.Persistence;
    using Domain.Creator;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Coordinates transitioning between In-Game Editor Authoring Mode and Live Playtest Mode (<c>ARCH-05</c>, <c>EDT-02</c>).
    /// Captures a lossless in-memory snapshot of both <see cref="CreatorLevelData"/> and all entity component states
    /// before Playtest begins, and restores the exact pre-playtest scene upon exiting Playtest.
    /// </summary>
    public sealed class EditorPlaytestCoordinator
    {
        private readonly CreatorStudioService _studio;
        private bool _isPlaytesting;
        private string _authoredLevelSnapshotJson;
        private readonly Dictionary<InstanceGuid, EntitySaveRecord> _prePlaytestEntityStates = new Dictionary<InstanceGuid, EntitySaveRecord>();

        public bool IsPlaytesting => _isPlaytesting;

        public EditorPlaytestCoordinator(CreatorStudioService studio)
        {
            _studio = studio ?? throw new ArgumentNullException(nameof(studio));
        }

        /// <summary>
        /// Snapshots the authored world and entity states in RAM and transitions to Live Playtest Mode.
        /// </summary>
        public void EnterPlaytest()
        {
            if (_isPlaytesting)
            {
                return;
            }

            var jsonSettings = FantasyEngineJsonSettings.CreateDefault();

            // 1. Capture CreatorLevelData (WorldAuthoringData v2)
            CreatorLevelData levelData = _studio.CaptureLevelData("PlaytestSnapshot");
            _authoredLevelSnapshotJson = JsonConvert.SerializeObject(levelData, jsonSettings);

            // 2. Capture ISaveableComponent states (Chest lock state, Actor HP/attributes, Lever state) for all placed objects
            _prePlaytestEntityStates.Clear();
            foreach (var kvp in _studio.PlacedByGuid)
            {
                InstanceGuid guid = kvp.Key;
                GameObject go = kvp.Value;
                if (go == null) continue;

                var pec = go.GetComponent<PersistentEntityComponent>();
                if (pec == null)
                {
                    pec = go.AddComponent<PersistentEntityComponent>();
                }
                pec.InstanceId = guid;

                _prePlaytestEntityStates[guid] = pec.CaptureRecord(jsonSettings);
            }

            _isPlaytesting = true;
            _studio.SetBuildMode(false);
        }

        /// <summary>
        /// Purges runtime playtest mutations and reconstructs the exact authored world and component states from RAM (<c>EDT-02</c>).
        /// Preserves the user's <see cref="ICommandHistory"/> Undo/Redo stack.
        /// </summary>
        public void ExitPlaytest()
        {
            if (!_isPlaytesting)
            {
                return;
            }

            var jsonSettings = FantasyEngineJsonSettings.CreateDefault();

            if (!string.IsNullOrWhiteSpace(_authoredLevelSnapshotJson))
            {
                var levelData = JsonConvert.DeserializeObject<CreatorLevelData>(_authoredLevelSnapshotJson, jsonSettings);
                if (levelData != null)
                {
                    _studio.RestoreLevelData(levelData, clearCommandHistory: false);

                    // Restore pre-playtest component states (locked chests, 100% actor HP, unpulled levers, etc.)
                    foreach (var kvp in _prePlaytestEntityStates)
                    {
                        if (_studio.TryGetPlacedObject(kvp.Key, out GameObject go) && go != null)
                        {
                            var pec = go.GetComponent<PersistentEntityComponent>();
                            if (pec == null)
                            {
                                pec = go.AddComponent<PersistentEntityComponent>();
                                pec.InstanceId = kvp.Key;
                            }

                            pec.RestoreRecord(kvp.Value, _studio);
                        }
                    }
                }
            }

            _prePlaytestEntityStates.Clear();
            _authoredLevelSnapshotJson = null;
            _isPlaytesting = false;
            _studio.SetBuildMode(true);
        }
    }
}
