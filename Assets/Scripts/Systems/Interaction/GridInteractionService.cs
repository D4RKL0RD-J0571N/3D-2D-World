namespace Systems.Interaction
{
    using UnityEngine;
    using Core;
    using Domain.Interaction;

    public class GridInteractionService : IGameService
    {
        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// Converts a Unity Physics RaycastHit into a GridHitInfo indicating the exact integer block coordinates.
        /// Ensures the targeted cell in the RoomGridData is actually a solid block and not air.
        /// </summary>
        public GridHitInfo ResolveHit(RaycastHit hit)
        {
            if (hit.collider == null)
                return default;

            var link = hit.collider.GetComponent<Domain.Grid.GridRoomLink>();
            if (link == null || link.GridData == null)
            {
                return default;
            }

            // Push inside the hit block along the negative surface normal
            Vector3 pointInside = hit.point - hit.normal * 0.1f;
            Vector3 localPos = pointInside - link.WorldPosition;

            int localX = Mathf.FloorToInt(localPos.x);
            int localY = Mathf.FloorToInt(localPos.y);
            int localZ = Mathf.FloorToInt(localPos.z);

            // Verify that this voxel cell actually exists and is NOT Air (0)
            ushort blockId = link.GridData.GetBlock(localX, localY, localZ);
            if (blockId == 0)
            {
                return default;
            }

            // World block integer coordinate
            Vector3Int worldCoord = new Vector3Int(
                Mathf.FloorToInt(link.WorldPosition.x) + localX,
                Mathf.FloorToInt(link.WorldPosition.y) + localY,
                Mathf.FloorToInt(link.WorldPosition.z) + localZ
            );

            return new GridHitInfo
            {
                IsHit = true,
                BlockCoordinate = worldCoord,
                Normal = new Vector3Int(
                    Mathf.RoundToInt(hit.normal.x),
                    Mathf.RoundToInt(hit.normal.y),
                    Mathf.RoundToInt(hit.normal.z)
                ),
                HitObject = hit.collider.gameObject
            };
        }
    }
}
