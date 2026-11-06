namespace Core.Content
{
    using System;
    using System.Collections.Generic;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Non-generic base interface for administrative operations across all registries in <see cref="IContentService"/>.
    /// </summary>
    public interface IContentRegistry
    {
        Type DefinitionType { get; }
        int Count { get; }
        bool IsFrozen { get; }
        void Freeze();
        void UnfreezeForReload();
        void Clear();
    }

    /// <summary>
    /// Unified, type-safe runtime registry for <see cref="IContentDefinition"/> records of type <typeparamref name="T"/>.
    /// Supports O(1) <see cref="ResourceId"/> lookup, case-insensitive legacy string alias resolution,
    /// secondary index tables (e.g., voxel <c>ushort</c> runtime IDs), provenance tracking, and freeze protection.
    /// </summary>
    public sealed class ContentRegistry<T> : IContentRegistry where T : class, IContentDefinition
    {
        #region Fields

        private readonly Dictionary<ResourceId, ContentRegistryEntry<T>> _entries = new();
        private readonly Dictionary<string, ResourceId> _legacyAliases = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<T> _orderedDefinitions = new();
        private readonly Dictionary<string, ISecondaryIndex> _secondaryIndices = new(StringComparer.Ordinal);
        private bool _isFrozen;

        #endregion

        #region Properties

        public Type DefinitionType => typeof(T);
        public int Count => _entries.Count;
        public bool IsFrozen => _isFrozen;

        #endregion

        #region Registration & Lifecycle

        /// <summary>
        /// Registers a definition into the registry using the specified conflict policy and optional legacy string alias.
        /// </summary>
        public bool Register(
            T definition,
            string modId = ResourceId.DefaultNamespace,
            ContentRegistrationPolicy policy = ContentRegistrationPolicy.OverrideExisting,
            string explicitLegacyAlias = null)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            ResourceId id = definition.ContentId;
            if (id.IsEmpty)
            {
                throw new ArgumentException($"Cannot register {typeof(T).Name} '{definition.DisplayName}' with an empty ResourceId.", nameof(definition));
            }

            if (_isFrozen)
            {
                throw new RegistryFrozenException(typeof(T).Name, id);
            }

            string effectiveModId = string.IsNullOrWhiteSpace(modId)
                ? (string.IsNullOrWhiteSpace(definition.SourceModId) ? ResourceId.DefaultNamespace : definition.SourceModId)
                : modId;

            string alias = !string.IsNullOrWhiteSpace(explicitLegacyAlias)
                ? explicitLegacyAlias.Trim()
                : definition.Id;

            if (_entries.TryGetValue(id, out ContentRegistryEntry<T> existing))
            {
                if (policy == ContentRegistrationPolicy.AddOnly)
                {
                    Debug.LogWarning($"[ContentRegistry<{typeof(T).Name}>] AddOnly conflict: '{id}' already registered by '{existing.SourceModId}'. Skipping registration from '{effectiveModId}'.");
                    return false;
                }

                ReplaceOrderedDefinition(existing.Definition, definition);
                existing.Definition = definition;
                existing.LastModifiedByModId = effectiveModId;
                existing.ModificationCount++;

                if (!string.IsNullOrWhiteSpace(alias))
                {
                    existing.LegacyAlias = alias;
                    _legacyAliases[alias] = id;
                }

                UpdateSecondaryIndices(definition);
                return true;
            }

            var newEntry = new ContentRegistryEntry<T>(id, definition, effectiveModId, effectiveModId, alias);
            _entries.Add(id, newEntry);
            _orderedDefinitions.Add(definition);

            RegisterAliasesForDefinition(id, definition, alias);
            UpdateSecondaryIndices(definition);
            return true;
        }

        /// <summary>
        /// Explicitly binds an additional legacy string alias (such as an old <c>ACT_0001</c> ID or asset name) to a registered <see cref="ResourceId"/>.
        /// </summary>
        public void RegisterLegacyAlias(string legacyId, ResourceId targetId)
        {
            if (string.IsNullOrWhiteSpace(legacyId) || targetId.IsEmpty)
            {
                return;
            }

            if (_isFrozen)
            {
                throw new RegistryFrozenException(typeof(T).Name, targetId);
            }

            _legacyAliases[legacyId.Trim()] = targetId;
        }

        /// <summary>Locks the registry against further mutations during active gameplay.</summary>
        public void Freeze()
        {
            _isFrozen = true;
        }

        /// <summary>Unlocks the registry so mod profiles or hot-reloads can register or patch definitions.</summary>
        public void UnfreezeForReload()
        {
            _isFrozen = false;
        }

        /// <summary>Clears all entries and indices (unlocking the registry).</summary>
        public void Clear()
        {
            _isFrozen = false;
            _entries.Clear();
            _legacyAliases.Clear();
            _orderedDefinitions.Clear();

            foreach (var index in _secondaryIndices.Values)
            {
                index.Clear();
            }
        }

        #endregion

        #region Queries & Resolution

        /// <summary>Returns true if the canonical <see cref="ResourceId"/> exists in this registry.</summary>
        public bool Contains(ResourceId id)
        {
            return !id.IsEmpty && _entries.ContainsKey(id);
        }

        /// <summary>Attempts O(1) lookup by canonical <see cref="ResourceId"/>.</summary>
        public bool TryGet(ResourceId id, out T definition)
        {
            if (!id.IsEmpty && _entries.TryGetValue(id, out ContentRegistryEntry<T> entry))
            {
                definition = entry.Definition;
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>Returns the definition for <paramref name="id"/>, or null if not found.</summary>
        public T GetOrNull(ResourceId id)
        {
            return TryGet(id, out T def) ? def : null;
        }

        /// <summary>Attempts to retrieve the full provenance entry (<c>SourceModId</c>, <c>LastModifiedByModId</c>) for <paramref name="id"/>.</summary>
        public bool TryGetEntry(ResourceId id, out ContentRegistryEntry<T> entry)
        {
            if (!id.IsEmpty)
            {
                return _entries.TryGetValue(id, out entry);
            }

            entry = null;
            return false;
        }

        /// <summary>
        /// Resolves a definition by a legacy string ID (e.g., <c>"ITM_0001"</c>, <c>"BLCK_0010"</c>, or asset name),
        /// or by parsing <paramref name="legacyOrCanonicalId"/> directly if it is already in <c>namespace:path</c> format.
        /// </summary>
        public bool TryGetByLegacyAlias(string legacyOrCanonicalId, out T definition)
        {
            if (string.IsNullOrWhiteSpace(legacyOrCanonicalId))
            {
                definition = null;
                return false;
            }

            string trimmed = legacyOrCanonicalId.Trim();

            if (_legacyAliases.TryGetValue(trimmed, out ResourceId mappedId) && TryGet(mappedId, out definition))
            {
                return true;
            }

            if (ResourceId.TryParse(trimmed, out ResourceId parsedId) && TryGet(parsedId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>Returns a read-only view of all registered definitions in registration order.</summary>
        public IReadOnlyList<T> GetAll()
        {
            return _orderedDefinitions;
        }

        #endregion

        #region Secondary Index Support

        /// <summary>
        /// Registers a secondary O(1) lookup index on <typeparamref name="TKey"/> (e.g., <c>ushort RuntimeId</c> for voxel blocks).
        /// Immediately indexes all currently registered definitions.
        /// </summary>
        public void RegisterSecondaryIndex<TKey>(string indexName, Func<T, TKey> keySelector)
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            var typedIndex = new SecondaryIndex<TKey>(keySelector);
            for (int i = 0; i < _orderedDefinitions.Count; i++)
            {
                typedIndex.IndexDefinition(_orderedDefinitions[i]);
            }

            _secondaryIndices[indexName] = typedIndex;
        }

        /// <summary>Attempts O(1) lookup using a named secondary index.</summary>
        public bool TryGetByIndex<TKey>(string indexName, TKey key, out T definition)
        {
            if (_secondaryIndices.TryGetValue(indexName, out ISecondaryIndex rawIndex) &&
                rawIndex is SecondaryIndex<TKey> typedIndex)
            {
                return typedIndex.TryGet(key, out definition);
            }

            definition = null;
            return false;
        }

        #endregion

        #region Internal Helpers

        private void RegisterAliasesForDefinition(ResourceId id, T definition, string explicitAlias)
        {
            if (!string.IsNullOrWhiteSpace(explicitAlias))
            {
                _legacyAliases[explicitAlias] = id;
            }

            if (!string.IsNullOrWhiteSpace(definition.Id))
            {
                _legacyAliases[definition.Id.Trim()] = id;
            }

            if (definition is UnityEngine.Object unityObj && !string.IsNullOrWhiteSpace(unityObj.name))
            {
                _legacyAliases[unityObj.name.Trim()] = id;
            }
        }

        private void ReplaceOrderedDefinition(T oldDef, T newDef)
        {
            int idx = _orderedDefinitions.IndexOf(oldDef);
            if (idx >= 0)
            {
                _orderedDefinitions[idx] = newDef;
            }
            else
            {
                _orderedDefinitions.Add(newDef);
            }
        }

        private void UpdateSecondaryIndices(T definition)
        {
            foreach (var index in _secondaryIndices.Values)
            {
                index.IndexDefinition(definition);
            }
        }

        private interface ISecondaryIndex
        {
            void IndexDefinition(T definition);
            void Clear();
        }

        private sealed class SecondaryIndex<TKey> : ISecondaryIndex
        {
            private readonly Func<T, TKey> _keySelector;
            private readonly Dictionary<TKey, T> _map = new();

            public SecondaryIndex(Func<T, TKey> keySelector)
            {
                _keySelector = keySelector;
            }

            public void IndexDefinition(T definition)
            {
                TKey key = _keySelector(definition);
                if (key != null)
                {
                    _map[key] = definition;
                }
            }

            public bool TryGet(TKey key, out T definition)
            {
                if (key != null)
                {
                    return _map.TryGetValue(key, out definition);
                }

                definition = null;
                return false;
            }

            public void Clear()
            {
                _map.Clear();
            }
        }

        #endregion
    }
}
