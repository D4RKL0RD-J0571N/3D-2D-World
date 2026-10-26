using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class addMapObjectInformation : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool activateAtStart;
	public bool activateOnEnable;

	public bool callCreateMapIconInfoIfComponentExists;

	public string mainMapCreatorManagerName = "Map Creator";

	[Space]
	[Header ("Map Settings")]
	[Space]

	public bool addMapIcon;
	public string mapObjectName;
	public string mapObjectTypeName;
	[TextArea (3, 10)] public string description;

	public bool visibleInAllBuildings;
	public bool visibleInAllFloors;
	public bool calculateFloorAtStart;

	public bool setFloorNumber;
	public int buildingIndex;
	public int floorNumber;

	[Space]
	[Header ("Objective Screen Icon Settings")]
	[Space]

	public bool addIconOnScreen;
	public float triggerRadius;
	public bool showOffScreenIcon;
	public bool useCloseDistance;
	public bool showMapWindowIcon;
	public bool showDistance;
	public bool showDistanceOffScreen;
	public string objectiveIconName;
	public float objectiveOffset;

	public bool useCustomObjectiveColor;
	public Color objectiveColor;
	public bool removeCustomObjectiveColor;

	public string mainManagerName = "Screen Objectives Manager";

	mapObjectInformation mapObjectInformationManager;
	mapCreator mapCreatorManager;

	screenObjectivesSystem mainscreenObjectivesSystem;

	bool componentAlreadyExists;

	void Start ()
	{
		if (activateAtStart) {
			activateMapObject ();
		}
	}

	void OnEnable ()
	{
		if (activateOnEnable) {
			activateMapObject ();
		}
	}

	public void checkGetMapCreatorManager ()
	{
		bool mapCreatorManagerAssigned = (mapCreatorManager != null);

		if (!mapCreatorManagerAssigned) {
			mapCreatorManager = mapCreator.Instance;

			mapCreatorManagerAssigned = mapCreatorManager != null;
		}

		if (!mapCreatorManagerAssigned) {
			GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (mapCreator.getMainManagerName (), typeof(mapCreator), true);

			mapCreatorManager = mapCreator.Instance;

			mapCreatorManagerAssigned = (mapCreatorManager != null);
		}

		if (!mapCreatorManagerAssigned) {
			mapCreatorManager = GKC_Utils.findObjectByComponent<mapCreator> ();
		} 
	}

	public void activateMapObject ()
	{
		if (addMapIcon) {

			checkGetMapCreatorManager ();

			if (mapCreatorManager == null) {
				print ("Warning: there is no map system configured, so the object " + gameObject.name + " won't use a new map object icon");

				return;
			}

			if (mapObjectInformationManager == null) {
				mapObjectInformationManager = gameObject.AddComponent<mapObjectInformation> ();
			} else {
				componentAlreadyExists = true;
			}

			if (mapObjectInformationManager == null) {
				mapObjectInformationManager = gameObject.GetComponent<mapObjectInformation> ();
			}

			mapObjectInformationManager.assignID (mapCreatorManager.getAndIncreaselastMapObjectInformationIDAssigned ());

			mapObjectInformationManager.setMapObjectName (mapObjectName);

			if (addIconOnScreen) {
				mapObjectInformationManager.setCustomValues (visibleInAllBuildings, visibleInAllFloors, calculateFloorAtStart, useCloseDistance, 
					triggerRadius, showOffScreenIcon, showMapWindowIcon, showDistance, showDistanceOffScreen, objectiveIconName, useCustomObjectiveColor, objectiveColor, removeCustomObjectiveColor);
			}

			if (setFloorNumber) {
				mapObjectInformationManager.floorIndex = floorNumber;
				mapObjectInformationManager.buildingIndex = buildingIndex;
			}

			mapObjectInformationManager.getMapObjectInformation ();
			mapObjectInformationManager.getIconTypeIndexByName (mapObjectTypeName);
			mapObjectInformationManager.description = description;

			if (componentAlreadyExists) {
				if (callCreateMapIconInfoIfComponentExists) {
					mapObjectInformationManager.createMapIconInfo ();

				}
			}
		} else {
			if (addIconOnScreen) {
				bool mainscreenObjectivesSystemLocated = mainscreenObjectivesSystem != null;

				if (!mainscreenObjectivesSystemLocated) {
					mainscreenObjectivesSystem = screenObjectivesSystem.Instance;

					mainscreenObjectivesSystemLocated = mainscreenObjectivesSystem != null;
				}

				if (!mainscreenObjectivesSystemLocated) {
					GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (screenObjectivesSystem.getMainManagerName (), typeof(screenObjectivesSystem), true);

					mainscreenObjectivesSystem = screenObjectivesSystem.Instance;

					mainscreenObjectivesSystemLocated = mainscreenObjectivesSystem != null;
				}

				if (mainscreenObjectivesSystemLocated) {
					mainscreenObjectivesSystem.addElementToScreenObjectiveList (gameObject, useCloseDistance, triggerRadius, showOffScreenIcon, 
						showDistance, showDistanceOffScreen, objectiveIconName, useCustomObjectiveColor, objectiveColor, removeCustomObjectiveColor, objectiveOffset);
				}
			}
		}
	}

	public void removeMapObject ()
	{
		bool mainscreenObjectivesSystemLocated = mainscreenObjectivesSystem != null;

		if (!mainscreenObjectivesSystemLocated) {
			mainscreenObjectivesSystem = screenObjectivesSystem.Instance;

			mainscreenObjectivesSystemLocated = mainscreenObjectivesSystem != null;
		}

		if (!mainscreenObjectivesSystemLocated) {
			GKC_Utils.instantiateMainManagerOnSceneWithTypeOnApplicationPlaying (screenObjectivesSystem.getMainManagerName (), typeof(screenObjectivesSystem), true);

			mainscreenObjectivesSystem = screenObjectivesSystem.Instance;

			mainscreenObjectivesSystemLocated = mainscreenObjectivesSystem != null;
		}

		if (mainscreenObjectivesSystemLocated) {
			mainscreenObjectivesSystem.removeGameObjectFromList (gameObject);
		}
	}
}
