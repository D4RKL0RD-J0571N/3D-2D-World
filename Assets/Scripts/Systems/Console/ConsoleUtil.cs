namespace Systems.Console
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Domain.Console;
    using Gameplay.UI;
    using UnityEngine;

    /// <summary>
    /// Engine bridge emulating ConsoleUtilSSE (Nexus mod 24858).
    /// Provides an extensible scripting API to programmatically execute developer console commands,
    /// read/write console output, and track the currently selected reference in console mode.
    /// </summary>
    public static class ConsoleUtil
    {
        #region Fields

        private static GameObject _selectedReference;
        private static InspectedObjectData _selectedData;
        private static readonly List<string> _commandHistory = new();

        #endregion

        #region Events

        public static event Action<GameObject, InspectedObjectData> SelectedReferenceChanged;
        public static event Action<GameObject> OnSelectionChanged;
        public static event Action<string> ConsoleMessageLogged;
        public static event Action<string> OnLogMessage;

        #endregion

        #region Properties

        public static GameObject SelectedReference => _selectedReference;
        public static InspectedObjectData SelectedData => _selectedData;
        public static IReadOnlyList<string> CommandHistory => _commandHistory;

        #endregion

        #region Public API

        /// <summary>
        /// Programmatically executes a console command line through the registry.
        /// </summary>
        public static bool ExecuteCommand(string commandLine, ConsoleCommandRegistry registry = null)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return false;

            _commandHistory.Add(commandLine);
            var targetRegistry = registry ?? CommandConsoleHUD.Registry;
            if (targetRegistry == null) return false;

            return targetRegistry.ExecuteLine(commandLine, PrintMessage);
        }

        /// <summary>
        /// Retrieves the currently targeted reference in console mode.
        /// </summary>
        public static GameObject GetSelectedReference()
        {
            return _selectedReference;
        }

        /// <summary>
        /// Sets the active console reference and extracts its inspected metadata.
        /// </summary>
        public static void SetSelectedReference(GameObject reference, GameObject player = null)
        {
            _selectedReference = reference;
            _selectedData = reference != null ? InspectedObjectData.FromGameObject(reference, player) : null;
            SelectedReferenceChanged?.Invoke(_selectedReference, _selectedData);
            OnSelectionChanged?.Invoke(_selectedReference);
        }

        /// <summary>
        /// Clears the currently selected reference.
        /// </summary>
        public static void ClearSelectedReference()
        {
            SetSelectedReference(null, null);
        }

        /// <summary>
        /// Prints a message to the active console log.
        /// </summary>
        public static void PrintMessage(string message)
        {
            ConsoleMessageLogged?.Invoke(message);
            OnLogMessage?.Invoke(message);
        }

        /// <summary>
        /// Resets state on session shutdown.
        /// </summary>
        public static void Clear()
        {
            _selectedReference = null;
            _selectedData = null;
            _commandHistory.Clear();
        }

        #endregion
    }
}
