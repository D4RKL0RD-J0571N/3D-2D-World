using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Threading;
using UnityEngine.SceneManagement;
using Unity.EditorCoroutines.Editor;

namespace SoftKitty.MasterNavigationMap
{
    [CustomEditor(typeof(MapCreator))]
    public class MapCreator_Inspector : Editor
    {
        Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        Color _disableColor = new Color(0F, 0.1F, 0.3F);
        Color _titleColor = new Color(0.3F, 0.5F, 1F);
        Color _buttonColor = new Color(0F, 0.8F, 0.3F);
        GUIStyle _titleButtonStyle;
        bool _customExpand = false;

        private void RefreshMap()
        {
            MapCreator myTarget = (MapCreator)target;
            myTarget.CurrentMap = null;
            myTarget.Init();
        }
        public override void OnInspectorGUI()
        {
            
            GUI.changed = false;
            bool _valueChanged = false;
            _titleButtonStyle = new GUIStyle(GUI.skin.button);
            _titleButtonStyle.alignment = TextAnchor.MiddleLeft;
            Color _backgroundColor = GUI.backgroundColor;
            var script = MonoScript.FromScriptableObject(this);
            MapCreator myTarget = (MapCreator)target;

            string _thePath = AssetDatabase.GetAssetPath(script);
            _thePath = _thePath.Replace("MapCreator_Inspector.cs", "");
            string _packRootPath = _thePath.Replace("Editor/", "");
            Texture logoIcon = (Texture)AssetDatabase.LoadAssetAtPath(_thePath + "Logo.png", typeof(Texture));
            GUILayout.BeginHorizontal();
            GUILayout.Box(logoIcon);
            GUILayout.EndHorizontal();

            List<string> _layers = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if(layerName!="") _layers.Add(layerName);
            }
            string[] layers = _layers.ToArray();

            RefreshMap();

            GUILayout.BeginHorizontal();
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Open Map Setting", GUILayout.Width(210)))
            {
                SettingsService.OpenProjectSettings("Project/SoftKitty/Map Navigation");
            }
            GUI.backgroundColor = new Color(0.3F, 0.8F, 0.8F, 1F);
            if (GUILayout.Button(new GUIContent("Open Online Documentation", "Opens the official online documentation, including guides, node reference, and examples."), GUILayout.Width(210)))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/master-map-navigation/map-generator");
            }
            GUI.backgroundColor = _backgroundColor;
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            EditorUtils.TitleBox(EditorUtils.TextHint("Render Target"), EditorUtils._black, 0);
            GUILayout.BeginVertical(EditorUtils._groupStyle);

