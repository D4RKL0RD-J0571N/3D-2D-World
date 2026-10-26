using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace SoftKitty.MasterNavigationMap
{
    #region Data Class
    public enum MouseButton
    {
        Left_Mouse_Button,
        Right_Mouse_Button,
        Middle_Mouse_Button
    }
    [System.Serializable]
    public class MapPointPackage
    {
        public MapPointData Data;
        public int State;
        public MapPointPackage(MapPointData _data,int _state)
        {
            Data = _data;
            State = _state;
        }
    }

    [System.Serializable]
    public class MapPointData
    {
        public bool DisplayIcon = false;
        public Texture2D Icon;
        public Vector2 IconOffset;
        public string Text;
        public bool DisplayTextInNavigationBar = false;
        public bool DisplayTextInMiniMap = false;
        public bool DisplayTextInWorldMap = true;
        public bool DisplayTextIn3dWorld = false;
        public string HintText;
        public Vector2 TextOffset;
        public float Range = 0F;
        public Color IconColor;
        public Color TextColor;
        public Color RangeColor;
        public float DisapperDistance;
        public Texture2D[] StateIcons;
        public int GroupId;
        public int IconSize = 64;
        public int FontSize = 16;
        public bool AlwaysKeepSize = false;
        public bool AlwaysKeepSizeInNavigagtionBar = false;
        public bool Poping = false;
        public bool Visible = true;
        public bool ShowIconIn3dWorld = false;
        public float IconOffsetIn3dWorld = 1f;
        public float IconDisappearDistanceIn3dWorld = 50F;

        

        public MapPointData(){
            DisplayIcon = false;
            Icon = null;
            IconOffset = Vector2.zero;
            Text = "";
            DisplayTextInNavigationBar = false;
            DisplayTextInMiniMap = false;
            DisplayTextInWorldMap = true;
            DisplayTextIn3dWorld = false;
            HintText = "";
            TextOffset = Vector2.zero;
            Range = 0F;
            IconColor = Color.white;
            TextColor = Color.white;
            RangeColor = Color.white;
            DisapperDistance = 400F;
            StateIcons = new Texture2D[0];
            GroupId = 0;
            IconSize = 64;
            FontSize = 16;
            AlwaysKeepSize = false;
            AlwaysKeepSizeInNavigagtionBar = false;
            Poping = false;
            Visible = true;
            ShowIconIn3dWorld = false;
            IconOffsetIn3dWorld = 1F;
            IconDisappearDistanceIn3dWorld = 50F;
        }
    }
    #endregion

    public class MapManager : MonoBehaviour
    {
        #region Variables
        public static MMN_Settings CurrentSetting {
            get
            {
                return SGD_Settings.Instance.GetData<MMN_Settings>();
            }
        }
        public static bool LockPlayerForwardToNorth = false;
        public static Dictionary<string, SceneMapSetting> SceneMapDic = new Dictionary<string, SceneMapSetting>();
        public static Dictionary<string, CustomMarkerSetting> CustomMarkerSettingDic = new Dictionary<string, CustomMarkerSetting>();
        public static List<CustomMarkerData> CustomMarkersList = new List<CustomMarkerData>();
        public static SceneMapSetting CurrentSceneMapSetting;
        public static int CurrentMapLayer = 0;
        public static SubMapSetting CurrentSubMapSetting;
        public static int NavmeshAreaMask = -1;
        public static bool[] IconCategoryVisible = new bool[1];
        public static Transform Player
        {
            get
            {
                if (_player == null)
                {
                    if(Time.frameCount%100==0 && Time.frameCount!=0) Debug.LogError("Player is not set yet, please use MapManager.SetPlayer(Transform _playerTransform) to set the player to Master Map Navigation system");
                    if (playerPlaceHolder == null)
                    {
                        GameObject _newObj = new GameObject("Player Place Holder");
                        playerPlaceHolder = _newObj.transform;
                    }
                    else if (Camera.main!=null)
                    {
                        playerPlaceHolder.position = Camera.main.transform.position;
                    }
                    return playerPlaceHolder;
                }
                return _player;
            }
        }
        public static Camera PlayerCamera
        {
            get
            {
                if (_playerCamera == null)
                {
                    if (Camera.main != null)
                    {
                        return Camera.main;
                    }
                    if (Time.frameCount % 100 == 0 && Time.frameCount != 0) Debug.LogError("Player camera is not set yet, please use MapManager.SetCamera(Camera _camera) to set the player camera to Master Map Navigation system");
                    return null;
                }
                return _playerCamera;
            }
        }
        public static Dictionary<Transform, MapPointData> MapPointDic = new Dictionary<Transform, MapPointData>();
        public delegate void MapPointStateChange(Transform _key, int _state);
        public delegate void MapPointCreate(Transform _key, MapPointData _data, int _state);
        public delegate void MapPointDataUpdate(Transform _key, MapPointData _data);
        public delegate void MapPointRemove(Transform _key);
        public delegate void NavigationPathUpdate(List<Vector3> _path,bool _updateAll);
        public delegate void MapIconClick(MapPoint _point, int _button);
        public delegate void MapLayerChange(int _layer);
        public delegate void MarkerOutLimit(Vector3 _worldPos);
        private static string InitScene = "";
        private static Transform _player;
        private static Camera _playerCamera;
        private static Dictionary<string, List<MapPoint>> LayerEntrance = new Dictionary<string, List<MapPoint>>();
        private static Transform navigationTransform;
        private static Transform playerPlaceHolder;
        private static List<MapPoint> Markers = new List<MapPoint>();
        private static bool Inited = false;
        #endregion

        #region Internal Methods
        private static void FormatMarkerNames()
        {
            for (int i = 0; i < Markers.Count; i++)
            {
                Markers[i].Data.Text = (i + 1).ToString();
                Markers[i].Data.HintText = "Marker #" + (i + 1).ToString()+"("+ Mathf.FloorToInt(Markers[i].transform.position.x).ToString()+" , " + Mathf.FloorToInt(Markers[i].transform.position.z).ToString() + ")";
                Markers[i].UpdateData();
            }
        }

        public static void SceneCheck()
        {
            if (SceneManager.GetActiveScene().name != InitScene)
            {
                InitScene = SceneManager.GetActiveScene().name;
                if (!Inited) Init();
            }
            else
            {
                Inited = false;
            }
        }
        public static float FormAngle(float _angle)
        {
            _angle = _angle % 360F;
            if (_angle < -180F) _angle += 360F;
            if (_angle > 180F) _angle -= 360F;
            return _angle;
        }
        public static void Init()
        {
            MapPointStateChangeCallback = null;
            MapPointCreateCallback = null;
            MapPointRemoveCallback = null;
            MapPointDataUpdateCallback = null;
            NavigationPathCallback = null;
            MapLayerChangeCallback = null;
            MapIconClickCallback = null;
            MarkerOutLimitCallback = null;

            MapPointStateChangeCallback += OnMapStateChange;
            MapPointCreateCallback += OnMapPointCreate;
            MapPointRemoveCallback += OnMapPointRemove;
            MapPointDataUpdateCallback += OnMapPointDataUpdate;
            NavigationPathCallback += OnNavigationPathUpdate;
            MapLayerChangeCallback += OnMapLayerChange;
            MapIconClickCallback += OnMapIconClick;
            MarkerOutLimitCallback += OnMarkerOutLimit;
            Markers.Clear();
            LayerEntrance.Clear();
            SceneMapDic.Clear();
            CustomMarkerSettingDic.Clear();
            CustomMarkersList.Clear();

            for (int i = 0; i < CurrentSetting.CustomMarkerSettings.Count; i++)
            {
                CustomMarkerSettingDic.Add(CurrentSetting.CustomMarkerSettings[i]._uid, CurrentSetting.CustomMarkerSettings[i]);
            }
            for (int i = 0; i < CurrentSetting.SceneMaps.Count; i++)
            {
                SceneMapDic.Add(CurrentSetting.SceneMaps[i]._sceneName, CurrentSetting.SceneMaps[i]);
            }
            string _scene = SceneManager.GetActiveScene().name;
            if (SceneMapDic.ContainsKey(_scene))
                CurrentSceneMapSetting = SceneMapDic[_scene];
            else
                CurrentSceneMapSetting = null;
            LockPlayerForwardToNorth = CurrentSetting.LockPlayerNorth;

            if (IconCategoryVisible.Length != CurrentSetting.IconGroups.Count)
            {
                IconCategoryVisible = new bool[CurrentSetting.IconGroups.Count];
                for(int i = 0;i < IconCategoryVisible.Length; i++)
                {
                    IconCategoryVisible[i] = CurrentSetting.IconGroups[i]._visisbleDefault;
                }
            }
            if (CurrentSetting != null && CurrentSceneMapSetting!=null)
            {
                if (CurrentSetting.MapMode== MMN_Settings.MapModes.StaticMap && CurrentSceneMapSetting._fogOfWarEnable)
                {
                    if (FogOfWar.instance!=null) {
                        FogOfWar.instance.InitFog();
                    }
                }
            }
            UpdateInfo();
            Inited = true;
        }

        public static void RegisterLayerEntrance(int _ownLayer, int _targetLayer, MapPoint _point)
        {
            string _key = _ownLayer.ToString() + "_" + _targetLayer.ToString();
            if (LayerEntrance.ContainsKey(_key)) {
                LayerEntrance[_key].Add(_point);
            }
            else
            {
                List<MapPoint> _points = new List<MapPoint>();
                _points.Add(_point);
                LayerEntrance.Add(_key, _points);
            }
        }

        static int SortByDistance(MapPoint p1, MapPoint p2)
        {
            return Vector3.Distance (p1.transform.position,Player.position).CompareTo(Vector3.Distance(p2.transform.position, Player.position));
        }


      

        public static void UpdateInfo() {
            if (Player == null || CurrentSceneMapSetting == null) return;
            float _playerHeight = Player.position.y;
            for (int i = CurrentSceneMapSetting._maps.Count-1; i>=0;i--)
            {
                if (_playerHeight >= CurrentSceneMapSetting._maps[i]._height)
                {
                    if (CurrentMapLayer != i)MapLayerChangeCallback(i);
                    break;
                }
            }

        }

        public static void SetMapTexture(ref RawImage _image, ref RectTransform _rect,string _submapPreview="")
        {
            if (Player == null)
            {
                CurrentSubMapSetting = null;
                return;
            }
            if (CurrentSetting.MapMode == MMN_Settings.MapModes.StaticMap)
            {
                if (Player == null || CurrentSceneMapSetting == null) return;
                UpdateInfo();
                bool _matchedSubmap = false;
                if (CurrentSceneMapSetting._subMaps.Count > 0)
                {
                    foreach (var submap in CurrentSceneMapSetting._subMaps)
                    {
                        if ((Player.position.x >= submap._bottomLeft.x && Player.position.x <= submap._topRight.x
                            && Player.position.z >= submap._bottomLeft.z && Player.position.z <= submap._topRight.z)
                            || submap.uid == _submapPreview)
                        {
                            for (int i = submap._maps.Count - 1; i >= 0; i--)
                            {
                                if ((i == 0 && _submapPreview != "") || (Player.position.y >= submap._maps[i]._height && Player.position.y < submap._maps[i]._ceil))
                                {
                                    _matchedSubmap = true;
                                    float _width = submap._topRight.x - submap._bottomLeft.x;
                                    float _height = submap._topRight.z - submap._bottomLeft.z;

                                    _rect.sizeDelta = new Vector2(_width, _height);
                                    if (submap._maps[i]._map != null)
                                    {
                                        _image.texture = submap._maps[i]._map;
                                    }
                                    else
                                    {
                                        _image.texture = submap._maps[0]._map;
                                    }
                                    CurrentSubMapSetting = submap;
                                    _image.GetComponent<RectTransform>().sizeDelta = _rect.sizeDelta;
                                    _image.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                                        submap._bottomLeft.x + _width * 0.5F,
                                        submap._bottomLeft.z + _height * 0.5F
                                        );
                                }
                            }
                        }
                    }
                }
                else
                {
                    CurrentSubMapSetting = null;
                }

                if (!_matchedSubmap)
                {
                    CurrentSubMapSetting = null;
                    float _width = CurrentSceneMapSetting._topRight.x - CurrentSceneMapSetting._bottomLeft.x;
                    float _height = CurrentSceneMapSetting._topRight.z - CurrentSceneMapSetting._bottomLeft.z;

                    _rect.sizeDelta = new Vector2(_width, _height);
                    if (CurrentSceneMapSetting._maps[CurrentMapLayer]._map != null)
                    {
                        _image.texture = CurrentSceneMapSetting._maps[CurrentMapLayer]._map;
                    }
                    else
                    {
                        _image.texture = CurrentSceneMapSetting._maps[0]._map;
                    }
                    _image.GetComponent<RectTransform>().sizeDelta = _rect.sizeDelta;
                    _image.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                        CurrentSceneMapSetting._bottomLeft.x + _width * 0.5F,
                        CurrentSceneMapSetting._bottomLeft.z + _height * 0.5F
                        );
                }
                _rect.parent.parent.GetComponent<Image>().color = CurrentSceneMapSetting._backgroundColor;
            }
            else if (CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap)
            {
                CurrentSubMapSetting = null;
                MMN_RT.instance.MoveToPosition(Player.position);
                _rect.sizeDelta = new Vector2(CurrentSetting.WorldMapSize, CurrentSetting.WorldMapSize);
                _image.texture = MMN_RT.instance.GetTexture(CurrentSetting.WorldMapSize*0.5F);
                _image.GetComponent<RectTransform>().sizeDelta = _rect.sizeDelta;
                _image.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                    Player.position.x,
                    Player.position.z
                    );
            }
        }

        public static void OnMapIconClick(MapPoint _point, int _button)
        {

        }
        public static void OnMapLayerChange(int _layer)
        {
            CurrentMapLayer = _layer;
        }

        public static void OnMarkerOutLimit(Vector3 _worldPos)
        {
          
        }

        public static void OnMapStateChange(Transform _key, int _state)
        {

        }
        public static void OnMapPointCreate(Transform _key, MapPointData _data, int _state)
        {
            if (!MapPointDic.ContainsKey(_key))
            {
                MapPointDic.Add(_key, _data);
            }
        }
        public static void OnMapPointRemove(Transform _key)
        {
            if (MapPointDic.ContainsKey(_key))
            {
                MapPointDic.Remove(_key);
            }
            foreach (var key in LayerEntrance.Keys) {
                for (int i= LayerEntrance[key].Count-1; i>=0;i--) {
                    if (LayerEntrance[key][i].transform == _key)
                    {
                        LayerEntrance[key].RemoveAt(i);
                    }
                }
            }
        }

        public static void OnMapPointDataUpdate(Transform _key, MapPointData _data)
        {
            if (MapPointDic.ContainsKey(_key))
            {
                MapPointDic[_key]= _data;
            }
            if (CurrentSetting.EnableCustomMarker && CurrentSetting.AllowPlayerToRenameCustomMarker)
            {
                string _uid = _key.transform.position.x.ToString("0.00") + "_" + _key.transform.position.y.ToString("0.00") + "_" + _key.transform.position.z.ToString("0.00");
                for (int i = CustomMarkersList.Count - 1; i >= 0; i--)
                {
                    if (CustomMarkersList[i]._uid == _uid)
                    {
                        CustomMarkersList[i]._name= _data.Text;
                    }
                }
            }
        }

        public static void OnNavigationPathUpdate(List<Vector3> _path, bool _updateAll)
        {
            
        }
        #endregion


        //===================Callbacks=========================

        /// <summary>
        /// Trigger: When the state variable of a MapPoint changes.
        /// Usage Example:
        ///MapManager.MapPointStateChangeCallback += OnMapStateChange;
        ///public void OnMapStateChange(Transform _key, int _state) { }
        /// </summary>
        public static MapPointStateChange MapPointStateChangeCallback;

        /// <summary>
        /// Trigger: When a MapPoint is added to the map system.
        /// Usage Example:
        ///MapManager.MapPointCreateCallback += OnMapPointCreate;
        ///public void OnMapPointCreate(Transform _key, MapPointData _data, int _state) { }
        /// </summary>
        public static MapPointCreate MapPointCreateCallback;//

        /// <summary>
        /// Trigger: When a MapPoint is removed from the map system.
        /// Usage Example:
        ///MapManager.MapPointRemoveCallback+= OnMapPointRemove;
        ///public void OnMapPointRemove(Transform _key) { }
        /// </summary>
        public static MapPointRemove MapPointRemoveCallback;//

        /// <summary>
        /// Trigger: When the data of a MapPoint is updated.
        /// Usage Example:
        ///MapManager.MapPointDataUpdateCallback += OnMapPointDataUpdateCallback;
        ///public void OnMapPointDataUpdateCallback(Transform _key, MapPointData _data) { }
        /// </summary>
        public static MapPointDataUpdate MapPointDataUpdateCallback;//

        /// <summary>
        /// Trigger: When a navigation path is created, removed, or updated.
        /// Usage Example:
        ///MapManager.NavigationPathCallback += OnNavigationPathUpdate;
        ///public void OnNavigationPathUpdate(List<Vector3> _path, bool _updateAll) { }
        ///When _updateAll is false, means only the first and last position of the List has changed, 
        ///you should pull the first and last value of the list to replace the current one.When the List is empty means the path has been removed.
        /// </summary>
        public static NavigationPathUpdate NavigationPathCallback;

        /// <summary>
        /// Trigger: When the player enters a different map layer.
        /// Usage Example:
        ///MapManager.MapLayerChangeCallback+= OnMapLayerChange;
        ///public void OnMapLayerChange(int _layer) { }
        /// </summary>
        public static MapLayerChange MapLayerChangeCallback;

        /// <summary>
        /// Trigger: When the player try to place more marker than the maximum allowed number.
        /// Usage Example:
        ///MapManager.MarkerOutLimitCallback+= OnMarkerOutLimit;
        ///public void OnMarkerOutLimit(Vector3 _worldPos) { }
        /// </summary>
        public static MarkerOutLimit MarkerOutLimitCallback;

        /// <summary>
        /// Trigger: When a player clicks on a map icon.
        /// Usage Example:
        ///MapManager.MapIconClickCallback += OnMapIconClick;
        ///public void OnMapIconClick(MapPoint _point, int _button) { }
        /// </summary>
        public static MapIconClick MapIconClickCallback;//

        //===================Callbacks=========================



        /// <summary>
        /// Assigns the player's transform to the Master Map Navigation system. 
        ///This function must be called when the scene starts to initialize the player's position.
        /// </summary>
        /// <param name="_playerTransform"></param>
        public static void SetPlayer(Transform _playerTransform)
        {
            _player = _playerTransform;
        }

        /// <summary>
        /// Assigns the player's camera to the Master Map Navigation system. Otherwise Camera.main will be used.
        /// This function must be called when the scene starts to initialize the player's orientation.
        /// </summary>
        /// <param name="_camera"></param>
        public static void SetCamera(Camera _camera)
        {
            _playerCamera = _camera;
        }

        /// <summary>
        /// Retrieve the mini-map instance in the canvas.
        /// </summary>
        /// <returns></returns>
        public static MapInteractive GetMiniMapInstance()
        {
            return MapInteractive.MiniMapInstance;
        }

        /// <summary>
        /// Retrieve the world-map instance in the canvas.
        /// </summary>
        /// <returns></returns>
        public static MapInteractive GetWorldMapInstance()
        {
            return MapInteractive.WorldMapInstance;
        }

        /// <summary>
        /// Convert the current `Fog of War` data into a json string.
        /// </summary>
        /// <returns></returns>
        public static string SaveFog()
        {
            if (!CurrentSceneMapSetting._fogOfWarEnable || CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap || FogOfWar.instance==null) return "";
            return FogOfWar.instance.SaveFog();
        }

        /// <summary>
        /// Load the `Fog of War` data with a json string.
        /// </summary>
        /// <param name="_json"></param>
        public static void LoadFog(string _json)
        {
            if (!CurrentSceneMapSetting._fogOfWarEnable || CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap || FogOfWar.instance == null) return;
            FogOfWar.instance.LoadFog(_json);
        }

        /// <summary>
        /// Cover a world space position with `Fog of War` by normalized radius (0~1).
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <param name="_radius"></param>
        public static void AddFog(Vector3 _worldPos, float _radius)
        {
            if (!CurrentSceneMapSetting._fogOfWarEnable || CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap || FogOfWar.instance == null) return;
            FogOfWar.instance.AddFog(MapInteractive.WorldToNormalizedMapPosition(_worldPos), _radius);
        }


        /// <summary>
        /// Reveal a world space position from `Fog of War` by normalized radius (0~1).
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <param name="_radius"></param>
        public static void RemoveFog(Vector3 _worldPos, float _radius)
        {
            if (!CurrentSceneMapSetting._fogOfWarEnable || CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap || FogOfWar.instance == null) return;
            FogOfWar.instance.RemoveFog(MapInteractive.WorldToNormalizedMapPosition(_worldPos), _radius);
        }

        /// <summary>
        /// Clear all `Fog of War` from the map.
        /// </summary>
        public static void ClearFog()
        {
            if (!CurrentSceneMapSetting._fogOfWarEnable || CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap || FogOfWar.instance == null) return;
            FogOfWar.instance.ClearFog();
        }

        /// <summary>
        /// Cover the whole map with `Fog of War`.
        /// </summary>
        public static void FillFog()
        {
            if (!CurrentSceneMapSetting._fogOfWarEnable || CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap || FogOfWar.instance == null) return;
            FogOfWar.instance.FillFog();
        }


        /// <summary>
        /// Start tracking a transform on the mini-map, a white arrow will point to the target on the map.
        /// </summary>
        /// <param name="_target"></param>
        public static void StartTrack(Transform _target)
        {
            if (MapInteractive.MiniMapInstance != null && MapInteractive.MiniMapInstance.TrackArrowScript!=null)
            {
                MapInteractive.MiniMapInstance.TrackArrowScript.StartTrack(_target);
            }
        }

        /// <summary>
        /// Stop tracking a transform on the mini-map and hide the white arrow. Refer to the StartTrack(Transform _target).
        /// </summary>
        public static void EndTrack()
        {
            if (MapInteractive.MiniMapInstance != null && MapInteractive.MiniMapInstance.TrackArrowScript != null)
            {
                MapInteractive.MiniMapInstance.TrackArrowScript.EndTrack();
            }
        }


        /// <summary>
        /// Creates a navigation path from the player's current position to the specified world space position.
        ///_worldPos: The target position in world space.
        /// </summary>
        /// <param name="_worldPos"></param>
        public static void NavigateToHere(Vector3 _worldPos)
        {
            if (navigationTransform == null)
            {
                GameObject _newObj = new GameObject("MapNavigation");
                navigationTransform = _newObj.transform;
            }
            navigationTransform.position = NavigationPath.GetNavMeshPoint(_worldPos);
            NavigationPath.GetPath(navigationTransform);
            if (CurrentSetting.ShowTrackArrowWhenNavigate) StartTrack(navigationTransform);
        }



        /// <summary>
        /// Clears the current navigation path and stops the navigation.
        /// </summary>
        public static void StopNavigation()
        {
            NavigationPath.Stop();
            if (CurrentSetting.ShowTrackArrowWhenNavigate) EndTrack();
        }

        /// <summary>
        /// Get the world space rect(x,z) of the current map, this api only work with Static-Mode.
        /// </summary>
        /// <returns></returns>
        public static Rect GetCurrentMapRect()
        {
            if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap) return new Rect(0, 0, 0, 0);
            Vector2 _start = new Vector2(CurrentSceneMapSetting._bottomLeft.x, CurrentSceneMapSetting._bottomLeft.z);
            Vector2 _size= new Vector2(CurrentSceneMapSetting._topRight.x- CurrentSceneMapSetting._bottomLeft.x, CurrentSceneMapSetting._topRight.z - CurrentSceneMapSetting._bottomLeft.z);
            return new Rect(_start,_size);
        }

        /// <summary>
        /// Get the markers list on the map.
        /// </summary>
        /// <returns></returns>
        public static List<MapPoint> GetMarkers()
        {
            return Markers;
        }


        /// <summary>
        /// Export the custom markers data into json text, save this text with your own code so you can load it later.
        /// </summary>
        /// <returns></returns>
        public static string ExportCustomMarkerJsonData()
        {
            CustomMarkerJsonRoot _data = new CustomMarkerJsonRoot();
            _data.Data = CustomMarkersList.ToArray();
            return JsonUtility.ToJson(_data);
        }

        /// <summary>
        /// Import the custom markers from json text, use your own code to load the json text you saved and call this function to load the marker icons in the start of the scene.
        /// </summary>
        /// <param name="_jsonText"></param>
        public static void ImportCustomMarkerJsonData(string _jsonText)
        {
            CustomMarkerJsonRoot _data = JsonUtility.FromJson<CustomMarkerJsonRoot>(_jsonText);
            if (_data == null || _data.Data.Length <= 0) return;
            CustomMarkersList.Clear();
            foreach (var obj in _data.Data) {
                PlaceCustomMarker(obj._pos, obj._settingUid, obj._name);
            }
        }


        /// <summary>
        /// Place a custom marker by its position and the uid from Custom Marker List Settings. If the _name is null, the default name will be applied.
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <param name="_uid"></param>
        /// <param name="_name"></param>
        /// <returns></returns>
        public static MapPoint PlaceCustomMarker(Vector3 _worldPos, string _uid, string _name="")
        {
            GameObject _newMarker = Instantiate(Resources.Load<GameObject>("MapNavigationSystem/CustomMarker"), _worldPos, Quaternion.identity);
            _newMarker.GetComponent<MapPoint>().Data.Icon = CustomMarkerSettingDic[_uid]._icon;
            _newMarker.GetComponent<MapPoint>().Data.Text = (_name != "" ? _name : CustomMarkerSettingDic[_uid]._defaultName);
            _newMarker.SetActive(true);
            CustomMarkersList.Add(new CustomMarkerData() { 
                _name = _newMarker.GetComponent<MapPoint>().Data.Text,
                _pos= _worldPos,
                _settingUid= _uid,
                _uid =_worldPos.x.ToString("0.00")+"_"+ _worldPos.y.ToString("0.00") + "_" + _worldPos.z.ToString("0.00")
            });
            return _newMarker.GetComponent<MapPoint>();
        }

        /// <summary>
        /// Remove a custom marker by the map point component.
        /// </summary>
        /// <param name="_point"></param>
        public static void RemoveCustomMarker(MapPoint _point)
        {
            string _uid = _point.transform.position.x.ToString("0.00") + "_" + _point.transform.position.y.ToString("0.00") + "_" + _point.transform.position.z.ToString("0.00");
            for (int i= CustomMarkersList.Count-1;i>=0;i--)
            {
                if (CustomMarkersList[i]._uid == _uid)
                {
                    CustomMarkersList.RemoveAt(i);
                    if(_point.gameObject!=null) Destroy(_point.gameObject);
                }
            }
        }



        /// <summary>
        ///Adding a custom marker on the map to mark points of interest. 
        ///Places a marker on the map at the specified world space position of a 3d object.(transform.position)
        ///Return the MapPoint component representing the created marker.
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <param name="_markerPrefabPath"></param>
        /// <returns></returns>
        public static MapPoint PlaceMarker(Vector3 _worldPos,string _markerPrefabPath= "MapNavigationSystem/Marker")
        {
            for (int i=Markers.Count-1;i>=0;i--) {
                if (Markers[i] == null) Markers.RemoveAt(i);
            }
            if (Markers.Count >= CurrentSetting.MaximumMarkers)
            {
                if (CurrentSetting.MarkerOutNumberMethod == MMN_Settings.MarkerOutNumberMethods.Callback)
                {
                    if (MarkerOutLimitCallback != null) MarkerOutLimitCallback(_worldPos);
                    return null;
                }
                else if (CurrentSetting.MarkerOutNumberMethod == MMN_Settings.MarkerOutNumberMethods.ReplaceTheFirstOne)
                {
                    if (Markers[0] != null)
                        RemoveMarker(Markers[0]);
                    else
                        Markers.RemoveAt(0);
                }
                else if (CurrentSetting.MarkerOutNumberMethod == MMN_Settings.MarkerOutNumberMethods.ReplaceTheLastOne)
                {
                    if (Markers[Markers.Count - 1] != null)
                        RemoveMarker(Markers[Markers.Count-1]);
                    else
                        Markers.RemoveAt(Markers.Count - 1);
                }
                else
                {
                    return null;
                }
            }
            GameObject _newMarker = Instantiate(Resources.Load<GameObject>(_markerPrefabPath), _worldPos, Quaternion.identity);
            int _id = Markers.Count + 1;
            _newMarker.SetActive(true);
            Markers.Add(_newMarker.GetComponent<MapPoint>());
            FormatMarkerNames();
            return _newMarker.GetComponent<MapPoint>();
        }


        /// <summary>
        /// Removes a previously placed marker from the map.
        ///_point: The MapPoint component of the marker to be removed.
        /// </summary>
        /// <param name="_point"></param>
        public static void RemoveMarker(MapPoint _point)
        {
            if (Markers.Contains(_point))
            {
                GameObject _obj = _point.gameObject;
                Markers.Remove(_point);
                Destroy(_obj);
                FormatMarkerNames();
            }
        }


        /// <summary>
        /// Retrieves the layer ID of the map based on a given world space position. The layer ID where the position resides.
        ///_pos: The target position in world space.
        /// </summary>
        /// <param name="_pos"></param>
        /// <returns></returns>
        public static int GetLayerByPosition(Vector3 _pos)
        {
            if (CurrentSceneMapSetting == null) return 0;
            int _layer = 0;
            for (int u = 0; u < MapManager.CurrentSceneMapSetting._maps.Count; u++)
            {
                if (_pos.y > MapManager.CurrentSceneMapSetting._maps[u]._height) _layer = u;
            }
            return _layer;
        }


        /// <summary>
        /// Retrieves a list of entrance points to move from one map layer _ownLayer to another _targetLayer.
        ///_ownLayer: The current layer ID._targetLayer: The destination layer ID.
        /// </summary>
        /// <param name="_ownLayer"></param>
        /// <param name="_targetLayer"></param>
        /// <returns></returns>
        public static List<MapPoint> GetLayerEntrance(int _ownLayer, int _targetLayer)
        {
            string _key = _ownLayer.ToString() + "_" + _targetLayer.ToString();
            if (LayerEntrance.ContainsKey(_key))
            {
                return LayerEntrance[_key];
            }
            else
            {
                return new List<MapPoint>();
            }
        }


        /// <summary>
        /// Finds the closest entrance point between one map layer _ownLayer to another _targetLayer. 
        ///_ownLayer: The current layer ID._targetLayer: The destination layer ID.
        /// </summary>
        /// <param name="_ownLayer"></param>
        /// <param name="_targetLayer"></param>
        /// <returns></returns>
        public static MapPoint GetLayerCloestEntrance(int _ownLayer, int _targetLayer)
        {
            List<MapPoint> _points = GetLayerEntrance(_ownLayer, _targetLayer);
            if (_points.Count > 0)
            {
                _points.Sort(SortByDistance);
                return _points[0];
            }
            else
            {
                return null;
            }
        }

    }

}




