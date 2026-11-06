using UnityEngine;
using UnityEngine.UI;

namespace MagicPigGames.Northstar
{
    [System.Serializable]
    [RequireComponent(typeof(CompassIcon))]
    public class RequireLineOfSightCompassIcon : MonoBehaviour
    {
        [Header("Options")] 
        public Transform playerTransform;
        public bool mustBeInCameraView = true;
        public LayerMask layerMask = -1;

        private Transform _playerTransform;
        private Image _image;
        private CompassIcon _compassIcon;
        private Camera _camera;
        private Collider _targetCollider;
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private void Start()
        {
            _playerTransform = playerTransform;
            if (_playerTransform == null && Camera.main != null)
                _playerTransform = Camera.main.transform;

            _image = GetComponent<Image>();
            _compassIcon = GetComponent<CompassIcon>();

            if (_playerTransform == null)
            {
                Debug.LogError("No transform was found! Should have found the camera if no player was provided");
                Destroy(this);
                return;
            }

            _camera = _playerTransform.GetComponent<Camera>();
            if (_camera == null)
                _camera = Camera.main;
        }

        private void LateUpdate() => _image.enabled = HasLineOfSight();

        private bool HasLineOfSight()
        {
            if (_compassIcon.target == null || NorthstarSystem.Instance == null)
                return false;

            var playerPosition = _playerTransform.position;
            var targetPosition = _compassIcon.target.position;
            var direction = targetPosition - playerPosition;
            var distance = direction.magnitude;

            // First we check if the target is within the NorthstarSystem's detection range
            if (distance > NorthstarSystem.Instance.detectionRange)
                return false;

            if (mustBeInCameraView && _camera != null)
            {
                // Then we check if the target is within the camera's view frustrum. Colliders are often
                // on children of the tracked object, so look there too.
                if (_targetCollider == null)
                    _targetCollider = _compassIcon.target.GetComponentInChildren<Collider>();

                if (_targetCollider != null)
                {
                    GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
                    if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, _targetCollider.bounds))
                        return false;
                }
            }

            /*
             * NOTE: If the icon is flickering in/out, you may be colliding with your own collider, or the player etc.
             * Put that on it's own layer (often "Player") and set the LayerMask to ignore that layer.
             */

            // Cast a ray toward the target, and return true if the first hit is the target (or one of its
            // children -- character rigs usually have their colliders on child bones).
            var ray = new Ray(playerPosition, direction);
            if (Physics.Raycast(ray, out var hit, distance + 1, layerMask))
                return hit.collider.transform.IsChildOf(_compassIcon.target);

            // If it didn't hit the target, we return false
            return false;
        }
    }

}
