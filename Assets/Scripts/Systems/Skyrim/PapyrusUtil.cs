namespace Systems.Skyrim
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using UnityEngine;

    /// <summary>
    /// Runtime scripting and storage utility library emulating PapyrusUtil SE.
    /// Provides persistent JSON storage, dynamic typed lists, and actor variable bags for modders.
    /// </summary>
    public static class PapyrusUtil
    {
        #region Fields

        private static readonly Dictionary<string, string> _jsonStorage = new();
        private static readonly Dictionary<string, List<string>> _stringLists = new();
        private static readonly Dictionary<string, List<float>> _floatLists = new();
        private static readonly Dictionary<string, Dictionary<string, object>> _actorVariables = new();

        #endregion

        #region JSON API

        /// <summary>
        /// Serializes and stores an object under a unique key string.
        /// </summary>
        public static bool JsonSave<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key)) return false;
            try
            {
                string json = JsonConvert.SerializeObject(data);
                _jsonStorage[key] = json;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PapyrusUtil] Failed to JsonSave '{key}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deserializes an object stored under a key.
        /// </summary>
        public static T JsonLoad<T>(string key)
        {
            if (string.IsNullOrEmpty(key) || !_jsonStorage.TryGetValue(key, out string json))
            {
                return default;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PapyrusUtil] Failed to JsonLoad '{key}': {ex.Message}");
                return default;
            }
        }

        #endregion

        #region Dynamic Lists

        public static void StringListAdd(string listKey, string value)
        {
            if (string.IsNullOrEmpty(listKey)) return;
            if (!_stringLists.TryGetValue(listKey, out var list))
            {
                list = new List<string>();
                _stringLists[listKey] = list;
            }
            list.Add(value);
        }

        public static int StringListCount(string listKey)
        {
            return _stringLists.TryGetValue(listKey, out var list) ? list.Count : 0;
        }

        public static string StringListGet(string listKey, int index)
        {
            if (_stringLists.TryGetValue(listKey, out var list) && index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return string.Empty;
        }

        public static void FloatListAdd(string listKey, float value)
        {
            if (string.IsNullOrEmpty(listKey)) return;
            if (!_floatLists.TryGetValue(listKey, out var list))
            {
                list = new List<float>();
                _floatLists[listKey] = list;
            }
            list.Add(value);
        }

        public static float FloatListGet(string listKey, int index)
        {
            if (_floatLists.TryGetValue(listKey, out var list) && index >= 0 && index < list.Count)
            {
                return list[index];
            }
            return 0f;
        }

        #endregion

        #region Actor Variables

        public static void SetActorVariable(string actorId, string varName, object value)
        {
            if (string.IsNullOrEmpty(actorId) || string.IsNullOrEmpty(varName)) return;

            if (!_actorVariables.TryGetValue(actorId, out var dict))
            {
                dict = new Dictionary<string, object>();
                _actorVariables[actorId] = dict;
            }
            dict[varName] = value;
        }

        public static T GetActorVariable<T>(string actorId, string varName, T defaultValue = default)
        {
            if (_actorVariables.TryGetValue(actorId, out var dict) && dict.TryGetValue(varName, out object obj))
            {
                if (obj is T typed) return typed;
            }
            return defaultValue;
        }

        #endregion

        #region Cleanup

        public static void Clear()
        {
            _jsonStorage.Clear();
            _stringLists.Clear();
            _floatLists.Clear();
            _actorVariables.Clear();
        }

        #endregion
    }
}
