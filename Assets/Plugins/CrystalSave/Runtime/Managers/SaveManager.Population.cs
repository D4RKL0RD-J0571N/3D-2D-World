#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    public partial class SaveManager
    {
        internal Task QueuePrefabPopulationAsync(int? slotNumber = null)
            => Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                await PopulatePrefabsCoreAsync(context, slotNumber);
            });

        // Internal composition calls this core directly while retaining its current
        // FIFO context. Public adapters always enqueue a separate request.
        internal Task RunSceneWorkflowAsync(Func<SaveOperationContext, Task> workflow)
            => Operations.RunAsync(async context =>
            {
                await AwaitOperationReady(context);
                await workflow(context);
            });

        internal Task SaveForSceneWorkflowAsync(SaveOperationContext context, int slotNumber)
            => SaveOperations.RunSaveAsync(context, slotNumber, null, null, false, false);

        internal async Task PopulatePrefabsCoreAsync(SaveOperationContext context,
            int? slotNumber = null, string lastActiveScene = null)
        {
            SaveData data;
            if (slotNumber.HasValue)
            {
                data = await LoadSaveDataForSlotCoreAsync(context, slotNumber.Value);
                if (data == null)
                    throw new InvalidOperationException($"No save data exists in slot {slotNumber.Value}.");
            }
            else
            {
                var previous = CurrentSaveData;
                try
                {
                    data = CollectSaveData(lastActiveScene);
                    ValidateCandidate(data);
                    context.CheckCancellation();
                }
                finally
                {
                    // Collection retains its legacy publishing behavior for its
                    // other callers; this prepared candidate is published below.
                    CurrentSaveData = previous;
                }
            }

            context.BeginCommit();
            if (slotNumber.HasValue)
                componentManager.ImportHomeSceneSnapshots(data.HomeSceneComponentSnapshots, data.HomeScenePrefabAssetIDs);
            else
                componentManager.SnapshotCurrentSceneAll();
            CurrentSaveData = data;
            gameObjectTracker.ReplaceDestroyedIDs(data.DestroyedGameObjects);

            // Population is additive: it must preserve unrelated live instances,
            // existing prefab queues and the current pending-data generation.
            await Operations.RunCoroutine(PopulatePreparedDataCoroutine(data), context);
            if (!await FinishComponentRestorationAsync(context))
                throw new AggregateException("Prefab population could not restore all required components.", context.RestoreErrors);
        }

        private IEnumerator PopulatePreparedDataCoroutine(SaveData data)
        {
            yield return prefabManager.InstantiatePrefabsCoroutine(data.Prefabs,
                gameObjectTracker.GetDestroyedGameObjectIDs(), clearExistingPrefabs: false);
            SaveableComponent.ResetLoadCallCounts();
            componentManager.ResetDeserializedComponents();
            componentManager.InstantiateMissingHomeSceneObjectsForLoadedScenes();
            int batchSize = saveSettings?.componentApplyBatchSize ?? 0;
            if (batchSize > 0)
                yield return componentManager.ApplyComponentDataCoroutine(data, batchSize);
            else
                componentManager.ApplyComponentData(data);
            if (data.GameObjectStates != null)
                ApplyGameObjectActiveStates(data.GameObjectStates);
        }
    }
}
#endif
