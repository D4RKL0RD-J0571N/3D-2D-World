using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIAimRotationManager : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool AIAimRotationEnabled = true;

    public bool setAIAimRotationStateOnAwake;

    public string AIAimRotationStateOnAwakeName;

    [Space]
    [Header ("AI Aim Rotation States List Settings")]
    [Space]

    public List<AIAimRotationInfo> AIAimRotationInfoList = new List<AIAimRotationInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool randomAimPositionOffsetActive;
    public string currentAIAimRotationInfoName;

    [Space]
    [Header ("Components")]
    [Space]

    public playerCamera mainPlayerCamera;
    public playerController mainPlayerController;
    public findObjectivesSystem mainFindObjectivesSystem;


    float lastTimeRandomAimPositionOffsetActive = 0;

    float currentWaitTimeRandomAimPositionOffset;
    float currentRandomAimPositionOffset;
    float currentRandomAimPositionOffsetDuration;

    float currentMinRandomAimPositionOffset;

    bool currentUseMinRandomAimPositionOffset;

    Coroutine updateCoroutine;

    Vector2 randomAimPositionOffsetRange;
    Vector2 randomAimPositionOffsetDurationRange;
    Vector2 randomAimPositionOffsetWaitRange;

    void Awake ()
    {
        if (setAIAimRotationStateOnAwake) {
            setAIAImRotationState (AIAimRotationStateOnAwakeName);
        }
    }

    public void stopUpdateCoroutine ()
    {
        if (updateCoroutine != null) {
            StopCoroutine (updateCoroutine);
        }

        randomAimPositionOffsetActive = false;
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
        if (!randomAimPositionOffsetActive) {
            return;
        }

        if (!mainFindObjectivesSystem.isOnSpotted ()) {
            if (currentWaitTimeRandomAimPositionOffset != 0) {
                if (showDebugPrint) {
                    print ("character is not attacking a target, reseting random aim position offset state");
                }

                disableAndResetRandomAimPositionValues ();
            }

            return;
        }

        if (currentWaitTimeRandomAimPositionOffset == 0) {
            currentWaitTimeRandomAimPositionOffset = Random.Range (randomAimPositionOffsetWaitRange.x, randomAimPositionOffsetWaitRange.y);

            currentRandomAimPositionOffset = Random.Range (randomAimPositionOffsetRange.x, randomAimPositionOffsetRange.y);

            if (currentUseMinRandomAimPositionOffset) {
                if (currentMinRandomAimPositionOffset != 0) {
                    if (currentRandomAimPositionOffset > 0) {
                        if (currentRandomAimPositionOffset < currentMinRandomAimPositionOffset) {
                            currentRandomAimPositionOffset = currentMinRandomAimPositionOffset;
                        }
                    } else {
                        if (Mathf.Abs (currentRandomAimPositionOffset) < currentMinRandomAimPositionOffset) {
                            currentRandomAimPositionOffset = -currentMinRandomAimPositionOffset;
                        }
                    }
                }
            }

            lastTimeRandomAimPositionOffsetActive = Time.time;

            if (showDebugPrint) {
                print ("select wait time " + currentWaitTimeRandomAimPositionOffset);
            }
        } else {
            if (Time.time > lastTimeRandomAimPositionOffsetActive + currentWaitTimeRandomAimPositionOffset) {
                if (currentRandomAimPositionOffsetDuration == 0) {
                    currentRandomAimPositionOffsetDuration = Random.Range (randomAimPositionOffsetDurationRange.x, randomAimPositionOffsetDurationRange.y);

                    mainFindObjectivesSystem.addLookDirectionToTargetOffset (currentRandomAimPositionOffset);

                    lastTimeRandomAimPositionOffsetActive = Time.time;

                    if (showDebugPrint) {
                        print ("select offset duration " + currentRandomAimPositionOffsetDuration);

                        print ("current random position offset " + currentRandomAimPositionOffset);
                    }
                }
            }

            if (currentRandomAimPositionOffsetDuration != 0) {
                if (Time.time > lastTimeRandomAimPositionOffsetActive + currentRandomAimPositionOffsetDuration) {
                    disableAndResetRandomAimPositionValues ();
                }
            }
        }
    }

    void disableAndResetRandomAimPositionValues ()
    {
        mainFindObjectivesSystem.addLookDirectionToTargetOffset (0);

        lastTimeRandomAimPositionOffsetActive = Time.time;

        currentWaitTimeRandomAimPositionOffset = 0;

        currentRandomAimPositionOffsetDuration = 0;

        if (showDebugPrint) {
            print ("reset values");
        }
    }

    public void setAIAImRotationState (string stateName)
    {
        if (!AIAimRotationEnabled) {
            return;
        }

        int newIndex = AIAimRotationInfoList.FindIndex (s => s.Name.Equals (stateName));

        if (newIndex > -1) {
            AIAimRotationInfo currentInfo = AIAimRotationInfoList [newIndex];

            if (currentInfo.stateEnabled) {
                currentAIAimRotationInfoName = currentInfo.Name;

                mainPlayerCamera.changeRotationSpeedValue (currentInfo.verticalAimRotationSpeed, currentInfo.horizontalAimRotationSpeed);

                mainPlayerCamera.updateOriginalRotationSpeedValues ();

                if (currentInfo.setRotateDirectlyTowardCameraOnStrafe) {
                    mainPlayerController.setRotateDirectlyTowardCameraOnStrafeState (currentInfo.rotateDirectlyTowardCameraOnStrafe);
                }

                if (currentInfo.setLookAtTargetSpeed) {
                    mainFindObjectivesSystem.setLookAtTargetSpeedValue (currentInfo.lookAtTargetSpeed);
                }

                if (currentInfo.setAutoTurnSpeed) {
                    mainPlayerController.setAutoTurnSpeed (currentInfo.autoTurnSpeed);
                }

                if (currentInfo.setAimTurnSpeed) {
                    mainPlayerController.setAimTurnSpeed (currentInfo.aimTurnSpeed);
                }

                if (randomAimPositionOffsetActive) {
                    stopUpdateCoroutine ();
                }

                if (currentInfo.addRandomAimPositionOffset) {
                    randomAimPositionOffsetActive = true;

                    updateCoroutine = StartCoroutine (updateSystemCoroutine ());

                    randomAimPositionOffsetRange = currentInfo.randomAimPositionOffsetRange;
                    randomAimPositionOffsetDurationRange = currentInfo.randomAimPositionOffsetDurationRange;
                    randomAimPositionOffsetWaitRange = currentInfo.randomAimPositionOffsetWaitRange;

                    currentMinRandomAimPositionOffset = currentInfo.minRandomAimPositionOffset;

                    currentUseMinRandomAimPositionOffset = currentInfo.useMinRandomAimPositionOffset;

                    lastTimeRandomAimPositionOffsetActive = 0;

                    currentWaitTimeRandomAimPositionOffset = 0;
                } else {
                    mainFindObjectivesSystem.addLookDirectionToTargetOffset (0);
                }

                if (showDebugPrint) {
                    print ("setting aim state " + currentAIAimRotationInfoName);
                }
            }
        }
    }

    public void setAIAimRotationEnabledState (bool state)
    {
        AIAimRotationEnabled = state;
    }

    public void setAIAimRotationEnabledStateFromEditor (bool state)
    {
        setAIAimRotationEnabledState (state);

        updateComponent ();
    }

    public void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update AI Aim Rotation Manager " + gameObject.name, gameObject);
    }


    [System.Serializable]
    public class AIAimRotationInfo
    {
        [Header ("Aiming Rotation Speed Settings")]
        [Space]

        public string Name;
        public bool stateEnabled = true;

        [Space]

        public bool setAimRotationSpeed;
        public float verticalAimRotationSpeed;
        public float horizontalAimRotationSpeed;

        [Space]

        public bool setRotateDirectlyTowardCameraOnStrafe;
        public bool rotateDirectlyTowardCameraOnStrafe;

        [Space]

        public bool setLookAtTargetSpeed;
        public float lookAtTargetSpeed;

        [Space]

        public bool setAutoTurnSpeed;
        public float autoTurnSpeed;

        [Space]

        public bool setAimTurnSpeed;
        public float aimTurnSpeed;

        [Space]

        public bool addRandomAimPositionOffset;
        public Vector2 randomAimPositionOffsetDurationRange;
        public Vector2 randomAimPositionOffsetWaitRange;

        [Space]

        public Vector2 randomAimPositionOffsetRange;

        public bool useMinRandomAimPositionOffset;
        public float minRandomAimPositionOffset;
    }
}
