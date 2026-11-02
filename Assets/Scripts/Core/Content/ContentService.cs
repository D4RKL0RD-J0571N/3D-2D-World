namespace Core.Content
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;

    /// <summary>
    /// Default implementation of <see cref="IContentService"/>.
    /// Maintains a type-indexed map of <see cref="ContentRegistry{T}"/> instances and coordinates registry freezing/unfreezing.
    /// </summary>
    public sealed class ContentService : IContentService
    {
        #region Fields

        private readonly Dictionary<Type, IContentRegistry> _registries = new();
        private bool _isFrozen;

        #endregion

        #region IGameService

        public void Initialize()
        {
            _isFrozen = false;
        }

        public void Shutdown()
        {
            foreach (var registry in _registries.Values)
            {
                registry.Clear();
            }

            _registries.Clear();
            _isFrozen = false;
        }

        #endregion

        #region IContentService

        public ContentRegistry<T> GetRegistry<T>() where T : class, IContentDefinition
        {
            Type type = typeof(T);
            if (_registries.TryGetValue(type, out IContentRegistry existing))
            {
                return (ContentRegistry<T>)existing;
            }

            var created = new ContentRegistry<T>();
            if (_isFrozen)
            {
                created.Freeze();
            }

            _registries[type] = created;
            return created;
        }

        public bool TryResolve<T>(ResourceId id, out T definition) where T : class, IContentDefinition
        {
            if (id.IsEmpty)
            {
                definition = null;
                return false;
            }

            return GetRegistry<T>().TryGet(id, out definition);
        }

        public bool TryResolveByLegacyAlias<T>(string legacyOrCanonicalId, out T definition) where T : class, IContentDefinition
        {
            if (string.IsNullOrWhiteSpace(legacyOrCanonicalId))
            {
                definition = null;
                return false;
            }

            return GetRegistry<T>().TryGetByLegacyAlias(legacyOrCanonicalId, out definition);
        }

        public void FreezeAll()
        {
            _isFrozen = true;
            foreach (var registry in _registries.Values)
            {
                registry.Freeze();
            }
        }

        public void UnfreezeAll()
        {
            _isFrozen = false;
            foreach (var registry in _registries.Values)
            {
                registry.UnfreezeForReload();
            }
        }

        #endregion
    }
}
