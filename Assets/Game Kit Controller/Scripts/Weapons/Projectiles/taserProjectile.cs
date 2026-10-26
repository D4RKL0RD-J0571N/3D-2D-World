using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GameKitController.Audio;

public class taserProjectile : projectileSystem
{
    [Header ("Main Settings")]
    [Space]

    public LayerMask layer;

    [Space]

    public bool useLayerMaskToIgnore;

    public LayerMask layerMaskToIgnore;

    [Space]
    [Header ("Ragdoll Settings")]
    [Space]

    public float sedateDelay;
    public bool useWeakSpotToReduceDelay;
    public bool sedateUntilReceiveDamage;
    public float sedateDuration;

    public bool useActionSystemOnImpact;
    public string actionSystemOnImpactName;


    //when the bullet touchs a surface, then
    public void checkObjectDetected (Collider col)
    {
        if (canActivateEffect (col)) {
            if (currentProjectileInfo.impactAudioElement != null) {
                currentProjectileInfo.impactAudioElement.audioSource = GetComponent<AudioSource> ();
                AudioPlayer.PlayOneShot (currentProjectileInfo.impactAudioElement, gameObject);
            }

            setProjectileUsedState (true);

            setProjectileScorch ();

            //creatImpactParticles ();

            //set the bullet kinematic
            objectToDamage = col.GetComponent<Collider> ().gameObject;

            mainRigidbody.isKinematic = true;

            if ((1 << col.gameObject.layer & layer.value) == 1 << col.gameObject.layer) {

                if (useLayerMaskToIgnore) {
                    if ((1 << col.gameObject.layer & layerMaskToIgnore.value) == 1 << col.gameObject.layer) {

                        setProjectileUsedState (false);

                        setIgnoreProjectileWithAbilityState (true);

                        checkSurface (col);

                        return;
                    }
                }

                bool isCharacter = applyDamage.isCharacter (objectToDamage);

                if (isCharacter) {
                    objectToDamage = applyDamage.getCharacter (objectToDamage);

                    applyDamage.sedateCharacter (objectToDamage, transform.position, sedateDelay, useWeakSpotToReduceDelay,
                        sedateUntilReceiveDamage, sedateDuration);

                    if (useActionSystemOnImpact) {
                        playerController currentPlayerController = objectToDamage.GetComponent<playerController> ();

                        if (currentPlayerController != null) {
                            currentPlayerController.activateCustomAction (actionSystemOnImpactName);
                        }
                    }
                }
            }

            checkProjectilesParent ();
        }
    }

    public void destroyObject ()
    {
        destroyProjectile ();
    }

    public override void resetProjectile ()
    {
        base.resetProjectile ();


    }
}