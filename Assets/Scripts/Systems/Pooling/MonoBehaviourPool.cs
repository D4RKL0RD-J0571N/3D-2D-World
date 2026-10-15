namespace Systems.Pooling
{
    using System.Collections.Generic;
    using UnityEngine;

    public interface IPool
    {
        int CountAll { get; }
        int CountActive { get; }
        int CountInactive { get; }
        void Clear();
    }

    public class MonoBehaviourPool<T> : IPool where T : MonoBehaviour
    {
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;

        public int CountAll { get; private set; }
        public int CountActive { get; private set; }
        public int CountInactive => _pool.Count;

        public MonoBehaviourPool(GameObject prefab, Transform parent = null, int maxSize = 100)
        {
            _prefab = prefab;
            _parent = parent;
            _maxSize = maxSize;
        }

        public T Get()
        {
            if (_pool.Count > 0)
            {
                T item = _pool.Dequeue();
                item.gameObject.SetActive(true);
                CountActive++;
                return item;
            }

            GameObject newObj = Object.Instantiate(_prefab, _parent);
            newObj.SetActive(true);
            T component = newObj.GetComponent<T>();
            if (component == null)
            {
                component = newObj.AddComponent<T>();
            }

            CountAll++;
            CountActive++;
            return component;
        }

        public void Release(T item)
        {
            if (item == null) return;

            if (CountActive <= 0) return;

            CountActive--;
            item.gameObject.SetActive(false);

            if (_parent != null)
            {
                item.transform.SetParent(_parent);
            }

            _pool.Enqueue(item);
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                T item = _pool.Dequeue();
                if (item != null && item.gameObject != null)
                {
                    Object.DestroyImmediate(item.gameObject);
                }
            }

            CountAll = 0;
            CountActive = 0;
        }

        public void Warm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject newObj = Object.Instantiate(_prefab, _parent);
                newObj.SetActive(false);
                T component = newObj.GetComponent<T>();
                if (component == null)
                {
                    component = newObj.AddComponent<T>();
                }
                _pool.Enqueue(component);
            }

            CountAll += count;
        }
    }
}