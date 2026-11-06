using UnityEngine;
using UnityEngine.InputSystem;

namespace MagicPigGames.Northstar.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleFirstPersonController : MonoBehaviour
    {
        public float moveSpeed = 3.0f;
        public float turnSpeed = 300.0f;
        public float tiltSpeed = 200.0f;
        public float maxTilt = 40.0f;

        private CharacterController characterController;
        private float tilt = 0.0f;

        void Start()
        {
            characterController = GetComponent<CharacterController>();
            //Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;

            // Mouse Look Control
            float turn = mouseDelta.x * turnSpeed * Time.deltaTime;
            transform.Rotate(0, turn, 0);

            tilt -= mouseDelta.y * tiltSpeed * Time.deltaTime;
            tilt = Mathf.Clamp(tilt, -maxTilt, maxTilt);
            Camera.main.transform.localRotation = Quaternion.Euler(tilt, 0, 0);

            // Movement Control
            float horizontal = 0f;
            float vertical = 0f;
            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            }

            float moveDirectionY = characterController.velocity.y;
            Vector3 forwardMovement = transform.TransformDirection(Vector3.forward) * vertical * moveSpeed;
            Vector3 rightMovement = transform.TransformDirection(Vector3.right) * horizontal * moveSpeed;
            Vector3 move = forwardMovement + rightMovement;

            if (!characterController.isGrounded)
            {
                move.y = moveDirectionY;
            }

            characterController.SimpleMove(move);
        }
    }
}
