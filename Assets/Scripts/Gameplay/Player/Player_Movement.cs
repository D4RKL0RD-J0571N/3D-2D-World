using UnityEngine;
using Core;

namespace Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class Player_Movement : MonoBehaviour
    {
        [SerializeField] private CharacterController _controller;
        [SerializeField] private Player_CameraRig _cameraRig;
        [SerializeField] private float _moveSpeed = 5.0f;
        [SerializeField] private float _runMultiplier = 1.6f;
        [SerializeField] private float _crouchMultiplier = 0.55f;
        [SerializeField] private float _jumpHeight = 1.35f;
        [SerializeField] private float _gravity = -18.0f;
        [SerializeField] private float _stickToGroundForce = -2.0f;
        [SerializeField] private float _standHeight = 1.8f;
        [SerializeField] private float _crouchHeight = 1.1f;
        [SerializeField] private float _cameraStandHeight = 1.6f;
        [SerializeField] private float _cameraCrouchHeight = 1.0f;
        [SerializeField] private float _stepOffset = 1.05f;

        private void Awake()
        {
            if (_controller != null)
            {
                _controller.stepOffset = _stepOffset;
            }
        }

        private Vector3 _verticalVelocity;
        private float _currentSpeedMultiplier = 1.0f;
        private Vector2 _moveInput;
        private bool _runHeld;
        private bool _crouchHeld;
        private bool _jumpQueued;

        public bool IsCrouching { get; private set; }

        private void OnEnable()
        {
            GameEventManager.AddListener<PlayerMoveInputEvent>(OnMoveInput);
            GameEventManager.AddListener<PlayerRunInputEvent>(OnRunInput);
            GameEventManager.AddListener<PlayerJumpInputEvent>(OnJumpInput);
            GameEventManager.AddListener<PlayerCrouchInputEvent>(OnCrouchInput);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<PlayerMoveInputEvent>(OnMoveInput);
            GameEventManager.RemoveListener<PlayerRunInputEvent>(OnRunInput);
            GameEventManager.RemoveListener<PlayerJumpInputEvent>(OnJumpInput);
            GameEventManager.RemoveListener<PlayerCrouchInputEvent>(OnCrouchInput);
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            IsCrouching = _crouchHeld;
            _currentSpeedMultiplier = IsCrouching ? _crouchMultiplier : (_runHeld ? _runMultiplier : 1.0f);
            ApplyStance();

            Vector2 moveInput = Vector2.ClampMagnitude(_moveInput, 1f);
            Vector3 horizontalMove = transform.forward * moveInput.y + transform.right * moveInput.x;
            horizontalMove.y = 0f;
            horizontalMove = Vector3.ClampMagnitude(horizontalMove, 1f) * (_moveSpeed * _currentSpeedMultiplier);

            if (_controller.isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = _stickToGroundForce;
            }

            if (_jumpQueued && _controller.isGrounded && !IsCrouching)
            {
                _verticalVelocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }

            _jumpQueued = false;

            _verticalVelocity.y += _gravity * Time.deltaTime;

            Vector3 motion = (horizontalMove + _verticalVelocity) * Time.deltaTime;
            _controller.Move(motion);
        }

        private void OnMoveInput(PlayerMoveInputEvent gameEvent)
        {
            _moveInput = gameEvent.Move;
        }

        private void OnRunInput(PlayerRunInputEvent gameEvent)
        {
            _runHeld = gameEvent.Held;
        }

        private void OnJumpInput(PlayerJumpInputEvent gameEvent)
        {
            _jumpQueued = true;
        }

        private void OnCrouchInput(PlayerCrouchInputEvent gameEvent)
        {
            _crouchHeld = gameEvent.Held;
        }

        private void ApplyStance()
        {
            float targetHeight = IsCrouching ? _crouchHeight : _standHeight;
            float targetCameraHeight = IsCrouching ? _cameraCrouchHeight : _cameraStandHeight;

            if (_controller.height != targetHeight)
            {
                _controller.height = targetHeight;
                _controller.center = new Vector3(0f, targetHeight * 0.5f, 0f);
            }

            if (_cameraRig != null)
            {
                _cameraRig.SetEyeHeight(targetCameraHeight);
            }
        }
    }
}
