// SaveablePrefabPool.cs ©2025 Arawn – Crystal Save
#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	public sealed class SaveablePrefabPool : IDisposable
	{
		/* ─── Global root ───────────────────────────────────────────────────── */
		private static Transform _globalRoot;
		private static Transform GlobalRoot
		{
			get
			{
				if (_globalRoot == null)
				{
					var go = new GameObject("RememberMe_Pools") { hideFlags = HideFlags.DontSave };
					// DontDestroyOnLoad is a Play Mode-only API. Keeping the
					// transient root in the active EditMode scene lets editor
					// diagnostics exercise pooling without emitting an error.
					if (Application.isPlaying)
						UnityEngine.Object.DontDestroyOnLoad(go);
					_globalRoot = go.transform;
				}

				return _globalRoot;
			}
		}

		/* ─── Fields ────────────────────────────────────────────────────────── */
		private readonly ObjectPool<SaveablePrefab> _pool;
		private readonly HashSet<SaveablePrefab> _active = new();
		private readonly HashSet<SaveablePrefab> _inactive = new();
		private readonly SaveablePrefab _prefab;
		private readonly string _prefabAssetID;
		private readonly string _prefabName;
		private readonly string _poolName;
		private readonly bool _remember;
		private int _targetSize;
		private Transform _container;
		private bool _restoreSpawnInProgress;
		private bool _capacityAdjustmentInProgress;

		/* ─── Ctor ──────────────────────────────────────────────────────────── */
		public SaveablePrefabPool(SaveablePrefab prefab,
								  int initialSize,
								  bool rememberObjects,
								  bool collectionCheck = false)
		{
			if (!prefab) throw new ArgumentNullException(nameof(prefab));

			_prefab = prefab;
			_prefabAssetID = prefab.PrefabAssetID;
			_prefabName = string.IsNullOrEmpty(prefab.name) ? nameof(SaveablePrefab) : prefab.name;
			_poolName = $"{_prefabName}_Pool";
			_remember = rememberObjects;
			_targetSize = Mathf.Max(0, initialSize);

			/* Re-use bucket if one already exists (prevents duplicates) */
			_container = GlobalRoot.Find(_poolName)
			             ?? new GameObject(_poolName).transform;
			_container.SetParent(GlobalRoot, false);
			_container.gameObject.SetActive(false);

			_pool = new ObjectPool<SaveablePrefab>(
				CreatePooledItem, OnSpawn, OnDespawn, null,
				collectionCheck, 0, int.MaxValue);          // start empty

			EnsureCapacity();                               // warm-up once

			if (_remember) EnsurePrefabRegistered(_prefab);
		}

		/* ─── Public API ───────────────────────────────────────────────────── */
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		internal static void ResetStaticStateForFastEnterPlayMode()
		{
			_globalRoot = null;
		}

		internal bool IsUsable => _prefab;

		internal bool IsValidForPrefab(SaveablePrefab prefab)
		{
			if (!_prefab || !prefab)
				return false;

			string incomingId = prefab.PrefabAssetID;
			return string.IsNullOrEmpty(_prefabAssetID) ||
			       string.IsNullOrEmpty(incomingId) ||
			       string.Equals(_prefabAssetID, incomingId, StringComparison.Ordinal);
		}

		public SaveablePrefab Spawn(Vector3 pos, Quaternion rot)
		{
			return SpawnInternal(pos, rot, false);
		}

		public SaveablePrefab SpawnForRestore(Vector3 pos, Quaternion rot)
		{
			return SpawnInternal(pos, rot, true);
		}

		private SaveablePrefab SpawnInternal(Vector3 pos, Quaternion rot, bool restoreSpawn)
		{
			if (!IsUsable)
			{
				Logger.Log($"SaveablePrefabPool: Cannot spawn from stale pool '{_poolName}' because its prefab was destroyed.", LogCategory.SaveablePrefab, LogLevel.Warning);
				return null;
			}

			SaveablePrefab obj;
			_restoreSpawnInProgress = restoreSpawn;
			try
			{
				obj = _pool.Get() ?? CreatePooledItem();
			}
			finally
			{
				_restoreSpawnInProgress = false;
			}

			if (restoreSpawn)
				PrepareForRestoreSpawn(obj);

			if (!obj)
				return null;

			_active.Add(obj);

			obj.transform.SetParent(null, true);

			var settings = SaveManager.Instance?.SaveSettings;
			if (settings?.spawnPooledPrefabsInScene == true)
			{
				SceneManager.MoveGameObjectToScene(obj.gameObject, SceneManager.GetActiveScene());
			}

			obj.transform.SetPositionAndRotation(pos, rot);

				if (obj.RememberHomeScene && Logger.ShouldLog(LogCategory.SaveablePrefab))
				{
					var builder = new StringBuilder(256);
					builder.Append("[CrystalSave][PoolHomeScene] Pool.Spawn placed '")
						.Append(Logger.GetUnityObjectName(obj))
						.Append("'. objectScene='").Append(SceneLabel(obj.gameObject.scene))
						.Append("', activeScene='").Append(SceneLabel(SceneManager.GetActiveScene()))
						.Append("', homeScene='").Append(HomeSceneLabel(obj.HomeScene))
						.Append("', spawnPooledPrefabsInScene=").Append(settings?.spawnPooledPrefabsInScene == true)
						.Append(", prefabId='").Append(obj.PrefabAssetID)
						.Append("', uid='").Append(obj.UniqueID)
						.Append("', position=");
					AppendVector3(builder, obj.transform.position);
					builder.Append('.');
					Logger.Log(builder.ToString(), LogCategory.SaveablePrefab, LogLevel.Info);
				}

			return obj;
		}

		public void Despawn(SaveablePrefab inst)
		{
			if (!inst || _inactive.Contains(inst))
				return;

			_pool.Release(inst);
		}

		public void SetTargetSize(int size, bool ensureCapacity = true)
		{
			_targetSize = Mathf.Max(0, size);
			if (ensureCapacity && IsUsable)
				EnsureCapacity();
		}

		public void Dispose()
		{
			/* 1 ─ destroy any STILL-ACTIVE clones (bullets in mid-air, etc.) */
			foreach (var sp in new List<SaveablePrefab>(_active))
			{
				if (!sp) continue;
				DestroyPoolObject(sp.gameObject);
			}
			_active.Clear();

			/* 2 ─ flush the inactive stack */
			_pool.Clear();
			_inactive.Clear();

			/* 3 ─ hard-destroy whatever is parked in the bucket right now    */
			if (_container != null)
			{
				for (int i = _container.childCount - 1; i >= 0; i--)
				{
					var go = _container.GetChild(i).gameObject;
					DestroyPoolObject(go);
				}

				_container.gameObject.SetActive(false);      // keep the bucket itself
			}
		}

		public int DespawnActiveForHomeScene(string sceneName)
		{
			PruneDestroyedActive();
			if (string.IsNullOrEmpty(sceneName) || _active.Count == 0)
				return 0;

			var matches = new List<SaveablePrefab>();
			foreach (var sp in _active)
			{
				if (!sp)
					continue;

				if (sp.RememberHomeScene &&
				    string.Equals(sp.HomeScene, sceneName, StringComparison.Ordinal))
				{
					matches.Add(sp);
				}
			}

			foreach (var sp in matches)
				Despawn(sp);

			return matches.Count;
		}

		/* ─── Capacity management ─────────────────────────────────────────── */
		internal void EnsureCapacity()
		{
			if (!IsUsable)
				return;

			PruneDestroyedActive();
			var settings = SaveManager.Instance?.SaveSettings;
			if (settings?.enablePooledPrefabBatching == true && settings.pooledPrefabSpawnBatchSize > 0)
			{
				// Use coroutine-based batching for better performance
				StartEnsureCapacityCoroutine();
			}
			else
			{
				// Use immediate capacity adjustment (original behavior)
				EnsureCapacityImmediate();
			}
		}

		private void StartEnsureCapacityCoroutine()
		{
			if (_container != null && _container.gameObject.activeInHierarchy)
			{
				SaveManager.Instance?.StartCoroutine(EnsureCapacityCoroutine());
			}
			else
			{
				// Fallback to immediate if no active container
				EnsureCapacityImmediate();
			}
		}

		private System.Collections.IEnumerator EnsureCapacityCoroutine()
		{
			if (!IsUsable)
				yield break;

			PruneDestroyedActive();
			int desiredInactive = Mathf.Max(0, _targetSize - _active.Count);
			int currentInactive = _pool.CountInactive;
			var settings = SaveManager.Instance?.SaveSettings;
			int batchSize = settings?.pooledPrefabSpawnBatchSize ?? 10;

			if (currentInactive < desiredInactive)
			{
				int missing = desiredInactive - currentInactive;
				int processed = 0;

				for (int i = 0; i < missing; i++)
				{
					var clone = CreatePooledItem();
					if (!clone)
						yield break;

					clone.gameObject.SetActive(false);
					_pool.Release(clone);               // parks it + runs OnDespawn

					processed++;
					if (processed % batchSize == 0)
						yield return null; // Yield to prevent frame drops
				}
			}
			else if (currentInactive > desiredInactive)
			{
				int excess = currentInactive - desiredInactive;
				int processed = 0;

				for (int i = 0; i < excess; i++)
				{
					SaveablePrefab inst;
					_capacityAdjustmentInProgress = true;
					try
					{
						inst = _pool.Get();         // remove from pool
					}
					finally
					{
						_capacityAdjustmentInProgress = false;
					}
					if (!inst) continue;

					if (inst.RegisterWithSaveSystem)
						SoftUnregister(inst);

					// Deactivate first so any SaveableComponents
					// immediately unregister from the manager.
					if (inst.gameObject.activeSelf)
						inst.gameObject.SetActive(false);

					// We no longer capture a snapshot or register
					// a destroyed ID for pooled clones that exceed
					// the desired capacity.
					UnityEngine.Object.Destroy(inst.gameObject);

					processed++;
					if (processed % batchSize == 0)
						yield return null; // Yield to prevent frame drops
				}
			}
		}

		private void EnsureCapacityImmediate()
		{
			if (!IsUsable)
				return;

			PruneDestroyedActive();
			int desiredInactive = Mathf.Max(0, _targetSize - _active.Count);
			int currentInactive = _pool.CountInactive;

			if (currentInactive < desiredInactive)
			{
				int missing = desiredInactive - currentInactive;
				for (int i = 0; i < missing; i++)
				{
					var clone = CreatePooledItem();
					if (!clone)
						break;

					clone.gameObject.SetActive(false);
					_pool.Release(clone);               // parks it + runs OnDespawn
				}
			}
			else if (currentInactive > desiredInactive)
			{
				int excess = currentInactive - desiredInactive;
				for (int i = 0; i < excess; i++)
				{
					SaveablePrefab inst;
					_capacityAdjustmentInProgress = true;
					try
					{
						inst = _pool.Get();         // remove from pool
					}
					finally
					{
						_capacityAdjustmentInProgress = false;
					}
					if (!inst) continue;

					if (inst.RegisterWithSaveSystem)
						SoftUnregister(inst);

					// Deactivate first so any SaveableComponents
					// immediately unregister from the manager.
					if (inst.gameObject.activeSelf)
						inst.gameObject.SetActive(false);

					// We no longer capture a snapshot or register
					// a destroyed ID for pooled clones that exceed
					// the desired capacity.
					UnityEngine.Object.Destroy(inst.gameObject);
				}
			}
		}

		/* ─── Pool callbacks & helpers ────────────────────────────────────── */
		private Transform EnsureContainer()
		{
			if (_container == null)
			{
				_container = new GameObject(_poolName).transform;
				_container.SetParent(GlobalRoot, false);
				_container.gameObject.SetActive(false);
			}
			return _container;
		}

		private SaveablePrefab CreatePooledItem()
		{
			if (!IsUsable)
				return null;

			var clone = UnityEngine.Object.Instantiate(_prefab, EnsureContainer());
			if (!clone)
				return null;

			if (SaveManager.Instance)
				SaveManager.Instance.SoftUnregisterGameObject(clone.gameObject);

			clone.RegisterWithSaveSystem = false;
			clone.ClearRegisteredFlag();

			/* NEW ▸ never keep pooled clones across scene loads                        */
			clone.KeepAcrossScenes = false;                          // <───── added
			return clone;
		}

		private void OnSpawn(SaveablePrefab sp)
		{
			if (!sp) return;

			_inactive.Remove(sp);

				if (sp.RememberHomeScene && Logger.ShouldLog(LogCategory.SaveablePrefab))
				{
					var builder = new StringBuilder(256);
					builder.Append("[CrystalSave][PoolHomeScene] Pool.OnSpawn begin for '")
						.Append(Logger.GetUnityObjectName(sp))
						.Append("'. objectScene='").Append(SceneLabel(sp.gameObject.scene))
						.Append("', activeScene='").Append(SceneLabel(SceneManager.GetActiveScene()))
						.Append("', homeBefore='").Append(HomeSceneLabel(sp.HomeScene))
						.Append("', registerBefore=").Append(sp.RegisterWithSaveSystem)
						.Append(", rememberObjects=").Append(_remember)
						.Append(", prefabId='").Append(sp.PrefabAssetID)
						.Append("', uid='").Append(sp.UniqueID).Append("'.");
					Logger.Log(builder.ToString(), LogCategory.SaveablePrefab, LogLevel.Info);
				}

			/* NEW ▸ guarantee the runtime instance itself is *not* KAS even if the     *
			 * prefab asset had the flag set in the Inspector                           */
			sp.KeepAcrossScenes = false;                             // <───── added

			if (_remember)
			{
				sp.MarkAsAddedAtRuntime();
				sp.ReuseSceneInstanceOnLoad = false;
				bool restoreSpawn = _restoreSpawnInProgress ||
					_capacityAdjustmentInProgress ||
					SaveManager.Instance?.StateMachine?.CurrentState == SaveState.Loading ||
					sp.IsLoading;
				if (restoreSpawn)
				{
					PrepareForRestoreSpawn(sp);
				}
				else
				{
					sp.RegisterWithSaveSystem = true;
					sp.OnBeforeSpawn();                                  // fresh GUID + registration
				}
			}
			else
			{
				SoftUnregister(sp);
				sp.RegisterWithSaveSystem = false;
			}

			if (!_capacityAdjustmentInProgress)
				sp.gameObject.SetActive(true);

				if (sp.RememberHomeScene && Logger.ShouldLog(LogCategory.SaveablePrefab))
				{
					var builder = new StringBuilder(256);
					builder.Append("[CrystalSave][PoolHomeScene] Pool.OnSpawn end for '")
						.Append(Logger.GetUnityObjectName(sp))
						.Append("'. objectScene='").Append(SceneLabel(sp.gameObject.scene))
						.Append("', activeScene='").Append(SceneLabel(SceneManager.GetActiveScene()))
						.Append("', homeAfter='").Append(HomeSceneLabel(sp.HomeScene))
						.Append("', registerAfter=").Append(sp.RegisterWithSaveSystem)
						.Append(", rememberObjects=").Append(_remember)
						.Append(", prefabId='").Append(sp.PrefabAssetID)
						.Append("', uid='").Append(sp.UniqueID).Append("'.");
					Logger.Log(builder.ToString(), LogCategory.SaveablePrefab, LogLevel.Info);
				}
		}

		private void OnDespawn(SaveablePrefab sp)
		{
			if (!sp) return;

			_active.Remove(sp);
			_inactive.Add(sp);

			if (sp.RegisterWithSaveSystem)
			{
				SoftUnregister(sp);
				sp.RegisterWithSaveSystem = false;
			}

			sp.ResetForPooling();
			sp.gameObject.name = _prefabName + "(Clone)";
			sp.transform.SetParent(EnsureContainer(), false);
			sp.gameObject.SetActive(false);
		}

		private static void PrepareForRestoreSpawn(SaveablePrefab sp)
		{
			if (!sp) return;

			sp.ResetForPooling();
			sp.MarkAsAddedAtRuntime();
			sp.ReuseSceneInstanceOnLoad = false;
			sp.RegisterWithSaveSystem = false;
		}

		private static void SoftUnregister(SaveablePrefab sp)
		{
			if (sp && SaveManager.Instance && !string.IsNullOrEmpty(sp.UniqueID))
				SaveManager.Instance.SoftUnregisterGameObject(sp.gameObject);
		}

		private static void DestroyPoolObject(GameObject go)
		{
			if (!go) return;

			if (go.TryGetComponent(out SaveablePrefab sp) && sp != null)
			{
				SaveablePrefabFactory.CancelPendingDestroy(sp);

				if (!string.IsNullOrEmpty(sp.UniqueID))
					SoftUnregister(sp);

				sp.RegisterWithSaveSystem = false;
				sp.ClearRegisteredFlag();
			}

			if (go.activeSelf)
				go.SetActive(false);

			DestroyHelper.DestroyWithLogging(go, "SaveablePrefabPool.Dispose");
		}

		private static void EnsurePrefabRegistered(SaveablePrefab prefab)
		{
			if (!prefab) return;

			var registry = AssetProvider.Load<PrefabRegistry>("PrefabRegistry");
			if (!registry) return;

			if (string.IsNullOrEmpty(prefab.PrefabAssetID))
				prefab.PrefabAssetID = Guid.NewGuid().ToString();

			if (!registry.prefabEntries.Exists(e => e.uniqueID == prefab.PrefabAssetID))
				registry.TryAddPrefab(prefab.PrefabAssetID, prefab.gameObject, out _);
		}

		private void PruneDestroyedActive()
		{
			if (_active.Count == 0)
				return;

			_active.RemoveWhere(sp => !sp);
		}

		private static string SceneLabel(Scene scene)
		{
			return scene.IsValid() ? scene.name : "<invalid>";
		}

		private static string HomeSceneLabel(string sceneName)
		{
			return string.IsNullOrEmpty(sceneName) ? "<empty>" : sceneName;
		}

		private static void AppendVector3(StringBuilder builder, Vector3 value)
		{
			builder.Append('(')
				.Append(value.x.ToString("F2")).Append(", ")
				.Append(value.y.ToString("F2")).Append(", ")
				.Append(value.z.ToString("F2")).Append(')');
		}
	}
}
#endif
