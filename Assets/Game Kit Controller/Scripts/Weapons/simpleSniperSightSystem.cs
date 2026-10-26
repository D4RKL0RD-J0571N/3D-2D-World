using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simpleSniperSightSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool sniperSightThirdPersonEnabled = true;
    public bool sniperSightFirstPersonEnabled = true;

    public bool sniperSightThirdPersonWithoutAimEnabled;

    [Space]

    public string sniperCameraThirdPersonStateName = "Sniper Camera View Third Person";
    public string sniperCameraFirstPersonStateName = "Sniper Camera View First Person";

    public string sniperCameraFBAStateName = "Sniper Camera View FBA";

    [Space]

    public string sniperCameraThirdPersonCrouchStateName = "Sniper Camera View Third Person Crouch";
    public string sniperCameraFirstPersonCrouchStateName = "Sniper Camera View First Person Crouch";

    public string sniperCameraFBACrouchStateName = "Sniper Camera View FBA Crouch";

    [Space]

    public float newCameraFovValueThirdPerson = 20;

    public float newCameraFovValueFirstPerson = 15;

    public bool changeFovSmoothly;
    public float changeFovSpeed;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool sniperSightActive;

    public string previuousCameraState;

    public bool sniperSightActivePreviously;

    [Space]
    [Header ("Components")]
    [Space]

    public GameObject sniperSightPanelGameObject;
    public playerCamera mainPlayerCamera;
    public playerController mainPlayerController;
    public playerWeaponsManager mainPlayerWeaponsManager;
    public Camera weaponsCamera;

    Coroutine sniperSightCoroutine;

    public void enableOrDisableSniperSight (bool state)
    {
        bool isFirstPersonActive = mainPlayerCamera.isFirstPersonActive ();

        if (isFirstPersonActive || mainPlayerCamera.isFullBodyAwarenessActive ()) {
            if (!sniperSightFirstPersonEnabled) {
                return;
            }
        } else {
            if (!sniperSightThirdPersonEnabled) {
                return;
            }
        }

        if (!mainPlayerCamera.isCameraTypeFree ()) {
            return;
        }

        if (mainPlayerCamera.isFullBodyAwarenessActive ()) {
            if (mainPlayerWeaponsManager != null) {
                bool setSniperSightResult = true;

                if (state) {
                    if (!mainPlayerWeaponsManager.isAimModeInputPressed ()) {
                        setSniperSightResult = false;
                    }
                } else {
                    if (!sniperSightActive) {
                        setSniperSightResult = false;
                    }
                }

                if (!setSniperSightResult) {
                    return;
                }
            }
        } else {
            if (mainPlayerWeaponsManager != null) {
                bool setSniperSightResult = true;

                if (!sniperSightThirdPersonWithoutAimEnabled) {
                    if (state) {
                        if (!mainPlayerWeaponsManager.isAimModeInputPressed ()) {
                            setSniperSightResult = false;
                        }
                    } else {
                        if (!sniperSightActive) {
                            setSniperSightResult = false;
                        }
                    }
                }

                if (!setSniperSightResult) {
                    return;
                }
            }
        }

        setSniperSightState (state, isFirstPersonActive);
    }

    void setSniperSightState (bool state, bool isFirstPersonActive)
    {
        sniperSightActive = state;

        if (sniperSightPanelGameObject.activeSelf != sniperSightActive) {
            sniperSightPanelGameObject.SetActive (sniperSightActive);
        }

        if (sniperSightActive) {
            previuousCameraState = mainPlayerCamera.getCurrentStateName ();

            if (sniperSightCoroutine != null) {
                StopCoroutine (sniperSightCoroutine);
            }

            sniperSightCoroutine = StartCoroutine (enableSniperSightCoroutine ());

        } else {
            if (previuousCameraState != "") {
                mainPlayerCamera.setCameraStateExternally (previuousCameraState);

                setCameraStateValues ();

                if (isFirstPersonActive) {
                    weaponsCamera.enabled = true;
                }

                setUseCustomCameraStateActiveState (false);
            }

            previuousCameraState = "";
        }

        sniperSightActivePreviously = false;

        mainPlayerCamera.setPauseChangeCameraFovActiveState (sniperSightActive);

        mainPlayerCamera.setIgnoreMainCameraFovOnSetCameraState (sniperSightActive);
    }

    public void disableSniperSight ()
    {
        setSniperSightState (false, mainPlayerCamera.isFirstPersonActive ());
    }

    public void disableSniperSightIfActive ()
    {
        if (sniperSightActive) {
            disableSniperSight ();

            sniperSightActivePreviously = true;
        }
    }

    public void enableSniperSightIfActivePreviously ()
    {
        if (sniperSightActivePreviously) {
            if (mainPlayerController.isPlayerAiming ()) {
                enableOrDisableSniperSight (true);
            }
        }

        sniperSightActivePreviously = false;
    }


    IEnumerator enableSniperSightCoroutine ()
    {
        yield return new WaitForSeconds (0.001f);

        bool isFirstPersonActive = mainPlayerCamera.isFirstPersonActive ();

        if (isFirstPersonActive) {
            mainPlayerCamera.setCameraStateExternally (sniperCameraFirstPersonStateName);

            weaponsCamera.enabled = false;
        } else {
            if (mainPlayerCamera.isFullBodyAwarenessActive ()) {
                if (mainPlayerController.isCrouching ()) {
                    mainPlayerCamera.setCameraState (sniperCameraFBACrouchStateName);
                } else {
                    mainPlayerCamera.setCameraState (sniperCameraFBAStateName);
                }
            } else {
                if (mainPlayerController.isCrouching ()) {
                    mainPlayerCamera.setCameraStateExternally (sniperCameraThirdPersonCrouchStateName);
                } else {
                    mainPlayerCamera.setCameraStateExternally (sniperCameraThirdPersonStateName);
                }
            }

            setUseCustomCameraStateActiveState (true);
        }

        setCameraStateValues ();

        mainPlayerCamera.stopFovChangeCoroutine ();

        mainPlayerCamera.stopCameraFovStartAndEndCoroutine ();

        float currentCameraFoValue = newCameraFovValueThirdPerson;

        if (mainPlayerCamera.isFullBodyAwarenessActive () || mainPlayerCamera.isFirstPersonActive ()) {
            currentCameraFoValue = newCameraFovValueFirstPerson;
        }

        if (changeFovSmoothly) {
            mainPlayerCamera.setMainCameraFov (currentCameraFoValue, changeFovSpeed);
        } else {
            mainPlayerCamera.quickChangeFovValue (currentCameraFoValue);
        }
    }

    void setCameraStateValues ()
    {
        mainPlayerCamera.resetCurrentCameraStateAtOnce ();

        mainPlayerCamera.configureCameraAndPivotPositionAtOnce ();

        mainPlayerCamera.quickChangeFovValue (mainPlayerCamera.getOriginalCameraFov ());
    }

    void setUseCustomCameraStateActiveState (bool state)
    {
        if (!state) {
            mainPlayerCamera.setUseCustomThirdPersonAimActivePausedState (false);
        }

        if (mainPlayerCamera.isFullBodyAwarenessActive ()) {
            mainPlayerCamera.setUseCustomThirdPersonAimActiveState (state,
                sniperCameraFBAStateName,
                sniperCameraFBAStateName);

            mainPlayerCamera.setUseCustomThirdPersonAimCrouchActiveState (state,
                sniperCameraFBACrouchStateName,
                sniperCameraFBACrouchStateName);
        } else {
            mainPlayerCamera.setUseCustomThirdPersonAimActiveState (state,
              sniperCameraThirdPersonStateName,
              sniperCameraThirdPersonStateName);

            mainPlayerCamera.setUseCustomThirdPersonAimCrouchActiveState (state,
                sniperCameraThirdPersonCrouchStateName,
                sniperCameraThirdPersonCrouchStateName);
        }

        mainPlayerCamera.setCheckUseCustomFBAAimActiveState (state);

        if (state) {
            mainPlayerCamera.setUseCustomThirdPersonAimActivePausedState (true);
        }
    }
}
