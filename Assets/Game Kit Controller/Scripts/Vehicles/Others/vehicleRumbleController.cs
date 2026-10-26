using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class vehicleRumbleController : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool enableRumble = true;

    public float smoothing = 10f;

    [Range (0, 2)] public float vibrationMultiplier = 1;

    [Space]
    [Header ("Impact Settings")]
    [Space]

    public float collisionThreshold = 5f;

    [Space]
    [Header ("Vibration States List Settings")]
    [Space]

    public List<rumbleData> rumbleDataList = new List<rumbleData> ();

    [Space]
    [Header ("Damage Received Settings")]
    [Space]

    public float damageReceivedMultiplier = 0.05f;

    public float damageReceivedMinLow = 0.15f;
    public float damageReceivedMaxLow = 1f;

    public float damageReceivedMinHigh = 0.08f;
    public float damageReceivedMaxHigh = 0.7f;

    public float damageReceivedDuration = 0.2f;

    [Space]
    [Header ("Landing Settings")]
    [Space]

    public float landingMinVelocity = 2f;
    public float landingMaxVelocity = 18f;

    public float landingLowMin = 0.2f;
    public float landingLowMax = 0.9f;

    public float landingHighMin = 0.05f;
    public float landingHighMax = 0.25f;

    public float landingDurationMin = 0.05f;
    public float landingDurationMax = 0.2f;

    [Space]
    [Header ("Debug")]
    [Space]

    public bool vehicleRumbleActive;

    public bool gameIsPaused;

    public bool isGrounded;

    [Space]
    [Header ("Components")]
    [Space]

    public bool mainSimpleGamepadVibrationHelperLocated;

    public Rigidbody rb;

    public simpleGamepadVibrationHelper mainSimpleGamepadVibrationHelper;

    // =================================================
    // RUNTIME
    // =================================================

    protected float currentLow;
    protected float currentHigh;

    protected float targetLow;
    protected float targetHigh;

    protected bool wasGrounded;

    // =================================================
    // IMPACT LAYER
    // =================================================

    protected float impactLow;
    protected float impactHigh;
    protected float impactTimer;

    // =================================================
    // WEAPON LAYER
    // =================================================

    protected float weaponLow;
    protected float weaponHigh;
    protected float weaponTimer;

    // =================================================
    // DICTIONARY
    // =================================================

    protected Dictionary<int, rumbleData> rumbleDataDict;

    protected bool rumbleDataDictInitialized;

    protected bool vibrationStoppedChecked;

    // =================================================
    // PAUSE
    // =================================================

    public void setGameIsPausedState (bool state)
    {
        gameIsPaused = state;
    }

    // =================================================
    // MAIN UPDATE
    // =================================================

    public void updateVehicleControllerRumble ()
    {
        if (!enableRumble) {

            stopRumble ();

            return;
        }

        if (!mainSimpleGamepadVibrationHelperLocated) {
            return;
        }

        if (!mainSimpleGamepadVibrationHelper
            .isUsingGamepad ()) {
            return;
        }

        if (gameIsPaused) {

            if (!vibrationStoppedChecked) {

                stopRumble ();

                vibrationStoppedChecked = true;
            }

            return;

        } else {

            vibrationStoppedChecked = false;
        }

        updateVehicleStates ();

        updateImpacts ();

        updateWeaponLayer ();

        applyFinalRumble ();
    }

    // =================================================
    // CHILD IMPLEMENTS THIS
    // =================================================

    protected abstract void updateVehicleStates ();

    // =================================================
    // COMMON RUMBLE OUTPUT
    // =================================================

    protected void setCurrentRumble (float low, float high)
    {
        targetLow =
            Mathf.Clamp01 (low);

        targetHigh =
            Mathf.Clamp01 (high);

        currentLow =
            Mathf.Lerp (
                currentLow,
                targetLow,
                Time.deltaTime * smoothing
            );

        currentHigh =
            Mathf.Lerp (
                currentHigh,
                targetHigh,
                Time.deltaTime * smoothing
            );
    }

    // =================================================
    // IMPACTS
    // =================================================

    protected virtual void updateImpacts ()
    {
        if (impactTimer > 0f) {

            impactTimer -= Time.deltaTime;

            if (impactTimer <= 0f) {

                impactLow = 0f;

                impactHigh = 0f;
            }
        }

        handleLanding ();
    }

    void handleLanding ()
    {
        if (!wasGrounded && isGrounded) {
            float verticalVelocity = Mathf.Abs (rb.linearVelocity.y);

            float t = Mathf.InverseLerp (
                landingMinVelocity,
                landingMaxVelocity,
                verticalVelocity
            );

            float low = Mathf.Lerp (
                landingLowMin,
                landingLowMax,
                t
            );

            float high = Mathf.Lerp (
                landingHighMin,
                landingHighMax,
                t
            );

            float duration = Mathf.Lerp (
                landingDurationMin,
                landingDurationMax,
                t
            );

            triggerImpact (low, high, duration);
        }

        wasGrounded = isGrounded;
    }

    protected void triggerImpact (float low, float high, float duration)
    {
        impactLow = low;

        impactHigh = high;

        impactTimer = duration;
    }

    public void setCollisionDetected (Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;

        if (impact < collisionThreshold)
            return;

        float t = Mathf.InverseLerp (collisionThreshold, 40f, impact);

        triggerImpact (
            Mathf.Lerp (0.3f, 1f, t),
            Mathf.Lerp (0.2f, 0.6f, t),
            0.15f
        );
    }

    // =================================================
    // WEAPON LAYER
    // =================================================

    protected virtual void updateWeaponLayer ()
    {
        if (weaponTimer > 0f) {

            weaponTimer -= Time.deltaTime;

            if (weaponTimer <= 0f) {

                weaponLow = 0f;

                weaponHigh = 0f;
            }
        }
    }

    public void applyWeaponRumble (float low, float high, float duration)
    {
        weaponLow = low;

        weaponHigh = high;

        weaponTimer = duration;
    }

    // =================================================
    // FINAL APPLY
    // =================================================

    protected virtual void applyFinalRumble ()
    {
        float low = currentLow;

        float high = currentHigh;

        // DAMAGE PRIORITY

        if (impactTimer > 0f) {

            low = Mathf.Max (low, impactLow);

            high = Mathf.Max (high, impactHigh);
        }

        // WEAPON PRIORITY

        if (weaponTimer > 0f) {

            low = Mathf.Lerp (low, weaponLow, 0.8f);

            high = Mathf.Lerp (high, weaponHigh, 0.8f);
        }

        float finalLowValue = Mathf.Clamp01 (low);
        float finalHighValue = Mathf.Clamp01 (high);

        if (vibrationMultiplier != 1) {
            finalLowValue *= vibrationMultiplier;
            finalHighValue *= vibrationMultiplier;
        }

        mainSimpleGamepadVibrationHelper.setGamepadVibration (finalLowValue, finalHighValue);
    }

    // =================================================
    // STOP
    // =================================================

    public void stopRumble ()
    {
        currentLow = 0f;

        currentHigh = 0f;

        if (mainSimpleGamepadVibrationHelperLocated) {
            mainSimpleGamepadVibrationHelper.setGamepadVibration (0f, 0f);
        }
    }

    // =================================================
    // PRESET RUMBLES
    // =================================================

    public void setGamepadVibrationByID (int vibrationID)
    {
        if (!mainSimpleGamepadVibrationHelperLocated) {
            return;
        }

        if (!mainSimpleGamepadVibrationHelper
            .isUsingGamepad ()) {
            return;
        }

        if (rumbleDataDict.TryGetValue (vibrationID, out var currentRumbleData)) {

            if (currentRumbleData.stateEnabled) {

                applyWeaponRumble (currentRumbleData.low, currentRumbleData.high, currentRumbleData.duration);
            }
        }
    }

    public void setDamageReceived (float damageAmount)
    {
        if (damageAmount <= 0f) {
            return;
        }

        float normalizedDamage =
            Mathf.Clamp01 (
                damageAmount * damageReceivedMultiplier
            );

        float low =
            Mathf.Lerp (
                damageReceivedMinLow,
                damageReceivedMaxLow,
                normalizedDamage
            );

        float high =
            Mathf.Lerp (
                damageReceivedMinHigh,
                damageReceivedMaxHigh,
                normalizedDamage
            );

        triggerImpact (
            low,
            high,
            damageReceivedDuration
        );
    }

    public void setCurrentSimpleGamepadVibrationHelper (simpleGamepadVibrationHelper newSimpleGamepadVibrationHelper)
    {
        mainSimpleGamepadVibrationHelper = newSimpleGamepadVibrationHelper;

        mainSimpleGamepadVibrationHelperLocated = mainSimpleGamepadVibrationHelper != null;

        if (mainSimpleGamepadVibrationHelperLocated) {

            if (!rumbleDataDictInitialized) {

                buildRumbleDictionary ();

                rumbleDataDictInitialized = true;
            }
        }

        vibrationStoppedChecked = false;
    }

    void buildRumbleDictionary ()
    {
        rumbleDataDict = new Dictionary<int, rumbleData> ();

        for (int i = 0; i < rumbleDataList.Count; i++) {

            rumbleData data = rumbleDataList [i];

            if (rumbleDataDict.ContainsKey (data.vibrationID)) {

                Debug.LogWarning (
                    $"Duplicate vibrationID found: {data.vibrationID}"
                );

                continue;
            }

            rumbleDataDict.Add (data.vibrationID, data);
        }
    }

    [Serializable]
    public class rumbleData
    {
        public string Name;

        public bool stateEnabled;

        public int vibrationID;

        [Range (0f, 1f)]
        public float low;

        [Range (0f, 1f)]
        public float high;

        [Range (0.01f, 1f)]
        public float duration;
    }
}