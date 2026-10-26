using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class grabObjectGenericModeMountPointSystem : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool grabObjectsEnabled = true;

	public bool useCustomGrabbedObjectScaleMultiplier;
	public float customGrabbedObjectScaleMultiplier;

	[Space]
	[Header ("Mount Point List Settings")]
	[Space]

	public List<objectPointInfo> objectPointInfoList = new List<objectPointInfo> ();


	public bool isUseCustomGrabbedObjectScaleMultiplierActive ()
	{
		return useCustomGrabbedObjectScaleMultiplier;
	}

	public float getCustomGrabbedObjectScaleMultiplier ()
	{
		return customGrabbedObjectScaleMultiplier;
	}

	public bool isGrabObjectsEnabled ()
	{
		return grabObjectsEnabled;
	}

	public Transform getMountPointByName (string mountPointName)
	{
		int mountPointIndex = objectPointInfoList.FindIndex (s => s.Name.Equals (mountPointName));

		if (mountPointIndex > -1) {
			return objectPointInfoList [mountPointIndex].objectTransform;
		}

		return null;
	}

	public Transform getDefaultMountPoint ()
	{
		for (int i = 0; i < objectPointInfoList.Count; i++) {
			if (objectPointInfoList [i].usePointAsDefault) {
				return objectPointInfoList [i].objectTransform;
			}
	
		}

		return null;
	}

	public Transform getReferencePositionByName (string mountPointName, string referencePositionName)
	{
		int mountPointIndex = objectPointInfoList.FindIndex (s => s.Name.Equals (mountPointName));

		if (mountPointIndex > -1) {
			int referencePositionIndex =
				objectPointInfoList [mountPointIndex].referencePositionInfoList.FindIndex (s => s.Name.Equals (referencePositionName));

			if (referencePositionIndex > -1) {
				return objectPointInfoList [mountPointIndex].referencePositionInfoList [referencePositionIndex].referencePosition;
			}
		}

		return null;
	}

	[System.Serializable]
	public class objectPointInfo
	{
		public string Name;
		public Transform objectTransform;

		public bool usePointAsDefault;

		[Space]

		public List<referencePositionInfo> referencePositionInfoList = new List<referencePositionInfo> ();
	}

	[System.Serializable]
	public class referencePositionInfo
	{
		public string Name;
		public Transform referencePosition;
	}
}
