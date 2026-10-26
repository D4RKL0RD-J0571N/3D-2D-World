using UnityEngine;

#if ENABLE_LEGACY_INPUT_MANAGER
namespace SoftKitty
{
    public class InputProxyLegacy : IInputProxy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            InputProxy.SetImplementation(new InputProxyLegacy());
        }

        public Vector3 mousePosition
        {
            get
            {
                return Input.mousePosition;
            }
        }

        public bool GetMouseButton(int _button)
        {
            return Input.GetMouseButton(_button);
        }

        public bool GetMouseButtonDown(int _button)
        {
            return Input.GetMouseButtonDown(_button);
        }
        public bool GetMouseButtonUp(int _button)
        {
            return Input.GetMouseButtonUp(_button);
        }

        public bool GetKey(KeyCode _key)
        {
            return Input.GetKey(_key);
        }

        public bool GetKeyUp(KeyCode _key)
        {
            return Input.GetKeyUp(_key);
        }

        public bool GetKeyDown(KeyCode _key)
        {
            return Input.GetKeyDown(_key);
        }

        public float GetMouseX()
        {
            return Input.GetAxis("Mouse X");
        }
        public float GetMouseY()
        {
            return Input.GetAxis("Mouse Y");
        }
        public float GetMouseScrollWheel()
        {
            return Input.GetAxis("Mouse ScrollWheel");
        }
    }
}
#endif