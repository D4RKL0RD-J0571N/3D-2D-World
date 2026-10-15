namespace Gameplay.World
{
    using UnityEngine;
    using Core;
    using Domain.Entities;

    /// <summary>
    /// Trigger placed inside each generated room to detect when the player enters or exits,
    /// notifying the AreaSchedulerService.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RoomTrackerTrigger : MonoBehaviour
    {
        [SerializeField] private string _roomId;
        [SerializeField] private Vector2Int _gridPosition;

        public void Configure(string roomId, Vector2Int gridPos, Vector3 size)
        {
            _roomId = roomId;
            _gridPosition = gridPos;

            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = new Vector3(0f, size.y * 0.5f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                GameEventManager.Publish(new RoomEnteredEvent
                {
                    RoomId = _roomId,
                    RoomGridPosition = _gridPosition
                });
            }
        }
    }
}
