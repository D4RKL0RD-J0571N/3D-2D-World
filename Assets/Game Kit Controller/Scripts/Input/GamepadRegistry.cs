using System.Collections.Generic;
using UnityEngine;

using static inputManager;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif

public static class GamepadRegistry
{
#if ENABLE_INPUT_SYSTEM
    // ID -> Gamepad
    private static Dictionary<int, Gamepad> gamepadsById = new Dictionary<int, Gamepad> ();

    public static void Register (int id, Gamepad pad)
    {
        gamepadsById [id] = pad;
    }
#endif

    public static void Unregister (int id)
    {
#if ENABLE_INPUT_SYSTEM
        if (gamepadsById.ContainsKey (id)) {
            gamepadsById.Remove (id);
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    public static Gamepad Get (int id)
    {
        return gamepadsById.TryGetValue (id, out var pad) ? pad : null;
    }
#endif

    public static bool IsValid (int id)
    {
#if ENABLE_INPUT_SYSTEM
        return gamepadsById.ContainsKey (id);
#else
        return false;
#endif
    }

    public static void clearRegister ()
    {
#if ENABLE_INPUT_SYSTEM
        gamepadsById.Clear ();
#endif
    }

    public static bool anyGamepadFound ()
    {
#if ENABLE_INPUT_SYSTEM
        return gamepadsById.Count > 0;
#else
        return false;
#endif
    }

    public static Vector2 getNewInputLeftJoysticksAxisInputValue (int gamepadId)
    {
        if (gamepadId > 0 && IsValid (gamepadId)) {
#if ENABLE_INPUT_SYSTEM
            if (gamepadId <= 0) return Vector2.zero;

            var pad = GamepadRegistry.Get (gamepadId);
            if (pad == null) return Vector2.zero;

            return pad.leftStick.ReadValue ();
#endif
        }

        return Vector2.zero;
    }

    public static Vector2 getNewInputRightJoysticksAxisInputValue (int gamepadId)
    {
        if (gamepadId > 0 && IsValid (gamepadId)) {
#if ENABLE_INPUT_SYSTEM
            if (gamepadId <= 0) return Vector2.zero;

            var pad = GamepadRegistry.Get (gamepadId);
            if (pad == null) return Vector2.zero;

            return pad.rightStick.ReadValue ();
#endif
        }

        return Vector2.zero;
    }

    public static bool GetReturnClickDown (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.buttonEast.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetReturnClickUp (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.buttonEast.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetReturnClick (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.buttonEast.isPressed;
#else
        return false;
#endif
    }

    public static bool GetClickDown (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.buttonSouth.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetClickUp (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.buttonSouth.wasReleasedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetClick (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.buttonSouth.isPressed;
#else
        return false;
#endif
    }

    public static bool GetRightTriggerClick (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.rightTrigger.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetLeftTriggerClick (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.leftTrigger.wasPressedThisFrame;
#else
        return false;
#endif
    }

    public static bool GetRightJoystickPressClick (int gamepadId)
    {
        if (gamepadId <= 0) return false;

#if ENABLE_INPUT_SYSTEM
        var pad = GamepadRegistry.Get (gamepadId);
        if (pad == null) return false;

        return pad.rightStickButton.wasPressedThisFrame;
#else
        return false;
#endif
    }
}