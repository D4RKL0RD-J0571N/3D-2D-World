
using UnityEngine;


namespace SoftKitty
{

    public interface IInputProxy
    {
        Vector3 mousePosition { get; }
        bool GetMouseButton(int _button);
        bool GetMouseButtonDown(int _button);
        bool GetMouseButtonUp(int _button);
        float GetMouseX();
        float GetMouseY();
        float GetMouseScrollWheel();
        bool GetKey(KeyCode _key);
        bool GetKeyUp(KeyCode _key);
        bool GetKeyDown(KeyCode _key);
    }


    public static class InputProxy
    {
        private static IInputProxy implementation;
        public static void SetImplementation(IInputProxy impl)
        {
            implementation = impl;
        }
        public static Vector3 mousePosition => implementation.mousePosition;

        public static bool GetMouseButton(int _button) => implementation.GetMouseButton(_button);

        public static bool GetMouseButtonDown(int _button) => implementation.GetMouseButtonDown(_button);
        public static bool GetMouseButtonUp(int _button) => implementation.GetMouseButtonUp(_button);
        public static float GetMouseX() => implementation.GetMouseX();
        public static float GetMouseY() => implementation.GetMouseY();
        public static float GetMouseScrollWheel() => implementation.GetMouseScrollWheel();
        public static bool GetKey(KeyCode _key) => implementation.GetKey(_key);
        public static bool GetKeyUp(KeyCode _key) => implementation.GetKeyUp(_key);
        public static bool GetKeyDown(KeyCode _key) => implementation.GetKeyDown(_key);

    }
}