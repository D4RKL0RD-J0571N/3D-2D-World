using UnityEngine;

namespace Gameplay.Player
{
    [DisallowMultipleComponent]
    public class Player_CameraRig : MonoBehaviour
    {
        [SerializeField] private float _eyeHeight = 2f;
        [SerializeField] private Camera _camera;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponentInChildren<Camera>(true);
            }
        }

        public void SetEyeHeight(float eyeHeight)
        {
            _eyeHeight = eyeHeight;
        }

        private void LateUpdate()
        {
            Vector3 localPosition = transform.localPosition;
            localPosition.x = 0f;
            localPosition.y = _eyeHeight;
            localPosition.z = 0f;
            transform.localPosition = localPosition;

            if (_camera != null)
            {
                _camera.transform.localPosition = Vector3.zero;
                _camera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
