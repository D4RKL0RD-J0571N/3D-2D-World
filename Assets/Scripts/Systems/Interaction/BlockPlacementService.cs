namespace Systems.Interaction
{
    using UnityEngine;
    using Core;
    using Domain.Interaction;
    using Domain.Grid;
    using Systems.Meshing;

    public class BlockPlacementService : IGameService
    {
        private GreedyMeshingService _meshingService;
        // Reference the data directly instead of just an ID
        public BlockDefinition SelectedBlock;

        public void Initialize()
        {
            ServiceLocator.TryGet(out _meshingService);
            GameEventManager.AddListener<GridBlockClickedEvent>(OnBlockClicked);
            GameEventManager.AddListener<PlayerHotbarSlotSelectedEvent>(OnHotbarSlotSelected);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<GridBlockClickedEvent>(OnBlockClicked);
            GameEventManager.RemoveListener<PlayerHotbarSlotSelectedEvent>(OnHotbarSlotSelected);
        }

        private void OnBlockClicked(GridBlockClickedEvent evt)
        {
            if (!evt.HitInfo.IsHit || evt.HitInfo.HitObject == null) return;

            if (_meshingService == null)
            {
                ServiceLocator.TryGet(out _meshingService);
            }
            if (_meshingService == null) return;

            var link = evt.HitInfo.HitObject.GetComponent<GridRoomLink>();
            if (link == null || link.GridData == null) return;


            ushort blockIdToPlace = (ushort)((SelectedBlock != null) ? SelectedBlock.RuntimeId : 0);

            // TODO: Check if we actually CAN place it
            // This is where your "rules of what can be placed next to what" will live later.
            // Example: if (SelectedBlock.IsSolid) { ... }

            // BlockCoordinate is in world space, we need it in local space for the specific RoomGridData
            // Since the hit mesh is positioned at link.WorldPosition, we just subtract that
            Vector3 localPosF = (Vector3)evt.HitInfo.BlockCoordinate - link.WorldPosition;
            Vector3Int localCoord = new Vector3Int(
                Mathf.RoundToInt(localPosF.x),
                Mathf.RoundToInt(localPosF.y),
                Mathf.RoundToInt(localPosF.z)
            );


            // Left click (0) = Break (Set to Air / 0)
            // Right click (1) = Place (e.g., place Block ID 1 or another type)
            if (evt.MouseButton == 0)
            {
                // Break
                link.GridData.SetBlock(localCoord.x, localCoord.y, localCoord.z, 0);
                Debug.Log($"[BlockPlacementService] Broken block at World: {evt.HitInfo.BlockCoordinate} Local: {localCoord}");
            }
            else if (evt.MouseButton == 1)
            {
                Vector3Int placeWorldCoord = evt.HitInfo.BlockCoordinate + evt.HitInfo.Normal;
                Vector3 localPlaceF = (Vector3)placeWorldCoord - link.WorldPosition;
                Vector3Int localPlaceCoord = new Vector3Int(
                    Mathf.RoundToInt(localPlaceF.x),
                    Mathf.RoundToInt(localPlaceF.y),
                    Mathf.RoundToInt(localPlaceF.z)
                );

                // 3. Execute the placement
                link.GridData.SetBlock(localPlaceCoord.x, localPlaceCoord.y, localPlaceCoord.z, blockIdToPlace);
                Debug.Log($"[BlockPlacementService] Placed block at World: {placeWorldCoord} Local: {localPlaceCoord} ID: {blockIdToPlace}");
            }

            // Immediately rebuild mesh
            _meshingService.UpdateMeshForRoom(link.GridData, evt.HitInfo.HitObject);
        }

        private void OnHotbarSlotSelected(PlayerHotbarSlotSelectedEvent evt)
        {
            if (ServiceLocator.TryGet<HotbarService>(out var hotbar))
            {
                SelectedBlock = hotbar.GetBlockAtSlot(evt.SlotIndex);
                Debug.Log($"[BlockPlacementService] Active block set to: {SelectedBlock?.DisplayName ?? "Air/None"}");
            }
        }
    }
}
