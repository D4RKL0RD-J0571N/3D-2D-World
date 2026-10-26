using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class simpleChainSaw : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public Transform mainRotationPivot;

    public float initialPivotRotation;

    public bool useFixedVerticalHorizontalRotations;

    public float angleRotationAmount;

    public float rotationSpeed;

    public float manualRotationAmount;

    public float maxRotationAngle = 80;

    public float minRotationAngle = -10;

    [Space]
    [Header ("Weapon Settings")]
    [Space]

    public bool useFuelEnabled = true;
    public float useFuelRate;
    public int amountFuelUsed;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool chainsawActive;

    [Space]
    [Header ("Events Settings")]
    [Space]

    public UnityEvent eventOnActivateChainsaw;

    public UnityEvent eventOnDeactivateChainsaw;

    public UnityEvent eventOnRotateWeapon;

    [Space]
    [Header ("Components")]
    [Space]

    public playerWeaponSystem weaponManager;


    bool rotating;

    bool rotationToggleState;

    Coroutine rotationCoroutine;

    Coroutine updateCoroutine;

    bool isWeaponReloading;

    bool remainAmmoInClip;

    bool reloading;

    float lastTimeUsed;

    bool ignoreDisableCoroutine;


    void Start ()
    {
        mainRotationPivot.localEulerAngles = new Vector3 (0, 0, initialPivotRotation);
    }

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
        isWeaponReloading = weaponManager.isWeaponReloading ();

        remainAmmoInClip = weaponManager.remainAmmoInClip ();

        if (reloading) {
            if (remainAmmoInClip && weaponManager.carryingWeapon () && !isWeaponReloading) {
                reloading = false;
            } else {
                return;
            }
        }

        if (reloading) {
            return;
        }

        if (!chainsawActive) {
            return;
        }

        if (useFuelEnabled) {
            if (Time.time > lastTimeUsed + useFuelRate) {
                if (remainAmmoInClip && !isWeaponReloading) {
                    lastTimeUsed = Time.time;

                    weaponManager.useAmmo (amountFuelUsed);

                    weaponManager.checkToUpdateInventoryWeaponAmmoTextFromWeaponSystem ();
                }

                if (!remainAmmoInClip || isWeaponReloading) {
                    ignoreDisableCoroutine = true;

                    activateOrDeactivateChainsaw (false);

                    ignoreDisableCoroutine = false;

                    reloading = true;

                    return;
                }
            }
        }
    }

    public void activateChainSaw ()
    {
        activateOrDeactivateChainsaw (true);
    }

    public void deactivateChainSaw ()
    {
        activateOrDeactivateChainsaw (false);
    }

    void activateOrDeactivateChainsaw (bool state)
    {
        if (chainsawActive == state) {
            return;
        }

        chainsawActive = state;

        if (chainsawActive) {
            eventOnActivateChainsaw.Invoke ();
        } else {
            eventOnDeactivateChainsaw.Invoke ();
        }

        if (ignoreDisableCoroutine) {
            return;
        }

        if (chainsawActive) {
            updateCoroutine = StartCoroutine (updateSystemCoroutine ());
        } else {
            stopUpdateCoroutine ();
        }
    }

    public void rotatePivotToRight ()
    {
        mainRotationPivot.Rotate (manualRotationAmount * Vector3.forward);

        checkMainRotationPivotMaxRotationAngle ();
    }

    public void rotatePivotToLeft ()
    {
        mainRotationPivot.Rotate (manualRotationAmount * (-Vector3.forward));

        checkMainRotationPivotMaxRotationAngle ();
    }

    void checkMainRotationPivotMaxRotationAngle ()
    {
        if (maxRotationAngle == 0) {
            return;
        }

        Vector3 mainRotationPivotEulerAngles = mainRotationPivot.localEulerAngles;

        float newZAngle = mainRotationPivotEulerAngles.z;

        if (newZAngle > 0 && newZAngle < 180) {
            newZAngle = Mathf.Clamp (newZAngle, 0, maxRotationAngle);

        } else {
            if (newZAngle > 180) {
                newZAngle -= 360;
            }

            newZAngle = Mathf.Clamp (newZAngle, minRotationAngle, 0);
        }

        mainRotationPivot.localEulerAngles = new Vector3 (mainRotationPivotEulerAngles.x, mainRotationPivotEulerAngles.y, newZAngle);
    }

    public void changePivotRotation ()
    {
        if (rotating) {
            return;
        }

        float rotationAmount = 0;

        if (useFixedVerticalHorizontalRotations) {
            rotationToggleState = !rotationToggleState;

            if (rotationToggleState) {
                rotationAmount = 90;
            } else {
                rotationAmount = 0;
            }
        } else {
            rotationAmount += angleRotationAmount;

            if (rotationAmount > 360) {
                rotationAmount = 360 - rotationAmount;
            }
        }

        eventOnRotateWeapon.Invoke ();

        stopChangePivotRotation ();

        rotationCoroutine = StartCoroutine (stopChangePivotRotationCoroutine (rotationAmount));
    }

    public void stopChangePivotRotation ()
    {
        if (rotationCoroutine != null) {
            StopCoroutine (rotationCoroutine);
        }
    }

    IEnumerator stopChangePivotRotationCoroutine (float rotationAmount)
    {
        rotating = true;

        Vector3 eulerTarget = Vector3.zero;

        eulerTarget.z = rotationAmount;

        Quaternion rotationTarget = Quaternion.Euler (eulerTarget);

        float t = 0;

        float movementTimer = 0;

        bool targetReached = false;

        float angleDifference = 0;

        while (!targetReached) {
            t += Time.deltaTime * rotationSpeed;
            mainRotationPivot.localRotation = Quaternion.Lerp (mainRotationPivot.localRotation, rotationTarget, t);

            movementTimer += Time.deltaTime;

            angleDifference = Quaternion.Angle (mainRotationPivot.localRotation, rotationTarget);

            movementTimer += Time.deltaTime;

            if (angleDifference < 0.2f || movementTimer > 2) {
                targetReached = true;
            }
            yield return null;
        }

        rotating = false;
    }
}
