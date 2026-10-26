using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

namespace SoftKitty
{
    public enum EditorIcon
    {
        Copy,
        Paste,
        Up,
        Down,
        Warning,
        Reset,
        Line,
        DataLogo,
        GraphLogo,
        DarkBox,
        EntityLogo,
        LightBox,
        TitleBox,
        Dot,
        GroupPanel,
        Duplicate,
        Button,
        DataIcon,
        visible0,
        visible1,
        Add,
        Help,
        Lock,
        Socket
    }
    public class EditorUtils : UnityEditor.Editor
    {
        public static Color _activeColor = new Color(0.1F, 0.3F, 0.5F);
        public static Color _disableColor = new Color(0F, 0.1F, 0.3F);
        public static Color _actionColor = new Color(1F, 1F, 0F);
        public static Color _activeColor2 = new Color(0.1F, 0.1F, 0.1F);
        public static Color _disableColor2 = new Color(0.24F, 0.26F, 0.28F);
        public static Color _titleColor = new Color(0.3F, 0.5F, 1F);
        public static Color _buttonColor = new Color(0.2F, 0.9F, 0.6F);
        public static Color _tagColor = new Color(0.4F, 0.45F, 0.45F);
        public static Color _red= new Color(0.9F, 0.35F, 0.35F);
        public static Color _black = new Color(0.3F, 0.3F, 0.3F);
        public static Color _gray = new Color(0.7F, 0.7F, 0.7F);
        public static Color _codeColor = new Color(0.86F, 0.86F, 0.82F);
        private static string mPath = "";
        private static string dPath = "";
        public static Dictionary<EditorIcon, Texture> IconTextures = new Dictionary<EditorIcon, Texture>();
        public static Dictionary<string, Texture> CustomTextures = new Dictionary<string, Texture>();


        public static GUIStyle _titleButtonStyle
        {
            get
            {
                var _style = new GUIStyle();
                Texture2D _bg= (Texture2D)GetTexture(EditorIcon.TitleBox);
                _style.normal.background = _bg;
                _style.normal.textColor = new Color(0.82F, 0.82F, 0.82F, 1F);
                _style.active.background = _bg;
                _style.active.textColor = new Color(1F, 1F, 1F, 1F);
                _style.hover.background = _bg;
                _style.hover.textColor = new Color(1F, 1F, 1F, 1F);
                _style.padding = new RectOffset() { bottom = 5, left = 10, top = 4, right = 10 };
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.alignment = TextAnchor.MiddleLeft;
                _style.richText = true;
                return _style;
            }
        }

        

        public static GUIStyle _titleBgStyle
        {
            get
            {
                var _style = new GUIStyle();
                Texture2D _bg = (Texture2D)GetTexture(EditorIcon.TitleBox);
                _style.normal.background = _bg;
                _style.normal.textColor = new Color(0.82F, 0.82F, 0.82F, 1F);
                _style.active.background = _bg;
                _style.active.textColor = new Color(1F, 1F, 1F, 1F);
                _style.hover.background = _bg;
                _style.hover.textColor = new Color(1F, 1F, 1F, 1F);
                _style.padding = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.alignment = TextAnchor.MiddleLeft;
                _style.richText = true;
                return _style;
            }
        }

        public static GUIStyle _groupStyle
        {
            get
            {
                var _style = new GUIStyle();
                Texture2D _bg = (Texture2D)GetTexture(EditorIcon.GroupPanel);
                _style.normal.background = _bg;
                _style.normal.textColor = new Color(0.82F, 0.82F, 0.82F, 1F);
                _style.active.background = _bg;
                _style.active.textColor = new Color(0.82F, 0.82F, 0.82F, 1F);
                _style.padding = new RectOffset() { bottom = 5, left = 10, top = 4, right = 10 };
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.alignment = TextAnchor.UpperLeft;
                return _style;
            }
        }

        public static GUIStyle _toolButtonStyle
        {
            get
            {
                var _style = new GUIStyle();
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 2, right = 0 };
                _style.padding = new RectOffset() { bottom = 0, left = 0, top = 2, right = 0 };
                return _style;
            }
        }

        public static GUIStyle _darkBoxStyle
        {
            get
            {
                GUIStyle _style = new GUIStyle();
                _style.normal.background = (Texture2D)GetTexture(EditorIcon.DarkBox);
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.padding = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.normal.textColor = Color.white;
                return _style;
            }
        }

