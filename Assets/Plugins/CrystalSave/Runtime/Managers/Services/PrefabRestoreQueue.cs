#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;

namespace Arawn.CrystalSave.Runtime
{
    // Owns scene-pending and explicit-streaming data only. PrefabManager retains Unity
    // callbacks, generation boundaries, instantiation and notifications to streamers.
    internal sealed class PrefabRestoreQueue
    {
        private const string GlobalDeferredSceneKey = "__GLOBAL__";
        private readonly Dictionary<string, List<SaveablePrefabData>> pendingPrefabs = new();
        private readonly Dictionary<string, Queue<SaveablePrefabData>> deferredPrefabs = new();
        private readonly HashSet<string> pendingScenesWaitingForActivation = new(StringComparer.Ordinal);
        private static readonly ProfilerMarker PrepareBatchMarker =
            new ProfilerMarker("CrystalSave.PrefabLoad.PrepareBatch");

        internal bool HasPending => pendingPrefabs.Count != 0;
        internal bool DeferredStagedSinceClear { get; private set; }
        internal string[] SnapshotPendingScenes() => pendingPrefabs.Keys.ToArray();

        internal void Clear(bool preserveDeferred = false)
        {
            pendingPrefabs.Clear();
            pendingScenesWaitingForActivation.Clear();
            if (!preserveDeferred)
                deferredPrefabs.Clear();
            DeferredStagedSinceClear = false;
        }

        internal void QueuePendingRange(string sceneName, IEnumerable<SaveablePrefabData> entries)
        {
            if (!pendingPrefabs.TryGetValue(sceneName, out var list))
                pendingPrefabs[sceneName] = list = new List<SaveablePrefabData>();
            list.AddRange(entries);
        }

        internal void QueuePending(string sceneName, SaveablePrefabData entry)
        {
            if (!pendingPrefabs.TryGetValue(sceneName, out var list))
                pendingPrefabs[sceneName] = list = new List<SaveablePrefabData>();
            list.Add(entry);
        }

        internal IReadOnlyList<SaveablePrefabData> PeekPendingScene(string sceneName)
            => pendingPrefabs.TryGetValue(sceneName, out var list)
                ? list.AsReadOnly() : Array.Empty<SaveablePrefabData>();

        internal void RemovePendingScene(string sceneName) => pendingPrefabs.Remove(sceneName);
        internal bool MarkWaitingForActivation(string sceneName) => pendingScenesWaitingForActivation.Add(sceneName);
        internal void ReleaseActivationWait(string sceneName) => pendingScenesWaitingForActivation.Remove(sceneName);

        internal void AppendCaptureData(List<SaveablePrefabData> destination,
            Action<string, string, SaveablePrefabData> describe = null)
        {
            foreach (var pair in pendingPrefabs)
            {
                if (describe != null)
                    foreach (var data in pair.Value)
                        describe("pending", pair.Key, data);
                destination.AddRange(pair.Value);
            }
            foreach (var pair in deferredPrefabs)
            {
                if (describe != null)
                    foreach (var data in pair.Value)
                        describe("deferred", pair.Key, data);
                destination.AddRange(pair.Value);
            }
        }

