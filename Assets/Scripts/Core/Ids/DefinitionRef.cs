namespace Core.Ids
{
    using System;
    using Core.Content;
    using UnityEngine;

    /// <summary>
    /// Strongly-typed, serializable reference to an <see cref="IContentDefinition"/> of type <typeparamref name="T"/>.
    /// Prevents passing a mismatched definition ID (e.g., an Item ID where a Skill ID is required) at compile time,
    /// and caches the resolved definition instance at runtime.
    /// </summary>
    /// <typeparam name="T">The target content definition type.</typeparam>
    [Serializable]
    public struct DefinitionRef<T> : IEquatable<DefinitionRef<T>> where T : class, IContentDefinition
    {
        #region Serialized Fields

        [SerializeField]
        private ResourceId _id;

        #endregion

        #region Non-Serialized Runtime Cache

        [NonSerialized]
        private T _cached;

        #endregion

        #region Properties

        /// <summary>Gets the target <see cref="ResourceId"/>.</summary>
        public ResourceId Id => _id;

        /// <summary>Returns true if a non-empty <see cref="ResourceId"/> is assigned.</summary>
        public bool IsAssigned => !_id.IsEmpty;

        #endregion

        #region Constructors

        public DefinitionRef(ResourceId id)
        {
            _id = id;
            _cached = null;
        }

        public DefinitionRef(T definition)
        {
            _id = definition != null ? definition.ContentId : ResourceId.Empty;
            _cached = definition;
        }

        #endregion

        #region Resolution

        /// <summary>
        /// Resolves the referenced definition from the provided <see cref="IContentService"/>
        /// (or from <see cref="ServiceLocator"/> if null), caching the result for subsequent calls.
        /// </summary>
        public T Resolve(IContentService contentService = null)
        {
            TryResolve(out T result, contentService);
            return result;
        }

        /// <summary>
        /// Attempts to resolve the referenced definition without throwing on miss.
        /// </summary>
        public bool TryResolve(out T definition, IContentService contentService = null)
        {
            if (_cached != null)
            {
                definition = _cached;
                return true;
            }

            if (_id.IsEmpty)
            {
                definition = null;
                return false;
            }

            IContentService service = contentService;
            if (service == null)
            {
                ServiceLocator.TryGet(out service);
            }

            if (service != null && service.TryResolve(_id, out _cached))
            {
                definition = _cached;
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>Clears the cached object pointer (called when registries reload or unfreeze).</summary>
        public void InvalidateCache()
        {
            _cached = null;
        }

        #endregion

        #region Equality & Operators

        public bool Equals(DefinitionRef<T> other)
        {
            return _id.Equals(other._id);
        }

        public override bool Equals(object obj)
        {
            return obj is DefinitionRef<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public override string ToString()
        {
            return _id.ToString();
        }

        public static implicit operator DefinitionRef<T>(ResourceId id) => new DefinitionRef<T>(id);

        public static bool operator ==(DefinitionRef<T> left, DefinitionRef<T> right) => left.Equals(right);

        public static bool operator !=(DefinitionRef<T> left, DefinitionRef<T> right) => !left.Equals(right);

        #endregion
    }
}
