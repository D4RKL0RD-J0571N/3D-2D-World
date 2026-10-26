using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GKC_PoolingAIManager : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool findDisabledElementsOnSceneGameObjects = true;

	[Space]
	[Header ("AI Pooling List Settings")]
	[Space]

	public List<GKC_PoolingElementAI> GKC_PoolingElementAIList = new List<GKC_PoolingElementAI> ();

	public void getAllGKC_PoolingElementAIOnLevel ()
	{
		GKC_PoolingElementAIList.Clear ();

		if (findDisabledElementsOnSceneGameObjects) {
            GKC_PoolingElementAI [] temporalGKC_PoolingElementAIList = GKC_Utils.findObjectsByComponent<GKC_PoolingElementAI> (true);

			foreach (GKC_PoolingElementAI currentGKC_PoolingElementAI in temporalGKC_PoolingElementAIList) {
				if (!GKC_PoolingElementAIList.Contains (currentGKC_PoolingElementAI)) {
					GKC_PoolingElementAIList.Add (currentGKC_PoolingElementAI);
				}
			}
        } else {
			GKC_PoolingElementAI[] newGKC_PoolingElementAIList = GKC_Utils.findObjectsByComponent<GKC_PoolingElementAI> ();

			foreach (GKC_PoolingElementAI currentGKC_PoolingElementAI in newGKC_PoolingElementAIList) {
				if (!GKC_PoolingElementAIList.Contains (currentGKC_PoolingElementAI)) {
					GKC_PoolingElementAIList.Add (currentGKC_PoolingElementAI);
				}
			}
		}

		updateComponent ();
	}

	public void clearGKC_PoolingElementAIList ()
	{
		GKC_PoolingElementAIList.Clear ();

		updateComponent ();
	}

	public void enableAllGKC_PoolingElementAIOnLevel ()
	{
		enableOrDisableAllGKC_PoolingElementAIOnLevel (true);
	}

	public void disableAllGKC_PoolingElementAIOnLevel ()
	{
		enableOrDisableAllGKC_PoolingElementAIOnLevel (false);
	}

	public void enableOrDisableAllGKC_PoolingElementAIOnLevel (bool state)
	{
		for (int i = 0; i < GKC_PoolingElementAIList.Count; i++) {
			GKC_PoolingElementAIList [i].checkEventsOnEnableOrDisablePoolingManagementOnObject (state);
		}

		updateComponent ();
	}

	public void updateComponent ()
	{
		GKC_Utils.updateComponent (this);

		GKC_Utils.updateDirtyScene ("Update Pooling AI Manager " + gameObject.name, gameObject);
	}
}
