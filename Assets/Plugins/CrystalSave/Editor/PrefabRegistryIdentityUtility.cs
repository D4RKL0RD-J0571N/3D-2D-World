#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	internal static class PrefabRegistryIdentityUtility
	{
		private static readonly ProfilerMarker InspectionSnapshotMarker =
			new ProfilerMarker("CrystalSave.PrefabRegistry.InspectionSnapshot");

		private static readonly Dictionary<int, InspectionSnapshot> InspectionSnapshots = new();

		internal sealed class InspectionSnapshot
		{
			internal PrefabRegistry Registry { get; set; }
			internal PrefabRegistryIdentityIndex IdentityIndex { get; set; }
			internal IReadOnlyList<RegistryRowInfo> Rows { get; set; }
			internal PrefabRegistryNormalizationUtility.Result ConflictResult { get; set; }
			internal int StructuralStamp { get; set; }
		}

		internal sealed class RegistryRowInfo
		{
			internal int Index { get; set; }
			internal PrefabRegistry.PrefabEntry Entry { get; set; }
			internal GameObject Prefab { get; set; }
			internal GameObject ResolvedAsset { get; set; }
			internal SaveablePrefab SaveablePrefab { get; set; }
			internal string RegistryID { get; set; }
			internal string PrefabAssetID { get; set; }
			internal string PrefabName { get; set; }
			internal string AssetPath { get; set; }
			internal string AssetGuid { get; set; }
			internal string AssetKey { get; set; }
			internal string VariantSourcePath { get; set; }
			internal string VariantSourceName { get; set; }
			internal string VariantSourcePrefabAssetID { get; set; }
			internal bool MissingPrefab { get; set; }
			internal bool InvalidAssetReference { get; set; }
			internal bool HasSaveablePrefab { get; set; }
			internal bool IsDuplicateID { get; set; }
			internal bool IsDuplicatePrefabRow { get; set; }
			internal bool IsIDMismatch { get; set; }
			internal bool VariantInheritsSourceID { get; set; }

			internal bool HasWarnings =>
				MissingPrefab ||
				InvalidAssetReference ||
				!HasSaveablePrefab ||
				IsDuplicateID ||
				IsDuplicatePrefabRow ||
				IsIDMismatch ||
				VariantInheritsSourceID;

			internal bool IsInvalidRegistryEntry =>
				Entry == null ||
				MissingPrefab ||
				InvalidAssetReference ||
				!HasSaveablePrefab;

			internal string DisplayName
			{
				get
				{
					if (MissingPrefab) return "<missing prefab>";
					if (!string.IsNullOrEmpty(PrefabName)) return PrefabName;
					return "<unnamed prefab>";
				}
			}

			internal string StatusSummary
			{
				get
				{
					List<string> labels = GetIssueLabels().ToList();
					return labels.Count == 0 ? "OK" : string.Join(", ", labels);
				}
			}

			internal IEnumerable<string> GetIssueLabels()
			{
				if (MissingPrefab) yield return "Missing prefab";
				if (!MissingPrefab && InvalidAssetReference) yield return "Not a persistent prefab asset";
				if (!MissingPrefab && !InvalidAssetReference && !HasSaveablePrefab) yield return "No SaveablePrefab";
				if (IsIDMismatch) yield return "ID mismatch";
				if (IsDuplicateID) yield return "Duplicate ID";
				if (IsDuplicatePrefabRow) yield return "Duplicate prefab row";
				if (VariantInheritsSourceID) yield return "Variant inherits source ID";
			}
		}

		internal static List<RegistryRowInfo> AnalyzeRows(PrefabRegistry registry)
		{
			return new List<RegistryRowInfo>(GetInspectionSnapshot(registry).Rows);
		}

		internal static InspectionSnapshot GetInspectionSnapshot(PrefabRegistry registry)
		{
			if (registry == null)
			{
				return new InspectionSnapshot
				{
					Rows = Array.Empty<RegistryRowInfo>(),
					ConflictResult = new PrefabRegistryNormalizationUtility.Result(),
					StructuralStamp = 0
				};
			}

			int registryId = GetObjectId(registry);
			int structuralStamp = ComputeStructuralStamp(registry);
			PrefabRegistryIdentityIndex currentIndex = PrefabRegistryIdentityIndex.GetOrCreate(registry);
			if (InspectionSnapshots.TryGetValue(registryId, out InspectionSnapshot cached) &&
			    cached.Registry == registry &&
			    cached.StructuralStamp == structuralStamp &&
			    ReferenceEquals(cached.IdentityIndex, currentIndex))
			{
				return cached;
			}

			using (InspectionSnapshotMarker.Auto())
			{
				List<RegistryRowInfo> rows = BuildRows(currentIndex);
				var snapshot = new InspectionSnapshot
				{
					Registry = registry,
					IdentityIndex = currentIndex,
					Rows = rows,
					ConflictResult = PrefabRegistryNormalizationUtility.AnalyzeConflicts(currentIndex),
					StructuralStamp = structuralStamp
				};

				InspectionSnapshots[registryId] = snapshot;
				PrefabRegistryEditorDiagnosticCounters.RecordInspectionSnapshotBuild(registry);
				return snapshot;
			}
		}

		internal static void InvalidateInspectionSnapshot(PrefabRegistry registry)
		{
			if (registry == null)
				return;

			InspectionSnapshots.Remove(GetObjectId(registry));
		}

		internal static void InvalidateAllInspectionSnapshots()
		{
			InspectionSnapshots.Clear();
		}

		private static List<RegistryRowInfo> BuildRows(PrefabRegistryIdentityIndex index)
		{
			List<RegistryRowInfo> rows = new List<RegistryRowInfo>();
			if (index?.Rows == null)
				return rows;

			foreach (PrefabRegistryIdentityRow indexedRow in index.Rows)
				rows.Add(CreateRowInfo(indexedRow));

			foreach (IGrouping<string, RegistryRowInfo> group in rows
				         .Where(row => !string.IsNullOrEmpty(row.RegistryID))
				         .GroupBy(row => row.RegistryID, StringComparer.Ordinal)
				         .Where(group => group.Count() > 1))
			{
				foreach (RegistryRowInfo row in group)
					row.IsDuplicateID = true;
			}

			foreach (IGrouping<string, RegistryRowInfo> group in rows
				         .Where(row => !string.IsNullOrEmpty(row.AssetKey))
				         .GroupBy(row => row.AssetKey, StringComparer.Ordinal)
				         .Where(group => group.Count() > 1))
			{
				foreach (RegistryRowInfo row in group)
					row.IsDuplicatePrefabRow = true;
			}

			return rows;
		}

		internal static bool DrawRegistryEntriesWithFilter(
			PrefabRegistry registry,
			SerializedObject serializedRegistry,
			ref string filter)
		{
			return DrawRegistryEntriesWithFilter(
				GetInspectionSnapshot(registry),
				serializedRegistry,
				ref filter);
		}

		internal static bool DrawRegistryEntriesWithFilter(
			InspectionSnapshot snapshot,
			SerializedObject serializedRegistry,
			ref string filter)
		{
			filter ??= string.Empty;
			IReadOnlyList<RegistryRowInfo> rows = snapshot?.Rows ?? Array.Empty<RegistryRowInfo>();

			DrawFilterToolbar(ref filter);
			DrawRegistrySummary(rows);

			if (string.IsNullOrWhiteSpace(filter))
				return false;

			SerializedProperty entriesProperty = serializedRegistry?.FindProperty("prefabEntries");
			if (entriesProperty == null)
			{
				EditorGUILayout.HelpBox("PrefabRegistry entries could not be read.", MessageType.Warning);
				return true;
			}

			string activeFilter = filter;
			List<RegistryRowInfo> matches = rows
				.Where(row => MatchesFilter(row, activeFilter))
				.ToList();

			EditorGUILayout.HelpBox(
				$"Showing {matches.Count} of {rows.Count} PrefabRegistry row{(rows.Count == 1 ? "" : "s")}. Clear the filter to return to Unity's full editable list.",
				MessageType.Info);

			foreach (RegistryRowInfo row in matches)
			{
				if (row.Index < 0 || row.Index >= entriesProperty.arraySize)
					continue;

				DrawFilteredRow(row, entriesProperty.GetArrayElementAtIndex(row.Index));
			}

			return true;
		}

		internal static bool MatchesFilter(RegistryRowInfo row, string filter)
		{
			if (row == null)
				return false;
			if (string.IsNullOrWhiteSpace(filter))
				return true;

			string[] tokens = filter
				.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

			return tokens.All(token => MatchesToken(row, token));
		}

		internal static string ToMarkdownTable(IEnumerable<RegistryRowInfo> rows)
		{
			List<RegistryRowInfo> rowList = rows?.ToList() ?? new List<RegistryRowInfo>();
			if (rowList.Count == 0)
				return "_No PrefabRegistry rows found._\n";

			List<string> lines = new List<string>
			{
				"| Row | Status | Prefab | Registry ID | PrefabAssetID | Asset GUID | Path | Variant Source |",
				"| --- | --- | --- | --- | --- | --- | --- | --- |"
			};

			foreach (RegistryRowInfo row in rowList)
			{
				lines.Add(
					$"| {row.Index} | {Escape(row.StatusSummary)} | {Escape(row.DisplayName)} | {Escape(row.RegistryID)} | {Escape(row.PrefabAssetID)} | {Escape(row.AssetGuid)} | {Escape(row.AssetPath)} | {Escape(row.VariantSourcePath)} |");
			}

			return string.Join("\n", lines) + "\n";
		}

		private static RegistryRowInfo CreateRowInfo(PrefabRegistryIdentityRow indexedRow)
		{
			PrefabRegistry.PrefabEntry entry = indexedRow?.Entry;
			RegistryRowInfo row = new RegistryRowInfo
			{
				Index = indexedRow?.Index ?? -1,
				Entry = entry,
				RegistryID = indexedRow?.RegistryId,
				Prefab = indexedRow?.PrefabReference,
				ResolvedAsset = indexedRow?.ResolvedAsset,
				SaveablePrefab = indexedRow?.SaveablePrefab,
				PrefabAssetID = indexedRow?.PrefabAssetId,
				AssetPath = indexedRow?.AssetPath,
				AssetGuid = indexedRow?.AssetGuid,
				AssetKey = indexedRow?.AssetKey,
				VariantSourcePath = indexedRow?.VariantSourceAsset != null
					? AssetDatabase.GetAssetPath(indexedRow.VariantSourceAsset)
					: null,
				VariantSourceName = indexedRow?.VariantSourceAsset != null
					? indexedRow.VariantSourceAsset.name
					: null,
				VariantSourcePrefabAssetID = indexedRow?.VariantSourcePrefabAssetId,
				VariantInheritsSourceID = indexedRow?.VariantInheritsSourceId ?? false
			};

			if (row.Prefab == null)
			{
				row.MissingPrefab = true;
				return row;
			}

			row.PrefabName = row.ResolvedAsset != null ? row.ResolvedAsset.name : row.Prefab.name;
			row.InvalidAssetReference =
				indexedRow == null ||
				!indexedRow.IsPersistentPrefabAsset;
			row.HasSaveablePrefab = row.SaveablePrefab != null;
			row.IsIDMismatch =
				row.SaveablePrefab != null &&
				!string.IsNullOrEmpty(row.RegistryID) &&
				!string.IsNullOrEmpty(row.PrefabAssetID) &&
				!string.Equals(row.RegistryID, row.PrefabAssetID, StringComparison.Ordinal);

			return row;
		}

		private static int ComputeStructuralStamp(PrefabRegistry registry)
		{
			unchecked
			{
				int hash = 17;
				List<PrefabRegistry.PrefabEntry> entries = registry?.prefabEntries;
				if (entries == null)
					return hash;

				hash = hash * 31 + entries.Count;
				foreach (PrefabRegistry.PrefabEntry entry in entries)
				{
					hash = hash * 31 + (entry == null ? 0 : 1);
					if (entry == null)
						continue;

					hash = hash * 31 + GetObjectId(entry.prefab);
					hash = hash * 31 + (entry.uniqueID == null
						? 0
						: StringComparer.Ordinal.GetHashCode(entry.uniqueID));
				}

				return hash;
			}
		}

		private static int GetObjectId(UnityEngine.Object obj)
		{
#if UNITY_6000_5_OR_NEWER
			return obj != null ? obj.GetEntityId().GetHashCode() : 0;
#else
			return obj != null ? obj.GetInstanceID() : 0;
#endif
		}

		private static void DrawFilterToolbar(ref string filter)
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label("Filter", GUILayout.Width(42f));

			GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSeachTextField") ??
			                       GUI.skin.FindStyle("ToolbarSearchTextField") ??
			                       EditorStyles.toolbarTextField;
			filter = GUILayout.TextField(filter ?? string.Empty, searchStyle);

			using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(filter)))
			{
				if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(52f)))
				{
					filter = string.Empty;
					GUI.FocusControl(null);
				}
			}

			EditorGUILayout.EndHorizontal();
		}

		private static void DrawRegistrySummary(IReadOnlyCollection<RegistryRowInfo> rows)
		{
			if (rows == null || rows.Count == 0)
				return;

			int warningCount = rows.Count(row => row.HasWarnings);
			if (warningCount == 0)
			{
				EditorGUILayout.HelpBox($"PrefabRegistry rows: {rows.Count}. No identity warnings detected.", MessageType.Info);
				return;
			}

			EditorGUILayout.HelpBox(
				$"PrefabRegistry rows: {rows.Count}. Rows with identity warnings: {warningCount}. Use the filter to search by prefab name, path, asset GUID, registry ID, or PrefabAssetID.",
				MessageType.Warning);
		}

		private static void DrawFilteredRow(RegistryRowInfo row, SerializedProperty entryProperty)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField($"Row {row.Index}: {row.DisplayName}", EditorStyles.boldLabel);

			MessageType messageType = row.HasWarnings ? MessageType.Warning : MessageType.Info;
			EditorGUILayout.HelpBox(row.StatusSummary, messageType);

			EditorGUILayout.LabelField("Path", string.IsNullOrEmpty(row.AssetPath) ? "<none>" : row.AssetPath);
			EditorGUILayout.LabelField("Asset GUID", string.IsNullOrEmpty(row.AssetGuid) ? "<none>" : row.AssetGuid);
			EditorGUILayout.LabelField("Registry ID", string.IsNullOrEmpty(row.RegistryID) ? "<empty>" : row.RegistryID);
			EditorGUILayout.LabelField("Prefab Asset ID", string.IsNullOrEmpty(row.PrefabAssetID) ? "<empty>" : row.PrefabAssetID);

			if (!string.IsNullOrEmpty(row.VariantSourcePath))
			{
				EditorGUILayout.LabelField("Variant Source", row.VariantSourcePath);
				EditorGUILayout.LabelField("Source Prefab Asset ID", string.IsNullOrEmpty(row.VariantSourcePrefabAssetID) ? "<empty>" : row.VariantSourcePrefabAssetID);
			}

			EditorGUILayout.PropertyField(entryProperty, new GUIContent($"Editable Row {row.Index}"), true);
			EditorGUILayout.EndVertical();
		}

		private static bool MatchesToken(RegistryRowInfo row, string token)
		{
			return Contains(row.DisplayName, token) ||
			       Contains(row.AssetPath, token) ||
			       Contains(row.AssetGuid, token) ||
			       Contains(row.RegistryID, token) ||
			       Contains(row.PrefabAssetID, token) ||
			       Contains(row.VariantSourceName, token) ||
			       Contains(row.VariantSourcePath, token) ||
			       Contains(row.VariantSourcePrefabAssetID, token) ||
			       Contains(row.StatusSummary, token);
		}

		private static bool Contains(string value, string token)
		{
			return !string.IsNullOrEmpty(value) &&
			       value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string Escape(string value)
		{
			if (string.IsNullOrEmpty(value))
				return "";

			return value
				.Replace("\\", "\\\\")
				.Replace("|", "\\|")
				.Replace("\r", " ")
				.Replace("\n", " ");
		}
	}
}
#endif
#endif
