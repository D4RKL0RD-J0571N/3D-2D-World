using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace SoftKitty.MasterNavigationMap
{
    [CustomEditor(typeof(MMN_Settings))]
    public class MMN_Settings_Inspector : Editor
    {
        bool _sceneMapExpand = false;
        bool _generalExpand = false;
        bool _mapSettingExpand = false;
        bool _miniMapSettingExpand = false;
        string _packRootPath;
        Texture logoIcon;




        public override void OnInspectorGUI()
        {
            GUI.changed = false;

            Color _backgroundColor = GUI.backgroundColor;
            var script = MonoScript.FromScriptableObject(this);
            MMN_Settings myTarget = (MMN_Settings)target;

            string _thePath = AssetDatabase.GetAssetPath(script);
            _thePath = _thePath.Replace("MMN_Settings_Inspector.cs", "");
            _packRootPath = _thePath.Replace("Editor/", "");
            if (logoIcon==null) {
                logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
            }
            EditorUtils.TitleLogo(logoIcon,0);
            EditorGUILayout.Separator();
            EditorUtils.Document("master-map-navigation/overview", true);
            EditorGUILayout.Separator();

            int _lastLayer = 1;
            for (int i = 0; i < 32; i++)
            {
                if (LayerMask.LayerToName(i) != "") _lastLayer=i+1;
            }

            List<string> _layers = new List<string>();
            for (int i = 0; i < _lastLayer; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (layerName != "") 
                    _layers.Add(layerName);
                else
                    _layers.Add(i.ToString()+" Empty");
            }
            string[] layers = _layers.ToArray();

            EditorUtils.TitleButtonWithHelp(EditorUtils.TextHint("General Settings"), ref _generalExpand,10,true,0, "master-map-navigation/settings/general-settings");
            if (_generalExpand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);


                if (EditorUtils.TitleBox(new GUIContent("Map Icon Categories:"), EditorUtils._titleColor, 10, true,"Add",100))
                {
                    IconGroupSetting _newSetting = new IconGroupSetting();
                    _newSetting._name = "New Category " + MMN_SettingsProvider.CurrentSettings.IconGroups.Count.ToString();
                    _newSetting._icon = null;
                    _newSetting._fold = false;
                    MMN_SettingsProvider.CurrentSettings.IconGroups.Add(_newSetting);
                }

                

                for (int i=0;i< MMN_SettingsProvider.CurrentSettings.IconGroups.Count;i++) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    MMN_SettingsProvider.CurrentSettings.IconGroups[i]._fold = EditorGUILayout.Foldout(
                        MMN_SettingsProvider.CurrentSettings.IconGroups[i]._fold,
                        MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name,true);
                    GUILayout.Space(100);
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button("X",GUILayout.Width(20))) {
                        MMN_SettingsProvider.CurrentSettings.IconGroups.RemoveAt(i);
                        GUILayout.EndHorizontal();
                        return;
                    }
                    GUILayout.FlexibleSpace();
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();

                    if (MMN_SettingsProvider.CurrentSettings.IconGroups[i]._fold) {
                        string _oldName = MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name;
                        EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name, EditorUtils.TextHint( "Category Name:"), 50);
                        if (_oldName != MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name && _oldName!="" && MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name!="") {
                            for (int u=0;u< MMN_SettingsProvider.CurrentSettings.IconGroups.Count;u++) {
                                if (u != i && MMN_SettingsProvider.CurrentSettings.IconGroups[u]._name == MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name)
                                {
                                    MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name += " ("+i.ToString()+")";
                                }
                            }
                        }
                        TextureField(ref MMN_SettingsProvider.CurrentSettings.IconGroups[i]._icon, "Category Icon:", 50);
                        EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.IconGroups[i]._visisbleDefault, EditorUtils.TextHint("Default Visibility:"), 50);
                    }
                }

                EditorGUILayout.Separator();


                EditorUtils.TitleBox(new GUIContent("Player Settings:"), EditorUtils._titleColor, 10, false);

                TextureField(ref MMN_SettingsProvider.CurrentSettings.PlayerIcon, "Player's Icon:", 30);

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label("Player Orientation:", GUILayout.Width(150));
                GUI.backgroundColor = EditorUtils._black;
                myTarget.PlayerOrientation = (MMN_Settings.PlayerOrientations)EditorGUILayout.EnumPopup(myTarget.PlayerOrientation, GUILayout.Width(150));
                GUILayout.EndHorizontal();

              
                EditorGUILayout.Separator();


                EditorUtils.TitleBox(new GUIContent("Custom Map Marker Settings:"), EditorUtils._titleColor, 10, false);

                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.EnableCustomMarker, EditorUtils.TextHint("Enable Custom Markers"), 30);

                if (MMN_SettingsProvider.CurrentSettings.EnableCustomMarker)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("When placing custom marker:", GUILayout.Width(250));
                    GUILayout.EndHorizontal();
                    MouseButton(ref MMN_SettingsProvider.CurrentSettings.CustomMarkerConfirmButton, "Confirm Button:", 50);
                    MouseButton(ref MMN_SettingsProvider.CurrentSettings.CustomMarkerCancelButton, "Cancel Button:", 50);
                    KeyButton(ref MMN_SettingsProvider.CurrentSettings.CustomMarkerCancelKey, "Cancel Key:", 50);
                    EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.AllowPlayerToRenameCustomMarker, EditorUtils.TextHint("Allow player to rename"), 50);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._black;
                    GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                    GUILayout.Label("Custom Markers Settings:");
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add", GUILayout.Width(100)))
                    {
                        CustomMarkerSetting _newSetting = new CustomMarkerSetting();
                        _newSetting._defaultName = "Custom Marker " + MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings.Count.ToString();
                        _newSetting._uid= "cm" + MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings.Count.ToString();
                        _newSetting._icon = null;
                        MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings.Add(_newSetting);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();

                    for (int i = 0; i < MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(40);
                        if (MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._icon == null) GUI.color = Color.red;
                        MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._fold = EditorGUILayout.Foldout(
                            MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._fold,
                            MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._defaultName, true);
                        EditorUtils.ResetColor();
                        GUILayout.Space(100);
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings.RemoveAt(i);
                            GUILayout.EndHorizontal();
                            return;
                        }
                        GUILayout.FlexibleSpace();
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();

                        if (MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._fold) {
                            EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._defaultName, EditorUtils.TextHint( "Default Name:"), 60);
                            string _oldName = MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._uid;
                            EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._uid, EditorUtils.TextHint("Unique ID:"), 60);
                            if (_oldName != MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._uid && _oldName != "" && MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._uid != "")
                            {
                                for (int u = 0; u < MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings.Count; u++)
                                {
                                    if (u != i && MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[u]._uid == MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._uid)
                                    {
                                        MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._uid += "(" + i.ToString() + ")";
                                    }
                                }
                            }
                            if (MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._icon == null) GUI.color = Color.red;
                            TextureField(ref MMN_SettingsProvider.CurrentSettings.CustomMarkerSettings[i]._icon, "Marker Icon:", 60);
                            EditorUtils.ResetColor();
                        }
                    }
                }

                EditorGUILayout.Separator();

                EditorUtils.TitleBox(new GUIContent("Visual Settings:"), EditorUtils._titleColor, 10, false);
                EditorUtils.ColorField(ref MMN_SettingsProvider.CurrentSettings.NavigationLineColor, new GUIContent("Navigation Line Color", "The color of the navigation lines on the map and 3d world."), 30, 150, true, new Color(0F, 0.6F, 1F, 1F));
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button("Modify The Line Prefab", GUILayout.Width(250)))
                {
                    GameObject _linePrefab= (GameObject)AssetDatabase.LoadAssetAtPath("Assets/SoftKitty/MapNavigationSystem/Resources/MapNavigationSystem/NavigationPath.prefab",typeof(GameObject));
                    Selection.activeObject = _linePrefab;
                    EditorGUIUtility.PingObject(_linePrefab);
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button("Modify The Marker Prefab", GUILayout.Width(250)))
                {
                    GameObject _linePrefab = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/SoftKitty/MapNavigationSystem/Resources/MapNavigationSystem/Marker.prefab", typeof(GameObject));
                    Selection.activeObject = _linePrefab;
                    EditorGUIUtility.PingObject(_linePrefab);
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button("Modify The Custom Marker Prefab", GUILayout.Width(250)))
                {
                    GameObject _linePrefab = (GameObject)AssetDatabase.LoadAssetAtPath("Assets/SoftKitty/MapNavigationSystem/Resources/MapNavigationSystem/CustomMarker.prefab", typeof(GameObject));
                    Selection.activeObject = _linePrefab;
                    EditorGUIUtility.PingObject(_linePrefab);
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();
                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }

            EditorUtils.TitleButtonWithHelp(EditorUtils.TextHint("Scene | Map"),ref _sceneMapExpand,10,true,0, "master-map-navigation/settings/scene-map");
            if (_sceneMapExpand) 
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("Map Mode:",GUILayout.Width(80));
                GUI.backgroundColor = EditorUtils._titleColor;
                myTarget.MapMode = (MMN_Settings.MapModes)EditorGUILayout.EnumPopup(myTarget.MapMode, GUILayout.Width(150));
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                if (myTarget.MapMode == MMN_Settings.MapModes.StaticMap)
                {
                    EditorUtils.HelpInfo("Pre-render your 3D scene into a stylized 2D map texture using the Map Generator tool.\nThis mode provides the best performance and works seamlessly for scene-based games with pre-designed levels.",10 );
                    EditorUtils.HelpInfo("Best For:\nGames with pre-designed scenes where the world layout doesn't change, such as RPGs, dungeon crawlers, or strategy games with fixed maps.",10 );

                    EditorGUILayout.Separator();

                    PreGeneratedMode();
                }
                else if (myTarget.MapMode == MMN_Settings.MapModes.DynamicMap)
                {
                    
                    EditorUtils.HelpInfo("Render a real-time stylized map using a top-down camera that captures the 3D scene dynamically.\nThis mode is ideal for procedurally generated worlds or massive open-world games that require seamless and real-time updates.", 10);
                    EditorUtils.HelpInfo("Best For:\nGames with procedural generation, seamless open-world exploration, or environments where the terrain and objects may change dynamically at runtime.", 10);
                    

                    EditorGUILayout.Separator();
                    EditorUtils.TitleBox(EditorUtils.TextHint("Render Texture Settings:"), EditorUtils._titleColor, 10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("RT LayerMask:", GUILayout.Width(150));
                    GUI.backgroundColor = EditorUtils._black;
                    MMN_SettingsProvider.CurrentSettings.RtLayerMask = EditorGUILayout.MaskField(MMN_SettingsProvider.CurrentSettings.RtLayerMask, layers, GUILayout.Width(150));
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                    RealTimeMode();
                }

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }

            EditorUtils.TitleButtonWithHelp(EditorUtils.TextHint("World Map Settings"), ref _mapSettingExpand,10,true,0, "master-map-navigation/settings/world-map");
            if (_mapSettingExpand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.WorldMapDefaultZoom,0f,1f, EditorUtils.TextHint("Default Zoom(normalized):"), " ", 10,200,true,0.5F);
                EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.WorldMapMaxmiumZoomIn, 2f, 10F, EditorUtils.TextHint("Maximum Zoom In:"), "x", 10, 200, true, 3F);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.PanBeyondBounds, EditorUtils.TextHint("Pan Beyond Bounds"), 10, 200);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.LockIconScale, EditorUtils.TextHint("Lock Icon Scale:"), 10, 200);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.DisplaySceneTitle, EditorUtils.TextHint("Display Scene Title:"), 10, 200);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.EnableNavigationPath, EditorUtils.TextHint("Enable Navigation Path"), 10, 200, "master-map-navigation/navigation");
                if(MMN_SettingsProvider.CurrentSettings.EnableNavigationPath) MouseButton(ref MMN_SettingsProvider.CurrentSettings.StartNavigationMouseButton, "Start Navigation Button:", 10);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.EnableScroll, EditorUtils.TextHint("Enable scroll with keyboard"), 10, 200);
                if (MMN_SettingsProvider.CurrentSettings.EnableScroll)
                {
                    KeyButton(ref MMN_SettingsProvider.CurrentSettings.MapScrollLeftKey, "Map Scroll Left Key:", 10);
                    KeyButton(ref MMN_SettingsProvider.CurrentSettings.MapScrollRightKey, "Map Scroll Right Key:", 10);
                    KeyButton(ref MMN_SettingsProvider.CurrentSettings.MapScrollUpKey, "Map Scroll Up Key:", 10);
                    KeyButton(ref MMN_SettingsProvider.CurrentSettings.MapScrollDownKey, "Map Scroll Down Key:", 10);
                    EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.ScrollSpeed, 0.5F, 3F, EditorUtils.TextHint("Scroll Speed:"), "x", 10, 200, true, 1F);
                }
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.EnableMarkers, EditorUtils.TextHint("Allow player place map markers"), 10, 200);
                if (MMN_SettingsProvider.CurrentSettings.EnableMarkers)
                {
                    EditorUtils.IntSlider(ref MMN_SettingsProvider.CurrentSettings.MaximumMarkers, 1, 99, EditorUtils.TextHint("Maximum Markers:"), "", 30, 200);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("When more than "+ MMN_SettingsProvider.CurrentSettings.MaximumMarkers+" markers:", GUILayout.Width(170));
                    GUI.backgroundColor = EditorUtils._black;
                    myTarget.MarkerOutNumberMethod = (MMN_Settings.MarkerOutNumberMethods)EditorGUILayout.EnumPopup(myTarget.MarkerOutNumberMethod, GUILayout.Width(150));
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                    if (myTarget.MarkerOutNumberMethod == MMN_Settings.MarkerOutNumberMethods.Callback)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        EditorGUILayout.HelpBox("Callback code example:\n" +
                            "MarkerOutLimitCallback += OnMarkerOutLimit;\n" +
                            "public void OnMapLayerChange(int _layer) {...}", MessageType.Info, true);
                        GUILayout.Space(20);
                        GUILayout.EndHorizontal();
                    }
                }
                if (MMN_SettingsProvider.CurrentSettings.EnableMarkers) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    GUILayout.Label("Create Map Marker Key + Mouse Button:");
                    GUILayout.EndHorizontal();
                    MouseKeyButton(ref MMN_SettingsProvider.CurrentSettings.MapMarkerKey, ref MMN_SettingsProvider.CurrentSettings.MapMarkerButton, 30);
                }

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }

            EditorUtils.TitleButtonWithHelp(EditorUtils.TextHint("Mini Map Settings"), ref _miniMapSettingExpand,10,true,0, "master-map-navigation/settings/mini-map");
            if (_miniMapSettingExpand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.MiniMapDefaultZoom, 0f, 1F, EditorUtils.TextHint("Default Zoom(normalized):"), " ", 10, 200, true,0.5F);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.AllowZoomInOut, EditorUtils.TextHint("Allow Zoom In/Out:"), 10,200);
                if (MMN_SettingsProvider.CurrentSettings.AllowZoomInOut) EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.MiniMapMaxmiumZoomIn, 2f, 10F, EditorUtils.TextHint("Maximum Zoom In:"), "x", 10, 200, true,5);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.MiniLockIconScale, EditorUtils.TextHint("Lock Icon Scale:"), 10, 200);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.MiniDisplaySceneTitle, EditorUtils.TextHint("Display Scene Title:"), 10, 200);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.MiniDisplayCoordinate, EditorUtils.TextHint("Display Coordinate:"), 10, 200);
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.MiniMapRotateWithCamera, EditorUtils.TextHint("Map Rotate With Camera:"), 10, 200);
                if (!MMN_SettingsProvider.CurrentSettings.MiniMapRotateWithCamera)
                {
                    EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.ToggleCompassButton, EditorUtils.TextHint("Enable Player Orientation Button:"), 10, 200);
                    EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.LockPlayerNorth, EditorUtils.TextHint("Player Orientation as Default:"), 10, 200);
                }
                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.ShowTrackArrowWhenNavigate, EditorUtils.TextHint("Show Tack Arrow When Navigate:"), 10, 200);
                EditorUtils.HelpInfo("When navigate to a target position, toggle this option to show a white arrow point to this position on the mini-map. You can also manually call:\n" +
                    "<color=#33FF66>MapManager.StartTrack(Transform _target)</color> and <color=#33FF66>MapManager.EndTrack()</color> to use this arrow with your own logic.", 10);

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }


            if (GUI.changed && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(myTarget);
        }


        private void RealTimeMode()
        {

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.Label("Render Area Size:", GUILayout.Width(150));
            MMN_SettingsProvider.CurrentSettings.WorldMapSize = Mathf.Max(500, EditorGUILayout.IntField(MMN_SettingsProvider.CurrentSettings.WorldMapSize, GUILayout.Width(50)));
            GUILayout.Label("m x " + MMN_SettingsProvider.CurrentSettings.WorldMapSize + "m", GUILayout.Width(80));
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();

            EditorUtils.HelpInfo("This determines how large the area around the player will be captured by the top-down render texture camera.\n" +
                "Larger sizes allow the player to scroll further on the world map when [Unlimited WorldMap] is unchecked.\n" +
                "A larger size also reduces the frequency of minimap updates but may have a higher performance cost.", 30);

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label("RT Size:", GUILayout.Width(150));
            GUI.backgroundColor = EditorUtils._black;
            MMN_SettingsProvider.CurrentSettings.RtSize = (MMN_Settings.TextureSize)EditorGUILayout.EnumPopup(MMN_SettingsProvider.CurrentSettings.RtSize, GUILayout.Width(150));
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();

            EditorUtils.HelpInfo("The render texture size depends on the [Render Area Size].\n" +
                "For optimal clarity, use a larger texture size with a larger render area size (ideally 2 pixels per meter or higher).\n" +
                "However, larger textures may cause noticeable performance spikes when the minimap updates. (The minimap only updates when the player moves out of the previously rendered area.)", 30);

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            int _size = 1024 * Mathf.FloorToInt(Mathf.Pow(2, (int)MMN_SettingsProvider.CurrentSettings.RtSize));
            GUI.color = EditorUtils._gray;
            GUILayout.Box("Resolution: "+ (_size/ MMN_SettingsProvider.CurrentSettings.WorldMapSize).ToString("0.0")+" pixels/meter");
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.UnlimitedWorldMap, EditorUtils.TextHint("Unlimited WorldMap"), 30,150);
            EditorUtils.HelpInfo("When enabled, players can scroll the map to any position in the 3D world.\n" +
                "Ensure that in your script, the 3D scene within MapInteractive.WorldMapInstance.ViewRect is always loaded to match the visible map area.", 30);


            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label("RT Camera Height:", GUILayout.Width(150));
            MMN_SettingsProvider.CurrentSettings.RtCameraHeight =GUILayout.HorizontalSlider(MMN_SettingsProvider.CurrentSettings.RtCameraHeight,50F,1000F);
            GUILayout.Label(Mathf.FloorToInt(MMN_SettingsProvider.CurrentSettings.RtCameraHeight).ToString()+"m", GUILayout.Width(50));
            GUILayout.Space(30);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label("RT Camera Depth:", GUILayout.Width(150));
            MMN_SettingsProvider.CurrentSettings.RtCameraDepth = Mathf.Clamp(MMN_SettingsProvider.CurrentSettings.RtCameraDepth, MMN_SettingsProvider.CurrentSettings.RtCameraHeight+10, MMN_SettingsProvider.CurrentSettings.RtCameraHeight + 500F);
            MMN_SettingsProvider.CurrentSettings.RtCameraDepth = GUILayout.HorizontalSlider(MMN_SettingsProvider.CurrentSettings.RtCameraDepth, MMN_SettingsProvider.CurrentSettings.RtCameraHeight+10, MMN_SettingsProvider.CurrentSettings.RtCameraHeight+500F);
            GUILayout.Label(Mathf.FloorToInt(MMN_SettingsProvider.CurrentSettings.RtCameraDepth).ToString() + "m", GUILayout.Width(50));
            GUILayout.Space(30);
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.RtStylizedRendering, EditorUtils.TextHint("Stylized Rendering"),30);
            if (MMN_SettingsProvider.CurrentSettings.RtStylizedRendering) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.Label("RT Material:", GUILayout.Width(150));
                GUI.backgroundColor = EditorUtils._black;
                MMN_SettingsProvider.CurrentSettings.RtStylizedeMaterial = (Material)EditorGUILayout.ObjectField(MMN_SettingsProvider.CurrentSettings.RtStylizedeMaterial, typeof(Material),false, GUILayout.Width(200));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                EditorUtils.HelpInfo("Adjust the stylized settings on the material.\n" +
                    "You could create your own custom render texture shader.\n" +
                    "and assign the material here.", 30);

                EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.RtDepthOnly, EditorUtils.TextHint("Depth Only"), 30);
                EditorUtils.HelpInfo("When using my custom render texture material.\n" +
                    "Please make sure the [ Depth Only ] is checked.", 30);

            }

        }

        private void PreGeneratedMode()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button("Open [Map Generator] Tool (Render Map Texture)"))
            {
                MapCreator _creator = (MapCreator)Object.FindAnyObjectByType<MapCreator>();
                if (_creator == null)
                {
                    GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_packRootPath + "Prefabs/MapGenerator.prefab");
                    GameObject _tool = Instantiate(_prefab) as GameObject;
                    _tool.name = "Map Generator (MMN Tool)";
                    _creator = _tool.GetComponent<MapCreator>();
                }
                Selection.activeObject = _creator.gameObject;
                _creator.Init();

            }
            EditorUtils.ResetColor();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Refresh Scene List From Build Settings"))
            {
                int _sceneCount = SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < _sceneCount; i++)
                {
                    string _scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    int _index = _scenePath.LastIndexOf("/");
                    string _sceneName = _scenePath.Substring(_index + 1, _scenePath.Length - _index - 1).Replace(".unity", "");
                    AddScene(_sceneName, _scenePath);
                }
                UpdateGenertorTool();
            }
            GUILayout.Space(10);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            if (GUILayout.Button("Add Current Scene"))
            {
                Scene _scene = SceneManager.GetActiveScene();
                string _sceneName = _scene.name;
                string _scenePath = _scene.path;
                AddScene(_sceneName, _scenePath);
                UpdateGenertorTool();
            }
            EditorUtils.ResetColor();
            GUILayout.Space(10);
            GUILayout.EndHorizontal();


            for (int i = 0; i < MMN_SettingsProvider.CurrentSettings.SceneMaps.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                SubTitleButton(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._sceneName, ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fold);
                GUILayout.Space(120);
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    MMN_SettingsProvider.CurrentSettings.SceneMaps.RemoveAt(i);
                    GUILayout.EndHorizontal();
                    UpdateGenertorTool();
                    return;
                }
                EditorUtils.ResetColor();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fold)
                {
                    GUI.backgroundColor = EditorUtils._black;
                    EditorUtils.BoxInfo(EditorUtils.TextHint(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._scenePath), 30,true);
                    EditorUtils.ResetColor();
                    EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._sceneTitle, EditorUtils.TextHint("Display Title:"), 30,150,true);

                    EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarEnable, EditorUtils.TextHint("Enable Fog of War."),30,150, "master-map-navigation/fog-of-war");

                    if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarEnable) {
                        EditorUtils.HelpInfo("Fog of war is enabled, initially the map will be covered with '<color=#6688FF>Fog Color</color>', player will have to walk over the map to reveal the map. Make sure save/load the fog of war data via:\n" +
                            "<color=#66FF88>MapManager.SaveFog()</color> & <color=#66FF88>MapManager.LoadFog()</color>", 50);
                        EditorUtils.ColorField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarColor, EditorUtils.TextHint("Fog Color:", "The color of the fog. Use the alpha channel for overlay transparency."), 50, 150, true, Color.black);
                        EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarRadius, 0.01F, 0.3F, EditorUtils.TextHint("Reveal Radius:", "Normalized value of the whole map ."), "", 50, 150, true, 0.05F);
                        EditorUtils.IntField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarWidth, EditorUtils.TextHint("Mask Texture Width:", "The width of the mask texture, 256~512 is recommended."), 50, true, 256);
                        EditorUtils.IntField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarWidth, EditorUtils.TextHint("Mask Texture Height:", "The height of the mask texture, 256~512 is recommended."), 50, true, 256);
                        EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._hideIconsInFog, EditorUtils.TextHint("Hide Map Icons in Fog:", "Toggle this option to hide map icons in fog area."), 50);
                        EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarWorldMap, EditorUtils.TextHint("Enable fog for world map:", "Enable fog of war for the world map."), 50);
                        EditorUtils.Toggle(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._fogOfWarMiniMap, EditorUtils.TextHint("Enable fog for mini map:", "Enable fog of war for the mini map."), 50);
                        EditorGUILayout.Separator();
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                    GUILayout.Label("Map Layers");
                    if (GUILayout.Button("Add", GUILayout.Width(100)))
                    {
                        MapSetting _map = new MapSetting();
                        _map._name = MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count == 0 ? "Base" : ("Layer" + (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count + 1).ToString());
                        _map._fold = false;
                        _map._map = SearchMap(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._sceneName, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count);
                        MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Add(_map);
                        UpdateGenertorTool();
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                    GUILayout.Space(30);
                    GUILayout.EndHorizontal();

                    if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count == 0)
                    {
                        EditorUtils.Warnning("You must have at least one map layer, please click above button to add one.", 30);
                    }

                    for (int u = 0; u < MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count; u++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(40);
                        SubTitleButton("Map Layer " + u.ToString() + " (" + MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._name + ")",
                            ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._fold);
                        GUILayout.Space(120);
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.RemoveAt(u);
                            GUILayout.EndHorizontal();
                            UpdateGenertorTool();
                            return;
                        }
                        EditorUtils.ResetColor();
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._fold)
                        {
                            EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._name, EditorUtils.TextHint( "Name:"), 60,150,true);
                            TextureField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._map, "Map Texture:", 60);
                            EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._height, -50F, 1000F, EditorUtils.TextHint("Layer Height:"), "m", 60);
                            EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._ceil, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._height+2F, 1000F, EditorUtils.TextHint("Layer Ceil:"), "m", 60);
                        }
                    }

                    
                    Vector3Field(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._bottomLeft, "Bottom Left Corner Position:", 30);
                    bool _outside= Vector3FieldLimited(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._topRight, "Top Right Corner Position:", 30,
                        new Vector2(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._bottomLeft.x,100000000),
                        new Vector2(-50F, 1000F),
                        new Vector2(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._bottomLeft.z, 100000000)
                        );
                    if (_outside)
                    {
                        EditorUtils.Warnning("The x and z value of the top right corner can not be less than the bottom left corner.", 30);
                    }

                    EditorUtils.ColorField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._backgroundColor, EditorUtils.TextHint("Map Background:"), 30);

                    EditorGUILayout.Separator();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    SubTitleButton("Detailed Location Sub-Maps", ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMapFold);
                    GUILayout.EndHorizontal();

                    if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMapFold) {
                        EditorUtils.HelpInfo("Enhance navigation by adding high-detail sub-maps for key locations like dungeons or castles.\n" +
                            "The system will seamlessly transition to these sub-maps upon entry,\n" +
                            "or they can be accessed via an interactive icon on your primary map.", 50);

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        GUI.backgroundColor = EditorUtils._black;
                        GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                        GUILayout.Label("Sub-Maps");
                        if (GUILayout.Button("Add", GUILayout.Width(100)))
                        {
                            SubMapSetting _newSubMap = new SubMapSetting();
                            _newSubMap._areaTitle = "New Sub-Map " + MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps.Count;
                            _newSubMap.uid = "submap" + MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps.Count;
                            _newSubMap._bottomLeft = MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._bottomLeft;
                            _newSubMap._topRight = MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._topRight;
                            _newSubMap._maps = new List<MapSetting>();
                            _newSubMap._fold = false;
                            MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps.Add(_newSubMap);
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                        GUILayout.Space(50);
                        GUILayout.EndHorizontal();


                        for (int u = 0; u < MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps.Count; u++)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(50);
                            SubTitleButton("(UID: "+ MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u].uid+ ") "+
                                MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._areaTitle, ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._fold);
                            GUILayout.Space(200);
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button("X", GUILayout.Width(20)))
                            {
                                MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps.RemoveAt(u);
                                GUILayout.EndHorizontal();
                                return;
                            }
                            EditorUtils.ResetColor();
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();

                            if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._fold) {
                                EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u].uid, EditorUtils.TextHint("(Sub-Map) UID:"), 70,150,true);
                                EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._areaTitle, EditorUtils.TextHint("(Sub-Map) Display Title:"), 70, 150, true);

                                GUILayout.BeginHorizontal();
                                GUILayout.Space(70);
                                GUI.backgroundColor = MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Count==0?Color.red:EditorUtils._titleColor;
                                if (GUILayout.Button("(Sub-Map) Add New Map Layer", GUILayout.Width(200)))
                                {
                                    MapSetting _map = new MapSetting();
                                    _map._name = MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Count == 0 ? "Base" : ("Layer" + (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Count + 1).ToString());
                                    _map._fold = false;
                                    _map._map = SearchMap(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._sceneName, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Count,"_sub"+u.ToString());
                                    MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Add(_map);
                                    UpdateGenertorTool();
                                }
                                EditorUtils.ResetColor();
                                GUILayout.Space(40);
                                GUILayout.EndHorizontal();

                                if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Count == 0)
                                {
                                    EditorUtils.Warnning("You must have at least one map layer, please click above button to add one.",70);
                                }

                                for (int y = 0; y < MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.Count; y++)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Space(70);
                                    SubTitleButton("(Sub-Map) Map Layer " + y.ToString() + " (" + MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._name + ")",
                                        ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._fold);
                                    GUILayout.Space(180);
                                    GUI.backgroundColor = EditorUtils._black;
                                    if (GUILayout.Button("X", GUILayout.Width(20)))
                                    {
                                        MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps.RemoveAt(y);
                                        GUILayout.EndHorizontal();
                                        UpdateGenertorTool();
                                        return;
                                    }
                                    EditorUtils.ResetColor();
                                    GUILayout.FlexibleSpace();
                                    GUILayout.EndHorizontal();

                                    if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._fold)
                                    {
                                        EditorUtils.TextField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._name, EditorUtils.TextHint("Name:"), 90);
                                        TextureField(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._map, "Map Texture:", 90);
                                        EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._height, -50F, 1000F, EditorUtils.TextHint("Layer Height:"), "m", 90);
                                        EditorUtils.FloatSlider(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._ceil, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._maps[y]._height+2F, 1000F, EditorUtils.TextHint("Layer Ceil:"), "m", 90);
                                    }
                                }

                                bool _outrange= Vector3FieldLimited(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._bottomLeft, "(Sub-Map) Bottom Left Corner Position:", 70,
                                  new Vector2(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._bottomLeft.x, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._topRight.x),
                                  new Vector2(-50,1000),
                                  new Vector2(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._bottomLeft.z, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._topRight.z));
                                if (_outrange) {
                                    EditorUtils.Warnning("The bottom left corner can not be outside the main map rect.", 70);
                                }
                                _outrange = Vector3FieldLimited(ref MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._topRight, "(Sub-Map) Top Right Corner Position:", 70,
                                  new Vector2(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._bottomLeft.x, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._topRight.x),
                                  new Vector2(-50, 1000),
                                  new Vector2(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u]._bottomLeft.z, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._topRight.z));
                                if (_outrange)
                                {
                                    EditorUtils.Warnning("The top right corner can not be outside the main map rect.", 70);
                                }
                            }
                        }
                    }

                    

                }
            }
        }


        private Texture2D SearchMap(string _sceneName, int _layer, string _extra="")
        {
            string _path = _packRootPath + "MapTextures/" + _sceneName + _extra + "_" +(_layer+1).ToString()+ ".png";
            return (Texture2D)AssetDatabase.LoadAssetAtPath(_path, typeof(Texture2D));
        }

        private void Vector3Field(ref Vector3 _value,string _name, int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name);
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(_space+20);
            GUILayout.Label("x:", GUILayout.Width(20));
            _value.x = EditorGUILayout.FloatField(_value.x, GUILayout.Width(40));
            GUILayout.Label("y:", GUILayout.Width(20));
            _value.y = EditorGUILayout.FloatField(_value.y, GUILayout.Width(40));
            GUILayout.Label("z:", GUILayout.Width(20));
            _value.z = EditorGUILayout.FloatField(_value.z, GUILayout.Width(40));
            GUILayout.Space(10);
            GUI.backgroundColor = Selection.activeTransform!=null? EditorUtils._titleColor :Color.gray;
            if (GUILayout.Button("Copy from selected"))
            {
                _value = Selection.activeTransform.position;
            }
            EditorUtils.ResetColor();
            GUILayout.Space(30);
            GUILayout.EndHorizontal();

        }

        private bool Vector3FieldLimited(ref Vector3 _value, string _name, int _space,Vector2 _xRange, Vector2 _yRange, Vector2 _zRange)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name);
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(_space + 20);
            GUI.backgroundColor = (_value.x < _xRange.x || _value.x > _xRange.y) ? Color.red : Color.white;
            GUILayout.Label("x:", GUILayout.Width(20));
            _value.x = EditorGUILayout.FloatField(_value.x, GUILayout.Width(40));
            GUI.backgroundColor = (_value.y < _yRange.x || _value.y > _yRange.y) ? Color.red : Color.white;
            GUILayout.Label("y:", GUILayout.Width(20));
            _value.y = EditorGUILayout.FloatField(_value.y, GUILayout.Width(40));
            GUI.backgroundColor = (_value.z < _zRange.x || _value.z > _zRange.y) ? Color.red : Color.white;
            GUILayout.Label("z:", GUILayout.Width(20));
            _value.z = EditorGUILayout.FloatField(_value.z, GUILayout.Width(40));
            GUILayout.Space(10);
            GUI.backgroundColor = Selection.activeTransform != null ? EditorUtils._titleColor : Color.gray;
            if (GUILayout.Button("Copy from selected"))
            {
                _value = Selection.activeTransform.position;
            }
            EditorUtils.ResetColor();
            GUILayout.Space(30);
            GUILayout.EndHorizontal();
            return (_value.x < _xRange.x || _value.x > _xRange.y || _value.y < _yRange.x || _value.y > _yRange.y || _value.z < _zRange.x || _value.z > _zRange.y);
        }


        private void TextureField(ref Texture2D _object,  string _name, int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name, GUILayout.Width(150));
            GUI.backgroundColor = EditorUtils._black;
            _object = (Texture2D)EditorGUILayout.ObjectField(_object,typeof(Texture2D),false);
            GUI.backgroundColor = Color.white;
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
        }



        private void Label(string _text,int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_text);
            GUILayout.EndHorizontal();
        }

        private void KeyButton(ref KeyCode _value,string _name,int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name, GUILayout.Width(200));
            GUI.backgroundColor = EditorUtils._black;
            _value = (KeyCode)EditorGUILayout.EnumPopup(_value, GUILayout.Width(150));
            GUI.backgroundColor = Color.white;
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
        }

        private void MouseKeyButton(ref KeyCode _keyValue, ref MouseButton _mouseValue, int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUI.backgroundColor = EditorUtils._black;
            _keyValue = (KeyCode)EditorGUILayout.EnumPopup(_keyValue, GUILayout.Width(140));
            GUILayout.Label("+",GUILayout.Width(20));
            _mouseValue = (MouseButton)EditorGUILayout.EnumPopup(_mouseValue, GUILayout.Width(140));
            GUI.backgroundColor = Color.white;
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
        }

        private void MouseButton(ref MouseButton _value, string _name, int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name, GUILayout.Width(200));
            GUI.backgroundColor = EditorUtils._black;
            _value = (MouseButton)EditorGUILayout.EnumPopup(_value, GUILayout.Width(150));
            GUI.backgroundColor = Color.white;
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
        }

        private void UpdateGenertorTool()
        {
            MapCreator [] _creator = FindObjectsByType<MapCreator>(FindObjectsSortMode.None);
            foreach (var obj in _creator)
            {
                if (obj != null) obj.Init();
            }
        }

        private void AddScene(string _sceneName, string _scenePath)
        {
            Dictionary<string, SceneMapSetting> _sceneMapDic = new Dictionary<string, SceneMapSetting>();
            for (int i = 0; i < MMN_SettingsProvider.CurrentSettings.SceneMaps.Count; i++)
            {
                _sceneMapDic.Add(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._sceneName, MMN_SettingsProvider.CurrentSettings.SceneMaps[i]);
            }
            if (_sceneMapDic.ContainsKey(_sceneName))
            {
                _sceneMapDic[_sceneName]._scenePath = _scenePath;
            }
            else
            {
                SceneMapSetting _newSetting = new SceneMapSetting();
                _newSetting._scenePath = _scenePath;
                _newSetting._sceneName = _sceneName;
                _newSetting._sceneTitle = _sceneName;
                _newSetting._maps = new List<MapSetting>();
                _newSetting._bottomLeft = new Vector3(-500F,-10F,-500F);
                _newSetting._topRight = new Vector3(500F, -10F, 500F);
                MapSetting _map = new MapSetting();
                _map._name = "Base";
                _map._fold = false;
                _map._map = SearchMap(_sceneName,0);
                _newSetting._maps.Add(_map);
                _newSetting._fold = false;
                MMN_SettingsProvider.CurrentSettings.SceneMaps.Add(_newSetting);
            }
        }



        private void SubTitleButton(string _text, ref bool _toggle)
        {
            _toggle = EditorGUILayout.Foldout(_toggle, _text,true);
        }


    }
}
