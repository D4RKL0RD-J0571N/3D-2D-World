using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class dynamicReticleSystem : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool dynamicReticleEnbled = true;

    public bool useDelayToResetReticleSize;
    public float delayToResetReticleSize;

    [Space]
    [Header ("Reticle List Settings")]
    [Space]

    public List<dynamicReticleInfo> dynamicReticleInfoList = new List<dynamicReticleInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool reticleActive;

    public Vector2 currentReticleSizeDelta;

    public string currentReticleName;

    [Space]
    [Header ("Components")]
    [Space]

    public playerController mainPlayerController;
    public playerCamera mainPlayerCamera;
    public playerWeaponsManager mainPlayerWeaponsManager;


    dynamicReticleInfo currentDynamicReticleInfo;

    Coroutine updateCoroutine;

    RectTransform currentReticleRectTransform;

    float lastTimeIncreaseReticleActive;


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
        bool increaseReticleResult = false;

        currentReticleSizeDelta = currentDynamicReticleInfo.reticleMinDeltaSize;

        if (currentDynamicReticleInfo.addExtraDeltaSizeOnWalking) {
            if (mainPlayerController.isPlayerMoving (0.1f) && mainPlayerController.getRawAxisValues () != Vector2.zero) {
                currentReticleSizeDelta += currentDynamicReticleInfo.deltaSizeOnWalking;

                increaseReticleResult = true;
            }
        }

        if (currentDynamicReticleInfo.addExtraDeltaSizeOnRunning) {
            if (mainPlayerController.isPlayerRunning ()) {
                currentReticleSizeDelta += currentDynamicReticleInfo.deltaSizeOnRunning;

                increaseReticleResult = true;
            }
        }

        if (currentDynamicReticleInfo.addExtraDeltaSizeOnLooking) {
            if (mainPlayerCamera.isCameraRotating () && mainPlayerCamera.checkIfCameraRotatingWithTolerance (0.5f)) {
                currentReticleSizeDelta += currentDynamicReticleInfo.deltaSizeOnLooking;

                increaseReticleResult = true;
            }
        }

        if (currentDynamicReticleInfo.addExtraDeltaSizeOnAir) {
            if (!mainPlayerController.isPlayerOnGround ()) {
                currentReticleSizeDelta += currentDynamicReticleInfo.extraDeltaSizeOnAir;

                increaseReticleResult = true;
            }
        }

        if (currentDynamicReticleInfo.addExtraDeltaSizeOnShooting) {
            if (mainPlayerWeaponsManager.isCharacterShooting ()) {
                currentReticleSizeDelta += currentDynamicReticleInfo.extraDeltaSizeOnShooting;

                currentReticleRectTransform.sizeDelta += currentDynamicReticleInfo.extraDeltaSizeOnShooting;

                increaseReticleResult = true;
            }
        }

        if (useDelayToResetReticleSize) {
            if (!increaseReticleResult) {
                if (lastTimeIncreaseReticleActive != 0) {
                    if (Time.time < delayToResetReticleSize + lastTimeIncreaseReticleActive) {
                        return;
                    }
                }
            }
        }

        if (increaseReticleResult) {
            lastTimeIncreaseReticleActive = Time.time;

            if (currentDynamicReticleInfo.addSpreadToWeapon) {
                float sizeDeltaValue = (currentReticleRectTransform.sizeDelta.x - currentDynamicReticleInfo.reticleMinDeltaSize.x) /
                    (currentDynamicReticleInfo.reticleMaxDeltaSize.x - currentDynamicReticleInfo.reticleMinDeltaSize.x);

                float spreadAmount = currentDynamicReticleInfo.spreadToWeapon * sizeDeltaValue;

                spreadAmount = Mathf.Clamp (spreadAmount, currentDynamicReticleInfo.minSpreadToWeapon, currentDynamicReticleInfo.maxSpreadToWeapon);

                mainPlayerWeaponsManager.setExternalSpreadMultiplier (spreadAmount);
            }
        } else {
            if (currentDynamicReticleInfo.addSpreadToWeapon) {
                mainPlayerWeaponsManager.setExternalSpreadMultiplier (0);
            }
        }

        currentReticleRectTransform.sizeDelta = Vector2.Lerp (currentReticleRectTransform.sizeDelta,
            currentReticleSizeDelta, Time.fixedDeltaTime * currentDynamicReticleInfo.reticleScaleSpeed);

        Vector2 currentSizeDelta = currentReticleRectTransform.sizeDelta;

        currentSizeDelta.x = Mathf.Clamp (currentSizeDelta.x,
            currentDynamicReticleInfo.reticleMinDeltaSize.x, currentDynamicReticleInfo.reticleMaxDeltaSize.x);
        currentSizeDelta.y = Mathf.Clamp (currentSizeDelta.y,
             currentDynamicReticleInfo.reticleMinDeltaSize.y, currentDynamicReticleInfo.reticleMaxDeltaSize.y);

        currentReticleRectTransform.sizeDelta = currentSizeDelta;
    }

    public void enableOrDisableReticleByName (string reticleName, bool state)
    {
        if (!dynamicReticleEnbled) {
            return;
        }

        if (showDebugPrint) {
            print ("reticle state " + state + "  " + reticleName);
        }

        if (state) {
            if (!reticleName.Equals ("") && reticleName.Equals (currentReticleName)) {
                if (reticleActive) {
                    if (showDebugPrint) {
                        print ("trying to set the reticle which is already active, cancelling");
                    }

                    return;
                }
            }
        }

        if (reticleActive) {
            if (currentDynamicReticleInfo.reticleGameObject.activeSelf) {
                currentDynamicReticleInfo.reticleGameObject.SetActive (false);
            }

            if (currentDynamicReticleInfo.addSpreadToWeapon) {
                mainPlayerWeaponsManager.setExternalSpreadMultiplier (0);
            }
        }

        if (!state) {
            stopUpdateCoroutine ();

            reticleActive = false;

            return;
        }

        int currentIndex = dynamicReticleInfoList.FindIndex (s => s.Name.Equals (reticleName));

        if (currentIndex > -1) {
            currentDynamicReticleInfo = dynamicReticleInfoList [currentIndex];

            if (currentDynamicReticleInfo.reticleEnabled) {
                reticleActive = state;

                if (currentDynamicReticleInfo.reticleGameObject.activeSelf != reticleActive) {
                    currentDynamicReticleInfo.reticleGameObject.SetActive (reticleActive);
                }

                currentReticleRectTransform = currentDynamicReticleInfo.reticleRectTransform;

                currentReticleSizeDelta = currentDynamicReticleInfo.reticleMinDeltaSize;

                currentReticleRectTransform.sizeDelta = currentReticleSizeDelta;

                currentReticleName = currentDynamicReticleInfo.Name;

                lastTimeIncreaseReticleActive = 0;

                stopUpdateCoroutine ();

                if (reticleActive) {
                    updateCoroutine = StartCoroutine (updateSystemCoroutine ());
                }
            }
        }
    }

    public bool isReticleActive ()
    {
        return reticleActive;
    }


    [System.Serializable]
    public class dynamicReticleInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public bool reticleEnabled = true;

        public Vector2 reticleMinDeltaSize;
        public Vector2 reticleMaxDeltaSize;

        public float reticleScaleSpeed;

        [Space]
        [Header ("Size Settings")]
        [Space]

        public bool addExtraDeltaSizeOnWalking;
        public Vector2 deltaSizeOnWalking;

        public bool addExtraDeltaSizeOnRunning;
        public Vector2 deltaSizeOnRunning;

        public bool addExtraDeltaSizeOnLooking;
        public Vector2 deltaSizeOnLooking;

        public bool addExtraDeltaSizeOnShooting;
        public Vector2 extraDeltaSizeOnShooting;

        public bool addExtraDeltaSizeOnAir;
        public Vector2 extraDeltaSizeOnAir;

        [Space]
        [Header ("Components")]
        [Space]

        public bool addSpreadToWeapon;
        public float spreadToWeapon;

        public float minSpreadToWeapon;
        public float maxSpreadToWeapon;

        [Space]
        [Header ("Components")]
        [Space]

        public RectTransform reticleRectTransform;

        public GameObject reticleGameObject;

    }
}
