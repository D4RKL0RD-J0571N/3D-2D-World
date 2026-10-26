using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class healthBarManagementSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool showSlidersActive = true;

	[Space]
	[Header ("Debug")]
	[Space]

	public List<healthSliderInfo> healthSliderInfoList = new List<healthSliderInfo> ();

	public List<playerHealthBarManagementSystem> playerHealthBarManagementSystemList = new List<playerHealthBarManagementSystem> ();

	int currentID = 0;


	public const string mainManagerName = "Health Bar Manager";

	public static string getMainManagerName ()
	{
		return mainManagerName;
	}

	private static healthBarManagementSystem _healthBarManagementSystemInstance;

	public static healthBarManagementSystem Instance { get { return _healthBarManagementSystemInstance; } }

	bool instanceInitialized;


	public void getComponentInstance ()
	{
		if (instanceInitialized) {
//			print ("already initialized manager");

			return;
		}

		if (_healthBarManagementSystemInstance != null && _healthBarManagementSystemInstance != this) {
			Destroy (this.gameObject);

//			print (_healthBarManagementSystemInstance.gameObject.name);

//			this.gameObject.SetActive (false);

//			print ("destroy health bar");

			return;
		} 

		_healthBarManagementSystemInstance = this;

		instanceInitialized = true;
	}

	void Awake ()
	{
		getComponentInstance ();
	}

	public void addNewPlayer (playerHealthBarManagementSystem newPlayer)
	{
		if (!showSlidersActive) {
			return;
		}

		playerHealthBarManagementSystemList.Add (newPlayer);


		int healthSliderInfoListCount = healthSliderInfoList.Count;

		if (healthSliderInfoListCount > 0) {

			for (int i = 0; i < healthSliderInfoListCount; i++) {
				newPlayer.addNewTargetSlider (
					healthSliderInfoList [i].sliderOwner.gameObject, 
					healthSliderInfoList [i].sliderPrefab, 
					healthSliderInfoList [i].sliderOffset,
					healthSliderInfoList [i].healthAmount,
					healthSliderInfoList [i].shieldAmount,
					healthSliderInfoList [i].Name,
					healthSliderInfoList [i].textColor,
					healthSliderInfoList [i].sliderColor,
					healthSliderInfoList [i].ID,
					healthSliderInfoList [i].healthBarSliderActiveOnStart,
					healthSliderInfoList [i].useHealthSlideInfoOnScreen,
					healthSliderInfoList [i].useCircleHealthSlider);
			}
		}
	}

	public void disableHealhtBars ()
	{
		if (!showSlidersActive) {
			return;
		}

		enableOrDisableHealhtBars (false);
	}

	public void enableHealhtBars ()
	{
		if (!showSlidersActive) {
			return;
		}

		enableOrDisableHealhtBars (true);
	}

	public void enableOrDisableHealhtBars (bool state)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].enableOrDisableHealhtBars (state);
		}
	}

	public int addNewTargetSlider (GameObject sliderOwner, GameObject sliderPrefab, Vector3 sliderOffset, float healthAmount, 
	                               float shieldAmount, string ownerName, Color textColor, Color sliderColor, bool healthBarSliderActiveOnStart,
	                               bool useHealthSlideInfoOnScreen, bool useCircleHealthSlider)
	{
		if (!showSlidersActive) {
			return -1;
		}

		healthSliderInfo newHealthSliderInfo = new healthSliderInfo ();
		newHealthSliderInfo.Name = ownerName;
		newHealthSliderInfo.sliderOwner = sliderOwner.transform;

		currentID++;

		newHealthSliderInfo.ID = currentID;


		newHealthSliderInfo.sliderPrefab = sliderPrefab;
		newHealthSliderInfo.sliderOffset = sliderOffset;
		newHealthSliderInfo.healthAmount = healthAmount;
		newHealthSliderInfo.shieldAmount = shieldAmount;

		newHealthSliderInfo.textColor = textColor;
		newHealthSliderInfo.sliderColor = sliderColor;
		newHealthSliderInfo.healthBarSliderActiveOnStart = healthBarSliderActiveOnStart;
		newHealthSliderInfo.useHealthSlideInfoOnScreen = useHealthSlideInfoOnScreen;
		newHealthSliderInfo.useCircleHealthSlider = useCircleHealthSlider;
	

		healthSliderInfoList.Add (newHealthSliderInfo);

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].addNewTargetSlider (sliderOwner, sliderPrefab, sliderOffset, healthAmount, shieldAmount, ownerName, 
				textColor, sliderColor, currentID, healthBarSliderActiveOnStart, useHealthSlideInfoOnScreen, useCircleHealthSlider);
		}

		return currentID;
	}

	public void removeTargetSlider (int objectID)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].removeTargetSlider (objectID);
		}

		for (int i = 0; i < healthSliderInfoList.Count; i++) {
			if (healthSliderInfoList [i].ID == objectID) {
				healthSliderInfoList.RemoveAt (i);
				return;
			}
		}
	}

	public void removeElementFromObjectiveListCalledByPlayer (int objectId, GameObject currentPlayer)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < healthSliderInfoList.Count; i++) {
			if (healthSliderInfoList [i].ID == objectId) {

				healthSliderInfoList.Remove (healthSliderInfoList [i]);

				for (int j = 0; j < playerHealthBarManagementSystemList.Count; j++) {
					if (playerHealthBarManagementSystemList [j].playerGameObject != currentPlayer) {
						playerHealthBarManagementSystemList [j].removeTargetSlider (objectId);
					}
				}

				return;
			}
		}
	}

	public void udpateSliderInfo (int objectID, string newName, Color textColor, Color backgroundColor)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].setSliderInfo (objectID, newName, textColor, backgroundColor);
		}
	}

	public void updateSliderAmount (int objectID, float value)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].setSliderAmount (objectID, value);
		}
	}

	public void setSliderAmount (int objectID, float sliderValue)
	{
		if (!showSlidersActive) {
			return;
		}

		updateSliderAmount (objectID, sliderValue);
	}

	public void updateShieldSliderAmount (int objectID, float value)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].setShieldSliderAmount (objectID, value);
		}
	}

	public void setShieldSliderAmount (int objectID, float sliderValue)
	{
		if (!showSlidersActive) {
			return;
		}

		updateShieldSliderAmount (objectID, sliderValue);
	}

	public void updateSliderMaxValue (int objectID, float maxSliderValue)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].updateSliderMaxValue (objectID, maxSliderValue);
		}
	}

	public void updateShieldSliderMaxValue (int objectID, float maxSliderValue)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].updateShieldSliderMaxValue (objectID, maxSliderValue);
		}
	}

	public void updateSliderOffset (int objectID, float value)
	{
		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].updateSliderOffset (objectID, value);
		}
	}

	public void setSliderInfo (int objectID, string newName, Color textColor, Color backgroundColor)
	{
		if (!showSlidersActive) {
			return;
		}

		udpateSliderInfo (objectID, newName, textColor, backgroundColor);
	}

	public void setSliderVisibleStateForPlayer (int objectID, GameObject player, bool state)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			if (playerHealthBarManagementSystemList [i].getPlayerGameObject () == player) {
				playerHealthBarManagementSystemList [i].setSliderVisibleState (objectID, state);
			}
		}
	}

	public void setSliderLocatedState (int objectID, bool state)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].setSliderLocatedState (objectID, state);
		}
	}

	public void setSliderLocatedStateForPlayer (int objectID, GameObject player, bool state)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			if (playerHealthBarManagementSystemList [i].getPlayerGameObject () == player) {
				playerHealthBarManagementSystemList [i].setSliderLocatedState (objectID, state);
			}
		}
	}

	public void setSliderVisibleState (int objectID, bool state)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].setSliderVisibleState (objectID, state);
		}
	}

	public void pauseOrResumeShowHealthSliders (bool state)
	{
		if (!showSlidersActive) {
			return;
		}

		for (int i = 0; i < playerHealthBarManagementSystemList.Count; i++) {
			playerHealthBarManagementSystemList [i].pauseOrResumeShowHealthSliders (state);
		}
	}
}
