using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
namespace SoftKitty
{
    public interface IOverTimeEffectInspectorModule { bool DrawInspector(OverTimeEffectObject myTarget, int i); }

    public static class OverTimeEffectInspectorRegistry
    { 
        public static List<IOverTimeEffectInspectorModule> Modules = new List<IOverTimeEffectInspectorModule>(); 
        public static void Register(IOverTimeEffectInspectorModule module) { 
            if (!Modules.Contains(module)) Modules.Add(module); 
        } 
    }


    [CustomEditor(typeof(OverTimeEffectObject))]
    public class OverTimeEffectObject_Inspector : UnityEditor.Editor
    {
        public static OverTimeEffect CopiedEffect;
        public static List<CustomField> CopiedCustomData = new List<CustomField>();
        private string[] SearchOptions = new string[3] { "uid", "name", "category" };
        public List<string> Categories = new List<string>();
        private void ApplyAndSave(OverTimeEffectObject scriptableObject)
        {
            scriptableObject.GenerateUniqueHash();
            EditorUtility.SetDirty(scriptableObject);
        }

        public override void OnInspectorGUI()
        {
            GUI.changed = false;
            bool _valueChanged = false;
            OverTimeEffectObject myTarget = (OverTimeEffectObject)target;
           
            string _newHash = "";
            if (GameManager.OverTimeEffectData != null)
            {
                _newHash = GameManager.OverTimeEffectData.Hash;
                if (myTarget.CategoryHash != _newHash || Categories.Count == 0)
                {
                    Categories.Clear();
                    foreach (var obj in GameManager.OverTimeEffectData.overTimeEffects)
                    {
                        if (!string.IsNullOrEmpty(obj.category) && !Categories.Contains(obj.category)) Categories.Add(obj.category);
                    }
                    myTarget.CategoryHash = _newHash;

                }
            }

            EditorUtils.TitleLogo(EditorUtils.GetTexture(EditorIcon.DataLogo), 0);

            EditorUtils.PreBox(EditorUtils.TextHint("Data Hash:", "The data hash is a unique string representing the current state of the data. It changes automatically whenever any data is modified."), 6, Color.black, 70);
            GUI.backgroundColor = EditorUtils._black;
            GUILayout.Box(myTarget.Hash, EditorUtils._boxLeftStyle);
            
            if (GUILayout.Button("Generate", GUILayout.Width(60)))
            {
                myTarget.GenerateUniqueHash();
            }
            EditorUtils.ResetColor();
            EditorUtils.End(0);

            EditorUtils.Document("core/over-time-effects/OverTimeEffectObject", true);
            EditorGUILayout.Separator();

            EditorUtils.Save();

            EditorUtils.FloatSlider(ref myTarget.OverTimeEffectInterval, 0.005F, 3F, EditorUtils.TextHint("Tick Interval:", "The interval for Over Time Effect ticks."), " sec", 30);

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUILayout.Label("Search:", EditorUtils._boxLeftStyle, GUILayout.Width(50));
            GUI.backgroundColor = EditorUtils._titleColor;
            myTarget.SearchType = EditorGUILayout.Popup(myTarget.SearchType, SearchOptions, GUILayout.Width(100));
            GUI.backgroundColor = string.IsNullOrEmpty(myTarget.SearchText) ? EditorUtils._gray : new Color(1F, 1F, 0F);
            if (!string.IsNullOrEmpty(myTarget.SearchText)) GUI.color = new Color(1F, 0.5F, 0F);
            myTarget.SearchText = EditorGUILayout.TextField(myTarget.SearchText);
            EditorUtils.ResetColor();
            GUI.backgroundColor = EditorUtils._black;
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                myTarget.SearchText = "";
                EditorGUI.FocusTextInControl(null);
                ApplyAndSave(myTarget);
            }
            EditorUtils.ResetColor();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            EditorGUILayout.Separator();

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            GUI.backgroundColor = EditorUtils._buttonColor;
            if (GUILayout.Button(new GUIContent("Add New [OverTimeEffect]", "Create a new Over Time Effect."), GUILayout.Width(170)))
            {
                OverTimeEffect _newEffect = new OverTimeEffect();
                _newEffect.name = "New Effect" + myTarget.overTimeEffects.Count.ToString();
                _newEffect.uid = "Eff" + myTarget.overTimeEffects.Count.ToString();
                _newEffect.iconPath="";
                _newEffect.category="";
                _newEffect.directRefIcon=null;
                _newEffect.iconLoadMethod= LoadMethod.DirectReference;
                _newEffect.permanent = false;
                _newEffect.duration = 1F;
                _newEffect.layered = false;
                _newEffect.maxLayer = 99;
                _newEffect.canBeExtended = false;
                _newEffect.canBeRefreshed = true;
                _newEffect.canBeDispelled = true;
                _newEffect.backGroundColor=Color.white;
                _newEffect.frameColor=Color.white;
                _newEffect.DesignGraph=null;
                _newEffect.mCustomField = new System.Collections.Generic.List<CustomField>();
                _newEffect.fold = true;
                myTarget.overTimeEffects.Add(_newEffect);
                _valueChanged = true;
                EditorGUI.FocusTextInControl(null);
            }
            GUI.backgroundColor = EditorUtils._tagColor;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Expand All", GUILayout.Width(80)))
            {
                for (int i = 0; i < myTarget.overTimeEffects.Count; i++) myTarget.overTimeEffects[i].fold = true;
            }

