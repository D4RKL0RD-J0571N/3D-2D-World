// SaveablePrefab.Persistence.cs
#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveablePrefab
	{
		public static event System.Action<SaveablePrefab> OnPrefabInstantiated;
		public static event System.Action<SaveablePrefab> OnPrefabDestroyed;
		public static event System.Action<SaveablePrefab> OnAfterRestore;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticStateForFastEnterPlayMode()
		{
			s_PrefabCache.Clear();
			s_UniqueIdCache.Clear();
			s_AssetIdCache.Clear();
			SaveablePrefabUtil.ClearTypeCache();

			OnPrefabInstantiated = null;
			OnPrefabDestroyed = null;
			OnAfterRestore = null;
		}

		internal static void RaiseAfterRestore(SaveablePrefab p)
		{
			p?.RefreshOptimizationBaseline();
			OnAfterRestore?.Invoke(p);
		}

		public void RegisterForSaving()
		{
			if (isRegisteredWithSaveManager) return;

			var mgr = SaveManager.Instance;
			if (mgr == null) return;

			if (settings == null)
			{
				var remember = GetComponent<RememberGameObject>();
				settings = remember ? remember.PropertySettings : propertySettings;
				settings ??= new GameObjectPropertySettings { RememberActive = true };
			}

			mgr.RegisterGameObject(gameObject, settings);
			isRegisteredWithSaveManager = true;

			// Capture initial snapshot for RememberHomeScene functionality
			// This ensures design-time prefabs registered at runtime have snapshot data
			// for snapshot-based scene switching (LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync)
			if (rememberHomeScene && !lastSnapshot.HasValue)
			{
					if (TryCaptureCurrentState(out var snapshot))
					{
						lastSnapshot = snapshot;
						if (Logger.ShouldLog(LogCategory.SaveablePrefab))
						{
							Logger.Log($"[CrystalSave][RegisterForSaving] Captured initial snapshot for '{Logger.GetUnityObjectName(this)}' (RememberHomeScene enabled)", LogCategory.SaveablePrefab, LogLevel.Info);
						}
					}
				}
		}

		internal bool IsTrackedBySaveManager =>
			isRegisteredWithSaveManager ||
			(SaveManager.Instance != null && SaveManager.Instance.IsGameObjectTracked(gameObject));

		public void UnregisterFromSaving()
		{
			var mgr = SaveManager.Instance;
			if (mgr == null) return;
			if (!isRegisteredWithSaveManager && !mgr.IsGameObjectTracked(gameObject)) return;

			mgr.UnregisterGameObject(gameObject);
			isRegisteredWithSaveManager = false;
		}

		public void ClearRegisteredFlag() => isRegisteredWithSaveManager = false;
	}
}
#endif
