namespace Gameplay.World
{
    using UnityEngine;

    [RequireComponent(typeof(MeshRenderer))]
    public sealed class RoomPrimitiveTile : MonoBehaviour
    {
        private MeshRenderer _meshRenderer;
        private Collider _collider;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
        }

        public void Setup(Material material, Vector3 localPosition, Vector3 scale)
        {
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            
            _meshRenderer.sharedMaterial = material;
            transform.localPosition = localPosition;
            transform.localScale = scale;
        }

        public void SetTrigger(bool isTrigger)
        {
            if (_collider == null) _collider = GetComponent<Collider>();
            if (_collider != null)
            {
                _collider.isTrigger = isTrigger;
            }
        }
    }
}
