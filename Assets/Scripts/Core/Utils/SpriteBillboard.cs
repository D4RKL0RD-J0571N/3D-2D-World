namespace Core.Utils
{
    using UnityEngine;

    public enum BillboardAxis
    {
        AllAxes, // Free camera tracking
        YAxisOnly // Cylindrical billboard (keeps character upright)
    }

    /// <summary>
    /// Utility component that aligns a sprite or quad towards the active Camera.
    /// Can be swapped or extended with animated billboard sheets.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Utils/Sprite Billboard")]
    [DisallowMultipleComponent]
    public class SpriteBillboard : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private BillboardAxis _axis = BillboardAxis.YAxisOnly;
        [SerializeField] private bool _invertForward = false;

        private Camera _mainCamera;

        private void Start()
        {
            EnsureCamera();
        }

        private void LateUpdate()
        {
            if (_mainCamera == null && !EnsureCamera())
                return;

            Vector3 cameraPosition = _mainCamera.transform.position;

            if (_axis == BillboardAxis.YAxisOnly)
            {
                Vector3 lookTarget = cameraPosition;
                lookTarget.y = transform.position.y; // Keep level with this object

                Vector3 direction = _invertForward
                    ? transform.position - lookTarget
                    : lookTarget - transform.position;

                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                }
            }
            else
            {
                Vector3 direction = _invertForward
                    ? transform.position - cameraPosition
                    : cameraPosition - transform.position;

                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction, _mainCamera.transform.up);
                }
            }
        }

        private bool EnsureCamera()
        {
            _mainCamera = Camera.main;
            return _mainCamera != null;
        }
    }
}
