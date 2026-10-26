#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using Unity.Profiling;
using Logger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.Editor
{
	[CustomEditor(typeof(PrefabRegistry))]
	public class PrefabRegistryEditor : UnityEditor.Editor
	{
		private string prefabRegistryFilter = string.Empty;

		private void OnEnable()
		{
			if (target is PrefabRegistry registry)
			{
				PrefabRegistryValidationCoordinator.RequestValidation(
					registry,
					PrefabRegistryValidationCoordinator.ValidationReason.Activation);
			}
		}

		public override void OnInspectorGUI()
		{
			PrefabRegistry registry = (PrefabRegistry)target;
			Event currentEvent = Event.current;
			if (currentEvent != null)
			{
				PrefabRegistryEditorDiagnosticCounters.RecordStandaloneInspectorEvent(
					registry,
					currentEvent.type);
			}

			serializedObject.UpdateIfRequiredOrScript();

			PrefabRegistryIdentityUtility.InspectionSnapshot snapshot =
				PrefabRegistryIdentityUtility.GetInspectionSnapshot(registry);
			PrefabRegistryNormalizationUtility.Result normalizationResult =
				snapshot.ConflictResult;
			PrefabRegistryNormalizationUtility.DrawConflictHelpBox(normalizationResult);

			bool drewFilteredRegistry =
				PrefabRegistryIdentityUtility.DrawRegistryEntriesWithFilter(
					snapshot,
					serializedObject,
					ref prefabRegistryFilter);
			if (!drewFilteredRegistry)
				EditorGUILayout.PropertyField(serializedObject.FindProperty("prefabEntries"), true);

			bool serializedChanged = serializedObject.ApplyModifiedProperties();
			if (serializedChanged)
			{
				EditorUtility.SetDirty(registry);
				registry.RebuildLookup();
				PrefabRegistryValidationCoordinator.NotifyRegistryEdited(registry);
			}

			GUILayout.Space(6);

			/* Auto-register button --------------------------------------- */
			if (GUILayout.Button("Auto-Register Prefab Assets"))
			{
				AutoRegisterPrefabs(registry);
				serializedObject.Update();
			}

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Clean Invalid Entries"))
			{
				PrefabRegistryNormalizationUtility.InvalidEntryCleanupResult cleanup =
					PrefabRegistryNormalizationUtility.CleanInvalidEntriesWithConfirmation(registry);
				if (cleanup.Changed)
				{
					serializedObject.Update();
					Logger.Log(
						$"[Crystal Save] Removed {cleanup.RemovedCount} invalid prefab registry entr{(cleanup.RemovedCount == 1 ? "y" : "ies")}.",
						LogLevel.Warning);
				}
			}

			/* Clean duplicates button ------------------------------------ */
			if (GUILayout.Button("Clean Duplicates"))
			{
				int removed = PrefabRegistryNormalizationUtility.CleanDuplicateEntries(registry);
				if (removed > 0)
					Logger.Log($"[Crystal Save] Removed {removed} duplicate prefab entr{(removed == 1 ? "y" : "ies")}.", LogLevel.Warning);
				else
					Logger.Log("[Crystal Save] No duplicates found in PrefabRegistry.", LogLevel.Info);
			}
			GUILayout.EndHorizontal();

			/* Sync pooling settings button -------------------------------- */
			GUILayout.Space(6);
			if (GUILayout.Button("Sync Pooling Settings to SaveablePrefabs"))
			{
				SyncPoolingSettingsToComponents(registry);
			}
		}

		internal void SetPrefabRegistryPerformanceDiagnosticFilter(string filter)
		{
			prefabRegistryFilter = filter ?? string.Empty;
			Repaint();
		}

		/* ─────────────────────────────────────────────────────────────── */
		private static void AutoRegisterPrefabs(PrefabRegistry registry)
			{
				string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });
				Undo.RecordObject(registry, "Auto-Register Prefab Assets");
				registry.prefabEntries.Clear();
				registry.RebuildLookup();
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.Build(registry);

				foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				if (pf == null) continue;

				SaveablePrefab saveable = pf.GetComponent<SaveablePrefab>();
				if (saveable == null) continue;

				string prefabAssetId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(saveable, pf, index);
				if (string.IsNullOrEmpty(prefabAssetId)) continue;

				if (!registry.TryAddPrefab(prefabAssetId, pf, out string reason))
					Logger.Log($"[Crystal Save] Could not add prefab '{pf.name}': {reason}", LogLevel.Warning);
				else
					index.ReserveIdOwner(prefabAssetId, pf);
			}

			registry.RebuildLookup();
			EditorUtility.SetDirty(registry);
			PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
			PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
			AssetDatabase.SaveAssets();
			Logger.Log("PrefabRegistryEditor: Auto-registered prefab assets.", LogLevel.Info);
		}

		/* ─────────────────────────────────────────────────────────────── */
		private static void SyncPoolingSettingsToComponents(PrefabRegistry registry)
		{
			int syncedCount = 0;
			string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" });

                        foreach (string guid in guids)
                        {
                                string path = AssetDatabase.GUIDToAssetPath(guid);
                                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                                if (prefab == null) continue;

                                SaveablePrefab saveablePrefab = prefab.GetComponent<SaveablePrefab>();
                                if (saveablePrefab == null || string.IsNullOrEmpty(saveablePrefab.PrefabAssetID)) continue;

                                // Only attempt to sync when the prefab is registered.
                                var entry = registry.FindEntryByID(saveablePrefab.PrefabAssetID);
                                if (entry == null) continue;

                                bool registryDisablesPooling = entry.disablePooling;

                                if (saveablePrefab.DisablePooling != registryDisablesPooling)
                                {
                                        Undo.RecordObject(saveablePrefab, "Sync Disable Pooling from Registry");
                                        saveablePrefab.DisablePooling = registryDisablesPooling;
                                        EditorUtility.SetDirty(prefab);
                                        syncedCount++;
                                        Logger.Log($"PrefabRegistryEditor: Synced DisablePooling={registryDisablesPooling} to SaveablePrefab '{prefab.name}'.", LogLevel.Info);
                                }
                        }

			if (syncedCount > 0)
			{
				AssetDatabase.SaveAssets();
				Logger.Log($"PrefabRegistryEditor: Synced pooling settings to {syncedCount} SaveablePrefab component{(syncedCount == 1 ? "" : "s")}.", LogLevel.Info);
			}
			else
			{
				Logger.Log("PrefabRegistryEditor: No SaveablePrefab components needed pooling setting synchronization.", LogLevel.Info);
			}
			}
		}

		internal static class PrefabRegistryNormalizationUtility
		{
			private static readonly ProfilerMarker NormalizeMarker =
				new ProfilerMarker("CrystalSave.PrefabRegistry.Normalize");

			internal sealed class Result
			{
				internal bool Changed { get; set; }
				internal List<ConflictInfo> Conflicts { get; } = new();
				internal bool HasConflicts => Conflicts.Count > 0;
			}

			internal sealed class ConflictInfo
			{
				internal string PrefabName { get; set; }
				internal string AssetPath { get; set; }
				internal string CurrentPrefabAssetID { get; set; }
				internal IReadOnlyList<string> RegistryIDs { get; set; }
				internal int RowCount { get; set; }
			}

			internal sealed class InvalidEntryCleanupResult
			{
				internal int InvalidEntryCount { get; set; }
				internal int RemovedCount { get; set; }
				internal bool Cancelled { get; set; }
				internal List<string> AffectedEntries { get; } = new();
				internal bool Changed => RemovedCount > 0;
			}

			private sealed class EntryRecord
			{
				internal PrefabRegistry.PrefabEntry Entry { get; set; }
				internal GameObject Asset { get; set; }
				internal SaveablePrefab SaveablePrefab { get; set; }
				internal string AssetPath { get; set; }
				internal int Index { get; set; }
				internal bool RequiresIndexSynchronization { get; set; }
			}

			internal static bool NormalizeEntries(PrefabRegistry registry, out Result result)
			{
				PrefabRegistryIdentityIndex index = PrefabRegistryIdentityIndex.GetOrCreate(registry);
				return NormalizeEntries(registry, index, out result);
			}

			internal static bool NormalizeEntries(
				PrefabRegistry registry,
				PrefabRegistryIdentityIndex index,
				out Result result,
				bool rebuildLookup = true)
			{
				result = new Result();
				if (registry == null || registry.prefabEntries == null)
					return false;
				PrefabRegistryEditorDiagnosticCounters.RecordNormalizationPass(registry);
				index ??= PrefabRegistryIdentityIndex.GetOrCreate(registry);

				using (NormalizeMarker.Auto())
				{
					bool changed = false;
					bool undoRecorded = false;
					List<List<EntryRecord>> groups = BuildAssetGroups(index)
						.OrderBy(group => group.Min(record => record.Index))
						.ToList();

					void RecordUndo()
					{
						if (undoRecorded) return;
						Undo.RecordObject(registry, "Normalize Prefab Registry");
						undoRecorded = true;
					}

					foreach (List<EntryRecord> group in groups)
					{
						foreach (EntryRecord record in group)
						{
							if (record.Entry == null || record.Asset == null || record.Entry.prefab == record.Asset)
								continue;

							RecordUndo();
							string previousName = record.Entry.prefab != null ? record.Entry.prefab.name : "<missing>";
							Logger.Log($"[Crystal Save] Corrected prefab registry reference '{previousName}' to prefab asset '{record.Asset.name}'.", LogLevel.Warning);
							record.Entry.prefab = record.Asset;
							record.RequiresIndexSynchronization = true;
							changed = true;
						}

						if (TryBuildConflict(group, out ConflictInfo conflict))
						{
							SynchronizeMutatedRecords(index, group);
							result.Conflicts.Add(conflict);
							continue;
						}

						EntryRecord representative = group.FirstOrDefault(record =>
							record.Entry != null &&
							record.Asset != null &&
							record.SaveablePrefab != null);
						if (representative == null)
						{
							SynchronizeMutatedRecords(index, group);
							continue;
						}

						SaveablePrefab saveable = representative.SaveablePrefab;
						string previousPrefabAssetId = saveable.PrefabAssetID;
						string authoritativeId = null;
						index.TryGetAuthoritativeId(representative.Asset, out authoritativeId);

						string ensuredId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(
							saveable,
							representative.Asset,
							index,
							authoritativeId);

						if (string.IsNullOrEmpty(ensuredId))
						{
							SynchronizeMutatedRecords(index, group);
							continue;
						}

						if (!string.Equals(previousPrefabAssetId, ensuredId, StringComparison.Ordinal))
							changed = true;

						foreach (EntryRecord record in group)
						{
							if (record.Entry == null || string.Equals(record.Entry.uniqueID, ensuredId, StringComparison.Ordinal))
								continue;

							RecordUndo();
							record.Entry.uniqueID = ensuredId;
							record.RequiresIndexSynchronization = true;
							changed = true;
						}

						SynchronizeMutatedRecords(index, group);
					}

					if (changed)
					{
						EditorUtility.SetDirty(registry);
						if (rebuildLookup)
							registry.RebuildLookup();
						PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
						PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
					}

					result.Changed = changed;
					return changed;
				}
			}

			internal static Result AnalyzeConflicts(PrefabRegistry registry)
			{
				if (registry == null || registry.prefabEntries == null)
					return new Result();

				return AnalyzeConflicts(PrefabRegistryIdentityIndex.GetOrCreate(registry));
			}

			internal static Result AnalyzeConflicts(PrefabRegistryIdentityIndex index)
			{
				var result = new Result();
				foreach (var group in BuildAssetGroups(index))
				{
					if (TryBuildConflict(group, out ConflictInfo conflict))
						result.Conflicts.Add(conflict);
				}

				return result;
			}

			internal static int CleanDuplicateEntries(PrefabRegistry registry)
			{
				if (registry == null || registry.prefabEntries == null)
					return 0;

				var groups = BuildAssetGroups(registry)
					.Where(group => group.Count > 1)
					.ToList();
				if (groups.Count == 0)
					return 0;

				Undo.RecordObject(registry, "Clean Prefab Registry Duplicates");

				int removed = 0;
				foreach (var group in groups)
				{
					EntryRecord keeper = ChooseKeeper(group);
					if (keeper == null)
						continue;

					GameObject keeperAsset = keeper.Asset;
					if (keeperAsset != null && keeper.Entry.prefab != keeperAsset)
						keeper.Entry.prefab = keeperAsset;

					int firstPoolSize = group
						.Select(record => record.Entry != null ? record.Entry.poolSize : 0)
						.FirstOrDefault(size => size > 0);
					if (firstPoolSize > 0)
						keeper.Entry.poolSize = firstPoolSize;

					keeper.Entry.disablePooling = group.Any(record => record.Entry != null && record.Entry.disablePooling);

					foreach (var record in group)
					{
						if (record == keeper || record.Entry == null)
							continue;

						if (registry.prefabEntries.Remove(record.Entry))
							removed++;
					}
				}

				if (removed > 0)
				{
					registry.RebuildLookup();
					EditorUtility.SetDirty(registry);
					PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
					PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
					AssetDatabase.SaveAssets();
				}

				return removed;
			}

			internal static InvalidEntryCleanupResult AnalyzeInvalidEntries(PrefabRegistry registry)
			{
				var result = new InvalidEntryCleanupResult();
				foreach (PrefabRegistryIdentityUtility.RegistryRowInfo row in
				         PrefabRegistryIdentityUtility.AnalyzeRows(registry).Where(row => row.IsInvalidRegistryEntry))
				{
					result.InvalidEntryCount++;
					result.AffectedEntries.Add(DescribeInvalidEntry(row));
				}

				return result;
			}

			internal static InvalidEntryCleanupResult CleanInvalidEntries(
				PrefabRegistry registry,
				bool saveAssets = true,
				bool rebuildLookup = true)
			{
				List<PrefabRegistryIdentityUtility.RegistryRowInfo> invalidRows =
					PrefabRegistryIdentityUtility.AnalyzeRows(registry)
						.Where(row => row.IsInvalidRegistryEntry)
						.OrderByDescending(row => row.Index)
						.ToList();

				var result = new InvalidEntryCleanupResult
				{
					InvalidEntryCount = invalidRows.Count
				};
				foreach (PrefabRegistryIdentityUtility.RegistryRowInfo row in invalidRows.OrderBy(row => row.Index))
					result.AffectedEntries.Add(DescribeInvalidEntry(row));

				if (registry?.prefabEntries == null || invalidRows.Count == 0)
					return result;

				Undo.RecordObject(registry, "Clean Invalid Prefab Registry Entries");
				foreach (PrefabRegistryIdentityUtility.RegistryRowInfo row in invalidRows)
				{
					if (row.Index < 0 || row.Index >= registry.prefabEntries.Count)
						continue;

					registry.prefabEntries.RemoveAt(row.Index);
					result.RemovedCount++;
				}

				if (result.RemovedCount > 0)
				{
					if (rebuildLookup)
						registry.RebuildLookup();
					EditorUtility.SetDirty(registry);
					PrefabRegistryIdentityIndex.NotifyRegistryMutated(registry);
					PrefabRegistryIdentityUtility.InvalidateInspectionSnapshot(registry);
					if (saveAssets)
						AssetDatabase.SaveAssets();
				}

				return result;
			}

			internal static InvalidEntryCleanupResult CleanInvalidEntriesWithConfirmation(PrefabRegistry registry)
			{
				InvalidEntryCleanupResult analysis = AnalyzeInvalidEntries(registry);
				if (analysis.InvalidEntryCount == 0)
				{
					EditorUtility.DisplayDialog(
						"Clean Invalid Prefab Registry Entries",
						"No missing, non-prefab, or no-SaveablePrefab registry entries were found.",
						"OK");
					return analysis;
				}

				string preview = string.Join("\n", analysis.AffectedEntries.Take(8).Select(entry => $"- {entry}"));
				if (analysis.AffectedEntries.Count > 8)
					preview += $"\n- ...and {analysis.AffectedEntries.Count - 8} more";

				bool confirmed = EditorUtility.DisplayDialog(
					"Clean Invalid Prefab Registry Entries",
					$"Remove {analysis.InvalidEntryCount} invalid PrefabRegistry " +
					$"entr{(analysis.InvalidEntryCount == 1 ? "y" : "ies")}?\n\n{preview}\n\n" +
					"Duplicate rows and ID mismatches will not be changed. This action supports Undo.",
					"Clean Invalid Entries",
					"Cancel");
				if (!confirmed)
				{
					analysis.Cancelled = true;
					return analysis;
				}

				return CleanInvalidEntries(registry);
			}

			internal static void DrawConflictHelpBox(Result result)
			{
				if (result == null || !result.HasConflicts)
					return;

				EditorGUILayout.HelpBox(BuildConflictMessage(result), MessageType.Warning);
			}

			private static List<List<EntryRecord>> BuildAssetGroups(PrefabRegistry registry)
			{
				return registry == null
					? new List<List<EntryRecord>>()
					: BuildAssetGroups(PrefabRegistryIdentityIndex.GetOrCreate(registry));
			}

			private static List<List<EntryRecord>> BuildAssetGroups(PrefabRegistryIdentityIndex index)
			{
				var groups = new Dictionary<string, List<EntryRecord>>(StringComparer.Ordinal);
				if (index?.Rows == null)
					return groups.Values.ToList();

				foreach (PrefabRegistryIdentityRow row in index.Rows)
				{
					PrefabRegistry.PrefabEntry entry = row?.Entry;
					if (entry == null || row.PrefabReference == null)
						continue;

					GameObject asset = row.ResolvedAsset;
					if (asset == null)
						continue;

					string assetPath = row.AssetPath;
					string key = row.AssetKey;
					if (string.IsNullOrEmpty(key))
						continue;

					if (!groups.TryGetValue(key, out var records))
					{
						records = new List<EntryRecord>();
						groups.Add(key, records);
					}

					records.Add(new EntryRecord
					{
						Entry = entry,
						Asset = asset,
						SaveablePrefab = row.SaveablePrefab,
						AssetPath = assetPath,
						Index = row.Index
					});
				}

				return groups.Values.ToList();
			}

			private static void SynchronizeMutatedRecords(
				PrefabRegistryIdentityIndex index,
				IEnumerable<EntryRecord> records)
			{
				if (index == null || records == null)
					return;

				foreach (EntryRecord record in records)
				{
					if (record == null || !record.RequiresIndexSynchronization || record.Entry == null)
						continue;

					index.SynchronizeMutation(record.Entry, record.Asset);
					record.RequiresIndexSynchronization = false;
				}
			}

			private static bool TryBuildConflict(List<EntryRecord> group, out ConflictInfo conflict)
			{
				conflict = null;
				if (group == null || group.Count <= 1)
					return false;

				var ids = group
					.Select(record => record.Entry != null ? record.Entry.uniqueID : null)
					.Where(id => !string.IsNullOrEmpty(id))
					.Distinct(StringComparer.Ordinal)
					.ToList();

				if (ids.Count <= 1)
					return false;

				GameObject asset = group[0].Asset;
				SaveablePrefab saveable = group[0].SaveablePrefab;
				conflict = new ConflictInfo
				{
					PrefabName = asset != null ? asset.name : "<missing prefab>",
					AssetPath = group[0].AssetPath,
					CurrentPrefabAssetID = saveable != null ? saveable.PrefabAssetID : null,
					RegistryIDs = ids,
					RowCount = group.Count
				};
				return true;
			}

			private static EntryRecord ChooseKeeper(List<EntryRecord> group)
			{
				if (group == null || group.Count == 0)
					return null;

				SaveablePrefab saveable = group[0].SaveablePrefab;
				string currentPrefabAssetId = saveable != null ? saveable.PrefabAssetID : null;

				if (!string.IsNullOrEmpty(currentPrefabAssetId))
				{
					EntryRecord matchingAssetId = group.FirstOrDefault(record =>
						record.Entry != null &&
						string.Equals(record.Entry.uniqueID, currentPrefabAssetId, StringComparison.Ordinal));
					if (matchingAssetId != null)
						return matchingAssetId;
				}

				EntryRecord firstNonEmptyId = group.FirstOrDefault(record =>
					record.Entry != null &&
					!string.IsNullOrEmpty(record.Entry.uniqueID));
				return firstNonEmptyId ?? group.OrderBy(record => record.Index).FirstOrDefault();
			}

			private static string DescribeInvalidEntry(PrefabRegistryIdentityUtility.RegistryRowInfo row)
			{
				if (row == null)
					return "Unknown registry row";

				string location = string.IsNullOrEmpty(row.AssetPath) ? "no asset path" : row.AssetPath;
				return $"Row {row.Index}: {row.DisplayName} ({location}) - {row.StatusSummary}";
			}

			private static string BuildConflictMessage(Result result)
			{
				string message =
					$"PrefabRegistry contains conflicting duplicate rows for {result.Conflicts.Count} prefab asset{(result.Conflicts.Count == 1 ? "" : "s")}. " +
					"Crystal Save has paused auto-normalization for those prefabs so their PrefabAssetID values are not rewritten repeatedly during Unity imports. " +
					"Use Clean Duplicates to keep one registry row per prefab asset.";

				foreach (var conflict in result.Conflicts.Take(5))
				{
					string ids = string.Join(", ", conflict.RegistryIDs);
					string current = string.IsNullOrEmpty(conflict.CurrentPrefabAssetID)
						? "empty"
						: conflict.CurrentPrefabAssetID;
					message += $"\n\n• {conflict.PrefabName} ({conflict.RowCount} rows)\nCurrent prefab ID: {current}\nRegistry IDs: {ids}";
				}

				if (result.Conflicts.Count > 5)
					message += $"\n\n...and {result.Conflicts.Count - 5} more conflict{(result.Conflicts.Count - 5 == 1 ? "" : "s")}.";

				return message;
			}
		}
	}
	#endif
	#endif
