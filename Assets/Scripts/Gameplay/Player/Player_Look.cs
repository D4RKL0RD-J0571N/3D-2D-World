using UnityEngine;
using Core;

namespace Gameplay.Player
{
    public class Player_Look : MonoBehaviour
    {
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private float _lookSensitivity = 0.08f;
        [SerializeField] private float _minPitch = -85f;
        [SerializeField] private float _maxPitch = 85f;

        private float _pitch;
        private Vector2 _lookInput;

        private void OnEnable()
        {
            GameEventManager.AddListener<PlayerLookInputEvent>(OnLookInput);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<PlayerLookInputEvent>(OnLookInput);
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (_cameraPivot == null)
            {
                return;
            }

            Vector2 lookDelta = _lookInput;
            _lookInput = Vector2.zero;

            if (lookDelta == Vector2.zero)
            {
                return;
            }

            transform.Rotate(0f, lookDelta.x * _lookSensitivity, 0f, Space.World);

            _pitch -= lookDelta.y * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void OnLookInput(PlayerLookInputEvent gameEvent)
        {
            _lookInput = gameEvent.LookDelta;
        }
    }
}
