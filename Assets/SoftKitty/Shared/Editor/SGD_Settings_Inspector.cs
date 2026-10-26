using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace SoftKitty
{
    [CustomEditor(typeof(SGD_Settings))]
    public class SGD_Settings_Inspector : UnityEditor.Editor
    {

        private void ApplyAndSave(SGD_Settings scriptableObject)
        {
            EditorUtility.SetDirty(scriptableObject);
            AssetDatabase.SaveAssets();
        }
        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            GUIStyle header = new GUIStyle();
            header.fontStyle= FontStyle.Bold;
            header.normal.textColor = Color.white;
            header.alignment = TextAnchor.MiddleLeft;
            SGD_Settings myTarget = (SGD_Settings)target;
            EditorUtils.TitleLogo(EditorUtils.GetTexture(EditorIcon.DataLogo), 0);
            EditorGUILayout.Separator();
            EditorUtils.Document("core/general/SGD_Settings",true);
            EditorGUILayout.Separator();

            string[] debugLevelText = new string[4] {"None", "Errors Only", "Critical Only", "Full Logging" };
            EditorUtils.IntSlider(ref myTarget.DebugLevel, 0, 3, EditorUtils.TextHint("Debug Level (" + debugLevelText[myTarget.DebugLevel] + ")", "Controls the verbosity level of debug logs."),"", 6,200);

          

            EditorUtils.TitleButton(EditorUtils.TextHint("General"), ref myTarget.general_expand);
            if (myTarget.general_expand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.Pre(EditorUtils.TextHint("Custom Loader", "Custom loader used to load resources from Asset Bundles or Unity Addressables."), 10);
                myTarget.CustomLoader = (AssetLoader)EditorGUILayout.ObjectField(myTarget.CustomLoader, typeof(AssetLoader), false, GUILayout.Width(150));
                EditorUtils.End(0);

                EditorUtils.LayerMaskField(ref myTarget.GroundLayer, EditorUtils.TextHint("Ground LayerMask:", "Layers used to identify ground, walls, roofs, and terrain in the project."), 10);
                
                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(24);
                GUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Data", "Database objects used by SoftKitty packages."), ref myTarget.data_expand);
            if (myTarget.data_expand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.BeginVertical(EditorUtils._groupStyle);
                EditorUtils.ResetColor();
                EditorUtils.PreBox(EditorUtils.TextHint("Data Object List ("+ myTarget.DataObjects.Count+") :", "List of database objects used by SoftKitty packages."), 10, EditorUtils._titleColor);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button("Add",GUILayout.Width(100))) {
                    myTarget.DataObjects.Add(null);
                    EditorGUI.FocusTextInControl(null);
                    ApplyAndSave(myTarget);
                }
                EditorUtils.ResetColor();
                EditorUtils.End(10);

                List<string> _existingGuids = new List<string>();
                List<string> _existingTypes = new List<string>();
                for (int i = 0; i < myTarget.DataObjects.Count; i++)
                {
                    EditorUtils.Pre(EditorUtils.TextHint(myTarget.DataObjects[i]==null?"Empty Data": myTarget.DataObjects[i].DataName()), 30);
                    EditorGUI.BeginChangeCheck();
                    myTarget.DataObjects[i] = (DataObject)EditorGUILayout.ObjectField(myTarget.DataObjects[i], typeof(DataObject), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        myTarget.RefreshDatabase();
                    }
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(17)))
                    {
                        myTarget.DataObjects.RemoveAt(i);
                        EditorGUI.FocusTextInControl(null);
                        ApplyAndSave(myTarget);
                        myTarget.RefreshDatabase();
                        break;
                    }
                    EditorUtils.ResetColor();
                    EditorUtils.End(20);
                    if (myTarget.DataObjects[i] != null)
                    {
                        _existingGuids.Add(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(myTarget.DataObjects[i])));
                        _existingTypes.Add(myTarget.DataObjects[i].TypeString());
                    }
                }

                var filter = $"t:{typeof(DataObject).FullName}";
                var guids = AssetDatabase.FindAssets(filter);
                foreach (var _guid in guids)
                {
                    if (!_existingGuids.Contains(_guid))
                    {
                        var objpath = AssetDatabase.GUIDToAssetPath(_guid);
                        var obj= AssetDatabase.LoadAssetAtPath<DataObject>(objpath);
                        if (obj!=null && !_existingTypes.Contains(obj.TypeString())) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Box(EditorUtils.GetTexture(EditorIcon.Warning), EditorUtils._toolButtonStyle, GUILayout.Width(15), GUILayout.Height(15));
                            EditorGUILayout.LabelField(EditorUtils.TextHint("Found an unassigned data object in the project <<color=#22FFAA>" + obj.TypeString() + "</color>>: <color=#FF2222>" + obj.name + "</color>\n"+ objpath, objpath), EditorUtils._helpStyle);
                            GUI.backgroundColor = EditorUtils._red;
                            if (GUILayout.Button(new GUIContent("Assign", "Assign this data object."), GUILayout.Width(70)))
                            {
                                myTarget.DataObjects.Add(obj);
                                EditorGUI.FocusTextInControl(null);
                                myTarget.RefreshDatabase();
                                ApplyAndSave(myTarget);
                            }
                            EditorUtils.ResetColor();
                            EditorUtils.End(20);
                        }
                    }
                }

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(24);
                GUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }

            EditorUtils.TitleButton(EditorUtils.TextHint("Audio", "Audio settings used by SoftKitty packages."), ref myTarget.audio_expand);
            if (myTarget.audio_expand)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.FloatSlider(ref myTarget.VolumeMultiplier, 0F, 1F, EditorUtils.TextHint("Volume Multiplier:", "Global volume multiplier for sounds played by SoftKitty packages."), "", 10);
                EditorUtils.IntSlider(ref myTarget.AudioPriority, 0, 256, EditorUtils.TextHint("Audio Priority:", "Audio priority for sounds played by SoftKitty packages. Lower values indicate higher priority."), "", 10);
                
                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(24);
                GUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }

            if (GUI.changed && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(myTarget);
        }


        

       

    }
}
