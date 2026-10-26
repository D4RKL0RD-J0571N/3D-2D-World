#if MEMORYPACK && ARAWN_REMEMBERME
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Compatibility facade for prefab restoration. SaveManager owns the queue,
    /// application outcome and retry data for every entry point.
    /// </summary>
    public class PrefabRestoreService
    {
        readonly SaveManager manager;

        public PrefabRestoreService(SaveManager manager, PrefabManager prefabManager, GameObjectTracker tracker)
        {
            this.manager = manager;
        }

        public void RestoreDestroyedPrefab(string uniqueID, SaveData data = null)
            => manager.RestoreDestroyedPrefab(uniqueID, data);
        public void RestoreDestroyedPrefabByAssetID(string prefabAssetID, SaveData data = null)
            => manager.RestoreDestroyedPrefabByAssetID(prefabAssetID, data);
        public void RestoreAllDestroyedPrefabs(SaveData data = null)
            => manager.RestoreAllDestroyedPrefabs(data);
        public bool RestoreAllDestroyedPrefabsFromCurrentData()
            => manager.RestoreAllDestroyedPrefabsFromCurrentData();
        public Task<bool> RestoreAllDestroyedPrefabsFromCurrentDataWithRetryAsync(int maxRetries = 3, int retryDelayMs = 500)
            => manager.RestoreAllDestroyedPrefabsFromCurrentDataWithRetryAsync(maxRetries, retryDelayMs);
        public Task<bool> RestoreDestroyedPrefabWithRetryAsync(string uniqueID, int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => manager.RestoreDestroyedPrefabWithRetryAsync(uniqueID, slotNumber, maxRetries, retryDelayMs);
        public void RestoreDestroyedPrefab(string uniqueID, int slotNumber)
            => manager.RestoreDestroyedPrefab(uniqueID, slotNumber);
        public Task<bool> RestoreDestroyedPrefabByAssetIDWithRetryAsync(string prefabAssetID, int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => manager.RestoreDestroyedPrefabByAssetIDWithRetryAsync(prefabAssetID, slotNumber, maxRetries, retryDelayMs);
        public void RestoreDestroyedPrefabByAssetID(string prefabAssetID, int slotNumber)
            => manager.RestoreDestroyedPrefabByAssetID(prefabAssetID, slotNumber);
        public Task<bool> RestoreAllDestroyedPrefabsWithRetryAsync(int slotNumber, int maxRetries = 3, int retryDelayMs = 500)
            => manager.RestoreAllDestroyedPrefabsWithRetryAsync(slotNumber, maxRetries, retryDelayMs);
        public void RestoreAllDestroyedPrefabs(int slotNumber)
            => manager.RestoreAllDestroyedPrefabs(slotNumber);

        /// <summary>Retained for API compatibility; the manager's FIFO resumes queued requests automatically.</summary>
        public void ProcessPendingRestores() { }
    }
}
#endif
