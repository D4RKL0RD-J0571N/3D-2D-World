namespace Systems.Pooling
{
    using System.Collections.Generic;
    using Core;
    using UnityEngine;

    public class PoolingManager : IGameService
    {
        private Dictionary<string, IPool> _pools = new();
        private Transform _poolContainer;

        public void Initialize()
        {
            _poolContainer = new GameObject("PoolContainer").transform;
        }

        public MonoBehaviourPool<T> GetPool<T>(GameObject prefab, int maxSize = 50) where T : MonoBehaviour
        {
            string poolKey = $"{prefab.name}_{typeof(T).Name}";

            if (!_pools.ContainsKey(poolKey))
            {
                var pool = new MonoBehaviourPool<T>(prefab, _poolContainer, maxSize);
                _pools[poolKey] = pool;

                // Warm the pool with a few objects
                pool.Warm(Mathf.Min(5, maxSize / 4));
            }

            return _pools[poolKey] as MonoBehaviourPool<T>;
        }

        public T GetPooledObject<T>(GameObject prefab) where T : MonoBehaviour
        {
            var pool = GetPool<T>(prefab);
            return pool.Get();
        }

        public void ReleasePooledObject<T>(T item, GameObject prefab) where T : MonoBehaviour
        {
            if (item == null) return;

            string poolKey = $"{prefab.name}_{typeof(T).Name}";
            if (_pools.ContainsKey(poolKey))
            {
                var pool = _pools[poolKey] as MonoBehaviourPool<T>;
                pool?.Release(item);
            }
            else
            {
                // If no pool exists, just destroy the object
                Object.Destroy(item.gameObject);
            }
        }

        public void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool?.Clear();
            }
            _pools.Clear();

            Debug.Log("All object pools cleared");
        }

        public void Shutdown()
        {
            ClearAllPools();

            if (_poolContainer != null)
            {
                Object.DestroyImmediate(_poolContainer.gameObject);
                _poolContainer = null;
            }
        }

        public void LogPoolStatistics()
        {
            Debug.Log("=== Pool Statistics ===");
            foreach (var kvp in _pools)
            {
                var pool = kvp.Value;
                Debug.Log($"Pool {kvp.Key}: Active={pool.CountActive}, Inactive={pool.CountInactive}, Total={pool.CountAll}");
            }
        }

        public int GetTotalPooledObjects()
        {
            int total = 0;
            foreach (var pool in _pools.Values)
            {
                total += pool.CountAll;
            }
            return total;
        }

        public int GetTotalActiveObjects()
        {
            int total = 0;
            foreach (var pool in _pools.Values)
            {
                total += pool.CountActive;
            }
            return total;
        }

    }
}