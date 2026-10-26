#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    internal static class PrefabReusePolicy
    {
        internal static bool IsParticipant(SaveablePrefab prefab) => prefab != null && (prefab.RegisterWithSaveSystem || prefab.IsTrackedBySaveManager);
        internal static bool IsSceneBacked(SaveablePrefab prefab)
        {
            if (prefab == null || prefab.gameObject == null)
                return false;
            if (prefab.IsAddedAtRuntime)
                return false;
            var scene = prefab.gameObject.scene;
            bool nonPersistentDontDestroy = scene.IsValid() && string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal) && !prefab.KeepAcrossScenes;
            return !nonPersistentDontDestroy;
        }
    }
}
#endif
