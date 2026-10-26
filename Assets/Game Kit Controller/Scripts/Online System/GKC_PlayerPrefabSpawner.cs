using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GKC_PlayerPrefabSpawner : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool checkPlayerPrefabSpawnOnStartEnabled;

	public bool isMainPlayer;

	public Vector3 spawnPosition;
	public Vector3 spawnRotation;

	[Space]
	[Header ("Event Settings")]
	[Space]

	public bool useEventsOnPlayerSpawn;
	public UnityEvent eventOnMainPlayer;
	public UnityEvent eventOnPawnCharacter;

	[Space]
	[Header ("Components")]
	[Space]

	public playerController mainPlayerController;
	public playerCamera mainPlayerCamera;

	public saveGameSystem mainSaveGameSystem;

	public menuPause pauseManager;

	public mapSystem mainMapSystem;

	public playerInputManager mainPlayerInputManager;

	[Space]
	[Header ("Main Managers")]
	[Space]

	public playerCharactersManager mainPlayerCharactersManager;

	public inputManager mainInputManager;

	public gameManager mainGameManager;

	void Awake ()
	{
		if (checkPlayerPrefabSpawnOnStartEnabled) {
			checkPlayerPrefabSpawnOnAwake ();
		}
	}

	void Start ()
	{
		if (checkPlayerPrefabSpawnOnStartEnabled) {
			checkPlayerPrefabSpawn ();
		}
	}

	public void setPositionToSpawn (Vector3 newValue)
	{
		spawnPosition = newValue;
	}

	public void setRotationToSpawn (Vector3 newValue)
	{
		spawnRotation = newValue;
	}

	public void checkPlayerPrefabSpawnOnAwake ()
	{
		getMainPlayerCharacterManager ();

		findInputManager ();

		findGameManager ();

		int characterIndex = mainPlayerCharactersManager.getPlayerListCount ();
	
		isMainPlayer = (characterIndex == 0);

		characterIndex++;

		mainPlayerController.setPlayerIsNonLocalAvatarState (!isMainPlayer);

		mainPlayerInputManager.setPlayerIsNonLocalAvatarState (!isMainPlayer);

		mainPlayerController.setPlayerNonLocalAvatarID (characterIndex);

		mainPlayerInputManager.setPlayerNonLocalAvatarID (characterIndex);

		if (isMainPlayer) {
			mainGameManager.setMainCamera (mainPlayerCamera.getMainCamera ());

			mainGameManager.setMainPlayerGameObject (mainPlayerController.gameObject);

			mainGameManager.setMainPlayerCameraGameObject (mainPlayerCamera.gameObject);

			mainGameManager.setCurrentPlayerCamera (mainPlayerCamera);

			if (mainMapSystem != null) {
				mainMapSystem.checkMapSystemAddedOnMapCreator ();

				mainMapSystem.checkElementsOnAwake ();
			}
		} else {
			if (mainMapSystem != null) {
				mainMapSystem.setMapEnabledState (false);
			}

			mainPlayerInputManager.setPlayerInputEnabledState (false);

			mainPlayerInputManager.setScreenActionPanelsEnabledState (false);

			mainPlayerCamera.enableOrDisableMainCamera (false);

			mainPlayerCamera.enableOrDisableCameraAudioListener (false);
		}

		mainSaveGameSystem.checkComponentsToInitialize ();

		print ("initializing awake");

		if (!isMainPlayer) {
			pauseManager.setIgnoreCreateIngameMenuPanelState (true);

			print ("ignore menue panel create");

			pauseManager.enableOrDisablePlayerHUD (false);

			pauseManager.enableOrDisableDynamicElementsOnScreen (false);
		}

		if (useEventsOnPlayerSpawn) {
			if (isMainPlayer) {
				eventOnMainPlayer.Invoke ();
			} else {
				eventOnPawnCharacter.Invoke ();

				print ("H)SDHISHDSH");
			}
		}

	}

	public void checkPlayerPrefabSpawn ()
	{
		print ("initializing start");

		mainPlayerController.setNewPlayerCharacterPositionAndRotation (spawnPosition, spawnRotation);

		if (isMainPlayer) {
			mainInputManager.setMainPlayerInfo (mainPlayerInputManager, pauseManager);

			mainInputManager.getTouchButtonList ();

			mainInputManager.checkInputManagerElementsOnStart ();
		}
	}

	void getMainPlayerCharacterManager ()
	{
		bool mainPlayerCharactersManagerLocated = mainPlayerCharactersManager != null;

		if (!mainPlayerCharactersManagerLocated) {
			mainPlayerCharactersManager = playerCharactersManager.Instance;

			mainPlayerCharactersManagerLocated = mainPlayerCharactersManager != null;
		}

		if (!mainPlayerCharactersManagerLocated) {
			mainPlayerCharactersManager = GKC_Utils.findObjectByComponent<playerCharactersManager> ();

			mainPlayerCharactersManager.getComponentInstanceOnApplicationPlaying ();
		} 
	}

	void findInputManager ()
	{
		bool inputManagerLocated = mainInputManager != null;

		if (!inputManagerLocated) {
			mainInputManager = inputManager.Instance;

			inputManagerLocated = mainInputManager != null;
		}

		if (!inputManagerLocated) {
			mainInputManager = GKC_Utils.findObjectByComponent<inputManager> ();

			mainInputManager.getComponentInstanceOnApplicationPlaying ();
		} 
	}

	void findGameManager ()
	{
		bool mainGameManagerLocated = mainGameManager != null;

		if (!mainGameManagerLocated) {
			mainGameManager = gameManager.Instance;

			mainGameManagerLocated = mainGameManager != null;
		}

		if (!mainGameManagerLocated) {
			mainGameManager = GKC_Utils.findObjectByComponent<gameManager> ();

			mainGameManager.getComponentInstanceOnApplicationPlaying ();
		} 
	}

	public void setIsMainPlayerState (bool state)
	{
		isMainPlayer = state;
	}
}