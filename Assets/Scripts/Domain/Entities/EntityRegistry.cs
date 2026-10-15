namespace Domain.Entities
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using Data.Config;

    [CreateAssetMenu(fileName = "EntityRegistry", menuName = "3D2DWorld/Entities/Entity Registry")]
    public sealed class EntityRegistry : ScriptableObject
    {
        [SerializeField] private List<EntityData> _entities = new();

        private Dictionary<string, EntityData> _entityMap;

        public void Initialize()
        {
            _entityMap = new Dictionary<string, EntityData>();
            foreach (var entity in _entities.Where(e => e != null))
            {
                if (string.IsNullOrEmpty(entity.Id))
                {
                    Debug.LogWarning($"[EntityRegistry] Entity with empty ID found: {entity.name}");
                    continue;
                }

                if (!_entityMap.ContainsKey(entity.Id))
                {
                    _entityMap.Add(entity.Id, entity);
                }
                else
                {
                    Debug.LogError($"[EntityRegistry] Duplicate EntityId found: {entity.Id} on {entity.name}");
                }
            }
        }

        public EntityData GetEntity(string id)
        {
            if (_entityMap == null) Initialize();

            _entityMap.TryGetValue(id, out var data);
            return data;
        }
    }
}