            if (myTarget.CurrentMap != null)
            {
                if (myTarget.CurrentMap._subMaps.Count > 0)
                {
                    bool _wasBakeForSubmap = myTarget.BakeForSubMap;
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    myTarget.BakeForSubMap = GUILayout.Toggle(myTarget.BakeForSubMap," Bake for Sub-Map ( total:"+ myTarget.CurrentMap._subMaps.Count.ToString() + " )");
                    GUILayout.EndHorizontal();
                    if (_wasBakeForSubmap!= myTarget.BakeForSubMap) {
                        myTarget.CurrentLayer = 0;
                        if (myTarget.BakeForSubMap)
                        {
                            myTarget.Lowest = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._height;
                            myTarget.Height= myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._ceil;
                        }
                        else
                        {
                            myTarget.Lowest = myTarget.CurrentMap._maps[myTarget.CurrentLayer]._height;
                            myTarget.Height = myTarget.CurrentMap._maps[myTarget.CurrentLayer]._ceil;
                        }
                    }

                    if (myTarget.BakeForSubMap) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        GUI.backgroundColor = _buttonColor;
                        if (GUILayout.Button(" < ", GUILayout.Width(50)))
                        {
                            if (myTarget.SubMapIndex > 0)
                                myTarget.SubMapIndex--;
                            else
                                myTarget.SubMapIndex = myTarget.CurrentMap._subMaps.Count - 1;
                            myTarget.CurrentLayer = 0;
                            myTarget.Lowest = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._height;
                            myTarget.Height = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._ceil;
                        }
                        GUILayout.Label(new GUIContent("(Sub-Map) " + myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._areaTitle, "Please adjust this setting in ( Project Settings > Master Map Navigation )"), GUILayout.Width(200));
                        if (GUILayout.Button(" > ", GUILayout.Width(50)))
                        {
                            if (myTarget.SubMapIndex < myTarget.CurrentMap._subMaps.Count - 1)
                                myTarget.SubMapIndex++;
                            else
                                myTarget.SubMapIndex = 0;
                            myTarget.CurrentLayer = 0;
                            myTarget.Lowest = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._height;
                            myTarget.Height = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._ceil;
                        }
                        GUI.backgroundColor = _backgroundColor;
                        GUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.Separator();

                if (myTarget.BakeForSubMap) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    GUI.backgroundColor = _titleColor;
                    if (GUILayout.Button(" < ", GUILayout.Width(50)))
                    {
                        if (myTarget.CurrentLayer > 0)
                            myTarget.CurrentLayer--;
                        else
                            myTarget.CurrentLayer = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps.Count - 1;

                        myTarget.Lowest = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._height;
                        myTarget.Height = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._ceil;
                    }
                    GUILayout.Label(new GUIContent("(Sub-Map) Map Layer " + (myTarget.CurrentLayer + 1).ToString() + "/" + myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps.Count, "Please adjust this setting in ( Project Settings > Master Map Navigation )"), GUILayout.Width(200));
                    if (GUILayout.Button(" > ", GUILayout.Width(50)))
                    {
                        if (myTarget.CurrentLayer < myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps.Count - 1)
                            myTarget.CurrentLayer++;
                        else
                            myTarget.CurrentLayer = 0;

                        myTarget.Lowest = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._height;
                        myTarget.Height = myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._ceil;
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                } else {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    GUI.backgroundColor = _titleColor;
                    if (GUILayout.Button(" < ", GUILayout.Width(50))) {
                        if (myTarget.CurrentLayer > 0)
                            myTarget.CurrentLayer--;
                        else
                            myTarget.CurrentLayer = myTarget.CurrentMap._maps.Count - 1;

                        myTarget.Lowest = myTarget.CurrentMap._maps[myTarget.CurrentLayer]._height;
                        myTarget.Height = myTarget.CurrentMap._maps[myTarget.CurrentLayer]._ceil;
                    }
                    GUILayout.Label(new GUIContent("Map Layer " + (myTarget.CurrentLayer + 1).ToString() + "/" + myTarget.CurrentMap._maps.Count, "Please adjust this setting in ( Project Settings > Master Map Navigation )"), GUILayout.Width(200));
                    if (GUILayout.Button(" > ", GUILayout.Width(50)))
                    {
                        if (myTarget.CurrentLayer < myTarget.CurrentMap._maps.Count - 1)
                            myTarget.CurrentLayer++;
                        else
                            myTarget.CurrentLayer = 0;

                        myTarget.Lowest = myTarget.CurrentMap._maps[myTarget.CurrentLayer]._height;
                        myTarget.Height = myTarget.CurrentMap._maps[myTarget.CurrentLayer]._ceil;
                    }
                    GUI.backgroundColor = _backgroundColor;
                    GUILayout.EndHorizontal();
                }
            }

            if (myTarget.CurrentLayer > 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                myTarget.UseBackGroundTexture = GUILayout.Toggle(myTarget.UseBackGroundTexture,"Use background texture.",GUILayout.Width(150));
                if (myTarget.UseBackGroundTexture)
                {
                    myTarget.BackGroundTexture = (Texture2D)EditorGUILayout.ObjectField(myTarget.BackGroundTexture,typeof(Texture2D),false,GUILayout.Width(100));
                    if (myTarget.BackGroundTexture != null)
                    {
                        TextureImporter _importer = (TextureImporter)TextureImporter.GetAtPath(AssetDatabase.GetAssetPath(myTarget.BackGroundTexture));
                        if (!_importer.isReadable || _importer.maxTextureSize!= GetSize(myTarget.Size))
                        {
                            _importer.isReadable = true;
                            _importer.maxTextureSize = GetSize(myTarget.Size);
                            _importer.SaveAndReimport();
                        }
                    }
                }
                
                GUILayout.EndHorizontal();

                
            }

            EditorGUILayout.Separator();
            GUILayout.EndVertical();
            EditorGUILayout.Separator();

            EditorUtils.TitleBox(EditorUtils.TextHint("Render Area"), EditorUtils._black, 0);
            GUILayout.BeginVertical(EditorUtils._groupStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(new GUIContent("Map Size Handler:","Move the below transforms to define the area inside the map."));
            GUILayout.EndHorizontal();

            if (myTarget.CurrentLayer == 0)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUI.backgroundColor = _titleColor;
                if (GUILayout.Button("Select Bottom Left"))
                {
                    Selection.activeObject = myTarget.BottomLeft.gameObject;
                }
                if (GUILayout.Button("Select Top Right"))
                {
                    Selection.activeObject = myTarget.TopRight.gameObject;
                }
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("You can only adjust the map size for the 1st layer.", MessageType.Warning,true);
                GUILayout.EndHorizontal();
            }

            GUI.backgroundColor = _backgroundColor;
            EditorGUILayout.Separator();

            if (myTarget.Height <= myTarget.Lowest + 50) myTarget.Height = myTarget.Lowest + 50;

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.Label(new GUIContent("Height Range:","Only objects within this height range will be rendered."),GUILayout.Width(150));
            EditorGUILayout.MinMaxSlider(ref myTarget.Lowest, ref myTarget.Height, -90F, 1000F);
            GUILayout.Label(myTarget.Lowest.ToString("00")+"m ~ "+ myTarget.Height.ToString("000")+"m", GUILayout.Width(100));
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();
            GUILayout.EndVertical();
            EditorGUILayout.Separator();

            EditorUtils.TitleBox(EditorUtils.TextHint("Render Settings"), EditorUtils._black, 0);
            GUILayout.BeginVertical(EditorUtils._groupStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Output Size:","Output map texture pixel size."), GUILayout.Width(150));
            GUI.backgroundColor = EditorUtils._black;
            myTarget.Size = (MapCreator.MapSize)EditorGUILayout.Popup((int)myTarget.Size, System.Enum.GetNames(typeof(MapCreator.MapSize)),GUILayout.Width(150));
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            EditorGUILayout.HelpBox("It is recommended to output smaller map first to test the settings.", MessageType.Info);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Layer Mask:","Only objects within this layer mask will be rendered."), GUILayout.Width(150));
            GUI.backgroundColor = EditorUtils._black;
            myTarget.GroundLayer = EditorGUILayout.MaskField(myTarget.GroundLayer, layers,GUILayout.Width(150));
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Pattern Texture:", "R:Flat Ground  G:Mountains  B:Water"), GUILayout.Width(150));
            myTarget.PatternTexture = (Texture2D)EditorGUILayout.ObjectField(myTarget.PatternTexture,typeof(Texture2D),false,GUILayout.Width(150));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Angle Threshold:", "The angle threshold for moutains, smaller value will make more areas defined as mountains."), GUILayout.Width(150));
            myTarget.MountainAngleThreshold = EditorGUILayout.Slider(myTarget.MountainAngleThreshold,5F,30F, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default",GUILayout.Width(50F))) myTarget.MountainAngleThreshold = 10F;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Step Height Threshold:", "Step height threshold for moutains, smaller value will make more areas defined as mountains."), GUILayout.Width(150));
            myTarget.MountainStepHeightThreshold = EditorGUILayout.Slider(myTarget.MountainStepHeightThreshold, 0.1F, 2F, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.MountainStepHeightThreshold = 0.3F;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Detail Intensity:","The intensity of small details."), GUILayout.Width(150));
            myTarget.GroundDetailIntensity = EditorGUILayout.Slider(myTarget.GroundDetailIntensity, 0F, 1F, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.GroundDetailIntensity = 0.3F;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("AO Intensity:", "The intensity of ambient occlusion."), GUILayout.Width(150));
            myTarget.AoIntensity = EditorGUILayout.Slider(myTarget.AoIntensity, 0F, 1F, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.AoIntensity = 0.5F;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Edge Intensity:", "The intensity of edge lines surrounding objects."), GUILayout.Width(150));
            myTarget.GroupEdgeIntensity = EditorGUILayout.Slider(myTarget.GroupEdgeIntensity, 0F, 1F, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.GroupEdgeIntensity = 0.3F;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label(new GUIContent("Sample Radius:","Larger value will result in more detailed image, but cost more time to render."), GUILayout.Width(150));
            myTarget.SampleRadius = EditorGUILayout.IntSlider(myTarget.SampleRadius, 10, 60, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.SampleRadius = 30;
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Flat Ground Color:", GUILayout.Width(150));
            myTarget.GroundColor=EditorGUILayout.ColorField (myTarget.GroundColor,GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.GroundColor = new Color(0.6666667F, 0.6078432F, 0.4862745F,1F);
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Mountains Color:", GUILayout.Width(150));
            myTarget.MountainColor = EditorGUILayout.ColorField(myTarget.MountainColor, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.MountainColor = new Color(0.5188679F, 0.4290185F, 0.331227F, 1F);
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Water Color:", GUILayout.Width(150));
            myTarget.WaterColor = EditorGUILayout.ColorField(myTarget.WaterColor, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.WaterColor = new Color(0.3843137F, 0.3882353F, 0.3568628F, 1F);
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Coast Border Color:", GUILayout.Width(150));
            myTarget.BorderColor = EditorGUILayout.ColorField(myTarget.BorderColor, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.BorderColor = new Color(0.2830189F, 0.269457F, 0.2260591F, 1F);
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("In Navmesh Multiply:", GUILayout.Width(150));
            myTarget.InNavmeshColor = EditorGUILayout.ColorField(myTarget.InNavmeshColor, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.InNavmeshColor = new Color(1F, 1F, 1F, 1F);
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("Out Navmesh Multiply:", GUILayout.Width(150));
            myTarget.OutsideNavmeshColor = EditorGUILayout.ColorField(myTarget.OutsideNavmeshColor, GUILayout.Width(150));
            GUI.backgroundColor = _titleColor;
            if (GUILayout.Button("Default", GUILayout.Width(50F))) myTarget.OutsideNavmeshColor = new Color(1F, 1F, 1F, 1F);
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            


            EditorUtils.TitleButton(new GUIContent("Custom Group Color", "Use custom groups to assign specified color to filtered objects."), ref _customExpand, 10, true, 0, false);

            if (_customExpand) {

                GUILayout.BeginHorizontal();
                GUILayout.Space(25);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(new GUIContent("Add Group","Add a new custom group."), GUILayout.Width(100)))
                {
                    MapCreatorTagData _newData = new MapCreatorTagData();
                    _newData._name = "New Group (" + (myTarget.CustomColors.Count + 1).ToString() + ")";
                    _newData._color = myTarget.GroundColor;
                    _newData._tag = "";
                    _newData._expand = false;
                     myTarget.CustomColors.Add(_newData);
                }
                GUI.backgroundColor = _backgroundColor;
                GUILayout.EndHorizontal();

                

                for (int i=0;i< myTarget.CustomColors.Count;i++) {
                    if (EditorUtils.TitleButton(EditorUtils.TextHint(myTarget.CustomColors[i]._name), ref myTarget.CustomColors[i]._expand,
                        10,false,10,true)){
                        myTarget.CustomColors.RemoveAt(i);
                        GUILayout.EndHorizontal();
                        return;
                    }
                   
                    if (myTarget.CustomColors[i]._expand) {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.Label(new GUIContent("Name:","Name of this group, this doesn't affect the filter."),GUILayout.Width(100));
                        myTarget.CustomColors[i]._name = GUILayout.TextArea(myTarget.CustomColors[i]._name);
                        GUILayout.Space(30);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.Label(new GUIContent("Color:", "Objects in this group will be displayed with this color on the map."), GUILayout.Width(100));
                        myTarget.CustomColors[i]._color = EditorGUILayout.ColorField(myTarget.CustomColors[i]._color);
                        GUILayout.Space(30);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.Label(new GUIContent("Layer Filter:","Filter objects with these layers."), GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._black;
                        myTarget.CustomColors[i]._layer = EditorGUILayout.MaskField(myTarget.CustomColors[i]._layer, layers);
                        EditorUtils.ResetColor();
                        GUILayout.Space(30);
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.Label(new GUIContent("Tag Filter:", "Filter objects with this tag."), GUILayout.Width(100));
                        myTarget.CustomColors[i]._tag = GUILayout.TextArea(myTarget.CustomColors[i]._tag);
                        GUILayout.Space(30);
                        GUILayout.EndHorizontal();

                      
                    }
                }

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(10);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            EditorGUILayout.Separator();

            EditorGUILayout.Separator();

            string _root = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
            string _mapPath = _packRootPath + "MapTextures/" + SceneManager.GetActiveScene().name+ (myTarget.BakeForSubMap? "_sub"+ myTarget.SubMapIndex.ToString() : "") + "_" + (myTarget.CurrentLayer + 1).ToString() + ".png";
            myTarget.FilePath = _root + _mapPath;

            EditorUtils.TitleBox(EditorUtils.TextHint("Output"), EditorUtils._black, 0);

            GUILayout.BeginVertical(EditorUtils._groupStyle);
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUI.backgroundColor = EditorUtils._buttonColor;
            if (GUILayout.Button(new GUIContent("Bake","Bake the map texture to the following path."),GUILayout.Width(100))) {
               BakeMap(_mapPath);
            }
            GUILayout.Space(10);
            myTarget.Preview = GUILayout.Toggle(myTarget.Preview ,"Progress Preview");
            GUI.backgroundColor = _backgroundColor;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.Box(_mapPath);
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();


            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            GUILayout.EndVertical();


           

            if (myTarget.CurrentMap != null)
            {
                if (myTarget.BakeForSubMap)
                {
                    myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._height = myTarget.Lowest;
                    myTarget.CurrentMap._subMaps[myTarget.SubMapIndex]._maps[myTarget.CurrentLayer]._ceil = myTarget.Height;
                }
                else
                {
                    myTarget.CurrentMap._maps[myTarget.CurrentLayer]._height = myTarget.Lowest;
                    myTarget.CurrentMap._maps[myTarget.CurrentLayer]._ceil = myTarget.Height;
                }
               
            }


            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            EditorUtils.TitleBox(EditorUtils.TextHint("References"), EditorUtils._black, 0);
            GUILayout.BeginVertical(EditorUtils._groupStyle);
            base.OnInspectorGUI();
            GUILayout.EndVertical();

            if ((_valueChanged || GUI.changed) && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(myTarget);
        }

        public void BakeMap( string _path)
        {
            MapCreator myTarget = (MapCreator)target;

            RenderPreviewWindow _previewWindow = EditorWindow.GetWindow<RenderPreviewWindow>(false, "Map Renderer", true);
            _previewWindow.minSize = new Vector2(myTarget.Preview ? 800 : 500, myTarget.Preview ? 800 : 50);
            _previewWindow.maxSize = new Vector2(myTarget.Preview ? 800 : 500, myTarget.Preview ? 800 : 50);
            _previewWindow.myTarget = myTarget;
            _previewWindow.Show();
            _previewWindow.StartBaking(_path);

        }


        private int GetSize(MapCreator.MapSize Size)
        {
            switch (Size)
            {
                case MapCreator.MapSize.Square_512: return 512;
                case MapCreator.MapSize.Square_1024: return 1024;
                case MapCreator.MapSize.Square_2048: return 2048;
                case MapCreator.MapSize.Square_4096: return 4096;
                case MapCreator.MapSize.Square_8192: return 8192;
            }
            return 1024;
        }



    }
}
