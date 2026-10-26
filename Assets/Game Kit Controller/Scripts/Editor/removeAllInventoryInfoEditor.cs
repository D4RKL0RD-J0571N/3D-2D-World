using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.IO;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;

public class removeAllInventoryInfoEditor : EditorWindow
{
	GUISkin guiSkin;
	Rect windowRect = new Rect ();
	Event currentEvent;

	Vector2 rectSize = new Vector2 (600, 330);

	bool objectRemoved;

	float timeToBuild = 0.2f;
	float timer;

	GUIStyle style = new GUIStyle ();

	float windowHeightPercentage = 0.3f;

	Vector2 screenResolution;

	public string objectToRemoveName;

	public bool removeObjectFromPlayer;

	public bool removeObjectFromAI;

	public bool removeObjectFromCharactersEvenIfNotInventoryFound;

	Vector2 scrollPos1;

	float maxLayoutWidht = 320;

	bool closeWindowAfterRemovingObject = true;

	[MenuItem ("Game Kit Controller/Remove Inventory Object Info", false, 20)]
	public static void removeAllInventoryInfo ()
	{
		GetWindow<removeAllInventoryInfoEditor> ();
	}

	void OnEnable ()
	{
		objectToRemoveName = "";

		screenResolution = new Vector2 (Screen.currentResolution.width, Screen.currentResolution.height);

		//		Debug.Log (screenResolution + " " + partsHeight + " " + settingsHeight + " " + previewHeight);

		float totalHeight = screenResolution.y * windowHeightPercentage;

		if (totalHeight < 330) {
			totalHeight = 330;
		}

		rectSize = new Vector2 (600, totalHeight);

		resetCreatorValues ();
	}

	void OnDisable ()
	{
		resetCreatorValues ();
	}

	void resetCreatorValues ()
	{
		objectRemoved = false;

		removeObjectFromPlayer = false;

		removeObjectFromAI = false;

		removeObjectFromCharactersEvenIfNotInventoryFound = false;
	
		Debug.Log ("Object window closed");
	}