        public static GUIStyle _iconStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.box);
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.padding = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                return _style;
            }
        }

        public static GUIStyle _helpStyle
        {
            get
            {
                var _style = new GUIStyle(EditorStyles.helpBox);
                _style.alignment = TextAnchor.MiddleLeft;
                _style.richText = true;
                return _style;
            }
        }

        public static GUIStyle _helpStyleNoRich
        {
            get
            {
                var _style = new GUIStyle(EditorStyles.helpBox);
                _style.alignment = TextAnchor.MiddleLeft;
                _style.richText = false;
                return _style;
            }
        }

        public static GUIStyle _richLabelStyle
        {
            get
            {
                var _style = new GUIStyle(EditorStyles.label);
                _style.richText = true;
                return _style;
            }
        }

        public static GUIStyle _titleStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.label);
                _style.alignment = TextAnchor.LowerRight;
                return _style;
            }
        }

        public static GUIStyle _centerStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.label);
                _style.alignment = TextAnchor.MiddleCenter;
                return _style;
            }
        }

        public static GUIStyle _boxStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.MiddleCenter;
                _style.padding.top = 0;
                _style.padding.bottom = 0;
                return _style;
            }
        }

        public static GUIStyle _boxRichStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.MiddleCenter;
                _style.padding.top = 0;
                _style.padding.bottom = 0;
                _style.richText = true;
                return _style;
            }
        }

        public static GUIStyle _smallBoxStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.MiddleCenter;
                _style.border = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.padding = new RectOffset() { bottom = 0, left = 0, top = 0, right = 0 };
                _style.fontStyle = FontStyle.Italic;
                _style.fontSize = 8;
                return _style;
            }
        }

        public static GUIStyle _logoStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.MiddleLeft;
                _style.padding.top = 0;
                _style.padding.bottom = 0;
                _style.padding.left = 0;
                _style.padding.right = 0;
                return _style;
            }
        }

        public static GUIStyle _boxLeftStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.box);
                _style.alignment = TextAnchor.MiddleLeft;
                _style.padding.top = 0;
                _style.padding.bottom = 0;
                _style.normal.textColor = Color.white;
                return _style;
            }
        }

        public static GUIStyle _boldStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.label);
                _style.fontStyle = FontStyle.Bold;
                return _style;
            }
        }

        public static GUIStyle _boldTitleStyle
        {
            get
            {
                var _style = new GUIStyle(GUI.skin.label);
                _style.fontStyle = FontStyle.Bold;
                _style.alignment = TextAnchor.MiddleCenter;
                return _style;
            }
        }

        public static void ShowBoxNotification(string message)
        {
            EditorWindow window = EditorWindow.mouseOverWindow;

            if (window == null)
                window = EditorWindow.focusedWindow;

            if (window != null)
            {
                window.ShowNotification(new GUIContent(message),1F);
            }
            else
            {
                SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(message),1F);
            }
        }

        public static string SharedEditorPath
        {
            get
            {
                if (string.IsNullOrEmpty(mPath)) {
                    var filter = "EditorUtils";
                    var guids = AssetDatabase.FindAssets(filter);
                    var hasGuids = guids.Length > 0;
                    if (hasGuids)
                    {
                        foreach (var obj in guids)
                        {
                            string _path = AssetDatabase.GUIDToAssetPath(obj);
                            if (_path.Contains("SoftKitty"))
                            {
                                mPath = _path.Replace("EditorUtils.cs", "");
                                return mPath;
                            }
                        }
                        mPath = "Assets/SoftKitty/Shared/Editor/";
                    }
                    else
                    {
                        mPath = "Assets/SoftKitty/Shared/Editor/";
                    }
                }
                return mPath;
            }
        }

        public static string DataPath
        {
            get
            {
                if (string.IsNullOrEmpty(dPath))
                {
                    var filter = "EditorUtils";
                    var guids = AssetDatabase.FindAssets(filter);
                    var hasGuids = guids.Length > 0;
                    if (hasGuids)
                    {
                        foreach (var obj in guids)
                        {
                            string _path = AssetDatabase.GUIDToAssetPath(guids[0]);
                            if (_path.Contains("SoftKitty"))
                            {
                                dPath = _path.Replace("Shared/Editor/EditorUtils.cs", "Data/");
                                return dPath;
                            }
                        }
                        dPath = "Assets/SoftKitty/Data/";
                    }
                    else
                    {
                        dPath = "Assets/SoftKitty/Data/";
                    }
                }
                return dPath;
            }
        }

        public static void Save()
        {
            GUI.backgroundColor = _gray;
            GUILayout.BeginHorizontal(_titleButtonStyle);
            GUILayout.Space(5);
            GUI.backgroundColor =_titleColor;
            if (GUILayout.Button("Save", GUILayout.Width(100))) {
                AssetDatabase.SaveAssets();
            }
            GUILayout.Label("Save your changes");
            ResetColor();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public static void Document(string _path= "master-combat-core/overview", bool _community=false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.3F, 0.8F, 0.8F,1F);
            if (GUILayout.Button(new GUIContent("  Open Online Documentation  ", "Opens the official online documentation, including guides, node reference, and examples."))){
                Application.OpenURL("https://www.soft-kitty.com/docs/"+ _path);
            }
            if (_community) {
                if (GUILayout.Button(new GUIContent("  Community (GitHub Discussions)  ", "Opens the official GitHub Discussions for sharing and discovering Graphs.")))
                {
                    Application.OpenURL("https://github.com/blakeseow-cloud/Soft-Kitty-Documentation/discussions/");
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        public static Texture GetTexture(EditorIcon _icon)
        {
            if (IconTextures.ContainsKey(_icon))
            {
                return IconTextures[_icon];
            }
            else if (AssetDatabase.AssetPathExists(SharedEditorPath + _icon.ToString() + ".png"))
            {
                Texture _tex = (Texture)AssetDatabase.LoadAssetAtPath(SharedEditorPath + _icon.ToString() + ".png", typeof(Texture));
                IconTextures.Add(_icon, _tex);
                return IconTextures[_icon];
            }
            return null;
        }

        public static Texture GetTexture(string _textureName)
        {
            if (CustomTextures.ContainsKey(_textureName))
            {
                return CustomTextures[_textureName];
            }
            else if (AssetDatabase.AssetPathExists(SharedEditorPath + _textureName + ".png"))
            {
                Texture _tex = (Texture)AssetDatabase.LoadAssetAtPath(SharedEditorPath + _textureName + ".png", typeof(Texture));
                CustomTextures.Add(_textureName, _tex);
                return CustomTextures[_textureName];
            }
            return null;
        }

        public static void TitleLogo(Texture _logo,int _space,int _width=-1, int _height=-1)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUI.backgroundColor = Color.gray;
            GUILayout.BeginHorizontal(_groupStyle);
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.white;
            List<GUILayoutOption> _options = new List<GUILayoutOption>();
            if (_width > -1) _options.Add(GUILayout.Width(_width));
            if (_height > -1) _options.Add(GUILayout.Height(_height));
            if (_options.Count > 0)
                GUILayout.Box(_logo,_logoStyle, _options.ToArray());
            else
                GUILayout.Box(_logo, _logoStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();
        }
        public static void Warnning(string _text, int _space)
        {
            GUI.backgroundColor = Color.red;
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            EditorGUILayout.LabelField(_text, _helpStyle);
            GUILayout.Space(Mathf.Min(20, _space));
            GUILayout.EndHorizontal();
            ResetColor();
        }

        public static void HelpInfo(string _text, int _space, bool _richText=true)
        {
            GUI.backgroundColor = new Color(0.8F, 0.7F, 0.2F,1F);
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            EditorGUILayout.LabelField(_text, _richText?_helpStyle:_helpStyleNoRich);
            GUILayout.Space(Mathf.Min(20, _space));
            GUILayout.EndHorizontal();
            ResetColor();
        }

        public static void Info(string _text, int _space)
        {
            GUI.backgroundColor = Color.black;
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            EditorGUILayout.LabelField(_text, _helpStyle);
            GUILayout.Space(Mathf.Min(20, _space));
            GUILayout.EndHorizontal();
            ResetColor();
        }

        public static void BoxInfo(string _text, int _space=0)
        {
            GUI.backgroundColor = _black;
            GUILayout.BeginHorizontal(_titleBgStyle);
            GUILayout.Space(_space);
            EditorGUILayout.LabelField(_text, _richLabelStyle);
            GUILayout.Space(Mathf.Min(20, _space));
            GUILayout.EndHorizontal();
            ResetColor();
        }

        public static void BoxInfo(GUIContent _text, int _space = 0, bool _copy=false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.BeginHorizontal(_titleBgStyle);
            EditorGUILayout.LabelField(_text, _richLabelStyle);
            if (_copy)
            {
                if (GUILayout.Button(new GUIContent(GetTexture(EditorIcon.Copy), "Copy the string."), _toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    GUIUtility.systemCopyBuffer = _text.text;
                    ShowBoxNotification("Text string copied!");
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
            ResetColor();
        }


        public static void Vector3Field(ref Vector3 _value, GUIContent _name, int _space)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name);
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(_space + 20);
            GUILayout.Label("x:", GUILayout.Width(20));
            _value.x = EditorGUILayout.FloatField(_value.x, GUILayout.Width(50));
            GUILayout.Label("y:", GUILayout.Width(20));
            _value.y = EditorGUILayout.FloatField(_value.y, GUILayout.Width(50));
            GUILayout.Label("z:", GUILayout.Width(20));
            _value.z = EditorGUILayout.FloatField(_value.z, GUILayout.Width(50));
            GUILayout.Space(10);
            GUI.backgroundColor = Selection.activeTransform != null ? _titleColor : Color.gray;
            if (GUILayout.Button("Copy from selected Transform"))
            {
                _value = Selection.activeTransform.position;
            }
            ResetColor();
            End(_space);

        }

        public static bool Vector3FieldLimited(ref Vector3 _value, GUIContent _name, int _space, Vector2 _xRange, Vector2 _yRange, Vector2 _zRange)
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
            _value.x = EditorGUILayout.FloatField(_value.x, GUILayout.Width(50));
            GUI.backgroundColor = (_value.y < _yRange.x || _value.y > _yRange.y) ? Color.red : Color.white;
            GUILayout.Label("y:", GUILayout.Width(20));
            _value.y = EditorGUILayout.FloatField(_value.y, GUILayout.Width(50));
            GUI.backgroundColor = (_value.z < _zRange.x || _value.z > _zRange.y) ? Color.red : Color.white;
            GUILayout.Label("z:", GUILayout.Width(20));
            _value.z = EditorGUILayout.FloatField(_value.z, GUILayout.Width(50));
            GUILayout.Space(10);
            GUI.backgroundColor = Selection.activeTransform != null ? _titleColor : Color.gray;
            if (GUILayout.Button("Copy from selected Transform"))
            {
                _value = Selection.activeTransform.position;
            }
            ResetColor();
            End(_space);
            return (_value.x < _xRange.x || _value.x > _xRange.y || _value.y < _yRange.x || _value.y > _yRange.y || _value.z < _zRange.x || _value.z > _zRange.y);
        }

        public static void ColorField(ref Color _value, GUIContent _name, int _space, int _width = 150, bool _reset = false, Color _default = default(Color))
        {
            Pre(_name, _space, _width);
            _value = EditorGUILayout.ColorField(_value, GUILayout.Width(150));
            if (_reset)
            {
                GUI.backgroundColor = Color.gray;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Reset), "Reset the value to default."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    _value = _default;
                }
                ResetColor();
            }
            End(_space);
        }

        public static void Pre(GUIContent _name, int _space, int _width=150)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUILayout.Label(_name, GUILayout.Width(_width));
        }

        public static void PreBox(GUIContent _name, int _space, Color _color, int _width = 150)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUI.backgroundColor = _color;
            GUI.color = Color.white;
            GUILayout.Box(_name, _boxLeftStyle, GUILayout.Width(_width) );
            GUI.backgroundColor = Color.white;
        }

        public static void End(int _space)
        {
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
        }

        public static void FloatSlider(ref float _value, float _min, float _max, GUIContent _name, string _unit, int _space, int _labelWidth=150, bool _reset = false, float _default = 0F)
        {
            Pre(_name, _space, _labelWidth);
            _value = GUILayout.HorizontalSlider(_value, _min, _max);
            GUILayout.Label(_value.ToString("0.00") + _unit, GUILayout.Width(40));
            if (_reset)
            {
                GUI.backgroundColor = Color.gray;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Reset), "Reset the value to default."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    _value = _default;
                }
                ResetColor();
            }
            End(_space);
        }

        public static void FloatField(ref float _value, GUIContent _name, int _space, bool _reset = false,float _default=0F, int _labelWidth = 150,string _unit="")
        {
            Pre(_name, _space, _labelWidth);
            _value = EditorGUILayout.FloatField(_value,GUILayout.Width(50));
            if (_unit!="") {
                GUILayout.Label(_unit,GUILayout.Width(50),GUILayout.Height(17));
            }
            if (_reset)
            {
                GUI.backgroundColor = Color.gray;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Reset), "Reset the value to default."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    _value = _default;
                }
                ResetColor();
            }
            End(_space);
        }

        public static void IntField(ref int _value, GUIContent _name, int _space, bool _reset = false, int _default = 0, int _labelWidth = 150, string _unit = "")
        {
            Pre(_name, _space, _labelWidth);
            _value = EditorGUILayout.IntField(_value, GUILayout.Width(50));
            if (_unit != "")
            {
                GUILayout.Label(_unit, GUILayout.Width(50), GUILayout.Height(17));
            }
            if (_reset)
            {
                GUI.backgroundColor = Color.gray;
                if (GUILayout.Button(new GUIContent(EditorUtils.GetTexture(EditorIcon.Reset), "Reset the value to default."), EditorUtils._toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    _value = _default;
                }
                ResetColor();
            }
            End(_space);
        }

        public static void PopUp(ref int _value, string [] _options, GUIContent _name, int _space, int _width = 150, int _popWidth=100)
        {
            Pre(_name, _space, _width);
            int _tempValue = _value;
            _tempValue = EditorGUILayout.Popup("", _tempValue, _options, GUILayout.Width(_popWidth));
            if (_tempValue != _value) _value = _tempValue;
            ResetColor();
            End(_space);
        }

        public static void PopUpIntField(ref int _value,ref int _intValue,  string[] _options, GUIContent _name, int _space, Color _popColor,Color _intColor, int _width = 150, int _popWidth = 100, int _intWidth=80 )
        {
            Pre(_name, _space, _width);
            GUI.backgroundColor = _popColor;
            int _tempValue = _value;
            _tempValue = EditorGUILayout.Popup("", _tempValue, _options, GUILayout.Width(_popWidth));
            if (_tempValue != _value) _value = _tempValue;
            GUI.backgroundColor = _intColor;
            _intValue = EditorGUILayout.IntField(_intValue, GUILayout.Width(_intWidth));
            ResetColor();
            End(_space);
        }

        public static void IntSlider(ref int _value, int _min, int _max, GUIContent _name, string _unit, int _space, int _width = 150)
        {
            Pre(_name, _space, _width);
            _value = EditorGUILayout.IntSlider(_value, _min, _max);
            GUILayout.Label(_unit, GUILayout.Width(50));
            GUILayout.Space(Mathf.Max(0, _space - 40));
            GUILayout.EndHorizontal();
        }

        public static void Curve(ref AnimationCurve _value,GUIContent _name, int _space, int _width = 150, int _curveWidth = 150)
        {
            Pre(_name, _space, _width);
            _value = EditorGUILayout.CurveField(_value, GUILayout.Width(_curveWidth));
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
        }

        public static void Texture2DField(ref Texture2D _object, GUIContent _name, int _space, int _width = 150)
        {
            Pre(_name, _space, _width);
            _object = (Texture2D)EditorGUILayout.ObjectField(_object, typeof(Texture2D), false);
            End(_space);
        }

        public static void TextureField(ref Texture _object, GUIContent _name, int _space, int _width = 150)
        {
            Pre(_name, _space, _width);
            _object = (Texture)EditorGUILayout.ObjectField(_object, typeof(Texture), false);
            End(_space);
        }

        public static void ObjectField(ref Object _object, GUIContent _name, int _space, int _width = 150)
        {
            Pre(_name, _space, _width);
            _object = (Object)EditorGUILayout.ObjectField(_object, typeof(Object), false);
            End(_space);
        }

        public static void Toggle(ref bool _value, GUIContent _name, int _space, int _width = 150, string _helpUrl="")
        {
            Pre(_name, _space, _width);
            _value = GUILayout.Toggle(_value, "", GUILayout.Width(25), GUILayout.Height(15));
            if (_helpUrl != "")
            {
                GUI.backgroundColor = _buttonColor*0.5F;
                if (GUILayout.Button("?", GUILayout.Width(21)))
                {
                    Application.OpenURL("https://www.soft-kitty.com/docs/" + _helpUrl);
                }
                GUI.backgroundColor = Color.white;
            }
            End(_space);
        }

        public static void TextField(ref string _text, GUIContent _name, int _space,int _width=150, bool _copy=false)
        {
            Pre(_name, _space, _width);
            _text = GUILayout.TextArea(_text,GUILayout.Height(17));
            ResetColor();
            if (_copy) {
                if (GUILayout.Button(new GUIContent(GetTexture(EditorIcon.Copy), "Copy the string."), _toolButtonStyle, GUILayout.Width(15)))
                {
                    EditorGUI.FocusTextInControl(null);
                    GUIUtility.systemCopyBuffer = _text;
                    ShowBoxNotification("Text string copied!");
                }
            }
            End(_space);
        }
        public static void Label(GUIContent _text, int _space, int _width = 0)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            if (_width > 0)
                GUILayout.Label(_text, _richLabelStyle, GUILayout.Width(_width));
            else
                GUILayout.Label(_text, _richLabelStyle);
            GUILayout.EndHorizontal();
        }

        public static void KeyButton(ref KeyCode _value, GUIContent _name, int _space)
        {
            Pre(_name, _space);
            _value = (KeyCode)EditorGUILayout.EnumPopup(_value, GUILayout.Width(150));
            End(_space);
        }


        public static void TitleButtonWithHelp(GUIContent _text, ref bool _toggle, int _endSpace = 20, bool _header = true, int _startSpace = 0, string _url="")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(_startSpace);
            GUI.backgroundColor = _toggle ? _activeColor2 : _disableColor2;
            if (_header) GUILayout.Label(_toggle ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button((_header ? "" : (_toggle ? "[-] " : "[+] ")) + _text, _titleButtonStyle))
            {
                _toggle = !_toggle;
            }
            
            if (_url!="")
            {
                GUI.backgroundColor = _buttonColor;
                if (GUILayout.Button(GetTexture( EditorIcon.Help), GUILayout.Width(21), GUILayout.Height(21)))
                {
                    Application.OpenURL("https://www.soft-kitty.com/docs/"+ _url);
                }
            }
            ResetColor();
            GUILayout.Space(_endSpace);
            GUILayout.EndHorizontal();
        }


        public static bool TitleButton(GUIContent _text, ref bool _toggle, int _endSpace=20, bool _header=true,int _startSpace = 0,bool _delete=false)
        {
            bool _result = false;
            GUILayout.BeginHorizontal();
            GUILayout.Space(_startSpace);
            GUI.backgroundColor = _toggle ? _activeColor2 : _disableColor2;
            if(_header)GUILayout.Label(_toggle ? "[-]" : "[+]", GUILayout.Width(20));
            if (GUILayout.Button((_header?"":(_toggle ? "[-] " : "[+] "))+ _text, _titleButtonStyle))
            {
                _toggle = !_toggle;
            }
            GUI.backgroundColor = _black;
            if (_delete)
            {
                if (GUILayout.Button("X", GUILayout.Width(25))){
                    _result = true;
                }
            }
            ResetColor();
            GUILayout.Space(_endSpace);
            GUILayout.EndHorizontal();
            return _result;
        }

        public static bool TitleBox(GUIContent _text,Color _color, int _space = 20, bool _button=false, string _buttonText="", int _buttonWidth=100)
        {
            bool _result = false;
            GUILayout.BeginHorizontal();
            GUILayout.Space(_space);
            GUI.color = Color.white;
            GUI.backgroundColor = _color*0.7F;
            GUILayout.Box(_text, _titleButtonStyle, GUILayout.Height(18));
            GUI.backgroundColor = _color;
            if (_button)
            {
                if (GUILayout.Button(_buttonText, GUILayout.Width(_buttonWidth), GUILayout.Height(18)))
                {
                    _result = true;
                }
            }
            ResetColor();
            GUILayout.Space(_space);
            GUILayout.EndHorizontal();
            return _result;
        }

        public static void LayerMaskField(ref LayerMask _value, GUIContent _name, int _space)
        {
            EditorUtils.Pre(_name, _space);
            LayerMask tempMask = EditorGUILayout.MaskField(InternalEditorUtility.LayerMaskToConcatenatedLayersMask(_value), InternalEditorUtility.layers, GUILayout.Width(150));
            _value = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
            EditorUtils.End(_space);
        }

        public static void SubTitleButton(GUIContent _text, ref bool _toggle)
        {
            _toggle = EditorGUILayout.Foldout(_toggle, _text, true);
        }

        public static void ResetColor()
        {
            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
        }

        public static GUIContent TextHint(string _text,string _hint="")
        {
            return new GUIContent(_text, _hint);
        }
    }
}
