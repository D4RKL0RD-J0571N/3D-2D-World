using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class doubleJumpAnimationTrigger : MonoBehaviour
{
    [Header ("Ground Jump Main Settings")]
    [Space]

    public bool groundJumpAnimationEnabled;

    public float delayToEnterOnAirStateOnGroundJump = 0.3f;

    public string groundJumpInPlaceAnimationTriggerName = "Ground Jump In Place";

    public string groundJumpMovingAnimationTriggerName = "Ground Jump Moving";

    [Space]
    [Header ("Double Jump Settings")]
    [Space]

    public bool doubleJumpAnimationEnabled = true;

    public string doubleJumpAnimationTriggerName = "Double Jump";

    public float headTrackCanLookPauseDuration = 0.5f;

    public bool useMinWaitToUseAnimationAfterDoubleJump;
    public float minWaitToUseAnimationAfterDoubleJump = 0.5f;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool ignoreGroundJumpAnimationActive;

    public bool showDebugPrint;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;

    public headTrack mainHeadTrack;


    int doubleJumpAnimationTriggerID = 0;

    float lastTimeDoubleJump;

    int groundJumpInPlaceAnimationTriggerID = 0;

    int groundJumpMovingAnimationTriggerID = 0;


    void Start ()
    {
        mainPlayerController.enableOrDisableUseDelayOnGroundJumpState (groundJumpAnimationEnabled, delayToEnterOnAirStateOnGroundJump);
    }

    public void activateGroundJumpAnimation ()
    {
        if (ignoreGroundJumpAnimationActive) {
            if (showDebugPrint) {
                print ("ignoreGroundJumpAnimationActive");
            }

            return;
        }

        if (groundJumpAnimationEnabled) {
            bool canActivateAnimationResult = true;

            if (mainPlayerController.isCustomCharacterControllerActive ()) {
                canActivateAnimationResult = false;
            }

            if (mainPlayerController.isLastJumpActivatedExternally ()) {
                canActivateAnimationResult = false;
            }

            if (showDebugPrint) {
                print ("canActivateAnimationResult " + canActivateAnimationResult);
            }

            if (canActivateAnimationResult) {
                if (groundJumpInPlaceAnimationTriggerID == 0) {
                    groundJumpInPlaceAnimationTriggerID = Animator.StringToHash (groundJumpInPlaceAnimationTriggerName);
                }

                if (groundJumpMovingAnimationTriggerID == 0) {
                    groundJumpMovingAnimationTriggerID = Animator.StringToHash (groundJumpMovingAnimationTriggerName);
                }

                if (mainPlayerController.isPlayerMoving (0.2f)) {
                    mainPlayerController.setAnimatorTrigger (groundJumpMovingAnimationTriggerID);

                    if (showDebugPrint) {
                        print ("jump while moving");
                    }
                } else {
                    mainPlayerController.setAnimatorTrigger (groundJumpInPlaceAnimationTriggerID);

                    if (showDebugPrint) {
                        print ("jump in place");
                    }
                }
            }
        }
    }

    public void activateDoubleJumpAnimation ()
    {
        if (doubleJumpAnimationEnabled) {
            bool canActivateAnimationResult = true;

            if (mainPlayerController.isFullBodyAwarenessActive ()) {
                canActivateAnimationResult = false;
            }

            if (mainPlayerController.isPlayerUsingWeapons ()) {
                canActivateAnimationResult = false;
            }

            if (mainPlayerController.isCustomCharacterControllerActive ()) {
                canActivateAnimationResult = false;
            }

            if (!mainPlayerController.canPlayerMove ()) {
                canActivateAnimationResult = false;
            }

            //if (mainPlayerController.isGrabbingToSurfaceActive ()) {
            //    print ("grabbing");

            //    canActivateAnimationResult = false;
            //}

            if (mainPlayerController.isAjustingToSurfaceToGrabInProcess ()) {
                canActivateAnimationResult = false;
            }

            if (useMinWaitToUseAnimationAfterDoubleJump) {
                if (lastTimeDoubleJump > 0 && Time.time < lastTimeDoubleJump + minWaitToUseAnimationAfterDoubleJump) {
                    canActivateAnimationResult = false;
                }
            }

            if (canActivateAnimationResult) {
                if (doubleJumpAnimationTriggerID == 0) {
                    doubleJumpAnimationTriggerID = Animator.StringToHash (doubleJumpAnimationTriggerName);
                }

                mainHeadTrack.setPauseCanLookStateWithDuration (headTrackCanLookPauseDuration);

                mainPlayerController.setAnimatorTrigger (doubleJumpAnimationTriggerID);

                lastTimeDoubleJump = Time.time;
            }
        }
    }

    public void setIgnoreGroundJumpAnimationActiveState (bool state)
    {
        ignoreGroundJumpAnimationActive = state;

        if (showDebugPrint) {
            print ("ignoreGroundJumpAnimationActive " + ignoreGroundJumpAnimationActive);
        }
    }

    public void setGroundJumpAnimationEnabledState (bool state)
    {
        groundJumpAnimationEnabled = state;

        if (showDebugPrint) {
            print ("groundJumpAnimationEnabled " + groundJumpAnimationEnabled);
        }
    }

    public void setDoubleJumpAnimationEnabledState (bool state)
    {
        doubleJumpAnimationEnabled = state;
    }

    //EDITOR FUNCTIONS
    public void setGroundJumpAnimationEnabledStateFromEditor (bool state)
    {
        setGroundJumpAnimationEnabledState (state);

        updateComponent ();
    }

    public void setDoubleJumpAnimationEnabledStateFromEditor (bool state)
    {
        setDoubleJumpAnimationEnabledState (state);

        updateComponent ();
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Double Jump Animation System", gameObject);
    }
}
