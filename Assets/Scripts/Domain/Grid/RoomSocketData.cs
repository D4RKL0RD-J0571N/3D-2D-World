namespace Domain.Grid
{
    using System;
    using UnityEngine;

    public enum SocketType
    {
        Walkway,
        PlayerSpawn,
        EnemySpawn,
        NpcSpawn,
        Chest,
        Interactable,
        PropLight
    }

    [Serializable]
    public struct RoomSocket
    {
        public Vector3Int LocalGridPosition;
        public Vector3 WorldPosition;
        public SocketType Type;
        public Vector3 LookDirection;

        public RoomSocket(Vector3Int localPos, Vector3 worldPos, SocketType type, Vector3 lookDir)
        {
            LocalGridPosition = localPos;
            WorldPosition = worldPos;
            Type = type;
            LookDirection = lookDir;
        }
    }
}
