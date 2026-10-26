#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR && REMEMBERME_ADDRESSABLES_PRESENT
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Resolves Unity asset references saved as strings. Prefer exact Addressables
    /// addresses and Resources paths, while keeping old name-only saves loadable.
    /// </summary>
    public static class SavedAssetReferenceResolver
    {
        private static readonly string[] EmptyFolders = { string.Empty };

        private static readonly string[] TextureFolders =
        {
            "", "Textures", "Texture", "Tex", "textures", "texture", "tex",
            "Images", "Image", "images", "image",
            "Graphics", "Graphic", "graphics", "graphic",
            "Sprites", "Sprite", "sprites", "sprite"
        };

        private static readonly string[] MeshFolders =
        {
            "", "Meshes", "Mesh", "meshes", "mesh",
            "Models", "Model", "models", "model"
        };

        private static readonly string[] AudioExtensions =
        {
            ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".mod", ".it", ".s3m", ".xm"
        };

        private static readonly string[] TextureExtensions =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".tif", ".tiff", ".bmp", ".gif", ".asset"
        };

        private static readonly string[] MaterialExtensions =
        {
            ".mat", ".asset"
        };

        private static readonly string[] AnimationExtensions =
        {
            ".anim", ".fbx", ".asset"
        };

        private static readonly string[] MeshExtensions =
        {
            ".asset", ".fbx", ".obj"
        };

        private static readonly string[] AudioMixerExtensions =
        {
            ".mixer"
        };

        private static readonly string[] PhysicMaterialExtensions =
        {
            ".physicMaterial", ".physicsMaterial2D", ".asset"
        };

        private static readonly string[] PrefabExtensions =
        {
            ".prefab"
        };

        private static readonly string[] DefaultAssetExtensions =
        {
            ".asset"
        };

        private static readonly Dictionary<string, UnityEngine.Object> LoadCache = new();
        private static readonly Dictionary<int, string> RuntimeObjectKeys = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForFastEnterPlayMode()
        {
            LoadCache.Clear();
            RuntimeObjectKeys.Clear();
        }

        public static string GetSaveKey(UnityEngine.Object asset)
        {
            if (!asset)
            {
                return string.Empty;
            }

            if (RuntimeObjectKeys.TryGetValue(UnityObjectHelper.GetUniqueId(asset), out string runtimeKey) &&
                !string.IsNullOrEmpty(runtimeKey))
            {
                return runtimeKey;
            }

#if UNITY_EDITOR
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                bool isSubAsset = AssetDatabase.IsSubAsset(asset);
                if (!isSubAsset)
                {
#if REMEMBERME_ADDRESSABLES_PRESENT
                    string addressableAddress = GetAddressableAddress(assetPath);
                    if (!string.IsNullOrEmpty(addressableAddress))
                    {
                        return addressableAddress;
                    }
#endif
                    string resourcePath = GetResourcePath(assetPath);
                    if (!string.IsNullOrEmpty(resourcePath))
                    {
                        return resourcePath;
                    }
                }

                return asset.name;
            }
#endif

            return StripUnityRuntimeSuffix(asset.name);
        }

        public static T Load<T>(string savedKey, string context = null, LogLevel missingLogLevel = LogLevel.Warning)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(savedKey))
            {
                return null;
            }

            string cacheKey = $"{typeof(T).FullName}|{savedKey}";
            if (LoadCache.TryGetValue(cacheKey, out UnityEngine.Object cached))
            {
                return cached as T;
            }

            List<string> attemptedKeys = new();
            foreach (string key in GetLookupKeys<T>(savedKey))
            {
                attemptedKeys.Add(key);
                T asset = AssetProvider.Load<T>(key, false);
                if (asset != null)
                {
                    LoadCache[cacheKey] = asset;
                    RegisterRuntimeKey(asset, key);
                    return asset;
                }
            }

#if UNITY_EDITOR
            T editorAsset = FindEditorAsset<T>(savedKey);
            if (editorAsset != null)
            {
                LoadCache[cacheKey] = editorAsset;
                RegisterRuntimeKey(editorAsset, savedKey);
                return editorAsset;
            }
