using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace SoftKitty
{
    [CustomEditor(typeof(EntityManagerObject))]
    public class EntityManagerObject_Inspector : UnityEditor.Editor
    {
        public CustomData.CustomTypes NewCustomType = CustomData.CustomTypes.Float;
        public string NewCustomUid = "Custom1";
        public string TagAdd = "";
        public string TagRemove= "";
        public string TagReplaceFrom = "";
        public string TagReplaceTo = "";
        public bool OnlyCoreStats = true;
        public int StatAddUid = 0;
        public int StatRemoveUid = 0;
        public int CusUid = 0;
        public float CusFloat = 0F;
        public int CusInt = 0;
        public bool CusBool = false;
        public string CusString = "";
        public int CusId = 0;
        public int SelAtt = 0;
        public static List<string> CopiedTags = new List<string>();
        public static List<AttributeData> CopiedAttributes = new List<AttributeData>();
        public static bool OnlyCoreAtt = false;

        private void ApplyAndSave(EntityManagerObject scriptableObject)
        {
            scriptableObject.GenerateUniqueHash();
            EditorUtility.SetDirty(scriptableObject);
        }

        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            EntityManagerObject myTarget = (EntityManagerObject)target;
            string _newHash = "";

            if (GameManager.AttributeData != null) _newHash = GameManager.AttributeData.Hash;
            if (myTarget.SearchOptions.Count == 0 || myTarget.SearchOptionHash != _newHash)
            {
                myTarget.SearchOptions.Clear();
                myTarget.SearchOptions.Add("uid");
                myTarget.SearchOptions.Add("Tag");
                if (GameManager.AttributeData != null)
                {
                    foreach (var obj in GameManager.AttributeData.AttributeList)
                    {
                        if(obj.stringValue) myTarget.SearchOptions.Add(obj.uid);
                    }
                    myTarget.SearchOptionHash = _newHash;
                }
                _valueChanged = true;
            }

            EditorUtils.TitleLogo(EditorUtils.GetTexture(EditorIcon.DataLogo),0);

            EditorUtils.PreBox(EditorUtils.TextHint("Data Hash:", "The data hash is a unique string representing the current state of the data. It changes automatically whenever any data is modified."), 6, Color.black, 70);
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.Box(myTarget.Hash, EditorUtils._boxLeftStyle);
            if (GUILayout.Button("Generate",GUILayout.Width(60))) {
                myTarget.GenerateUniqueHash();
            }
            EditorUtils.ResetColor();
            EditorUtils.End(0);

            EditorUtils.Document("core/entities/EntityManagerObject",true);
            EditorGUILayout.Separator();

            EditorUtils.Save();

            if (GameManager.AttributeData == null)
            {
                EditorUtils.Warnning("Attribute Setting Object is not assigned, You can create this object via the context menu in any Project folder:" +
                "<color=6688FF>Create ¡ú SoftKitty ¡ú Data Objects ¡ú Attribute Data</color>" +
                "You can assign the created asset to the database in: <color=6688FF>Project Settings ¡ú SoftKitty ¡ú Data Settings ¡ú Data</color>", 10);
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

            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            GUI.backgroundColor = myTarget.CustomFold ? EditorUtils._activeColor2 : EditorUtils._disableColor2;
            if (GUILayout.Button(EditorUtils.TextHint((myTarget.CustomFold ? "-" : "+") +" Custom Data Settings (" + myTarget.CustomDataSetting.Count + ")", "Manage custom data for entities, such as character customization, skills, and equipment."), EditorUtils._titleButtonStyle))
            {
                myTarget.CustomFold = !myTarget.CustomFold;
                EditorGUI.FocusTextInControl(null);
            }
            EditorUtils.ResetColor();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            if (myTarget.CustomFold) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(5);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.Label("New uid:", GUILayout.Width(60));
                bool _duplicated = false;
                foreach (var obj in myTarget.CustomDataSetting)
                {
                    if (obj.uid == NewCustomUid) _duplicated = true;
                }
                GUI.color = _duplicated ? Color.red : Color.white;
                GUI.backgroundColor = _duplicated?Color.red: EditorUtils._titleColor;
                NewCustomUid = EditorGUILayout.TextField(NewCustomUid,GUILayout.Width(100)).Replace(" ", "").ToLower();
                GUI.backgroundColor = EditorUtils._buttonColor;
                GUI.color = Color.white;
                NewCustomType = (CustomData.CustomTypes)EditorGUILayout.EnumPopup(NewCustomType, GUILayout.Width(100));
                
                GUI.backgroundColor = _duplicated? Color.gray : EditorUtils._titleColor;
                GUI.color = _duplicated ? Color.gray : Color.white;
                if (GUILayout.Button(EditorUtils.TextHint("Add", "Add new custom data to all entities."), GUILayout.Width(50)) && !_duplicated && !string.IsNullOrEmpty(NewCustomUid)) {
                    myTarget.AddCustomData(NewCustomUid, NewCustomType);
                    NewCustomUid = "";
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                if (_duplicated)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(80);
                    GUI.backgroundColor = Color.red;
                    GUI.color = Color.red;
                    EditorGUILayout.LabelField("Duplicated uid !", EditorUtils._helpStyle, GUILayout.Width(100));
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                }

                for (int i=0;i< myTarget.CustomDataSetting.Count;i++) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.Label("(uid)", GUILayout.Width(30));
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(myTarget.CustomDataSetting[i].uid, EditorUtils._boxLeftStyle, GUILayout.Width(145));
                    EditorUtils.ResetColor();
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the uid of this custom data."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        EditorGUI.FocusTextInControl(null);
                        GUIUtility.systemCopyBuffer = myTarget.CustomDataSetting[i].uid;
                        EditorUtils.ShowBoxNotification("uid of this custom data copied!");
                    }
                    GUI.color = Color.white;
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box(myTarget.CustomDataSetting[i].type.ToString(), EditorUtils._boxStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button("X",GUILayout.Width(20))) {
                        myTarget.RemoveCustomData(myTarget.CustomDataSetting[i].uid);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                }
                EditorGUILayout.Separator();
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUI.backgroundColor = EditorUtils._titleColor ;
                if (GUILayout.Button(EditorUtils.TextHint("Sync Custom Data Settings with All Entities.", "Sync custom data for all entities with the database. Removes non-existent entries and adds missing ones.")))
                {
                    foreach (var obj in myTarget.EntityData.EntityList)
                    {
                        SyncCustomData(obj,myTarget);
                    }
                    EditorGUI.FocusTextInControl(null);
                }
                EditorUtils.ResetColor();
                GUILayout.Space(80);
                GUILayout.EndHorizontal();


                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(23);
                GUILayout.EndHorizontal();
            }

        
            GUILayout.BeginHorizontal();
            GUILayout.Space(6);
            GUI.backgroundColor = myTarget.ToolFold ? EditorUtils._activeColor2 : EditorUtils._disableColor2;
            if (GUILayout.Button(EditorUtils.TextHint((myTarget.ToolFold ? "-" : "+") + " Batch Management Tools", "Tools for batch managing entities."), EditorUtils._titleButtonStyle))
            {
                myTarget.ToolFold = !myTarget.ToolFold;
                EditorGUI.FocusTextInControl(null);
            }
            EditorUtils.ResetColor();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            if (myTarget.ToolFold) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(5);
                GUILayout.BeginVertical(EditorUtils._groupStyle);

                EditorUtils.HelpInfo("Actions apply only to search results when a search filter is active.", 15);
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.Label(new GUIContent( "Tag Tools:", "Tools for batch managing entity tags."));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Add Tag", "Add the selected tag to all entities."), GUILayout.Width(150))) {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            if (!_entity.Tags.Contains(TagAdd)) _entity.Tags.Add(TagAdd);
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.Label(">>",GUILayout.Width(20));
                GUI.backgroundColor = EditorUtils._gray;
                TagAdd = EditorGUILayout.TextField(TagAdd, GUILayout.Width(70));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Remove Tag", "Remove the selected tag from all entities."), GUILayout.Width(150)))
                {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            if (isMatching(_entity, myTarget))
                            {
                                if (_entity.Tags.Contains(TagAdd)) _entity.Tags.Remove(TagRemove);
                            }
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.Label(">>", GUILayout.Width(20));
                GUI.backgroundColor = EditorUtils._gray;
                TagRemove = EditorGUILayout.TextField(TagRemove, GUILayout.Width(70));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Replace Tag", "Replace existing tags on all entities with the selected tag."), GUILayout.Width(150)))
                {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            for (int i = 0; i < _entity.Tags.Count; i++)
                            {
                                if (_entity.Tags[i] == TagReplaceFrom) _entity.Tags[i] = TagReplaceTo;
                            }
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.Label(">>", GUILayout.Width(20));
                GUI.backgroundColor = EditorUtils._gray;
                TagReplaceFrom = EditorGUILayout.TextField(TagReplaceFrom, GUILayout.Width(70));
                GUILayout.Label("to", GUILayout.Width(20));
                TagReplaceTo = EditorGUILayout.TextField(TagReplaceTo, GUILayout.Width(70));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.Label(new GUIContent( "Attributes:", "Tools for batch managing entity attributes."));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Fill Attributes", "Fill all available attributes for all entities."), GUILayout.Width(150)))
                {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            for (int i =0;i<GameManager.AttributeData.AttributeList.Count;i++)
                            {
                                if (GameManager.AttributeData.AttributeList[i].coreStats || !OnlyCoreStats)
                                {
                                    _entity.AddAttributeData(GameManager.AttributeData.AttributeList[i].uid);
                                }
                            }
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                EditorUtils.ResetColor();
                OnlyCoreStats = GUILayout.Toggle(OnlyCoreStats, EditorUtils.TextHint("Only Core Attributes", "Only fill attributes marked as Core Attributes."));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Add Attribute", "Add the selected attribute to all entities."), GUILayout.Width(150)))
                {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            _entity.AddAttributeData(GameManager.AttributeData.AttributesUidArray[StatAddUid]);
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.Label(">>", GUILayout.Width(20));
                GUI.backgroundColor = EditorUtils._gray;
                StatAddUid = EditorGUILayout.Popup(StatAddUid,GameManager.AttributeData.AttributesNames, GUILayout.Width(150));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Remove Attribute", "Remove the selected attribute from all entities."), GUILayout.Width(150)))
                {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            _entity.RemoveAttributeData(GameManager.AttributeData.AttributesUidArray[StatRemoveUid]);
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.Label(">>", GUILayout.Width(20));
                GUI.backgroundColor = EditorUtils._gray;
                StatRemoveUid = EditorGUILayout.Popup(StatRemoveUid, GameManager.AttributeData.AttributesNames, GUILayout.Width(150));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Clear Attributes", "Remove all attributes from all entities."), GUILayout.Width(150)))
                {
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget))
                        {
                            _entity.Attributes.Clear();
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.Label(new GUIContent("Custom Data:", "Tools for batch managing entity custom data."));
                GUILayout.EndHorizontal();

                string _selUid = myTarget.CustomDataUidArray.Length>0?myTarget.CustomDataUidArray[CusUid]:"";
                CustomData.CustomTypes _selType = myTarget.GetCustomType(_selUid);
                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                if (GUILayout.Button(EditorUtils.TextHint("Batch Set Value", "Set the value of the selected custom data for all entities."), GUILayout.Width(150)))
                {
                    int _selIndex = myTarget.GetCustomIndex(_selUid);
                    foreach (var _entity in myTarget.EntityData.EntityList)
                    {
                        if (isMatching(_entity, myTarget)){
                            switch (_selType)
                            {
                                case CustomData.CustomTypes.Float:
                                    if(_selIndex>=0 && _selIndex< _entity.CustomFloat.Count) _entity.CustomFloat[_selIndex] = CusFloat;
                                    break;
                                case CustomData.CustomTypes.Int:
                                    if (_selIndex >= 0 && _selIndex < _entity.CustomInt.Count) _entity.CustomInt[_selIndex] = CusInt;
                                    break;
                                case CustomData.CustomTypes.Bool:
                                    if (_selIndex >= 0 && _selIndex < _entity.CustomBool.Count) _entity.CustomBool[_selIndex] = CusBool;
                                    break;
                                case CustomData.CustomTypes.String:
                                    if (_selIndex >= 0 && _selIndex < _entity.CustomString.Count) _entity.CustomString[_selIndex] = CusString;
                                    break;
                                case CustomData.CustomTypes.IntList:
                                    if (_selIndex >= 0 && _selIndex < _entity.CustomIntList.Count) _entity.CustomIntList[_selIndex].SetValueByIndex(CusId,CusInt);
                                    break;
                                case CustomData.CustomTypes.IdIntList:
                                    if (_selIndex >= 0 && _selIndex < _entity.CustomIdIntList.Count) _entity.CustomIdIntList[_selIndex].SetValueById(CusId, CusInt);
                                    break;
                                case CustomData.CustomTypes.IdFloatList:
                                    if (_selIndex >= 0 && _selIndex < _entity.CustomIdFloatList.Count) _entity.CustomIdFloatList[_selIndex].SetValueById(CusId, CusFloat);
                                    break;
                            }
                        }
                    }
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                GUILayout.Label(">>", GUILayout.Width(20));
                GUI.backgroundColor = EditorUtils._gray;
                CusUid = EditorGUILayout.Popup(CusUid, myTarget.CustomDataUidArray, GUILayout.Width(150));
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();
                
                
                GUILayout.BeginHorizontal();
                GUILayout.Space(55);
                GUILayout.Label("<"+ _selType.ToString() + "> New value:", GUILayout.Width(150));
                switch (_selType) {
                    case CustomData.CustomTypes.Float:
                        CusFloat = EditorGUILayout.FloatField(CusFloat,GUILayout.Width(50));
                        break;
                    case CustomData.CustomTypes.Int:
                        CusInt = EditorGUILayout.IntField(CusInt, GUILayout.Width(50));
                        break;
                    case CustomData.CustomTypes.Bool:
                        CusBool = EditorGUILayout.Toggle(CusBool, GUILayout.Width(50));
                        break;
                    case CustomData.CustomTypes.String:
                        CusString = EditorGUILayout.TextField(CusString, GUILayout.Width(150));
                        break;
                    case CustomData.CustomTypes.IntList:
                        GUILayout.Label("idx:", GUILayout.Width(25));
                        CusId = Mathf.Max(0, EditorGUILayout.IntField(CusId, GUILayout.Width(30)));
                        GUILayout.Label("value:", GUILayout.Width(35));
                        CusInt = EditorGUILayout.IntField(CusInt, GUILayout.Width(45));
                        break;
                    case CustomData.CustomTypes.IdIntList:
                        GUILayout.Label("id:", GUILayout.Width(25));
                        CusId = Mathf.Max(0, EditorGUILayout.IntField(CusId, GUILayout.Width(30)));
                        GUILayout.Label("value:", GUILayout.Width(35));
                        CusInt = EditorGUILayout.IntField(CusInt, GUILayout.Width(45));
                        break;
                    case CustomData.CustomTypes.IdFloatList:
                        GUILayout.Label("id:", GUILayout.Width(25));
                        CusId = Mathf.Max(0, EditorGUILayout.IntField(CusId, GUILayout.Width(30)));
                        GUILayout.Label("value:", GUILayout.Width(35));
                        CusFloat = EditorGUILayout.FloatField(CusFloat, GUILayout.Width(45));
                        break;
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.Separator();
                GUILayout.EndVertical();
                GUILayout.Space(23);
                GUILayout.EndHorizontal();
            }

            EditorGUILayout.Separator();
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(25);
            GUILayout.Label("Search:", EditorUtils._boxLeftStyle, GUILayout.Width(50));
            GUI.backgroundColor = EditorUtils._titleColor;
            myTarget.SearchType = EditorGUILayout.Popup(myTarget.SearchType, myTarget.SearchOptions.ToArray(),GUILayout.Width(100));
            GUI.backgroundColor = string.IsNullOrEmpty(myTarget.SearchText) ? EditorUtils._gray : new Color(1F,1F,0F);
            if(!string.IsNullOrEmpty(myTarget.SearchText)) GUI.color= new Color(1F, 0.5F, 0F);
            myTarget.SearchText = EditorGUILayout.TextField(myTarget.SearchText);
            EditorUtils.ResetColor();
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("X", GUILayout.Width(20))){
                myTarget.SearchText = "";
                EditorGUI.FocusTextInControl(null);
                _valueChanged = true;
            }
            EditorUtils.ResetColor();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

           
            EditorGUILayout.Separator();


            GUILayout.BeginHorizontal();
            GUILayout.Space(25);
            GUI.backgroundColor = EditorUtils._buttonColor;
            if (GUILayout.Button(new GUIContent("Add New [Entity]", "Create a new Entity."), GUILayout.Width(120)))
            {
                myTarget.NewEntity();
                _valueChanged = true;
                EditorGUI.FocusTextInControl(null);
                _valueChanged = true;
            }
            EditorUtils.ResetColor();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = EditorUtils._tagColor;
            if (GUILayout.Button("Expand All", GUILayout.Width(80)))
            {
                for (int i = 0; i < myTarget.EntityData.EntityList.Count; i++) myTarget.EntityData.EntityList[i].uiFold = true;
            }

            if (GUILayout.Button("Fold All", GUILayout.Width(80)))
            {
                for (int i = 0; i < myTarget.EntityData.EntityList.Count; i++) myTarget.EntityData.EntityList[i].uiFold = false;
            }
            GUILayout.Space(20);
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(myTarget.SearchText))
            {
                EditorUtils.Label(EditorUtils.TextHint("Search results of [" + myTarget.SearchOptions[myTarget.SearchType] + "] matching: '<color=#FF7733>" + myTarget.SearchText +"</color>' ..."),20 );
            }

            List<string> _uids = new List<string>();
            for (int i = 0; i < myTarget.EntityData.EntityList.Count; i++)
            {
                bool _duplicateUid = _uids.Contains(myTarget.EntityData.EntityList[i].uid);
                if (!_duplicateUid) _uids.Add(myTarget.EntityData.EntityList[i].uid);

                if (isMatching(myTarget.EntityData.EntityList[i], myTarget)) {
                    GUILayout.BeginHorizontal();
                    GUI.backgroundColor = myTarget.EntityData.EntityList[i].uiFold ? EditorUtils._activeColor2 : EditorUtils._disableColor2;
                    GUILayout.Label(myTarget.EntityData.EntityList[i].uiFold ? "[-]" : "[+]", GUILayout.Width(20));
                    if (_duplicateUid)
                    {
                        GUI.backgroundColor = Color.white;
                        GUILayout.Box(EditorUtils.GetTexture(EditorIcon.Warning), EditorUtils._iconStyle, GUILayout.Width(16), GUILayout.Height(16));
                        GUI.backgroundColor = Color.red;
                    }
                    int _id = myTarget.EntityData.EntityList[i].id;
                    string _key = myTarget.EntityData.EntityList[i].uid;
                    GUILayout.BeginHorizontal(EditorUtils._titleBgStyle, GUILayout.Height(25));
                    if (GUILayout.Button(new GUIContent(_key + "  ( ID: " + _id + " )", "Click to expand"), EditorUtils._titleButtonStyle))
                    {
                        myTarget.EntityData.EntityList[i].uiFold = !myTarget.EntityData.EntityList[i].uiFold;
                        EditorGUI.FocusTextInControl(null);
                    }
                    GUI.color = Color.white;
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Help), "Opens the official online documentation, including guides, node reference, and examples."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        Application.OpenURL("https://www.soft-kitty.com/docs/core/entities/Entity");
                        EditorGUI.FocusTextInControl(null);
                    }
                    EditorUtils.ResetColor();
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Duplicate), "Duplicate this entity."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        Entity _newEntity = myTarget.EntityData.EntityList[i].Copy();
                        string _base = _newEntity.uid + "_copy";
                        int _maxId = 0;
                        foreach (var obj in myTarget.EntityData.EntityList) {
                            if (obj.uid.Contains(_base))
                            {
                                int _testId = -1;
                                if (int.TryParse(obj.uid.Replace(_base, ""), out _testId))_maxId = Mathf.Max(_maxId, _testId);
                            }
                        }
                        _newEntity.uid = _base +(_maxId+1).ToString();
                        myTarget.EntityData.EntityList.Add(_newEntity);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    EditorUtils.ResetColor();
                    GUI.color = i > 0 ? Color.white : Color.gray;
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Up), "Move this Entity up."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        if (i > 0)
                        {
                            Entity _mEntity = myTarget.EntityData.EntityList[i].Copy();
                            _mEntity.uiFold = false;
                            myTarget.EntityData.EntityList.RemoveAt(i);
                            myTarget.EntityData.EntityList.Insert(i - 1, _mEntity);
                            EditorGUI.FocusTextInControl(null);
                            ApplyAndSave(myTarget);
                            return;
                        }
                    }
                    GUI.color = i < myTarget.EntityData.EntityList.Count - 1 ? Color.white : Color.gray;
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Down), "Move this Entity down."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        if (i < myTarget.EntityData.EntityList.Count - 1)
                        {
                            Entity _mEntity = myTarget.EntityData.EntityList[i].Copy();
                            _mEntity.uiFold = false;
                            myTarget.EntityData.EntityList.RemoveAt(i);
                            myTarget.EntityData.EntityList.Insert(i + 1, _mEntity);
                            EditorGUI.FocusTextInControl(null);
                            ApplyAndSave(myTarget);
                            return;
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUI.color = Color.white;

                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button(new GUIContent("X", "Delete this Entity."), GUILayout.Width(20)))
                    {
                        myTarget.EntityData.EntityList.RemoveAt(i);
                        myTarget.IdManager.RemoveKey(_id);
                        EditorGUI.FocusTextInControl(null);
                        ApplyAndSave(myTarget);
                        return;
                    }


                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();

                    if (myTarget.EntityData.EntityList[i].uiFold)
                    {
                        if (_duplicateUid) EditorUtils.Warnning("Duplicated UID! Please make every UID unique.", 70);

                        EditorGUI.BeginChangeCheck();
                        if (DrawEntityInterface(myTarget.EntityData.EntityList[i], myTarget)) _valueChanged = true;
                        if (_key != myTarget.EntityData.EntityList[i].uid)
                        {
                            myTarget.IdManager.ReplaceKey(_id, myTarget.EntityData.EntityList[i].uid);
                        }
                        if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();
                    }
                }
            }

           
            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = myTarget.IdManager.uiFold ? EditorUtils._titleColor : EditorUtils._gray * 0.6F;
            if (GUILayout.Button(EditorUtils.TextHint("ID List Count: " + myTarget.IdManager.idToKey.Count, "An auto-generated list of unique integer IDs. Using integer IDs improves performance."), GUILayout.Width(120)))
            {
                myTarget.IdManager.uiFold = !myTarget.IdManager.uiFold;
            }
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Rebuild", GUILayout.Width(80)))
            {
                if (EditorUtility.DisplayDialog("Warning!", "Rebuilding the ID list will break the mapping between string UIDs and integer IDs. Any code accessing attributes by integer ID will stop working.", "I'm Aware", "Cancel"))
                {
                    myTarget.IdManager.idToKey.Clear();
                }
            }
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();
            if (myTarget.IdManager.uiFold)
            {
                foreach (var obj in myTarget.IdManager.idToKey)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Box(" " + obj.key, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUILayout.Box(" " + obj.id.ToString(), EditorUtils._boxLeftStyle, GUILayout.Width(80));
                    GUILayout.EndHorizontal();
                }
            }


            if ((_valueChanged || GUI.changed) && !Application.isPlaying)
            {
                ApplyAndSave(myTarget);
            }
        }


        private bool DrawEntityInterface(Entity _entity, EntityManagerObject _myTarget)
        {
            bool _valueChanged = false;
            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.BeginVertical(EditorUtils._groupStyle);
            EditorUtils.Pre(EditorUtils.TextHint("UID:", "A unique string identifier for this entity."), 10, 40);
            string _oldUid = _entity.uid;
            _entity.uid = GUILayout.TextField(_entity.uid, GUILayout.Height(17)).Replace(" ","");
            if (_oldUid != _entity.uid)
            {
                _myTarget.IdManager.ReplaceKey(_oldUid, _entity.uid);
            }
            EditorUtils.ResetColor();
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the uid of this entity."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                EditorGUI.FocusTextInControl(null);
                GUIUtility.systemCopyBuffer = _entity.uid;
                EditorUtils.ShowBoxNotification("uid of this entity copied!");
            }
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button(new GUIContent("Random", "Generate a random unique UID."), GUILayout.Width(70), GUILayout.Height(17)))
            {
                string _uid = "";
                for (int i = 0; i < Random.Range(8, 15); i++)
                {
                    int intValue = Random.Range(48, 91); // ASCII value for 'A'
                    _uid += (char)intValue;
                }
                _entity.uid = _uid.Replace(":", "a").Replace(";", "b").Replace("<", "c").Replace(">", "d").Replace("?", "e");
                _valueChanged = true;
            }
            GUI.backgroundColor = EditorUtils._buttonColor;
            if (GUILayout.Button(new GUIContent("Set as Player", "Set this entity as the player. Ensure only one entity is marked as the player in the entire game."), GUILayout.Width(90), GUILayout.Height(17)))
            {
                _entity.uid = "player";
                _valueChanged = true;
            }
            GUI.backgroundColor = Color.white;
            EditorUtils.End(10);

            EditorUtils.Pre(EditorUtils.TextHint("ID:", "The unique int id of this entity."), 10, 40);
            GUI.backgroundColor = EditorUtils._buttonColor;
            GUILayout.Box(EditorUtils.TextHint(_entity.id.ToString(), "This unique integer ID is auto-generated. Using integer IDs provides better performance than string UIDs"), EditorUtils._boxLeftStyle);
            EditorUtils.ResetColor();
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the int id of this entity."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                EditorGUI.FocusTextInControl(null);
                GUIUtility.systemCopyBuffer = _entity.id.ToString();
                EditorUtils.ShowBoxNotification("int id of this Entity copied!");
            }
            EditorUtils.End(10);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            _entity.MultipleInstances = GUILayout.Toggle(_entity.MultipleInstances, new GUIContent("Multiple Instances", "If enabled, this Entity will no longer be one to one connection with EntityComponent, it will allow multiple EntityComponents inherit this data instead, but they don't write data back, think of this Entity as prefab,EntityComponent as instantiation."));
            GUILayout.EndHorizontal();

            if (_entity.MultipleInstances)
            {
                EditorUtils.HelpInfo("This entity is now '<color=#22AAFF>Multiple Instances</color>', it allows you to have multiple <color=#22FFAA>EntityComponents</color> in the scene," +
                    "they will inherit the data from <color=#22AAFF>EntityManager</color>, but they don't write data back, think of this <color=#22AAFF>Entity</color> as prefab, <color=#22FFAA>EntityComponent</color> as instantiation", 15);
            }

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            _entity.AvailableForInteraction = GUILayout.Toggle(_entity.AvailableForInteraction, new GUIContent("Available for Interaction", "If disabled, the entity cannot be interacted with (commonly used for dead entities)."));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            _entity.ApplyTransformDataWhenInstantiate = GUILayout.Toggle(_entity.ApplyTransformDataWhenInstantiate, new GUIContent("Apply Transform Data When Instantiate", "Whether to apply transform data from database to the instantiated entity instance."));
            GUILayout.EndHorizontal();

            if (_entity.ApplyTransformDataWhenInstantiate)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button(new GUIContent("Copy Selected Transform", "Copy Position, Forward, and Scale values from the selected transform.")))
                {
                    Transform transform = Selection.activeTransform;
                    if (transform != null)
                    {
                        _entity.Position = transform.position;
                        _entity.Forward = transform.forward;
                        _entity.Scale = transform.localScale;
                        _valueChanged = true;
                    }
                }
                if (GUILayout.Button(new GUIContent("Reset to Default", "Reset Position, Forward, and Scale to their default values.")))
                {
                    _entity.Position = Vector3.zero;
                    _entity.Forward = Vector3.forward;
                    _entity.Scale = Vector3.one;
                    _valueChanged = true;
                }
                GUILayout.Space(10);
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();

                EditorUtils.Pre(EditorUtils.TextHint("Position:", "Position of this entity."), 10, 110);
                _entity.Position = EditorGUILayout.Vector3Field("", _entity.Position, GUILayout.Width(200), GUILayout.Height(17));
                EditorUtils.End(10);

                EditorUtils.Pre(EditorUtils.TextHint("Forward Direction:", "Forward direction of this entity."), 10, 110);
                _entity.Forward = EditorGUILayout.Vector3Field("", _entity.Forward, GUILayout.Width(200), GUILayout.Height(17));
                EditorUtils.End(10);

                EditorUtils.Pre(EditorUtils.TextHint("Scale:", "Local scale of this entity."), 10, 110);
                _entity.Scale = EditorGUILayout.Vector3Field("", _entity.Scale, GUILayout.Width(200), GUILayout.Height(17));
                EditorUtils.End(10);
            }

            GUI.backgroundColor = EditorUtils._buttonColor*0.5F;
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal(EditorUtils._titleBgStyle, GUILayout.Height(25));
            GUILayout.Label(EditorUtils.TextHint("Tags: (" + _entity.Tags.Count + ")", "Entity tags used for damage detection and other identification logic."), GUILayout.Width(75));
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the tag list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                CopiedTags.Clear();
                for (int i = 0; i < _entity.Tags.Count; i++) CopiedTags.Add(_entity.Tags[i]);
                EditorUtils.ShowBoxNotification("Tags Data copied to clipboard!");
            }
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Paste), "Paste the tag list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                if (CopiedTags.Count > 0)
                {
                    _entity.Tags.Clear();
                    for (int i = 0; i < CopiedTags.Count; i++)
                    {
                        _entity.Tags.Add(CopiedTags[i]);
                    }
                    _valueChanged = true;
                }
            }
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button(new GUIContent("Add", "Add a new tag for this entity."), GUILayout.Width(80)))
            {
                _entity.Tags.Add("NewTag");
                _valueChanged = true;
            }
           
            GUILayout.EndHorizontal();
            EditorUtils.End(10);

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            for (int i = 0; i < _entity.Tags.Count; i++)
            {

                GUI.backgroundColor = EditorUtils._buttonColor;
                _entity.Tags[i] = GUILayout.TextField(_entity.Tags[i], GUILayout.Width(70), GUILayout.Height(17)).ToLower().Replace(" ", "");
                EditorUtils.ResetColor();
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this tag."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    GUIUtility.systemCopyBuffer = _entity.Tags[i];
                    EditorUtils.ShowBoxNotification("Tag copied!");
                }
                GUI.backgroundColor = EditorUtils._black;
                if (GUILayout.Button(new GUIContent("X", "Remove this tag."), GUILayout.Width(20), GUILayout.Height(17)))
                {
                    _entity.Tags.RemoveAt(i);
                    _valueChanged = true;
                }
                if ((i + 1) % 3 == 0 && i != 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUI.backgroundColor = EditorUtils._titleColor * 0.6F;
            GUILayout.BeginHorizontal(EditorUtils._titleBgStyle, GUILayout.Height(25));
            
            if (GUILayout.Button(EditorUtils.TextHint((_entity.attFold?"-":"+")+" Attributes: (" + _entity.Attributes.Count + ") (Upgrade Level: " + _entity.AttributesUpgradeLevel + ")", "Attributes of this entity."),EditorUtils._titleButtonStyle)) {
                _entity.attFold = !_entity.attFold;
            }
            bool _canDecrease = (_entity.AttributesUpgradeLevel > 0);
            GUI.color = Color.white;
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Help), "Opens the official online documentation, including guides, node reference, and examples."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/core/attributes/AttributeData");
                EditorGUI.FocusTextInControl(null);
            }
            //GUI.backgroundColor = _canDecrease ? EditorUtils._red : Color.gray;
            GUI.color = _canDecrease ? Color.white : Color.gray;
            if (GUILayout.Button(new GUIContent("-", "Decrease the Upgrade Level."), GUILayout.Width(20)) && _canDecrease)
            {
                _entity.AttributesUpgradeLevel--;
                _valueChanged = true;
            }
            GUI.color = Color.white;
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button(new GUIContent("+", "Increase the Upgrade Level."), GUILayout.Width(20)))
            {
                _entity.AttributesUpgradeLevel++;
                _valueChanged = true;
            }
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the Attribute list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                CopiedAttributes.Clear();
                for (int i = 0; i < _entity.Attributes.Count; i++) CopiedAttributes.Add(
                  new AttributeData(_entity.Attributes[i].uid,
                  _entity.Attributes[i].floatValue,
                  _entity.Attributes[i].stringValue));

                EditorUtils.ShowBoxNotification("Attributes Data copied to clipboard!");
            }
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Paste), "Paste the Attribute list."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                if (CopiedAttributes.Count > 0)
                {
                    _entity.Attributes.Clear();
                    for (int i = 0; i < CopiedAttributes.Count; i++)
                    {
                        _entity.Attributes.Add(new AttributeData(CopiedAttributes[i].uid, CopiedAttributes[i].floatValue, CopiedAttributes[i].stringValue));
                    }
                    _valueChanged = true;
                }
            }
            GUILayout.EndHorizontal();
            EditorUtils.End(10);

            if (_entity.attFold)
            {

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._buttonColor;
                if (GUILayout.Button(new GUIContent("Fill Attributes from Database", "Populate the attribute list by copying all attributes from the database."), GUILayout.Width(180)))
                {
                    for (int u = 0; u< GameManager.AttributeData.AttributeList.Count; u++)
                    {
                        bool _exist = false;
                        for (int i = 0; i < _entity.Attributes.Count; i++)
                        {
                            if (_entity.Attributes[i].uid == GameManager.AttributeData.AttributeList[u].uid)
                            {
                                _exist = true;
                            }
                        }
                        if (!_exist && (GameManager.AttributeData.AttributeList[u].coreStats || !OnlyCoreAtt))
                        {
                            float _default = 0F;
                            float.TryParse(GameManager.AttributeData.AttributeList[u].defaultValue, out _default);
                            _entity.Attributes.Add(new AttributeData(GameManager.AttributeData.AttributeList[u].uid,
                                _default, GameManager.AttributeData.AttributeList[u].defaultValue));
                        }
                    }
                    _valueChanged = true;
                }

                OnlyCoreAtt = GUILayout.Toggle(OnlyCoreAtt, new GUIContent("Only Core Attributes", "Only copy attributes marked as Core Attributes from the database."));
                EditorUtils.ResetColor();
                EditorUtils.End(10);

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = EditorUtils._titleColor;
                SelAtt = EditorGUILayout.Popup(SelAtt, GameManager.AttributeData.AttributesNames, GUILayout.Width(140), GUILayout.Height(17));
                bool _duplicate = false;
                for (int i = 0; i < _entity.Attributes.Count; i++)
                {
                    if (SelAtt< GameManager.AttributeData.AttributesUidArray.Length && _entity.Attributes[i].uid == GameManager.AttributeData.AttributesUidArray[SelAtt])
                    {
                        _duplicate = true;
                        break;
                    }
                }
                GUI.backgroundColor = _duplicate ? Color.white : EditorUtils._titleColor;
                GUI.color = _duplicate ? Color.gray : Color.white;
                if (GUILayout.Button(new GUIContent("Add", "Add the selected attribute from the left dropdown box to this entity."), GUILayout.Width(105)) && !_duplicate)
                {
                    float _default = 0F;
                    float.TryParse(GameManager.AttributeData.AttributeList[SelAtt].defaultValue, out _default);
                    _entity.Attributes.Add(new AttributeData(GameManager.AttributeData.AttributesUidArray[SelAtt],
                        _default, GameManager.AttributeData.AttributeList[SelAtt].defaultValue));
                    _valueChanged = true;
                }
                GUI.backgroundColor = _entity.Attributes.Count > 0 ? EditorUtils._red : EditorUtils._black;
                GUI.color = _entity.Attributes.Count > 0 ? Color.white : Color.gray;
                if (GUILayout.Button(new GUIContent("Clear All", "Remove all attributes from this entity."), GUILayout.Width(120)))
                {
                    if (EditorUtility.DisplayDialog("Remove all attributes", "Are you sure you want to remove all attributes from this entity?", "Confirm", "Cancel"))
                    {
                        _entity.Attributes.Clear();
                        _valueChanged = true;
                    }
                }
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                List<string> _keys = new List<string>();
                List<int> _duplicateList = new List<int>();
                for (int i = 0; i < _entity.Attributes.Count; i++)
                {
                    string _key = _entity.Attributes[i].uid;
                    if (_keys.Contains(_key))
                    {
                        _duplicateList.Add(i);
                        _duplicateList.Add(_keys.IndexOf(_key));
                        _keys.Add(i.ToString());
                    }
                    else
                    {
                        _keys.Add(_key);
                    }
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                GUI.backgroundColor = Color.black;
                GUILayout.Box(EditorUtils.TextHint("Attribute Name", "The display name of the attribute."), GUILayout.Width(160));
                GUILayout.Box(EditorUtils.TextHint("UID", "String UID used to access this attribute in code. String UIDs are more convenient, while integer IDs offer better performance."), GUILayout.Width(70));
                GUILayout.Box(EditorUtils.TextHint("ID", "Integer ID used to access this attribute in code. Integer IDs offer better performance, while string UIDs are more convenient."), GUILayout.Width(30));
                GUILayout.Box(EditorUtils.TextHint("Random", "Whether the value of this attribute is random."), GUILayout.Width(80));
                GUILayout.Box(EditorUtils.TextHint("Value", "The value of the attribute."), GUILayout.Width(150));
                GUILayout.EndHorizontal();
                EditorUtils.ResetColor();
                for (int i = 0; i < _entity.Attributes.Count; i++)
                {
                    string _key = _entity.Attributes[i].uid;
                    int _index = GameManager.AttributeData.IndexOfAttributes(_key);
                    if (_index < 0)
                    {
                        _entity.Attributes.RemoveAt(i);
                        _valueChanged = true;
                        break;
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    bool _duplicateAtt = _duplicateList.Contains(i);
                    GUI.color = _duplicateAtt ? EditorUtils._red : Color.white;
                    GUI.backgroundColor = Color.gray*1.3F;
                    _index = EditorGUILayout.Popup(Mathf.Max(0, _index), GameManager.AttributeData.AttributesNames, GUILayout.Width(140), GUILayout.Height(17));
                    _entity.Attributes[i].uid = GameManager.AttributeData.AttributesUidArray[_index];
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the uid of this Attributes."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        EditorGUI.FocusTextInControl(null);
                        GUIUtility.systemCopyBuffer = _entity.Attributes[i].id.ToString();
                        EditorUtils.ShowBoxNotification("uid of this Attributes copied!");
                    }
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_key, EditorUtils._boxStyle, GUILayout.Width(70), GUILayout.Height(17));
                    GUILayout.Box(_entity.Attributes[i].id.ToString(), EditorUtils._boxStyle, GUILayout.Width(30), GUILayout.Height(17));
                   
                    if (GameManager.AttributeData.GetAttribute(_key).stringValue)
                    {
                        GUI.backgroundColor = EditorUtils._black;
                        GUILayout.Box("-", EditorUtils._boxStyle, GUILayout.Width(80), GUILayout.Height(17));
                        EditorUtils.ResetColor();
                        _entity.Attributes[i].stringValue = EditorGUILayout.TextField(_entity.Attributes[i].stringValue, GUILayout.Width(130), GUILayout.Height(17));
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button(new GUIContent("X", "Remove this attribute from the entity."), GUILayout.Width(20), GUILayout.Height(17)))
                        {
                            _entity.Attributes.RemoveAt(i);
                            _valueChanged = true;
                        }
                    }
                    else
                    {
                        GUI.backgroundColor = EditorUtils._gray;
                        _entity.Attributes[i].isFixed = !GUILayout.Toggle(!_entity.Attributes[i].isFixed, EditorUtils.TextHint("Random", "Whether the value of this attribute is random."), GUILayout.Width(80), GUILayout.Height(17));
                        EditorUtils.ResetColor();
                        if (_entity.Attributes[i].isFixed)
                        {
                            _entity.Attributes[i].floatValue = EditorGUILayout.FloatField(_entity.Attributes[i].floatValue, GUILayout.Width(130), GUILayout.Height(17));
                            _entity.Attributes[i].minValue = _entity.Attributes[i].floatValue;
                            _entity.Attributes[i].maxValue = _entity.Attributes[i].floatValue;
                        }
                        else
                        {
                            _entity.Attributes[i].minValue = EditorGUILayout.FloatField(_entity.Attributes[i].minValue, GUILayout.Width(50), GUILayout.Height(17));
                            GUILayout.Label(" ~ ", GUILayout.Width(22), GUILayout.Height(17));
                            _entity.Attributes[i].maxValue = EditorGUILayout.FloatField(_entity.Attributes[i].maxValue, GUILayout.Width(50), GUILayout.Height(17));
                            _entity.Attributes[i].floatValue = _entity.Attributes[i].minValue;
                        }
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button(new GUIContent("X", "Remove this attribute from the entity."), GUILayout.Width(20), GUILayout.Height(17)))
                        {
                            _entity.Attributes.RemoveAt(i);
                            _valueChanged = true;
                        }
                        if (!_duplicateAtt && _entity.AttributesUpgradeLevel > 0)
                        {
                            GUILayout.Label("(w/ upgrade: " + (_entity.Attributes[i].floatValue +
                                GameManager.AttributeData.GetAttribute(_key).upgradeIncrement
                                * _entity.AttributesUpgradeLevel).ToString() + ")");
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
            int _customCount = _entity.CustomFloat.Count+ _entity.CustomInt.Count+ _entity.CustomBool.Count+ _entity.CustomString.Count
                + _entity.CustomIntList.Count + _entity.CustomIdIntList.Count+ _entity.CustomIdFloatList.Count;
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.BeginHorizontal(EditorUtils._titleBgStyle, GUILayout.Height(25));
            if (GUILayout.Button((_entity.customFold ? "-" : "+") + EditorUtils.TextHint(" Custom Data: (" + _customCount + ")", "Custom data assigned to this entity."), EditorUtils._titleButtonStyle)) {
                _entity.customFold = !_entity.customFold;
                
            }
            GUI.color = Color.white;
            if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Help), "Opens the official online documentation, including guides, node reference, and examples."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
            {
                Application.OpenURL("https://www.soft-kitty.com/docs/core/CustomData");
                EditorGUI.FocusTextInControl(null);
            }
            GUI.backgroundColor = EditorUtils._titleColor;
            if (GUILayout.Button(EditorUtils.TextHint("Sync with Database", "Sync custom data with the database. Removes non-existent entries and adds missing ones."),GUILayout.Width(120))) {
                SyncCustomData(_entity, _myTarget);
                EditorGUI.FocusTextInControl(null);
                _valueChanged = true;
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();

            if (_entity.customFold)
            {
                for (int i = 0; i < _entity.CustomFloat.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.Float, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("Float", EditorUtils._boxStyle, GUILayout.Width(80));
                    EditorUtils.ResetColor();
                    _entity.CustomFloat[i] = EditorGUILayout.FloatField(_entity.CustomFloat[i], GUILayout.Width(50));
                    GUILayout.EndHorizontal();
                }
                for (int i = 0; i < _entity.CustomInt.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.Int, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("Int", EditorUtils._boxStyle, GUILayout.Width(80));
                    EditorUtils.ResetColor();
                    _entity.CustomInt[i] = EditorGUILayout.IntField(_entity.CustomInt[i], GUILayout.Width(50));
                    GUILayout.EndHorizontal();
                }
                for (int i = 0; i < _entity.CustomBool.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.Bool, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("Bool", EditorUtils._boxStyle, GUILayout.Width(80));
                    EditorUtils.ResetColor();
                    _entity.CustomBool[i] = EditorGUILayout.Toggle(_entity.CustomBool[i], GUILayout.Width(50));
                    GUILayout.EndHorizontal();
                }
                for (int i = 0; i < _entity.CustomString.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.String, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("String", EditorUtils._boxStyle, GUILayout.Width(80));
                    EditorUtils.ResetColor();
                    _entity.CustomString[i] = EditorGUILayout.TextField(_entity.CustomString[i], GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                }
                for (int i = 0; i < _entity.CustomIntList.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.IntList, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("Int List", EditorUtils._boxStyle, GUILayout.Width(80));
                    GUILayout.Label("(" + _entity.CustomIntList[i].list.Count + ")", GUILayout.Width(25));
                    GUI.backgroundColor = EditorUtils._titleColor;
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        _entity.CustomIntList[i].list.Add(0);
                        EditorGUI.FocusTextInControl(null);
                        _valueChanged = true;
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                    for (int u = 0; u < _entity.CustomIntList[i].list.Count; u++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(90);
                        GUILayout.Label("(" + u + ")", GUILayout.Width(25));
                        _entity.CustomIntList[i].list[u] =
                            EditorGUILayout.IntField(_entity.CustomIntList[i].list[u],
                            GUILayout.Width(50));
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(17)))
                        {
                            _entity.CustomIntList[i].list.RemoveAt(u);
                            EditorGUI.FocusTextInControl(null);
                            _valueChanged = true;
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                    }
                }
                for (int i = 0; i < _entity.CustomIdIntList.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.IdIntList, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("ID Int List", EditorUtils._boxStyle, GUILayout.Width(80));
                    GUILayout.Label("(" + _entity.CustomIdIntList[i].list.Count + ")", GUILayout.Width(25));
                    GUI.backgroundColor = EditorUtils._titleColor;
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        _entity.CustomIdIntList[i].list.Add(new IdInt());
                        EditorGUI.FocusTextInControl(null);
                        _valueChanged = true;
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                    for (int u = 0; u < _entity.CustomIdIntList[i].list.Count; u++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(90);
                        GUILayout.Label("ID:", GUILayout.Width(25));
                        _entity.CustomIdIntList[i].list[u].id =
                          EditorGUILayout.IntField(
                            _entity.CustomIdIntList[i].list[u].id,
                            GUILayout.Width(30));
                        GUILayout.Label("Value:", GUILayout.Width(40));
                        _entity.CustomIdIntList[i].list[u].value =
                          EditorGUILayout.IntField(
                            _entity.CustomIdIntList[i].list[u].value,
                            GUILayout.Width(50));
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Width(20)))
                        {
                            _entity.CustomIdIntList[i].list.RemoveAt(u);
                            EditorGUI.FocusTextInControl(null);
                            _valueChanged = true;
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                    }
                }
                for (int i = 0; i < _entity.CustomIdFloatList.Count; i++)
                {
                    string _uid = _myTarget.GetCustomUid(CustomData.CustomTypes.IdFloatList, i);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._titleColor;
                    GUILayout.Box(_uid, EditorUtils._boxLeftStyle, GUILayout.Width(100));
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box("ID Float List", EditorUtils._boxStyle, GUILayout.Width(80));
                    GUILayout.Label("(" + _entity.CustomIdFloatList[i].list.Count + ")", GUILayout.Width(25));
                    GUI.backgroundColor = EditorUtils._titleColor;
                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        _entity.CustomIdFloatList[i].list.Add(new IdFloat());
                        EditorGUI.FocusTextInControl(null);
                        _valueChanged = true;
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();
                    for (int u = 0; u < _entity.CustomIdFloatList[i].list.Count; u++)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(90);
                        GUILayout.Label("ID:", GUILayout.Width(25));
                        _entity.CustomIdFloatList[i].list[u].id =
                          EditorGUILayout.IntField(
                            _entity.CustomIdFloatList[i].list[u].id,
                            GUILayout.Width(30));
                        GUILayout.Label("Value:", GUILayout.Width(40));
                        _entity.CustomIdFloatList[i].list[u].value =
                          EditorGUILayout.FloatField(
                            _entity.CustomIdFloatList[i].list[u].value,
                            GUILayout.Width(50));
                        GUI.backgroundColor = EditorUtils._black;
                        if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Width(20)))
                        {
                            _entity.CustomIdFloatList[i].list.RemoveAt(u);
                            EditorGUI.FocusTextInControl(null);
                            _valueChanged = true;
                        }
                        EditorUtils.ResetColor();
                        GUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.Separator();



            }

            if (!SGD_Settings.isRuntime)
            {
                foreach (var module in EntityInspectorRegistry.Modules)
                {
                    if (module.DrawInspector(_entity, _myTarget)) _valueChanged = true;
                }
                if (_valueChanged)
                {
                    foreach (var obj in _entity.Modules) obj.Save();
                }
            }
            else
            {

                EditorUtils.HelpInfo("You can not modify Modules during runtime.", 10);
            }

            EditorGUILayout.Separator();
            GUILayout.EndVertical();
            GUILayout.Space(27);
            GUILayout.EndHorizontal();
            EditorGUILayout.Separator();
            if ( GUI.changed) _valueChanged=true;
            return _valueChanged;
        }




        private void SyncCustomData(Entity _entity, EntityManagerObject _myTarget)
        {
            int _maxFloat = 0;
            int _maxInt = 0;
            int _maxString = 0;
            int _maxBool = 0;
            int _maxIntList = 0;
            int _maxIdIntList = 0;
            int _maxIdFloatList = 0;
            foreach (var obj in _myTarget.CustomDataSetting)
            {
                switch (obj.type)
                {
                    case CustomData.CustomTypes.Float:
                        _maxFloat = Mathf.Max(_maxFloat, obj.index);
                        if (_entity.CustomFloat.Count <= obj.index)
                        {
                            for (int x = _entity.CustomFloat.Count; x <= obj.index; x++) _entity.CustomFloat.Add(0F);
                        }
                        break;
                    case CustomData.CustomTypes.Int:
                        _maxInt = Mathf.Max(_maxInt, obj.index);
                        if (_entity.CustomInt.Count <= obj.index)
                        {
                            for (int x = _entity.CustomInt.Count; x <= obj.index; x++) _entity.CustomInt.Add(0);
                        }
                        break;
                    case CustomData.CustomTypes.String:
                        _maxString = Mathf.Max(_maxString, obj.index);
                        if (_entity.CustomString.Count <= obj.index)
                        {
                            for (int x = _entity.CustomString.Count; x <= obj.index; x++) _entity.CustomString.Add("");
                        }
                        break;
                    case CustomData.CustomTypes.Bool:
                        _maxBool = Mathf.Max(_maxBool, obj.index);
                        if (_entity.CustomBool.Count <= obj.index)
                        {
                            for (int x = _entity.CustomBool.Count; x <= obj.index; x++) _entity.CustomBool.Add(false);
                        }
                        break;
                    case CustomData.CustomTypes.IntList:
                        _maxIntList = Mathf.Max(_maxIntList, obj.index);
                        if (_entity.CustomIntList.Count <= obj.index)
                        {
                            for (int x = _entity.CustomIntList.Count; x <= obj.index; x++) _entity.CustomIntList.Add(new IntList());
                        }
                        break;
                    case CustomData.CustomTypes.IdIntList:
                        _maxIdIntList = Mathf.Max(_maxIdIntList, obj.index);
                        if (_entity.CustomIdIntList.Count <= obj.index)
                        {
                            for (int x = _entity.CustomIdIntList.Count; x <= obj.index; x++) _entity.CustomIdIntList.Add(new IdIntList());
                        }
                        break;
                    case CustomData.CustomTypes.IdFloatList:
                        _maxIdFloatList = Mathf.Max(_maxIdFloatList, obj.index);
                        if (_entity.CustomIdFloatList.Count <= obj.index)
                        {
                            for (int x = _entity.CustomIdFloatList.Count; x <= obj.index; x++) _entity.CustomIdFloatList.Add(new IdFloatList());
                        }
                        break;
                }
            }
            for (int x = _entity.CustomFloat.Count - 1; x > _maxFloat; x--) _entity.CustomFloat.RemoveAt(_entity.CustomFloat.Count - 1);
            for (int x = _entity.CustomInt.Count - 1; x > _maxInt; x--) _entity.CustomInt.RemoveAt(_entity.CustomInt.Count - 1);
            for (int x = _entity.CustomBool.Count - 1; x > _maxBool; x--) _entity.CustomBool.RemoveAt(_entity.CustomBool.Count - 1);
            for (int x = _entity.CustomString.Count - 1; x > _maxString; x--) _entity.CustomString.RemoveAt(_entity.CustomString.Count - 1);
            for (int x = _entity.CustomIntList.Count - 1; x > _maxIntList; x--) _entity.CustomIntList.RemoveAt(_entity.CustomIntList.Count - 1);
            for (int x = _entity.CustomIdIntList.Count - 1; x > _maxIdIntList; x--) _entity.CustomIdIntList.RemoveAt(_entity.CustomIdIntList.Count - 1);
            for (int x = _entity.CustomIdFloatList.Count - 1; x > _maxIdFloatList; x--) _entity.CustomIdFloatList.RemoveAt(_entity.CustomIdFloatList.Count - 1);
        }

        private bool isMatching(Entity _entity, EntityManagerObject _target)
        {
            if (string.IsNullOrEmpty(_target.SearchText)) return true;
            if (_target.SearchType == 0)
            {
                return _entity.uid.ToLower().Contains(_target.SearchText.ToLower());
            }
            else if (_target.SearchType == 1)
            {
                return _entity.hasMatchingTag(_target.SearchText.ToLower());
            }
            else
            {
                return _entity.GetAttributeString(_target.SearchOptions[_target.SearchType]).ToLower().Contains(_target.SearchText.ToLower());
            }
        }

    }
}
