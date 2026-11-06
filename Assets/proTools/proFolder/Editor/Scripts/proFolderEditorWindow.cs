using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace proTools.proFolder
{
    [Serializable]
    public class EditorWindowData
    {
        public bool enableOption = true;
        public bool hotkeyOption = true;
        public List<Color> colorPresetList = new List<Color>();
    }

    public static class proFolderSettings
    {
        private static readonly string DataFilePath = "Assets/proTools/proFolder/EditorWindowData.json";
        private static EditorWindowData _cachedData;

        public static bool EnableOption => LoadData().enableOption;
        public static bool HotkeyOption => LoadData().hotkeyOption;
        public static List<Color> ColorPresetList => new List<Color>(LoadData().colorPresetList);

        public static void SaveData(EditorWindowData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(DataFilePath, json);
            _cachedData = data;
        }

        public static EditorWindowData LoadData()
        {
            if (_cachedData != null) return _cachedData;

            if (File.Exists(DataFilePath))
            {
                string json = File.ReadAllText(DataFilePath);
                _cachedData = JsonUtility.FromJson<EditorWindowData>(json);
            }
            else
            {
                _cachedData = new EditorWindowData
                {
                    colorPresetList = GetDefaultColors()
                };
            }

            return _cachedData;
        }

        public static void ResetToDefaults()
        {
            _cachedData = new EditorWindowData
            {
                colorPresetList = GetDefaultColors()
            };
            SaveData(_cachedData);
        }

        private static List<Color> GetDefaultColors()
        {
            return new List<Color>
            {
                new Color(0.91f, 0.27f, 0.27f),
                new Color(0.99f, 0.60f, 0.30f),
                new Color(0.98f, 0.88f, 0.37f),
                new Color(0.71f, 0.91f, 0.42f),
                new Color(0.49f, 0.84f, 0.53f),
                new Color(0.38f, 0.84f, 0.78f),
                new Color(0.38f, 0.70f, 0.99f),
                new Color(0.58f, 0.55f, 0.97f),
                new Color(0.76f, 0.45f, 0.91f),
                new Color(0.91f, 0.47f, 0.76f)
            };
        }
    }

    public class proFolderEditorWindow : EditorWindow
    {
        private Texture2D logoTexture;
        private Vector2 scrollPosition;
        private EditorWindowData data;

        [MenuItem("Tools/proTools/proFolder")]
        public static void ShowWindow()
        {
            GetWindow<proFolderEditorWindow>("proFolder");
        }

        private void OnEnable()
        {
            logoTexture = Resources.Load<Texture2D>("proFolderBanner");
            data = proFolderSettings.LoadData();
        }

        private void OnDisable()
        {
            proFolderSettings.SaveData(data);
        }

        private void OnGUI()
        {
            if (logoTexture != null)
                GUILayout.Label(logoTexture, GUILayout.Height(80));

            DrawSettings();
            DrawColorPresets();
            DrawActions();
        }

        private void DrawSettings()
        {
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);

            int currentIndex = data.enableOption ? 0 : 1;
            int selectedIndex = EditorGUILayout.Popup("Effect", currentIndex, new[] { "Enable", "Disable" });
            data.enableOption = selectedIndex == 0;

            int currentHotkeyIndex = data.hotkeyOption ? 0 : 1;
            int selectedHotkeyIndex = EditorGUILayout.Popup("Hotkey Mode", currentHotkeyIndex, new[] { "Shift + Click", "Alt + Click" });
            data.hotkeyOption = selectedHotkeyIndex == 0;

            EditorGUILayout.EndVertical();
        }

        private void DrawColorPresets()
        {
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Folder Color Presets", EditorStyles.boldLabel);

            float scrollHeight = Mathf.Min(data.colorPresetList.Count * 25f + 10f, 220f);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollHeight));

            for (int i = 0; i < data.colorPresetList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                data.colorPresetList[i] = EditorGUILayout.ColorField($"Color {i + 1}", data.colorPresetList[i]);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    data.colorPresetList.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("New Color"))
                data.colorPresetList.Add(Color.white);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Colors"))
                data.colorPresetList.Clear();

            if (GUILayout.Button("Restore Default Colors"))
            {
                data.colorPresetList.Clear();
                data.colorPresetList.AddRange(proFolderSettings.LoadData().colorPresetList);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Auto & Reset", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto Assign Marks"))
            {
                FolderDataManager.AutoAssignMarkers();
                AssetDatabase.Refresh();
                EditorApplication.RepaintProjectWindow();
            }

            if (GUILayout.Button("Reset Effects"))
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Reset Colors"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Reset Colors", "Remove all folder colors?", "Yes", "Cancel"))
                    {
                        FolderDataManager.folderColors.Clear();
                        FolderDataManager.Save();
                        EditorApplication.RepaintProjectWindow();
                    }
                });

                menu.AddItem(new GUIContent("Reset Markers"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Reset Markers", "Remove all folder markers?", "Yes", "Cancel"))
                    {
                        FolderDataManager.folderMarkers.Clear();
                        FolderDataManager.Save();
                        EditorApplication.RepaintProjectWindow();
                    }
                });

                menu.AddItem(new GUIContent("Reset All"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Reset All", "Remove all folder colors and markers?", "Yes", "Cancel"))
                    {
                        FolderDataManager.folderColors.Clear();
                        FolderDataManager.folderMarkers.Clear();
                        FolderDataManager.Save();
                        EditorApplication.RepaintProjectWindow();
                    }
                });

                menu.ShowAsContext();
            }
        }
    }
}