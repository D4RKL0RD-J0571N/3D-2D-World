#if MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_PURRNET_PRESENT
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ADDRESSABLES_PRESENT
using UnityEngine.AddressableAssets;
using PurrNet;
using PurrNet.Modules;
#endif

namespace Arawn.CrystalSave.Runtime.Sync
{
#if ADDRESSABLES_PRESENT
    /// <summary>
    /// An orchestrator for loading Addressable scenes in a PurrNet multiplayer environment,
    /// integrated with Crystal Save for syncing state across the network.
    /// </summary>
    public class PurrNetAddressableSceneLoader : SceneLoadOrchestratorBase
    {
        private int currentSaveSlot = 0;

        public void SetSaveSlot(int slotNumber)
        {
            currentSaveSlot = slotNumber;
        }

        public override async Task OnScenePreLoad(string sceneName)
        {
            // Prime Crystal Save with prefab data BEFORE the scene loads across the network
            await SaveManager.Instance.PopulatePendingPrefabsFromSlotAsync(currentSaveSlot);
            Debug.Log($"[PurrNetAddressableSceneLoader] Primed prefabs from slot {currentSaveSlot}");
        }

        public override void OnSceneActivated(Scene scene)
        {
            Debug.Log($"[PurrNetAddressableSceneLoader] Scene '{scene.name}' activated. Crystal Save will spawn network entities.");
        }

        /// <summary>
        /// Loads a scene via PurrNet's Addressable system and integrates Crystal Save.
        /// </summary>
        public async Task LoadSceneAsync(AssetReference sceneRef, int saveSlot, PurrSceneSettings settings = default)
        {
            if (NetworkManager.main == null || !NetworkManager.main.isServer)
            {
                Debug.LogWarning("[PurrNetAddressableSceneLoader] Only the server can initiate an Addressable scene load via PurrNet.");
                return;
            }

            currentSaveSlot = saveSlot;

            // Trigger OnScenePreLoad to prepare Crystal Save
            await OnScenePreLoad("PurrNetAddressableScene");

            var sceneModule = NetworkManager.main.sceneModule;
            if (sceneModule == null)
            {
                Debug.LogError("[PurrNetAddressableSceneLoader] PurrNet ScenesModule is missing!");
                return;
            }

            // Tell PurrNet to load the Addressable scene
            var handle = sceneModule.LoadAddressableSceneAsync(sceneRef, settings);
            
            while (!handle.IsDone)
            {
                await Task.Yield();
            }

            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                Scene loadedScene = handle.Result.Scene;
                
                await OnSceneWillActivate(loadedScene);
                OnSceneActivated(loadedScene);
                
                // Wait a frame for NetworkIdentities to spawn and Crystal Save to populate them
                await Task.Yield();
            }
            else
            {
                Debug.LogError($"[PurrNetAddressableSceneLoader] Failed to load Addressable scene: {handle.OperationException}");
            }
        }
    }
#endif
}
#endif
