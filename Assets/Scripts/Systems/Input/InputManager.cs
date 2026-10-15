namespace Systems.Input
{
    using Core;
    using Domain.Entities.StateMachine;
    using Domain.Grid;

    using Gameplay.Player;
    using UnityEngine;
    using UnityEngine.InputSystem;

    public class InputManager : IGameService
    {
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _runAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private InputAction _pauseAction;
        private InputAction _confirmAction;
        private InputAction _cancelAction;
        private InputAction _interactPrimaryAction;
        private InputAction _interactSecondaryAction;
        private InputAction _hotbarAction;
        private InputAction _saveAction;
        private GameStateMachine _gameStateManager;

        private float _deadzone = 0.1f;
        private float _sensitivity = 1.0f;
        private bool _useRawInput = false;

        public void Initialize()
        {
            _gameStateManager = ServiceLocator.Get<GameStateMachine>();

            _moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
            _moveAction.AddBinding("<Gamepad>/dpad");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _lookAction = new InputAction("Look", binding: "<Mouse>/delta");
            _lookAction.AddBinding("<Gamepad>/rightStick");

            _runAction = new InputAction("Run");
            _runAction.AddBinding("<Keyboard>/leftShift");
            _runAction.AddBinding("<Gamepad>/leftStickPress");

            _jumpAction = new InputAction("Jump");
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");

            _crouchAction = new InputAction("Crouch");
            _crouchAction.AddBinding("<Keyboard>/leftCtrl");
            _crouchAction.AddBinding("<Gamepad>/rightStickPress");

            _pauseAction = new InputAction("Pause", binding: "<Keyboard>/escape");
            _pauseAction.AddBinding("<Keyboard>/p");
            _pauseAction.AddBinding("<Gamepad>/start");

            _confirmAction = new InputAction("Confirm", binding: "<Keyboard>/enter");
            _confirmAction.AddBinding("<Keyboard>/space");
            _confirmAction.AddBinding("<Gamepad>/buttonSouth");

            _cancelAction = new InputAction("Cancel", binding: "<Keyboard>/backspace");
            _cancelAction.AddBinding("<Gamepad>/buttonEast");

            _interactPrimaryAction = new InputAction("InteractPrimary", binding: "<Mouse>/leftButton");
            _interactPrimaryAction.AddBinding("<Gamepad>/rightTrigger");

            _interactSecondaryAction = new InputAction("InteractSecondary", binding: "<Mouse>/rightButton");
            _interactSecondaryAction.AddBinding("<Gamepad>/leftTrigger");

            _saveAction = new InputAction("Save", binding: "<Keyboard>/f5");
            _hotbarAction = new InputAction("Hotbar");

            _hotbarAction.AddBinding("<Keyboard>/1");
            _hotbarAction.AddBinding("<Keyboard>/2");
            _hotbarAction.AddBinding("<Keyboard>/3");
            _hotbarAction.AddBinding("<Keyboard>/4");
            _hotbarAction.AddBinding("<Keyboard>/5");
            _hotbarAction.AddBinding("<Keyboard>/6");
            _hotbarAction.AddBinding("<Keyboard>/7");
            _hotbarAction.AddBinding("<Keyboard>/8");
            _hotbarAction.AddBinding("<Keyboard>/9");

            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;
            _lookAction.performed += OnLook;
            _lookAction.canceled += OnLook;
            _runAction.started += OnRun;
            _runAction.canceled += OnRun;
            _jumpAction.performed += OnJump;
            _crouchAction.started += OnCrouch;
            _crouchAction.canceled += OnCrouch;
            _pauseAction.performed += OnPause;
            _confirmAction.performed += OnConfirm;
            _interactPrimaryAction.performed += OnInteractPrimary;
            _interactSecondaryAction.performed += OnInteractSecondary;
            _saveAction.performed += OnSave;
            _hotbarAction.performed += OnHotbar;

            _moveAction.Enable();
            _lookAction.Enable();
            _runAction.Enable();
            _jumpAction.Enable();
            _crouchAction.Enable();
            _pauseAction.Enable();
            _confirmAction.Enable();
            _cancelAction.Enable();
            _interactPrimaryAction.Enable();
            _interactSecondaryAction.Enable();
            _saveAction.Enable();
            _hotbarAction.Enable();
        }

        private void OnSave(InputAction.CallbackContext context)
        {
            if (context.performed) GameEventManager.Publish(new Domain.Persistence.GameSaveRequestedEvent());
        }

        private void OnHotbar(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayInput())
            {
                return;
            }

            int slotIndex = context.control.device is Keyboard
                ? context.control.path[^1] - '1'
                : -1;

            if (slotIndex >= 0 && slotIndex < 9)
            {
                GameEventManager.Publish(
                    new PlayerHotbarSlotSelectedEvent
                    {
                        SlotIndex = slotIndex
                    });
            }
        }

        private void OnInteractPrimary(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayInput()) return;
            if (context.performed) GameEventManager.Publish(new PlayerInteractPrimaryInputEvent());
        }

        private void OnInteractSecondary(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayInput()) return;
            if (context.performed) GameEventManager.Publish(new PlayerInteractSecondaryInputEvent());
        }

        private void OnConfirm(InputAction.CallbackContext context)
        {
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayInput())
            {
                return;
            }

            Vector2 input = context.ReadValue<Vector2>();
            if (input.magnitude < _deadzone)
            {
                input = Vector2.zero;
            }
            else if (!_useRawInput)
            {
                input = Vector2.ClampMagnitude(input * _sensitivity, 1.0f);
            }

            GameEventManager.Publish(new PlayerMoveInputEvent { Move = input });
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayInput())
            {
                return;
            }

            Vector2 input = context.ReadValue<Vector2>();
            Vector2 lookDelta = _useRawInput ? input : input * _sensitivity;
            GameEventManager.Publish(new PlayerLookInputEvent { LookDelta = lookDelta });
        }

        private void OnRun(InputAction.CallbackContext context)
        {
            GameEventManager.Publish(new PlayerRunInputEvent { Held = CanReadGameplayInput() && context.ReadValueAsButton() });
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            if (!CanReadGameplayInput())
            {
                return;
            }

            if (context.performed)
            {
                GameEventManager.Publish(new PlayerJumpInputEvent());
            }
        }

        private void OnCrouch(InputAction.CallbackContext context)
        {
            GameEventManager.Publish(new PlayerCrouchInputEvent { Held = CanReadGameplayInput() && context.ReadValueAsButton() });
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            if (_gameStateManager.CurrentStateType == GameStateType.Playing || _gameStateManager.CurrentStateType == GameStateType.Paused)
            {
            }
        }

        private bool CanReadGameplayInput()
        {
            return _gameStateManager == null || _gameStateManager.CurrentStateType == GameStateType.Playing;
        }

        public void Shutdown()
        {
            if (_moveAction != null)
            {
                _moveAction.performed -= OnMove;
                _moveAction.canceled -= OnMove;
                _moveAction.Disable();
                _moveAction.Dispose();
            }

            if (_lookAction != null)
            {
                _lookAction.performed -= OnLook;
                _lookAction.canceled -= OnLook;
                _lookAction.Disable();
                _lookAction.Dispose();
            }

            if (_runAction != null)
            {
                _runAction.started -= OnRun;
                _runAction.canceled -= OnRun;
                _runAction.Disable();
                _runAction.Dispose();
            }

            if (_jumpAction != null)
            {
                _jumpAction.performed -= OnJump;
                _jumpAction.Disable();
                _jumpAction.Dispose();
            }

            if (_crouchAction != null)
            {
                _crouchAction.started -= OnCrouch;
                _crouchAction.canceled -= OnCrouch;
                _crouchAction.Disable();
                _crouchAction.Dispose();
            }

            if (_pauseAction != null)
            {
                _pauseAction.performed -= OnPause;
                _pauseAction.Disable();
                _pauseAction.Dispose();
            }

            if (_confirmAction != null)
            {
                _confirmAction.performed -= OnConfirm;
                _confirmAction.Disable();
                _confirmAction.Dispose();
            }

            if (_cancelAction != null)
            {
                _cancelAction.Disable();
                _cancelAction.Dispose();
            }

            if (_interactPrimaryAction != null)
            {
                _interactPrimaryAction.performed -= OnInteractPrimary;
                _interactPrimaryAction.Disable();
                _interactPrimaryAction.Dispose();
            }

            if (_interactSecondaryAction != null)
            {
                _interactSecondaryAction.performed -= OnInteractSecondary;
                _interactSecondaryAction.Disable();
                _interactSecondaryAction.Dispose();
            }

            if (_saveAction != null)
            {
                _saveAction.performed -= OnSave;
                _saveAction.Disable();
                _saveAction.Dispose();
            }

            if (_hotbarAction != null)
            {
                _hotbarAction.performed -= OnHotbar;
                _hotbarAction.Disable();
                _hotbarAction.Dispose();
            }
        }
    }
}
