using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace SoftKitty.MasterNavigationMap
{
    [CustomEditor(typeof(MapPoint))]
    public class MapPoint_Inspector : Editor
    {
        private int _entranceId = 0;
        public static Texture logoIcon;
        private bool _fold = false;
        private string[] _layerNames=new string[1] { "Base" };
        private List<string> _subMaps = new List<string>();
        private int _layer = 0;
        private int _sceneId = -1;
        private List<string> _groups = new List<string>();
        private string _activeScene = "none";
        private Color _backgroundColor;
        private MapPoint myTarget;
        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            _backgroundColor = GUI.backgroundColor;
            myTarget = (MapPoint)target;

            if (logoIcon == null)
            {
                var script = MonoScript.FromScriptableObject(this);
                string _thePath = AssetDatabase.GetAssetPath(script);
                _thePath = _thePath.Replace("MapPoint_Inspector.cs", "");
                logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
            }

            GUILayout.BeginHorizontal();
            GUILayout.Box(logoIcon);
            GUILayout.EndHorizontal();

            EditorUtils.Document("master-map-navigation/map-point", false);

            if (EditorUtils.TitleButton(new GUIContent("Settings"), ref _fold, 0, false, 0, false))
            {
                _fold = !_fold;
            }

            if (_fold)
            {


                if (_activeScene != SceneManager.GetActiveScene().name)
                {
                    _activeScene = SceneManager.GetActiveScene().name;
                    _layer = 0;
                    _layerNames = new string[1] { "Base" };
                    _subMaps.Clear();
                    _sceneId = -1;
                    for (int i = 0; i < MMN_SettingsProvider.CurrentSettings.SceneMaps.Count; i++)
                    {
                        if (MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._sceneName == _activeScene)
                        {
                            _sceneId = i;
                            _layerNames = new string[MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count];
                            for (int u = 0; u < MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps.Count; u++)
                            {
                                _subMaps.Add(MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._subMaps[u].uid);
                            }
                            for (int u = 0; u < MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps.Count; u++)
                            {
                                _layerNames[u] = MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._name;
                                if (myTarget.transform.position.y > MMN_SettingsProvider.CurrentSettings.SceneMaps[i]._maps[u]._height) _layer = u;
                            }
                        }
                    }
                }

                if (_groups.Count != MMN_SettingsProvider.CurrentSettings.IconGroups.Count)
                {
                    _groups.Clear();
                    for (int i = 0; i < MMN_SettingsProvider.CurrentSettings.IconGroups.Count; i++) _groups.Add(MMN_SettingsProvider.CurrentSettings.IconGroups[i]._name);
                }

                if (myTarget.Data == null)
                {
                    myTarget.Data = new MapPointData();
                    myTarget.Data.Range = 0F;
                    myTarget.Data.Text = "";
                    myTarget.Data.TextOffset = Vector2.zero;
                    myTarget.Data.GroupId = 0;
                }


                GUILayout.BeginHorizontal();
                myTarget.Data.Visible = GUILayout.Toggle(myTarget.Data.Visible, "", GUILayout.Width(15));
                GUILayout.Label("Visible");
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button("Stick Ground", GUILayout.Width(120)))
                {
                    Vector3 _worldPos = myTarget.transform.position;
                    float _ceil = 500F;
                    if (_sceneId != -1)
                    {
                        _worldPos.y = MMN_SettingsProvider.CurrentSettings.SceneMaps[_sceneId]._maps[_layer]._height;
                        if (MMN_SettingsProvider.CurrentSettings.SceneMaps[_sceneId]._maps.Count > _layer + 1) _ceil = MMN_SettingsProvider.CurrentSettings.SceneMaps[_sceneId]._maps[_layer + 1]._height - _worldPos.y - 1F;
                    }
                    RaycastHit hit;
                    if (Physics.Raycast(_worldPos + Vector3.up * _ceil, Vector3.down, out hit, 2000F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                    {
                        _worldPos.y = hit.point.y;
                        myTarget.transform.position = _worldPos;
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();



                EditorGUILayout.Separator();

                GUI.backgroundColor = EditorUtils._titleColor;
                GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                if (_groups.Count > 0)
                {
                    GUILayout.Label("Icon Category:", GUILayout.Width(150));
                    int _selected = myTarget.Data.GroupId;
                    if (_selected < 0)
                    {
                        _selected = 0;
                        myTarget.Data.GroupId = _selected;
                    }
                    GUI.backgroundColor = EditorUtils._black;
                    _selected = EditorGUILayout.Popup(_selected, _groups.ToArray(), GUILayout.Width(150));
                    GUI.backgroundColor = _backgroundColor;
                    if (EditorGUI.EndChangeCheck())
                    {
                        myTarget.Data.GroupId = _selected;
                    }
                }
                else
                {
                    GUILayout.Box(EditorUtils.GetTexture(EditorIcon.Warning), GUILayout.Width(16));
                    GUILayout.Label("Please set up categories in:Project Settings>Master Map Navigation", GUILayout.Width(390));
                    GUI.backgroundColor = EditorUtils._actionColor;
                    if (GUILayout.Button("Open Setting", GUILayout.Width(100)))
                    {
                        SettingsService.OpenProjectSettings("Project/Master Map Navigation");
                    }
                    GUI.backgroundColor = _backgroundColor;
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();


                GUILayout.BeginVertical(EditorUtils._groupStyle);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Layer: " + _layerNames[_layer] + " (" + _layer + ")", GUILayout.Width(160));
                if (myTarget.EntranceOfLayer > -1)
                {
                    GUI.color = EditorUtils._buttonColor;
                    myTarget.EntranceOfLayer = Mathf.Clamp(myTarget.EntranceOfLayer, -1, _layerNames.Length - 1);
                    GUILayout.Label("Entrance of: " + _layerNames[myTarget.EntranceOfLayer] + " (" + myTarget.EntranceOfLayer + ")", GUILayout.Width(140));
                    GUI.color = Color.white;
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        myTarget.EntranceOfLayer = -1;
                    }
                    GUI.backgroundColor = _backgroundColor;
                }
                GUILayout.EndHorizontal();

                if (_sceneId > -1 && _layerNames.Length > 1)
                {
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = _entranceId == _layer ? Color.gray : EditorUtils._titleColor;
                    if (GUILayout.Button("Set as Entrance of Layer:", GUILayout.Width(160)))
                    {
                        if (_entranceId == _layer)
                        {
                            EditorUtility.DisplayDialog("Set as Entrance of Layer:" + _entranceId, "Sorry, you can not set this map point as entrance of its own layer.", "OK");
                        }
                        else
                        {
                            myTarget.EntranceOfLayer = _entranceId;
                        }
                    }
                    GUI.backgroundColor = EditorUtils._black;
                    _entranceId = EditorGUILayout.Popup(_entranceId, _layerNames, GUILayout.Width(140));
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                myTarget.Data.AlwaysKeepSize = GUILayout.Toggle(myTarget.Data.AlwaysKeepSize, "Always keep the same size on the map.", GUILayout.Width(300));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                myTarget.Data.AlwaysKeepSizeInNavigagtionBar = GUILayout.Toggle(myTarget.Data.AlwaysKeepSizeInNavigagtionBar, "Always keep the same size on the navigation bar.", GUILayout.Width(300));
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Mouse hover text:", GUILayout.Width(110));
                myTarget.Data.HintText = GUILayout.TextArea(myTarget.Data.HintText);
                GUILayout.Space(10);
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                if (myTarget.Data.Icon != null) myTarget.Data.DisplayIcon = true;
                myTarget.Data.DisplayIcon = GUILayout.Toggle(myTarget.Data.DisplayIcon, "Display Icon", GUILayout.Width(150));

                if (myTarget.Data.DisplayIcon)
                {
                    myTarget.Data.Icon = (Texture2D)EditorGUILayout.ObjectField(myTarget.Data.Icon, typeof(Texture2D), false, GUILayout.Width(150));
                }
                else
                {
                    myTarget.Data.Icon = null;
                }
                GUILayout.EndHorizontal();

                if (myTarget.Data.DisplayIcon)
                {

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    GUILayout.Label("Icon Size:", GUILayout.Width(110));
                    myTarget.Data.IconSize = EditorGUILayout.IntSlider(myTarget.Data.IconSize, 16, 128, GUILayout.Width(150));
                    GUILayout.Label("Pixels", GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    GUILayout.Label("Icon Offset:", GUILayout.Width(110));
                    myTarget.Data.IconOffset = EditorGUILayout.Vector2Field("", myTarget.Data.IconOffset, GUILayout.Width(150));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    myTarget.Data.ShowIconIn3dWorld = GUILayout.Toggle(myTarget.Data.ShowIconIn3dWorld, "Show Icon In 3d World.", GUILayout.Width(200));
                    GUILayout.EndHorizontal();

                    if (myTarget.Data.ShowIconIn3dWorld)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(60);
                        GUILayout.Label("Y Offset In 3d World:", GUILayout.Width(150));
                        myTarget.Data.IconOffsetIn3dWorld = EditorGUILayout.FloatField(myTarget.Data.IconOffsetIn3dWorld, GUILayout.Width(50));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(60);
                        GUILayout.Label("Visible Distance:", GUILayout.Width(150));
                        myTarget.Data.IconDisappearDistanceIn3dWorld = EditorGUILayout.Slider(myTarget.Data.IconDisappearDistanceIn3dWorld, 5F, 500F, GUILayout.Width(150));
                        GUILayout.Label("meters", GUILayout.Width(50));
                        GUILayout.EndHorizontal();
                    }
                }

                GUILayout.BeginHorizontal();
                bool _wasToggle = myTarget.Data.Text != "";

                bool _toggleText = GUILayout.Toggle(myTarget.Data.Text != "", "Display Text", GUILayout.Width(150));
                if (_wasToggle != _toggleText)
                {
                    if (!_toggleText)
                    {
                        myTarget.Data.Text = "";
                        myTarget.Data.DisplayTextInNavigationBar = false;
                        myTarget.Data.DisplayTextInWorldMap = false;
                        myTarget.Data.DisplayTextInMiniMap = false;
                        myTarget.Data.DisplayTextIn3dWorld = false;
                    }
                    else
                    {
                        myTarget.Data.Text = "Map Point Text";
                        myTarget.Data.TextColor = Color.white;
                    }
                }
                if (_toggleText)
                {
                    myTarget.Data.Text = GUILayout.TextField(myTarget.Data.Text, GUILayout.Width(150));
                    myTarget.Data.TextColor = EditorGUILayout.ColorField(myTarget.Data.TextColor, GUILayout.Width(50));
                }
                GUILayout.EndHorizontal();

                if (_toggleText)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    myTarget.Data.DisplayTextInWorldMap = GUILayout.Toggle(myTarget.Data.DisplayTextInWorldMap, "", GUILayout.Width(15));
                    GUILayout.Label("Display in World Map.", GUILayout.Width(200));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    myTarget.Data.DisplayTextInMiniMap = GUILayout.Toggle(myTarget.Data.DisplayTextInMiniMap, "", GUILayout.Width(15));
                    GUILayout.Label("Display in Mini Map.", GUILayout.Width(200));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    myTarget.Data.DisplayTextInNavigationBar = GUILayout.Toggle(myTarget.Data.DisplayTextInNavigationBar, "", GUILayout.Width(15));
                    GUILayout.Label("Display in Navigation Bar.", GUILayout.Width(200));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    myTarget.Data.DisplayTextIn3dWorld = GUILayout.Toggle(myTarget.Data.DisplayTextIn3dWorld, "", GUILayout.Width(15));
                    GUILayout.Label("Display in 3d World.", GUILayout.Width(200));
                    GUILayout.EndHorizontal();




                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    GUILayout.Label("Text Offset:", GUILayout.Width(110));
                    myTarget.Data.TextOffset = EditorGUILayout.Vector2Field("", myTarget.Data.TextOffset, GUILayout.Width(150));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(40);
                    GUILayout.Label("Font Size:", GUILayout.Width(110));
                    myTarget.Data.FontSize = EditorGUILayout.IntSlider(myTarget.Data.FontSize, 8, 32, GUILayout.Width(150));
                    GUILayout.EndHorizontal();
                }


                GUILayout.BeginHorizontal();
                _wasToggle = myTarget.Data.Range != 0F;
                bool _toggleRange = GUILayout.Toggle(myTarget.Data.Range != 0F, "Display Range Circle", GUILayout.Width(150));
                if (_wasToggle != _toggleRange)
                {
                    if (!_toggleRange)
                    {
                        myTarget.Data.Range = 0F;
                    }
                    else
                    {
                        myTarget.Data.Range = 10F;
                        myTarget.Data.RangeColor = Color.white;
                    }
                }
                if (_toggleRange)
                {
                    myTarget.Data.Range = EditorGUILayout.Slider(myTarget.Data.Range, 2F, 300F, GUILayout.Width(150));
                    myTarget.Data.RangeColor = EditorGUILayout.ColorField(myTarget.Data.RangeColor, GUILayout.Width(50));
                }
                GUILayout.EndHorizontal();

                if (!myTarget.Data.DisplayIcon && !_toggleText && !_toggleRange)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Box(EditorUtils.GetTexture(EditorIcon.Warning), GUILayout.Width(16));
                    GUILayout.Label("Please enable at least one option from above.", GUILayout.Width(390));
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Visible Distance In Navigation Bar:", GUILayout.Width(200));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Space(40);
                myTarget.Data.DisapperDistance = EditorGUILayout.Slider(myTarget.Data.DisapperDistance, 20F, 5000F, GUILayout.Width(250));
                GUILayout.Label("meters", GUILayout.Width(50));
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();

                if (MMN_SettingsProvider.CurrentSettings.MapMode == MMN_Settings.MapModes.StaticMap && _subMaps.Count > 0)
                {
                    GUILayout.BeginHorizontal();
                    myTarget.ClickToOpenSubMap = GUILayout.Toggle(myTarget.ClickToOpenSubMap, "Click to open sub-map");
                    GUILayout.EndHorizontal();

                    if (myTarget.ClickToOpenSubMap)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        int _submapIndex = 0;
                        if (_subMaps.Contains(myTarget.SubMapUid))
                        {
                            _submapIndex = _subMaps.IndexOf(myTarget.SubMapUid);
                        }
                        if (GUILayout.Button(" < ", GUILayout.Width(50), GUILayout.Height(17)))
                        {
                            if (_submapIndex > 0)
                                _submapIndex--;
                            else
                                _submapIndex = _subMaps.Count - 1;
                        }
                        GUILayout.Label(myTarget.SubMapUid, EditorUtils._centerStyle, GUILayout.Width(100), GUILayout.Height(17));
                        if (GUILayout.Button(" > ", GUILayout.Width(50), GUILayout.Height(17)))
                        {
                            if (_submapIndex < _subMaps.Count - 1)
                                _submapIndex++;
                            else
                                _submapIndex = 0;
                        }
                        myTarget.SubMapUid = _subMaps[_submapIndex];
                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        myTarget.SubMapUid = "";
                    }
                }
                else
                {
                    myTarget.ClickToOpenSubMap = false;
                }

                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                myTarget.Data.Poping = GUILayout.Toggle(myTarget.Data.Poping, "Poping");
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();
                EditorGUILayout.Separator();

                GUI.backgroundColor = EditorUtils._black;
                GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                GUILayout.Label("States: (" + myTarget.State.ToString() + ")", GUILayout.Width(130));
                GUI.backgroundColor = EditorUtils._titleColor;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add New State", GUILayout.Width(120)))
                {
                    List<Texture2D> _states = new List<Texture2D>();
                    _states.AddRange(myTarget.Data.StateIcons);
                    _states.Add(null);
                    myTarget.Data.StateIcons = _states.ToArray();
                }
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical(EditorUtils._groupStyle);
                for (int i = 0; i < myTarget.Data.StateIcons.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Label("State ID: " + i.ToString() + (i == 0 ? " (Default)" : ""), GUILayout.Width(130));
                    myTarget.Data.StateIcons[i] = (Texture2D)EditorGUILayout.ObjectField(myTarget.Data.StateIcons[i], typeof(Texture2D), false, GUILayout.Width(150));
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        List<Texture2D> _states = new List<Texture2D>();
                        _states.AddRange(myTarget.Data.StateIcons);
                        _states.RemoveAt(i);
                        myTarget.Data.StateIcons = _states.ToArray();
                        GUILayout.EndHorizontal();
                        return;
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                EditorGUILayout.Separator();


                EditorGUILayout.Separator();


                GUI.backgroundColor = EditorUtils._black;
                GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                GUILayout.Label("Scene View Gizmos Settings (Editor Only):");
                GUILayout.EndHorizontal();
                GUI.backgroundColor = EditorUtils._gray;
                GUILayout.BeginHorizontal(EditorUtils._groupStyle);
                myTarget.ShowDebugText = GUILayout.Toggle(myTarget.ShowDebugText, "Text", GUILayout.Width(100));
                myTarget.ShowDebugRange = GUILayout.Toggle(myTarget.ShowDebugRange, "Range", GUILayout.Width(100));
                GUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;

                if (GUI.changed && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(myTarget);
            }
        }
    }
}
