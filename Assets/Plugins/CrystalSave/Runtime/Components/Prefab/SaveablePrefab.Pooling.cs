#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arawn.CrystalSave.Runtime
{
	public partial class SaveablePrefab : IPoolableSaveable
	{
		public void OnBeforeSpawn()
		{
			if (!RegisterWithSaveSystem)
			{
				if (RememberHomeScene)
				{
					Logger.Log(
						$"[CrystalSave][PoolHomeScene] OnBeforeSpawn skipped for '{name}' because RegisterWithSaveSystem is false. " +
						$"objectScene='{SceneLabel(gameObject.scene)}', activeScene='{SceneLabel(SceneManager.GetActiveScene())}', " +
						$"homeScene='{HomeSceneLabel(HomeScene)}', homeMode={HomeSceneCaptureMode}, prefabId='{PrefabAssetID}', uid='{UniqueID}'.",
						LogCategory.SaveablePrefab,
						LogLevel.Info);
				}
				return;
			}
			
			// During loading (either global loading state or individual prefab loading state),
			// PrefabManager will assign the saved UniqueID after spawning,
			// so we should not generate a new one here as it would break restoration
			var currentState = SaveManager.Instance?.StateMachine?.CurrentState;
			if (currentState == SaveState.Loading || IsLoading)
			{
				if (RememberHomeScene)
				{
					Logger.Log(
						$"[CrystalSave][PoolHomeScene] OnBeforeSpawn skipped ID/home-scene capture for '{name}' during load. " +
						$"state={currentState}, isLoading={IsLoading}, objectScene='{SceneLabel(gameObject.scene)}', " +
						$"activeScene='{SceneLabel(SceneManager.GetActiveScene())}', homeScene='{HomeSceneLabel(HomeScene)}', " +
						$"homeMode={HomeSceneCaptureMode}, prefabId='{PrefabAssetID}', uid='{UniqueID}'.",
						LogCategory.SaveablePrefab,
						LogLevel.Info);
				}
				return;
			}

			if (RememberHomeScene)
			{
				Logger.Log(
					$"[CrystalSave][PoolHomeScene] OnBeforeSpawn begin for '{name}'. " +
					$"objectScene='{SceneLabel(gameObject.scene)}', activeScene='{SceneLabel(SceneManager.GetActiveScene())}', " +
					$"homeBefore='{HomeSceneLabel(HomeScene)}', homeMode={HomeSceneCaptureMode}, prefabId='{PrefabAssetID}', uidBefore='{UniqueID}'.",
					LogCategory.SaveablePrefab,
					LogLevel.Info);
			}

			// fresh composite key
			SetUniqueID(Guid.NewGuid().ToString());

			// Fix for pooled prefabs: Capture the ACTIVE scene as HomeScene when remembering home scene,
			// not the pool's scene (which is typically "DontDestroyOnLoad")
			if (RememberHomeScene && HomeSceneCaptureMode == HomeSceneMode.InstantiationScene)
			{
				var activeScene = SceneManager.GetActiveScene();
				if (activeScene.IsValid())
				{
					SetHomeScene(activeScene.name);
					Logger.Log(
						$"[CrystalSave][PoolHomeScene] OnBeforeSpawn captured HomeScene='{HomeSceneLabel(HomeScene)}' from active scene for '{name}'. " +
						$"objectScene='{SceneLabel(gameObject.scene)}', prefabId='{PrefabAssetID}', uid='{UniqueID}'.",
						LogCategory.SaveablePrefab,
						LogLevel.Info);
				}
			}

			// (re-)register exactly once
			RegisterForSaving();

			if (RememberHomeScene)
			{
				Logger.Log(
					$"[CrystalSave][PoolHomeScene] OnBeforeSpawn registered '{name}'. " +
					$"objectScene='{SceneLabel(gameObject.scene)}', activeScene='{SceneLabel(SceneManager.GetActiveScene())}', " +
					$"homeAfter='{HomeSceneLabel(HomeScene)}', prefabId='{PrefabAssetID}', uid='{UniqueID}'.",
					LogCategory.SaveablePrefab,
					LogLevel.Info);
			}
		}

		public void OnBeforeDespawn()
		{
			if (RegisterWithSaveSystem)
				UnregisterFromSaving();
		}

		private static string SceneLabel(Scene scene)
		{
			return scene.IsValid() ? scene.name : "<invalid>";
		}

		private static string HomeSceneLabel(string sceneName)
		{
			return string.IsNullOrEmpty(sceneName) ? "<empty>" : sceneName;
		}
	}
}
#endif
