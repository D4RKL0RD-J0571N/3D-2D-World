#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime.Examples
{
	/// <summary>
	/// Drop this into a scene to inspect how prefab pooling interacts with
	/// RememberHomeScene and HomeSceneMode.InstantiationScene.
	/// </summary>
	[AddComponentMenu("Crystal Save/Diagnostics/Prefab Pooling Home Scene Diagnostic")]
		public sealed class PrefabPoolingHomeSceneDiagnostic : MonoBehaviour
		{
			private static PrefabPoolingHomeSceneDiagnostic persistentInstance;

			[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
			private static void ResetStaticStateForFastEnterPlayMode()
			{
				persistentInstance = null;
			}

			private enum FactoryPoolingOverride
		{
			UseSaveSettings,
			ForcePooling,
			ForceInstantiate
		}

		[Header("Prefab")]
		[SerializeField] private GameObject prefabAsset;
		[SerializeField] private bool registerWithSaveSystem = true;
		[SerializeField] private FactoryPoolingOverride factoryPoolingOverride = FactoryPoolingOverride.UseSaveSettings;
		[SerializeField] private bool useSceneNameOverload = false;
		[SerializeField] private string explicitSceneName = "";

		[Header("Spawn")]
		[SerializeField] private Vector3 spawnPosition = Vector3.zero;
		[SerializeField] private Vector3 spawnEulerAngles = Vector3.zero;
		[SerializeField] private Vector3 spawnOffset = new Vector3(1.5f, 0f, 0f);

		[Header("Runtime SaveSettings Overrides")]
		[SerializeField] private bool applyRuntimeSaveSettingsOverrides = true;
		[SerializeField] private LogLevel runtimeLogLevel = LogLevel.Info;
		[SerializeField] private bool runtimeUsePrefabPooling = true;
		[SerializeField] private bool runtimeSpawnPooledPrefabsInScene = false;
		[SerializeField] private int runtimeDefaultPrefabPoolSize = 4;

		[Header("Runtime LoggerConfig Overrides")]
		[SerializeField] private bool enableRelevantLoggerConfigCategories = true;

		[Header("Optional Prefab Overrides")]
		[SerializeField] private bool applyPrefabRuntimeOverrides = false;
		[SerializeField] private bool runtimeRememberHomeScene = true;
		[SerializeField] private SaveablePrefab.HomeSceneMode runtimeHomeSceneMode = SaveablePrefab.HomeSceneMode.InstantiationScene;
		[SerializeField] private bool runtimeDisablePooling = false;

		[Header("Save / Load Buttons")]
		[SerializeField] private int saveSlotNumber = 1;
		[SerializeField] private bool restoreLastActiveSceneOnLoad = true;

		[Header("Scene Jump Buttons")]
		[SerializeField] private bool keepDiagnosticAcrossSceneLoads = true;
		[SerializeField] private string sceneAName = "SampleScene";
		[SerializeField] private string sceneBName = "SampleScene 1";
		[SerializeField] private bool useCrystalSaveSnapshotSceneJump = true;
		[SerializeField] private bool sceneJumpLoadAsync = false;
		[SerializeField] private bool sceneJumpAdditive = false;

		[Header("On-Screen Controls")]
		[SerializeField] private bool showOnGui = true;
		[SerializeField] private Vector2 guiPosition = new Vector2(16f, 16f);
		[SerializeField] private float guiWidth = 390f;

		private readonly List<SaveablePrefab> spawned = new();
		private SaveablePrefab lastSpawned;
		private int spawnCount;

		private SaveSettings settings;
		private bool capturedSettings;
		private LogLevel originalLogLevel;
		private bool originalUsePrefabPooling;
		private bool originalSpawnPooledPrefabsInScene;
		private int originalDefaultPrefabPoolSize;

		private SaveablePrefab prefabSaveable;
		private bool capturedPrefabSettings;
		private bool originalRememberHomeScene;
		private SaveablePrefab.HomeSceneMode originalHomeSceneMode;
		private bool originalDisablePooling;

		private LoggerConfig loggerConfig;
		private bool capturedLoggerConfig;
		private bool originalLogSaveablePrefab;
		private bool originalLogPrefabManager;
		private bool originalLogSaveManagerExtensions;
		private bool activeDiagnosticInstance;
		private bool sceneJumpInProgress;

		private void Awake()
		{
			if (keepDiagnosticAcrossSceneLoads)
			{
				if (persistentInstance != null && persistentInstance != this)
				{
					Debug.Log($"[PrefabPoolingHomeSceneDiagnostic] Destroying duplicate persistent diagnostic '{name}'. The existing persistent diagnostic is '{persistentInstance.name}'.");
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
			SaveablePrefab.OnAfterRestore += HandleAfterRestore;
		}

		private IEnumerator Start()
		{
			if (!activeDiagnosticInstance)
				yield break;

			yield return null;
			ApplyRuntimeOverrides();
			ApplyPrefabOverridesIfRequested();
			LogConfiguration();
		}

		private void OnDestroy()
		{
			if (!activeDiagnosticInstance)
				return;

			SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
			SceneManager.sceneLoaded -= HandleSceneLoaded;
			SceneManager.sceneUnloaded -= HandleSceneUnloaded;
			SaveablePrefab.OnAfterRestore -= HandleAfterRestore;

			RestorePrefabOverrides();
			RestoreLoggerConfigOverrides();
			RestoreRuntimeOverrides();

			if (persistentInstance == this)
				persistentInstance = null;
		}

		private void OnGUI()
		{
			if (!activeDiagnosticInstance)
				return;

			if (!showOnGui)
				return;

			GUILayout.BeginArea(new Rect(guiPosition.x, guiPosition.y, guiWidth, 650f), GUI.skin.box);
			GUILayout.Label("Crystal Save Pool/Home Scene Diagnostic");

			if (GUILayout.Button("Apply Runtime Overrides"))
				ApplyRuntimeOverrides();

			if (GUILayout.Button("Log Configuration"))
				LogConfiguration();

			if (GUILayout.Button("Spawn Probe"))
				SpawnProbe();

			if (GUILayout.Button("Dump Last Probe"))
				LogPrefabState("Dump last", lastSpawned);

			if (GUILayout.Button("Build Save Data For Last Probe"))
				BuildSaveDataForLast();

			if (GUILayout.Button("Despawn Last Via Factory"))
				DespawnLast();

			if (GUILayout.Button("Dump All SaveablePrefabs"))
				DumpAllSaveablePrefabs();

			GUILayout.Space(8f);
			GUILayout.Label($"Save slot: {saveSlotNumber}");

			if (GUILayout.Button("Save Slot"))
				_ = SaveSlotAsync();

			if (GUILayout.Button("Load Slot"))
				_ = LoadSlotAsync();

			GUILayout.Space(8f);
			GUILayout.Label($"Scene jump: {SceneLabel(SceneManager.GetActiveScene())}");

			if (GUILayout.Button($"Jump To {sceneAName}"))
				_ = JumpToSceneAsync(sceneAName);

			if (GUILayout.Button($"Jump To {sceneBName}"))
				_ = JumpToSceneAsync(sceneBName);

			if (GUILayout.Button("Jump To Other Scene"))
				_ = JumpToSceneAsync(GetOtherSceneName());

			GUILayout.EndArea();
		}

		public void SpawnProbe()
		{
			ApplyRuntimeOverrides();
			ApplyPrefabOverridesIfRequested();

			if (!prefabAsset)
			{
				Log("SpawnProbe failed: prefabAsset is not assigned.", LogLevel.Warning);
				return;
			}

			prefabSaveable = prefabAsset.GetComponent<SaveablePrefab>();
			if (!prefabSaveable)
			{
				Log($"SpawnProbe failed: prefab asset '{prefabAsset.name}' has no SaveablePrefab component.", LogLevel.Warning);
				return;
			}

			Vector3 position = spawnPosition + spawnOffset * spawnCount;
			Quaternion rotation = Quaternion.Euler(spawnEulerAngles);
			bool? poolingOverride = GetPoolingOverride();

			string sceneName = ResolveSceneName();
			SaveablePrefab spawnedPrefab = useSceneNameOverload
				? SaveablePrefabFactory.Instantiate(prefabAsset, position, rotation, sceneName, null, registerWithSaveSystem, poolingOverride)
				: SaveablePrefabFactory.Instantiate(prefabAsset, position, rotation, null, registerWithSaveSystem, poolingOverride);

			if (!spawnedPrefab)
			{
				Log("SpawnProbe failed: SaveablePrefabFactory returned null.", LogLevel.Warning);
				return;
			}

			spawnCount++;
			lastSpawned = spawnedPrefab;
			spawned.Add(spawnedPrefab);

			LogPrefabState("Spawn immediate", spawnedPrefab);
			StartCoroutine(LogAfterFrame(spawnedPrefab, "Spawn after one frame"));
			StartCoroutine(LogAfterFixedUpdate(spawnedPrefab, "Spawn after fixed update"));
		}

		public void DespawnLast()
		{
			if (!lastSpawned)
			{
				Log("DespawnLast skipped: no live lastSpawned reference.", LogLevel.Warning);
				return;
			}

			SaveablePrefab target = lastSpawned;
			LogPrefabState("Before factory despawn", target);
			SaveablePrefabFactory.Destroy(target);
			StartCoroutine(LogAfterFrame(target, "After factory despawn"));
		}

		public void BuildSaveDataForLast()
		{
			if (!lastSpawned)
			{
				Log("BuildSaveDataForLast skipped: no live lastSpawned reference.", LogLevel.Warning);
				return;
			}

			SaveablePrefabData data = lastSpawned.TryBuildSaveData();
			if (data == null)
			{
				LogPrefabState("BuildSaveData returned null", lastSpawned);
				return;
			}

			Log(
				$"BuildSaveData result for '{lastSpawned.name}': instanceId='{Empty(data.InstanceID)}', prefabId='{Empty(data.PrefabID)}', " +
				$"homeScene='{Empty(data.HomeScene)}', disablePooling={data.DisablePooling}, hasRigidbody={data.HasRigidbody}, " +
				$"position={data.Position}, objectScene='{SceneLabel(lastSpawned.gameObject.scene)}', componentHomeScene='{Empty(lastSpawned.HomeScene)}'.");
		}

		public void DumpAllSaveablePrefabs()
		{
			SaveablePrefab[] prefabs = UnityObjectSearch.FindIncludingInactive<SaveablePrefab>();

			Log($"DumpAllSaveablePrefabs: found {prefabs.Length} SaveablePrefab components.");
			foreach (SaveablePrefab prefab in prefabs)
			{
				LogPrefabState("Dump all", prefab);
			}
		}

		private async Task SaveSlotAsync()
		{
			SaveManager manager = SaveManager.Instance;
			if (manager == null)
			{
				Log("SaveSlot failed: SaveManager.Instance is null.", LogLevel.Warning);
				return;
			}

			await manager.WaitForSaveSlotsAsync();
			Log($"Saving slot {saveSlotNumber}.");
			await manager.SaveImmediateAsync(saveSlotNumber);
			Log($"Save slot {saveSlotNumber} completed.");
		}

		private async Task LoadSlotAsync()
		{
			SaveManager manager = SaveManager.Instance;
			if (manager == null)
			{
				Log("LoadSlot failed: SaveManager.Instance is null.", LogLevel.Warning);
				return;
			}

			await manager.WaitForSaveSlotsAsync();
			Log($"Loading slot {saveSlotNumber}. restoreLastActiveScene={restoreLastActiveSceneOnLoad}.");
			LoadResult result = await manager.LoadSaveSlotAsync(saveSlotNumber, restoreLastActiveSceneOnLoad, false);
			Log($"Load slot {saveSlotNumber} completed. success={result.Success}, error='{Empty(result.ErrorMessage)}'.",
				result.Success ? LogLevel.Info : LogLevel.Warning);
		}

		private async Task JumpToSceneAsync(string sceneName)
		{
			if (sceneJumpInProgress)
			{
				Log($"Scene jump skipped: a scene jump is already in progress.", LogLevel.Warning);
				return;
			}

			if (string.IsNullOrWhiteSpace(sceneName))
			{
				Log("Scene jump skipped: target scene name is empty.", LogLevel.Warning);
				return;
			}

			sceneJumpInProgress = true;
			try
			{
				ApplyRuntimeOverrides();
				Log($"Jumping to scene '{sceneName}'. useCrystalSaveSnapshotSceneJump={useCrystalSaveSnapshotSceneJump}, additive={sceneJumpAdditive}, async={sceneJumpLoadAsync}.");
				LogPrefabState("Before scene jump last", lastSpawned);

				if (useCrystalSaveSnapshotSceneJump)
				{
					SaveManager manager = SaveManager.Instance;
					if (manager == null)
					{
						Log("Scene jump failed: SaveManager.Instance is null.", LogLevel.Warning);
						return;
					}

					await SaveManagerExtensions.LoadSceneAfterSnapshotAndPopulatePendingPrefabsAsync(
						manager,
						sceneName,
						sceneJumpAdditive,
						sceneJumpLoadAsync);
				}
				else
				{
					LoadSceneMode mode = sceneJumpAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single;
					if (sceneJumpLoadAsync)
					{
						AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, mode);
						while (operation != null && !operation.isDone)
							await Task.Yield();
					}
					else
					{
						SceneManager.LoadScene(sceneName, mode);
					}
				}

				await Task.Yield();
				Log($"Scene jump to '{sceneName}' requested/completed. activeScene='{SceneLabel(SceneManager.GetActiveScene())}'.");
				LogPrefabState("After scene jump last", lastSpawned);
			}
			finally
			{
				sceneJumpInProgress = false;
			}
		}

		private IEnumerator LogAfterFrame(SaveablePrefab prefab, string label)
		{
			yield return null;
			LogPrefabState(label, prefab);
		}

		private IEnumerator LogAfterFixedUpdate(SaveablePrefab prefab, string label)
		{
			yield return new WaitForFixedUpdate();
			LogPrefabState(label, prefab);
		}

		private void ApplyRuntimeOverrides()
		{
			settings = SaveManager.Instance != null ? SaveManager.Instance.SaveSettings : settings;
			if (!applyRuntimeSaveSettingsOverrides || settings == null)
				return;

			if (!capturedSettings)
			{
				originalLogLevel = settings.logLevel;
				originalUsePrefabPooling = settings.usePrefabPooling;
				originalSpawnPooledPrefabsInScene = settings.spawnPooledPrefabsInScene;
				originalDefaultPrefabPoolSize = settings.defaultPrefabPoolSize;
				capturedSettings = true;
			}

			settings.logLevel = runtimeLogLevel;
			settings.usePrefabPooling = runtimeUsePrefabPooling;
			settings.spawnPooledPrefabsInScene = runtimeSpawnPooledPrefabsInScene;
			settings.defaultPrefabPoolSize = Mathf.Max(0, runtimeDefaultPrefabPoolSize);
			Logger.LogThreshold = runtimeLogLevel;
			ApplyLoggerConfigOverrides();

			Log(
				$"Applied runtime SaveSettings overrides: logLevel={settings.logLevel}, usePrefabPooling={settings.usePrefabPooling}, " +
				$"spawnPooledPrefabsInScene={settings.spawnPooledPrefabsInScene}, defaultPrefabPoolSize={settings.defaultPrefabPoolSize}.");
		}

		private void RestoreRuntimeOverrides()
		{
			if (!capturedSettings || settings == null)
				return;

			settings.logLevel = originalLogLevel;
			settings.usePrefabPooling = originalUsePrefabPooling;
			settings.spawnPooledPrefabsInScene = originalSpawnPooledPrefabsInScene;
			settings.defaultPrefabPoolSize = originalDefaultPrefabPoolSize;
			Logger.LogThreshold = originalLogLevel;
		}

		private void ApplyLoggerConfigOverrides()
		{
			if (!enableRelevantLoggerConfigCategories)
				return;

			loggerConfig ??= AssetProvider.Load<LoggerConfig>("LoggerConfig");
			if (loggerConfig == null)
				return;

			if (!capturedLoggerConfig)
			{
				originalLogSaveablePrefab = loggerConfig.saveablePrefab;
				originalLogPrefabManager = loggerConfig.prefabManager;
				originalLogSaveManagerExtensions = loggerConfig.saveManagerExtensions;
				capturedLoggerConfig = true;
			}

			loggerConfig.saveablePrefab = true;
			loggerConfig.prefabManager = true;
			loggerConfig.saveManagerExtensions = true;
			Logger.RefreshConfig();
		}

		private void RestoreLoggerConfigOverrides()
		{
			if (!capturedLoggerConfig || loggerConfig == null)
				return;

			loggerConfig.saveablePrefab = originalLogSaveablePrefab;
			loggerConfig.prefabManager = originalLogPrefabManager;
			loggerConfig.saveManagerExtensions = originalLogSaveManagerExtensions;
			Logger.RefreshConfig();
		}

		private void ApplyPrefabOverridesIfRequested()
		{
			if (!applyPrefabRuntimeOverrides || !prefabAsset)
				return;

			prefabSaveable = prefabAsset.GetComponent<SaveablePrefab>();
			if (!prefabSaveable)
			{
				Log($"Prefab overrides skipped: '{prefabAsset.name}' has no SaveablePrefab component.", LogLevel.Warning);
				return;
			}

			if (!capturedPrefabSettings)
			{
				originalRememberHomeScene = prefabSaveable.RememberHomeScene;
				originalHomeSceneMode = prefabSaveable.HomeSceneCaptureMode;
				originalDisablePooling = prefabSaveable.DisablePooling;
				capturedPrefabSettings = true;
			}

			prefabSaveable.RememberHomeScene = runtimeRememberHomeScene;
			prefabSaveable.HomeSceneCaptureMode = runtimeHomeSceneMode;
			prefabSaveable.DisablePooling = runtimeDisablePooling;

			Log(
				$"Applied runtime prefab overrides to '{prefabAsset.name}': rememberHomeScene={prefabSaveable.RememberHomeScene}, " +
				$"homeSceneMode={prefabSaveable.HomeSceneCaptureMode}, disablePooling={prefabSaveable.DisablePooling}.");
		}

		private void RestorePrefabOverrides()
		{
			if (!capturedPrefabSettings || prefabSaveable == null)
				return;

			prefabSaveable.RememberHomeScene = originalRememberHomeScene;
			prefabSaveable.HomeSceneCaptureMode = originalHomeSceneMode;
			prefabSaveable.DisablePooling = originalDisablePooling;
		}

		private void LogConfiguration()
		{
			SaveSettings activeSettings = SaveManager.Instance != null ? SaveManager.Instance.SaveSettings : settings;
			SaveablePrefab prefabComponent = prefabAsset ? prefabAsset.GetComponent<SaveablePrefab>() : null;
			Scene activeScene = SceneManager.GetActiveScene();

			Log(
				$"Configuration: activeScene='{SceneLabel(activeScene)}', prefab='{(prefabAsset ? prefabAsset.name : "<none>")}', " +
				$"useSceneNameOverload={useSceneNameOverload}, explicitSceneName='{Empty(explicitSceneName)}', registerWithSaveSystem={registerWithSaveSystem}, " +
				$"factoryPoolingOverride={factoryPoolingOverride}, saveSettingsPooling={activeSettings?.usePrefabPooling.ToString() ?? "<null>"}, " +
				$"spawnPooledPrefabsInScene={activeSettings?.spawnPooledPrefabsInScene.ToString() ?? "<null>"}, logLevel={activeSettings?.logLevel.ToString() ?? "<null>"}.");

			if (prefabComponent == null)
			{
				Log("Configuration warning: assigned prefab has no SaveablePrefab component.", LogLevel.Warning);
				return;
			}

			Log(
				$"Prefab config: name='{prefabComponent.name}', prefabId='{Empty(prefabComponent.PrefabAssetID)}', " +
				$"rememberHomeScene={prefabComponent.RememberHomeScene}, homeMode={prefabComponent.HomeSceneCaptureMode}, " +
				$"homeScene='{Empty(prefabComponent.HomeScene)}', disablePooling={prefabComponent.DisablePooling}, keepAcrossScenes={prefabComponent.KeepAcrossScenes}, " +
				$"registerWithSaveSystem={prefabComponent.RegisterWithSaveSystem}.");
		}

		private void LogPrefabState(string label, SaveablePrefab prefab)
		{
			if (!prefab)
			{
				Log($"{label}: prefab reference is null or destroyed.", LogLevel.Warning);
				return;
			}

			SaveSettings activeSettings = SaveManager.Instance != null ? SaveManager.Instance.SaveSettings : settings;
			Scene activeScene = SceneManager.GetActiveScene();
			Rigidbody rb = prefab.GetComponent<Rigidbody>();
			string rigidbodyInfo = rb
				? $"rbKinematic={rb.isKinematic}, rbSleeping={rb.IsSleeping()}"
				: "rb=<none>";

			Log(
				$"{label}: name='{prefab.name}', activeSelf={prefab.gameObject.activeSelf}, activeInHierarchy={prefab.gameObject.activeInHierarchy}, " +
				$"objectScene='{SceneLabel(prefab.gameObject.scene)}', activeScene='{SceneLabel(activeScene)}', homeScene='{Empty(prefab.HomeScene)}', " +
				$"rememberHomeScene={prefab.RememberHomeScene}, homeMode={prefab.HomeSceneCaptureMode}, isAddedAtRuntime={prefab.IsAddedAtRuntime}, " +
				$"registerWithSaveSystem={prefab.RegisterWithSaveSystem}, tracked={prefab.IsTrackedBySaveManager}, disablePooling={prefab.DisablePooling}, " +
				$"prefabId='{Empty(prefab.PrefabAssetID)}', uid='{Empty(prefab.UniqueID)}', globalPooling={activeSettings?.usePrefabPooling.ToString() ?? "<null>"}, " +
				$"spawnPooledPrefabsInScene={activeSettings?.spawnPooledPrefabsInScene.ToString() ?? "<null>"}, {rigidbodyInfo}.");
		}

		private void HandleActiveSceneChanged(Scene previous, Scene next)
		{
			Log($"Scene event: active scene changed '{SceneLabel(previous)}' -> '{SceneLabel(next)}'.");
			LogPrefabState("After activeSceneChanged last", lastSpawned);
		}

		private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			Log($"Scene event: loaded '{SceneLabel(scene)}' mode={mode}.");
			LogPrefabState("After sceneLoaded last", lastSpawned);
		}

		private void HandleSceneUnloaded(Scene scene)
		{
			Log($"Scene event: unloaded '{SceneLabel(scene)}'.");
			LogPrefabState("After sceneUnloaded last", lastSpawned);
		}

		private void HandleAfterRestore(SaveablePrefab prefab)
		{
			LogPrefabState("OnAfterRestore", prefab);
		}

		private bool? GetPoolingOverride()
		{
			return factoryPoolingOverride switch
			{
				FactoryPoolingOverride.ForcePooling => true,
				FactoryPoolingOverride.ForceInstantiate => false,
				_ => null
			};
		}

		private string ResolveSceneName()
		{
			if (!string.IsNullOrWhiteSpace(explicitSceneName))
				return explicitSceneName;

			Scene activeScene = SceneManager.GetActiveScene();
			return activeScene.IsValid() ? activeScene.name : "";
		}

		private string GetOtherSceneName()
		{
			string current = SceneManager.GetActiveScene().name;
			if (string.Equals(current, sceneAName, System.StringComparison.Ordinal))
				return sceneBName;

			return sceneAName;
		}

		private static string SceneLabel(Scene scene)
		{
			return scene.IsValid() ? scene.name : "<invalid>";
		}

		private static string Empty(string value)
		{
			return string.IsNullOrEmpty(value) ? "<empty>" : value;
		}

		private static void Log(string message, LogLevel level = LogLevel.Info)
		{
			Logger.Log($"[PrefabPoolingHomeSceneDiagnostic] {message}", level);
		}
	}
}
#endif
