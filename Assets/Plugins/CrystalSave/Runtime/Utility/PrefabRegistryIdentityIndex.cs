#if MEMORYPACK && ARAWN_REMEMBERME && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Immutable row information captured while building a PrefabRegistry identity index.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PrefabRegistryIdentityRow
    {
        public int Index { get; internal set; }
        public PrefabRegistry.PrefabEntry Entry { get; internal set; }
        public GameObject PrefabReference { get; internal set; }
        public GameObject ResolvedAsset { get; internal set; }
        public SaveablePrefab SaveablePrefab { get; internal set; }
        public string RegistryId { get; internal set; }
        public string PrefabAssetId { get; internal set; }
        public string AssetPath { get; internal set; }
        public string AssetGuid { get; internal set; }
        public string AssetKey { get; internal set; }
        public bool IsCanonicalReference { get; internal set; }
        public bool IsPersistentPrefabAsset { get; internal set; }
        public bool IsVariant { get; internal set; }
        public GameObject VariantSourceAsset { get; internal set; }
        public string VariantSourcePrefabAssetId { get; internal set; }
        public bool VariantInheritsSourceId { get; internal set; }
    }

    /// <summary>
    /// Counters from a single index build. They are intended for editor diagnostics and tests.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PrefabRegistryIdentityBuildMetrics
    {
        public int RowCount { get; internal set; }
        public int UniqueReferenceCount { get; internal set; }
        public int AssetResolutionCount { get; internal set; }
        public int UniqueAssetCount { get; internal set; }
        public int InvalidRowCount { get; internal set; }
        public long ElapsedTicks { get; internal set; }
        public double ElapsedMilliseconds { get; internal set; }
    }

    /// <summary>
    /// Indexed editor-only view of prefab registry identity and ownership data.
    /// </summary>
    [InitializeOnLoad]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PrefabRegistryIdentityIndex
    {
        private static readonly ProfilerMarker BuildMarker =
            new ProfilerMarker("CrystalSave.PrefabRegistryIdentityIndex.Build");

        private static readonly Dictionary<string, CacheEntry> Cache =
            new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private static double automaticSaveSuppressionUntil;
        private static PrefabRegistry automaticSaveSuppressionRegistry;

        private static readonly IReadOnlyList<PrefabRegistryIdentityRow> EmptyRows =
            Array.Empty<PrefabRegistryIdentityRow>();

        private readonly List<PrefabRegistryIdentityRow> rows =
            new List<PrefabRegistryIdentityRow>();
        private readonly Dictionary<string, List<PrefabRegistryIdentityRow>> rowsByAssetKey =
            new Dictionary<string, List<PrefabRegistryIdentityRow>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<PrefabRegistryIdentityRow>> rowsById =
            new Dictionary<string, List<PrefabRegistryIdentityRow>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<PrefabRegistryIdentityRow>> rowsByPrefabReferenceKey =
            new Dictionary<string, List<PrefabRegistryIdentityRow>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PrefabRegistryIdentityRow> firstOwnerById =
            new Dictionary<string, PrefabRegistryIdentityRow>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> reservedOwnerKeyById =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> queryAssetKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> objectAssetKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> prefabAssetIdsByAssetKey =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> knownAssetKeys =
            new HashSet<string>(StringComparer.Ordinal);

        private PrefabRegistryIdentityIndex(PrefabRegistry registry)
        {
            Registry = registry;
        }

        static PrefabRegistryIdentityIndex()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= InvalidateAll;
            AssemblyReloadEvents.beforeAssemblyReload += InvalidateAll;
            Undo.undoRedoPerformed -= InvalidateAll;
            Undo.undoRedoPerformed += InvalidateAll;
            EditorApplication.projectChanged -= InvalidateAll;
            EditorApplication.projectChanged += InvalidateAll;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public PrefabRegistry Registry { get; }
        public IReadOnlyList<PrefabRegistryIdentityRow> Rows => rows;
        public PrefabRegistryIdentityBuildMetrics Metrics { get; private set; } =
            new PrefabRegistryIdentityBuildMetrics();

        /// <summary>
        /// Returns a cached index while the registry revision and inexpensive structural stamp match.
        /// </summary>
        public static PrefabRegistryIdentityIndex GetOrCreate(PrefabRegistry registry)
        {
            if (registry == null)
                return Build(null);

            string registryKey = GetUnityObjectCacheKey(registry);
            StructuralStamp stamp = StructuralStamp.Create(registry);
            if (Cache.TryGetValue(registryKey, out CacheEntry cached) &&
                cached.Registry == registry &&
                cached.Stamp.Equals(stamp))
            {
                return cached.Index;
            }

            PrefabRegistryIdentityIndex index = BuildInternal(registry);
            Cache[registryKey] = new CacheEntry(registry, stamp, index);
            return index;
        }

        /// <summary>
        /// Builds a fresh index without consulting the cache.
        /// </summary>
        public static PrefabRegistryIdentityIndex Build(PrefabRegistry registry)
        {
            return BuildInternal(registry);
        }

        /// <summary>
        /// Rebuilds this instance in place after a controlled batch mutation.
        /// </summary>
        public void Refresh()
        {
            RebuildInternal();
            if (Registry != null)
            {
                Cache[GetUnityObjectCacheKey(Registry)] = new CacheEntry(
                    Registry,
                    StructuralStamp.Create(Registry),
                    this);
            }
        }

        /// <summary>
        /// Invalidates the cached view for one registry.
        /// </summary>
        public static void Invalidate(PrefabRegistry registry)
        {
            if (registry != null)
                Cache.Remove(GetUnityObjectCacheKey(registry));
        }

        /// <summary>
        /// Invalidates every cached registry view.
        /// </summary>
        public static void InvalidateAll()
        {
            Cache.Clear();
        }

        /// <summary>
        /// Notification for direct PrefabRegistry list/entry mutations.
        /// </summary>
        public static void NotifyRegistryMutated(PrefabRegistry registry)
        {
            Invalidate(registry);
        }

        /// <summary>
        /// Marks an automatic SaveAssets call so the editor validation coordinator can
        /// ignore its projectChanged feedback event.
        /// </summary>
        public static void NotifyAutomaticSaveRequested()
        {
            automaticSaveSuppressionUntil = EditorApplication.timeSinceStartup + 2d;
            automaticSaveSuppressionRegistry = null;
        }

        /// <summary>
        /// Marks an automatic SaveAssets call and attributes it to a registry when an
        /// editor performance diagnostic session is active for that registry.
        /// </summary>
        public static void NotifyAutomaticSaveRequested(PrefabRegistry registry)
        {
            automaticSaveSuppressionUntil = EditorApplication.timeSinceStartup + 2d;
            automaticSaveSuppressionRegistry = registry;
            PrefabRegistryEditorDiagnosticCounters.RecordAutomaticSaveRequest(registry);
        }

        public static bool ConsumeAutomaticSaveSuppression()
        {
            bool suppress = automaticSaveSuppressionUntil > 0d &&
                            EditorApplication.timeSinceStartup <= automaticSaveSuppressionUntil;
            automaticSaveSuppressionUntil = 0d;
            automaticSaveSuppressionRegistry = null;
            return suppress;
        }

        /// <summary>Clears a pending feedback-loop token only when it belongs to the target registry.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ClearAutomaticSaveSuppressionForEditorDiagnostics(PrefabRegistry registry)
        {
            if (registry == null || !ReferenceEquals(automaticSaveSuppressionRegistry, registry))
                return;

            automaticSaveSuppressionUntil = 0d;
            automaticSaveSuppressionRegistry = null;
        }

        /// <summary>
        /// Notification for a SaveablePrefab identity mutation. Only indexes that know the
        /// asset (including variant source assets) are invalidated.
        /// </summary>
        public static void NotifyPrefabIdentityChanged(GameObject prefabAsset)
        {
            if (prefabAsset == null || Cache.Count == 0)
                return;

            string directKey = GetDirectObjectKey(prefabAsset);
            var keysToRemove = new List<string>();
            foreach (KeyValuePair<string, CacheEntry> pair in Cache)
            {
                if (pair.Value.Index.ContainsKnownAsset(prefabAsset, directKey))
                    keysToRemove.Add(pair.Key);
            }

            foreach (string key in keysToRemove)
                Cache.Remove(key);
        }

        public static void NotifyPrefabIdentityChanged(SaveablePrefab saveablePrefab)
        {
            if (saveablePrefab != null)
                NotifyPrefabIdentityChanged(saveablePrefab.gameObject);
        }

        /// <summary>
        /// Reserves a newly assigned ID during a delayed batch so subsequent decisions see
        /// the first assignment before the registry index is rebuilt.
        /// </summary>
        public void ReserveIdOwner(string id, GameObject prefabAsset)
        {
            if (string.IsNullOrEmpty(id) || prefabAsset == null || firstOwnerById.ContainsKey(id))
                return;

            string assetKey = GetAssetKey(prefabAsset);
            if (string.IsNullOrEmpty(assetKey) || reservedOwnerKeyById.ContainsKey(id))
                return;

            reservedOwnerKeyById.Add(id, assetKey);
        }

        public void ClearReservedIdOwners()
        {
            reservedOwnerKeyById.Clear();
        }

        /// <summary>
        /// Applies an added or edited registry entry to this live index. This is intended for
        /// delayed mutation batches that need subsequent identity decisions to observe earlier
        /// changes without rebuilding or resolving the whole registry.
        /// </summary>
        public PrefabRegistryIdentityRow SynchronizeMutation(
            PrefabRegistry.PrefabEntry entry,
            GameObject resolvedAsset = null)
        {
            if (entry == null)
                return null;

            int existingPosition = FindRowPosition(entry);
            PrefabRegistryIdentityRow previousRow =
                existingPosition >= 0 ? rows[existingPosition] : null;
            string previousId = previousRow != null ? previousRow.RegistryId : null;
            if (previousRow != null)
                RemoveRowFromMaps(previousRow);

            var resolutions = new Dictionary<string, ResolutionRecord>(StringComparer.Ordinal);
            if (entry.prefab != null && resolvedAsset != null)
            {
                resolutions[GetDirectObjectKey(entry.prefab)] = new ResolutionRecord
                {
                    Asset = resolvedAsset,
                    AssetKey = GetDirectObjectKey(resolvedAsset)
                };
            }

            var assetInfoByKey = new Dictionary<string, AssetInfo>(StringComparer.Ordinal);
            var ignoredMetrics = new PrefabRegistryIdentityBuildMetrics();
            int registryPosition = Registry != null && Registry.prefabEntries != null
                ? Registry.prefabEntries.IndexOf(entry)
                : -1;
            PrefabRegistryIdentityRow updatedRow = BuildRow(
                entry,
                registryPosition >= 0 ? registryPosition : rows.Count,
                resolutions,
                assetInfoByKey,
                ignoredMetrics);

            if (existingPosition >= 0)
                rows[existingPosition] = updatedRow;
            else if (registryPosition >= 0 && registryPosition <= rows.Count)
                rows.Insert(registryPosition, updatedRow);
            else
                rows.Add(updatedRow);

            ReindexRows();
            AddRowToMaps(updatedRow);
            RebuildFirstOwner(previousId);
            RebuildFirstOwner(updatedRow.RegistryId);
            if (!string.IsNullOrEmpty(updatedRow.RegistryId))
                reservedOwnerKeyById.Remove(updatedRow.RegistryId);
            return updatedRow;
        }

        /// <summary>
        /// Removes a deleted registry entry from this live index.
        /// </summary>
        public bool RemoveRegistryRow(PrefabRegistry.PrefabEntry entry)
        {
            int position = FindRowPosition(entry);
            if (position < 0)
                return false;

            PrefabRegistryIdentityRow row = rows[position];
            string previousId = row.RegistryId;
            RemoveRowFromMaps(row);
            rows.RemoveAt(position);
            ReindexRows();
            RebuildFirstOwner(previousId);
            return true;
        }

        /// <summary>
        /// Updates component-ID snapshots in this live index after EnsurePrefabAssetIDForAsset
        /// mutates a prefab. Variant rows that reference the asset as their source are updated too.
        /// </summary>
        public void ApplyPrefabIdentityMutation(GameObject prefabAsset, string newId)
        {
            if (prefabAsset == null)
                return;

            string assetKey = GetAssetKey(prefabAsset);
            if (!string.IsNullOrEmpty(assetKey))
                prefabAssetIdsByAssetKey[assetKey] = newId;
            for (int i = 0; i < rows.Count; i++)
            {
                PrefabRegistryIdentityRow row = rows[i];
                if (!string.IsNullOrEmpty(assetKey) &&
                    string.Equals(row.AssetKey, assetKey, StringComparison.Ordinal))
                {
                    row.PrefabAssetId = newId;
                    row.VariantInheritsSourceId =
                        row.IsVariant &&
                        !string.IsNullOrEmpty(newId) &&
                        string.Equals(newId, row.VariantSourcePrefabAssetId, StringComparison.Ordinal);
                }

                if (row.VariantSourceAsset != null && AreSameAsset(row.VariantSourceAsset, prefabAsset))
                {
                    row.VariantSourcePrefabAssetId = newId;
                    row.VariantInheritsSourceId =
                        row.IsVariant &&
                        !string.IsNullOrEmpty(row.PrefabAssetId) &&
                        string.Equals(row.PrefabAssetId, newId, StringComparison.Ordinal);
                }
            }
        }

        /// <summary>
        /// Reports whether a known prefab or variant-source component ID differs from the
        /// value captured by this index. Unknown, unregistered assets return false.
        /// </summary>
        public bool HasPrefabIdentitySnapshotChanged(GameObject prefabAsset, string currentId)
        {
            string assetKey = GetAssetKey(prefabAsset);
            return !string.IsNullOrEmpty(assetKey) &&
                   prefabAssetIdsByAssetKey.TryGetValue(assetKey, out string indexedId) &&
                   !string.Equals(indexedId, currentId, StringComparison.Ordinal);
        }

        public IReadOnlyList<PrefabRegistryIdentityRow> GetRowsForAsset(GameObject prefabAsset)
        {
            string key = GetAssetKey(prefabAsset);
            return !string.IsNullOrEmpty(key) && rowsByAssetKey.TryGetValue(key, out var result)
                ? result
                : EmptyRows;
        }

        public IReadOnlyList<PrefabRegistryIdentityRow> GetRowsForPrefabReference(GameObject prefab)
        {
            string key = GetDirectObjectKey(prefab);
            return !string.IsNullOrEmpty(key) && rowsByPrefabReferenceKey.TryGetValue(key, out var result)
                ? result
                : EmptyRows;
        }

        public IReadOnlyList<PrefabRegistryIdentityRow> GetRowsForId(string id)
        {
            return !string.IsNullOrEmpty(id) && rowsById.TryGetValue(id, out var result)
                ? result
                : EmptyRows;
        }

        /// <summary>
        /// Returns the sole non-empty registry ID for an asset. Empty duplicate rows do not
        /// create a conflict and therefore adopt this value during normalization.
        /// </summary>
        public bool TryGetAuthoritativeId(GameObject prefabAsset, out string authoritativeId)
        {
            authoritativeId = null;
            IReadOnlyList<PrefabRegistryIdentityRow> assetRows = GetRowsForAsset(prefabAsset);
            for (int i = 0; i < assetRows.Count; i++)
            {
                string candidate = assetRows[i].RegistryId;
                if (string.IsNullOrEmpty(candidate))
                    continue;

                if (authoritativeId == null)
                {
                    authoritativeId = candidate;
                    continue;
                }

                if (!string.Equals(authoritativeId, candidate, StringComparison.Ordinal))
                {
                    authoritativeId = null;
                    return false;
                }
            }

            return !string.IsNullOrEmpty(authoritativeId);
        }

        public bool HasConflictingIdsForAsset(GameObject prefabAsset)
        {
            IReadOnlyList<PrefabRegistryIdentityRow> assetRows = GetRowsForAsset(prefabAsset);
            string firstId = null;
            for (int i = 0; i < assetRows.Count; i++)
            {
                string id = assetRows[i].RegistryId;
                if (string.IsNullOrEmpty(id))
                    continue;

                if (firstId == null)
                {
                    firstId = id;
                    continue;
                }

                if (!string.Equals(firstId, id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the first registry row with this ID and a prefab reference.
        /// </summary>
        public bool TryGetIdOwner(string id, out PrefabRegistryIdentityRow owner)
        {
            if (string.IsNullOrEmpty(id))
            {
                owner = null;
                return false;
            }

            return firstOwnerById.TryGetValue(id, out owner);
        }

        public bool TryGetIdOwnerAsset(string id, out GameObject ownerAsset)
        {
            ownerAsset = null;
            if (TryGetIdOwner(id, out PrefabRegistryIdentityRow row))
            {
                ownerAsset = row.ResolvedAsset;
                return ownerAsset != null;
            }

            return false;
        }

        public bool IsIdOwnedByDifferentAsset(string id, GameObject prefabAsset)
        {
            if (string.IsNullOrEmpty(id) || prefabAsset == null)
                return false;

            string targetKey = GetAssetKey(prefabAsset);
            if (string.IsNullOrEmpty(targetKey))
                return false;

            if (firstOwnerById.TryGetValue(id, out PrefabRegistryIdentityRow owner))
            {
                return !string.IsNullOrEmpty(owner.AssetKey) &&
                       !string.Equals(owner.AssetKey, targetKey, StringComparison.Ordinal);
            }

            return reservedOwnerKeyById.TryGetValue(id, out string reservedOwnerKey) &&
                   !string.Equals(reservedOwnerKey, targetKey, StringComparison.Ordinal);
        }

        public bool AreSameAsset(GameObject left, GameObject right)
        {
            if (left == null || right == null)
                return false;
            if (left == right)
                return true;

            string leftKey = GetAssetKey(left);
            string rightKey = GetAssetKey(right);
            return !string.IsNullOrEmpty(leftKey) &&
                   string.Equals(leftKey, rightKey, StringComparison.Ordinal);
        }

        public bool IsVariantSourceId(GameObject prefabAsset, string id)
        {
            if (prefabAsset == null || string.IsNullOrEmpty(id))
                return false;

            string assetKey = GetAssetKey(prefabAsset);
            if (string.IsNullOrEmpty(assetKey) ||
                !rowsByAssetKey.TryGetValue(assetKey, out List<PrefabRegistryIdentityRow> assetRows))
            {
                return IsVariantSourceIdUncached(prefabAsset, id);
            }

            for (int i = 0; i < assetRows.Count; i++)
            {
                PrefabRegistryIdentityRow row = assetRows[i];
                if (row.IsVariant &&
                    string.Equals(row.VariantSourcePrefabAssetId, id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the GUID/path based identity key used by the index. Registered assets are
        /// constant-time after index construction; unregistered instances are resolved once.
        /// </summary>
        public string GetAssetKey(GameObject prefabAsset)
        {
            if (prefabAsset == null)
                return null;

            string objectKey = GetUnityObjectCacheKey(prefabAsset);
            if (!string.IsNullOrEmpty(objectKey) &&
                objectAssetKeys.TryGetValue(objectKey, out string objectAssetKey))
                return objectAssetKey;

            string directKey = GetDirectObjectKey(prefabAsset);
            if (string.IsNullOrEmpty(directKey))
                return null;

            if (queryAssetKeys.TryGetValue(directKey, out string cachedKey))
                return cachedKey;

            if (knownAssetKeys.Contains(directKey))
            {
                queryAssetKeys[directKey] = directKey;
                return directKey;
            }

            GameObject resolved = PrefabAssetResolver.ResolveNearestPrefabAsset(prefabAsset) ?? prefabAsset;
            string resolvedKey = GetDirectObjectKey(resolved);
            queryAssetKeys[directKey] = resolvedKey;
            if (!string.IsNullOrEmpty(objectKey))
                objectAssetKeys[objectKey] = resolvedKey;
            if (!string.IsNullOrEmpty(resolvedKey))
            {
                queryAssetKeys[resolvedKey] = resolvedKey;
                string resolvedObjectKey = GetUnityObjectCacheKey(resolved);
                if (!string.IsNullOrEmpty(resolvedObjectKey))
                    objectAssetKeys[resolvedObjectKey] = resolvedKey;
            }
            return resolvedKey;
        }

        private static PrefabRegistryIdentityIndex BuildInternal(PrefabRegistry registry)
        {
            var index = new PrefabRegistryIdentityIndex(registry);
            index.RebuildInternal();
            return index;
        }

        private void RebuildInternal()
        {
            using (BuildMarker.Auto())
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                rows.Clear();
                rowsByAssetKey.Clear();
                rowsById.Clear();
                rowsByPrefabReferenceKey.Clear();
                firstOwnerById.Clear();
                reservedOwnerKeyById.Clear();
                queryAssetKeys.Clear();
                objectAssetKeys.Clear();
                prefabAssetIdsByAssetKey.Clear();
                knownAssetKeys.Clear();

                var metrics = new PrefabRegistryIdentityBuildMetrics();
                var resolutions = new Dictionary<string, ResolutionRecord>(StringComparer.Ordinal);
                var assetInfoByKey = new Dictionary<string, AssetInfo>(StringComparer.Ordinal);

                if (Registry != null && Registry.prefabEntries != null)
                {
                    metrics.RowCount = Registry.prefabEntries.Count;
                    for (int i = 0; i < Registry.prefabEntries.Count; i++)
                    {
                        PrefabRegistry.PrefabEntry entry = Registry.prefabEntries[i];
                        PrefabRegistryIdentityRow row = BuildRow(
                            entry,
                            i,
                            resolutions,
                            assetInfoByKey,
                            metrics);
                        rows.Add(row);

                        if (!row.IsPersistentPrefabAsset || row.SaveablePrefab == null)
                            metrics.InvalidRowCount++;

                        AddRowToMaps(row);

                        if (!string.IsNullOrEmpty(row.RegistryId) &&
                            row.ResolvedAsset != null &&
                            !firstOwnerById.ContainsKey(row.RegistryId))
                        {
                            firstOwnerById.Add(row.RegistryId, row);
                        }
                    }
                }

                metrics.UniqueReferenceCount = resolutions.Count;
                metrics.UniqueAssetCount = assetInfoByKey.Count;
                stopwatch.Stop();
                metrics.ElapsedTicks = stopwatch.ElapsedTicks;
                metrics.ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                Metrics = metrics;
                PrefabRegistryEditorDiagnosticCounters.RecordIndexBuild(Registry, metrics);
            }
        }

        private PrefabRegistryIdentityRow BuildRow(
            PrefabRegistry.PrefabEntry entry,
            int index,
            Dictionary<string, ResolutionRecord> resolutions,
            Dictionary<string, AssetInfo> assetInfoByKey,
            PrefabRegistryIdentityBuildMetrics metrics)
        {
            var row = new PrefabRegistryIdentityRow
            {
                Index = index,
                Entry = entry,
                PrefabReference = entry != null ? entry.prefab : null,
                RegistryId = entry != null ? entry.uniqueID : null
            };

            if (row.PrefabReference == null)
                return row;

            ResolutionRecord resolution = ResolveOnce(row.PrefabReference, resolutions, metrics);
            row.ResolvedAsset = resolution.Asset;
            row.AssetKey = resolution.AssetKey;
            row.IsCanonicalReference = row.ResolvedAsset != null && row.PrefabReference == row.ResolvedAsset;

            if (string.IsNullOrEmpty(row.AssetKey))
                return row;

            knownAssetKeys.Add(row.AssetKey);
            queryAssetKeys[GetDirectObjectKey(row.PrefabReference)] = row.AssetKey;
            queryAssetKeys[GetDirectObjectKey(row.ResolvedAsset)] = row.AssetKey;
            objectAssetKeys[GetUnityObjectCacheKey(row.PrefabReference)] = row.AssetKey;
            objectAssetKeys[GetUnityObjectCacheKey(row.ResolvedAsset)] = row.AssetKey;

            if (!assetInfoByKey.TryGetValue(row.AssetKey, out AssetInfo assetInfo))
            {
                assetInfo = BuildAssetInfo(row.ResolvedAsset, row.AssetKey, resolutions, metrics);
                assetInfoByKey.Add(row.AssetKey, assetInfo);
            }

            row.SaveablePrefab = assetInfo.SaveablePrefab;
            row.PrefabAssetId = assetInfo.PrefabAssetId;
            row.AssetPath = assetInfo.AssetPath;
            row.AssetGuid = assetInfo.AssetGuid;
            row.IsPersistentPrefabAsset = assetInfo.IsPersistentPrefabAsset;
            row.IsVariant = assetInfo.IsVariant;
            row.VariantSourceAsset = assetInfo.VariantSourceAsset;
            row.VariantSourcePrefabAssetId = assetInfo.VariantSourcePrefabAssetId;
            row.VariantInheritsSourceId =
                row.IsVariant &&
                !string.IsNullOrEmpty(row.PrefabAssetId) &&
                string.Equals(
                    row.PrefabAssetId,
                    row.VariantSourcePrefabAssetId,
                    StringComparison.Ordinal);
            prefabAssetIdsByAssetKey[row.AssetKey] = row.PrefabAssetId;
            return row;
        }

        private AssetInfo BuildAssetInfo(
            GameObject asset,
            string assetKey,
            Dictionary<string, ResolutionRecord> resolutions,
            PrefabRegistryIdentityBuildMetrics metrics)
        {
            string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
            string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
            bool persistentPrefab =
                asset != null &&
                EditorUtility.IsPersistent(asset) &&
                !string.IsNullOrEmpty(path) &&
                PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.NotAPrefab;
            SaveablePrefab saveablePrefab = persistentPrefab ? asset.GetComponent<SaveablePrefab>() : null;

            var info = new AssetInfo
            {
                Asset = asset,
                AssetKey = assetKey,
                AssetPath = path,
                AssetGuid = guid,
                IsPersistentPrefabAsset = persistentPrefab,
                SaveablePrefab = saveablePrefab,
                PrefabAssetId = saveablePrefab != null ? saveablePrefab.PrefabAssetID : null,
                IsVariant = persistentPrefab &&
                            PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant
            };

            if (!info.IsVariant)
                return info;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(asset);
            if (source == null)
                return info;

            ResolutionRecord sourceResolution = ResolveOnce(source, resolutions, metrics);
            info.VariantSourceAsset = sourceResolution.Asset;
            if (!string.IsNullOrEmpty(sourceResolution.AssetKey))
            {
                knownAssetKeys.Add(sourceResolution.AssetKey);
                queryAssetKeys[GetDirectObjectKey(source)] = sourceResolution.AssetKey;
                queryAssetKeys[GetDirectObjectKey(sourceResolution.Asset)] = sourceResolution.AssetKey;
                objectAssetKeys[GetUnityObjectCacheKey(source)] = sourceResolution.AssetKey;
                objectAssetKeys[GetUnityObjectCacheKey(sourceResolution.Asset)] = sourceResolution.AssetKey;
            }

            SaveablePrefab sourceSaveable = info.VariantSourceAsset != null
                ? info.VariantSourceAsset.GetComponent<SaveablePrefab>()
                : null;
            info.VariantSourcePrefabAssetId =
                sourceSaveable != null ? sourceSaveable.PrefabAssetID : null;
            if (!string.IsNullOrEmpty(sourceResolution.AssetKey))
                prefabAssetIdsByAssetKey[sourceResolution.AssetKey] = info.VariantSourcePrefabAssetId;
            return info;
        }

        private static ResolutionRecord ResolveOnce(
            GameObject prefab,
            Dictionary<string, ResolutionRecord> resolutions,
            PrefabRegistryIdentityBuildMetrics metrics)
        {
            string referenceKey = GetDirectObjectKey(prefab);
            if (string.IsNullOrEmpty(referenceKey))
                return default;

            if (resolutions.TryGetValue(referenceKey, out ResolutionRecord cached))
                return cached;

            GameObject resolved = PrefabAssetResolver.ResolveNearestPrefabAsset(prefab) ?? prefab;
            var result = new ResolutionRecord
            {
                Asset = resolved,
                AssetKey = GetDirectObjectKey(resolved)
            };
            resolutions.Add(referenceKey, result);
            metrics.AssetResolutionCount++;
            return result;
        }

        private static void AddToMap(
            Dictionary<string, List<PrefabRegistryIdentityRow>> map,
            string key,
            PrefabRegistryIdentityRow row)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (!map.TryGetValue(key, out List<PrefabRegistryIdentityRow> list))
            {
                list = new List<PrefabRegistryIdentityRow>();
                map.Add(key, list);
            }

            int insertAt = list.FindIndex(existing => existing.Index > row.Index);
            if (insertAt >= 0)
                list.Insert(insertAt, row);
            else
                list.Add(row);
        }

        private void AddRowToMaps(PrefabRegistryIdentityRow row)
        {
            if (row == null)
                return;

            AddToMap(rowsByPrefabReferenceKey, GetDirectObjectKey(row.PrefabReference), row);
            AddToMap(rowsByAssetKey, row.AssetKey, row);
            AddToMap(rowsById, row.RegistryId, row);
        }

        private void RemoveRowFromMaps(PrefabRegistryIdentityRow row)
        {
            if (row == null)
                return;

            RemoveFromMap(rowsByPrefabReferenceKey, GetDirectObjectKey(row.PrefabReference), row);
            RemoveFromMap(rowsByAssetKey, row.AssetKey, row);
            RemoveFromMap(rowsById, row.RegistryId, row);
        }

        private static void RemoveFromMap(
            Dictionary<string, List<PrefabRegistryIdentityRow>> map,
            string key,
            PrefabRegistryIdentityRow row)
        {
            if (string.IsNullOrEmpty(key) || !map.TryGetValue(key, out var list))
                return;

            list.Remove(row);
            if (list.Count == 0)
                map.Remove(key);
        }

        private int FindRowPosition(PrefabRegistry.PrefabEntry entry)
        {
            if (entry == null)
                return -1;

            for (int i = 0; i < rows.Count; i++)
            {
                if (ReferenceEquals(rows[i].Entry, entry))
                    return i;
            }

            return -1;
        }

        private void ReindexRows()
        {
            for (int i = 0; i < rows.Count; i++)
                rows[i].Index = i;
        }

        private void RebuildFirstOwner(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            firstOwnerById.Remove(id);
            for (int i = 0; i < rows.Count; i++)
            {
                PrefabRegistryIdentityRow candidate = rows[i];
                if (candidate.ResolvedAsset == null ||
                    !string.Equals(candidate.RegistryId, id, StringComparison.Ordinal))
                    continue;

                firstOwnerById.Add(id, candidate);
                break;
            }
        }

        private bool ContainsKnownAsset(GameObject asset, string directKey)
        {
            string objectKey = GetUnityObjectCacheKey(asset);
            if (!string.IsNullOrEmpty(objectKey) &&
                objectAssetKeys.TryGetValue(objectKey, out string objectAssetKey) &&
                knownAssetKeys.Contains(objectAssetKey))
                return true;

            if (!string.IsNullOrEmpty(directKey) && knownAssetKeys.Contains(directKey))
                return true;

            string key = GetAssetKey(asset);
            return !string.IsNullOrEmpty(key) && knownAssetKeys.Contains(key);
        }

        private static bool IsVariantSourceIdUncached(GameObject prefabAsset, string id)
        {
            if (PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.Variant)
                return false;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(prefabAsset);
            if (source == null)
                return false;

            GameObject sourceAsset = PrefabAssetResolver.ResolveNearestPrefabAsset(source) ?? source;
            SaveablePrefab sourceSaveable = sourceAsset.GetComponent<SaveablePrefab>();
            return sourceSaveable != null &&
                   string.Equals(sourceSaveable.PrefabAssetID, id, StringComparison.Ordinal);
        }

        private static string GetDirectObjectKey(GameObject asset)
        {
            if (asset == null)
                return null;

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(guid)
                    ? $"guid:{guid}"
                    : $"path:{path}";
            }

#if UNITY_6000_5_OR_NEWER
            return $"instance:{asset.GetEntityId()}";
#else
            return $"instance:{asset.GetInstanceID()}";
#endif
        }

        private static string GetUnityObjectCacheKey(UnityEngine.Object asset)
        {
            if (asset == null)
                return null;

#if UNITY_6000_5_OR_NEWER
            return asset.GetEntityId().ToString();
#else
            return asset.GetInstanceID().ToString();
#endif
        }

        private static int GetUnityObjectIdentityHash(UnityEngine.Object asset)
        {
            if (asset == null)
                return 0;

#if UNITY_6000_5_OR_NEWER
            return asset.GetEntityId().GetHashCode();
#else
            return asset.GetInstanceID();
#endif
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            automaticSaveSuppressionUntil = 0d;
            automaticSaveSuppressionRegistry = null;
            InvalidateAll();
        }

        private sealed class CacheEntry
        {
            internal CacheEntry(
                PrefabRegistry registry,
                StructuralStamp stamp,
                PrefabRegistryIdentityIndex index)
            {
                Registry = registry;
                Stamp = stamp;
                Index = index;
            }

            internal PrefabRegistry Registry { get; }
            internal StructuralStamp Stamp { get; }
            internal PrefabRegistryIdentityIndex Index { get; }
        }

        private struct ResolutionRecord
        {
            internal GameObject Asset;
            internal string AssetKey;
        }

        private sealed class AssetInfo
        {
            internal GameObject Asset;
            internal string AssetKey;
            internal string AssetPath;
            internal string AssetGuid;
            internal bool IsPersistentPrefabAsset;
            internal SaveablePrefab SaveablePrefab;
            internal string PrefabAssetId;
            internal bool IsVariant;
            internal GameObject VariantSourceAsset;
            internal string VariantSourcePrefabAssetId;
        }

        private struct StructuralStamp : IEquatable<StructuralStamp>
        {
            private int revision;
            private int count;
            private long hash;

            internal static StructuralStamp Create(PrefabRegistry registry)
            {
                var result = new StructuralStamp();
                if (registry == null)
                    return result;

                result.revision = registry.EditorRevision;
                List<PrefabRegistry.PrefabEntry> entries = registry.prefabEntries;
                result.count = entries != null ? entries.Count : -1;

                unchecked
                {
                    long currentHash = 1469598103934665603L;
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            PrefabRegistry.PrefabEntry entry = entries[i];
                            currentHash = (currentHash * 1099511628211L) ^ i;
                            if (entry == null)
                                continue;

                            currentHash = (currentHash * 1099511628211L) ^
                                          RuntimeHelpers.GetHashCode(entry);
                            currentHash = (currentHash * 1099511628211L) ^
                                          GetUnityObjectIdentityHash(entry.prefab);
                            currentHash = (currentHash * 1099511628211L) ^
                                          (entry.uniqueID != null
                                              ? StringComparer.Ordinal.GetHashCode(entry.uniqueID)
                                              : 0);
                        }
                    }

                    result.hash = currentHash;
                }

                return result;
            }

            public bool Equals(StructuralStamp other)
            {
                return revision == other.revision && count == other.count && hash == other.hash;
            }

            public override bool Equals(object obj)
            {
                return obj is StructuralStamp other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = revision;
                    result = (result * 397) ^ count;
                    result = (result * 397) ^ hash.GetHashCode();
                    return result;
                }
            }
        }
    }
}
#endif
