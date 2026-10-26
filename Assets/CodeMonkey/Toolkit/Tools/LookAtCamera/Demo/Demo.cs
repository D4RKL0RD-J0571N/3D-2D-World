using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TLookAtCamera.Demo {

    public class Demo : MonoBehaviour {


        [SerializeField] private Transform cameraManagerTransform;


        private void Update() {
            Vector2 inputVector = new Vector2(0, 0);

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.wKey.isPressed) {
                inputVector.y = +1;
            }
            if (Keyboard.current.sKey.isPressed) {
                inputVector.y = -1;
            }
            if (Keyboard.current.aKey.isPressed) {
                inputVector.x = -1;
            }
            if (Keyboard.current.dKey.isPressed) {
                inputVector.x = +1;
            }
#else
            if (Input.GetKey(KeyCode.W)) {
                inputVector.y = +1;
            }
            if (Input.GetKey(KeyCode.S)) {
                inputVector.y = -1;
            }
            if (Input.GetKey(KeyCode.A)) {
                inputVector.x = -1;
            }
            if (Input.GetKey(KeyCode.D)) {
                inputVector.x = +1;
            }
#endif

            Vector3 moveDir = cameraManagerTransform.forward * inputVector.y + cameraManagerTransform.right * inputVector.x;
            float moveSpeed = 10f;
            cameraManagerTransform.position += moveDir * moveSpeed * Time.deltaTime;


            float rotateAmount = 0f;
#if ENABLE_INPUT_SYSTEM
            bool isQKeyPressed = Keyboard.current.qKey.isPressed;
#else
            bool isQKeyPressed = Input.GetKey(KeyCode.Q);
#endif
            if (isQKeyPressed) {
                rotateAmount = +90;
            }
#if ENABLE_INPUT_SYSTEM
            bool isEKeyPressed = Keyboard.current.eKey.isPressed;
#else
            bool isEKeyPressed = Input.GetKey(KeyCode.E);
#endif
            if (isEKeyPressed) {
                rotateAmount = -90;
            }
            cameraManagerTransform.eulerAngles += new Vector3(0, rotateAmount, 0) * Time.deltaTime;
        }

    }

}