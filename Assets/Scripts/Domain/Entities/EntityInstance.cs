namespace Domain.Entities
{
    using UnityEngine;
    using Data.Config;

    public class EntityInstance
    {
        public EntityData Data { get; }

        public EntityInstance(EntityData data)
        {
            Data = data;
        }
    }
}