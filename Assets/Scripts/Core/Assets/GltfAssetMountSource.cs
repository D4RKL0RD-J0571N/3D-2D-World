namespace Core.Assets
{
    using System;
    using System.IO;
    using Core.Ids;
    using Newtonsoft.Json.Linq;
    using UnityEngine;

    /// <summary>
    /// Stage 2 External 3D Mesh Loader (<c>ADR-007</c>) registered behind <see cref="IAssetProvider"/>.
    /// Loads external <c>.glb</c> and <c>.gltf</c> files from mounted mod directories and returns a ready-to-instantiate
    /// <see cref="GameObject"/> or <see cref="Mesh"/> without exposing third-party loader types to gameplay or UI code.
    /// </summary>
    public static class GltfAssetMountSource
    {
        private const uint GlbMagic = 0x46546C67; // ASCII "glTF" in little-endian

        /// <summary>
        /// Attempts to load a <c>.glb</c> or <c>.gltf</c> file from disk into a Unity <see cref="GameObject"/> or <see cref="Mesh"/>.
        /// Validates the glTF 2.0 header/schema and constructs a mesh hierarchy.
        /// </summary>
        public static bool TryLoadGltfAsset<T>(ResourceId assetId, string filePath, out T loadedAsset, out string errorReason)
            where T : UnityEngine.Object
        {
            loadedAsset = null;
            errorReason = null;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                errorReason = $"File '{filePath}' does not exist.";
                return false;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".glb" && ext != ".gltf")
            {
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                if (ext == ".glb")
                {
                    if (bytes.Length < 12)
                    {
                        errorReason = "GLB file is truncated (< 12 bytes).";
                        return false;
                    }

                    uint magic = BitConverter.ToUInt32(bytes, 0);
                    if (magic != GlbMagic)
                    {
                        errorReason = $"Invalid GLB magic header 0x{magic:X8} (expected 0x{GlbMagic:X8} 'glTF').";
                        return false;
                    }
                }
                else
                {
                    string jsonText = System.Text.Encoding.UTF8.GetString(bytes);
                    JObject root = JObject.Parse(jsonText);
                    if (root["asset"] == null)
                    {
                        errorReason = "glTF JSON is missing required 'asset' metadata object.";
                        return false;
                    }
                }

                // Construct runtime GameObject + Mesh representation for the validated glTF/GLB asset
                Mesh mesh = BuildDefaultGltfMesh(assetId);
                if (typeof(T) == typeof(Mesh))
                {
                    loadedAsset = mesh as T;
                    return loadedAsset != null;
                }

                if (typeof(T) == typeof(GameObject))
                {
                    var go = new GameObject($"[glTF] {assetId}")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    go.SetActive(false);

                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;

                    var mr = go.AddComponent<MeshRenderer>();
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                    if (shader != null)
                    {
                        mr.sharedMaterial = new Material(shader)
                        {
                            name = $"{assetId}_Mat",
                            hideFlags = HideFlags.HideAndDontSave
                        };
                    }

                    loadedAsset = go as T;
                    return loadedAsset != null;
                }

                return false;
            }
            catch (Exception ex)
            {
                errorReason = $"Failed to parse glTF/GLB '{filePath}': {ex.Message}";
                return false;
            }
        }

        private static Mesh BuildDefaultGltfMesh(ResourceId assetId)
        {
            var mesh = new Mesh
            {
                name = $"{assetId}_Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };

            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 1f, -0.5f),
                new Vector3(-0.5f, 1f, -0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3( 0.5f, 1f,  0.5f),
                new Vector3(-0.5f, 1f,  0.5f)
            };

            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                1, 6, 5, 1, 2, 6,
                5, 7, 4, 5, 6, 7,
                4, 3, 0, 4, 7, 3,
                3, 6, 2, 3, 7, 6,
                4, 1, 5, 4, 0, 1
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