#endif

            if (missingLogLevel != LogLevel.Off)
            {
                string label = string.IsNullOrEmpty(context) ? typeof(T).Name : $"{context} {typeof(T).Name}";
                Logger.Log($"SavedAssetReferenceResolver: Could not load {label} '{savedKey}'. Tried {attemptedKeys.Count} Addressables/Resources keys, including {GetPreview(attemptedKeys)}. The asset must be Addressable, under Resources, or saved with an exact loadable key.", missingLogLevel);
            }

            return null;
        }

        public static void RegisterRuntimeKey(UnityEngine.Object asset, string key)
        {
            if (!asset || string.IsNullOrEmpty(key))
            {
                return;
            }

            RuntimeObjectKeys[UnityObjectHelper.GetUniqueId(asset)] = key;
        }

        public static IEnumerable<string> GetLookupKeys<T>(string savedKey)
            where T : UnityEngine.Object
        {
            Type type = typeof(T);
            string[] folders = GetFolders(type);
            string[] extensions = GetExtensions(type);
            HashSet<string> keys = new(StringComparer.Ordinal);

            foreach (string candidate in GetNameCandidates(savedKey))
            {
                AddLookupKey(keys, candidate, extensions);

                if (ContainsPathSeparator(candidate))
                {
                    continue;
                }

                foreach (string folder in folders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }

                    AddLookupKey(keys, $"{folder}/{candidate}", extensions);
                }
            }

            foreach (string key in keys)
            {
                yield return key;
            }
        }

        public static string StripUnityRuntimeSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            return name
                .Replace(" (Instance)", string.Empty)
                .Replace("(Instance)", string.Empty)
                .Replace(" (Clone)", string.Empty)
                .Replace("(Clone)", string.Empty)
                .Trim();
        }

        private static IEnumerable<string> GetNameCandidates(string savedKey)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            AddNameCandidate(names, savedKey);
            AddNameCandidate(names, StripUnityRuntimeSuffix(savedKey));

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(savedKey);
            AddNameCandidate(names, fileNameWithoutExtension);
            AddNameCandidate(names, StripUnityRuntimeSuffix(fileNameWithoutExtension));

            return names;
        }

        private static void AddNameCandidate(HashSet<string> names, string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        private static void AddLookupKey(HashSet<string> keys, string key, string[] extensions)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            keys.Add(key);

            if (Path.HasExtension(key))
            {
                return;
            }

            foreach (string extension in extensions)
            {
                keys.Add($"{key}{extension}");
            }
        }

        private static bool ContainsPathSeparator(string key)
        {
            return key.IndexOf('/') >= 0 || key.IndexOf('\\') >= 0;
        }

        private static string[] GetFolders(Type type)
        {
            if (typeof(Material).IsAssignableFrom(type))
            {
                return ResourceLoader.MaterialSubfolders;
            }

            if (typeof(Texture).IsAssignableFrom(type) || type == typeof(Sprite))
            {
                return TextureFolders;
            }

            if (typeof(AudioClip).IsAssignableFrom(type))
            {
                return ResourceLoader.AudioSubfolders;
            }

            if (typeof(AnimationClip).IsAssignableFrom(type))
            {
                return ResourceLoader.AnimationSubfolders;
            }

            if (typeof(Mesh).IsAssignableFrom(type))
            {
                return MeshFolders;
            }

            return EmptyFolders;
        }

        private static string[] GetExtensions(Type type)
        {
            if (typeof(Material).IsAssignableFrom(type))
            {
                return MaterialExtensions;
            }

            if (typeof(AudioClip).IsAssignableFrom(type))
            {
                return AudioExtensions;
            }

            if (typeof(AnimationClip).IsAssignableFrom(type))
            {
                return AnimationExtensions;
            }

            if (typeof(Texture).IsAssignableFrom(type) || type == typeof(Sprite))
            {
                return TextureExtensions;
            }

            if (typeof(Mesh).IsAssignableFrom(type))
            {
                return MeshExtensions;
            }

            if (typeof(UnityEngine.Audio.AudioMixer).IsAssignableFrom(type))
            {
                return AudioMixerExtensions;
            }

#if UNITY_6000_0_OR_NEWER
            if (typeof(PhysicsMaterial).IsAssignableFrom(type) || typeof(PhysicsMaterial2D).IsAssignableFrom(type))
#else
            if (typeof(PhysicMaterial).IsAssignableFrom(type) || typeof(PhysicsMaterial2D).IsAssignableFrom(type))
#endif
            {
                return PhysicMaterialExtensions;
            }

            if (typeof(GameObject).IsAssignableFrom(type))
            {
                return PrefabExtensions;
            }

            return DefaultAssetExtensions;
        }

        private static string GetPreview(List<string> attemptedKeys)
        {
            const int maxPreviewKeys = 12;
            int previewCount = Math.Min(attemptedKeys.Count, maxPreviewKeys);
            if (previewCount == 0)
            {
                return "none";
            }

            string preview = string.Join(", ", attemptedKeys.GetRange(0, previewCount));
            if (attemptedKeys.Count > maxPreviewKeys)
            {
                preview += $", and {attemptedKeys.Count - maxPreviewKeys} more";
            }

            return preview;
        }

#if UNITY_EDITOR
        private static string GetResourcePath(string assetPath)
        {
            const string resourcesSegment = "/Resources/";
            int resourcesIndex = assetPath.IndexOf(resourcesSegment, StringComparison.Ordinal);
            if (resourcesIndex < 0)
            {
                return null;
            }

            string resourcePath = assetPath.Substring(resourcesIndex + resourcesSegment.Length);
            return Path.ChangeExtension(resourcePath, null);
        }

        private static T FindEditorAsset<T>(string savedKey)
            where T : UnityEngine.Object
        {
            HashSet<string> candidates = new(GetNameCandidates(savedKey), StringComparer.Ordinal);
            string filter = $"t:{typeof(T).Name}";

            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (candidates.Contains(asset.name) || candidates.Contains(fileName))
                {
                    return asset;
                }
            }

            return null;
        }
#endif

#if UNITY_EDITOR && REMEMBERME_ADDRESSABLES_PRESENT
        private static string GetAddressableAddress(string assetPath)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            return string.IsNullOrEmpty(entry?.address) ? null : entry.address;
        }
#endif
    }
}
#endif
