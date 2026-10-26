using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simpleGamepadVibrationHelper : MonoBehaviour
{
    [Header ("Main Settings")]
    [Space]

    public bool vibrationEnabled = true;

    public int playerID;

    public bool sendSignalToPlayerInputManager;

    [Range (0, 2)] public float vibrationMultiplier = 1;

    [Space]
    [Header ("Mobile Settings")]
    [Space]

    public bool mobileVibrationEnabled = true;

    public bool useOnlyPhoneVibrationOnMobile = true;

    [Space]
    [Header ("Vibration States List Settings")]
    [Space]

    public List<vibrationStateInfo> vibrationStateInfoList = new List<vibrationStateInfo> ();

    [Space]
    [Header ("Debug")]
    [Space]

    public bool showDebugPrint;
    public bool usingGamepad;
    public bool gameIsPaused;

    public bool touchPlatform;

    [Space]
    [Header ("Event Settings")]
    [Space]

    public eventParameters.eventToCallWithString eventToCallVibrationByString;
    public eventParameters.eventToCallWithInteger eventToStopVibrationByID;

    [Space]
    [Header ("Components")]
    [Space]

    public playerInputManager playerInput;


    [Space]
    [Header ("Test")]
    [Space]

    public int vibrationIDToTest = 0;


    bool playerInputLocated;

    float lastTimeVibrationActive = 0;
    float lastVibrationDuration = 0;

    private Dictionary<int, vibrationStateInfo> vibrationStateInfoDictionary;

    bool vibrationStateInfoDictionaryInitialized;


    float currentIntensityMultiplier = -1;

    Coroutine mobileHapticCoroutine;
    bool mobileHapticActive;

    //for vibration on mobile, make sure to add this line to the manifest on player settings, on the custom main manifest option

    //<uses-permission android:name="android.permission.VIBRATE" />


    void Awake ()
    {
        playerInputLocated = playerInput != null;
    }

    public void setGamepadVibrationByID (int vibrationID)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        if (!vibrationStateInfoDictionaryInitialized) {
            buildDictionary ();
        }

        if (vibrationStateInfoDictionary.TryGetValue (vibrationID, out var currentVibrationStateInfo)) {
            if (currentVibrationStateInfo.stateEnabled) {
                if (lastTimeVibrationActive > 0) {
                    if (Time.time < lastTimeVibrationActive + lastVibrationDuration) {
                        if (showDebugPrint) {
                            print ("vibration in process to ignore the rest ");
                        }

                        return;
                    }
                }

                if (currentVibrationStateInfo.ignoreOtherStatesWhileInProcess) {
                    lastTimeVibrationActive = Time.time;

                    lastVibrationDuration = currentVibrationStateInfo.durationAmount;
                }

                if (showDebugPrint) {
                    print ("calling vibration by id " + vibrationID);
                }

                if (currentIntensityMultiplier > -1) {
                    setGamepadVibration (currentVibrationStateInfo.lowAmount * currentIntensityMultiplier,
                        currentVibrationStateInfo.hightAmount * currentIntensityMultiplier,
                        currentVibrationStateInfo.durationAmount);

                } else {
                    setGamepadVibration (currentVibrationStateInfo.lowAmount, currentVibrationStateInfo.hightAmount, currentVibrationStateInfo.durationAmount);
                }
            }
        }
    }

    public void setGamepadVibrationByID (int vibrationID, float intensityMultiplier)
    {
        currentIntensityMultiplier = intensityMultiplier;

        setGamepadVibrationByID (vibrationID);

        currentIntensityMultiplier = -1;
    }

    public void setGamepadVibrationByName (string vibrationName, float intensityMultiplier)
    {
        currentIntensityMultiplier = intensityMultiplier;

        setGamepadVibrationByName (vibrationName);

        currentIntensityMultiplier = -1;
    }


    public void setGamepadVibrationByName (string vibrationName)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        int elementIndex = vibrationStateInfoList.FindIndex (s => s.Name.Equals (vibrationName));

        if (elementIndex > -1) {
            vibrationStateInfo currentVibrationStateInfo = vibrationStateInfoList [elementIndex];

            if (currentVibrationStateInfo.stateEnabled) {
                if (currentIntensityMultiplier > -1) {
                    setGamepadVibration (currentVibrationStateInfo.lowAmount * currentIntensityMultiplier,
                        currentVibrationStateInfo.hightAmount * currentIntensityMultiplier,
                        currentVibrationStateInfo.durationAmount);
                } else {
                    setGamepadVibration (currentVibrationStateInfo.lowAmount, currentVibrationStateInfo.hightAmount, currentVibrationStateInfo.durationAmount);
                }
            }
        }

    }

    public void setGamepadVibration (float low, float high)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        if (vibrationMultiplier <= 0) {
            return;
        }

        if (sendSignalToPlayerInputManager) {
            if (playerInputLocated) {
                playerInput.setGamepadVibrationByPlayerID (low * vibrationMultiplier, high * vibrationMultiplier);
            } else {

            }
        } else {
            InputAxisWrapper.setGamepadVibrationByPlayerID (playerID, low * vibrationMultiplier, high * vibrationMultiplier);
        }
    }

    public void setGamepadVibration (float low, float high, float duration)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        if (vibrationMultiplier <= 0) {
            return;
        }

        if (sendSignalToPlayerInputManager) {
            if (playerInputLocated) {
                playerInput.setGamepadVibrationByPlayerID (low * vibrationMultiplier, high * vibrationMultiplier, duration);
            } else {
                setGamepadVibrationExternally (low * vibrationMultiplier, high * vibrationMultiplier, duration);
            }
        } else {
            InputAxisWrapper.setGamepadVibrationByPlayerID (playerID, low * vibrationMultiplier, high * vibrationMultiplier, duration);
        }
    }

    void setGamepadVibrationExternally (float low, float high, float duration)
    {
        bool ignoreGamepadResult = false;

        if (useOnlyPhoneVibrationOnMobile && touchPlatform) {
            ignoreGamepadResult = true;
        }

        if (usingGamepad && !ignoreGamepadResult) {
            string vibrationInfoString =
                playerID.ToString () + ";" +
                low.ToString () + ";" +
                high.ToString () + ";" +
                duration.ToString ();

            eventToCallVibrationByString.Invoke (vibrationInfoString);
        } else {
            if (touchPlatform) {
                setMobileVibration (low, high, duration);
            }
        }
    }

    public void stopGamepadVibration ()
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        if (sendSignalToPlayerInputManager) {
            if (playerInputLocated) {
                playerInput.stopGamepadVibrationByPlayerID ();
            } else {
                eventToStopVibrationByID.Invoke (playerID);
            }
        } else {
            InputAxisWrapper.stopGamepadVibrationByPlayerID (playerID);
        }
    }

    public void setGamepadVibration (string infoString)
    {
        if (!vibrationEnabled) {
            return;
        }

        if (!isUsingGamepad ()) {
            return;
        }

        bool splitSymbolLocated = infoString.Contains (";");

        if (splitSymbolLocated) {
            string [] vibrationFields = infoString.Split (';');

            if (vibrationFields.Length >= 3) {
                float lowAmount = float.Parse (vibrationFields [0]);

                float hightAmount = float.Parse (vibrationFields [1]);

                float durationAmount = float.Parse (vibrationFields [2]);


                setGamepadVibration (lowAmount, hightAmount, durationAmount);
            }
        }
    }

    public void setTouchPlatformState (bool state)
    {
        touchPlatform = state;

        if (touchPlatform && mobileVibrationEnabled) {
            if (GKCAndroidHaptics.TryVibrate ()) {

                print ("TryVibrate is positive");
            } else {
                mobileVibrationEnabled = false;

                print ("TryVibrate is false, disabling vibration en mobile");
            }
        }
    }

    public bool isTouchPlatform ()
    {
        return touchPlatform;
    }

    public void setUsingGamepadState (bool state)
    {
        usingGamepad = state;

        playerInputLocated = playerInput != null;

        if (playerInputLocated) {
            playerID = playerInput.getPlayerID ();
        }
    }

    public bool isUsingGamepad ()
    {
        return usingGamepad || touchPlatform;
    }

    public bool isUseOnlyPhoneVibrationOnMobile ()
    {
        return useOnlyPhoneVibrationOnMobile;
    }

    void buildDictionary ()
    {
        if (!vibrationStateInfoDictionaryInitialized) {
            vibrationStateInfoDictionary = new Dictionary<int, vibrationStateInfo> ();

            for (int i = 0; i < vibrationStateInfoList.Count; i++) {
                vibrationStateInfo data = vibrationStateInfoList [i];

                if (vibrationStateInfoDictionary.ContainsKey (data.vibrationID)) {
                    Debug.LogWarning ($"Duplicate vibrationID found: {data.vibrationID}");
                    continue;
                }

                vibrationStateInfoDictionary.Add (data.vibrationID, data);
            }

            vibrationStateInfoDictionaryInitialized = true;
        }
    }

    public void setVibrationEnabledState (bool state)
    {
        vibrationEnabled = state;
    }

    public void setVibrationMultiplierValue (float value)
    {
        vibrationMultiplier = value;

        vibrationMultiplier = Mathf.Clamp (vibrationMultiplier, 0, 2);
    }


    //MOBILE VIBRATION
    public void setGameIsPausedState (bool state)
    {
        gameIsPaused = state;
    }

    public void setMobileVibration (float low, float high)
    {
        if (!mobileVibrationEnabled) {
            return;
        }

        float intensity = Mathf.Clamp01 (low * 0.75f + high * 0.5f);

        GKCAndroidHaptics.VibrateLoop (intensity);
    }


    public void setMobileVibration (float low, float high, float duration)
    {
        if (!mobileVibrationEnabled) {
            return;
        }

        float intensity = Mathf.Clamp01 (low * 0.75f + high * 0.5f);

        if (mobileHapticActive) {
            if (mobileHapticCoroutine != null) {
                StopCoroutine (mobileHapticCoroutine);
            }

            GKCAndroidHaptics.Cancel ();

            mobileHapticActive = false;
        }

        mobileHapticCoroutine = StartCoroutine (SetMobileVibrationCoroutine (intensity, duration));

    }

    IEnumerator SetMobileVibrationCoroutine (float intensity, float duration)
    {
        mobileHapticActive = true;

        int amplitude = Mathf.RoundToInt (Mathf.Clamp01 (intensity) * 255f);

        GKCAndroidHaptics.Vibrate (
            Mathf.RoundToInt (duration * 1000f),
            amplitude);

        float timer = duration;

        bool wasPaused = false;

        while (timer > 0f) {
            if (gameIsPaused) {
                if (!wasPaused) {
                    GKCAndroidHaptics.Cancel ();

                    wasPaused = true;
                }
            } else {
                if (wasPaused) {
                    float remainingTime = timer;

                    GKCAndroidHaptics.Vibrate (
                        Mathf.RoundToInt (remainingTime * 1000f),
                        amplitude);

                    wasPaused = false;
                }

                timer -= Time.unscaledDeltaTime;
            }

            yield return null;
        }

        GKCAndroidHaptics.Cancel ();

        mobileHapticCoroutine = null;

        mobileHapticActive = false;
    }

    public void stopMobileVibration ()
    {
        if (!mobileVibrationEnabled) {
            return;
        }

        GKCAndroidHaptics.Cancel ();
    }


    //EDITOR FUNCTIONS
    public void setVibrationMultiplierValueFromEditor (float value)
    {
        setVibrationMultiplierValue (value);

        updateComponent ();
    }
    public void setVibrationEnabledStateFromEditor (bool state)
    {
        setVibrationEnabledState (state);

        updateComponent ();
    }

    public void testVibration ()
    {
        setGamepadVibrationByID (vibrationIDToTest);
    }

    void updateComponent ()
    {
        GKC_Utils.updateComponent (this);

        GKC_Utils.updateDirtyScene ("Update Simple Vibration Helper", gameObject);
    }

    [System.Serializable]
    public class vibrationStateInfo
    {
        [Header ("Main Settings")]
        [Space]

        public string Name;

        public int vibrationID;

        public bool stateEnabled;

        public bool ignoreOtherStatesWhileInProcess;

        [Space]
        [Header ("Vibration Value Settings")]
        [Space]

        public float lowAmount;
        public float hightAmount;

        public float durationAmount;
    }
}
