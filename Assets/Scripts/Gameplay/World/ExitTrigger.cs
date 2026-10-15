namespace Gameplay.World
{
    using UnityEngine;
    using Core;

    [RequireComponent(typeof(BoxCollider))]
    public sealed class ExitTrigger : MonoBehaviour
    {
        private void Awake()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        public void Configure(Vector3 size)
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && enabled)
            {
                GameEventManager.Publish(new LevelCompletedEvent());
                enabled = false; // Disable just this script to prevent multiple fires
            }
        }
    }
}
