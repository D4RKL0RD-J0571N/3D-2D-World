#if MEMORYPACK && ARAWN_REMEMBERME
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	/// <summary>
	/// Proxy component to detect when a GameObject is activated, deactivated, or destroyed.
	/// </summary>
        public class TrackedGameObjectProxy : MonoBehaviour
        {
                private GameObjectTracker tracker;
                private string uniqueID;
                private bool pendingRemoval;

                internal bool IsPendingRemoval => pendingRemoval;

		/// <summary>
		/// Initializes the proxy with a reference to the SaveManager and the GameObject's UniqueID.
		/// </summary>
                /// <param name="tracker">Reference to the GameObjectTracker.</param>
                /// <param name="id">UniqueID of the GameObject.</param>
                public void Initialize(GameObjectTracker tracker, string id)
                {
                        if (pendingRemoval)
                                return;

                        this.tracker = tracker;
                        uniqueID = id;
                        if (Logger.ShouldLog(
                                LogCategory.TrackedGameObjectProxy,
                                LogLevel.Info))
                        {
                                Logger.Log(
                                        $"[TrackedGameObjectProxy] Initialized with UniqueID: {uniqueID}",
                                        LogCategory.TrackedGameObjectProxy,
                                        LogLevel.Info);
                        }
                }

                internal void Disarm(bool isPendingRemoval = false)
                {
                        tracker = null;
                        uniqueID = null;
                        pendingRemoval |= isPendingRemoval;
                }

		private void Awake()
		{
			hideFlags |= HideFlags.HideInInspector;
                        if (Logger.ShouldLog(
                                LogCategory.TrackedGameObjectProxy,
                                LogLevel.Info))
                        {
			        Logger.Log(
                                        $"[TrackedGameObjectProxy] Awake called on '{gameObject.name}'.",
                                        LogCategory.TrackedGameObjectProxy,
                                        LogLevel.Info);
                        }
		}

		private void OnEnable()
		{
                        bool shouldLog = Logger.ShouldLog(
                                LogCategory.TrackedGameObjectProxy,
                                LogLevel.Info);
                        if (shouldLog)
                        {
			        Logger.Log(
                                        $"[TrackedGameObjectProxy] OnEnable called on '{gameObject.name}'.",
                                        LogCategory.TrackedGameObjectProxy,
                                        LogLevel.Info);
                        }
                        if (tracker != null)
                        {
                                tracker.UpdateActiveState(uniqueID, gameObject, gameObject.activeSelf);
                                if (shouldLog)
                                {
                                        Logger.Log(
                                                $"TrackedGameObjectProxy: GameObject with ID '{uniqueID}' enabled.",
                                                LogCategory.TrackedGameObjectProxy,
                                                LogLevel.Info);
                                }
                        }
		}

		private void OnDisable()
		{
                        if (tracker != null)
                        {
                                if (tracker.HasDifferentLiveOwner(uniqueID, gameObject))
                                        return;

                                // Defensive: uniqueID may be empty during certain lifecycle edges
                                try { tracker.UpdateActiveState(uniqueID, gameObject, gameObject.activeSelf); }
                                catch { /* ignore edge-case tracker issues during shutdown/despawn */ }
                                if (Logger.ShouldLog(
                                        LogCategory.TrackedGameObjectProxy,
                                        LogLevel.Info))
                                {
                                        Logger.Log(
                                                $"TrackedGameObjectProxy: GameObject with ID '{uniqueID}' disabled.",
                                                LogCategory.TrackedGameObjectProxy,
                                                LogLevel.Info);
                                }

                                // -- Optionally capture final transform data if applicable and safe
                                if (Application.isPlaying)
                                {
                                        var sm = tracker.SaveManager;
                                        if (sm != null && !sm.IsLoading)
                                        {
                                                var rememberGO = gameObject.GetComponent<RememberGameObject>();
                                                var prefab     = SaveablePrefab.TryGetCachedSaveablePrefab(gameObject, out var cachedSp)
                                                                ? cachedSp
                                                                : gameObject.GetComponent<SaveablePrefab>();

                                                bool shouldCapture = false;

                                                if (prefab != null && prefab.PropertySettings != null && prefab.PropertySettings.RememberDestroyed)
                                                {
                                                        // Pool-driven despawns disable prefabs instead of destroying them; capture immediately.
                                                        shouldCapture = true;
                                                }
                                                else if (rememberGO != null && rememberGO.PropertySettings != null && rememberGO.PropertySettings.RememberDestroyed)
                                                {
                                                        // Only capture for plain RememberGameObjects once they've been marked destroyed.
                                                        shouldCapture = tracker.IsGameObjectDestroyed(uniqueID);
                                                }

                                                if (shouldCapture)
                                                {
                                                        tracker.CaptureDestroyedDataIfPossible(uniqueID);
                                                }
                                        }
                                }
                        }
                }

		private void OnDestroy()
		{
                        if (!Application.isPlaying) return;
                        if (tracker == null) return;
                        var sm = tracker.SaveManager;
                        if (sm == null || sm.IsLoading || sm.IsInSceneTransition) return;
                        if (tracker.IsGameObjectDestroyed(uniqueID)) return;

                        var rememberGO = gameObject.GetComponent<RememberGameObject>();
                        var prefab     = SaveablePrefab.TryGetCachedSaveablePrefab(gameObject, out var cachedSp)
                                                                ? cachedSp
                                                                : gameObject.GetComponent<SaveablePrefab>();
                        bool shouldRegister = (rememberGO != null && rememberGO.PropertySettings != null && rememberGO.PropertySettings.RememberDestroyed) ||
                                                                 (prefab != null && prefab.PropertySettings != null && prefab.PropertySettings.RememberDestroyed);

                        if (shouldRegister)
                        {
                                if (sm.TryRegisterDestroyedGameObjectFromOwner(uniqueID, gameObject))
                                {
                                        if (Logger.ShouldLog(
                                                LogCategory.TrackedGameObjectProxy,
                                                LogLevel.Info))
                                        {
                                                Logger.Log(
                                                        $"[TrackedGameObjectProxy] Registered destruction of '{uniqueID}'.",
                                                        LogCategory.TrackedGameObjectProxy,
                                                        LogLevel.Info);
                                        }
                                }
                        }
                        else
                        {
                                bool shouldLog = Logger.ShouldLog(
                                        LogCategory.TrackedGameObjectProxy,
                                        LogLevel.Info);
                                if (rememberGO == null && prefab == null)
                                {
                                        if (shouldLog)
                                        {
                                                Logger.Log(
                                                        $"[TrackedGameObjectProxy] No Remember settings found on '{gameObject.name}'.",
                                                        LogCategory.TrackedGameObjectProxy,
                                                        LogLevel.Info);
                                        }
                                }
                                else
                                {
                                        if (shouldLog)
                                        {
                                                Logger.Log(
                                                        $"[TrackedGameObjectProxy] SaveDestroyed is disabled for '{uniqueID}'.",
                                                        LogCategory.TrackedGameObjectProxy,
                                                        LogLevel.Info);
                                        }
                                }
                        }
		}
	}
}
#endif
