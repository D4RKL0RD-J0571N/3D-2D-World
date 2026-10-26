using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace SoftKitty.MasterNavigationMap
{
    [System.Serializable]
    public class MapSetting
    {
        public string _name;
        public Texture2D _map;
        public float _height;
        public float _ceil;
        public bool _fold = false;
    }

    [System.Serializable]
    public class SceneMapSetting
    {
        public string _scenePath;
        public string _sceneName;
        public string _sceneTitle;
        public List<MapSetting> _maps = new List<MapSetting>();
        public List<SubMapSetting> _subMaps = new List<SubMapSetting>();
        public Vector3 _bottomLeft;
        public Vector3 _topRight;
        public bool _fogOfWarEnable = false;
        public int _fogOfWarWidth = 256;
        public int _fogOfWarHeight = 256;
        public float _fogOfWarRadius = 0.05F;
        public Color _fogOfWarColor = Color.black;
        public bool _fogOfWarWorldMap = true;
        public bool _fogOfWarMiniMap = true;
        public bool _hideIconsInFog = true;
        public Color _backgroundColor = Color.black;
        public bool _fold = false;
        public bool _subMapFold = false;
    }

    [System.Serializable]
    public class SubMapSetting
    {
        public string uid;
        public string _areaTitle;
        public List<MapSetting> _maps = new List<MapSetting>();
        public Vector3 _bottomLeft;
        public Vector3 _topRight;
        public bool _fold = false;
    }

    [System.Serializable]
    public class IconGroupSetting
    {
        public string _name;
        public Texture2D _icon;
        public bool _visisbleDefault = true;
        public bool _fold = false;
    }

    [System.Serializable]
    public class CustomMarkerSetting
    {
        public string _uid;
        public string _defaultName;
        public Texture2D _icon;
        public bool _fold = false;
    }

    [System.Serializable]
    public class CustomMarkerData
    {
        public string _name;
        public string _settingUid;
        public string _uid;
        public Vector3 _pos;
    }

    [System.Serializable]
    public class CustomMarkerJsonRoot
    {
        public CustomMarkerData[] Data;
    }

    [CreateAssetMenu(fileName = "Map_Settings", menuName = "Soft Kitty/Data Objects/Map Settings")]
    public class MMN_Settings : DataObject
    {
        #region Variables
        public const string CONFIG_NAME = "com.SoftKitty.MapNavigationSystem.settings";
        public override string DataName() { return "Map Navigation"; }
        public override string TypeString() { return "SoftKitty.MasterNavigationMap.MMN_Settings"; }
        public enum MapModes
        {
            StaticMap,
            DynamicMap
        }
        public MapModes MapMode;
        public enum TextureSize
        {
            Square_1k,
            Square_2k,
            Square_4k
        }
        public TextureSize RtSize;
        public float RtCameraHeight = 400F;
        public float RtCameraDepth = 500F;
        public int WorldMapSize = 1000;
        public bool UnlimitedWorldMap = true;
        public LayerMask RtLayerMask;
        public bool RtStylizedRendering = true;
        public Material RtStylizedeMaterial;
        public bool RtDepthOnly = false;

        public List<SceneMapSetting> SceneMaps = new List<SceneMapSetting>();
        public List<IconGroupSetting> IconGroups = new List<IconGroupSetting>();
        public Texture2D PlayerIcon;
        public enum PlayerOrientations
        {
            PlayerTransform,
            PlayerCamera
        }
        public PlayerOrientations PlayerOrientation;
        public bool EnableNavigationPath=true;
        public float WorldMapDefaultZoom =0.5f;
        public float WorldMapMaxmiumZoomIn = 3F;
        public float MiniMapDefaultZoom = 0.5F;
        public float MiniMapMaxmiumZoomIn = 5F;
        public bool PanBeyondBounds = false;

        public bool DisplaySceneTitle = true;
        public bool LockIconScale = false;
        public bool MiniLockIconScale = false;
        public bool ToggleCompassButton = false;
        public bool LockPlayerNorth = false;
        public bool AllowZoomInOut = false;
        public bool MiniDisplaySceneTitle = true;
        public bool MiniDisplayCoordinate = true;
        public bool MiniMapRotateWithCamera = false;
        public bool ShowTrackArrowWhenNavigate=true;
        public Color NavigationLineColor = new Color(0F,0.6F,1F,1F);
        public MouseButton StartNavigationMouseButton;
        public KeyCode MapScrollLeftKey;
        public KeyCode MapScrollRightKey;
        public KeyCode MapScrollUpKey;
        public KeyCode MapScrollDownKey;
        public KeyCode MapMarkerKey;
        public MouseButton MapMarkerButton;
        public bool EnableScroll = true;
        public float ScrollSpeed = 1F;
        public bool EnableMarkers = true;
        public int MaximumMarkers = 10;
        public MarkerOutNumberMethods MarkerOutNumberMethod;
        public enum MarkerOutNumberMethods
        {
            ReplaceTheFirstOne,
            ReplaceTheLastOne,
            Callback,
            DoNothing
        }
        public bool EnableCustomMarker;
        public MouseButton CustomMarkerConfirmButton;
        public MouseButton CustomMarkerCancelButton;
        public KeyCode CustomMarkerCancelKey;
        public bool AllowPlayerToRenameCustomMarker = true;
        public List<CustomMarkerSetting> CustomMarkerSettings = new List<CustomMarkerSetting>();


        private static MMN_Settings instance;
        #endregion

        #region Methods
        private void OnEnable()
        {
            if (instance == null) instance = this;
        }


        public void ApplyData()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        #endregion
    }
}
