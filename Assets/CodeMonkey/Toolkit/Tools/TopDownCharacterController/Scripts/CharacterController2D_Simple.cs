using UnityEngine;
using UnityEngine.InputSystem;

namespace CodeMonkey.Toolkit.TTopDownCharacterController {

    /// <summary>
    /// ** Top Down Character Controller 2D **
    /// 
    /// Simple Character Controller from a Top Down Perspective<color=#0f0>(no gravity)</color>
    /// Move around, optionally with a dash or roll.
    /// Interacts with Physics2D
    /// </summary>
    public class CharacterController2D_Simple : MonoBehaviour {


        private const float MOVE_SPEED = 5f;
        private const float TELEPORT_AMOUNT = 5f;


        [SerializeField] private LayerMask teleportLayerMask;
        [SerializeField] private bool canTeleport = true;


        private Rigidbody2D characterRigidbody2D;
        private Vector3 moveDir;
        private Vector3 lastMoveDir;
        private bool isTeleportButtonDown;


        private void Awake() {
            characterRigidbody2D = GetComponent<Rigidbody2D>();
        }

        private void Update() {
            float moveX = 0f;
            float moveY = 0f;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current.wKey.isPressed) {
                moveY = +1;
            }
            if (Keyboard.current.sKey.isPressed) {
                moveY = -1;
            }
            if (Keyboard.current.aKey.isPressed) {
                moveX = -1;
            }
            if (Keyboard.current.dKey.isPressed) {
                moveX = +1;
            }
#else
            if (Input.GetKey(KeyCode.W)) {
                moveY = +1f;
            }
            if (Input.GetKey(KeyCode.S)) {
                moveY = -1f;
            }
            if (Input.GetKey(KeyCode.A)) {
                moveX = -1f;
            }
            if (Input.GetKey(KeyCode.D)) {
                moveX = +1f;
            }
#endif

            moveDir = new Vector3(moveX, moveY).normalized;
            if (moveX != 0 || moveY != 0) {
                // Not idle
                lastMoveDir = moveDir;
            }

#if ENABLE_INPUT_SYSTEM
            bool isSpaceKeyDown = Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            bool isSpaceKeyDown = Input.GetKeyDown(KeyCode.Space);
#endif
            if (canTeleport && isSpaceKeyDown) {
                isTeleportButtonDown = true;
            }
        }

        private void FixedUpdate() {
#if UNITY_6000_0_OR_NEWER
            characterRigidbody2D.linearVelocity = moveDir * MOVE_SPEED;
#else
            characterRigidbody2D.velocity = moveDir * MOVE_SPEED;
#endif
            if (isTeleportButtonDown) {
                // Instant teleport, doesn't go through walls
                Vector3 teleportPosition = transform.position + lastMoveDir * TELEPORT_AMOUNT;

                RaycastHit2D[] raycastHit2dArray = Physics2D.RaycastAll(transform.position, lastMoveDir, TELEPORT_AMOUNT, teleportLayerMask);
                foreach (RaycastHit2D raycastHit2D in raycastHit2dArray) {
                    if (raycastHit2D.transform == transform) {
                        // Hit self, ignore
                        continue;
                    }
                    if (raycastHit2D.collider != null) {
                        teleportPosition = raycastHit2D.point;
                    }
                }

                characterRigidbody2D.MovePosition(teleportPosition);
                isTeleportButtonDown = false;
            }
        }

    }

}