namespace Core.Content
{
    using System;
    using Core.Ids;

    /// <summary>
    /// Conflict resolution policy when a content pack or mod registers a definition whose <see cref="ResourceId"/>
    /// already exists in the target <see cref="ContentRegistry{T}"/>.
    /// </summary>
    public enum ContentRegistrationPolicy
    {
        /// <summary>Rejects registration if the <see cref="ResourceId"/> already exists.</summary>
        AddOnly = 0,

        /// <summary>Replaces the existing definition and records the new <c>LastModifiedByModId</c>.</summary>
        OverrideExisting = 1,

        /// <summary>Applies a field-level merge over the existing definition and records the new <c>LastModifiedByModId</c>.</summary>
        MergePatch = 2
    }

    /// <summary>
    /// Exception thrown when attempting to mutate a <see cref="ContentRegistry{T}"/> after <see cref="ContentRegistry{T}.Freeze"/> has been called.
    /// </summary>
    public sealed class RegistryFrozenException : InvalidOperationException
    {
        public RegistryFrozenException(string registryName, ResourceId attemptedId)
            : base($"Cannot register or modify '{attemptedId}' because registry '{registryName}' is frozen. Call UnfreezeForReload() before mutating.")
        {
        }
    }

    /// <summary>
    /// Base contract for all data-driven content definitions (Items, Actors, Blocks, Skills, Factions, Quests, Recipes, Placeables).
    /// Bridges canonical <see cref="ResourceId"/> identity with legacy <see cref="IIdentifiable"/> string IDs.
    /// </summary>
    public interface IContentDefinition : IIdentifiable
    {
        /// <summary>Canonical namespaced identifier (e.g., <c>nordic_rpg:item/iron_sword</c>).</summary>
        ResourceId ContentId { get; }

        /// <summary>Human-readable display name.</summary>
        string DisplayName { get; }

        /// <summary>Identifier of the content pack or mod that originally defined this record.</summary>
        string SourceModId { get; }
    }

    /// <summary>
    /// Provenance metadata wrapper stored for each registered <see cref="IContentDefinition"/> inside a <see cref="ContentRegistry{T}"/>.
    /// </summary>
    public sealed class ContentRegistryEntry<T> where T : class, IContentDefinition
    {
        public ResourceId ContentId { get; }
        public T Definition { get; internal set; }
        public string SourceModId { get; }
        public string LastModifiedByModId { get; internal set; }
        public int ModificationCount { get; internal set; }
        public string LegacyAlias { get; internal set; }

        public ContentRegistryEntry(
            ResourceId contentId,
            T definition,
            string sourceModId,
            string lastModifiedByModId,
            string legacyAlias)
        {
            ContentId = contentId;
            Definition = definition;
            SourceModId = string.IsNullOrWhiteSpace(sourceModId) ? ResourceId.DefaultNamespace : sourceModId;
            LastModifiedByModId = string.IsNullOrWhiteSpace(lastModifiedByModId) ? SourceModId : lastModifiedByModId;
            ModificationCount = 1;
            LegacyAlias = legacyAlias;
        }
    }
}
