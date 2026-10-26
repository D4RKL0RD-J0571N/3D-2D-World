using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace SoftKitty
{
    public class SGD_SettingsProvider : AssetSettingsProvider
    {
        private string searchContext;
        private VisualElement rootElement;
        public static SGD_Settings CurrentSettings
        {
            get
            {
                SGD_Settings settings;
                EditorBuildSettings.TryGetConfigObject(SGD_Settings.CONFIG_NAME, out settings);
                if (settings == null)
                {
                    settings = FindSGD_Settings();
                    if (settings != null)
                    {
                        EditorBuildSettings.AddConfigObject(SGD_Settings.CONFIG_NAME, settings, overwrite: true);
                        var settingsType = settings.GetType();
                        var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
                        preloadedAssets.RemoveAll(settings => (settings == null || settings.GetType() == settingsType));
                        preloadedAssets.Add(settings);
                        PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
                    }
                    else
                    {
                        EditorBuildSettings.RemoveConfigObject(SGD_Settings.CONFIG_NAME);
                        var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
                        preloadedAssets.RemoveAll(settings => (settings == null || settings.GetType() == typeof(SGD_Settings)));
                        PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
                    }
                }
                return settings;
            }
            set
            {
                var remove = (value == null);
                if (remove)
                {
                    EditorBuildSettings.RemoveConfigObject(SGD_Settings.CONFIG_NAME);
                    var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
                    preloadedAssets.RemoveAll(settings => (settings == null || settings.GetType() == typeof(SGD_Settings) ));
                    PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
                }
                else
                {
                    EditorBuildSettings.AddConfigObject(SGD_Settings.CONFIG_NAME, value, overwrite: true);
                    var settings = SGD_SettingsProvider.CurrentSettings;
                    var preloadedAssets = PlayerSettings.GetPreloadedAssets().ToList();
                    preloadedAssets.RemoveAll(settings => (settings == null || settings.GetType() == typeof(SGD_Settings)));
                    preloadedAssets.Add(settings);

                    PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
                }
            }
        }

        public SGD_SettingsProvider()
       : base("Project/SoftKitty/Data Settings", () => CurrentSettings)
        {
            CurrentSettings = FindSGD_Settings();
            keywords = GetSearchKeywordsFromGUIContentProperties<SGD_Settings>();
        }

        private static SGD_Settings FindSGD_Settings()
        {
            var filter = $"t:{typeof(SGD_Settings).FullName}";
            var guids = AssetDatabase.FindAssets(filter);
            var hasGuids = guids.Length > 0;
            if (hasGuids)
            {
                bool _warning = false;
                for (int i = 0; i < guids.Length; i++)
                {
                    if (i > 0)
                    {
                        AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guids[0]));
                        _warning = true;
                    }
                }
                if (_warning) EditorUtility.DisplayDialog("Warning", "You can only have one SGD_Settings.asset in your project, the extra ones will be removed.","OK");
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<SGD_Settings>(path);
            }
            else
            {
                return null;
            }

        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            this.rootElement = rootElement;
            this.searchContext = searchContext;
            base.OnActivate(searchContext, rootElement);
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space();
            if (CurrentSettings == null)
            {
                CurrentSettings = FindSGD_Settings();
                if (CurrentSettings != null)
                {
                    RefreshEditor();
                    return;
                }
            }

            if (CurrentSettings == null)
            {
                DisplaySettingsCreationGUI();
            }
            else
            {
                DrawCurrentSettingsGUI();
                base.OnGUI(searchContext);
            }
        }

        private void DrawCurrentSettingsGUI()
        {

            EditorGUI.BeginChangeCheck();

            EditorGUI.indentLevel++;
            var settings = EditorGUILayout.ObjectField("Current Settings", CurrentSettings,
                typeof(SGD_Settings), allowSceneObjects: false) as SGD_Settings;
            if (settings) DrawCurrentSettingsMessage();
            EditorGUI.indentLevel--;

            var newSettings = EditorGUI.EndChangeCheck();
            if (newSettings)
            {
                CurrentSettings = settings;
                RefreshEditor();
            }

        }

        private void RefreshEditor()
        {
            base.OnActivate(searchContext, rootElement);
        }

        private void DisplaySettingsCreationGUI()
        {
            const string message = "You have no SoftKitty General Data Settings. Would you like to create one?";
            EditorGUILayout.HelpBox(message, MessageType.Info, wide: true);
            var openCreationdialog = GUILayout.Button("Create");
            if (openCreationdialog)
            {
                CurrentSettings = SaveSGD_Asset();
            }
        }

        private void DrawCurrentSettingsMessage()
        {
            const string message = "This is the current SoftKitty General Data Settings and " +
                "it will be automatically included into any builds.";
            EditorGUILayout.HelpBox(message, MessageType.Info, wide: true);
        }


        private static SGD_Settings SaveSGD_Asset()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                title: "Save SoftKitty General Data Settings", defaultName: "SGD_Settings", extension: "asset",
                message: "Please enter a filename to save the projects SoftKitty General Data Settings.");
            var invalidPath = string.IsNullOrEmpty(path);
            if (invalidPath) return null;

            var settings = ScriptableObject.CreateInstance<SGD_Settings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = settings;
            return settings;
        }

        [SettingsProvider]
        private static SettingsProvider CreateProjectSettingsMenu() => new SGD_SettingsProvider();

    }
}
