#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    internal sealed class ComponentRestoreEntry
    {
        internal string UniqueIdentifier { get; }
        internal byte[] Data { get; }
        internal int LoadPriority { get; }
        internal string HomeScene { get; }
        internal string GameObjectUniqueID { get; }
        internal string ComponentID { get; }

        internal ComponentRestoreEntry(string identifier, byte[] data, ComponentDataMetadata metadata)
        {
            UniqueIdentifier = identifier;
            Data = data;
            LoadPriority = Mathf.Clamp(metadata?.LoadPriority ?? 50, 0, 100);
            HomeScene = metadata?.HomeScene;
            GameObjectUniqueID = ExtractGameObjectID(identifier);
            ComponentID = ExtractComponentID(identifier);
        }

        internal static string ExtractGameObjectID(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return string.Empty;
            int separator = identifier.IndexOf('_');
            return separator < 0 ? identifier : identifier.Substring(0, separator);
        }

        internal static string ExtractComponentID(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return string.Empty;
            int separator = identifier.IndexOf('_');
            return separator < 0 || separator + 1 >= identifier.Length ? string.Empty : identifier.Substring(separator + 1);
        }
    }

    // Scene partitioning and priority ordering have no registration or application side effects.
    // Callers take a batch, retain its generation, and decide when to apply it.
    internal sealed class DeferredComponentQueue
    {
        const string GlobalSceneKey = "__GLOBAL__";
        readonly Dictionary<string, Queue<ComponentRestoreEntry>> scenes = new();
        internal bool HasEntries
        {
            get
            {
                foreach (var queue in scenes.Values)
                    if (queue.Count > 0)
                        return true;
                return false;
            }
        }

        internal IEnumerable<string> SceneKeys
        {
            get
            {
                foreach (var pair in scenes)
                    if (pair.Value.Count > 0)
                        yield return PublicSceneKey(pair.Key);
            }
        }

        internal void Clear() => scenes.Clear();
        internal IReadOnlyList<string> Peek(string scene)
        {
            if (!scenes.TryGetValue(SceneKey(scene), out var queue) || queue.Count == 0)
                return Array.Empty<string>();
            var identifiers = new List<string>(queue.Count);
            foreach (var entry in queue)
                if (entry != null)
                    identifiers.Add(entry.UniqueIdentifier);
            return identifiers.AsReadOnly();
        }

        internal Dictionary<string, ComponentRestoreEntry> SnapshotByIdentifier()
        {
            var snapshot = new Dictionary<string, ComponentRestoreEntry>(StringComparer.Ordinal);
            foreach (var queue in scenes.Values)
                foreach (var entry in queue)
                    if (entry != null && !string.IsNullOrEmpty(entry.UniqueIdentifier))
                        snapshot[entry.UniqueIdentifier] = entry;
            return snapshot;
        }

        internal IReadOnlyCollection<string> Enqueue(IEnumerable<ComponentRestoreEntry> entries)
        {
            var stagedScenes = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null)
                return Array.Empty<string>();
            var groups = new Dictionary<string, List<ComponentRestoreEntry>>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null)
                    continue;
                string key = SceneKey(entry.HomeScene);
                if (!groups.TryGetValue(key, out var group))
                    groups[key] = group = new List<ComponentRestoreEntry>();
                group.Add(entry);
            }

            foreach (var group in groups)
            {
                if (!scenes.TryGetValue(group.Key, out var queue))
                    scenes[group.Key] = queue = new Queue<ComponentRestoreEntry>();
                Sort(group.Value);
                foreach (var entry in group.Value)
                    queue.Enqueue(entry);
                stagedScenes.Add(PublicSceneKey(group.Key));
            }

            return new List<string>(stagedScenes);
        }

        internal List<ComponentRestoreEntry> TakeScene(string scene)
        {
            string key = SceneKey(scene);
            if (!scenes.TryGetValue(key, out var queue) || queue.Count == 0)
                return new List<ComponentRestoreEntry>();
            var entries = new List<ComponentRestoreEntry>(queue.Count);
            foreach (var entry in queue)
                if (entry != null)
                    entries.Add(entry);
            scenes.Remove(key);
            Sort(entries);
            return entries;
        }

        internal List<ComponentRestoreEntry> TakeAll()
        {
            var entries = new List<ComponentRestoreEntry>();
            foreach (var key in new List<string>(scenes.Keys))
                entries.AddRange(TakeScene(PublicSceneKey(key)));
            Sort(entries);
            return entries;
        }

        internal List<ComponentRestoreEntry> TakeMatching(Predicate<ComponentRestoreEntry> match)
        {
            var matches = new List<ComponentRestoreEntry>();
            if (match == null || scenes.Count == 0)
                return matches;
            foreach (var key in new List<string>(scenes.Keys))
            {
                var queue = scenes[key];
                var remaining = new Queue<ComponentRestoreEntry>();
                while (queue.Count > 0)
                {
                    var entry = queue.Dequeue();
                    if (entry == null)
                        continue;
                    if (match(entry))
                        matches.Add(entry);
                    else
                        remaining.Enqueue(entry);
                }

                if (remaining.Count > 0)
                    scenes[key] = remaining;
                else
                    scenes.Remove(key);
            }

            Sort(matches);
            return matches;
        }

        internal static void Partition(SaveData data, out List<ComponentRestoreEntry> immediate, out List<ComponentRestoreEntry> deferred)
        {
            immediate = new List<ComponentRestoreEntry>();
            deferred = new List<ComponentRestoreEntry>();
            if (data?.ComponentsData == null)
                return;
            foreach (var pair in data.ComponentsData)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    continue;
                ComponentDataMetadata metadata = null;
                data.ComponentMetadata?.TryGetValue(pair.Key, out metadata);
                var entry = new ComponentRestoreEntry(pair.Key, pair.Value, metadata);
                if (metadata != null && metadata.DeferLowPriorityUntilRequested)
                    deferred.Add(entry);
                else
                    immediate.Add(entry);
            }

            Sort(immediate);
            Sort(deferred);
        }

        static void Sort(List<ComponentRestoreEntry> entries) => entries.Sort((left, right) => right.LoadPriority.CompareTo(left.LoadPriority));
        static string SceneKey(string scene) => string.IsNullOrEmpty(scene) ? GlobalSceneKey : scene;
        static string PublicSceneKey(string key) => key == GlobalSceneKey ? string.Empty : key;
    }
}
#endif

