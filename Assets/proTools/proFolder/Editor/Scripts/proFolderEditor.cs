using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace proTools.proFolder
{
    public class proFolderEditor : EditorWindow
    {
        #region Constants
        private const string MARKER_LIBRARY_PATH = "Assets/proTools/proFolder/SO/MarkerLibrary.asset";
        private const int ICON_SIZE = 32;
        private const int MAX_COLUMNS = 11;
        private const int ROW_HEIGHT = 34;
        private const int BASE_HEIGHT = 46;
        private const int WINDOW_WIDTH = 388;
        private const int SPACING_SMALL = 8;
        private const int SPACING_LARGE = 16;
        #endregion

        #region Static Fields
        private static proFolderEditor _instance;
        private static List<Color> _colors;
        private static Texture2D[] _colorIcons;
        private static MarkerLibrary _markerLibrary;
        #endregion

        #region Events
        public static System.Action<Color> OnColorSelected;
        public static System.Action<int> OnMarkSelected;
        #endregion

        #region Public Methods
        public static void Open(Vector2 position)
        {
            CloseExistingInstance();
            InitializeData();

            Vector2 windowSize = CalculateWindowSize();
            CreateAndShowWindow(position, windowSize);
        }
        #endregion

        #region Window Management
        private static void CloseExistingInstance()
        {
            if (_instance != null)
            {
                _instance.Close();
                _instance = null;
            }
        }

        private static void CreateAndShowWindow(Vector2 position, Vector2 size)
        {
            _instance = CreateInstance<proFolderEditor>();
            _instance.position = new Rect(position.x, position.y, size.x, size.y);
            _instance.ShowPopup();
        }

        private static Vector2 CalculateWindowSize()
        {
            int colorRowCount = Mathf.CeilToInt((_colors.Count + 1) / (float)MAX_COLUMNS);
            int markerRowCount = CalculateMarkerRowCount();
            int totalHeight = (colorRowCount + markerRowCount) * ROW_HEIGHT + BASE_HEIGHT;

            return new Vector2(WINDOW_WIDTH, totalHeight);
        }

        private static int CalculateMarkerRowCount()
        {
            if (_markerLibrary == null) return 0;

            var groups = _markerLibrary.entries.GroupBy(entry => entry.groupName);
            int totalMarkerRows = 0;

            foreach (var group in groups)
            {
                int groupIconCount = group.Count();
                int iconRows = Mathf.CeilToInt(groupIconCount / (float)MAX_COLUMNS);
                totalMarkerRows += iconRows;
            }

            return totalMarkerRows;
        }
        #endregion

        #region Data Initialization
        private static void InitializeData()
        {
            LoadColors();
            GenerateColorIcons();
            LoadMarkerLibrary();
        }

        private static void LoadColors()
        {
            _colors = proFolderSettings.ColorPresetList;
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
        #endregion

        #region Color Icon Generation
        private static void GenerateColorIcons()
        {
            DisposeColorIcons();

            if (_colors == null || _colors.Count == 0) return;

            _colorIcons = new Texture2D[_colors.Count];

            for (int i = 0; i < _colors.Count; i++)
            {
                _colorIcons[i] = CreateColorTexture(_colors[i]);
            }
        }

        private static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[ICON_SIZE * ICON_SIZE];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void DisposeColorIcons()
        {
            if (_colorIcons == null) return;

            foreach (var icon in _colorIcons)
            {
                if (icon != null)
                {
                    DestroyImmediate(icon);
                }
            }
            _colorIcons = null;
        }
        #endregion

        #region Unity Callbacks
        private void OnDisable()
        {
            _instance = null;
        }

        private void OnLostFocus()
        {
            CloseWindow();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CloseWindow();
            }
        }

        private void CloseWindow()
        {
            Close();
            _instance = null;
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawColorGrid();
            DrawSpacer();
            DrawMarkerGrid();
        }
        #endregion

        #region GUI Drawing
        private void DrawHeader()
        {
            GUILayout.Label("proFolder", EditorStyles.boldLabel);
            GUILayout.Space(SPACING_SMALL);
        }

        private void DrawSpacer()
        {
            GUILayout.Space(SPACING_LARGE);
        }

        private void DrawColorGrid()
        {
            if (_colorIcons == null || _markerLibrary == null) return;

            int totalItems = _colorIcons.Length + 1;

            for (int i = 0; i < totalItems; i++)
            {
                if (i % MAX_COLUMNS == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                DrawColorButton(i);

                if ((i % MAX_COLUMNS) == (MAX_COLUMNS - 1) || i == totalItems - 1)
                {
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawColorButton(int index)
        {
            if (index == 0)
            {
                // Clear button
                if (_markerLibrary?.entries != null && _markerLibrary.entries.Count > 0)
                {
                    var clearIcon = _markerLibrary.entries[0].icon;
                    if (clearIcon != null && GUILayout.Button(clearIcon, GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE)))
                    {
                        OnColorSelected?.Invoke(Color.clear);
                        CloseWindow();
                    }
                }
            }
            else
            {
                // Color button
                int colorIndex = index - 1;
                if (colorIndex < _colorIcons.Length && _colorIcons[colorIndex] != null)
                {
                    if (GUILayout.Button(_colorIcons[colorIndex], GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE)))
                    {
                        OnColorSelected?.Invoke(_colors[colorIndex]);
                        CloseWindow();
                    }
                }
            }
        }

        private void DrawMarkerGrid()
        {
            if (_markerLibrary?.entries == null) return;

            var groups = _markerLibrary.entries
                .Select((entry, index) => new { entry, index })
                .GroupBy(x => x.entry.groupName);

            foreach (var group in groups)
            {
                DrawMarkerGroup(group);
            }
        }

        private void DrawMarkerGroup(IGrouping<string, dynamic> group)
        {
            int columnCount = 0;
            GUILayout.BeginHorizontal();

            foreach (var item in group)
            {
                if (columnCount >= MAX_COLUMNS)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    columnCount = 0;
                }

                if (item.entry.icon != null)
                {
                    if (GUILayout.Button(item.entry.icon, GUILayout.Width(ICON_SIZE), GUILayout.Height(ICON_SIZE)))
                    {
                        OnMarkSelected?.Invoke(item.index);
                        CloseWindow();
                    }
                }

                columnCount++;
            }

            GUILayout.EndHorizontal();
        }
        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            if (_instance == this)
            {
                DisposeColorIcons();
            }
        }
        #endregion
    }
}