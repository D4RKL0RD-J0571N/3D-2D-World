#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
	internal enum PrefabRegistrySyncStatus
	{
		Added,
		ExistingOwnerPreserved,
		RegistryIdAdopted,
		EmptyRegistryIdFilled,
		AlreadySynchronized,
		SkippedInvalidAsset,
		ConflictingRows,
		IdConflict,
		Failed
	}

	internal readonly struct PrefabRegistrySyncResult
	{
		internal PrefabRegistrySyncResult(
			PrefabRegistrySyncStatus status,
			string appliedId,
			string message,
			bool changed)
		{
			Status = status;
			AppliedId = appliedId;
			Message = message;
			Changed = changed;
		}

		internal PrefabRegistrySyncStatus Status { get; }
		internal string AppliedId { get; }
		internal string Message { get; }
		internal bool Changed { get; }

		internal bool Succeeded =>
			Status == PrefabRegistrySyncStatus.Added ||
			Status == PrefabRegistrySyncStatus.ExistingOwnerPreserved ||
			Status == PrefabRegistrySyncStatus.RegistryIdAdopted ||
			Status == PrefabRegistrySyncStatus.EmptyRegistryIdFilled ||
			Status == PrefabRegistrySyncStatus.AlreadySynchronized;
	}

	internal static class PrefabRegistrySyncUtility
	{
		internal static PrefabRegistrySyncResult Synchronize(
			PrefabRegistry registry,
			SaveablePrefab sourcePrefab,
			GameObject candidateAsset,
			string requestedId,
			bool saveAssets = true)
		{
			if (registry == null)
			{
				return Failure(
					PrefabRegistrySyncStatus.Failed,
					requestedId,
					"No active PrefabRegistry is available.");
			}

			GameObject asset = PrefabAssetResolver.ResolveNearestPrefabAsset(candidateAsset) ?? candidateAsset;
			if (!TryGetPersistentPrefabIdentity(asset, out _, out _))
			{
				return Failure(
					PrefabRegistrySyncStatus.SkippedInvalidAsset,
					requestedId,
					"Registry synchronization was skipped because the target does not resolve to a persistent prefab asset.");
			}

			SaveablePrefab assetComponent = asset.GetComponent<SaveablePrefab>();
			if (assetComponent == null)
			{
				return Failure(
					PrefabRegistrySyncStatus.SkippedInvalidAsset,
					requestedId,
					$"Registry synchronization was skipped because {DescribeAsset(asset)} has no root SaveablePrefab.");
			}

			SaveablePrefab identityComponent = ResolveIdentityComponent(sourcePrefab, asset, assetComponent);
			if (PrefabAssetResolver.HasConflictingRegistryEntriesForAsset(registry, asset))
			{
				return Failure(
					PrefabRegistrySyncStatus.ConflictingRows,
					requestedId,
					$"Registry synchronization was skipped for {DescribeAsset(asset)} because multiple rows assign different IDs to that prefab. Run Clean Duplicates first.");
			}

			PrefabRegistry.PrefabEntry sameAsset = registry.FindEntryByPrefab(asset);
			if (sameAsset != null && !string.IsNullOrEmpty(sameAsset.uniqueID))
			{
				bool componentChanged = ApplyPrefabAssetId(
					identityComponent,
					assetComponent,
					asset,
					sameAsset.uniqueID,
					"Adopt Prefab Registry ID");

				if (componentChanged && saveAssets)
					AssetDatabase.SaveAssets();

				PrefabRegistrySyncStatus status = componentChanged
					? PrefabRegistrySyncStatus.RegistryIdAdopted
					: PrefabRegistrySyncStatus.AlreadySynchronized;
				string message = componentChanged
					? $"Preserved registered PrefabAssetID '{sameAsset.uniqueID}' for {DescribeAsset(asset)} and restored it on the prefab."
					: $"{DescribeAsset(asset)} is already synchronized with PrefabAssetID '{sameAsset.uniqueID}'.";

				return new PrefabRegistrySyncResult(status, sameAsset.uniqueID, message, componentChanged);
			}

			string proposedId = !string.IsNullOrWhiteSpace(requestedId)
				? requestedId.Trim()
				: identityComponent.PrefabAssetID;
			PrefabRegistry.PrefabEntry previousOwner = FindIdOwner(registry, proposedId, asset);

			string previousIdentityId = identityComponent.PrefabAssetID;
			string previousAssetId = assetComponent.PrefabAssetID;
			RecordIdentityUndo(identityComponent, assetComponent, asset, "Synchronize Prefab Asset ID");
			string ensuredId = PrefabAssetResolver.EnsurePrefabAssetIDForAsset(
				identityComponent,
				asset,
				registry,
				proposedId);

			if (string.IsNullOrEmpty(ensuredId))
			{
				return Failure(
					PrefabRegistrySyncStatus.ConflictingRows,
					proposedId,
					$"Registry synchronization was skipped for {DescribeAsset(asset)} because its registry ownership is ambiguous.");
			}

			bool identityChanged =
				!string.Equals(previousIdentityId, ensuredId, StringComparison.Ordinal) ||
				!string.Equals(previousAssetId, ensuredId, StringComparison.Ordinal);
			identityChanged |= ApplyPrefabAssetIdWithoutUndo(identityComponent, assetComponent, asset, ensuredId);
			PrefabRegistry.PrefabEntry conflictingOwner = FindIdOwner(registry, ensuredId, asset);
			if (conflictingOwner != null)
			{
				return Failure(
					PrefabRegistrySyncStatus.IdConflict,
					ensuredId,
					$"Registry synchronization refused to assign PrefabAssetID '{ensuredId}' to {DescribeAsset(asset)} because it is owned by {DescribeAsset(conflictingOwner.prefab)}.");
			}

			if (sameAsset != null)
			{
				Undo.RecordObject(registry, "Fill Prefab Registry ID");
				sameAsset.uniqueID = ensuredId;
				FinalizeRegistryMutation(registry, asset, identityComponent, assetComponent, saveAssets);
				return new PrefabRegistrySyncResult(
					PrefabRegistrySyncStatus.EmptyRegistryIdFilled,
					ensuredId,
					$"Filled the empty registry ID for {DescribeAsset(asset)} with '{ensuredId}'.",
					true);
			}

			registry.RebuildLookup();
			Undo.RecordObject(registry, "Add Prefab to Registry");
			if (!registry.TryAddPrefab(ensuredId, asset, out string reason))
			{
				if (identityChanged && saveAssets)
					AssetDatabase.SaveAssets();

				return Failure(
					PrefabRegistrySyncStatus.IdConflict,
					ensuredId,
					$"Registry synchronization could not add {DescribeAsset(asset)}: {reason}");
			}

			FinalizeRegistryMutation(registry, asset, identityComponent, assetComponent, saveAssets);
			bool preservedExistingOwner = previousOwner != null &&
			                              !string.Equals(proposedId, ensuredId, StringComparison.Ordinal);
			if (preservedExistingOwner)
			{
				return new PrefabRegistrySyncResult(
					PrefabRegistrySyncStatus.ExistingOwnerPreserved,
					ensuredId,
					$"Preserved PrefabAssetID '{proposedId}' on {DescribeAsset(previousOwner.prefab)} and registered {DescribeAsset(asset)} with the new ID '{ensuredId}'.",
					true);
			}

			return new PrefabRegistrySyncResult(
				PrefabRegistrySyncStatus.Added,
				ensuredId,
				$"Registered {DescribeAsset(asset)} with PrefabAssetID '{ensuredId}'.",
				true);
		}

		internal static bool AreSamePrefabAsset(GameObject left, GameObject right)
		{
			if (left == null || right == null) return false;
			if (left == right) return true;

			if (!TryGetPersistentPrefabIdentity(left, out string leftPath, out string leftGuid) ||
			    !TryGetPersistentPrefabIdentity(right, out string rightPath, out string rightGuid))
				return false;

			if (!string.IsNullOrEmpty(leftGuid) && !string.IsNullOrEmpty(rightGuid))
				return string.Equals(leftGuid, rightGuid, StringComparison.Ordinal);

			return string.Equals(leftPath, rightPath, StringComparison.Ordinal);
		}

		internal static string DescribeAsset(GameObject asset)
		{
			if (asset == null) return "<missing prefab>";

			GameObject resolved = PrefabAssetResolver.ResolveNearestPrefabAsset(asset) ?? asset;
			string path = AssetDatabase.GetAssetPath(resolved);
			string guid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
			return $"'{resolved.name}' (path: {ValueOrNone(path)}, GUID: {ValueOrNone(guid)})";
		}

		private static SaveablePrefab ResolveIdentityComponent(
			SaveablePrefab sourcePrefab,
			GameObject asset,
			SaveablePrefab assetComponent)
		{
			if (sourcePrefab == null)
				return assetComponent;

			GameObject sourceAsset = PrefabAssetResolver.ResolveNearestPrefabAsset(sourcePrefab.gameObject);
			return AreSamePrefabAsset(sourceAsset, asset) ? sourcePrefab : assetComponent;
		}

		private static PrefabRegistry.PrefabEntry FindIdOwner(
			PrefabRegistry registry,
			string id,
			GameObject targetAsset)
		{
			if (registry?.prefabEntries == null || string.IsNullOrEmpty(id))
				return null;

			return registry.prefabEntries.FirstOrDefault(entry =>
				entry != null &&
				entry.prefab != null &&
				string.Equals(entry.uniqueID, id, StringComparison.Ordinal) &&
				!AreSamePrefabAsset(entry.prefab, targetAsset));
		}

		private static bool TryGetPersistentPrefabIdentity(
			GameObject candidate,
			out string assetPath,
			out string assetGuid)
		{
			assetPath = null;
			assetGuid = null;
			if (candidate == null)
				return false;

			GameObject asset = PrefabAssetResolver.ResolveNearestPrefabAsset(candidate) ?? candidate;
			if (!EditorUtility.IsPersistent(asset) ||
			    PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab)
				return false;

			assetPath = AssetDatabase.GetAssetPath(asset);
			if (string.IsNullOrEmpty(assetPath))
				return false;

			assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
			return !string.IsNullOrEmpty(assetGuid);
		}

		private static bool ApplyPrefabAssetId(
			SaveablePrefab sourcePrefab,
			SaveablePrefab assetComponent,
			GameObject asset,
			string id,
			string undoName)
		{
			bool needsChange =
				(sourcePrefab != null && !string.Equals(sourcePrefab.PrefabAssetID, id, StringComparison.Ordinal)) ||
				(assetComponent != null && !string.Equals(assetComponent.PrefabAssetID, id, StringComparison.Ordinal));
			if (!needsChange)
				return false;

			RecordIdentityUndo(sourcePrefab, assetComponent, asset, undoName);
			return ApplyPrefabAssetIdWithoutUndo(sourcePrefab, assetComponent, asset, id);
		}

		private static bool ApplyPrefabAssetIdWithoutUndo(
			SaveablePrefab sourcePrefab,
			SaveablePrefab assetComponent,
			GameObject asset,
			string id)
		{
			bool changed = false;
			if (sourcePrefab != null && !string.Equals(sourcePrefab.PrefabAssetID, id, StringComparison.Ordinal))
			{
				SetSerializedPrefabAssetId(sourcePrefab, id);
				changed = true;
			}

			if (assetComponent != null &&
			    assetComponent != sourcePrefab &&
			    !string.Equals(assetComponent.PrefabAssetID, id, StringComparison.Ordinal))
			{
				SetSerializedPrefabAssetId(assetComponent, id);
				changed = true;
			}

			if (changed && asset != null)
				EditorUtility.SetDirty(asset);

			return changed;
		}

		private static void SetSerializedPrefabAssetId(SaveablePrefab prefab, string id)
		{
			SerializedObject serialized = new SerializedObject(prefab);
			SerializedProperty property = serialized.FindProperty("prefabAssetID");
			if (property == null)
				return;

			property.stringValue = id;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(prefab);
		}

		private static void RecordIdentityUndo(
			SaveablePrefab sourcePrefab,
			SaveablePrefab assetComponent,
			GameObject asset,
			string undoName)
		{
			var objects = new HashSet<UnityEngine.Object>();
			if (sourcePrefab != null) objects.Add(sourcePrefab);
			if (assetComponent != null) objects.Add(assetComponent);
			if (asset != null) objects.Add(asset);
			if (objects.Count > 0)
				Undo.RecordObjects(objects.ToArray(), undoName);
		}

		private static void FinalizeRegistryMutation(
			PrefabRegistry registry,
			GameObject asset,
			SaveablePrefab sourcePrefab,
			SaveablePrefab assetComponent,
			bool saveAssets)
		{
			registry.RebuildLookup();
			EditorUtility.SetDirty(registry);
			if (asset != null) EditorUtility.SetDirty(asset);
			if (sourcePrefab != null) EditorUtility.SetDirty(sourcePrefab);
			if (assetComponent != null) EditorUtility.SetDirty(assetComponent);
			if (saveAssets) AssetDatabase.SaveAssets();
		}

		private static PrefabRegistrySyncResult Failure(
			PrefabRegistrySyncStatus status,
			string id,
			string message)
		{
			return new PrefabRegistrySyncResult(status, id, message, false);
		}

		private static string ValueOrNone(string value)
		{
			return string.IsNullOrEmpty(value) ? "<none>" : value;
		}
	}
}
#endif
