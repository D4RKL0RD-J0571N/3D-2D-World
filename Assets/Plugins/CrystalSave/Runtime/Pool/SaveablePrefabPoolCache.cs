// ©2025 Arawn – Crystal Save
#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>Hands out (and owns) one pool per prefab asset.</summary>
	public static class SaveablePrefabPoolCache
	{
		private static readonly Dictionary<string, SaveablePrefabPool> _pools = new();
		private static int _disposalSuppressionDepth;

		/// <summary>
		/// Flag to suppress pool disposal during snapshot-based scene transitions.
		/// This prevents pooled prefabs from being destroyed when using LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync.
		/// </summary>
		public static bool SuppressPoolDisposal { get; set; } = false;

		/// <summary>
		/// Temporarily prevents global pool disposal while Crystal Save changes the
		/// active scene only to place pooled prefabs in the correct additive scene.
		/// </summary>
		public static System.IDisposable SuppressDisposal()
		{
			_disposalSuppressionDepth++;
			return new DisposalSuppressionScope();
		}

		/* Return home-scene pooled objects when an additive scene unloads.
		 * Additive workflows revisit the same scene repeatedly, so destroying the
		 * whole pool here defeats pooling and can look like gameplay destruction. */
		static SaveablePrefabPoolCache()
		{
			RegisterSceneCallbacks();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticStateForFastEnterPlayMode()
		{
			_pools.Clear();
			_disposalSuppressionDepth = 0;
			SuppressPoolDisposal = false;
			SaveablePrefabPool.ResetStaticStateForFastEnterPlayMode();
			RegisterSceneCallbacks();
		}

		private static void RegisterSceneCallbacks()
		{
			SceneManager.sceneUnloaded -= HandleSceneUnloaded;
			SceneManager.sceneUnloaded += HandleSceneUnloaded;
		}

		private static void HandleSceneUnloaded(Scene scene)
		{
			// Skip pool mutation during managed load/snapshot scene transitions.
			if (IsDisposalSuppressed || IsLoadOrSceneTransition)
				return;

			foreach (var pool in new List<SaveablePrefabPool>(_pools.Values))
			{
				if (pool == null || !pool.IsUsable)
					continue;

				pool.DespawnActiveForHomeScene(scene.name);
			}
		}

		private static bool IsDisposalSuppressed =>
			SuppressPoolDisposal || _disposalSuppressionDepth > 0;

		private sealed class DisposalSuppressionScope : System.IDisposable
		{
			private bool disposed;

			public void Dispose()
			{
				if (disposed)
					return;

				disposed = true;
				if (_disposalSuppressionDepth > 0)
					_disposalSuppressionDepth--;
			}
		}

		private static bool IsLoadOrSceneTransition
		{
			get
			{
				var manager = SaveManager.Instance;
				return manager != null &&
				       (manager.StateMachine?.CurrentState == SaveState.Loading ||
				        manager.IsInSceneTransition);
			}
		}

		/* ---------------------------------------------------------------- */
		public static SaveablePrefabPool Get(
			SaveablePrefab prefab,
			int initialSize,
			bool rememberObjects)
		{
			if (!prefab) return null;

			string id = string.IsNullOrEmpty(prefab.PrefabAssetID)
				? prefab.PrefabAssetID = System.Guid.NewGuid().ToString()
				: prefab.PrefabAssetID;

			bool deferWarmup = IsLoadOrSceneTransition || prefab.IsLoading;
			if (_pools.TryGetValue(id, out var pool) && (pool == null || !pool.IsValidForPrefab(prefab)))
			{
				pool?.Dispose();
				_pools.Remove(id);
			}

			if (!_pools.TryGetValue(id, out pool) || pool == null)
			{
				int warm = deferWarmup ? 0 : initialSize;
				pool = new SaveablePrefabPool(prefab, warm, rememberObjects);
				_pools[id] = pool;
			}

			// Keep the configured target, but do not prewarm hundreds of
			// inactive clones while a load or scene-transition restore is
			// actively trying to recreate saved objects.
			pool.SetTargetSize(initialSize, ensureCapacity: !deferWarmup);
			return pool;
		}

		/// <summary>
		/// Convenience overload using default warm-up and remembering behaviour.
		/// </summary>
		public static SaveablePrefabPool Get(SaveablePrefab prefab)
			=> Get(prefab, 0, true);

		public static bool TryDespawn(
			SaveablePrefab inst,
			int initialSize,
			bool rememberObjects)
		{
			if (!inst) return false;

			string id = string.IsNullOrEmpty(inst.PrefabAssetID)
				? inst.PrefabAssetID = System.Guid.NewGuid().ToString()
				: inst.PrefabAssetID;

			if (_pools.TryGetValue(id, out var pool) && (pool == null || !pool.IsUsable))
			{
				pool?.Dispose();
				_pools.Remove(id);
			}

			if (!_pools.TryGetValue(id, out pool) || pool == null)
			{
				pool = new SaveablePrefabPool(inst, 0, rememberObjects);
				_pools[id] = pool;
			}

			// Returning an existing object to the pool should not force an
			// immediate top-up to the target size. During home-scene
			// transitions this can otherwise allocate the full pool before
			// the current scene's active objects have been returned.
			pool.SetTargetSize(initialSize, ensureCapacity: false);
			pool.Despawn(inst);
			return true;
		}

		/// <summary>
		/// Convenience overload using default warm-up and remembering behaviour.
		/// </summary>
		public static bool TryDespawn(SaveablePrefab inst)
			=> TryDespawn(inst, 0, true);
	}
}
#endif