            if (GUILayout.Button("Fold All", GUILayout.Width(80)))
            {
                for (int i = 0; i < myTarget.overTimeEffects.Count; i++) myTarget.overTimeEffects[i].fold = false;
            }
            EditorUtils.ResetColor();
            GUILayout.Space(20);
            GUILayout.EndHorizontal();

            List<string> _uids = new List<string>();

            if (!string.IsNullOrEmpty(myTarget.SearchText))
            {
                EditorUtils.Label(EditorUtils.TextHint("Search results of [" + SearchOptions[myTarget.SearchType] + "] matching: '<color=#FF7733>" + myTarget.SearchText + "</color>' ..."), 20);
            }

            for (int i = 0; i < myTarget.overTimeEffects.Count; i++)
            {
                bool _duplicateUid = _uids.Contains(myTarget.overTimeEffects[i].uid);
                if(!_duplicateUid) _uids.Add(myTarget.overTimeEffects[i].uid);

                if (isMatching(myTarget.overTimeEffects[i], myTarget))
                {
                    int _oid = myTarget.overTimeEffects[i].id;
                    string _key= myTarget.overTimeEffects[i].uid;
                    GUI.backgroundColor = myTarget.overTimeEffects[i].fold ? EditorUtils._activeColor2 : EditorUtils._disableColor2;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(myTarget.overTimeEffects[i].fold ? "[-]" : "[+]", GUILayout.Width(20));
                    if (_duplicateUid)
                    {
                        GUI.backgroundColor = Color.white;
                        GUILayout.Box(EditorUtils.GetTexture(EditorIcon.Warning), EditorUtils._iconStyle, GUILayout.Width(16), GUILayout.Height(16));
                        GUI.backgroundColor = Color.red;
                    }
                    GUILayout.BeginHorizontal(EditorUtils._titleBgStyle);
                    if (GUILayout.Button(new GUIContent(myTarget.overTimeEffects[i].name + " (UID: " + myTarget.overTimeEffects[i].uid+" ,ID: " + myTarget.overTimeEffects[i] .id+ ")","Click to expand"), EditorUtils._titleButtonStyle))
                    {
                        myTarget.overTimeEffects[i].fold = !myTarget.overTimeEffects[i].fold;
                        EditorGUI.FocusTextInControl(null);
                    }
                    EditorUtils.ResetColor();
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Duplicate), "Duplicate this effect."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        OverTimeEffect _newEffect = myTarget.overTimeEffects[i].Copy();
                        string _base = _newEffect.uid + "_copy";
                        int _maxId = 0;
                        foreach (var obj in myTarget.overTimeEffects)
                        {
                            if (obj.uid.Contains(_base))
                            {
                                int _testId = -1;
                                if (int.TryParse(obj.uid.Replace(_base, ""), out _testId)) _maxId = Mathf.Max(_maxId, _testId);
                            }
                        }
                        _newEffect.uid = _base + (_maxId + 1).ToString();
                        myTarget.overTimeEffects.Add(_newEffect);
                        _valueChanged = true;
                        EditorGUI.FocusTextInControl(null);
                    }
                    EditorUtils.ResetColor();
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this effect to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        CopiedEffect = myTarget.overTimeEffects[i].Copy();
                        EditorUtils.ShowBoxNotification("Effect Data copied to clipboard!");
                    }
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Paste), "Paste this effect to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        if (CopiedEffect != null)
                        {
                            EditorGUI.FocusTextInControl(null);
                            myTarget.overTimeEffects[i] = CopiedEffect.Copy();
                        }
                    }

                    GUI.color = i > 0 ? Color.white : Color.gray;
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Up), "Move this OverTimeEffect up."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        if (i > 0)
                        {
                            OverTimeEffect _mAtt = myTarget.overTimeEffects[i].Copy();
                            _mAtt.fold = false;
                            myTarget.overTimeEffects.RemoveAt(i);
                            myTarget.overTimeEffects.Insert(i - 1, _mAtt);
                            EditorGUI.FocusTextInControl(null);
                            ApplyAndSave(myTarget);
                            return;
                        }
                    }
                    GUI.color = i < myTarget.overTimeEffects.Count - 1 ? Color.white : Color.gray;
                    if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Down), "Move this OverTimeEffect down."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                    {
                        if (i < myTarget.overTimeEffects.Count - 1)
                        {
                            OverTimeEffect _mAtt = myTarget.overTimeEffects[i].Copy();
                            _mAtt.fold = false;
                            myTarget.overTimeEffects.RemoveAt(i);
                            myTarget.overTimeEffects.Insert(i + 1, _mAtt);
                            EditorGUI.FocusTextInControl(null);
                            ApplyAndSave(myTarget);
                            return;
                        }
                    }
                    GUILayout.EndHorizontal();
                    GUI.color = Color.white;
                    GUI.backgroundColor = EditorUtils._black;
                    if (GUILayout.Button(new GUIContent("X", "Delete this OverTimeEffect."), GUILayout.Width(20), GUILayout.Height(17)))
                    {
                        myTarget.overTimeEffects.RemoveAt(i);
                        myTarget.IdManager.RemoveKey(_oid);
                        EditorGUI.FocusTextInControl(null);
                        ApplyAndSave(myTarget);
                        return;
                    }
                    EditorUtils.ResetColor();
                    GUILayout.EndHorizontal();

                    if (myTarget.overTimeEffects[i].fold)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(30);
                        GUILayout.BeginVertical(EditorUtils._groupStyle);
                        EditorUtils.TextField(ref myTarget.overTimeEffects[i].name, new GUIContent("Display Name:", "The name displayed in the interface for this effect."), 10, 130);
                        if (_duplicateUid) EditorUtils.Warnning("Duplicated UID ! Please make every uid unique.", 10);
                        EditorUtils.TextField(ref myTarget.overTimeEffects[i].uid, new GUIContent("UID:", "A unique string used to reference this over time effect in code. For example, if the UID is 'fireDot', you can apply it by calling <EntityComponent>().AddOverTimeEffect('fireDot', _dealer)."), 10, 130,true);
                        if (_key != myTarget.overTimeEffects[i].uid)
                        {
                            myTarget.IdManager.ReplaceKey(_oid, myTarget.overTimeEffects[i].uid);
                        }
                        EditorUtils.Pre(EditorUtils.TextHint("ID:", "The unique int id of this OverTimeEffect."), 10, 130);
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        GUILayout.Box(EditorUtils.TextHint(myTarget.overTimeEffects[i].id.ToString(), "This unique integer ID is auto-generated. Using integer IDs provides better performance than string UIDs"), EditorUtils._boxLeftStyle);
                        EditorUtils.ResetColor();
                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy the int id of this OverTimeEffect."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            EditorGUI.FocusTextInControl(null);
                            GUIUtility.systemCopyBuffer = myTarget.overTimeEffects[i].id.ToString();
                            EditorUtils.ShowBoxNotification("int id of this OverTimeEffect copied!");
                        }
                        EditorUtils.End(10);

                        EditorUtils.Pre(new GUIContent("Category:", "A category string used to group effects (e.g. Buff, DoT)."), 10, 130);
                        myTarget.overTimeEffects[i].category = GUILayout.TextField(myTarget.overTimeEffects[i].category, GUILayout.Width(80), GUILayout.Height(17)).Replace(" ", "");

                        int _cateSel = Categories.IndexOf(myTarget.overTimeEffects[i].category);
                        EditorGUI.BeginChangeCheck();
                        _cateSel = EditorGUILayout.Popup(_cateSel, Categories.ToArray(), GUILayout.Height(17));
                        if (EditorGUI.EndChangeCheck())
                        {
                            EditorGUI.FocusTextInControl(null);
                            myTarget.overTimeEffects[i].category = Categories[_cateSel];
                        }
                        EditorUtils.End(10);

                        foreach (var module in OverTimeEffectInspectorRegistry.Modules) { 
                            if (module.DrawInspector(myTarget,i)) _valueChanged=true; 
                        }

                        EditorUtils.Pre(new GUIContent("Load Method:", "The method used to load the icon texture."), 10, 130);
                        myTarget.overTimeEffects[i].iconLoadMethod = (LoadMethod)EditorGUILayout.EnumPopup(myTarget.overTimeEffects[i].iconLoadMethod);
                        EditorUtils.End(10);
                        if (myTarget.overTimeEffects[i].iconLoadMethod == LoadMethod.DirectReference)
                        {
                            EditorUtils.TextureField(ref myTarget.overTimeEffects[i].directRefIcon, new GUIContent("Icon Texture:", "A direct reference to the icon texture."), 10, 130);
                        }
                        else
                        {
                            EditorUtils.TextField(ref myTarget.overTimeEffects[i].iconPath, new GUIContent("Icon Path:", "The resource path used to load the icon texture."), 10, 130);
                        }
                        EditorUtils.FloatField(ref myTarget.overTimeEffects[i].duration, new GUIContent("Duration:", "The duration of this effect in seconds."), 10, false, 1, 130, "sec");
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].permanent, new GUIContent("Permanent Effect:", "Whether this effect has infinite duration."), 10, 130);
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].canBeExtended, new GUIContent("Can be Extended:", "Whether the duration increases when the effect is applied again."), 10, 130);
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].canBeRefreshed, new GUIContent("Can be Refreshed:", "Whether the duration resets to maximum when the effect is applied again."), 10, 130);
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].canBeDispelled, new GUIContent("Can be Dispelled:", "Whether this effect can be dispelled."), 10, 130);

                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].layered, new GUIContent("Layered:", "Whether this effect can stack when applied multiple times."), 10, 130);
                        if (myTarget.overTimeEffects[i].layered)
                        {
                            EditorUtils.IntField(ref myTarget.overTimeEffects[i].maxLayer, new GUIContent("Maximum Layers:", "The maximum number of stacks this effect can have."), 10, true, 99, 130);
                        }
                        else
                        {
                            myTarget.overTimeEffects[i].maxLayer = 1;
                        }
                        EditorUtils.ColorField(ref myTarget.overTimeEffects[i].backGroundColor, new GUIContent("Background Color:", "The background color of this effect in the UI."), 10, 130);
                        EditorUtils.ColorField(ref myTarget.overTimeEffects[i].frameColor, new GUIContent("Frame Color:", "The frame color of this effect in the UI."), 10, 130);


                        EditorUtils.PreBox(new GUIContent("Custom Data: (" + myTarget.overTimeEffects[i].mCustomField.Count + ")", "Custom data can be prefabs, audio clip, model, texture, etc¡­ Custom data such as prefabs, audio clips, models, or textures.These can be accessed via<OverTimeEffect>().GetCustomObject<T>(string key)."), 10, EditorUtils._buttonColor, 130);

                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Copy), "Copy this effect list to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            CopiedCustomData.Clear();
                            for (int x = 0; x < myTarget.overTimeEffects[i].mCustomField.Count; x++)
                            {
                                CopiedCustomData.Add(myTarget.overTimeEffects[i].mCustomField[x].Copy());
                            }
                            EditorUtils.ShowBoxNotification("Custom data list copied to clipboard!");
                        }
                        if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Paste), "Paste this effect list to clipboard."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                        {
                            EditorGUI.FocusTextInControl(null);
                            myTarget.overTimeEffects[i].mCustomField.Clear();
                            for (int x = 0; x < CopiedCustomData.Count; x++)
                            {
                                myTarget.overTimeEffects[i].mCustomField.Add(CopiedCustomData[x].Copy());
                            }
                        }
                        GUI.backgroundColor = EditorUtils._buttonColor;
                        if (GUILayout.Button(new GUIContent("Add", "Add a new custom data entry."), GUILayout.Width(50)))
                        {
                            CustomField _newData = new CustomField();
                            int _mid = -1;
                            for (int x = 0; x < myTarget.overTimeEffects[i].mCustomField.Count; x++)
                            {
                                int _tid = -1;
                                int.TryParse(myTarget.overTimeEffects[i].mCustomField[x].key.Substring(myTarget.overTimeEffects[i].mCustomField[x].key.Length - 1, 1), out _tid);
                                if (_tid > _mid) _mid = _tid;
                            }
                            int _id = _mid + 1;
                            for (int x = 0; x <= _mid + 1; x++)
                            {
                                bool _exist = false;
                                for (int y = 0; y < myTarget.overTimeEffects[i].mCustomField.Count; y++)
                                {
                                    if (myTarget.overTimeEffects[i].mCustomField[y].key.Contains(x.ToString())) _exist = true;
                                }
                                if (!_exist)
                                {
                                    _id = x;
                                    break;
                                }
                            }
                            _newData.key = "CustomData" + _id;
                            _newData.loadMethod = LoadMethod.DirectReference;
                            _newData.value = null;
                            _newData.loadPath = "";
                            myTarget.overTimeEffects[i].mCustomField.Add(_newData);
                            _valueChanged = true;
                            EditorGUI.FocusTextInControl(null);
                        }
                        EditorUtils.ResetColor();
                        EditorUtils.End(10);

                        for (int u = 0; u < myTarget.overTimeEffects[i].mCustomField.Count; u++)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            myTarget.overTimeEffects[i].mCustomField[u].fold = EditorGUILayout.Foldout(myTarget.overTimeEffects[i].mCustomField[u].fold, myTarget.overTimeEffects[i].mCustomField[u].key);
                            GUI.backgroundColor = EditorUtils._black;
                            if (GUILayout.Button(new GUIContent("X", "Delete this Custom Data."), GUILayout.Width(30)))
                            {
                                myTarget.overTimeEffects[i].mCustomField.RemoveAt(u);
                                EditorGUI.FocusTextInControl(null);
                                ApplyAndSave(myTarget);
                                return;
                            }
                            EditorUtils.ResetColor();
                            GUILayout.Space(50);
                            GUILayout.EndHorizontal();
                            if (myTarget.overTimeEffects[i].mCustomField[u].fold)
                            {
                                EditorUtils.TextField(ref myTarget.overTimeEffects[i].mCustomField[u].key, new GUIContent("Key:", "The key used to access this custom data in code."), 50, 90);
                                EditorUtils.Pre(new GUIContent("Load Method:", "The method used to load this custom data."), 50, 90);
                                myTarget.overTimeEffects[i].mCustomField[u].loadMethod = (LoadMethod)EditorGUILayout.EnumPopup(myTarget.overTimeEffects[i].mCustomField[u].loadMethod);
                                EditorUtils.End(50);
                                if (myTarget.overTimeEffects[i].mCustomField[u].loadMethod == LoadMethod.DirectReference)
                                {
                                    EditorUtils.ObjectField(ref myTarget.overTimeEffects[i].mCustomField[u].value, new GUIContent("Data Object:", "A direct reference to the data object."), 50, 90);
                                }
                                else
                                {
                                    EditorUtils.TextField(ref myTarget.overTimeEffects[i].mCustomField[u].loadPath, new GUIContent("Load Path:", "The string path used to load the data object."), 50, 90);
                                }
                            }
                        }

