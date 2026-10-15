namespace Databases
{
    using Core;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;

    public abstract class Database<T> : IGameService where T : ScriptableObject, IIdentifiable
    {
        protected List<T> Definitions = new();
        protected abstract string ResourcesPath { get; }

        public virtual void Initialize()
        {
            TryRegisterSelf();
            LoadAll();
        }

        public virtual void Shutdown() { }

        public void LoadAll()
        {
            Definitions = Resources.LoadAll<T>(ResourcesPath).ToList();
        }

        public virtual T Get(string id) => Definitions.FirstOrDefault(d => d.Id == id);
        public IReadOnlyList<T> GetAll() => Definitions;

        // The method the Drawers need
        public string[] GetAllIDs() => Definitions.Select(d => d.Id).ToArray();

        protected void TryRegisterSelf()
        {
            try
            {
                ServiceLocator.Register(this);
            }
            catch
            {
                throw new System.Exception("Failed to register database");

            }

        }

        // Helper for Editor Drawers to get data without ServiceLocator
        public static string[] GetIDsForEditor(string path)
        {
            return Resources.LoadAll<T>(path).Select(d => d.Id).ToArray();
        }
    }
}