namespace Core.Assets
{
    using System.Threading.Tasks;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Engine Core abstraction for loading built-in Unity assets, mounted <see cref="AssetBundle"/>s,
    /// and external loose mod files (<c>.png</c>, <c>.jpg</c>) by <see cref="ResourceId"/>.
    /// Guarantees non-null fallback placeholders for missing visual/audio assets.
    /// </summary>
    public interface IAssetProvider : IGameService
    {
        /// <summary>Catalog of non-null placeholder assets returned when an asset cannot be found.</summary>
        FallbackAssetCatalog Fallbacks { get; }

        /// <summary>Registers an in-memory or built-in Unity asset under a canonical <see cref="ResourceId"/>.</summary>
        void RegisterBuiltInAsset(ResourceId assetId, Object asset);

        /// <summary>Mounts a loaded <see cref="AssetBundle"/> under the given namespace (e.g. <c>nordic_rpg</c>).</summary>
        void MountAssetBundle(string namespaceId, AssetBundle bundle);

        /// <summary>Mounts a local filesystem directory containing loose mod assets (<c>.png</c>, <c>.jpg</c>) under the given namespace.</summary>
        void MountExternalDirectory(string namespaceId, string absoluteDirectoryPath);

        /// <summary>Returns <c>true</c> if <paramref name="assetId"/> is registered in built-in assets, a mounted bundle, or an external directory.</summary>
        bool HasAsset(ResourceId assetId);

        /// <summary>
        /// Synchronously resolves <paramref name="assetId"/> as <typeparamref name="T"/>.
        /// If the asset cannot be found, logs a warning to <see cref="Core.Diagnostics.IDiagnosticService"/>
        /// and returns a non-null placeholder from <see cref="Fallbacks"/> when available.
        /// </summary>
        T LoadAsset<T>(ResourceId assetId) where T : Object;

        /// <summary>
        /// Asynchronously resolves <paramref name="assetId"/> as <typeparamref name="T"/>.
        /// Falls back to <see cref="Fallbacks"/> if missing.
        /// </summary>
        Task<T> LoadAssetAsync<T>(ResourceId assetId) where T : Object;

        /// <summary>Unmounts all AssetBundles and external directories registered for <paramref name="namespaceId"/>.</summary>
        void UnmountNamespace(string namespaceId, bool unloadAllLoadedObjects = false);
    }
}