#if MASTER_HEALTH_BAR
                        EditorUtils.IntSlider(ref myTarget.overTimeEffects[i].barEffectType, -1, 2, new GUIContent("Bar Effect Type:"), "", 10, 130);
                        EditorUtils.ColorField(ref myTarget.overTimeEffects[i].barEffctColor, new GUIContent("Bar Effetc Color:", "The VFX color of this effect in the HealthBar."), 10, 130);
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].endTime, new GUIContent("Is Endtime Visible:"), 10, 130);
                        if (myTarget.overTimeEffects[i].endTime)
                        {
                            EditorUtils.ColorField(ref myTarget.overTimeEffects[i].timeColor, new GUIContent("Endtime Color:", "The color of this endtime."), 30, 130);
                        }
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].fadeVisible, new GUIContent("Is Fade Effeect Visible:"), 10, 130);
                        if (myTarget.overTimeEffects[i].fadeVisible)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(30);
                            GUILayout.Label("Icon Fade Type:", GUILayout.Width(130));
                            GUI.color = Color.white;
                            myTarget.overTimeEffects[i].fadeType = (OverTimeEffect.FadeType)EditorGUILayout.EnumPopup(myTarget.overTimeEffects[i].fadeType, GUILayout.Width(150));
                            GUILayout.EndHorizontal();
                        }
                        EditorUtils.Toggle(ref myTarget.overTimeEffects[i].flash, new GUIContent("Is Icon Flash:"), 10, 130);
                        if (myTarget.overTimeEffects[i].flash)
                        {
                            EditorUtils.FloatSlider(ref myTarget.overTimeEffects[i].flashThreshold, 1.0f, 10.0f, new GUIContent("Flash Threshold Time:"), "s", 30, 130);
                        }
#endif

                        GUILayout.EndVertical();
                        GUILayout.Space(27);
                        GUILayout.EndHorizontal();
                        EditorGUILayout.Separator();
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
                myTarget.GenerateUniqueHash();
                ApplyAndSave(myTarget);
            }
        }

        private bool isMatching(OverTimeEffect _entity, OverTimeEffectObject _target)
        {
            if (string.IsNullOrEmpty(_target.SearchText)) return true;
            if (_target.SearchType == 0)
            {
                return _entity.uid.ToLower().Contains(_target.SearchText.ToLower());
            }
            else if (_target.SearchType == 1)
            {
                return _entity.name.ToLower().Contains(_target.SearchText.ToLower());
            }
            else
            {
                return _entity.category.ToLower().Contains(_target.SearchText.ToLower());
            }
        }

    }
}
