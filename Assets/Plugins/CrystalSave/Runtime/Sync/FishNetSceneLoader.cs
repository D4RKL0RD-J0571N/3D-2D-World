#if MEMORYPACK && ARAWN_REMEMBERME && REMEMBERME_FISHNET_PRESENT
using System;
using System.Threading.Tasks;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Arawn.CrystalSave.Runtime.Sync
{
    /// <summary>Primes a saved slot before a FishNet global build-settings scene load.</summary>
    /// <remarks>
    /// Server/host only. Completion means the local SDK scene load has finished; client acknowledgements
    /// and deferred prefab application remain managed by FishNet and Crystal Save respectively.
    /// This does not spawn FishNet NetworkObjects or apply incoming sync envelopes to gameplay.
    /// </remarks>
    public sealed class FishNetSceneLoader
    {
        private readonly NetworkManager networkManager;
        private readonly bool hasNetworkManager;

        public FishNetSceneLoader(NetworkManager networkManager = null)
        {
            this.networkManager = networkManager;
            hasNetworkManager = networkManager != null;
        }

        public async Task<Scene> LoadSceneAsync(string sceneName, int saveSlot,
            ReplaceOption replaceScenes = ReplaceOption.None, float timeoutSeconds = 60f)
        {
            var network = hasNetworkManager ? networkManager : InstanceFinder.NetworkManager;
            RequireServer(network);
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
                throw new ArgumentException("The scene must be enabled in the player's build settings.", nameof(sceneName));
            if (float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            if (replaceScenes != ReplaceOption.None && replaceScenes != ReplaceOption.All && replaceScenes != ReplaceOption.OnlineOnly)
                throw new ArgumentOutOfRangeException(nameof(replaceScenes));
            var manager = SaveManager.Instance;
            if (!manager) throw new InvalidOperationException("Crystal Save is unavailable.");
            Scene loaded = default;
            await manager.RunSceneWorkflowAsync(async context =>
            {
                RequireServer(network);
                // Own preparation and transition under the same FIFO entry; never queue recursively.
                await manager.PopulatePrefabsCoreAsync(context, saveSlot);
                RequireServer(network);
                var data = new SceneLoadData(sceneName)
                {
                    ReplaceScenes = replaceScenes,
                    PreferredActiveScene = new PreferredScene(new SceneLookupData(sceneName))
                };
                bool finished = false;
                void OnLoaded(SceneLoadEndEventArgs args)
                {
                    if (!args.QueueData.AsServer || !ReferenceEquals(args.QueueData.SceneLoadData, data)) return;
                    foreach (var scene in args.LoadedScenes)
                        if (scene.IsValid() && scene.isLoaded) { loaded = scene; break; }
                    if (!loaded.IsValid())
                        loaded = UnitySceneManager.GetSceneByName(System.IO.Path.GetFileNameWithoutExtension(sceneName));
                    finished = true;
                }
                var scenes = network.SceneManager;
                scenes.OnLoadEnd += OnLoaded;
                manager.IsInSceneTransitionInternal = true;
                try
                {
                    float deadline = Time.realtimeSinceStartup + timeoutSeconds;
                    scenes.LoadGlobalScenes(data);
                    while (!finished)
                    {
                        RequireServer(network);
                        if (Time.realtimeSinceStartup >= deadline)
                            throw new TimeoutException("FishNet scene load did not finish. An SDK load already started cannot be rolled back.");
                        await Task.Yield();
                    }
                    if (!loaded.IsValid() || !loaded.isLoaded)
                        throw new InvalidOperationException("FishNet did not load the requested scene.");
                }
                finally
                {
                    if (scenes) scenes.OnLoadEnd -= OnLoaded;
                    if (manager) manager.IsInSceneTransitionInternal = false;
                }
            });
            return loaded;
        }

        private static void RequireServer(NetworkManager manager)
        {
            if (!manager || !manager.IsServerStarted || !manager.SceneManager)
                throw new InvalidOperationException("Only a running FishNet server or host can initiate a Crystal Save scene load.");
        }
    }
}
#endif
