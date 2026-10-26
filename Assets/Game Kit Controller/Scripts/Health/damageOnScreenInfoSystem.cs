using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class damageOnScreenInfoSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool showDamageActive = true;

	[Space]
	[Header ("Debug")]
	[Space]

	public List<targetInfo> targetInfoList = new List<targetInfo> ();

	public List<playerDamageOnScreenInfoSystem> playerDamageOnScreenInfoSystemList = new List<playerDamageOnScreenInfoSystem> ();

	int currentID = 0;


	public const string mainManagerName = "Damage On Screen Info Manager";

	public static string getMainManagerName ()
	{
		return mainManagerName;
	}

	private static damageOnScreenInfoSystem _damageOnScreenInfoSystemInstance;

	public static damageOnScreenInfoSystem Instance { get { return _damageOnScreenInfoSystemInstance; } }

	bool instanceInitialized;


	public void getComponentInstance ()
	{
		if (instanceInitialized) {
			return;
		}

		if (_damageOnScreenInfoSystemInstance != null && _damageOnScreenInfoSystemInstance != this) {
			Destroy (this.gameObject);

			return;
		} 

		_damageOnScreenInfoSystemInstance = this;

		instanceInitialized = true;
	}

	void Awake ()
	{
		getComponentInstance ();
	}


	public void addNewPlayer (playerDamageOnScreenInfoSystem newPlayer)
	{
		if (!showDamageActive) {
			return;
		}

		newPlayer.damageOnScreenId = currentID;

		playerDamageOnScreenInfoSystemList.Add (newPlayer);

		currentID++;


		int targetInfoListCount = targetInfoList.Count;

		if (targetInfoListCount > 0) {

			for (int i = 0; i < targetInfoListCount; i++) {
				newPlayer.addNewTarget (targetInfoList [i]);
			}
		}
	}

	public int addNewTarget (Transform newTarget, bool removeDamageInScreenOnDeathValue, Vector3 iconOffset)
	{
		if (!showDamageActive) {
			return -1;
		}

		targetInfo newTargetInfo = new targetInfo ();
		newTargetInfo.Name = newTarget.name;
		newTargetInfo.target = newTarget;

		newTargetInfo.iconOffset = iconOffset;

		newTargetInfo.useIconOffset = iconOffset != Vector3.zero;

		newTargetInfo.ID = currentID;
		newTargetInfo.removeDamageInScreenOnDeath = removeDamageInScreenOnDeathValue;

		targetInfoList.Add (newTargetInfo);

		for (int i = 0; i < playerDamageOnScreenInfoSystemList.Count; i++) {
			playerDamageOnScreenInfoSystemList [i].addNewTarget (newTargetInfo);
		}

		currentID++;

		return currentID - 1;
	}

	public void setDamageInfo (int objectId, float amount, bool isDamage, Vector3 direction, float healhtAmount, float criticalDamageProbability)
	{
		if (!showDamageActive) {
			return;
		}

		for (int j = 0; j < targetInfoList.Count; j++) {
			if (targetInfoList [j].ID == objectId) {
				for (int i = 0; i < playerDamageOnScreenInfoSystemList.Count; i++) {
					playerDamageOnScreenInfoSystemList [i].setDamageInfo (j, amount, isDamage, direction, healhtAmount, criticalDamageProbability);
				}

				return;
			}
		}
	}

	//if the target is reached, disable all the parameters and clear the list, so a new objective can be added in any moment
	public void removeElementFromList (int objectId)
	{
		if (!showDamageActive) {
			return;
		}

		for (int j = 0; j < targetInfoList.Count; j++) {
			if (targetInfoList [j].ID == objectId) {
				for (int i = 0; i < playerDamageOnScreenInfoSystemList.Count; i++) {
					playerDamageOnScreenInfoSystemList [i].removeElementFromList (objectId);
				}
				targetInfoList.RemoveAt (j);

				return;
			}
		}
	}

	public void removeElementFromTargetListCalledByPlayer (int objectId, GameObject currentPlayer)
	{
		if (!showDamageActive) {
			return;
		}

		for (int i = 0; i < targetInfoList.Count; i++) {
			if (targetInfoList [i].ID == objectId) {
				targetInfoList.Remove (targetInfoList [i]);

				for (int j = 0; j < playerDamageOnScreenInfoSystemList.Count; j++) {
					if (playerDamageOnScreenInfoSystemList [j].player != currentPlayer) {
						playerDamageOnScreenInfoSystemList [j].removeElementFromList (objectId);
					}
				}

				return;
			}
		}
	}


	[System.Serializable]
	public class targetInfo
	{
		public string Name;
		public Transform target;
		public Vector3 iconOffset;

		public bool useIconOffset;

		public int ID;
		public RectTransform targetRectTransform;
		public GameObject targetRectTransformGameObject;

		public CanvasGroup mainCanvasGroup;

		public bool removeDamageInScreenOnDeath;

		public bool iconActive;

		public bool isDead;
		public bool containsNumberToShow;
		public List<damageNumberInfo> damageNumberInfoList = new List<damageNumberInfo> ();
	}

	[System.Serializable]
	public class damageNumberInfo
	{
		public Text damageNumberText;
		public RectTransform damageNumberRectTransform;
		public Coroutine movementCoroutine;
	}
}
