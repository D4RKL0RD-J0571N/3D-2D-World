namespace Systems.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Gameplay.Entities;
    using UnityEngine;

    /// <summary>
    /// Event data dispatched by SKSE custom mod event listeners.
    /// </summary>
    public class SKSEModEvent : GameEvent
    {
        public string EventName;
        public string StringArg;
        public float NumericArg;

        public SKSEModEvent(string eventName, string strArg, float numArg)
        {
            EventName = eventName;
            StringArg = strArg;
            NumericArg = numArg;
        }
    }

    /// <summary>
    /// Core engine bridge emulating the Skyrim Script Extender (SKSE64).
    /// Exposes native engine hooks, player actor access, custom mod event dispatching,
    /// and script-level delegation for community plugins.
    /// </summary>
    public static class SKSE
    {
        #region Constants

        public const string VersionString = "SKSE64 2.2.6 (runtime 1.6.1170)";
        public const int VersionMajor = 2;
        public const int VersionMinor = 2;
        public const int VersionBeta = 6;

        #endregion

        #region Fields

        private static readonly Dictionary<string, List<Action<string, float>>> _eventListeners = new();
        private static GameObject _playerOverride;

        #endregion

        #region Public API

        /// <summary>
        /// Explicitly overrides the player GameObject reference (useful for testing or dedicated cameras).
        /// </summary>
        public static void SetPlayerOverride(GameObject player)
        {
            _playerOverride = player;
        }

        /// <summary>
        /// Retrieves the active player actor GameObject if spawned.
        /// </summary>
        public static GameObject GetPlayer()
        {
            if (_playerOverride != null) return _playerOverride;
            return GameObject.FindWithTag("Player");
        }

        /// <summary>
        /// Retrieves the player's <see cref="BaseActor"/> entity component.
        /// </summary>
        public static BaseActor GetPlayerActor()
        {
            var p = GetPlayer();
            return p != null ? p.GetComponent<BaseActor>() : null;
        }

        /// <summary>
        /// Registers a mod callback for a named SKSE mod event.
        /// </summary>
        public static void RegisterForModEvent(string eventName, Action<string, float> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            if (!_eventListeners.TryGetValue(eventName, out var list))
            {
                list = new List<Action<string, float>>();
                _eventListeners[eventName] = list;
            }

            if (!list.Contains(callback)) list.Add(callback);
        }

        /// <summary>
        /// Unregisters a mod callback for a named SKSE mod event.
        /// </summary>
        public static void UnregisterForModEvent(string eventName, Action<string, float> callback)
        {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            if (_eventListeners.TryGetValue(eventName, out var list))
            {
                list.Remove(callback);
            }
        }

        /// <summary>
        /// Dispatches an SKSE mod event to all registered listeners and the global GameEventManager.
        /// </summary>
        public static void SendModEvent(string eventName, string strArg = "", float numArg = 0f)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            if (_eventListeners.TryGetValue(eventName, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                {
                    try
                    {
                        list[i]?.Invoke(strArg, numArg);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[SKSE] Error invoking mod event '{eventName}': {ex.Message}");
                    }
                }
            }

            GameEventManager.Publish(new SKSEModEvent(eventName, strArg, numArg));
        }

        /// <summary>
        /// Clears all event listener subscriptions and player overrides (used on session shutdown).
        /// </summary>
        public static void Clear()
        {
            _eventListeners.Clear();
            _playerOverride = null;
        }

        #endregion
    }
}
