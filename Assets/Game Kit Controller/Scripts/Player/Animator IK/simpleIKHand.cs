using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simpleIKHand : OnAnimatorIKComponent
{
    [Header ("Main Settings")]
    [Space]

    public bool simpleIKHandActive;

    public float IKWeight;

    public AvatarIKGoal handIKGoal;

    [Space]
    [Header ("Components")]
    [Space]

    public Animator mainAnimator;

    public Transform IKPositionTransform;

    public override void updateOnAnimatorIKState ()
    {
        if (simpleIKHandActive) {
            mainAnimator.SetIKRotationWeight (handIKGoal, IKWeight);
            mainAnimator.SetIKPositionWeight (handIKGoal, IKWeight);
            mainAnimator.SetIKPosition (handIKGoal, IKPositionTransform.position);
            mainAnimator.SetIKRotation (handIKGoal, IKPositionTransform.rotation);
        }
    }

    public override void setActiveState (bool state)
    {
        simpleIKHandActive = state;
    }

    public override void setCharacterElements (GameObject newCharacter)
    {
        playerComponentsManager currentPlayerComponentsManager = newCharacter.GetComponent<playerComponentsManager> ();

        if (currentPlayerComponentsManager != null) {
            mainAnimator = currentPlayerComponentsManager.getPlayerController ().getCharacterAnimator ();
        }
    }

}
