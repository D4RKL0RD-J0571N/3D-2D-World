using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class gamepadVibrationCoroutineController : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool gamepadVibrationEnabled = true;

    [Space]
    [Header ("Over Time States")]
    [Space]

    public List<overtimeVibrationState> overtimeVibrationStateList = new List<overtimeVibrationState> ();

    [Space]
    [Header ("Components")]
    [Space]

    public simpleGamepadVibrationHelper mainSimpleGamepadVibrationHelper;

    Coroutine activeCoroutine;

    bool mainSimpleGamepadVibrationHelperLocated;

    bool componentsInitalized;

    // --------------------------------------------------
    // BASIC TIMED VIBRATION
    // --------------------------------------------------

    public void vibrateOverTimeStateByID (int stateID)
    {
        if (!gamepadVibrationEnabled) {
            return;
        }

        int index = overtimeVibrationStateList.FindIndex (s => s.stateID == stateID);

        if (index == -1) {
            return;
        }

        overtimeVibrationState state = overtimeVibrationStateList [index];

        if (!state.stateEnabled) {
            return;
        }

        vibrateOverTimeByID (
            state.vibrationID,
            state.duration,
            state.minInterval,
            state.maxInterval,
            state.intensityNoise);
    }

    public void vibrateByID (int id, float duration)
    {
        if (!gamepadVibrationEnabled) {
            return;
        }

        StartRoutine (RegularByID (id, duration));
    }

    public void vibrateOverTimeByID (
        int id,
        float duration,
        float minInterval = 0.03f,
        float maxInterval = 0.08f,
        float intensityNoise = 0.2f)
    {
        if (!gamepadVibrationEnabled) {
            return;
        }

        StartRoutine (vibrateOverTimeByIDCoroutine (id, duration, minInterval, maxInterval, intensityNoise));
    }

    // --------------------------------------------------
    // STOP
    // --------------------------------------------------

    public void stopVibration ()
    {
        if (activeCoroutine != null) {
            StopCoroutine (activeCoroutine);
            activeCoroutine = null;
        }

        if (mainSimpleGamepadVibrationHelperLocated) {
            mainSimpleGamepadVibrationHelper.stopGamepadVibration ();
        }
    }

    // --------------------------------------------------
    // INTERNAL
    // --------------------------------------------------

    void StartRoutine (IEnumerator routine)
    {
        stopVibration ();
        activeCoroutine = StartCoroutine (routine);
    }

    // --------------------------------------------------
    // SIMPLE VIBRATION
    // --------------------------------------------------

    IEnumerator RegularByID (int id, float duration)
    {
        mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (id);

        yield return new WaitForSeconds (duration);

        mainSimpleGamepadVibrationHelper.stopGamepadVibration ();

        activeCoroutine = null;
    }

    // --------------------------------------------------
    // DYNAMIC
    // --------------------------------------------------

    IEnumerator vibrateOverTimeByIDCoroutine (
        int id,
        float duration,
        float minInterval,
        float maxInterval,
        float intensityNoise)
    {
        float t = 0f;

        while (t < duration) {
            float noise = 1f + Random.Range (-intensityNoise, intensityNoise);

            mainSimpleGamepadVibrationHelper.setGamepadVibrationByID (id, noise);

            float wait = Random.Range (minInterval, maxInterval);
            yield return new WaitForSeconds (wait);

            t += wait;
        }

        mainSimpleGamepadVibrationHelper.stopGamepadVibration ();

        activeCoroutine = null;
    }

    public void setGamepadVibrationEnabledState (bool state)
    {
        gamepadVibrationEnabled = state;
    }

    public void setMainSimpleGamepadVibrationHelper (simpleGamepadVibrationHelper newSimpleGamepadVibrationHelper)
    {
        mainSimpleGamepadVibrationHelper = newSimpleGamepadVibrationHelper;
    }

    void initializeComponents ()
    {
        if (!componentsInitalized) {
            mainSimpleGamepadVibrationHelperLocated = mainSimpleGamepadVibrationHelper != null;

            if (!mainSimpleGamepadVibrationHelperLocated) {
                setGamepadVibrationEnabledState (false);
            }

            componentsInitalized = true;
        }
    }

    [System.Serializable]
    public class overtimeVibrationState
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public int stateID;

        public bool stateEnabled = true;

        [Space]
        [Header ("Helper Vibration")]
        [Space]

        public int vibrationID;

        [Space]
        [Header ("Over Time Settings")]
        [Space]

        public float duration = 1f;

        public float minInterval = 0.03f;
        public float maxInterval = 0.08f;

        [Range (0f, 1f)]
        public float intensityNoise = 0.2f;
    }
}
