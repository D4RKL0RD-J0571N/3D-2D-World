using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class customCameraEffect : cameraEffect
{
	public Material mainMaterial;

	public bool useReleaseMethodEnabled;

	public override void renderEffect (RenderTexture source, RenderTexture destination, Camera mainCamera)
	{
		if (useReleaseMethodEnabled) {
			var temporaryTexture = RenderTexture.GetTemporary (source.width, source.height);
			Graphics.Blit (source, temporaryTexture, mainMaterial, 0);
			Graphics.Blit (temporaryTexture, destination, mainMaterial, 1);
			RenderTexture.ReleaseTemporary (temporaryTexture);
		} else {
			Graphics.Blit (source, destination, mainMaterial);
		}
	}
}
