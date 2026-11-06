using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Profiling;

namespace proTools.proFolder
{
    [InitializeOnLoad]
    public static class proFolder
    {
        #region Constants
        private const string MARKER_LIBRARY_PATH = "Assets/proTools/proFolder/SO/MarkerLibrary.asset";
        private const string WHITE_FOLDER_ICON_PATH = "Assets/proTools/proFolder/UI/Marks/Base/d_Folder.png";
        internal const string SESSION_STATE_KEY = "proFolder_Reload";

        private const int GRADIENT_WIDTH = 128;
        private const int GRADIENT_HEIGHT = 1;
        private const float GRADIENT_START_ALPHA = 0.4f;
        private const float GRADIENT_END_ALPHA = 0.1f;
        #endregion

        #region Private Fields
        private static MarkerLibrary _markerLibrary;
        private static Texture2D _gradientTexture;
        private static Texture2D _whiteFolderIcon;
        private static Color _lastGradientColor = Color.clear;
        #endregion

        #region Static Constructor
        static proFolder()
        {
            Initialize();
        }

        private static void Initialize()
        {
            FolderDataManager.Load();
            LoadAssets();
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowGUI;
        }
        #endregion

        #region Asset Loading
        private static void LoadAssets()
        {
            LoadMarkerLibrary();
            LoadWhiteFolderIcon();
        }

        private static void LoadMarkerLibrary()
        {
            if (_markerLibrary == null)
            {
                _markerLibrary = AssetDatabase.LoadAssetAtPath<MarkerLibrary>(MARKER_LIBRARY_PATH);
                if (_markerLibrary == null)
                {
                    Debug.LogWarning($"proFolder: MarkerLibrary not found at {MARKER_LIBRARY_PATH}");
                }
            }
        }

        private static void LoadWhiteFolderIcon()
        {
            if (_whiteFolderIcon == null)
            {
                _whiteFolderIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(WHITE_FOLDER_ICON_PATH);
                if (_whiteFolderIcon == null)
                {
                    Debug.LogWarning($"proFolder: White folder icon not found at {WHITE_FOLDER_ICON_PATH}");
                }
            }
        }
        #endregion

        #region Gradient Texture Management
        private static void EnsureGradientTexture(Color baseColor)
        {
            if (_gradientTexture != null && baseColor.Equals(_lastGradientColor))
                return;

            DisposeGradientTexture();
            CreateGradientTexture(baseColor);
            _lastGradientColor = baseColor;
        }