	void OnGUI ()
	{
		if (!guiSkin) {
			guiSkin = Resources.Load ("GUI") as GUISkin;
		}
		GUI.skin = guiSkin;

		this.minSize = rectSize;

		this.titleContent = new GUIContent ("Objects Info", null, "Remove Object Info");

		GUILayout.BeginVertical ("Remove All Object Info", "window");

		EditorGUILayout.Space ();
		EditorGUILayout.Space ();
		EditorGUILayout.Space ();

		windowRect = GUILayoutUtility.GetLastRect ();
		//		windowRect.position = new Vector2 (0, windowRect.position.y);
		windowRect.width = this.maxSize.x;

		GUILayout.BeginHorizontal ();

		EditorGUILayout.HelpBox ("", MessageType.Info);

		style = new GUIStyle (EditorStyles.helpBox);
		style.richText = true;

		style.fontStyle = FontStyle.Bold;
		style.fontSize = 17;

		EditorGUILayout.LabelField ("Write the name of the object to remove and press the button 'Remove Object Info'. \n\n", style);
		GUILayout.EndHorizontal ();

		scrollPos1 = EditorGUILayout.BeginScrollView (scrollPos1, false, false);

		GUILayout.FlexibleSpace ();

		EditorGUILayout.Space ();

		GUILayout.BeginHorizontal ();
		GUILayout.Label ("Object Name", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
		objectToRemoveName = (string)EditorGUILayout.TextField (objectToRemoveName, GUILayout.ExpandWidth (true)); 
		GUILayout.EndHorizontal ();

		EditorGUILayout.Space (); 

		GUILayout.BeginHorizontal ();
		GUILayout.Label ("Remove Object From Player", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
		removeObjectFromPlayer = (bool)EditorGUILayout.Toggle ("", removeObjectFromPlayer);
		GUILayout.EndHorizontal ();

		EditorGUILayout.Space ();

		GUILayout.BeginHorizontal ();
		GUILayout.Label ("Remove Object From AI", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
		removeObjectFromAI = (bool)EditorGUILayout.Toggle ("", removeObjectFromAI);
		GUILayout.EndHorizontal ();

		EditorGUILayout.Space ();
	
		EditorGUILayout.Space ();

		GUILayout.BeginHorizontal ();
		GUILayout.Label ("Close Wizard Once Object Removed", EditorStyles.boldLabel, GUILayout.MaxWidth (maxLayoutWidht));
		closeWindowAfterRemovingObject = (bool)EditorGUILayout.Toggle ("", closeWindowAfterRemovingObject);
		GUILayout.EndHorizontal ();

		EditorGUILayout.Space ();

		EditorGUILayout.EndScrollView ();

		EditorGUILayout.Space ();

		if (GUILayout.Button ("Remove Object Info")) {
			removeObject ();
		}

		if (GUILayout.Button ("Cancel")) {
			this.Close ();
		}

		GUILayout.EndVertical ();
	}

	void removeObject ()
	{
		if (objectToRemoveName == null || objectToRemoveName == "") {
			Debug.Log ("Please, make sure to write a name of an object to remove");

			return;
		}

		bool objectLocated = false;

		inventoryListManager mainInventoryListManager = inventoryListManager.Instance;

		bool mainInventoryManagerFound = mainInventoryListManager != null;

		if (!mainInventoryManagerFound) {
			GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (inventoryListManager.getMainManagerName (), typeof(inventoryListManager), true);

			mainInventoryListManager = inventoryListManager.Instance;

			mainInventoryManagerFound = (mainInventoryListManager != null);
		}

		if (!mainInventoryManagerFound) {
			mainInventoryListManager = GKC_Utils.findObjectByComponent<inventoryListManager> ();

			mainInventoryManagerFound = mainInventoryListManager != null;
		} 

		if (mainInventoryManagerFound) {
			bool objectFoundOnInventoryListManager = mainInventoryListManager.existInventoryInfoFromName (objectToRemoveName);

			if (objectFoundOnInventoryListManager) {
				mainInventoryListManager.removeInventoryObjectByName (objectToRemoveName);

				Debug.Log ("Object " + objectToRemoveName + " found and removed from inventory list manager");

				objectLocated = true;
			} else {
				Debug.Log ("Object " + objectToRemoveName + " not found on inventory list manager");
			}
		}

		bool removeObjectFromCharactersResult = true;

		if (!objectLocated) {
			if (!removeObjectFromCharactersEvenIfNotInventoryFound) {
				removeObjectFromCharactersResult = false;
			}
		}
			
		if (removeObjectFromCharactersResult) {
			playerComponentsManager[] playerComponentsManagerList = GKC_Utils.findObjectsByComponent<playerComponentsManager> ();

			foreach (playerComponentsManager currentPlayerComponentsManager in playerComponentsManagerList) {
				playerController currentPlayerController = currentPlayerComponentsManager.getPlayerController ();

				bool currentCharacterIsPlayer = currentPlayerController.isCharacterUsedByAI ();

				bool removeObjectOnlyOnPlayerResult = (currentCharacterIsPlayer && removeObjectFromPlayer) ||
				                                      (!currentCharacterIsPlayer && removeObjectFromAI);


				if (removeObjectOnlyOnPlayerResult) {
					playerWeaponsManager currentPlayerWeaponsManager = currentPlayerComponentsManager.getPlayerWeaponsManager ();

					bool weaponFound = currentPlayerWeaponsManager.checkIfWeaponExists (objectToRemoveName);

					if (weaponFound) {
						currentPlayerWeaponsManager.removeWeaponFromPlayerBodyByName (objectToRemoveName);

						Debug.Log ("Removing fire weapon from " + currentPlayerComponentsManager.gameObject.name);
					}

					meleeWeaponsGrabbedManager currentMeleeWeaponsGrabbedManager = currentPlayerComponentsManager.getMeleeWeaponsGrabbedManager ();

					bool meleeWeaponFound = currentMeleeWeaponsGrabbedManager.checkIfCanUseMeleeWeaponPrefabByName (objectToRemoveName);

					if (meleeWeaponFound) {
						currentMeleeWeaponsGrabbedManager.removeMeleeWeaponPrefab (objectToRemoveName);

						Debug.Log ("Removing melee weapon from " + currentPlayerComponentsManager.gameObject.name);
					}

					bool meleeShieldFound = currentMeleeWeaponsGrabbedManager.checkIfMeleeShieldExists (objectToRemoveName);

					if (meleeShieldFound) {
						currentMeleeWeaponsGrabbedManager.removeMeleeShieldPrefab (objectToRemoveName);

						Debug.Log ("Removing melee shield from " + currentPlayerComponentsManager.gameObject.name);
					}
				}

				Debug.Log ("\n\n");
			}
		}

		objectRemoved = true;
	}

	void Update ()
	{
		if (objectRemoved) {
			if (timer < timeToBuild) {
				timer += 0.01f;

				if (timer > timeToBuild) {
					timer = 0;

					if (closeWindowAfterRemovingObject) {
						this.Close ();
					} else {
						OnEnable ();
					}
				}
			}
		}
	}
}
#endif