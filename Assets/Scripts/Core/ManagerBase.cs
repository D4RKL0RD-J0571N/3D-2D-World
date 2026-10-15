using System.Collections.Generic;
using System.Linq;
using Core;
using Databases;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Generic base class for managers that handle runtime instances of type TInstance
    /// and optionally fetch definitions from a database of type TData.
    /// </summary>
    public abstract class ManagerBase<TInstance, TData, TDatabase> : IGameService
        where TData : ScriptableObject, IIdentifiable
        where TDatabase : Database<TData>
    {
        protected readonly Dictionary<string, TInstance> Instances = new();

        // Lazy reference to database
        protected TDatabase Database => ServiceLocator.Get<TDatabase>();

        /// <summary>
        /// Fetch the definition from the database, returns null if the database is not registered.
        /// </summary>
        protected TData GetDefinition(string id) => Database?.Get(id);

        #region IGameService

        public virtual void Initialize()
        {
            ServiceLocator.Register(this);
            OnInitialized();
        }

        protected virtual void OnInitialized() { }

        public virtual void Shutdown() => Instances.Clear();

        #endregion

        #region Instance Management

        public bool Has(string id) => Instances.ContainsKey(id);

        public TInstance Get(string id)
        {
            Instances.TryGetValue(id, out var instance);
            return instance;
        }

        public void Add(string id, TInstance instance)
        {
            if (string.IsNullOrEmpty(id) || instance == null) return;
            Instances[id] = instance;
        }

        public void Remove(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            Instances.Remove(id);
        }

        public IReadOnlyList<TInstance> GetAll() => Instances.Values.ToList();

        #endregion
    }
}