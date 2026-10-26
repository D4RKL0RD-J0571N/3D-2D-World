using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty.MasterNavigationMap
{
    public class MapPoint : MonoBehaviour
    {
        #region Variables
        public MapPointData Data;
        public int EntranceOfLayer = -1;
        public bool ShowDebugIcon = true;
        public bool ShowDebugText = true;
        public bool ShowDebugRange = true;
        public bool ClickToOpenSubMap = false;
        public string SubMapUid = "";
        
        private int _state = -1;
        private float _range = 0F;
        private Texture2D _icon;
        private string _text;
        #endregion

        #region Internal Methods

        void Start()
        {
            MapManager.SceneCheck();
            _range = Data.Range;
            _text = Data.Text;
            _icon = Data.Icon;
            MapManager.MapPointCreateCallback(transform, Data, _state);
            if (MapManager.CurrentSceneMapSetting != null)
            {
                if (EntranceOfLayer != -1 && MapManager.CurrentSceneMapSetting._maps.Count > 1)
                {
                    MapManager.RegisterLayerEntrance(MapManager.GetLayerByPosition(transform.position), EntranceOfLayer, this);
                }
            }
        }

        private void StateChange(int _value)
        {
            if (_value != _state)
            {
                _state = _value;
                MapManager.MapPointStateChangeCallback(transform, _state);
            }
        }

        public void UpdateData()
        {
            MapManager.MapPointDataUpdateCallback(transform, Data);
        }

        private void OnDestroy()
        {
            MapManager.MapPointRemoveCallback(transform);
        }
        #endregion

        #region Debug Gizmos
        private void OnDrawGizmos()
        {
            if (Data != null)
            {

#if UNITY_EDITOR
                if (UnityEditor.SceneView.lastActiveSceneView.camera.orthographic) return;
                if (Data.Range > 0f && ShowDebugRange)
                {
                    Gizmos.color = Data.RangeColor;
                    Gizmos.DrawWireSphere(transform.position, Data.Range);

                }

                //if (Data.Icon != null && ShowDebugIcon)
                //{
                //    string _path = UnityEditor.AssetDatabase.GetAssetPath(Data.Icon);
                //    Gizmos.DrawIcon(transform.position, _path, false);
                //    Data.Icon.hideFlags = HideFlags.None;
                //}
                if (Data.Text != "" && ShowDebugText)
                {
                    GUIStyle _newStyle = new GUIStyle();
                    _newStyle.fontStyle = FontStyle.Bold;
                    _newStyle.normal.textColor = Data.TextColor;
                    _newStyle.fontSize = Mathf.Max(8, Data.FontSize - 5);
                    _newStyle.alignment = TextAnchor.MiddleCenter;
                    Gizmos.DrawLine(transform.position, transform.position + Vector3.up * Data.FontSize * 0.5F);
                    UnityEditor.Handles.Label(transform.position + Vector3.up * Data.FontSize * 0.5F, Data.Text, _newStyle);
                }
#endif
            }
        }
        #endregion

        /// <summary>
        /// Get/Set the state of this map point. Setting the value to -1 will hide the state icon.
        /// </summary>
        public int State 
        {
            get
            {
                return _state;
            }
            set
            {
                StateChange(value);
            }
        }

        /// <summary>
        /// Enables or disables the "popping" effect of the map icon to draw the player's attention.
        /// </summary>
        /// <param name="_enable"></param>
        public void SetPoping(bool _enable)
        {
            Data.Poping = _enable;
            UpdateData();
        }

        /// <summary>
        /// Toggles the visibility of the map point.
        /// </summary>
        /// <param name="_visible"></param>
        public void SetVisible(bool _visible)
        {
            Data.Visible = _visible;
            UpdateData();
        }

        /// <summary>
        /// Enables or disables the visibility of the text associated with the map point.
        /// </summary>
        /// <param name="_enable"></param>
        public void Toggletext(bool _enable)
        {
            Data.Text = _enable ? _text : "";
            UpdateData();
        }

        /// <summary>
        /// Sets the content of the text displayed for the map point.
        /// </summary>
        /// <param name="_text"></param>
        public void SetText(string _text)
        {
            Data.Text = _text;
            _text = Data.Text;
            UpdateData();
        }

        /// <summary>
        /// Sets the color of the text.
        /// </summary>
        /// <param name="_color"></param>
        public void SetTextColor(Color _color)
        {
            Data.TextColor = _color;
            UpdateData();
        }

        /// <summary>
        /// Sets the font size of the text.
        /// </summary>
        /// <param name="_fontSize"></param>
        public void SetTextSize(int _fontSize)
        {
            Data.FontSize = _fontSize;
            UpdateData();
        }

        /// <summary>
        /// Enables or disables the visibility of the range circle around the map point.
        /// </summary>
        /// <param name="_enable"></param>
        public void ToggleRange(bool _enable)
        {
            Data.Range = _enable ? _range : 0F;
            UpdateData();
        }

        /// <summary>
        /// Sets the radius of the range circle.
        /// </summary>
        /// <param name="_radius"></param>
        public void SetRangeRadius(float _radius)
        {
            Data.Range = _radius;
            _range = Data.Range;
            UpdateData();
        }

        /// <summary>
        /// Sets the color of the range circle.
        /// </summary>
        /// <param name="_color"></param>
        public void SetRangeColor(Color _color)
        {
            Data.RangeColor = _color;
            UpdateData();
        }

        /// <summary>
        /// Enables or disables the visibility of the map icon.
        /// </summary>
        /// <param name="_enable"></param>
        public void ToggleIcon(bool _enable)
        {
            Data.Icon = _enable ? _icon : null;
            UpdateData();
        }

        /// <summary>
        /// Sets the texture of the map icon.
        /// </summary>
        /// <param name="_icon"></param>
        public void SetIcon(Texture2D _icon)
        {
            Data.Icon = _icon;
            _icon = Data.Icon;
            UpdateData();
        }

        /// <summary>
        /// Sets the color of the map icon.
        /// </summary>
        /// <param name="_color"></param>
        public void SetIconColor(Color _color)
        {
            Data.IconColor = _color;
            UpdateData();
        }

        /// <summary>
        /// Sets the pixel size of the map icon.
        /// </summary>
        /// <param name="_pixelSize"></param>
        public void SetIconSize(int _pixelSize)
        {
            Data.IconSize = _pixelSize;
            UpdateData();
        }


       

       
    }
}