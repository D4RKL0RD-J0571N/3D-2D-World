using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GKCAndroidHaptics
{
#if UNITY_ANDROID && !UNITY_EDITOR

    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass vibrationEffectClass;

    private static int sdkVersion;
    private static bool isVibratorAvailable;

#endif

    static GKCAndroidHaptics ()
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        using (AndroidJavaClass unityPlayer =
            new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            using (AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                using (AndroidJavaClass context =
                    new AndroidJavaClass("android.content.Context"))
                {
                    vibrator = activity.Call<AndroidJavaObject>(
                        "getSystemService",
                        context.GetStatic<string>("VIBRATOR_SERVICE"));
                }
            }
        }

        isVibratorAvailable = vibrator != null;

        using (AndroidJavaClass buildVersion =
            new AndroidJavaClass("android.os.Build$VERSION"))
        {
            sdkVersion = buildVersion.GetStatic<int>("SDK_INT");
        }

        if (sdkVersion >= 26)
        {
            vibrationEffectClass =
                new AndroidJavaClass("android.os.VibrationEffect");
        }

#endif
    }

    public static void Vibrate (long durationMs = 50, int amplitude = 128)
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (!isVibratorAvailable)
            return;

        amplitude = Mathf.Clamp(amplitude, 1, 255);

        if (sdkVersion >= 26)
        {
            using (AndroidJavaObject effect =
                vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createOneShot",
                    durationMs,
                    amplitude))
            {
                vibrator.Call("vibrate", effect);
            }
        }
        else
        {
            vibrator.Call("vibrate", durationMs);
        }

#endif
    }

    public static void VibrateLoop (float intensity)
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (!isVibratorAvailable)
            return;

        if (sdkVersion < 26)
        {
            vibrator.Call("vibrate", 1000L);
            return;
        }

        int amplitude =
            Mathf.RoundToInt(Mathf.Clamp01(intensity) * 255f);

        long[] timings =
        {
            0,
            50
        };

        int[] amplitudes =
        {
            0,
            amplitude
        };

        using (AndroidJavaObject effect =
            vibrationEffectClass.CallStatic<AndroidJavaObject>(
                "createWaveform",
                timings,
                amplitudes,
                0))
        {
            vibrator.Call("vibrate", effect);
        }

#endif
    }

    public static void Cancel ()
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (!isVibratorAvailable)
            return;

        vibrator.Call("cancel");

#endif
    }

    public static bool TryVibrate (long durationMs = 50, int amplitude = 128)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!isVibratorAvailable)
                return false;

            amplitude = Mathf.Clamp(amplitude, 1, 255);

            if (sdkVersion >= 26)
            {
                using (AndroidJavaObject effect =
                    vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot",
                        durationMs,
                        amplitude))
                {
                    vibrator.Call("vibrate", effect);
                }
            }
            else
            {
                vibrator.Call("vibrate", durationMs);
            }

            return true;
        }
        catch (AndroidJavaException)
        {
            isVibratorAvailable = false; // opcional pero útil
            return false;
        }
#endif

        return false;
    }
}