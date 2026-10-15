namespace Domain.Entities
{
    using System;
    using UnityEngine;

    public enum AreaSimulationTier
    {
        Active,   // Player is inside or adjacent: full 3D GameObjects, colliders, ticking AI
        Dormant,  // Nearby: visual meshes intact, entities cached in lightweight state
        Purged    // Distant: unrendered, meshes cleared from memory, entities saved to disk/registry
    }

    public enum EntityLifecyclePolicy
    {
        PersistAndSleep,  // Retains state, regenerates over time when player leaves
        DespawnAndRespawn // Despawns on distance/look-away, respawns fresh upon re-entry
    }

    [Serializable]
    public class VirtualEntityState
    {
        public string InstanceId;
        public string EntityBlueprintId;
        public string RoomId;
        public Vector3 Position;
        public Quaternion Rotation;
        public int CurrentHealth;
        public int MaxHealth;
        public float LastActiveTimestamp;
        public EntityLifecyclePolicy LifecyclePolicy;

        public VirtualEntityState()
        {
            InstanceId = Guid.NewGuid().ToString("N");
            LastActiveTimestamp = Time.time;
        }
    }
}
