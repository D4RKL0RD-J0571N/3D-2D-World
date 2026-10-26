using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class UIKeyboardManager
{
    public static void OpenKeyboard (
        InputField inputField,
         Action<bool> onFinished)
    {
        if (inputField == null) {
            onFinished?.Invoke (false);
            return;
        }

#if UNITY_ANDROID || UNITY_IOS

        CoroutineRunner.Run (
            OpenMobileKeyboard (inputField, onFinished));

#elif UNITY_PS5

        CoroutineRunner.Run (
            OpenPS5Keyboard (inputField, onFinished));

#elif UNITY_GAMECORE

        CoroutineRunner.Run (
            OpenXboxKeyboard (inputField, onFinished));

#elif UNITY_SWITCH

        CoroutineRunner.Run (
           OpenSwitchKeyboard (inputField, onFinished));
#else

        CoroutineRunner.Run (
            OpenPCKeyboard (inputField, onFinished));

#endif
    }

    //#if UNITY_ANDROID || UNITY_IOS

    static IEnumerator OpenMobileKeyboard (
        InputField inputField,
        Action<bool> onFinished)
    {
        if (!TouchScreenKeyboard.isSupported) {
            onFinished?.Invoke (false);
            yield break;
        }

        inputField.ActivateInputField ();

        TouchScreenKeyboard keyboard =
            TouchScreenKeyboard.Open (
                text: inputField.text,
                keyboardType: TouchScreenKeyboardType.Default,
                autocorrection: false,
                multiline: false,
                secure: false,
                alert: false);

        if (keyboard == null) {
            onFinished?.Invoke (false);
            yield break;
        }

        // TODO: Verify if some devices require handling TouchScreenKeyboard.Status.Canceled separately.
        while (keyboard.status == TouchScreenKeyboard.Status.Visible) {
            yield return null;
        }

        bool accepted =
            keyboard.status == TouchScreenKeyboard.Status.Done;

        if (accepted && inputField != null) {
            inputField.text = keyboard.text;
        }

        onFinished?.Invoke (accepted);
    }

    //#endif

    static IEnumerator OpenPCKeyboard (
        InputField inputField,
        Action<bool> onFinished)
    {
        bool finished = false;

        void EndEdit (string value)
        {
            finished = true;
        }

        inputField.onEndEdit.AddListener (EndEdit);

        inputField.ActivateInputField ();

        bool cancelled = false;

        while (!finished) {

            if (inputField == null) {
                cancelled = true;

                break;
            }

            yield return null;
        }

        if (inputField != null) {
            inputField.onEndEdit.RemoveListener (EndEdit);
        }

        onFinished?.Invoke (!cancelled);
    }
}