namespace Domain.Persistence
{
    using System;
    using UnityEngine;

    [Serializable]
    public class RoomSaveData
    {
        public Vector3 WorldPosition;
        public Vector3Int Size;
        public ushort[] VoxelData;
    }

    [Serializable]
    public class InventorySlotSaveData
    {
        public string ItemId;
        public int Count;
    }

    [Serializable]
    public class EntitySaveData
    {
        public string EntityId;
        public string InstanceId; // Unique ID for this specific instance
        public Vector3 Position;
        public Quaternion Rotation;
        // Basic stats
        public int CurrentHealth;
        public int Level;
        public float Experience;
        public InventorySlotSaveData[] Inventory;
    }

    [Serializable]
    public class LevelSaveData
    {
        public Domain.LevelGeneration.LevelLayout OriginalLayout;
        public RoomSaveData[] Grids;
        public EntitySaveData[] Entities;
        public EntitySaveData PlayerState; 
    }
}
