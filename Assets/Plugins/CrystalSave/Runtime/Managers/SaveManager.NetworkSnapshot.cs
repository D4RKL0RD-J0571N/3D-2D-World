#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
        /// <summary>Capture live state through the operation queue without writing a slot or broadcasting a save.</summary>
        public Task<byte[]> CaptureWorldSnapshotAsync(CancellationToken cancellationToken = default)
            => Operations.RunAsync(async context =>
            {
                await AwaitSnapshotReady(context);
                var previous = CurrentSaveData;
                try
                {
                    var data = SaveOperations.CaptureCandidate(SceneManager.GetActiveScene().name);
                    context.CheckCancellation();
                    return Serializer.Serialize(data);
                }
                finally { CurrentSaveData = previous; }
            }, cancellationToken);

        /// <summary>Apply a detached world snapshot to loaded scenes, awaiting active helper restoration.
        /// Network scene transitions and network object spawning remain owned by the networking SDK.</summary>
        public Task ApplyWorldSnapshotAsync(byte[] snapshot, CancellationToken cancellationToken = default)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Length == 0 || snapshot.Length > SyncSnapshotCodec.MaximumSnapshotBytes)
                throw new ArgumentException("Invalid world snapshot size.", nameof(snapshot));
            var owned = (byte[])snapshot.Clone();
            return Operations.RunAsync(async context =>
            {
                await AwaitSnapshotReady(context);
                var candidate = Serializer.DeserializeSaveDataExact(owned);
                ValidateCandidate(candidate);
                context.CheckCancellation();
                context.BeginCommit();
                componentManager.BeginFullLoadGeneration();
                prefabManager.BeginFullLoadGeneration();
                StopActiveStateWatch();
                CurrentSaveData = candidate;
                componentManager.ImportHomeSceneSnapshots(candidate.HomeSceneComponentSnapshots, candidate.HomeScenePrefabAssetIDs);
                gameObjectTracker.ReplaceDestroyedIDs(candidate.DestroyedGameObjects);
                await Operations.RunCoroutine(PopulatePreparedDataCoroutine(candidate), context);
                foreach (string id in candidate.DestroyedGameObjects)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    var target = FindGameObjectByUniqueID(id, IdentifierType.UniqueID);
                    if (target) DestroyHelper.DestroyWithLogging(target, "Late-join snapshot tombstone");
                }
                if (!await FinishComponentRestorationAsync(context))
                    throw new AggregateException("Late-join snapshot could not restore all active components.", context.RestoreErrors);
                if (EnforceActiveState) ApplyGameObjectActiveStates(candidate.GameObjectStates);
                StartActiveStateWatch(candidate.GameObjectStates);
                Physics.SyncTransforms();
            }, cancellationToken);
        }

        async Task AwaitSnapshotReady(SaveOperationContext context)
        {
            // A network snapshot does not need cloud authentication or disk slot discovery.
            RequireAvailableOperationHost();
            while (!IsInitialized || SaveOperations == null)
            {
                context.CheckCancellation();
                RequireAvailableOperationHost();
                await Task.Yield();
            }
        }
    }

    internal sealed class SaveManagerWorldSnapshotProvider : ISyncWorldSnapshotProvider
    {
        readonly SaveManager manager;
        internal SaveManagerWorldSnapshotProvider(SaveManager manager) { this.manager = manager; }
        public Task<byte[]> CaptureAsync(CancellationToken token) => manager.CaptureWorldSnapshotAsync(token);
        public Task ApplyAsync(byte[] snapshot, CancellationToken token) => manager.ApplyWorldSnapshotAsync(snapshot, token);
    }
}
#endif
