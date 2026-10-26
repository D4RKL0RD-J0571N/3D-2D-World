using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SoftKitty
{
    [CustomEditor(typeof(AttributeObject))]
    public class AttributeObject_Inspector : UnityEditor.Editor
    {
        private void ApplyAndSave(AttributeObject scriptableObject)
        {
            scriptableObject.GenerateUniqueHash();
            EditorUtility.SetDirty(scriptableObject);
        }
        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            AttributeObject myTarget = (AttributeObject)target;
            string[] _attFormatOption = new string[2];
            _attFormatOption[0] = "+";
            _attFormatOption[1] = ":";

            EditorUtils.TitleLogo(EditorUtils.GetTexture(EditorIcon.DataLogo),0);

            EditorUtils.PreBox(EditorUtils.TextHint("Data Hash:", "The data hash is a unique string representing the current state of the data. It changes automatically whenever any data is modified."), 6, Color.black, 70);
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.Box(myTarget.Hash, EditorUtils._boxLeftStyle);
           
            if (GUILayout.Button("Generate",GUILayout.Width(60))) {
                myTarget.GenerateUniqueHash();
            }
            EditorUtils.ResetColor();
            EditorUtils.End(0);

            

            EditorUtils.Document("core/attributes/AttributeObject",true);
            EditorGUILayout.Separator();

            EditorUtils.Save();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = EditorUtils._buttonColor;
            if (GUILayout.Button(new GUIContent("Add New [Attribute]", "Create a new attrubute."), GUILayout.Width(150)))
            {
                Attribute _newAtt = new Attribute();
                _newAtt.name = "New Attribute" + myTarget.AttributeList.Count.ToString();
                _newAtt.uid = "Att" + myTarget.AttributeList.Count.ToString();
                _newAtt.upgradeIncrement = 0F;
                _newAtt.visible = true;
                _newAtt.stringValue = false;
                _newAtt.fold = true;
                //_newAtt.value = "";
                myTarget.AttributeList.Add(_newAtt);
                _valueChanged = true;
                EditorGUI.FocusTextInControl(null);
            }
            GUI.backgroundColor = EditorUtils._tagColor;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand All", GUILayout.Width(80)))
            {
                for (int i = 0; i < myTarget.AttributeList.Count; i++) myTarget.AttributeList[i].fold = true;
            }

            if (GUILayout.Button("Fold All", GUILayout.Width(80)))
            {
                for (int i = 0; i < myTarget.AttributeList.Count; i++) myTarget.AttributeList[i].fold = false;
            }
            EditorUtils.ResetColor();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            List<string> _uids = new List<string>();
            for (int i = 0; i < myTarget.AttributeList.Count; i++)
            {
                bool _duplicateUid = _uids.Contains(myTarget.AttributeList[i].uid);
                if (!_duplicateUid) _uids.Add(myTarget.AttributeList[i].uid);
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = myTarget.AttributeList[i].fold ? EditorUtils._activeColor2 : EditorUtils._disableColor2;
                GUILayout.Label(myTarget.AttributeList[i].fold ? "[-]" : "[+]", GUILayout.Width(20));
                if (_duplicateUid)
                {
                    GUI.backgroundColor = Color.white;
                    GUILayout.Box(EditorUtils.GetTexture(EditorIcon.Warning), EditorUtils._iconStyle, GUILayout.Width(16), GUILayout.Height(16));
                    GUI.backgroundColor = Color.red;
                }
                int _id = myTarget.AttributeList[i].id;
                string _uid = myTarget.AttributeList[i].uid;
                GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                if (GUILayout.Button(new GUIContent(myTarget.AttributeList[i].name + " ( UID: " + _uid + " , ID: "+ _id + " )", "Click to expand"), EditorUtils._titleButtonStyle))
                {
                    myTarget.AttributeList[i].fold = !myTarget.AttributeList[i].fold;
                    EditorGUI.FocusTextInControl(null);
                }
                EditorUtils.ResetColor();
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Duplicate), "Duplicate this attribute."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    Attribute _newAtt = myTarget.AttributeList[i].Copy();
                    string _base = _newAtt.uid + "_copy";
                    int _maxId = 0;
                    foreach (var obj in myTarget.AttributeList)
                    {
                        if (obj.uid.Contains(_base))
                        {
                            int _testId = -1;
                            if (int.TryParse(obj.uid.Replace(_base, ""), out _testId)) _maxId = Mathf.Max(_maxId, _testId);
                        }
                    }
                    _newAtt.uid = _base + (_maxId + 1).ToString();
                    myTarget.AttributeList.Add(_newAtt);
                    _valueChanged = true;
                    EditorGUI.FocusTextInControl(null);
                }
                EditorUtils.ResetColor();
                GUI.color = i > 0 ? Color.white : Color.gray;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Up), "Move this attribute up."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    if (i > 0)
                    {
                        Attribute _mAtt = myTarget.AttributeList[i].Copy();
                        _mAtt.fold = false;
                        myTarget.AttributeList.RemoveAt(i);
                        myTarget.AttributeList.Insert(i - 1, _mAtt);
                        EditorGUI.FocusTextInControl(null);
                        ApplyAndSave(myTarget);
                        return;
                    }
                }
                GUI.color = i < myTarget.AttributeList.Count - 1 ? Color.white : Color.gray;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Down), "Move this attribute down."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    if (i < myTarget.AttributeList.Count - 1)
                    {
                        Attribute _mAtt = myTarget.AttributeList[i].Copy();
                        _mAtt.fold = false;
                        myTarget.AttributeList.RemoveAt(i);
                        myTarget.AttributeList.Insert(i + 1, _mAtt);
                        EditorGUI.FocusTextInControl(null);
                        ApplyAndSave(myTarget);
                        return;
                    }
                }
                GUILayout.EndHorizontal();
                GUI.color = Color.white;
                if (i > 0)
                {
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button(new GUIContent("X", "Delete this attribute."), GUILayout.Width(20), GUILayout.Height(17)))
                    {
                        myTarget.AttributeList.RemoveAt(i);
                        myTarget.IdManager.RemoveKey(_id);
                        EditorGUI.FocusTextInControl(null);
                        ApplyAndSave(myTarget);
                        return;
                    }

                }
                else
                {
                    GUILayout.Space(25);
                }
                EditorUtils.ResetColor();
                GUILayout.EndHorizontal();

                if (myTarget.AttributeList[i].fold)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUILayout.BeginVertical(EditorUtils._groupStyle);
                    string _oldUid = myTarget.AttributeList[i].uid;

                    EditorUtils.TextField(ref myTarget.AttributeList[i].name, new GUIContent("Display Name:", "The name displayed in the interface for this attribute."),10,130);
                    if (_duplicateUid) EditorUtils.Warnning("Duplicated Script uid! Please make every uid unique.", 10);

                    EditorUtils.TextField(ref myTarget.AttributeList[i].uid, new GUIContent("UID:", "A unique string used to access the value of this attribute in your script. "), 10, 130,true);
                    if (_oldUid != myTarget.AttributeList[i].uid)
                    {
                        myTarget.IdManager.ReplaceKey(_id, myTarget.AttributeList[i].uid);
                    }

                    EditorUtils.Pre(EditorUtils.TextHint("ID:", "The unique int id of this attribute."), 10, 130);
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Box(EditorUtils.TextHint(myTarget.AttributeList[i].id.ToString(), "This unique integer ID is auto-generated. Using integer IDs provides better performance than string UIDs"), EditorUtils._boxLeftStyle);
                    EditorUtils.ResetColor();
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the int id of this attribute."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        EditorGUI.FocusTextInControl(null);
                        GUIUtility.systemCopyBuffer = myTarget.AttributeList[i].id.ToString();
                        EditorUtils.ShowBoxNotification("int id of this attribute copied!");
                    }
                    EditorUtils.End(10);

                    EditorUtils.Toggle(ref myTarget.AttributeList[i].stringValue, new GUIContent("String Attribute:",
                        "Enable this if the attribute stores a string value. For example, an attribute named 'Creator' can store the player¡¯s name when an item is crafted by calling Item.UpdateAttribute('Creator', 'Player Name')."),
                        10, 130);

                    if (myTarget.AttributeList[i].stringValue)
                    {
                        EditorUtils.TextField(ref myTarget.AttributeList[i].defaultValue,
                           new GUIContent("Default Value:", "The default value of this attribute."),
                            10, 130);
                    }
                    else
                    {
                        float _value = 0F;
                        float.TryParse(myTarget.AttributeList[i].defaultValue, out _value);
                        EditorUtils.FloatField(ref _value,
                            new GUIContent("Default Value:", "The default value of this attribute."),
                             10, false, 0, 130);
                        myTarget.AttributeList[i].defaultValue = _value.ToString();
                    }

                    EditorUtils.FloatField(ref myTarget.AttributeList[i].upgradeIncrement,
                        new GUIContent("Upgrade Increment:", "Attribute can be upgraded through the upgrade level. Each upgrade level increases this attribute by the value specified here."),
                         10, true, 1, 130);


                    GUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    GUILayout.Label(new GUIContent("Display Format:", "Defines how this attribute is displayed in the mouse hover information panel."), GUILayout.Width(130));
                    myTarget.AttributeList[i].displayFormat = EditorGUILayout.Popup(myTarget.AttributeList[i].displayFormat, _attFormatOption, GUILayout.Width(50));
                    GUILayout.Label(" Suffixes:", GUILayout.Width(60),GUILayout.Height(17));
                    myTarget.AttributeList[i].suffixes = GUILayout.TextField(myTarget.AttributeList[i].suffixes, GUILayout.Width(40), GUILayout.Height(17));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(30);
                    GUI.backgroundColor = EditorUtils._buttonColor;
                    GUILayout.Label("Preview:", EditorUtils._boxLeftStyle, GUILayout.Width(110));
                   
                    GUILayout.Box(myTarget.AttributeList[i].name + " " + _attFormatOption[myTarget.AttributeList[i].displayFormat] + " 5 " + myTarget.AttributeList[i].suffixes,EditorUtils._boxLeftStyle);
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();


                    EditorUtils.Toggle(ref myTarget.AttributeList[i].coreStats, new GUIContent("Core Attribute:", "Determines whether this attribute is displayed in bold and prioritized above other attributes."),
                       10, 130);

                    EditorUtils.Toggle(ref myTarget.AttributeList[i].compareInfo, new GUIContent("Compare Info:", "Determines whether comparison information is displayed for this attribute in the mouse hover information panel."),
                      10, 130);

                    EditorUtils.Toggle(ref myTarget.AttributeList[i].visible, new GUIContent("Visible in Hover Info:", "Determines whether this attribute is visible in the mouse hover information panel."),
                      10, 130);

                    EditorUtils.Toggle(ref myTarget.AttributeList[i].visibleInStatsPanel, new GUIContent("Visible in Stats Panel:", "Determines whether this attribute is visible in the stats panel."),
                     10, 130);

                    EditorGUILayout.Separator();
                    GUILayout.EndVertical();
                    GUILayout.Space(27);
                    GUILayout.EndHorizontal();
                    EditorGUILayout.Separator();
                }

            }

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = myTarget.IdManager.uiFold? EditorUtils._titleColor: EditorUtils._gray*0.6F;
            if (GUILayout.Button(EditorUtils.TextHint("ID List Count: " + myTarget.IdManager.idToKey.Count, "An auto-generated list of unique integer IDs. Using integer IDs improves performance."), GUILayout.Width(120)))
            {
                myTarget.IdManager.uiFold = !myTarget.IdManager.uiFold;
            }
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("Rebuild", GUILayout.Width(80))) {
                if (EditorUtility.DisplayDialog("Warning!", "Rebuilding the ID list will break the mapping between string UIDs and integer IDs. Any code accessing attributes by integer ID will stop working.", "I'm Aware", "Cancel"))
                {
                    myTarget.IdManager.idToKey.Clear();
                }
            }
            EditorUtils.ResetColor();
            GUILayout.EndHorizontal();
            if (myTarget.IdManager.uiFold) {
                foreach (var obj in myTarget.IdManager.idToKey) {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(50);
                    GUILayout.Box(" "+obj.key, EditorUtils._boxLeftStyle,GUILayout.Width(100));
                    GUILayout.Box(" " + obj.id.ToString(), EditorUtils._boxLeftStyle, GUILayout.Width(80));
                    GUILayout.EndHorizontal();
                }
            }


            if ((_valueChanged || GUI.changed) && !Application.isPlaying)
            {
                myTarget.GenerateUniqueHash();
                ApplyAndSave(myTarget);
            }
        }
    }
}
