using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class simpleLightingModifier : MonoBehaviour
{
	[Header ("Main Settings")]
	[Space]

	public bool lightingAmbientEquatorSettingEnabled = true;

	[Space]
	[Header ("Debug")]
	[Space]

	public bool showDebugPrint;


	public void setAmbientColor (Color newColor)
	{
		if (!lightingAmbientEquatorSettingEnabled) {
			return;
		}

		if (showDebugPrint) {
			print (newColor);
		}

		RenderSettings.ambientLight = newColor;
		RenderSettings.ambientEquatorColor = newColor;
		RenderSettings.ambientGroundColor = newColor;
	}

	public void setAmbientColor (Vector4 newValue)
	{
		if (!lightingAmbientEquatorSettingEnabled) {
			return;
		}

		Color newColor = RenderSettings.ambientEquatorColor;

		newColor.r = newValue.x;
		newColor.g = newValue.y;
		newColor.b = newValue.z;
		newColor.a = newValue.w;

		setAmbientColor (newColor);
	}

	public void setAmbientColor (float newValue)
	{
		setAmbientColor (new Vector4 (newValue, newValue, newValue, newValue));
	}
}