        private static void CreateGradientTexture(Color baseColor)
        {
            _gradientTexture = new Texture2D(GRADIENT_WIDTH, GRADIENT_HEIGHT, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[GRADIENT_WIDTH * GRADIENT_HEIGHT];

            for (int x = 0; x < GRADIENT_WIDTH; x++)
            {
                float t = x / (float)(GRADIENT_WIDTH - 1);
                float alpha = Mathf.Lerp(GRADIENT_START_ALPHA, GRADIENT_END_ALPHA, t);
                pixels[x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }

            _gradientTexture.SetPixels(pixels);
            _gradientTexture.Apply();
        }

        private static void DisposeGradientTexture()
        {
            if (_gradientTexture != null)
            {
                Object.DestroyImmediate(_gradientTexture);
                _gradientTexture = null;
            }
        }
        #endregion

        #region GUI Rendering
        private static void OnProjectWindowGUI(string guid, Rect rect)
        {
            if (!proFolderSettings.EnableOption) return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) return;

            bool isColumnView = rect.width > rect.height;

            RenderFolderColor(path, rect, isColumnView);
            RenderFolderMarker(path, rect, isColumnView);
            HandleMouseInput(path, rect);
        }

        private static void RenderFolderColor(string path, Rect rect, bool isColumnView)
        {
            if (!FolderDataManager.folderColors.TryGetValue(path, out var color))
                return;

            if (isColumnView)
            {
                RenderColumnViewColor(color, rect);
            }
            else
            {
                RenderIconViewColor(color, rect);
            }
        }

        private static void RenderColumnViewColor(Color color, Rect rect)
        {
            EnsureGradientTexture(color);
            if (_gradientTexture == null) return;

            Rect fullRect = new Rect(rect.x - 160, rect.y, rect.width + 160, rect.height);
            GUI.DrawTexture(fullRect, _gradientTexture, ScaleMode.StretchToFill, true);
        }

        private static void RenderIconViewColor(Color color, Rect rect)
        {
            if (_whiteFolderIcon == null) return;

            Rect iconRect = new Rect(rect.x, rect.y, rect.width, rect.width);

            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(iconRect, _whiteFolderIcon, ScaleMode.ScaleToFit);
            GUI.color = oldColor;
        }

        private static void RenderFolderMarker(string path, Rect rect, bool isColumnView)
        {
            if (!FolderDataManager.folderMarkers.TryGetValue(path, out var index) ||
                _markerLibrary == null ||
                index <= 0 ||
                index >= _markerLibrary.entries.Count)
                return;

            var icon = _markerLibrary.entries[index].icon;
            if (icon == null) return;

            Rect iconRect = CalculateMarkerIconRect(rect, isColumnView);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
        }

        private static Rect CalculateMarkerIconRect(Rect rect, bool isColumnView)
        {
            float size = isColumnView ? rect.height * 0.5f : rect.height * 0.35f;
            float offsetX = isColumnView ?
                rect.x + rect.height - size + rect.height * 0.1f :
                rect.xMax - size;
            float offsetY = isColumnView ?
                rect.y + rect.height - size :
                rect.y + rect.height * 0.65f - size * 0.5f;

            return new Rect(offsetX, offsetY, size, size);
        }
        #endregion

        #region Input Handling
        private static void HandleMouseInput(string path, Rect rect)
        {
            if (Event.current.type != EventType.MouseDown ||
                !rect.Contains(Event.current.mousePosition) ||
                Event.current.button != 0)
                return;

            if (!IsHotkeyPressed()) return;

            Vector2 mouseScreenPos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            OpenFolderEditor(path, mouseScreenPos);
            Event.current.Use();
        }

        private static bool IsHotkeyPressed()
        {
            return proFolderSettings.HotkeyOption ? Event.current.shift : Event.current.alt;
        }

        private static void OpenFolderEditor(string path, Vector2 mouseScreenPos)
        {
            proFolderEditor.OnColorSelected = selectedColor =>
            {
                HandleColorSelection(path, selectedColor);
            };

            proFolderEditor.OnMarkSelected = selectedMarker =>
            {
                HandleMarkerSelection(path, selectedMarker);
            };

            proFolderEditor.Open(mouseScreenPos);
        }

        private static void HandleColorSelection(string path, Color selectedColor)
        {
            if (selectedColor.a == 0f || selectedColor == Color.clear)
            {
                FolderDataManager.folderColors.Remove(path);
            }
            else
            {
                FolderDataManager.folderColors[path] = selectedColor;
            }

            SaveAndRepaint();
        }

        private static void HandleMarkerSelection(string path, int selectedMarker)
        {
            if (selectedMarker == 0)
            {
                FolderDataManager.folderMarkers.Remove(path);
            }
            else
            {
                FolderDataManager.folderMarkers[path] = selectedMarker;
            }

            SaveAndRepaint();
        }

        private static void SaveAndRepaint()
        {
            FolderDataManager.Save();
            EditorApplication.RepaintProjectWindow();
        }
        #endregion
    }

    [InitializeOnLoad]
    public static class proFolderImportPrompt
    {
        static proFolderImportPrompt()
        {
            if (!SessionState.GetBool(proFolder.SESSION_STATE_KEY, false))
            {
                SessionState.SetBool(proFolder.SESSION_STATE_KEY, true);
                EditorApplication.delayCall += () =>
                {
                    EditorUtility.RequestScriptReload();
                };
            }
        }
    }
}