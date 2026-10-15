namespace Systems.Pooling
{
    using System.Collections.Generic;
    public class ObjectPool<T> where T : class, new()
    {
        private readonly Queue<T> _pool = new Queue<T>();
        private readonly System.Func<T> _createFunc;
        private readonly System.Action<T> _resetAction;
        private readonly int _maxSize;

        public int CountAll { get; private set; }
        public int CountActive { get; private set; }
        public int CountInactive => _pool.Count;

        public ObjectPool(System.Func<T> createFunc = null, System.Action<T> resetAction = null, int maxSize = 100)
        {
            _createFunc = createFunc ?? (() => new T());
            _resetAction = resetAction;
            _maxSize = maxSize;
        }

        public T Get()
        {
            if (_pool.Count > 0)
            {
                T item = _pool.Dequeue();
                CountActive++;
                return item;
            }

            T newItem = _createFunc();
            CountAll++;
            CountActive++;
            return newItem;
        }

        public void Release(T item)
        {
            if (item == null) return;

            if (CountActive <= 0) return;

            CountActive--;
            _resetAction?.Invoke(item);
            _pool.Enqueue(item);
        }

        public void Clear()
        {
            _pool.Clear();
            CountAll = 0;
            CountActive = 0;
        }

        public void Warm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T item = _createFunc();
                _resetAction?.Invoke(item);
                _pool.Enqueue(item);
            }

            CountAll += count;
        }
    }
}