        internal bool HasDeferred
        {
            get
            {
                foreach (var kvp in deferredPrefabs)
                {
                    if (kvp.Value.Count > 0)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Enumerates scene keys that contain deferred prefabs waiting to be processed.
        /// </summary>
        internal IEnumerable<string> DeferredSceneKeys()
        {
            foreach (var kvp in deferredPrefabs)
            {
                if (kvp.Value.Count == 0)
                    continue;

                yield return kvp.Key == GlobalDeferredSceneKey ? string.Empty : kvp.Key;
            }
        }

        /// <summary>
        /// Enumerates loaded-scene pending prefab keys waiting for their scene to load or activate.
        /// </summary>
        internal IEnumerable<string> PendingSceneKeys()
        {
            foreach (var kvp in pendingPrefabs)
            {
                if (kvp.Value == null || kvp.Value.Count == 0)
                    continue;

                yield return kvp.Key;
            }
        }

        /// <summary>
        /// Returns a snapshot of the deferred prefabs queued for a specific scene without dequeuing them.
        /// </summary>
        internal IReadOnlyList<SaveablePrefabData> PeekDeferredScene(string sceneName)
        {
            string key = ResolveDeferredSceneKey(sceneName);
            if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                return Array.Empty<SaveablePrefabData>();

            var result = new List<SaveablePrefabData>(queue.Count);
            foreach (var entry in queue)
            {
                if (entry != null)
                    result.Add(entry);
            }

            return result.AsReadOnly();
        }

        internal static PrefabManager.PreparedPrefabBatch PrepareBatch(
            IEnumerable<SaveablePrefabData> source,
            bool partitionDeferred)
        {
            using (PrepareBatchMarker.Auto())
            {
                PrefabLoadDiagnosticCounters.RecordBatchPreparation();
                var normalized = new List<SaveablePrefabData>();
                var instanceIndex = new Dictionary<string, int>(StringComparer.Ordinal);

                if (source != null)
                {
                    foreach (SaveablePrefabData data in source)
                    {
                        if (data == null)
                            continue;

                        string instanceId = data.InstanceID;
                        if (string.IsNullOrEmpty(instanceId))
                        {
                            normalized.Add(data);
                            continue;
                        }

                        if (!instanceIndex.TryGetValue(instanceId, out int existingIndex))
                        {
                            instanceIndex.Add(instanceId, normalized.Count);
                            normalized.Add(data);
                            continue;
                        }

                        SaveablePrefabData current = normalized[existingIndex];
                        if (ShouldPreferLoadEntry(data, current))
                            normalized[existingIndex] = data;
                    }
                }

                normalized.Sort((left, right) => right.LoadPriority.CompareTo(left.LoadPriority));

                if (!partitionDeferred)
                    return new PrefabManager.PreparedPrefabBatch(normalized, new List<SaveablePrefabData>());

                var immediate = new List<SaveablePrefabData>(normalized.Count);
                var deferred = new List<SaveablePrefabData>();
                for (int i = 0; i < normalized.Count; i++)
                {
                    SaveablePrefabData data = normalized[i];
                    if (data.DeferLowPriorityUntilRequested)
                        deferred.Add(data);
                    else
                        immediate.Add(data);
                }

                return new PrefabManager.PreparedPrefabBatch(immediate, deferred);
            }
        }

        private static bool ShouldPreferLoadEntry(
            SaveablePrefabData candidate,
            SaveablePrefabData current)
        {
            if (candidate == null)
                return false;
            if (current == null)
                return true;

            if (candidate.ReuseSceneInstanceOnLoad != current.ReuseSceneInstanceOnLoad)
                return candidate.ReuseSceneInstanceOnLoad;
            if (candidate.HasTransformOverride != current.HasTransformOverride)
                return candidate.HasTransformOverride;
            return candidate.LoadPriority > current.LoadPriority;
        }

        internal IReadOnlyList<SaveablePrefabData> QueueDeferred(IEnumerable<SaveablePrefabData> deferredEntries)
        {
            if (deferredEntries == null)
                return Array.Empty<SaveablePrefabData>();

            List<SaveablePrefabData> snapshot = null;
            var grouped = new Dictionary<string, List<SaveablePrefabData>>(StringComparer.Ordinal);

            foreach (var entry in deferredEntries)
            {
                if (entry == null)
                    continue;

                string key = GetDeferredSceneKey(entry);
                if (!grouped.TryGetValue(key, out var group))
                {
                    group = new List<SaveablePrefabData>();
                    grouped[key] = group;
                }

                group.Add(entry);
            }

            foreach (var group in grouped)
            {
                if (!deferredPrefabs.TryGetValue(group.Key, out var queue))
                {
                    queue = new Queue<SaveablePrefabData>();
                    deferredPrefabs[group.Key] = queue;
                }

                for (int i = 0; i < group.Value.Count; i++)
                {
                    SaveablePrefabData entry = group.Value[i];
                    snapshot ??= new List<SaveablePrefabData>();
                    snapshot.Add(entry);
                    queue.Enqueue(entry);
                }
            }

            if (snapshot != null && snapshot.Count > 0)
                DeferredStagedSinceClear = true;

            return snapshot == null ? Array.Empty<SaveablePrefabData>() : snapshot.AsReadOnly();
        }

        private static string GetDeferredSceneKey(SaveablePrefabData data)
        {
            if (data == null)
                return GlobalDeferredSceneKey;

            return string.IsNullOrEmpty(data.HomeScene) ? GlobalDeferredSceneKey : data.HomeScene;
        }

        private static string ResolveDeferredSceneKey(string sceneName)
        {
            return string.IsNullOrEmpty(sceneName) ? GlobalDeferredSceneKey : sceneName;
        }

        internal List<SaveablePrefabData> TakeDeferredScene(string sceneName)
        {
            string key = ResolveDeferredSceneKey(sceneName);
            if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                return new List<SaveablePrefabData>();

            var list = new List<SaveablePrefabData>(queue.Count);
            foreach (var entry in queue)
            {
                if (entry != null)
                    list.Add(entry);
            }
            deferredPrefabs.Remove(key);
            return list;
        }

        internal List<SaveablePrefabData> TakeAllDeferred()
        {
            var list = new List<SaveablePrefabData>();

            foreach (var key in new List<string>(deferredPrefabs.Keys))
            {
                list.AddRange(TakeDeferredScene(key == GlobalDeferredSceneKey ? string.Empty : key));
            }

            return list;
        }

        internal List<SaveablePrefabData> TakeDeferredAsset(string prefabAssetID)
        {
            var matches = new List<SaveablePrefabData>();

            if (string.IsNullOrEmpty(prefabAssetID))
                return matches;

            foreach (var key in new List<string>(deferredPrefabs.Keys))
            {
                if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                    continue;

                var remaining = new Queue<SaveablePrefabData>();
                int count = queue.Count;

                for (int i = 0; i < count; i++)
                {
                    var entry = queue.Dequeue();

                    if (entry != null && string.Equals(entry.PrefabID, prefabAssetID, StringComparison.Ordinal))
                        matches.Add(entry);
                    else
                        remaining.Enqueue(entry);
                }

                if (remaining.Count > 0)
                    deferredPrefabs[key] = remaining;
                else
                    deferredPrefabs.Remove(key);
            }

            return matches;
        }

        internal List<SaveablePrefabData> TakeDeferredInstance(string instanceID)
        {
            if (string.IsNullOrEmpty(instanceID))
                return new List<SaveablePrefabData>();

            return TakeDeferredInstances(new[] { instanceID });
        }

        internal List<SaveablePrefabData> TakeDeferredInstances(IReadOnlyCollection<string> instanceIDs)
        {
            var matches = new List<SaveablePrefabData>();

            if (instanceIDs == null || instanceIDs.Count == 0)
                return matches;

            var lookup = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in instanceIDs)
            {
                if (!string.IsNullOrEmpty(id))
                    lookup.Add(id);
            }

            if (lookup.Count == 0)
                return matches;

            foreach (var key in new List<string>(deferredPrefabs.Keys))
            {
                if (!deferredPrefabs.TryGetValue(key, out var queue) || queue.Count == 0)
                    continue;

                var extracted = ExtractMatchingDeferredPrefabs(queue, lookup, out var remaining);
                if (extracted.Count > 0)
                    matches.AddRange(extracted);

                if (remaining.Count > 0)
                    deferredPrefabs[key] = remaining;
                else
                    deferredPrefabs.Remove(key);
            }

            return matches;
        }

        private static List<SaveablePrefabData> ExtractMatchingDeferredPrefabs(Queue<SaveablePrefabData> source, ISet<string> instanceIDLookup, out Queue<SaveablePrefabData> remaining)
        {
            remaining = new Queue<SaveablePrefabData>();
            var matches = new List<SaveablePrefabData>();

            if (source == null || source.Count == 0 || instanceIDLookup == null || instanceIDLookup.Count == 0)
                return matches;

            int count = source.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = source.Dequeue();
                if (entry != null && instanceIDLookup.Contains(entry.InstanceID))
                    matches.Add(entry);
                else
                    remaining.Enqueue(entry);
            }

            return matches;
        }

    }
}
#endif
