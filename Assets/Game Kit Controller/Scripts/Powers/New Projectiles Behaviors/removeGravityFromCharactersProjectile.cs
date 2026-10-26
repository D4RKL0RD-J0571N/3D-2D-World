using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameKitController.Audio;

public class removeGravityFromCharactersProjectile : projectileSystem
{
	[Header ("Custom Settings")]
	[Space]

	public string remoteEventName = "Remove Gravity From Character";

	public applyEffectOnArea mainAreaEffectOnArea;

	//when the bullet touchs a surface, then
	public void checkObjectDetected (Collider col)
	{
		if (canActivateEffect (col)) {
			if (currentProjectileInfo.impactAudioElement != null) {
				currentProjectileInfo.impactAudioElement.audioSource = GetComponent<AudioSource> ();
				AudioPlayer.PlayOneShot (currentProjectileInfo.impactAudioElement, gameObject);
			}

			setProjectileUsedState (true);

			mainAreaEffectOnArea.gameObject.SetActive (true);

			mainAreaEffectOnArea.setEffectActiveState (true);

			mainRigidbody.isKinematic = true;

			creatImpactParticles ();

			disableBullet (currentProjectileInfo.impactDisableTimer);
		}
	}

	public override void resetProjectile ()
	{
		base.resetProjectile ();

		mainAreaEffectOnArea.gameObject.SetActive (false);

		mainAreaEffectOnArea.setEffectActiveState (false);
	}

	public void applyEffect (GameObject objectToAffect)
	{
		if (objectToAffect != null) {
			remoteEventSystem currentRemoteEventSystem = objectToAffect.GetComponent<remoteEventSystem> ();

			if (currentRemoteEventSystem == null) {
				playerComponentsManager currentplayerComponentsManager = objectToAffect.GetComponent<playerComponentsManager> ();

				if (currentplayerComponentsManager != null) {
					currentRemoteEventSystem = currentplayerComponentsManager.getRemoteEventSystem ();
				}
			}

            if (currentRemoteEventSystem != null) {
                currentRemoteEventSystem.callRemoteEvent (remoteEventName);
            }
        }
	}
}