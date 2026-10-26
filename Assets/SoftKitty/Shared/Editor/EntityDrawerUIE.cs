using UnityEditor;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    [CustomPropertyDrawer(typeof(Entity))]
    public class EntityDrawerUIE : PropertyDrawer
    {
        private int SelAtt = 0;
        public static List<string> CopiedTags = new List<string>();
        public static List<AttributeData> CopiedAttributes = new List<AttributeData>();
        public static bool OnlyCoreAtt = false;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            property.FindPropertyRelative("uiFold").boolValue = EditorGUILayout.Foldout(property.FindPropertyRelative("uiFold").boolValue, label);
            GUILayout.EndHorizontal();

            if (property.FindPropertyRelative("uiFold").boolValue) {
                EditorUtils.Pre(EditorUtils.TextHint("UID:", "A unique string identifier for this entity."), 50, 100);
                property.FindPropertyRelative("uid").stringValue = GUILayout.TextField(property.FindPropertyRelative("uid").stringValue, GUILayout.Height(17));
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(new GUIContent("Random", "Generate a random unique UID."),GUILayout.Width(70), GUILayout.Height(17))) {
                    string _uid = "";
                    for (int i=0;i<Random.Range(8,15);i++) {
                        int intValue = Random.Range(48,91); // ASCII value for 'A'
                        _uid += (char)intValue;
                    }
                    property.FindPropertyRelative("uid").stringValue = _uid.Replace(":","a").Replace(";", "b").Replace("<", "c").Replace(">", "d").Replace("?", "e");
                }
                GUI.backgroundColor = EditorUtils._buttonColor;
                if (GUILayout.Button(new GUIContent("Set as Player","Set this entity as player. Please make sure there is only one entity set as player across the whole game."), GUILayout.Width(100), GUILayout.Height(17)))
                {
                    property.FindPropertyRelative("uid").stringValue = "player";
                }
                GUI.backgroundColor = Color.white;
                EditorUtils.End(10);
               
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                property.FindPropertyRelative("AvaliableForInteractive").boolValue = GUILayout.Toggle(property.FindPropertyRelative("AvaliableForInteractive").boolValue, new GUIContent("Available for Interaction", "If disabled, the entity cannot be interacted with (commonly used for dead entities)."));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button(new GUIContent("Copy Selected Transform", "Copy Position, Forward, and Scale values from the selected transform."))) {
                    Transform transform = Selection.activeTransform;
                    if (transform != null)
                    {
                        property.FindPropertyRelative("Position").vector3Value = transform.position;
                        property.FindPropertyRelative("Forward").vector3Value = transform.forward;
                        property.FindPropertyRelative("Scale").vector3Value = transform.localScale;
                    }
                }
                if (GUILayout.Button(new GUIContent("Reset to Default", "Reset Position, Forward, and Scale to their default values.")))
                {
                    property.FindPropertyRelative("Position").vector3Value = Vector3.zero;
                    property.FindPropertyRelative("Forward").vector3Value = Vector3.forward;
                    property.FindPropertyRelative("Scale").vector3Value = Vector3.one;
                }
                GUILayout.Space(10);
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                EditorUtils.Pre(EditorUtils.TextHint("Position:", "Position of this entity."), 50, 110);
                property.FindPropertyRelative("Position").vector3Value = EditorGUILayout.Vector3Field("", property.FindPropertyRelative("Position").vector3Value, GUILayout.Width(200), GUILayout.Height(17));
                EditorUtils.End(10);

                EditorUtils.Pre(EditorUtils.TextHint("Forward Direction:", "Forward Direction of this entity."), 50, 110);
                property.FindPropertyRelative("Forward").vector3Value = EditorGUILayout.Vector3Field("", property.FindPropertyRelative("Forward").vector3Value, GUILayout.Width(200), GUILayout.Height(17));
                EditorUtils.End(10);

                EditorUtils.Pre(EditorUtils.TextHint("Scale:", "Local Scale of this entity."), 50, 110);
                property.FindPropertyRelative("Scale").vector3Value = EditorGUILayout.Vector3Field("", property.FindPropertyRelative("Scale").vector3Value, GUILayout.Width(200), GUILayout.Height(17));
                EditorUtils.End(10);

                EditorUtils.Pre(EditorUtils.TextHint("Tags: ("+ property.FindPropertyRelative("Tags").arraySize + ")", "Entity tags used for damage detection and other identification logic."), 50, 125);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(new GUIContent("Add","Add a new tag for this entity."), GUILayout.Width(80)))
                {
                    property.FindPropertyRelative("Tags").InsertArrayElementAtIndex(0);
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the tag list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    CopiedTags.Clear();
                    for (int i = 0; i < property.FindPropertyRelative("Tags").arraySize; i++) CopiedTags.Add(property.FindPropertyRelative("Tags").GetArrayElementAtIndex(i).stringValue);
                    EditorUtils.ShowBoxNotification("Tags Data Copied to Clipboard!");
                }
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Paste), "Paste the tag list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    if (CopiedTags.Count > 0)
                    {
                        property.FindPropertyRelative("Tags").ClearArray();
                        for (int i = CopiedTags.Count-1; i >=0 ; i--)
                        {
                            property.FindPropertyRelative("Tags").InsertArrayElementAtIndex(0);
                            property.FindPropertyRelative("Tags").GetArrayElementAtIndex(0).stringValue = CopiedTags[i];
                        }
                    }
                }
                EditorUtils.End(10);

                GUILayout.BeginHorizontal();
                GUILayout.Space(70);
                for (int i = 0; i < property.FindPropertyRelative("Tags").arraySize; i++)
                {
                   
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    property.FindPropertyRelative("Tags").GetArrayElementAtIndex(i).stringValue = GUILayout.TextField(property.FindPropertyRelative("Tags").GetArrayElementAtIndex(i).stringValue, GUILayout.Width(70), GUILayout.Height(17)).ToLower().Replace(" ","");
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button(new GUIContent("X","Remove this tag."), GUILayout.Width(20), GUILayout.Height(17))) {
                        property.FindPropertyRelative("Tags").DeleteArrayElementAtIndex(i);
                    }
                    if ((i+1)%3==0 && i!=0) {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                property.FindPropertyRelative("attFold").boolValue=  EditorGUILayout.Foldout(property.FindPropertyRelative("attFold").boolValue,EditorUtils.TextHint("Attributes: (" + property.FindPropertyRelative("Attributes").arraySize + ") (Upgrade Level: "+ property.FindPropertyRelative("AttributesUpgradeLevel").intValue + ")", "Attributes of this entity."));
                
                EditorUtils.End(10);

                GUILayout.BeginHorizontal();
                GUILayout.Space(70);
                bool _canDecrease = (property.FindPropertyRelative("AttributesUpgradeLevel").intValue > 0);

                //GUI.backgroundColor = _canDecrease ? EditorUtils._red : Color.gray;
                GUI.color = _canDecrease ? Color.white : Color.gray;
                if (GUILayout.Button(new GUIContent("-", "Decrease the Upgrade Level."), GUILayout.Width(50)) && _canDecrease)
                {
                    property.FindPropertyRelative("AttributesUpgradeLevel").intValue--;
                }
                GUI.color = Color.white;
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(new GUIContent("+", "Increase the Upgrade Level."), GUILayout.Width(50)))
                {
                    property.FindPropertyRelative("AttributesUpgradeLevel").intValue++;
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the Attribute list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    CopiedAttributes.Clear();
                    for (int i = 0; i < property.FindPropertyRelative("Attributes").arraySize; i++) CopiedAttributes.Add(
                      new AttributeData(property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("uid").stringValue,
                      property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("floatValue").floatValue,
                      property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("stringValue").stringValue)
                        );

                    EditorUtils.ShowBoxNotification("Attributes Data Copied to Clipboard!");
                }
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Paste), "Paste the Attribute list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    if (CopiedAttributes.Count > 0)
                    {
                        property.FindPropertyRelative("Attributes").ClearArray();
                        for (int i = CopiedAttributes.Count - 1; i >= 0; i--)
                        {
                            property.FindPropertyRelative("Attributes").InsertArrayElementAtIndex(0);
                            property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("uid").stringValue = CopiedAttributes[i].uid;
                            property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("stringValue").stringValue = CopiedAttributes[i].stringValue;
                            property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("floatValue").floatValue = CopiedAttributes[i].floatValue;
                        }
                    }
                }
                

                EditorUtils.End(10);

                if (property.FindPropertyRelative("attFold").boolValue)
                {
                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(70);
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    if (GUILayout.Button(new GUIContent("Fill Attributes from Database", "Populate the attribute list by copying all attributes from the database."), GUILayout.Width(180)))
                    {
                        for (int u = GameManager.AttributeData.AttributeList.Count - 1; u >= 0; u--)
                        {
                            bool _exist = false;
                            for (int i = 0; i < property.FindPropertyRelative("Attributes").arraySize; i++)
                            {
                                if (property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("uid").stringValue == GameManager.AttributeData.AttributeList[u].uid)
                                {
                                    _exist = true;
                                }
                            }
                            if (!_exist && (GameManager.AttributeData.AttributeList[u].coreStats || !OnlyCoreAtt))
                            {
                                property.FindPropertyRelative("Attributes").InsertArrayElementAtIndex(0);
                                property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("uid").stringValue = GameManager.AttributeData.AttributeList[u].uid;
                                if (GameManager.AttributeData.AttributeList[u].stringValue)
                                {
                                    property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("stringValue").stringValue = GameManager.AttributeData.AttributeList[u].defaultValue;
                                }
                                else
                                {
                                    float _default = 0F;
                                    float.TryParse(GameManager.AttributeData.AttributeList[u].defaultValue, out _default);
                                    property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("floatValue").floatValue = _default;
                                }
                            }
                        }
                    }

                    OnlyCoreAtt = GUILayout.Toggle(OnlyCoreAtt, new GUIContent("Only Core Attributes", "Only copy attributes marked as Core Attributes from the database."));
                    EditorUtils.ResetColor();
                    EditorUtils.End(10);

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(70);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    SelAtt = EditorGUILayout.Popup(SelAtt, GameManager.AttributeData.AttributesNames, GUILayout.Width(140), GUILayout.Height(17));
                    bool _duplicate = false;
                    for (int i = 0; i < property.FindPropertyRelative("Attributes").arraySize; i++)
                    {
                        if (property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("uid").stringValue == GameManager.AttributeData.AttributesUidArray[SelAtt])
                        {
                            _duplicate = true;
                            break;
                        }
                    }
                    GUI.backgroundColor = _duplicate ? Color.white : EditorUtils._titleColor;
                    GUI.color = _duplicate ? Color.gray : Color.white;
                    if (GUILayout.Button(new GUIContent("Add", "Add the selected attribute from the left dropdown box to this entity."), GUILayout.Width(80)) && !_duplicate)
                    {
                        property.FindPropertyRelative("Attributes").InsertArrayElementAtIndex(0);
                        property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("uid").stringValue = GameManager.AttributeData.AttributesUidArray[SelAtt];
                        property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("stringValue").stringValue = "";
                        property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(0).FindPropertyRelative("floatValue").floatValue = 0F;
                    }
                    GUI.backgroundColor = property.FindPropertyRelative("Attributes").arraySize > 0 ? EditorUtils._red : EditorUtils._black;
                    GUI.color = property.FindPropertyRelative("Attributes").arraySize > 0 ? Color.white : Color.gray;
                    if (GUILayout.Button(new GUIContent("Clear All", "Remove all attributes from this entity."), GUILayout.Width(120)))
                    {
                        if (EditorUtility.DisplayDialog("Remove all attributes", "Are you sure you want to remove all attributes from this entity?", "Confirm", "Cancel"))
                        {
                            property.FindPropertyRelative("Attributes").ClearArray();
                        }
                    }
                    GUI.color = Color.white;
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                    List<string> _uids = new List<string>();
                    List<int> _duplicateList = new List<int>();
                    for (int i = 0; i < property.FindPropertyRelative("Attributes").arraySize; i++)
                    {
                        string _uid = property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("uid").stringValue;
                        if (_uids.Contains(_uid))
                        {
                            _duplicateList.Add(i);
                            _duplicateList.Add(_uids.IndexOf(_uid));
                            _uids.Add(i.ToString());
                        }
                        else
                        {
                            _uids.Add(_uid);
                        }
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(70);
                    GUI.backgroundColor = Color.black;
                    GUILayout.Box(EditorUtils.TextHint("Attribute Name", "The display name of the attribute."),GUILayout.Width(160));
                    GUILayout.Box(EditorUtils.TextHint("UID", "String UID used to access this attribute in code. String UIDs are more convenient, while integer IDs offer better performance."), GUILayout.Width(70));
                    GUILayout.Box(EditorUtils.TextHint("ID", "Integer ID used to access this attribute in code. Integer IDs offer better performance, while string UIDs are more convenient."), GUILayout.Width(30));
                    GUILayout.Box(EditorUtils.TextHint("Value", "The value of the attribute."), GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                    EditorUtils.ResetColor();
                    for (int i = 0; i < property.FindPropertyRelative("Attributes").arraySize; i++)
                    {
                        string _uid = property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("uid").stringValue;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        bool _duplicateAtt = _duplicateList.Contains(i);
                        GUI.color = _duplicateAtt ? EditorUtils._red : Color.white;
                        int _index = GameManager.AttributeData.IndexOfAttributes(_uid);
                        _index = EditorGUILayout.Popup(Mathf.Max(0, _index), GameManager.AttributeData.AttributesNames, GUILayout.Width(140), GUILayout.Height(17));
                        property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("uid").stringValue = GameManager.AttributeData.AttributesUidArray[_index];
                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the script id of this Attributes."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            EditorGUI.FocusTextInControl(null);
                            GUIUtility.systemCopyBuffer = GameManager.AttributeData.GetId(_uid).ToString();
                            EditorUtils.ShowBoxNotification("script id of this dynamic variable copied!");
                        }
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxStyle, GUILayout.Width(70), GUILayout.Height(17));
                        GUILayout.Box(GameManager.AttributeData.GetId(_uid).ToString(), EditorUtils._boxStyle, GUILayout.Width(30), GUILayout.Height(17));
                        EditorUtils.ResetColor();

                        if (GameManager.AttributeData.GetAttribute(_uid).stringValue)
                        {
                            property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("stringValue").stringValue = EditorGUILayout.TextField(property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("stringValue").stringValue, GUILayout.Width(80), GUILayout.Height(17));
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button(new GUIContent("X", "Remove this attribute from the entity."), GUILayout.Width(20), GUILayout.Height(17)))
                            {
                                property.FindPropertyRelative("Attributes").DeleteArrayElementAtIndex(i);
                            }
                        }
                        else
                        {
                            property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("floatValue").floatValue = EditorGUILayout.FloatField(property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("floatValue").floatValue, GUILayout.Width(40), GUILayout.Height(17));
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button(new GUIContent("X", "Remove this attribute from the entity."), GUILayout.Width(20), GUILayout.Height(17)))
                            {
                                property.FindPropertyRelative("Attributes").DeleteArrayElementAtIndex(i);
                            }
                            if (!_duplicateAtt && property.FindPropertyRelative("AttributesUpgradeLevel").intValue > 0)
                            {
                                GUILayout.Label("(w/ upgrade: " + (property.FindPropertyRelative("Attributes").GetArrayElementAtIndex(i).FindPropertyRelative("floatValue").floatValue +
                                    GameManager.AttributeData.GetAttribute(_uid).upgradeIncrement
                                    * property.FindPropertyRelative("AttributesUpgradeLevel").intValue).ToString() + ")");
                            }
                        }
                        if (_duplicateAtt)
                        {
                            GUI.color = EditorUtils._red;
                            GUILayout.Label("Duplicated!");
                            GUI.color = Color.white;
                        }

                        GUI.backgroundColor = Color.white;
                        GUILayout.EndHorizontal();
                    }
                }

                EditorUtils.ResetColor();
                int _customCount= GameManager.EntityManagerData.CustomDataSetting.Count;
                GUILayout.BeginHorizontal();
                GUILayout.Space(50);
                property.FindPropertyRelative("customFold").boolValue = EditorGUILayout.Foldout(property.FindPropertyRelative("customFold").boolValue, EditorUtils.TextHint("Custom Data: (" + _customCount + ")", "Custom data of this entity."));
                GUILayout.EndHorizontal();

                if (property.FindPropertyRelative("customFold").boolValue) {
                    for (int i = 0; i < property.FindPropertyRelative("CustomFloat").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.Float, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid,EditorUtils._boxLeftStyle,GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("Float", EditorUtils._boxStyle, GUILayout.Width(80));
                        EditorUtils.ResetColor();
                        property.FindPropertyRelative("CustomFloat").GetArrayElementAtIndex(i).floatValue = EditorGUILayout.FloatField(property.FindPropertyRelative("CustomFloat").GetArrayElementAtIndex(i).floatValue,GUILayout.Width(50));
                        GUILayout.EndHorizontal();
                    }
                    for (int i = 0; i < property.FindPropertyRelative("CustomInt").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.Int, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("Int", EditorUtils._boxStyle, GUILayout.Width(80));
                        EditorUtils.ResetColor();
                        property.FindPropertyRelative("CustomInt").GetArrayElementAtIndex(i).intValue = EditorGUILayout.IntField(property.FindPropertyRelative("CustomInt").GetArrayElementAtIndex(i).intValue, GUILayout.Width(50));
                        GUILayout.EndHorizontal();
                    }
                    for (int i = 0; i < property.FindPropertyRelative("CustomBool").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.Bool, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("Bool", EditorUtils._boxStyle, GUILayout.Width(80));
                        EditorUtils.ResetColor();
                        property.FindPropertyRelative("CustomBool").GetArrayElementAtIndex(i).boolValue = EditorGUILayout.Toggle(property.FindPropertyRelative("CustomBool").GetArrayElementAtIndex(i).boolValue, GUILayout.Width(50));
                        GUILayout.EndHorizontal();
                    }
                    for (int i = 0; i < property.FindPropertyRelative("CustomString").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.String, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("String", EditorUtils._boxStyle, GUILayout.Width(80));
                        EditorUtils.ResetColor();
                        property.FindPropertyRelative("CustomString").GetArrayElementAtIndex(i).stringValue = EditorGUILayout.TextField(property.FindPropertyRelative("CustomString").GetArrayElementAtIndex(i).stringValue, GUILayout.Width(100));
                        GUILayout.EndHorizontal();
                    }
                    for (int i = 0; i < property.FindPropertyRelative("CustomIntList").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.IntList, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("Int List", EditorUtils._boxStyle, GUILayout.Width(80));
                        GUILayout.Label("(" + property.FindPropertyRelative("CustomIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize + ")", GUILayout.Width(25));
                        GUI.backgroundColor = EditorUtils._titleColor;
                        if (GUILayout.Button("+",GUILayout.Width(20))) {
                            property.FindPropertyRelative("CustomIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").InsertArrayElementAtIndex(0);
                            EditorGUI.FocusTextInControl(null);
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                        for (int u = 0; u < property.FindPropertyRelative("CustomIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize; u++)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(90);
                            GUILayout.Label("(" + u + ")", GUILayout.Width(25));
                            property.FindPropertyRelative("CustomIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).intValue =
                                EditorGUILayout.IntField(property.FindPropertyRelative("CustomIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).intValue,
                                GUILayout.Width(50)) ;
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(17))) {
                                property.FindPropertyRelative("CustomIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").DeleteArrayElementAtIndex(u);
                                EditorGUI.FocusTextInControl(null);
                            }
                            EditorUtils.ResetColor();
                            GUILayout.EndHorizontal();
                        }
                    }
                    for (int i = 0; i < property.FindPropertyRelative("CustomIdIntList").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.IdIntList, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("ID Int List", EditorUtils._boxStyle, GUILayout.Width(80));
                        GUILayout.Label("(" + property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize + ")", GUILayout.Width(25));
                        GUI.backgroundColor = EditorUtils._titleColor;
                        if (GUILayout.Button("+", GUILayout.Width(20)))
                        {
                            property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").InsertArrayElementAtIndex(0);
                            EditorGUI.FocusTextInControl(null);
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                        for (int u = 0; u < property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize; u++)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(90);
                            GUILayout.Label("ID:",GUILayout.Width(25));
                            property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("id").intValue =
                              EditorGUILayout.IntField(
                                property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("id").intValue,
                                GUILayout.Width(30));
                            GUILayout.Label("Value:", GUILayout.Width(40));
                            property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("value").intValue =
                              EditorGUILayout.IntField(
                                property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("value").intValue,
                                GUILayout.Width(50));
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Width(20)))
                            {
                                property.FindPropertyRelative("CustomIdIntList").GetArrayElementAtIndex(i).FindPropertyRelative("list").DeleteArrayElementAtIndex(u);
                                EditorGUI.FocusTextInControl(null);
                            }
                            EditorUtils.ResetColor();
                            GUILayout.EndHorizontal();
                        }
                    }
                    for (int i = 0; i < property.FindPropertyRelative("CustomIdFloatList").arraySize; i++)
                    {
                        string _uid = GameManager.EntityManagerData.GetCustomUid(CustomData.CustomTypes.IdFloatList, i);
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(70);
                        GUI.backgroundColor = EditorUtils._titleColor;
                        GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box("ID Float List", EditorUtils._boxStyle, GUILayout.Width(80));
                        GUILayout.Label("(" + property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize + ")", GUILayout.Width(25));
                        GUI.backgroundColor = EditorUtils._titleColor;
                        if (GUILayout.Button("+", GUILayout.Width(20)))
                        {
                            property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").InsertArrayElementAtIndex(0);
                            EditorGUI.FocusTextInControl(null);
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                        for (int u = 0; u < property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").arraySize; u++)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(90);
                            GUILayout.Label("ID:", GUILayout.Width(25));
                            property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("id").intValue =
                              EditorGUILayout.IntField(
                                property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("id").intValue,
                                GUILayout.Width(30));
                            GUILayout.Label("Value:", GUILayout.Width(40));
                            property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("value").floatValue =
                              EditorGUILayout.FloatField(
                                property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").GetArrayElementAtIndex(u).FindPropertyRelative("value").floatValue,
                                GUILayout.Width(50));
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Width(20)))
                            {
                                property.FindPropertyRelative("CustomIdFloatList").GetArrayElementAtIndex(i).FindPropertyRelative("list").DeleteArrayElementAtIndex(u);
                                EditorGUI.FocusTextInControl(null);
                            }
                            EditorUtils.ResetColor();
                            GUILayout.EndHorizontal();
                        }
                    }
                }

                EditorGUILayout.Separator();
               
            }

        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return 0;
        }
    }
}
