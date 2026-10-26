using GameCreator.Runtime.Characters;
using UnityEngine;

namespace GameCreator.Runtime.Common
{
    public static class ShortcutPlayer
    {
        #if UNITY_EDITOR

        [UnityEditor.InitializeOnEnterPlayMode]
        private static void InitializeOnEnterPlayMode() => Instance = null;

        #endif

        // PROPERTIES: ----------------------------------------------------------------------------

        public static GameObject Instance { get; private set; }
        
        public static Transform Transform => Instance != null ? Instance.transform : null;

        // PUBLIC METHODS: ------------------------------------------------------------------------

        public static TComponent Get<TComponent>() where TComponent : Component
        {
            return Instance != null ? Instance.Get<TComponent>() : null;
        }
        
        public static void Change(GameObject player)
        {
            Instance = player;
        }
    }
}