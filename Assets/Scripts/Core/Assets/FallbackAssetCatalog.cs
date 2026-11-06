namespace Core.Assets
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Lazily constructs and caches safe, non-null placeholder assets (<see cref="Texture2D"/>,
    /// <see cref="Sprite"/>, <see cref="Material"/>, <see cref="GameObject"/>, <see cref="AudioClip"/>)
    /// returned by <see cref="AssetProviderService"/> when a requested asset is missing or corrupt.
    /// Prevents <see cref="NullReferenceException"/> cascades in modded content.
    /// </summary>
    public sealed class FallbackAssetCatalog : IDisposable
    {
        #region Fields

        private Texture2D _fallbackTexture;
        private Sprite _fallbackSprite;
        private Material _fallbackMaterial;
        private GameObject _fallbackPrefab;
        private AudioClip _fallbackAudioClip;

        #endregion

        #region Properties

        /// <summary>Returns a 4x4 magenta/black checkerboard texture.</summary>
        public Texture2D FallbackTexture
        {
            get
            {
                if (_fallbackTexture == null)
                {
                    _fallbackTexture = CreateCheckerboardTexture();
                }
                return _fallbackTexture;
            }
        }

        /// <summary>Returns a sprite backed by <see cref="FallbackTexture"/>.</summary>
        public Sprite FallbackSprite
        {
            get
            {
                if (_fallbackSprite == null)
                {
                    Texture2D tex = FallbackTexture;
                    _fallbackSprite = Sprite.Create(
                        tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f),
                        16f);
                    _fallbackSprite.name = "MissingSpritePlaceholder";
                }
                return _fallbackSprite;
            }
        }

        /// <summary>Returns a valid URP/Unlit or Sprites/Default magenta material.</summary>
        public Material FallbackMaterial
        {
            get
            {
                if (_fallbackMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                 ?? Shader.Find("Unlit/Color")
                                 ?? Shader.Find("Sprites/Default");
                    _fallbackMaterial = new Material(shader)
                    {
                        name = "MissingMaterialPlaceholder",
                        color = new Color(1f, 0f, 1f, 1f)
                    };
                }
                return _fallbackMaterial;
            }
        }

        /// <summary>Returns a hidden placeholder cube GameObject for missing prefabs/meshes.</summary>
        public GameObject FallbackPrefab
        {
            get
            {
                if (_fallbackPrefab == null)
                {
                    _fallbackPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _fallbackPrefab.name = "MissingMeshPlaceholder";
                    _fallbackPrefab.hideFlags = HideFlags.HideAndDontSave;
                    _fallbackPrefab.SetActive(false);

                    var renderer = _fallbackPrefab.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = FallbackMaterial;
                    }
                }
                return _fallbackPrefab;
            }
        }

        /// <summary>Returns a silent 0.1-second AudioClip placeholder.</summary>
        public AudioClip FallbackAudioClip
        {
            get
            {
                if (_fallbackAudioClip == null)
                {
                    _fallbackAudioClip = AudioClip.Create(
                        "MissingAudioPlaceholder",
                        lengthSamples: 4410,
                        channels: 1,
                        frequency: 44100,
                        stream: false);
                }
                return _fallbackAudioClip;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Resolves the typed fallback placeholder for <typeparamref name="T"/>, or <c>null</c> if <typeparamref name="T"/>
        /// is not a visual/audio UnityEngine.Object with a standard placeholder.
        /// </summary>
        public T GetFallback<T>() where T : UnityEngine.Object
        {
            Type requestedType = typeof(T);

            if (requestedType == typeof(Sprite))
            {
                return FallbackSprite as T;
            }

            if (requestedType == typeof(Texture2D) || requestedType == typeof(Texture))
            {
                return FallbackTexture as T;
            }

            if (requestedType == typeof(Material))
            {
                return FallbackMaterial as T;
            }

            if (requestedType == typeof(GameObject))
            {
                return FallbackPrefab as T;
            }

            if (requestedType == typeof(AudioClip))
            {
                return FallbackAudioClip as T;
            }

            return null;
        }

        /// <summary>Destroys any procedurally generated placeholder objects.</summary>
        public void Dispose()
        {
            DestroySafe(ref _fallbackSprite);
            DestroySafe(ref _fallbackTexture);
            DestroySafe(ref _fallbackMaterial);
            DestroySafe(ref _fallbackPrefab);
            DestroySafe(ref _fallbackAudioClip);
        }

        #endregion

        #region Private Helpers

        private static Texture2D CreateCheckerboardTexture()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, mipChain: false)
            {
                name = "MissingTexturePlaceholder",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color magenta = new Color(1f, 0f, 1f, 1f);
            Color black = new Color(0.1f, 0.1f, 0.1f, 1f);

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    tex.SetPixel(x, y, ((x + y) % 2 == 0) ? magenta : black);
                }
            }

            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return tex;
        }

        private static void DestroySafe<T>(ref T obj) where T : UnityEngine.Object
        {
            if (obj == null) return;

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }

            obj = null;
        }

        #endregion
    }
}
