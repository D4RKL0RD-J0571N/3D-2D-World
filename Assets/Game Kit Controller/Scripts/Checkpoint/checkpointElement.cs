using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class checkpointElement : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public int checkpointID;

	public bool overwriteThisCheckpoint;

	public bool saveFirstTimeCheckpointElementFound = true;

	public bool saveInEveryTriggerEnter;

	[Space]
	[Header ("Transform Settings")]
	[Space]

	public bool useCustomSaveTransform;
	public Transform customSaveTransform;

	public bool useCustomCameraTransform;
	public Transform customCameraTransform;
	public Transform customCameraPivotTransform;

	[Space]
	[Header ("Other Settings")]
	[Space]

	public List<string> tagToSave = new List<string> ();

	[Space]
	[Header ("Debug")]
	[Space]

	public bool checkpointAlreadyFound;

	[Space]
	[Header ("Event Settings")]
	[Space]

	public bool useEventOnCheckpointActivated;
	public UnityEvent eventOnCheckpointActivated;

	[Space]
	[Header ("Components")]
	[Space]

	public checkpointSystem checkpointManager;
	public Collider mainCollider;


	void Awake ()
	{
		StartCoroutine (activateTriggers ());
	}

	IEnumerator activateTriggers ()
	{
		if (mainCollider != null) {
			mainCollider.enabled = false;

			yield return new WaitForSeconds (1);

			mainCollider.enabled = true;
		}
	}

	public void setCheckPointManager (checkpointSystem manager)
	{
		checkpointManager = manager;

		updateComponent ();
	}

	public void OnTriggerEnter (Collider col)
	{
		if (!checkpointManager.isCheckpointSystemEnabled ()) {
			return;
		}

		if ((!checkpointAlreadyFound || saveInEveryTriggerEnter || saveFirstTimeCheckpointElementFound) && tagToSave.Contains (col.tag)) {
			bool saveGameResult = false;

			if (checkpointAlreadyFound) {
				if (saveInEveryTriggerEnter) {
					saveGameResult = true;
				}
			} else {
				if (saveFirstTimeCheckpointElementFound) {
					saveGameResult = true;
				}
			}

			checkpointAlreadyFound = true;

			if (checkpointManager == null) {
				checkpointManager = GKC_Utils.findObjectByComponent<checkpointSystem> (); 
			}

			if (saveGameResult) {
				playerComponentsManager currentPlayerComponentsManager = col.gameObject.GetComponent<playerComponentsManager> (); 

				if (currentPlayerComponentsManager != null) {
					saveGameSystem currentSaveGameSystem = currentPlayerComponentsManager.getSaveGameSystem ();

					if (checkpointManager != null) {
						currentSaveGameSystem.setSavingGameManuallyActiveState (true);

                        if (useCustomSaveTransform) {
							currentSaveGameSystem.saveGameCheckpoint (customSaveTransform, checkpointID, checkpointManager.checkpointSceneID, overwriteThisCheckpoint, false, true);
						} else {
							currentSaveGameSystem.saveGameCheckpoint (null, checkpointID, checkpointManager.checkpointSceneID, overwriteThisCheckpoint, false, true);
						}

                        currentSaveGameSystem.setSavingGameManuallyActiveState (false);
                    }
				}
			}

			checkpointManager.setCurrentCheckpointElement (transform);

			if (useEventOnCheckpointActivated) {
				eventOnCheckpointActivated.Invoke ();
			}
		}
	}

	public void updateComponent ()
	{
		GKC_Utils.updateComponent (this);

		GKC_Utils.updateDirtyScene ("Update Checkpoint Element Info", gameObject);
	}
}