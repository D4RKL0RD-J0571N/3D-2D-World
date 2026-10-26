#if MEMORYPACK && ARAWN_REMEMBERME
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    [CustomPropertyDrawer(typeof(Runtime.StyledHeaderAttribute))]
    internal class StyledHeaderDrawer : DecoratorDrawer
    {
#pragma warning disable UDR0001 // Editor-only GUI cache cleared on assembly reload and play-mode transitions.
        private static GUIStyle _style;
        private static Texture2D _bgDark;
        private static Texture2D _bgLight;
#pragma warning restore UDR0001

        private static readonly Color DarkBg   = new(0.20f, 0.20f, 0.24f, 1f);
        private static readonly Color LightBg  = new(0.76f, 0.76f, 0.80f, 1f);
        private static readonly Color DarkTxt  = new(0.88f, 0.88f, 0.94f, 1f);
        private static readonly Color LightTxt = new(0.12f, 0.12f, 0.12f, 1f);

        [InitializeOnLoadMethod]
        private static void InitializeFastEnterPlayModeCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ClearCache;
#pragma warning disable UDR0001 // Editor-only style callback is lifecycle-managed and clears generated textures before reload/play transitions.
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#pragma warning restore UDR0001
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                ClearCache();
            }
        }

        private static void ClearCache()
        {
            _style = null;
            DestroyTexture(ref _bgDark);
            DestroyTexture(ref _bgLight);
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture)
                Object.DestroyImmediate(texture);
            texture = null;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private static GUIStyle Style
        {
            get
            {
                if (_style != null && _style.normal.background == null)
                    _style = null;

                if (_style == null)
                {
                    bool dark = EditorGUIUtility.isProSkin;
                    if (dark  && (_bgDark  == null || !_bgDark))  _bgDark  = MakeTex(DarkBg);
                    if (!dark && (_bgLight == null || !_bgLight)) _bgLight = MakeTex(LightBg);

                    _style = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize    = 12,
                        fixedHeight = 24,
                        padding     = new RectOffset(8, 4, 4, 4),
                        alignment   = TextAnchor.MiddleLeft,
                    };
                    _style.normal.background = dark ? _bgDark : _bgLight;
                    _style.normal.textColor  = dark ? DarkTxt : LightTxt;
                }
                return _style;
            }
        }

        public override float GetHeight()
        {
            // 4px top spacing + 24px header bar + 4px bottom spacing
            return 32f;
        }

        public override void OnGUI(Rect position)
        {
            var attr = (Runtime.StyledHeaderAttribute)attribute;

            // Top spacing
            position.y      += 4f;
            position.height  = 24f;

            GUI.Label(position, attr.Header, Style);
        }
    }
}
#endif
#endif
