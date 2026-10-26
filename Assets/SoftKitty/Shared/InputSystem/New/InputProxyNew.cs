using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;

namespace SoftKitty
{
    public class InputProxyNew : IInputProxy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            InputProxy.SetImplementation(new InputProxyNew());
        }

        private InputAction[] mouseButtonActions = new InputAction[3]{
            new InputAction(binding: "<Mouse>/leftButton"),
            new InputAction(binding: "<Mouse>/rightButton"),
            new InputAction(binding: "<Mouse>/middleButton")
        };
        private  Dictionary<KeyCode, Key> lookup;
        private float smoothedMouseX = 0F;
        private float smoothedMouseY = 0F;
        public Vector3 mousePosition
        {
            get
            {
                return Mouse.current.position.ReadValue();
            }
        }
        public float GetMouseX()
        {
            float raw = Mouse.current != null ? Mouse.current.delta.ReadValue().x : 0f;
            smoothedMouseX = Mathf.Lerp(smoothedMouseX, raw * 0.05f, 0.5f);
            return smoothedMouseX;
        }
        public float GetMouseY()
        {
            float raw = Mouse.current != null ? Mouse.current.delta.ReadValue().y : 0f;
            smoothedMouseY = Mathf.Lerp(smoothedMouseY, raw * 0.05f, 0.5f);
            return smoothedMouseY;
        }
        public float GetMouseScrollWheel()
        {
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
        }
        public bool GetMouseButton(int _button)
        {
            if (!mouseButtonActions[_button].enabled) mouseButtonActions[_button].Enable();
            return mouseButtonActions[_button].IsPressed();
        }

        public bool GetMouseButtonDown(int _button)
        {
            if (!mouseButtonActions[_button].enabled) mouseButtonActions[_button].Enable();
            return mouseButtonActions[_button].WasPressedThisFrame();
        }
        public bool GetMouseButtonUp(int _button)
        {
            if (!mouseButtonActions[_button].enabled) mouseButtonActions[_button].Enable();
            return mouseButtonActions[_button].WasReleasedThisFrame();
        }

        public bool GetKey(KeyCode _key)
        {

            return Keyboard.current[GetKeyByKeyCode(_key)].IsPressed();
        }

        public bool GetKeyUp(KeyCode _key)
        {
            return Keyboard.current[GetKeyByKeyCode(_key)].wasReleasedThisFrame;
        }

        public bool GetKeyDown(KeyCode _key)
        {
            return Keyboard.current[GetKeyByKeyCode(_key)].wasPressedThisFrame;
        }

        private Key GetKeyByKeyCode(KeyCode _key)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<KeyCode, Key>();
                foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    var textVersion = keyCode.ToString().Replace("Alpha", "Digit").Replace("Keypad", "Numpad");
                    if (System.Enum.TryParse<Key>(textVersion, true, out var value))
                        lookup[keyCode] = value;
                }
                lookup[KeyCode.Return] = Key.Enter;
                lookup[KeyCode.KeypadEnter] = Key.NumpadEnter;
            }
            if (lookup.ContainsKey(_key))
                return lookup[_key];
            else
                return Key.None;
        }
    }
}
#endif
