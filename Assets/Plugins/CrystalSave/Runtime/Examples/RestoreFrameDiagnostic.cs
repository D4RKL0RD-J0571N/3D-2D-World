#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
	/// <summary>
	/// Drop this into a scene to measure whether restored prefabs appear over several
	/// frames, or whether physics/active state changes continue after they are present.
	/// </summary>
	[AddComponentMenu("Crystal Save/Diagnostics/Restore Frame Diagnostic")]
		public sealed class RestoreFrameDiagnostic : MonoBehaviour
		{
			private static RestoreFrameDiagnostic persistentInstance;

			[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
			private static void ResetStaticStateForFastEnterPlayMode()
			{
				persistentInstance = null;
			}

			[Header("Target Filter")]
		[SerializeField] private GameObject targetPrefabAsset;
		[SerializeField] private string targetNameContains = "CubeTower";
		[SerializeField] private string targetPrefabAssetID = "";

		[Header("Watch")]
		[SerializeField] private bool keepDiagnosticAcrossSceneLoads = true;
		[SerializeField] private bool startWatchingOnLoadStarted = true;
		[SerializeField] private int watchFramesAfterLoadCompleted = 120;
		[SerializeField] private int manualWatchFrames = 240;
		[SerializeField] private bool logEveryWatchedFrame = true;
		[SerializeField] private bool logPrefabEvents = false;
		[SerializeField] private float movingSpeedThreshold = 0.01f;

		[Header("Load Button")]
		[SerializeField] private int saveSlotNumber = 1;
		[SerializeField] private bool restoreLastActiveSceneOnLoad = true;
		[SerializeField] private bool loadAsync = false;
		[SerializeField] private bool allowSceneActivation = true;

		[Header("On-Screen Controls")]
		[SerializeField] private bool showOnGui = true;
		[SerializeField] private Vector2 guiPosition = new Vector2(16f, 16f);
		[SerializeField] private float guiWidth = 450f;

		[Header("Logging")]
		[SerializeField] private bool useCrystalSaveLogger = false;

		private SaveManager subscribedManager;
		private bool activeDiagnosticInstance;
		private bool watching;
		private bool loadInProgress;
		private bool loadButtonInProgress;
		private int watchFramesRemaining;
		private int watchStartFrame;
		private float watchStartRealtime;
		private string watchReason = "";
		private string lastSignature = "";
		private FrameStats lastStats;

		private int instantiatedEventsThisFrame;
		private int afterRestoreEventsThisFrame;
		private int instantiatedEventsTotal;
		private int afterRestoreEventsTotal;

		private void Awake()
		{
			if (keepDiagnosticAcrossSceneLoads)
			{
				if (persistentInstance != null && persistentInstance != this)
				{
					Debug.Log($"[RestoreFrameDiagnostic] Destroying duplicate persistent diagnostic '{name}'. The existing persistent diagnostic is '{persistentInstance.name}'.");
					enabled = false;
					Destroy(gameObject);
					return;
				}

				persistentInstance = this;
				DontDestroyOnLoad(gameObject);
			}

			activeDiagnosticInstance = true;
			SceneManager.activeSceneChanged += HandleActiveSceneChanged;
			SceneManager.sceneLoaded += HandleSceneLoaded;
			SceneManager.sceneUnloaded += HandleSceneUnloaded;
			SaveablePrefab.OnPrefabInstantiated += HandlePrefabInstantiated;
			SaveablePrefab.OnAfterRestore += HandleAfterRestore;
			TrySubscribeSaveManager();
		}

		private void Update()
		{
			if (!activeDiagnosticInstance)
				return;

			TrySubscribeSaveManager();
		}

		private void LateUpdate()
		{
			if (!activeDiagnosticInstance || !watching)
				return;

			FrameStats stats = CaptureStats();
			stats.Frame = Time.frameCount;
			stats.ElapsedMs = (Time.realtimeSinceStartup - watchStartRealtime) * 1000f;
			stats.LoadInProgress = loadInProgress || (SaveManager.Instance != null && SaveManager.Instance.IsLoading);
			stats.InstantiatedEventsThisFrame = instantiatedEventsThisFrame;
			stats.AfterRestoreEventsThisFrame = afterRestoreEventsThisFrame;
			stats.InstantiatedEventsTotal = instantiatedEventsTotal;
			stats.AfterRestoreEventsTotal = afterRestoreEventsTotal;

			string signature = stats.BuildSignature();
			bool changed = !string.Equals(signature, lastSignature, StringComparison.Ordinal);
			if (logEveryWatchedFrame || changed)
			{
				Log(stats.ToLogString(watchReason, lastStats));
				lastSignature = signature;
				lastStats = stats;
			}

			instantiatedEventsThisFrame = 0;
			afterRestoreEventsThisFrame = 0;

			if (!stats.LoadInProgress)
			{
				watchFramesRemaining--;
				if (watchFramesRemaining <= 0)
				{
					watching = false;
					Log($"Watch finished after frame {Time.frameCount}. Final: {stats.ToCompactString()}.");
				}
			}
		}

		private void OnDestroy()
		{
			if (!activeDiagnosticInstance)
				return;

			UnsubscribeSaveManager();
			SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
			SceneManager.sceneLoaded -= HandleSceneLoaded;
			SceneManager.sceneUnloaded -= HandleSceneUnloaded;
			SaveablePrefab.OnPrefabInstantiated -= HandlePrefabInstantiated;
			SaveablePrefab.OnAfterRestore -= HandleAfterRestore;

			if (persistentInstance == this)
				persistentInstance = null;
		}

		private void OnGUI()
		{
			if (!activeDiagnosticInstance || !showOnGui)
				return;

			GUILayout.BeginArea(new Rect(guiPosition.x, guiPosition.y, guiWidth, 360f), GUI.skin.box);
			GUILayout.Label("Crystal Save Restore Frame Diagnostic");
			GUILayout.Label($"Target: {TargetLabel()}");
			GUILayout.Label($"Scene: {SceneLabel(SceneManager.GetActiveScene())}");
			GUILayout.Label($"Watching: {watching}  Loading: {(SaveManager.Instance != null && SaveManager.Instance.IsLoading)}  Frames left: {Mathf.Max(0, watchFramesRemaining)}");

			if (lastStats != null)
				GUILayout.Label(lastStats.ToCompactString());

			if (GUILayout.Button("Start Watch"))
				StartWatch("manual", manualWatchFrames);

			if (GUILayout.Button("Stop Watch"))
				StopWatch();

			if (GUILayout.Button("Dump Snapshot"))
				DumpSnapshot();

			if (GUILayout.Button("Log SaveSettings Restore Knobs"))
				LogSaveSettings();

			GUILayout.Space(8f);
			GUILayout.Label($"Save slot: {saveSlotNumber}");

			GUI.enabled = !loadButtonInProgress;
			if (GUILayout.Button("Load Slot And Watch"))
				_ = LoadSlotAndWatchAsync();
			GUI.enabled = true;

			GUILayout.EndArea();
		}

		public void StartWatch(string reason)
		{
			StartWatch(reason, manualWatchFrames);
		}

		public void StopWatch()
		{
			if (!watching)
				return;

			watching = false;
			Log($"Watch stopped manually at frame {Time.frameCount}.");
		}

		public void DumpSnapshot()
		{
			FrameStats stats = CaptureStats();
			stats.Frame = Time.frameCount;
			stats.ElapsedMs = watching ? (Time.realtimeSinceStartup - watchStartRealtime) * 1000f : 0f;
			stats.LoadInProgress = SaveManager.Instance != null && SaveManager.Instance.IsLoading;
			stats.InstantiatedEventsTotal = instantiatedEventsTotal;
			stats.AfterRestoreEventsTotal = afterRestoreEventsTotal;
			Log(stats.ToLogString("snapshot", lastStats));
		}

		public void LogSaveSettings()
		{
			SaveSettings settings = SaveManager.Instance != null ? SaveManager.Instance.SaveSettings : AssetProvider.Load<SaveSettings>("SaveSettings");
			if (settings == null)
			{
				Log("SaveSettings unavailable.", LogLevel.Warning);
				return;
			}

			Log(
				$"SaveSettings restore knobs: usePrefabPooling={settings.usePrefabPooling}, defaultPrefabPoolSize={settings.defaultPrefabPoolSize}, " +
				$"prefabInstantiationBatchSize={settings.prefabInstantiationBatchSize}, groupInstantiationByScene={settings.groupInstantiationByScene}, " +
				$"componentApplyBatchSize={settings.componentApplyBatchSize}, activeStateApplyBatchSize={settings.activeStateApplyBatchSize}, " +
				$"syncTransformsAfterPrefabLoad={settings.syncTransformsAfterPrefabLoad}, enablePooledPrefabBatching={settings.enablePooledPrefabBatching}, " +
				$"pooledPrefabSpawnBatchSize={settings.pooledPrefabSpawnBatchSize}, spawnPooledPrefabsInScene={settings.spawnPooledPrefabsInScene}.");
		}

		private async Task LoadSlotAndWatchAsync()
		{
			if (loadButtonInProgress)
				return;

			SaveManager manager = SaveManager.Instance;
			if (manager == null)
			{
				Log("Load skipped: SaveManager.Instance is null.", LogLevel.Warning);
				return;
			}

			loadButtonInProgress = true;
			try
			{
				StartWatch("load button", watchFramesAfterLoadCompleted);
				await manager.WaitForSaveSlotsAsync();
				Log($"Loading slot {saveSlotNumber}. restoreLastActiveScene={restoreLastActiveSceneOnLoad}, loadAsync={loadAsync}, allowSceneActivation={allowSceneActivation}.");
				LoadResult result = await manager.LoadSaveSlotAsync(saveSlotNumber, restoreLastActiveSceneOnLoad, loadAsync, allowSceneActivation);
				Log($"Load slot {saveSlotNumber} finished. success={result.Success}, error='{Empty(result.ErrorMessage)}'.",
					result.Success ? LogLevel.Info : LogLevel.Warning);
			}
			finally
			{
				loadButtonInProgress = false;
			}
		}

		private void StartWatch(string reason, int framesAfterLoad)
		{
			watching = true;
			watchReason = string.IsNullOrWhiteSpace(reason) ? "watch" : reason;
			watchFramesRemaining = Mathf.Max(1, framesAfterLoad);
			watchStartFrame = Time.frameCount;
			watchStartRealtime = Time.realtimeSinceStartup;
			lastSignature = "";
			lastStats = null;
			instantiatedEventsThisFrame = 0;
			afterRestoreEventsThisFrame = 0;
			instantiatedEventsTotal = 0;
			afterRestoreEventsTotal = 0;
			Log($"Watch started. reason='{watchReason}', frame={watchStartFrame}, target={TargetLabel()}, postLoadFrames={watchFramesRemaining}.");
			LogSaveSettings();
		}

		private void TrySubscribeSaveManager()
		{
			SaveManager manager = SaveManager.Instance;
			if (manager == subscribedManager)
				return;

			UnsubscribeSaveManager();
			if (manager == null)
				return;

			subscribedManager = manager;
			subscribedManager.OnLoadStarted += HandleLoadStarted;
			subscribedManager.OnLoadCompleted += HandleLoadCompleted;
			subscribedManager.OnLoadFailed += HandleLoadFailed;
			subscribedManager.OnSceneLoadStarted += HandleSaveManagerSceneLoadStarted;
			subscribedManager.OnSceneLoadCompleted += HandleSaveManagerSceneLoadCompleted;
		}

		private void UnsubscribeSaveManager()
		{
			if (subscribedManager == null)
				return;

			subscribedManager.OnLoadStarted -= HandleLoadStarted;
			subscribedManager.OnLoadCompleted -= HandleLoadCompleted;
			subscribedManager.OnLoadFailed -= HandleLoadFailed;
			subscribedManager.OnSceneLoadStarted -= HandleSaveManagerSceneLoadStarted;
			subscribedManager.OnSceneLoadCompleted -= HandleSaveManagerSceneLoadCompleted;
			subscribedManager = null;
		}

		private void HandleLoadStarted(object sender, SaveManagerEventArgs args)
		{
			loadInProgress = true;
			if (startWatchingOnLoadStarted)
				StartWatch($"load started slot {SlotLabel(args?.Slot)}", watchFramesAfterLoadCompleted);

			Log($"OnLoadStarted slot={SlotLabel(args?.Slot)} frame={Time.frameCount} activeScene='{SceneLabel(SceneManager.GetActiveScene())}'.");
		}

		private void HandleLoadCompleted(object sender, SaveLoadEventArgs args)
		{
			loadInProgress = false;
			if (watching)
				watchFramesRemaining = Mathf.Max(watchFramesRemaining, watchFramesAfterLoadCompleted);

			Log($"OnLoadCompleted slot={SlotLabel(args?.Slot)} success={args != null && args.Success} frame={Time.frameCount} message='{Empty(args?.Message)}'.");
		}

		private void HandleLoadFailed(object sender, OperationFailedEventArgs args)
		{
			loadInProgress = false;
			if (watching)
				watchFramesRemaining = Mathf.Max(watchFramesRemaining, watchFramesAfterLoadCompleted);

			Log($"OnLoadFailed slot={SlotLabel(args?.Slot)} operation='{Empty(args?.OperationName)}' frame={Time.frameCount} error='{Empty(args?.ErrorMessage)}'.", LogLevel.Warning);
		}

		private void HandleSaveManagerSceneLoadStarted(string sceneName)
		{
			Log($"SaveManager scene load started '{Empty(sceneName)}' frame={Time.frameCount}.");
		}

		private void HandleSaveManagerSceneLoadCompleted(string sceneName)
		{
			Log($"SaveManager scene load completed '{Empty(sceneName)}' frame={Time.frameCount}.");
		}

		private void HandleActiveSceneChanged(Scene previous, Scene next)
		{
			Log($"Scene event: active scene changed '{SceneLabel(previous)}' -> '{SceneLabel(next)}' frame={Time.frameCount}.");
		}

		private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			Log($"Scene event: loaded '{SceneLabel(scene)}' mode={mode} frame={Time.frameCount}.");
		}

		private void HandleSceneUnloaded(Scene scene)
		{
			Log($"Scene event: unloaded '{SceneLabel(scene)}' frame={Time.frameCount}.");
		}

		private void HandlePrefabInstantiated(SaveablePrefab prefab)
		{
			if (!MatchesTarget(prefab))
				return;

			instantiatedEventsThisFrame++;
			instantiatedEventsTotal++;
			if (logPrefabEvents)
				LogPrefabEvent("OnPrefabInstantiated", prefab);
		}

		private void HandleAfterRestore(SaveablePrefab prefab)
		{
			if (!MatchesTarget(prefab))
				return;

			afterRestoreEventsThisFrame++;
			afterRestoreEventsTotal++;
			if (logPrefabEvents)
				LogPrefabEvent("OnAfterRestore", prefab);
		}

		private void LogPrefabEvent(string eventName, SaveablePrefab prefab)
		{
			if (!prefab)
				return;

			Rigidbody rb = prefab.GetComponent<Rigidbody>();
			string rbInfo = rb ? $"rbKinematic={rb.isKinematic}, rbSleeping={rb.IsSleeping()}, rbSpeed={GetRigidbodySpeed(rb):F3}" : "rb=<none>";
			Log(
				$"{eventName}: frame={Time.frameCount}, name='{prefab.name}', uid='{Empty(prefab.UniqueID)}', prefabId='{Empty(prefab.PrefabAssetID)}', " +
				$"activeSelf={prefab.gameObject.activeSelf}, activeInHierarchy={prefab.gameObject.activeInHierarchy}, scene='{SceneLabel(prefab.gameObject.scene)}', " +
				$"homeScene='{Empty(prefab.HomeScene)}', tracked={prefab.IsTrackedBySaveManager}, registerFlag={prefab.RegisterWithSaveSystem}, {rbInfo}.");
		}

		private FrameStats CaptureStats()
		{
			FrameStats stats = new FrameStats();
			stats.ActiveScene = SceneLabel(SceneManager.GetActiveScene());
			stats.TargetLabel = TargetLabel();

			SaveablePrefab[] prefabs = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();

			for (int i = 0; i < prefabs.Length; i++)
			{
				SaveablePrefab prefab = prefabs[i];
				if (!MatchesTarget(prefab))
					continue;

				GameObject go = prefab.gameObject;
				stats.Total++;
				if (go.activeSelf)
					stats.ActiveSelf++;
				if (go.activeInHierarchy)
					stats.ActiveInHierarchy++;
				if (!string.IsNullOrEmpty(prefab.UniqueID))
					stats.WithUniqueID++;
				if (prefab.RegisterWithSaveSystem)
					stats.RegisterFlag++;
				if (prefab.IsTrackedBySaveManager)
					stats.TrackedBySaveManager++;
				if (prefab.IsAddedAtRuntime)
					stats.RuntimeAdded++;
				if (prefab.IsLoading)
					stats.PrefabLoadingFlag++;
				if (prefab.RememberHomeScene)
					stats.RememberHomeScene++;

				stats.AddScene(SceneLabel(go.scene));
				stats.AddHomeScene(prefab.HomeScene);
				stats.Encapsulate(go.transform.position);

				Rigidbody rb = prefab.GetComponent<Rigidbody>();
				if (!rb)
					continue;

				stats.Rigidbodies++;
				if (rb.isKinematic)
				{
					stats.KinematicBodies++;
				}
				else
				{
					stats.DynamicBodies++;
					if (rb.IsSleeping())
						stats.SleepingDynamicBodies++;
					else
						stats.AwakeDynamicBodies++;
				}

				float speed = GetRigidbodySpeed(rb);
				stats.SpeedSum += speed;
				if (speed > movingSpeedThreshold)
					stats.MovingBodies++;
			}

			return stats;
		}

		private bool MatchesTarget(SaveablePrefab prefab)
		{
			if (!prefab)
				return false;

			GameObject go = prefab.gameObject;
			if (!go || !go.scene.IsValid())
				return false;

			string idFilter = ResolveTargetPrefabAssetID();
			if (!string.IsNullOrWhiteSpace(idFilter))
				return string.Equals(prefab.PrefabAssetID, idFilter, StringComparison.Ordinal);

			if (string.IsNullOrWhiteSpace(targetNameContains))
				return true;

			return prefab.name.IndexOf(targetNameContains, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private string ResolveTargetPrefabAssetID()
		{
			if (!string.IsNullOrWhiteSpace(targetPrefabAssetID))
				return targetPrefabAssetID.Trim();

			if (!targetPrefabAsset)
				return "";

			SaveablePrefab saveable = targetPrefabAsset.GetComponent<SaveablePrefab>();
			return saveable != null ? saveable.PrefabAssetID : "";
		}

		private string TargetLabel()
		{
			string id = ResolveTargetPrefabAssetID();
			if (!string.IsNullOrEmpty(id))
				return $"prefabId='{id}'";

			return string.IsNullOrWhiteSpace(targetNameContains)
				? "all SaveablePrefab instances"
				: $"name contains '{targetNameContains}'";
		}

		private static float GetRigidbodySpeed(Rigidbody rb)
		{
			if (!rb)
				return 0f;

#if UNITY_6000_0_OR_NEWER
			return rb.linearVelocity.magnitude;
#else
			return rb.velocity.magnitude;
#endif
		}

		private static string SceneLabel(Scene scene)
		{
			return scene.IsValid() ? scene.name : "<invalid>";
		}

		private static string SlotLabel(SaveSlot slot)
		{
			return slot != null ? slot.SlotNumber.ToString() : "<null>";
		}

		private static string Empty(string value)
		{
			return string.IsNullOrEmpty(value) ? "<empty>" : value;
		}

		private void Log(string message, LogLevel level = LogLevel.Info)
		{
			string formatted = $"[RestoreFrameDiagnostic] {message}";
			if (useCrystalSaveLogger)
			{
				Logger.Log(formatted, level);
				return;
			}

			switch (level)
			{
				case LogLevel.Error:
					Debug.LogError(formatted, this);
					break;
				case LogLevel.Warning:
					Debug.LogWarning(formatted, this);
					break;
				default:
					Debug.Log(formatted, this);
					break;
			}
		}

		private sealed class FrameStats
		{
			public int Frame;
			public float ElapsedMs;
			public bool LoadInProgress;
			public string ActiveScene = "";
			public string TargetLabel = "";
			public int Total;
			public int ActiveSelf;
			public int ActiveInHierarchy;
			public int WithUniqueID;
			public int RegisterFlag;
			public int TrackedBySaveManager;
			public int RuntimeAdded;
			public int PrefabLoadingFlag;
			public int RememberHomeScene;
			public int Rigidbodies;
			public int KinematicBodies;
			public int DynamicBodies;
			public int AwakeDynamicBodies;
			public int SleepingDynamicBodies;
			public int MovingBodies;
			public float SpeedSum;
			public int InstantiatedEventsThisFrame;
			public int AfterRestoreEventsThisFrame;
			public int InstantiatedEventsTotal;
			public int AfterRestoreEventsTotal;

			private readonly Dictionary<string, int> sceneCounts = new Dictionary<string, int>(StringComparer.Ordinal);
			private readonly Dictionary<string, int> homeSceneCounts = new Dictionary<string, int>(StringComparer.Ordinal);
			private bool hasBounds;
			private Vector3 min;
			private Vector3 max;

			public void AddScene(string sceneName)
			{
				AddCount(sceneCounts, string.IsNullOrEmpty(sceneName) ? "<empty>" : sceneName);
			}

			public void AddHomeScene(string sceneName)
			{
				AddCount(homeSceneCounts, string.IsNullOrEmpty(sceneName) ? "<empty>" : sceneName);
			}

			public void Encapsulate(Vector3 position)
			{
				if (!hasBounds)
				{
					min = position;
					max = position;
					hasBounds = true;
					return;
				}

				min = Vector3.Min(min, position);
				max = Vector3.Max(max, position);
			}

			public string BuildSignature()
			{
				var builder = new StringBuilder(192);
				AppendSignatureValue(builder, Total);
				AppendSignatureValue(builder, ActiveSelf);
				AppendSignatureValue(builder, ActiveInHierarchy);
				AppendSignatureValue(builder, WithUniqueID);
				AppendSignatureValue(builder, RegisterFlag);
				AppendSignatureValue(builder, TrackedBySaveManager);
				AppendSignatureValue(builder, RuntimeAdded);
				AppendSignatureValue(builder, PrefabLoadingFlag);
				AppendSignatureValue(builder, RememberHomeScene);
				AppendSignatureValue(builder, Rigidbodies);
				AppendSignatureValue(builder, KinematicBodies);
				AppendSignatureValue(builder, DynamicBodies);
				AppendSignatureValue(builder, AwakeDynamicBodies);
				AppendSignatureValue(builder, SleepingDynamicBodies);
				AppendSignatureValue(builder, MovingBodies);
				AppendSignatureValue(builder, Mathf.RoundToInt(SpeedSum * 1000f));
				AppendSignatureValue(builder, InstantiatedEventsTotal);
				AppendSignatureValue(builder, AfterRestoreEventsTotal);
				AppendSignatureValue(builder, Distribution(sceneCounts));
				AppendSignatureValue(builder, Distribution(homeSceneCounts));
				AppendSignatureValue(builder, BoundsString());
				return builder.ToString();
			}

			public string ToCompactString()
			{
				var builder = new StringBuilder(128);
				builder.Append("total=").Append(Total)
					.Append(", active=").Append(ActiveInHierarchy).Append('/').Append(ActiveSelf)
					.Append(", ids=").Append(WithUniqueID)
					.Append(", tracked=").Append(TrackedBySaveManager)
					.Append(", rb=").Append(Rigidbodies)
					.Append(", dynAwake=").Append(AwakeDynamicBodies)
					.Append(", dynSleep=").Append(SleepingDynamicBodies)
					.Append(", kin=").Append(KinematicBodies)
					.Append(", moving=").Append(MovingBodies);
				return builder.ToString();
			}

			public string ToLogString(string reason, FrameStats previous)
			{
				string delta = previous == null
					? "delta=<first>"
					: BuildDeltaString(previous);

				var builder = new StringBuilder(512);
				builder.Append("Frame sample reason='").Append(reason)
					.Append("' frame=").Append(Frame)
					.Append(", elapsedMs=").Append(ElapsedMs.ToString("F1"))
					.Append(", loadInProgress=").Append(LoadInProgress)
					.Append(", activeScene='").Append(ActiveScene)
					.Append("', target=").Append(TargetLabel)
					.Append(": ").Append(ToCompactString())
					.Append(", registerFlag=").Append(RegisterFlag)
					.Append(", runtimeAdded=").Append(RuntimeAdded)
					.Append(", loadingFlag=").Append(PrefabLoadingFlag)
					.Append(", rememberHomeScene=").Append(RememberHomeScene)
					.Append(", instantiatedEventsFrame=").Append(InstantiatedEventsThisFrame)
					.Append(", afterRestoreEventsFrame=").Append(AfterRestoreEventsThisFrame)
					.Append(", instantiatedEventsTotal=").Append(InstantiatedEventsTotal)
					.Append(", afterRestoreEventsTotal=").Append(AfterRestoreEventsTotal)
					.Append(", avgSpeed=").Append(AverageSpeed().ToString("F3"))
					.Append(", scenes=").Append(Distribution(sceneCounts))
					.Append(", homeScenes=").Append(Distribution(homeSceneCounts))
					.Append(", bounds=").Append(BoundsString())
					.Append(", ").Append(delta).Append('.');
				return builder.ToString();
			}

			private float AverageSpeed()
			{
				return Rigidbodies > 0 ? SpeedSum / Rigidbodies : 0f;
			}

			private static void AddCount(Dictionary<string, int> dictionary, string key)
			{
				if (dictionary.TryGetValue(key, out int count))
					dictionary[key] = count + 1;
				else
					dictionary.Add(key, 1);
			}

			private static string Distribution(Dictionary<string, int> dictionary)
			{
				if (dictionary.Count == 0)
					return "{}";

				List<string> keys = new List<string>(dictionary.Keys);
				keys.Sort(StringComparer.Ordinal);

				StringBuilder builder = new StringBuilder();
				builder.Append('{');
				for (int i = 0; i < keys.Count; i++)
				{
					if (i > 0)
						builder.Append(", ");

					string key = keys[i];
					builder.Append(key);
					builder.Append(':');
					builder.Append(dictionary[key]);
				}

				builder.Append('}');
				return builder.ToString();
			}

			private string BoundsString()
			{
				if (!hasBounds)
					return "<none>";

				return $"min={FormatVector(min)}, max={FormatVector(max)}";
			}

			private static string FormatVector(Vector3 value)
			{
				var builder = new StringBuilder(32);
				builder.Append('(')
					.Append(value.x.ToString("F2"))
					.Append(',')
					.Append(value.y.ToString("F2"))
					.Append(',')
					.Append(value.z.ToString("F2"))
					.Append(')');
				return builder.ToString();
			}

			private static void AppendSignatureValue(StringBuilder builder, string value)
			{
				if (builder.Length > 0)
					builder.Append('|');

				builder.Append(value);
			}

			private static void AppendSignatureValue(StringBuilder builder, int value)
			{
				if (builder.Length > 0)
					builder.Append('|');

				builder.Append(value);
			}

			private static void AppendSignatureValue(StringBuilder builder, bool value)
			{
				if (builder.Length > 0)
					builder.Append('|');

				builder.Append(value);
			}

			private string BuildDeltaString(FrameStats previous)
			{
				var builder = new StringBuilder(128);
				builder.Append("delta total=").Append(Signed(Total - previous.Total))
					.Append(", active=").Append(Signed(ActiveInHierarchy - previous.ActiveInHierarchy))
					.Append(", ids=").Append(Signed(WithUniqueID - previous.WithUniqueID))
					.Append(", tracked=").Append(Signed(TrackedBySaveManager - previous.TrackedBySaveManager))
					.Append(", moving=").Append(Signed(MovingBodies - previous.MovingBodies));
				return builder.ToString();
			}

			private static string Signed(int value)
			{
				return value >= 0 ? "+" + value : value.ToString();
			}
		}
	}
}
#endif
