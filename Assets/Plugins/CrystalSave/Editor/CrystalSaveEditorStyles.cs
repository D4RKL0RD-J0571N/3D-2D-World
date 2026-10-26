#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// Shared visual styles for Crystal Save custom inspectors.
    /// Provides consistent section headers, grouped boxes and separators
    /// for a clean, modern inspector layout.
    /// </summary>
    internal static class CrystalSaveEditorStyles
    {
#pragma warning disable UDR0001 // Editor-only GUI cache cleared on assembly reload and play-mode transitions.
        static GUIStyle _sectionHeader;
        static Texture2D _headerBgDark;
        static Texture2D _headerBgLight;
#pragma warning restore UDR0001

        static readonly Color DarkHeaderBg    = new(0.20f, 0.20f, 0.24f, 1f);
        static readonly Color LightHeaderBg   = new(0.76f, 0.76f, 0.80f, 1f);
        static readonly Color DarkHeaderText  = new(0.88f, 0.88f, 0.94f, 1f);
        static readonly Color LightHeaderText = new(0.12f, 0.12f, 0.12f, 1f);
        static readonly Color DarkSep         = new(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color LightSep        = new(0.66f, 0.66f, 0.66f, 1f);

        [InitializeOnLoadMethod]
        static void InitializeFastEnterPlayModeCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClearCache;
#pragma warning disable UDR0001 // Editor-only style callback is lifecycle-managed and clears generated textures before reload/play transitions.
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#pragma warning restore UDR0001
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                ClearCache();
            }
        }

        static void ClearCache()
        {
            _sectionHeader = null;
            DestroyTexture(ref _headerBgDark);
            DestroyTexture(ref _headerBgLight);
        }

        static void DestroyTexture(ref Texture2D texture)
        {
            if (texture)
                Object.DestroyImmediate(texture);
            texture = null;
        }

        static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static GUIStyle HeaderStyle
        {
            get
            {
                // Rebuild when texture is lost (domain reload).
                if (_sectionHeader != null && _sectionHeader.normal.background == null)
                    _sectionHeader = null;

                if (_sectionHeader == null)
                {
                    bool dark = EditorGUIUtility.isProSkin;
                    if (dark  && (_headerBgDark  == null || !_headerBgDark))  _headerBgDark  = MakeTex(DarkHeaderBg);
                    if (!dark && (_headerBgLight == null || !_headerBgLight)) _headerBgLight = MakeTex(LightHeaderBg);

                    _sectionHeader = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize    = 12,
                        fixedHeight = 24,
                        padding     = new RectOffset(8, 4, 4, 4),
                        margin      = new RectOffset(0, 0, 0, 4),
                        alignment   = TextAnchor.MiddleLeft,
                    };
                    _sectionHeader.normal.background = dark ? _headerBgDark : _headerBgLight;
                    _sectionHeader.normal.textColor  = dark ? DarkHeaderText : LightHeaderText;
                }
                return _sectionHeader;
            }
        }

        /// <summary>Draws a colored header bar with the given title.</summary>
        public static void DrawSectionHeader(string title)
        {
            GUILayout.Label(title, HeaderStyle, GUILayout.ExpandWidth(true));
        }

        /// <summary>Opens a bordered card and draws a header bar inside it.</summary>
        public static void BeginSection(string title)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSectionHeader(title);
        }

        /// <summary>Opens a bordered card with no header.</summary>
        public static void BeginSection()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        }

        /// <summary>Closes a section opened by <see cref="BeginSection(string)"/>.</summary>
        public static void EndSection()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();
        }

        /// <summary>Draws a thin horizontal separator line.</summary>
        public static void Separator()
        {
            EditorGUILayout.Space(3);
            var r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? DarkSep : LightSep);
            EditorGUILayout.Space(3);
        }
    }
}
#endif
#endif
