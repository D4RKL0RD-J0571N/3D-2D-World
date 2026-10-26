using UnityEngine;
using System;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class VirtualPointer
{
    public Vector2 Position;
    public bool Down;
    public bool Up;
    public bool Held;

    public Vector2 virtualMousePosition;
    public bool useVirtualMouse;

    //private const float speed = 1400f;

    public void Update (
        int gamepadId = 0,
        Vector2 virtualMousePositionValue = default,
        bool useVirtualMouseValue = false,
        bool screenFocused = false,
        bool ignoreActualMousePressEnabled = false,
        bool isUsingTouchControls = false)
    {
        Down = false;
        Up = false;

        virtualMousePosition = virtualMousePositionValue;
        useVirtualMouse = useVirtualMouseValue;

        // =========================
        // INPUT BASE (actual mouse)
        // =========================
        Vector2 mousePos = InputReader.MousePosition;

        if (ignoreActualMousePressEnabled) {
            Held = false;
        } else {
            if (InputReader.MouseDown) {
                Down = true;
            }

            if (InputReader.MouseUp) {
                Up = true;
            }

            Held = InputReader.MouseHeld;
        }

        // =========================
        // UPDATE CURSOR POSITION
        // =========================
        Position = useVirtualMouse ? virtualMousePosition : mousePos;

        // =========================
        // GAMEPAD OVERRIDE / MOD
        // =========================
        if (!isUsingTouchControls) {
            if (gamepadId > 0 && GamepadRegistry.IsValid (gamepadId)) {
                //Vector2 stick = GamepadRegistry.getNewInputLeftJoysticksAxisInputValue (gamepadId);

                //if (stick.sqrMagnitude > 0.01f) {
                //    Position += stick * speed * Time.unscaledDeltaTime;
                //}

                if (GamepadRegistry.GetClickDown (gamepadId)) {
                    Down = true;
                }

                if (GamepadRegistry.GetClickUp (gamepadId)) {
                    Up = true;
                }

                if (GamepadRegistry.GetClick (gamepadId)) {
                    Held = true;
                }
            }
        }

        if (!screenFocused) {
            Down = false;
            Up = false;
            Held = false;
        }

        Position.x = Mathf.Clamp (Position.x, 0, Screen.width);
        Position.y = Mathf.Clamp (Position.y, 0, Screen.height);
    }
}

public static class InputReader
{
    public static Vector2 MousePosition =>
#if ENABLE_INPUT_SYSTEM
        Mouse.current?.position.ReadValue () ?? Vector2.zero;
#else
        Vector2.zero;
#endif

    public static bool MouseDown =>
#if ENABLE_INPUT_SYSTEM
        Mouse.current?.leftButton.wasPressedThisFrame ?? false;
#else
        false;
#endif

    public static bool MouseUp =>
#if ENABLE_INPUT_SYSTEM
        Mouse.current?.leftButton.wasReleasedThisFrame ?? false;
#else
        false;
#endif

    public static bool MouseHeld =>
#if ENABLE_INPUT_SYSTEM
        Mouse.current?.leftButton.isPressed ?? false;
#else
        false;
#endif
}

public static class TouchAdapter
{
    public static VirtualTouch ToVirtualTouch (VirtualPointer pointer, int gamepadId)
    {
        return new VirtualTouch
        {
            position = pointer.Position,
            down = pointer.Down,
            up = pointer.Up,
            held = pointer.Held,

            fingerId = ResolveFingerId (gamepadId),

            gamepadId = gamepadId,

            virtualMousePosition = pointer.virtualMousePosition,
            useVirtualMouse = pointer.useVirtualMouse
        };
    }

    private static int ResolveFingerId (int gamepadId)
    {
        return 1000 + gamepadId;
    }
}

public struct VirtualTouch
{
    public Vector2 position;
    public bool down;
    public bool up;
    public bool held;

    public int fingerId;
    public int gamepadId;

    public Vector2 virtualMousePosition;
    public bool useVirtualMouse;
}
