namespace Core.Assets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Core.Diagnostics;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Concrete multi-source implementation of <see cref="IAssetProvider"/>.
    /// Resolves assets in priority order:
    /// 1. Mounted external directories (loose <c>.png</c>/<c>.jpg</c> textures and sprites, highest override priority)
    /// 2. Mounted <see cref="AssetBundle"/>s (in reverse mount order)
    /// 3. Explicitly registered built-in assets and <c>Resources.Load</c>
    /// 4. <see cref="FallbackAssetCatalog"/> non-null placeholder (while logging a structured diagnostic warning).
    /// </summary>
    public sealed class AssetProviderService : IAssetProvider
    {
        #region Fields

        private readonly Dictionary<ResourceId, UnityEngine.Object> _builtInAssets = new();
        private readonly Dictionary<string, List<AssetBundle>> _mountedBundles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _mountedDirectories = new(StringComparer.Ordinal);
        private readonly Dictionary<(ResourceId, Type), UnityEngine.Object> _externalCache = new();
        private readonly FallbackAssetCatalog _fallbacks = new();

        #endregion

        #region Properties

        public FallbackAssetCatalog Fallbacks => _fallbacks;

        #endregion

        #region IGameService Lifecycle

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _builtInAssets.Clear();
            _mountedBundles.Clear();
            _mountedDirectories.Clear();

            foreach (var kvp in _externalCache)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(kvp.Value);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(kvp.Value);
                    }
                }
            }
            _externalCache.Clear();
            _fallbacks.Dispose();
        }

        #endregion

        #region Registration & Mounting

        public void RegisterBuiltInAsset(ResourceId assetId, UnityEngine.Object asset)
        {
            if (!assetId.IsValid || asset == null)
            {
                return;
            }

            _builtInAssets[assetId] = asset;
        }

        public void MountAssetBundle(string namespaceId, AssetBundle bundle)
        {
            if (string.IsNullOrWhiteSpace(namespaceId) || bundle == null)
            {
                return;
            }

            string ns = namespaceId.Trim().ToLowerInvariant();
            if (!_mountedBundles.TryGetValue(ns, out List<AssetBundle> list))
            {
                list = new List<AssetBundle>();
                _mountedBundles[ns] = list;
            }

            if (!list.Contains(bundle))
            {
                list.Add(bundle);
            }
        }

        public void MountExternalDirectory(string namespaceId, string absoluteDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(namespaceId) || string.IsNullOrWhiteSpace(absoluteDirectoryPath))
            {
                return;
            }

            string ns = namespaceId.Trim().ToLowerInvariant();
            if (!_mountedDirectories.TryGetValue(ns, out List<string> dirs))
            {
                dirs = new List<string>();
                _mountedDirectories[ns] = dirs;
            }

            string normalized = Path.GetFullPath(absoluteDirectoryPath);
            if (!dirs.Contains(normalized))
            {
                dirs.Add(normalized);
            }
        }

        public void UnmountNamespace(string namespaceId, bool unloadAllLoadedObjects = false)
        {
            if (string.IsNullOrWhiteSpace(namespaceId)) return;
            string ns = namespaceId.Trim().ToLowerInvariant();

            if (_mountedBundles.TryGetValue(ns, out List<AssetBundle> bundles))
            {
                for (int i = bundles.Count - 1; i >= 0; i--)
                {
                    if (bundles[i] != null)
                    {
                        bundles[i].Unload(unloadAllLoadedObjects);
                    }
                }
                _mountedBundles.Remove(ns);
            }

            _mountedDirectories.Remove(ns);
        }

        #endregion

        #region Asset Resolution

        public bool HasAsset(ResourceId assetId)
        {
            if (!assetId.IsValid) return false;

            if (_builtInAssets.TryGetValue(assetId, out UnityEngine.Object builtIn) && builtIn != null)
            {
                return true;
            }

            if (TryResolveExternalFilePath(assetId, out _))
            {
                return true;
            }

            if (_mountedBundles.TryGetValue(assetId.Namespace, out List<AssetBundle> bundles))
            {
                for (int i = bundles.Count - 1; i >= 0; i--)
                {
                    AssetBundle bundle = bundles[i];
                    if (bundle != null && (bundle.Contains(assetId.Path) || bundle.Contains(Path.GetFileName(assetId.Path))))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public T LoadAsset<T>(ResourceId assetId) where T : UnityEngine.Object
        {
            if (!assetId.IsValid)
            {
                ReportMissingAsset(assetId, typeof(T), "Invalid (default) ResourceId requested.");
                return _fallbacks.GetFallback<T>();
            }

            // 1. Check external cache & loose mod directories first (highest override priority)
            if (_externalCache.TryGetValue((assetId, typeof(T)), out UnityEngine.Object cachedExternal) && cachedExternal is T typedExternal)
            {
                return typedExternal;
            }

            if (TryLoadFromExternalDirectory<T>(assetId, out T externalAsset))
            {
                return externalAsset;
            }

            // 2. Check mounted AssetBundles in reverse order
            if (_mountedBundles.TryGetValue(assetId.Namespace, out List<AssetBundle> bundles))
            {
                for (int i = bundles.Count - 1; i >= 0; i--)
                {
                    AssetBundle bundle = bundles[i];
                    if (bundle == null) continue;

                    T asset = bundle.LoadAsset<T>(assetId.Path) ?? bundle.LoadAsset<T>(Path.GetFileName(assetId.Path));
                    if (asset != null)
                    {
                        return asset;
                    }
                }
            }

            // 3. Check registered built-in assets
            if (_builtInAssets.TryGetValue(assetId, out UnityEngine.Object builtIn) && builtIn != null)
            {
                if (builtIn is T typedBuiltIn)
                {
                    return typedBuiltIn;
                }

                // Automatic Texture2D -> Sprite conversion if a Texture2D was registered
                if (typeof(T) == typeof(Sprite) && builtIn is Texture2D tex)
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    return sprite as T;
                }
            }

            // 4. Check Unity Resources folder by path
            T resourceAsset = Resources.Load<T>(assetId.Path);
            if (resourceAsset != null)
            {
                return resourceAsset;
            }

            // 5. Fallback placeholder + diagnostic report
            ReportMissingAsset(assetId, typeof(T), $"Asset '{assetId}' of type '{typeof(T).Name}' could not be found in any mounted source.");
            return _fallbacks.GetFallback<T>();
        }

        public Task<T> LoadAssetAsync<T>(ResourceId assetId) where T : UnityEngine.Object
        {
            T result = LoadAsset<T>(assetId);
            return Task.FromResult(result);
        }

        #endregion

        #region Private Helpers

        private bool TryResolveExternalFilePath(ResourceId assetId, out string resolvedFilePath)
        {
            resolvedFilePath = null;
            if (!_mountedDirectories.TryGetValue(assetId.Namespace, out List<string> dirs))
            {
                return false;
            }

            string relativePath = assetId.Path.Replace('/', Path.DirectorySeparatorChar);
            string[] candidateExtensions = { "", ".png", ".jpg", ".jpeg" };

            for (int i = dirs.Count - 1; i >= 0; i--)
            {
                string baseDir = dirs[i];
                for (int e = 0; e < candidateExtensions.Length; e++)
                {
                    string candidate = Path.Combine(baseDir, relativePath + candidateExtensions[e]);
                    if (File.Exists(candidate))
                    {
                        resolvedFilePath = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryLoadFromExternalDirectory<T>(ResourceId assetId, out T loadedAsset) where T : UnityEngine.Object
        {
            loadedAsset = null;
            if (!TryResolveExternalFilePath(assetId, out string filePath))
            {
                return false;
            }

            Type targetType = typeof(T);
            if (targetType == typeof(Texture2D) || targetType == typeof(Texture) || targetType == typeof(Sprite))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                    {
                        name = assetId.ToString()
                    };

                    if (tex.LoadImage(bytes))
                    {
                        _externalCache[(assetId, typeof(Texture2D))] = tex;
                        if (targetType == typeof(Sprite))
                        {
                            var sprite = Sprite.Create(
                                tex,
                                new Rect(0f, 0f, tex.width, tex.height),
                                new Vector2(0.5f, 0.5f),
                                100f);
                            sprite.name = assetId.ToString();
                            _externalCache[(assetId, typeof(Sprite))] = sprite;
                            loadedAsset = sprite as T;
                            return loadedAsset != null;
                        }

                        loadedAsset = tex as T;
                        return loadedAsset != null;
                    }
                }
                catch (Exception ex)
                {
                    ReportMissingAsset(assetId, typeof(T), $"Failed to load external image '{filePath}': {ex.Message}");
                }
            }

            return false;
        }

        private static void ReportMissingAsset(ResourceId assetId, Type requestedType, string reason)
        {
            if (ServiceLocator.TryGet<IDiagnosticService>(out var diag))
            {
                diag.RecordWarning(
                    context: "AssetProvider",
                    message: $"[AST-404] {reason} (Type: '{requestedType.Name}', Id: '{(assetId.IsValid ? assetId.ToString() : "<invalid>")}'). Using fallback placeholder.",
                    logToUnityConsole: false);
            }
        }

        #endregion
    }
}
