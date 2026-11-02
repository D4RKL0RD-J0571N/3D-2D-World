namespace Core.Content
{
    using Core.Ids;

    /// <summary>
    /// Central engine service managing all typed <see cref="ContentRegistry{T}"/> instances.
    /// Provides a single resolution point for base content packs, ScriptableObject bridges, and external mods.
    /// </summary>
    public interface IContentService : IGameService
    {
        /// <summary>Gets or creates the <see cref="ContentRegistry{T}"/> for definition type <typeparamref name="T"/>.</summary>
        ContentRegistry<T> GetRegistry<T>() where T : class, IContentDefinition;

        /// <summary>Attempts to resolve a definition of type <typeparamref name="T"/> by its canonical <see cref="ResourceId"/>.</summary>
        bool TryResolve<T>(ResourceId id, out T definition) where T : class, IContentDefinition;

        /// <summary>Attempts to resolve a definition of type <typeparamref name="T"/> by a legacy string alias or canonical ID.</summary>
        bool TryResolveByLegacyAlias<T>(string legacyOrCanonicalId, out T definition) where T : class, IContentDefinition;

        /// <summary>Freezes all active registries to prevent accidental mutations during gameplay.</summary>
        void FreezeAll();

        /// <summary>Unfreezes all active registries prior to mod reloading or content pack mounting.</summary>
        void UnfreezeAll();
    }
}
