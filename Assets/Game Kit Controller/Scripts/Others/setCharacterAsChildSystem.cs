using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class setCharacterAsChildSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool parentingEnabled = true;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public bool useEventOnStateChange;

    public UnityEvent eventOnActivateChildState;
    public UnityEvent eventOnDeactivateChildState;

    public UnityEvent eventOnStopParentingFromCharactersCheckState;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool parentingActive;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainCharacter;
    public playerController childCharacter;

    public Transform childParentPosition;
    public Transform mainChildParent;

    public playerInputManager childInputManager;

    public OnAnimatorIKComponent mainCharacterIKComponent;

    public Rigidbody childRigidbody;

    Rigidbody mainCharacterRigidbody;

    IKSystem mainIKSystem;

    Coroutine updateCoroutine;


    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }
    }

    IEnumerator updateSystemCoroutine ()
    {
        var waitTime = new WaitForFixedUpdate ();

        while (true) {
            updateSystem ();

            yield return waitTime;
        }
    }

    void updateSystem ()
    {
        if (mainCharacter.isPlayerOnGround ()) {
            float forwardAmount = mainCharacter.getForwardAmount ();

            childCharacter.setForwardAmountExternally (forwardAmount);
        }

        childRigidbody.linearVelocity = (mainCharacterRigidbody.linearVelocity);

        childInputManager.overrideInputValues (mainCharacter.getAxisValues (), true);

        childCharacter.transform.localPosition = Vector3.zero;
        childCharacter.transform.localRotation = Quaternion.identity;

        if (mainCharacter.isPlayerDead () || childCharacter.isPlayerDead ()) {
            enableOrDisableParenting (false);

            eventOnStopParentingFromCharactersCheckState.Invoke ();

            return;
        }
    }

    public void toggleParenting ()
    {
        enableOrDisableParenting (!parentingActive);
    }

    public void enableOrDisableParenting (bool state)
    {
        if (mainCharacter == null || childCharacter == null) {
            return;
        }

        parentingActive = state;

        Collider childPlayerCollider = childCharacter.getMainCollider ();

        if (state) {
            mainChildParent.SetParent (mainCharacter.transform);
            mainChildParent.localPosition = Vector3.zero;
            mainChildParent.localRotation = Quaternion.identity;

            childCharacter.transform.SetParent (childParentPosition);

            childCharacter.transform.localPosition = Vector3.zero;
            childCharacter.transform.localRotation = Quaternion.identity;

            childInputManager.setMovementAxisInputPausedState (true);

        } else {
            childInputManager.setMovementAxisInputPausedState (false);

            mainChildParent.SetParent (transform);

            childCharacter.transform.SetParent (null);

            stopUpdateCoroutine ();

            childCharacter.setExtraCharacterVelocity (Vector3.zero);
        }

        childCharacter.getRigidbody ().isKinematic = state;

        List<Collider> colliderList = childCharacter.getExtraColliderList ();

        for (int i = 0; i < colliderList.Count; i++) {
            mainCharacter.setIgnoreCollisionOnExternalCollider (colliderList [i], state);
        }

        mainCharacter.setIgnoreCollisionOnExternalCollider (childPlayerCollider, state);

        mainCharacter.setIgnoreCollisionOnExternalCollider (childCharacter.getPlayerCameraManager ().getHeadColliderOnFBA (), state);

        childCharacter.setActionActiveWithMovementState (state);

        if (!state) {
            childInputManager.overrideInputValues (Vector2.zero, false);
        }

        if (state) {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        }

        if (state) {
            mainCharacterIKComponent.setCharacterElements (mainCharacter.gameObject);

            mainIKSystem.setTemporalOnAnimatorIKComponentActiveIfNotInUse (mainCharacterIKComponent);
        } else {
            mainIKSystem.removeThisTemporalOnAnimatorIKComponentIfIsCurrent (mainCharacterIKComponent);
        }

        checkEventOnStateChange (state);
    }

    public void setMainCharacter (GameObject newCharacter)
    {
        playerController currentPlayerController = newCharacter.GetComponent<playerController> ();

        if (childCharacter != null && currentPlayerController == childCharacter) {
            playerComponentsManager currentPlayerComponentsManager = newCharacter.GetComponent<playerComponentsManager> ();

            if (currentPlayerComponentsManager != null) {
                usingDevicesSystem currentUsingDeviceSystem = currentPlayerComponentsManager.getUsingDevicesSystem ();

                if (currentUsingDeviceSystem != null) {
                    currentUsingDeviceSystem.removeDeviceFromList (gameObject);
                }
            }

            return;
        }

        mainCharacter = currentPlayerController;

        mainIKSystem = currentPlayerController.IKSystemManager;

        mainCharacterRigidbody = mainCharacter.getRigidbody ();
    }

    public void setChildCharacter (GameObject newCharacter)
    {
        playerController currentPlayerController = newCharacter.GetComponent<playerController> ();

        childCharacter = currentPlayerController;

        if (childCharacter != null) {
            childInputManager = childCharacter.getPlayerInput ();
        }
    }

    void checkEventOnStateChange (bool state)
    {
        if (useEventOnStateChange) {
            if (state) {
                eventOnActivateChildState.Invoke ();
            } else {
                eventOnDeactivateChildState.Invoke ();
            }
        }
    }
}
