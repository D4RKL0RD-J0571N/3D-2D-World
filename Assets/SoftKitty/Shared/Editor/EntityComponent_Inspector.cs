using UnityEngine;
using UnityEditor;

namespace SoftKitty
{
    [CustomEditor(typeof(EntityComponent))]
    public class EntityComponent_Inspector : UnityEditor.Editor
    {

        public string oldHash = "";


        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            EntityComponent myTarget = (EntityComponent)target;
            EditorUtils.TitleLogo(EditorUtils.GetTexture(EditorIcon.EntityLogo), 0);
            if (GameManager.EntityManagerData == null)
            {
                EditorUtils.HelpInfo("You have no <color=#22FFAA>Entity Manager Data Object</color> assigned. Please assign one in <color=#22AAFF>General Data Settings</color>.\n" +
"You can create one by right-clicking inside a folder in the Project panel and selecting:\n" +
"'<color=#22AAFF>Create > SoftKitty > Data Objects > Entity Manager Data</color>'\n" +
"Then assign the created data object in:\n" +
"'<color=#22AAFF>ProjectSettings > SoftKitty > General Data Settings > Data > Add</color>'"
, 0);
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = EditorUtils._buttonColor;
                if (GUILayout.Button("Assign One"))
                {
                    SGD_SettingsProvider.CurrentSettings.data_expand = true;
                    SettingsService.OpenProjectSettings("Project/SoftKitty/Entity Manager");
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();
                return;
            } 
            else if (GameManager.AttributeData == null)
            {
                EditorUtils.Warnning("Attribute Setting Object is not assigned, You can create this object via the context menu in any Project folder:" +
                "<color=6688FF>Create ¡ú SoftKitty ¡ú Data Objects ¡ú Attribute Data</color>" +
                "You can assign the created asset to the database in: <color=6688FF>Project Settings ¡ú SoftKitty ¡ú Data Settings ¡ú Data</color>", 0);

                GUILayout.BeginHorizontal();
                GUI.backgroundColor = EditorUtils._buttonColor;
                if (GUILayout.Button("Assign One"))
                {
                    SGD_SettingsProvider.CurrentSettings.data_expand = true;
                    SettingsService.OpenProjectSettings("Project/SoftKitty/SubData - Attributes");
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();
                return;
            }

            EditorUtils.HelpInfo("This is the GameObject interface of an Entity. Please go to <color=#22AAFF>ProjectSettings > SoftKitty > Entity Manager</color> to manage entity data.", 0);

           

            GUI.backgroundColor = Color.gray*0.7F;
            GUILayout.BeginHorizontal(EditorUtils._titleBgStyle, GUILayout.Height(25));
            GUILayout.Label(EditorUtils.TextHint("Entity UID:", "Select an entity from the database."), GUILayout.Width(80)) ;
            GUI.backgroundColor = EditorUtils._titleColor;
            int _selEntity = GameManager.EntityManagerData.EntityUidList.IndexOf(myTarget.uid);
            if (SGD_Settings.isRuntime)
            {
                GUILayout.Box(myTarget.uid, GUILayout.Width(150));
                EditorUtils.ResetColor();
                EditorUtils.End(0);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                _selEntity = EditorGUILayout.Popup(_selEntity, GameManager.EntityManagerData.EntityUidList.ToArray(), GUILayout.Width(150));
                if ((EditorGUI.EndChangeCheck() || oldHash != GameManager.EntityManagerData.Hash) && _selEntity >= 0)
                {
                    myTarget.uid = GameManager.EntityManagerData.EntityUidList[_selEntity];
                    myTarget.RefreshData();
                    oldHash = GameManager.EntityManagerData.Hash;
                    _valueChanged = true;
                }
                if (GUILayout.Button(EditorUtils.TextHint("Manage", "Open the Entity Manager to edit this entity¡¯s data."), GUILayout.Width(100)))
                {
                    EditorGUI.FocusTextInControl(null);
                    if (myTarget.uid != "" && _selEntity >= 0)
                    {
                        GameManager.EntityManagerData.SearchType = 0;
                        GameManager.EntityManagerData.SearchText = myTarget.uid;
                        GameManager.EntityManagerData.GetEntity(myTarget.uid).uiFold = true;
                    }
                    SettingsService.OpenProjectSettings("Project/SoftKitty/Entity Manager");
                }
                EditorUtils.ResetColor();
                EditorUtils.End(0);

                if (_selEntity < 0) EditorUtils.Warnning("Please select an <color=#22AAFF>Entity Data</color> from the dropdown above.", 0);
            }
           
           
            

            if (myTarget.uid != "" && myTarget.mData != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUI.backgroundColor = EditorUtils._titleColor * 0.5F;
                GUILayout.Label(" ( id: "+ GameManager.EntityManagerData.GetId(myTarget.uid)+" )", EditorUtils._titleButtonStyle, GUILayout.Width(65), GUILayout.Height(20));
                GUI.backgroundColor = Color.white;
                EditorGUILayout.LabelField("You can access an Entity via UID or ID in code. ID is faster, while UID is more convenient.", EditorUtils._helpStyle);
                GUILayout.Space(20);
                GUILayout.EndHorizontal();

                if (myTarget.mData.MultipleInstances)
                {
                    EditorUtils.HelpInfo("This entity supports '<color=#22AAFF>Multiple Instances</color>', it allows you to have multiple <color=#22FFAA>EntityComponents</color> in the scene," +
                        "they will inherit the data from <color=#22AAFF>EntityManager</color>, but they don't write data back, think of <color=#22AAFF>Entity</color> as prefab, <color=#22FFAA>EntityComponent</color> as instantiation", 15);
                }

                EditorGUILayout.Separator();
                EditorGUILayout.Separator();

                GUILayout.BeginHorizontal();
                if (SGD_Settings.isRuntime)
                {
                    myTarget.mData.AvailableForInteraction=GUILayout.Toggle(myTarget.mData.AvailableForInteraction, new GUIContent("Available for Interaction", "Unavailable for interaction usually means the entity is dead."));
                }
                else
                {
                    bool _avaliable = myTarget.mData.AvailableForInteraction;
                    bool _oldValue = _avaliable;
                    _avaliable = GUILayout.Toggle(_avaliable, new GUIContent("Available for Interaction", "Unavailable for interaction usually means the entity is dead."));
                    if (_oldValue != _avaliable)
                    {
                        foreach (var obj in GameManager.EntityManagerData.EntityData.EntityList)
                        {
                            if (obj.uid == myTarget.uid)
                            {
                                obj.AvailableForInteraction = _avaliable;
                                EditorUtility.SetDirty(GameManager.EntityManagerData);
                                GameManager.EntityManagerData.GenerateUniqueHash();
                                AssetDatabase.SaveAssets();
                                myTarget.RefreshData();
                                break;
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                myTarget.Engage= GUILayout.Toggle(myTarget.Engage, new GUIContent("Engage", "Whether this entity is currently engaging with other entities."));
                GUILayout.EndHorizontal();

                if (myTarget.Engage) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(15);
                    GUI.backgroundColor = EditorUtils._titleColor * 0.5F;
                    GUILayout.Label(new GUIContent(" Engaging With: ", "The EntityComponent this entity is currently engaging with."), EditorUtils._titleButtonStyle, GUILayout.Width(110), GUILayout.Height(20));
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button(myTarget.EngagedEntity == null ? "None" : myTarget.EngagedEntity.uid, GUILayout.Width(150))) {
                        if (myTarget.EngagedEntity != null) Selection.activeGameObject = myTarget.EngagedEntity.gameObject;
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();
                EditorGUILayout.Separator();

                EditorUtils.TitleButton(EditorUtils.TextHint("Entity Data:", "The data of this entity. To modify it, click the 'Manage' button above."), ref myTarget.UiFold, 0, false);
                if (myTarget.UiFold && myTarget.uid != "" && _selEntity >= 0)
                {
                    EditorUtils.HelpInfo("The data below is read-only. To modify it, click the '<color=#22AAFF>Manage</color>' button next to the Entity UID. Refer to the API documentation for script access.", 0);

                    GUILayout.BeginVertical(EditorUtils._groupStyle);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Position: ", EditorUtils._boldStyle, GUILayout.Width(80));
                    GUILayout.Label(myTarget.mData.Position.ToString(), GUILayout.Width(130));
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this data to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        string vectorString = string.Format("Vector3({0},{1},{2})", myTarget.mData.Position.x.ToString(), myTarget.mData.Position.y.ToString(), myTarget.mData.Position.z.ToString());
                        GUIUtility.systemCopyBuffer = vectorString;
                        EditorUtils.ShowBoxNotification("Data Copied to Clipboard!");
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Forward: ", EditorUtils._boldStyle, GUILayout.Width(80));
                    GUILayout.Label(myTarget.mData.Forward.ToString(), GUILayout.Width(130));
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this data to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        string vectorString = string.Format("Vector3({0},{1},{2})", myTarget.mData.Forward.x.ToString(), myTarget.mData.Forward.y.ToString(), myTarget.mData.Forward.z.ToString());
                        GUIUtility.systemCopyBuffer = vectorString;
                        EditorUtils.ShowBoxNotification("Data Copied to Clipboard!");
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Scale: ", EditorUtils._boldStyle, GUILayout.Width(80));
                    GUILayout.Label(myTarget.mData.Scale.ToString(), GUILayout.Width(130));
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this data to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        string vectorString = string.Format("Vector3({0},{1},{2})", myTarget.mData.Scale.x.ToString(), myTarget.mData.Scale.y.ToString(), myTarget.mData.Scale.z.ToString());
                        GUIUtility.systemCopyBuffer = vectorString;
                        EditorUtils.ShowBoxNotification("Data Copied to Clipboard!");
                    }
                    GUILayout.EndHorizontal();

                    if (!SGD_Settings.isRuntime)
                    {
                        GUILayout.BeginHorizontal();
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button(new GUIContent("Update Transform Data to Database", "Update the selected transform data (Position | Forward | Scale) to the database."), GUILayout.Width(235)))
                        {
                            foreach (var obj in GameManager.EntityManagerData.EntityData.EntityList)
                            {
                                if (obj.uid == myTarget.uid)
                                {
                                    obj.Position = myTarget.transform.position;
                                    obj.Forward = myTarget.transform.forward;
                                    obj.Scale = myTarget.transform.localScale;
                                    EditorUtility.SetDirty(GameManager.EntityManagerData);
                                    GameManager.EntityManagerData.GenerateUniqueHash();
                                    AssetDatabase.SaveAssets();
                                    myTarget.RefreshData();
                                    EditorUtils.ShowBoxNotification("Selected transform data (Position | Forward | Scale) has been successfully updated to the database.");
                                    break;
                                }
                            }
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Tags: (" + myTarget.mData.Tags.Count.ToString() + ")", EditorUtils._boldStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    for (int i = 0; i < myTarget.mData.Tags.Count; i++)
                    {

                        GUI.backgroundColor = Color.green;
                        GUILayout.Box(myTarget.mData.Tags[i], EditorUtils._boxStyle, GUILayout.Width(70), GUILayout.Height(17));
                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this data to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            GUIUtility.systemCopyBuffer = myTarget.mData.Tags[i];
                            EditorUtils.ShowBoxNotification("Tag Copied to Clipboard!");
                        }
                        if ((i + 1) % 4 == 0 && i != 0)
                        {
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                        }
                        else
                        {
                            GUILayout.Space(5);
                        }
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Attributes: (" + myTarget.mData.Attributes.Count.ToString() + ")", EditorUtils._boldStyle);
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUI.backgroundColor = Color.black;
                    GUILayout.Box("Name", EditorUtils._boxStyle, GUILayout.Width(120), GUILayout.Height(17));
                    GUILayout.Box("UID", EditorUtils._boxStyle, GUILayout.Width(100), GUILayout.Height(17));
                    GUILayout.Box("ID", EditorUtils._boxStyle, GUILayout.Width(50), GUILayout.Height(17));
                    GUILayout.Box("Value", EditorUtils._boxStyle, GUILayout.Width(120), GUILayout.Height(17));
                    GUILayout.EndHorizontal();
                    
                    for (int i = 0; i < myTarget.mData.Attributes.Count; i++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        Attribute _att = GameManager.AttributeData.GetAttribute(myTarget.mData.Attributes[i].uid);
                        EditorUtils.ResetColor();
                        GUI.backgroundColor = Color.blue;
                        GUILayout.Box(_att.name, EditorUtils._boxStyle, GUILayout.Width(120), GUILayout.Height(17));
                        EditorUtils.ResetColor();
                        GUI.backgroundColor = Color.gray;
                        if (GUILayout.Button(_att.uid, GUILayout.Width(100), GUILayout.Height(17)))
                        {
                            GUIUtility.systemCopyBuffer = _att.uid;
                            EditorUtils.ShowBoxNotification("uid Copied to Clipboard!");
                        }
                        if (GUILayout.Button(_att.id.ToString(), GUILayout.Width(50), GUILayout.Height(17)))
                        {
                            GUIUtility.systemCopyBuffer = _att.id.ToString();
                            EditorUtils.ShowBoxNotification("id Copied to Clipboard!");
                        }
                        string _value = "";
                        if (_att.stringValue)
                        {
                            _value = myTarget.mData.Attributes[i].stringValue;
                            GUI.backgroundColor = Color.gray;
                        }
                        else
                        {
                            if (SGD_Settings.isRuntime)
                            {
                                float _change = myTarget.mData.GetAttributeFloat(myTarget.mData.Attributes[i].uid) - myTarget.mData.Attributes[i].floatValue;
                                _value = myTarget.mData.Attributes[i].floatValue.ToString()+ (_change!=0F?(_change>0F?" + ":" - ")+ _change .ToString(): "");
                                GUI.backgroundColor = _change!=0F? (_change>0F?EditorUtils._buttonColor: EditorUtils._red) : Color.white;
                            }
                            else
                            {
                                _value = myTarget.mData.Attributes[i].floatValue.ToString();
                                GUI.backgroundColor = Color.white;
                            }
                        }
                        
                        if (GUILayout.Button(_value, GUILayout.Width(120), GUILayout.Height(17)))
                        {
                            GUIUtility.systemCopyBuffer = _value;
                            EditorUtils.ShowBoxNotification("Value Copied to Clipboard!");
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Separator();

                    if (SGD_Settings.isRuntime)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("OTE: ", EditorUtils._boldStyle, GUILayout.Width(80));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUI.backgroundColor = Color.black;
                        GUILayout.Box("Name", EditorUtils._boxStyle, GUILayout.Width(120), GUILayout.Height(17));
                        GUILayout.Box("Category", EditorUtils._boxStyle, GUILayout.Width(100), GUILayout.Height(17));
                        GUILayout.Box("UID", EditorUtils._boxStyle, GUILayout.Width(70), GUILayout.Height(17));
                        GUILayout.Box("Layer", EditorUtils._boxStyle, GUILayout.Width(50), GUILayout.Height(17));
                        GUILayout.Box("Time", EditorUtils._boxStyle, GUILayout.Width(50), GUILayout.Height(17));
                        GUILayout.EndHorizontal();

                        foreach (var _data in myTarget.mData.GetOverTimeEffectList()) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            OverTimeEffect _setting = GameManager.GetOverTimeEffect(_data.uid);
                            GUI.backgroundColor = Color.white;
                            GUILayout.Box(_setting.icon, EditorUtils._toolButtonStyle, GUILayout.Width(20), GUILayout.Height(17));
                            GUI.backgroundColor = Color.blue;
                            GUILayout.Box(_setting.name, EditorUtils._boxStyle, GUILayout.Width(100), GUILayout.Height(17));
                            GUI.backgroundColor = Color.gray;
                            GUILayout.Box(_setting.category, EditorUtils._boxStyle, GUILayout.Width(100), GUILayout.Height(17));
                            GUI.backgroundColor = Color.green;
                            GUILayout.Box(_data.uid, EditorUtils._boxStyle, GUILayout.Width(70), GUILayout.Height(17));
                            GUI.backgroundColor = Color.gray;
                            GUILayout.Box(_data.layer.ToString(), EditorUtils._boxStyle, GUILayout.Width(50), GUILayout.Height(17));
                            GUILayout.Box(_data.timer.ToString("0.0"), EditorUtils._boxStyle, GUILayout.Width(50), GUILayout.Height(17));
                            GUI.backgroundColor = Color.white;
                            GUILayout.EndHorizontal();
                        }
                    }

                    
                        foreach (var module in EntityInspectorRegistry.Modules)
                        {
                            module.DrawRuntimeInspector(myTarget);
                        }
                   
                    GUILayout.EndVertical();

                }
            }

            EditorGUILayout.Separator();
            EditorUtils.Document("core/entities/EntityComponent");

            if ((_valueChanged || GUI.changed) && !Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(myTarget);
            }
        }
    }

}
