using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoftKitty.MasterNavigationMap
{

    public class MapInteractive : MonoBehaviour
    {
        public static MapInteractive MiniMapInstance;
        public static MapInteractive WorldMapInstance;
        [Header("If your game use controller, inherit your script from ControllerMapping.cs, replace the one attached on this gameObject and assign it here:")]
        public ControllerMapping ControllerMappingScript;
        public TrackArrow TrackArrowScript;
        [HideInInspector]
        public Rect ViewRect;
        public float CurrentMapScale
        {
            get { return _mapScale; }
        }

        #region varibles
        public enum MapTypes
        {
            WorldMap,
            MiniMap
        }
        public MapTypes Type;
        [Range(0.1F,1F)]
        public float ZoomStep = 1F;
        public RawImage MapImage;
        public RectTransform MapDrag;
        public RectTransform MapOffset;
        public EventTrigger MapEvent;
        public EventTrigger IconEvent;
        public RectTransform TopRight;
        public RectTransform DownLeft;
        public RectTransform PlayerIcon;
        public GameObject IconPrefab;
        public GameObject LayerPrefab;
        public GameObject CategoryPrefab;
        public Button CompassButton;
        public GameObject SubMapCloseButton;
        public Text SubMapButtonText;
        public float MapMoveSpeed = 1f;
        public Text[] Hints;
        public Text Coordinate;
        public Text Title;
        public UILineRenderer NavLine;
        public GameObject CustomMarkerButton;
        public GameObject CustomMarkerListPrefab;
        public CanvasGroup CustomMarkerListCanvas;
        public GameObject CustomMarkerPopRoot;
        public RectTransform CustomMarkerPopMenu;
        public GameObject CustomMarkerRenamePanel;
        public InputField CustomMarkerNameInput;
        public Text CustomMarkerHints;

        private MapPoint PendingCustomMarker;
        private int ViewingLayer = 0;
        private Vector3 StartPos;
        private Vector3 StartMapPos;
        private float MapScale = 3f;
        private Vector3 MapPos=Vector3.zero;
        private float _mapScale = 3f;
        private Vector3 ScrollFocus;
        private Vector2 ScrollOffset;
        private Dictionary<Transform, MapIcon> IconDic = new Dictionary<Transform, MapIcon>();
        private List<Button> LayerList = new List<Button>();
        private List<CanvasGroup> CategoryList = new List<CanvasGroup>();
        private Dictionary<string,CanvasGroup> CustomMarkerList = new Dictionary<string,CanvasGroup>();
        private string SelectedCustomMarker = "";
        private bool Zooming = false;
        private bool outBound = false;
        private bool Inited = false;
        private bool needToRefresh = false;
        private float CanvasScale;
        private Vector2 CanvasSize;
        private Canvas RootCanvas;
        private string currentSubMap = "";
        private float normalizedScale = 0.5F;
        private Transform Player
        {
            get
            {
                if (MapManager.CurrentSetting.PlayerOrientation == MMN_Settings.PlayerOrientations.PlayerTransform)
                    return MapManager.Player;
                else
                    return MapManager.PlayerCamera.transform;
            }
        }

        #endregion

        #region MonoBehaviour
        void Awake()
        {
            MapManager.SceneCheck();
            MapManager.MapPointStateChangeCallback += OnMapStateChange;
            MapManager.MapPointCreateCallback += OnMapPointCreate;
            MapManager.MapPointRemoveCallback += OnMapPointRemove;
            MapManager.MapPointDataUpdateCallback += OnMapDataUpdate;
            MapManager.MapLayerChangeCallback += SwitchMapLayer;
            MapManager.MapIconClickCallback += OnMapIconClick;
            if (Type == MapTypes.WorldMap && MapManager.CurrentSetting.MapMode== MMN_Settings.MapModes.StaticMap) RecreateLayerButtonList();
            if (Type == MapTypes.WorldMap)
            {
                RecreateCatergoryList();
                WorldMapInstance = this;
                if (MapManager.CurrentSetting.EnableCustomMarker) RecreateCustomMarkerList();
                CustomMarkerButton.SetActive(MapManager.CurrentSetting.EnableCustomMarker && Type == MapTypes.WorldMap);
                CustomMarkerListCanvas.alpha = 0F;
                CustomMarkerListCanvas.interactable = false;
                CustomMarkerListCanvas.blocksRaycasts = false;
                CustomMarkerListCanvas.gameObject.SetActive(MapManager.CurrentSetting.EnableCustomMarker && Type == MapTypes.WorldMap);
            }
            SelectedCustomMarker = "";
            if (Type == MapTypes.MiniMap) MiniMapInstance = this;
            if (GetComponent<ControllerMapping>() && ControllerMappingScript == null) ControllerMappingScript = GetComponent<ControllerMapping>();
            currentSubMap = "";
            if (NavLine != null) NavLine.color = MapManager.CurrentSetting.NavigationLineColor;
        }

        private void OnDestroy()
        {
            MapManager.MapPointStateChangeCallback -= OnMapStateChange;
            MapManager.MapPointCreateCallback -= OnMapPointCreate;
            MapManager.MapPointRemoveCallback -= OnMapPointRemove;
            MapManager.MapLayerChangeCallback -= SwitchMapLayer;
            MapManager.MapIconClickCallback -= OnMapIconClick;
            MapManager.MapPointDataUpdateCallback -= OnMapDataUpdate;
        }

        private float GetMapScale() {
            return Mathf.Lerp(GetMinScale(), GetMaxScale(), normalizedScale);
        }

        private float GetMinScale()
        {
            float _multiplier = 1F;
            if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap)
            {
                
                if (Type == MapTypes.WorldMap)
                    _multiplier = 1F;
                else
                    _multiplier = 4F;

            }
            float _screenX = TopRight.localPosition.x - DownLeft.localPosition.x - 10f;
            float _screenY = TopRight.localPosition.y - DownLeft.localPosition.y - 10f;
            float _sizeX = MapOffset.sizeDelta.x - 10F;
            float _sizeY = MapOffset.sizeDelta.y - 10F;
            return Mathf.Max(_screenX / _sizeX, _screenY / _sizeY)*Mathf.Max(1F, _multiplier);
        }

        private float GetMaxScale()
        {
            return (Type == MapTypes.WorldMap ? MapManager.CurrentSetting.WorldMapMaxmiumZoomIn : MapManager.CurrentSetting.MiniMapMaxmiumZoomIn) * GetMinScale();
        }

        IEnumerator Start()
        {
            yield return 1;
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.Scroll;
            entry.callback.AddListener((data) => { OnMapScroll((PointerEventData)data); });
            MapEvent.triggers.Add(entry);
            IconEvent.triggers.Add(entry);
            normalizedScale = (Type == MapTypes.WorldMap ? MapManager.CurrentSetting.WorldMapDefaultZoom : MapManager.CurrentSetting.MiniMapDefaultZoom);
            MapScale = GetMapScale();
            _mapScale = MapScale;
            PlayerIcon.GetComponent<RawImage>().texture = MapManager.CurrentSetting.PlayerIcon;
            CompassButton.gameObject.SetActive(MapManager.CurrentSetting.MiniMapRotateWithCamera || MapManager.CurrentSetting.ToggleCompassButton);
            CompassButton.interactable = (Type == MapTypes.MiniMap && !MapManager.CurrentSetting.MiniMapRotateWithCamera);
            if (Type == MapTypes.WorldMap) InitHints();
            if (MapManager.CurrentSceneMapSetting!=null)
            {
                Title.gameObject.SetActive(true);
                Title.text = MapManager.CurrentSceneMapSetting._sceneTitle;
                Title.transform.parent.gameObject.SetActive((Type == MapTypes.MiniMap && MapManager.CurrentSetting.MiniDisplaySceneTitle) || (Type == MapTypes.WorldMap && MapManager.CurrentSetting.DisplaySceneTitle));
            }
            else
            {
                Title.gameObject.SetActive(false);
            }

           
            if (Coordinate) Coordinate.gameObject.SetActive(Type == MapTypes.MiniMap && MapManager.CurrentSetting.MiniDisplayCoordinate);
            OnEnable();
            if (ControllerMappingScript != null) ControllerMappingScript.OnMapInit(Type);
        }

        

        private void OnEnable()
        {
            StartCoroutine(EnableCo());
        }
        IEnumerator EnableCo()
        {
            yield return 1;
            SubmapCheck(true);
            if (MapManager.CurrentSceneMapSetting != null)
            {
                if (MapManager.CurrentSceneMapSetting._maps.Count > 1) SwitchMapLayer(MapManager.CurrentMapLayer);
            }
            foreach (Transform t in MapManager.MapPointDic.Keys)
            {
                if (!IconDic.ContainsKey(t))
                {
                    OnMapPointCreate(t, MapManager.MapPointDic[t], t.GetComponent<MapPoint>().State);
                }
            }
            ScrollOffset = new Vector2(0.5F, 0.5F);
            ScrollFocus = MapManager.Player.position;
            if (Type == MapTypes.WorldMap)
            {
                MapScale = GetMapScale();
                _mapScale = Mathf.Min(MapScale + 2F, 10F);
                BoundCheck();
                MapPos = GetFocusPoint(ScrollFocus, ScrollOffset);
            }
            Inited = true;
        }
        void Update()
        {
            if (ControllerMappingScript != null) ControllerMapping();
            StateUpdate();
            if (!Inited) return;
            MapMove();
            MapZooming();
            MapRefresh();
            PlayerIconState();
            MapManager.UpdateInfo();
        }


        private void LateUpdate()
        {
            if (!Inited) return;
            if (Type == MapTypes.MiniMap)
            {
                MapDrag.anchoredPosition = Vector3.zero;
            }
            else
            {
                MapDrag.position = MapPos;
                if (MapManager.CurrentSetting.UnlimitedWorldMap && MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap && _mapScale * CanvasScale != 0F)
                {
                    if(MMN_RT.instance.MoveToPosition2D(-MapPos/(_mapScale*CanvasScale))) MMN_RT.instance.Render();
                    MapImage.rectTransform.anchoredPosition = -MapPos / (_mapScale * CanvasScale);
                }
            }
            MapDrag.localScale = _mapScale * Vector3.one;
          
        }

        

        private void MapRefresh()
        {
            if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.StaticMap) SubmapCheck(false);
            if (Type == MapTypes.MiniMap)
            {
                MapOffset.anchoredPosition = -PlayerIcon.anchoredPosition;
                if (MapManager.CurrentSetting.MiniMapRotateWithCamera && MapManager.PlayerCamera != null)
                {
                    MapDrag.localEulerAngles = new Vector3(0F, 0F, MapManager.FormAngle(MapManager.PlayerCamera.transform.eulerAngles.y));
                }
                else if (MapManager.LockPlayerForwardToNorth)
                {
                    MapDrag.localEulerAngles = new Vector3(0F, 0F, MapManager.FormAngle(Player.eulerAngles.y));
                }
                else
                {
                    MapDrag.localEulerAngles = Vector3.zero;
                }
                if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap)
                {
                    if (WorldMapInstance != null && WorldMapInstance.gameObject.activeInHierarchy)
                    {
                        if (GetComponent<CanvasGroup>().alpha > 0F) GetComponent<CanvasGroup>().alpha = Mathf.MoveTowards(GetComponent<CanvasGroup>().alpha, 0F, Time.deltaTime * 2F);
                        needToRefresh = true;
                    }
                    else
                    {
                        if (needToRefresh)
                        {
                            RefreshMapImage();
                            needToRefresh = false;
                        }
                        if (GetComponent<CanvasGroup>().alpha < 1F) GetComponent<CanvasGroup>().alpha = Mathf.MoveTowards(GetComponent<CanvasGroup>().alpha, 1F, Time.deltaTime * 2F);
                    }
                    if (CheckMiniMapOutsideBound())
                    {
                        RefreshMapImage();
                    }
                }
            }
            else
            {
                MapDrag.localEulerAngles = Vector3.zero;
                MapOffset.anchoredPosition = Vector3.zero;
                if (MapManager.CurrentSetting.UnlimitedWorldMap && MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap)
                {
                    float _screenX = CanvasSize.x - 10F;
                    float _screenY = CanvasSize.y - 10f;
                    float _sizeX = MapOffset.sizeDelta.x * 0.5F - 10F;
                    float _sizeY = MapOffset.sizeDelta.y * 0.5F - 10F;
                    if (_mapScale * _sizeX < _screenX || _mapScale * _sizeY < _screenY)
                    {
                        _mapScale = Mathf.Max(_screenX / _sizeX, _screenY / _sizeY);
                        MapScale = _mapScale;
                    }
                    outBound = (MapScale * _sizeX - 1F <= _screenX || MapScale * _sizeY - 1F <= _screenY);
                }
                else
                {
                    BoundCheck();
                }
            }
            


        }

        private void SubmapCheck(bool _refreshAnyway)
        {
            if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.StaticMap)
            {
                string _submapUid = "";
                string _title = MapManager.CurrentSceneMapSetting._sceneTitle;
                if (MapManager.CurrentSceneMapSetting != null && MapManager.CurrentSceneMapSetting._subMaps.Count > 0)
                {
                    foreach (var submap in MapManager.CurrentSceneMapSetting._subMaps)
                    {
                        if (MapManager.Player.position.x >= submap._bottomLeft.x && MapManager.Player.position.x <= submap._topRight.x
                            && MapManager.Player.position.z >= submap._bottomLeft.z && MapManager.Player.position.z <= submap._topRight.z)
                        {
                            for (int i = 0; i < submap._maps.Count; i++)
                            {
                                if (MapManager.Player.position.y >= submap._maps[i]._height && MapManager.Player.position.y < submap._maps[i]._ceil)
                                {
                                    _submapUid = submap.uid;
                                    _title = submap._areaTitle;
                                }
                            }
                        }
                    }
                }
                if (currentSubMap != _submapUid || _refreshAnyway)
                {
                    Title.text = _title;
                    currentSubMap = _submapUid;
                    RefreshMapImage();
                    if (Type == MapTypes.WorldMap) RecreateLayerButtonList();
                    MapScale = GetMapScale();
                }
            }
            else
            {
                RefreshMapImage();
                MapScale = GetMapScale();
            }
        }


        private void StateUpdate()
        {
            if (RootCanvas == null && GetComponentInParent<CanvasScaler>()) RootCanvas = GetComponentInParent<CanvasScaler>().GetComponent<Canvas>();
            if (RootCanvas != null)
            {
                CanvasScale = RootCanvas.GetComponent<RectTransform>().localScale.x;
                CanvasSize = RootCanvas.GetComponent<RectTransform>().sizeDelta;
            }
            if (Type == MapTypes.MiniMap)
            {
                if (MapManager.CurrentSetting.MiniMapRotateWithCamera && MapManager.PlayerCamera != null)
                {
                    CompassButton.GetComponent<RectTransform>().localEulerAngles = new Vector3(0F, 0F, MapManager.FormAngle(MapManager.PlayerCamera.transform.eulerAngles.y));
                }
                else
                {
                    CompassButton.GetComponent<RectTransform>().localEulerAngles = new Vector3(0F, 0F, MapManager.LockPlayerForwardToNorth ? MapManager.FormAngle(Player.eulerAngles.y) : 0F);
                }
            }
            if (Type == MapTypes.MiniMap && MapManager.CurrentSetting.MiniDisplayCoordinate) Coordinate.text = Mathf.FloorToInt(MapManager.Player.position.x).ToString() + " , " + Mathf.FloorToInt(MapManager.Player.position.z).ToString();
            if (Type == MapTypes.WorldMap && MapManager.CurrentSetting.EnableCustomMarker )
            {
                if (CustomMarkerPopRoot.activeSelf && ControllerMappingScript.GetCancelCustomMarkerKey())
                {
                    CloseCustomMarkerPopMenu();
                }
                else if (CustomMarkerListCanvas.alpha > 0F && ControllerMappingScript.GetCancelCustomMarkerKey())
                {
                    ToggleCustomMarkerList();
                }
                CustomMarkerHints.transform.parent.gameObject.SetActive(SelectedCustomMarker!="" && CustomMarkerListCanvas.alpha>0F);
            }
        }

        private void MapZooming()
        {
            Zooming = (Mathf.Abs(_mapScale - MapScale) > 0.005F);
            if (Zooming)
            {
                _mapScale = Mathf.Lerp(_mapScale, MapScale, Time.deltaTime * 4f);
                if (Type != MapTypes.MiniMap) MapPos = GetFocusPoint(ScrollFocus, ScrollOffset);
            }

            if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap && Type == MapTypes.WorldMap)
            {
                ViewRect = new Rect(MMN_RT.instance.transform.position.x - MapManager.CurrentSetting.WorldMapSize * 0.5F,
                    MMN_RT.instance.transform.position.z - MapManager.CurrentSetting.WorldMapSize * 0.5F,
                   MapManager.CurrentSetting.WorldMapSize,
                    MapManager.CurrentSetting.WorldMapSize);
            }
            else if (MapManager.CurrentSceneMapSetting != null)
            {
                ViewRect = new Rect(MapManager.CurrentSceneMapSetting._bottomLeft.x,
                    MapManager.CurrentSceneMapSetting._bottomLeft.z,
                    MapManager.CurrentSceneMapSetting._topRight.x - MapManager.CurrentSceneMapSetting._bottomLeft.x,
                    MapManager.CurrentSceneMapSetting._topRight.z - MapManager.CurrentSceneMapSetting._bottomLeft.z
                    );
            }
        }

        private bool CheckMiniMapOutsideBound() {
            float _radius = Mathf.Min(MapImage.rectTransform.sizeDelta.x, MapImage.rectTransform.sizeDelta.y)* CanvasScale * _mapScale*0.5F;
            float _frameRadius = Mathf.Min(Mathf.Abs(TopRight.position.x - DownLeft.position.x), Mathf.Abs(TopRight.position.y - DownLeft.position.y)) * CanvasScale * 0.5F;
            if (Vector3.Distance( MapImage.rectTransform.position, transform.position)> _radius- _frameRadius
                || Vector3.Distance(MapImage.rectTransform.position, transform.position) > _radius- _frameRadius)
            {
                return true;
            }
            return false;
        }

        public virtual void ControllerMapping()
        {
            if (ControllerMappingScript.GetZoomInKey() || ControllerMappingScript.GetZoomOutKey())
            {
                if (Type == MapTypes.MiniMap && MapManager.CurrentSetting.AllowZoomInOut == false)
                {
                    return;
                }
                Vector3 _center = new Vector2(ControllerMappingScript.GetPointerPosition().x / Screen.width, ControllerMappingScript.GetPointerPosition().y / Screen.height);
                if (ControllerMappingScript.GetZoomOutKey())
                {
                    Zoom(-ZoomStep, _center);
                }
                else
                {
                    Zoom(ZoomStep, _center);
                }
            }
            if (Type == MapTypes.WorldMap && ControllerMappingScript.UseVirtualCursor && ControllerMappingScript.VirtualCursor != null && ControllerMappingScript.VirtualCursor.gameObject.activeSelf)
            {
                if (ControllerMappingScript.GetPlaceMarkerKey() || ControllerMappingScript.GetNavigateKey()) ClikMap();
            }
        }

        #endregion

        #region Internal Methods
        public void CloseCustomMarkerPopMenu()
        {
            PendingCustomMarker = null;
            CustomMarkerRenamePanel.SetActive(false);
            CustomMarkerPopRoot.SetActive(false);
        }

        public void RemoveSelectedCustomMarker()
        {
            if(PendingCustomMarker!=null) MapManager.RemoveCustomMarker(PendingCustomMarker);
            CloseCustomMarkerPopMenu();
        }

        public void RenameSelectedCustomMarker()
        {
            if (PendingCustomMarker == null) return;
            CustomMarkerNameInput.text = PendingCustomMarker.Data.Text;
            CustomMarkerRenamePanel.SetActive(true);
        }

        public void ConfirmRenameSelectedCustomMarker()
        {
            if (PendingCustomMarker == null || CustomMarkerNameInput.text.Length<=0 || CustomMarkerNameInput.text=="") return;
            PendingCustomMarker.SetText(CustomMarkerNameInput.text);
            CloseCustomMarkerPopMenu();
        }

        private Vector3 GetFocusPoint(Vector3 _worldPosition, Vector2 _center)
        {
            Vector3 _screen = TopRight.localPosition - DownLeft.localPosition;
            Vector3 _focus = _screen;
            _focus.x *= _center.x;
            _focus.y *= _center.y;
            Vector3 _tmapPos = WorldToMapPosition(_worldPosition);
            return transform.position - _screen*0.5F* CanvasScale + _focus * CanvasScale - _tmapPos * _mapScale * CanvasScale;
        }
        private void InitHints()
        {
            Hints[0].gameObject.SetActive(MapManager.CurrentSetting.EnableNavigationPath);
            Hints[0].text = ControllerMappingScript.GetNavigateKeyHint();
            Hints[1].text = ControllerMappingScript.GetRemoveMarkerKeyHint();
            Hints[2].text = ControllerMappingScript.GetPlaceMarkerKeyHint();
            if (Type == MapTypes.WorldMap && MapManager.CurrentSetting.EnableCustomMarker) CustomMarkerHints.text = ControllerMappingScript.GetPlaceCustomMarkerHint();
        }

       

        public void RefreshMapImage()
        {
            MapManager.SetMapTexture(ref MapImage, ref MapOffset);
        }

        private void RecreateLayerButtonList()
        {
            foreach (var obj in LayerList)
            {
                Destroy(obj.gameObject);
            }
            LayerList.Clear();
            if (MapManager.CurrentSceneMapSetting != null)
            {
                int _subMapIndex = -1;
                for (int i = 0; i < MapManager.CurrentSceneMapSetting._subMaps.Count; i++)
                {
                    if (MapManager.CurrentSceneMapSetting._subMaps[i].uid == currentSubMap)
                    {
                        _subMapIndex = i;
                        break;
                    }
                }
                if (currentSubMap != "" && _subMapIndex>=0)
                {
                    if (MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps.Count > 1)
                    {
                        for (int i = 0; i < MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps.Count; i++)
                        {
                            GameObject _newLayer = Instantiate(LayerPrefab, LayerPrefab.transform.parent);
                            _newLayer.name = i.ToString();
                            _newLayer.SetActive(true);
                            _newLayer.transform.SetAsFirstSibling();
                            LayerList.Add(_newLayer.GetComponent<Button>());
                        }
                    }
                    
                }
                else
                {
                    if (MapManager.CurrentSceneMapSetting._maps.Count > 1)
                    {
                        for (int i = 0; i < MapManager.CurrentSceneMapSetting._maps.Count; i++)
                        {
                            GameObject _newLayer = Instantiate(LayerPrefab, LayerPrefab.transform.parent);
                            _newLayer.name = i.ToString();
                            _newLayer.SetActive(true);
                            _newLayer.transform.SetAsFirstSibling();
                            LayerList.Add(_newLayer.GetComponent<Button>());
                        }
                    }
                }
            }
        }

       

        private void RecreateCatergoryList()
        {
            foreach (var obj in CategoryList)
            {
                Destroy(obj.gameObject);
            }
            CategoryList.Clear();
            for (int i = 0; i < MapManager.CurrentSetting.IconGroups.Count; i++)
            {
                GameObject _newGroup = Instantiate(CategoryPrefab, CategoryPrefab.transform.parent);
                CategoryList.Add(_newGroup.GetComponent<CanvasGroup>());
                _newGroup.SetActive(true);
                _newGroup.GetComponent<RawImage>().texture = MapManager.CurrentSetting.IconGroups[i]._icon;
                _newGroup.GetComponent<CanvasGroup>().alpha = MapManager.IconCategoryVisible[i] ? 1f : 0.4f;
                _newGroup.GetComponent<HintText>().HintString = MapManager.CurrentSetting.IconGroups[i]._name;
            }
        }

        private void RecreateCustomMarkerList()
        {
            foreach (var obj in CustomMarkerList.Keys)
            {
                Destroy(CustomMarkerList[obj].gameObject);
            }
            CustomMarkerList.Clear();
            for (int i = 0; i < MapManager.CurrentSetting.CustomMarkerSettings.Count; i++)
            {
                GameObject _newGroup = Instantiate(CustomMarkerListPrefab, CustomMarkerListPrefab.transform.parent);
                CustomMarkerList.Add(MapManager.CurrentSetting.CustomMarkerSettings[i]._uid,_newGroup.GetComponent<CanvasGroup>());
                _newGroup.SetActive(true);
                _newGroup.GetComponent<RawImage>().texture = MapManager.CurrentSetting.CustomMarkerSettings[i]._icon;
                _newGroup.GetComponent<CanvasGroup>().alpha = SelectedCustomMarker== MapManager.CurrentSetting.CustomMarkerSettings[i]._uid ? 1f : 0.7f;
                _newGroup.GetComponent<HintText>().HintString = MapManager.CurrentSetting.CustomMarkerSettings[i]._defaultName;
                _newGroup.gameObject.name = MapManager.CurrentSetting.CustomMarkerSettings[i]._uid;
            }
        }

        private void BoundCheck()
        {
            float _screenX = TopRight.localPosition.x - DownLeft.localPosition.x - 10f;
            float _screenY = TopRight.localPosition.y - DownLeft.localPosition.y - 10f;
            float _sizeX = MapOffset.sizeDelta.x - 10F;
            float _sizeY = MapOffset.sizeDelta.y - 10F;
            if (_mapScale * _sizeX < _screenX || _mapScale * _sizeY < _screenY)
            {
                _mapScale = Mathf.Max(_screenX / _sizeX, _screenY / _sizeY);
                MapScale = _mapScale;
            }
            outBound = (MapScale * _sizeX-1F <= _screenX || MapScale * _sizeY-1F <= _screenY);
            float _minX = transform.position.x + _screenX * 0.5F * CanvasScale - _sizeX * _mapScale * CanvasScale * 0.5f - MapImage.transform.localPosition.x * _mapScale * CanvasScale;
            float _maxX = transform.position.x - _screenX * 0.5F * CanvasScale + _sizeX * _mapScale * CanvasScale * 0.5f - MapImage.transform.localPosition.x * _mapScale * CanvasScale;
            float _minY = transform.position.y + _screenY * 0.5F * CanvasScale - _sizeY * _mapScale * CanvasScale * 0.5f - MapImage.transform.localPosition.y * _mapScale * CanvasScale;
            float _maxY = transform.position.y - _screenY * 0.5F * CanvasScale + _sizeY * _mapScale * CanvasScale * 0.5f - MapImage.transform.localPosition.y * _mapScale * CanvasScale;
            if(!MapManager.CurrentSetting.PanBeyondBounds) MapPos = new Vector3(Mathf.Clamp(MapPos.x, _minX , _maxX ), Mathf.Clamp(MapPos.y, _minY , _maxY ), 0f);
        }

       

        private bool isAtLimit(float _scroll)
        {
                if (_scroll < 0f)
                {
                    return outBound || MapScale <= GetMinScale();
                }
                else
                {
                    return MapScale >=GetMaxScale();
                }
        }

        private void MapMove()
        {
            if (Type == MapTypes.MiniMap || !MapManager.CurrentSetting.EnableScroll) return;
            if (CustomMarkerRenamePanel && CustomMarkerRenamePanel.activeSelf) return;
            if (ControllerMappingScript.GetScrollUpKey())
            {
                MapPos.y -= MapMoveSpeed * _mapScale * 180F*Time.deltaTime * MapManager.CurrentSetting.ScrollSpeed;
                if (Zooming) MapScale = _mapScale;
            }
            if (ControllerMappingScript.GetScrollDownKey())
            {
                MapPos.y += MapMoveSpeed * _mapScale * 180F * Time.deltaTime * MapManager.CurrentSetting.ScrollSpeed;
                if (Zooming) MapScale = _mapScale;
            }
            if (ControllerMappingScript.GetScrollLeftKey())
            {
                MapPos.x += MapMoveSpeed * _mapScale * 180F * Time.deltaTime * MapManager.CurrentSetting.ScrollSpeed;
                if (Zooming) MapScale = _mapScale;
            }
            if (ControllerMappingScript.GetScrollRightKey())
            {
                MapPos.x -= MapMoveSpeed * _mapScale * 180F * Time.deltaTime * MapManager.CurrentSetting.ScrollSpeed;
                if (Zooming) MapScale = _mapScale;
            }
        }

       

        public void PlayerIconState()
        {
            PlayerIcon.localPosition = WorldToMapPosition(MapManager.Player.position);
            PlayerIcon.localEulerAngles = new Vector3(0f, 0f, MapManager.FormAngle(-Player.eulerAngles.y));
            PlayerIcon.localScale = Vector3.one / _mapScale;
        }

        public void OnLayerClick(GameObject _layer)
        {
            SwitchMapLayer(int.Parse(_layer.name));
        }

        public void OnCategoryClick(CanvasGroup _group)
        {
            MapManager.IconCategoryVisible[CategoryList.IndexOf(_group)] = !MapManager.IconCategoryVisible[CategoryList.IndexOf(_group)];
            _group.alpha = MapManager.IconCategoryVisible[CategoryList.IndexOf(_group)] ? 1f : 0.4f;
        }

        public void ClikMap()
        {
            if (Type == MapTypes.MiniMap) return;
            Vector3 _mapPos = TransferPos(ControllerMappingScript.GetPointerPosition(), MapOffset);
            Vector3 _worldPos = MapToWorldPosition(MapOffset.InverseTransformPoint(_mapPos));
           
            float _ceil = 1000F;
            if (MapManager.CurrentSceneMapSetting != null)
            {
                _worldPos.y = MapManager.CurrentSceneMapSetting._maps[ViewingLayer]._height;
                if (MapManager.CurrentSceneMapSetting._maps.Count > ViewingLayer + 1) _ceil = MapManager.CurrentSceneMapSetting._maps[ViewingLayer + 1]._height - _worldPos.y - 1F;
            }
            RaycastHit hit;
            if (Physics.Raycast(_worldPos + Vector3.up * _ceil, Vector3.down, out hit, 2000F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                _worldPos.y = hit.point.y;
                if (ControllerMappingScript.GetPlaceMarkerKey() && MapManager.CurrentSetting.EnableMarkers)
                {
                    MapManager.PlaceMarker(_worldPos);
                } 
                else if (ControllerMappingScript.GetConfirmCustomMarkerKey() && MapManager.CurrentSetting.EnableCustomMarker && SelectedCustomMarker!="" && !ControllerMappingScript.GetCancelCustomMarkerKey() && CustomMarkerListCanvas.alpha>0F) {
                    MapManager.PlaceCustomMarker(_worldPos, SelectedCustomMarker);
                }
                else if (MapManager.CurrentSetting.EnableNavigationPath && ControllerMappingScript.GetNavigateKey() && (!MapManager.CurrentSetting.EnableCustomMarker || CustomMarkerListCanvas.alpha == 0F))
                {
                    MapManager.NavigateToHere(_worldPos);
                    SwitchMapLayer(ViewingLayer);
                }
            }
        }

       



        public void OnMapScroll(PointerEventData data)
        {
            if (Type == MapTypes.MiniMap && MapManager.CurrentSetting.AllowZoomInOut == false)
            {
                return;
            }
            Vector3 _center = new Vector2(ControllerMappingScript.GetPointerPosition().x / Screen.width, ControllerMappingScript.GetPointerPosition().y / Screen.height);
            if (data.scrollDelta.y < 0F)
            {
                Zoom(-ZoomStep, _center);
            }
            else
            {
                Zoom(ZoomStep, _center);
            }
        }

        public void OnMapPointerDown()
        {
            if (Type == MapTypes.MiniMap) return;
            if (Zooming) MapScale = _mapScale;
            StartPos = TransferPos(ControllerMappingScript.GetPointerPosition(), MapDrag.parent.GetComponent<RectTransform>());
            StartMapPos = MapPos;
        }

        public void OnMapDrag()
        {
            if (Type == MapTypes.MiniMap) return;
            if (Zooming) MapScale = _mapScale;
            Vector3 _dragPos = TransferPos(ControllerMappingScript.GetPointerPosition(), MapDrag.parent.GetComponent<RectTransform>());
            MapPos = StartMapPos + (_dragPos - StartPos);
        }

        public void SubMapClose()
        {
            if (Type == MapTypes.MiniMap) return;
            if (SubMapCloseButton.gameObject.activeSelf) {
                SubMapCloseButton.gameObject.SetActive(false);
                Title.text = MapManager.CurrentSceneMapSetting._sceneTitle;
                MapManager.SetMapTexture(ref MapImage, ref MapOffset);
                if (Type == MapTypes.WorldMap) RecreateLayerButtonList();
                MapScale = GetMapScale();
            }

        }

        #endregion




        #region Callbacks
        public void OnMapIconClick(MapPoint _point, int _button)
        {
            if (Type == MapTypes.MiniMap) return;
            if (_point.ClickToOpenSubMap)
            {
                foreach (var submap in MapManager.CurrentSceneMapSetting._subMaps)
                {
                    if (_point.SubMapUid==submap.uid)
                    {
                        Title.text = submap._areaTitle;
                        MapManager.SetMapTexture(ref MapImage, ref MapOffset, _point.SubMapUid);
                        SubMapButtonText.text = "Return to <" + MapManager.CurrentSceneMapSetting._sceneTitle + ">";
                        SubMapCloseButton.gameObject.SetActive(true);
                        if (Type == MapTypes.WorldMap) RecreateLayerButtonList();
                        MapScale = GetMapScale();
                        break;
                    }
                }
                
            }
            if (_button == (int)MapManager.CurrentSetting.MapMarkerButton && MapManager.CurrentSetting.EnableCustomMarker)
            {
                if (MapManager.CurrentSetting.AllowPlayerToRenameCustomMarker && IconDic.ContainsKey(_point.transform))
                {
                    CustomMarkerPopMenu.position = IconDic[_point.transform].RectTrans.position;
                    PendingCustomMarker = _point;
                    CustomMarkerPopRoot.SetActive(true);
                }
                else
                {
                    MapManager.RemoveCustomMarker(_point);
                }
            }
            if (_button == (int)MapManager.CurrentSetting.MapMarkerButton && MapManager.CurrentSetting.EnableMarkers)
            {
                MapManager.RemoveMarker(_point);
            }
            else if (MapManager.CurrentSetting.EnableNavigationPath && _button == (int)MapManager.CurrentSetting.StartNavigationMouseButton)
            {
                if (!NavigationPath.isSameDestination(_point.transform ) )
                {
                    NavigationPath.GetPath(_point.transform);
                }
                else
                {
                    NavigationPath.Stop();
                }
            }
        }


        public void OnMapDataUpdate(Transform _key, MapPointData _data)
        {
            if (IconDic.ContainsKey(_key))
            {
                IconDic[_key].UpdateData(_data);
            }
        }

        public void OnMapStateChange(Transform _key, int _state)
        {
            if (IconDic.ContainsKey(_key))
            {
                IconDic[_key].SetState(_state);
            }
        }
        public void OnMapPointCreate(Transform _key, MapPointData _data, int _state)
        {
            GameObject _newIcon = Instantiate(IconPrefab, IconPrefab.transform.parent);
            _newIcon.GetComponent<MapIcon>().SetData(_key, _data, _state);
            _newIcon.SetActive(true);
            _newIcon.GetComponent<MapIcon>().SetParent(this);
            PlayerIcon.SetAsLastSibling();
            IconDic.Add(_key, _newIcon.GetComponent<MapIcon>());
        }
        public void OnMapPointRemove(Transform _key)
        {
            if (IconDic.ContainsKey(_key))
            {
                IconDic[_key].DestroyMe();
                IconDic.Remove(_key);
            }
        }

        #endregion



        /// <summary>
        /// This method moves the map to focus on a specific 3D world position.
        ///_worldPosition: The 3D world position to focus on.
        ///_center: A normalized screen position (ranging from 0 to 1) that determines where the focused position will appear on the map.
        ///For example, to center the player¡¯s position on the map, you would use:
        ///Focus(Player.position, new Vector2(0.5f, 0.5f));
        ///Here, (0.5f, 0.5f) represents the center of the screen.Use other values to place the position elsewhere (e.g., (0, 0) for the bottom-left corner or(1, 1) for the top-right corner).
        /// </summary>
        /// <param name="_worldPosition"></param>
        /// <param name="_center"></param>
        public void Focus(Vector3 _worldPosition, Vector2 _center)
        {
            MapPos = GetFocusPoint(_worldPosition, _center);
        }


        /// <summary>
        /// This method zooms the map in or out by a specified value and optionally adjusts the focus point.
        ///_value: Determines the zoom level. Positive values zoom in. Negative values zoom out.
        ///_focus: A normalized screen position (ranging from 0 to 1) that specifies where the zoom will focus.
        ///For example, (0.5f, 0.5f) represents the center of the screen.
        ///Use other values (e.g., (0, 0) for bottom-left or (1, 1) for top-right) to focus on a different part of the map.
        ///If you don't need to focus on a specific position, pass new Vector2(0.5f, 0.5f) to keep the zoom centered on the screen.
        /// </summary>
        /// <param name="_value"></param>
        /// <param name="_focus"></param>
        public void Zoom(float _value,Vector2 _focus)
        {
            if (isAtLimit(_value*0.1F)) return;
           
            if (!Zooming)
            {
                Vector3 _mousePos = new Vector3(_focus.x * Screen.width, _focus.y*Screen.height,0F);
                Vector3 _mapPos = TransferPos(_mousePos, MapOffset);
                Vector3 _focusLocalPoint = GetComponent<RectTransform>().InverseTransformPoint( TransferPos(_mousePos, GetComponent<RectTransform>()));
                Vector3 _screen = TopRight.localPosition - DownLeft.localPosition;
                Vector2 _size = new Vector2(_screen.x, _screen.y);
                Vector2 _focusPoint = new Vector2((_focusLocalPoint.x + _size.x*0.5F) / _size.x, (_focusLocalPoint.y + _size.y * 0.5F) / _size.y);
                ScrollOffset = _focusPoint;
                ScrollFocus = MapToWorldPosition(MapOffset.InverseTransformPoint(_mapPos));
            }
            normalizedScale = Mathf.Clamp(normalizedScale + _value * 0.1F, 0F, 1F);
            MapScale = GetMapScale();
        }


        /// <summary>
        /// Switches the map to a specified layer. This only affects the current map instance interface; it does not alter the player¡¯s position or any other map instance.
        /// </summary>
        /// <param name="_layer"></param>
        public void SwitchMapLayer(int _layer)
        {
            if (MapManager.CurrentSetting.MapMode == MMN_Settings.MapModes.DynamicMap) return;
            ViewingLayer = _layer;
           
            
            if (Type == MapTypes.WorldMap)
            {
                for (int i = 0; i < LayerList.Count; i++)
                {
                    LayerList[i].interactable = (i != ViewingLayer);
                }
            }

            int _subMapIndex = -1;
            for(int i=0;i< MapManager.CurrentSceneMapSetting._subMaps.Count;i++) {
                if (MapManager.CurrentSceneMapSetting._subMaps[i].uid == currentSubMap)
                {
                    _subMapIndex = i;
                    break;
                }
            }

            if (currentSubMap != "" && _subMapIndex>=0)
            {
                
                MapImage.texture = MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps[ViewingLayer]._map;
                float _ceil = 10000f;
                if (ViewingLayer < MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps.Count - 1)
                {
                    _ceil = MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps[ViewingLayer + 1]._height;
                }
                foreach (var _key in IconDic.Keys)
                {
                    IconDic[_key].Visible = (_key.position.y >= MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps[ViewingLayer]._height && _key.position.y < _ceil);
                }
                PlayerIcon.GetComponent<RawImage>().color = new Color(1F, 1F, 1F,
                    MapManager.Player.position.y >= MapManager.CurrentSceneMapSetting._subMaps[_subMapIndex]._maps[ViewingLayer]._height && MapManager.Player.position.y < _ceil ? 1F : 0.2F);
            }
            else
            {
                MapImage.texture = MapManager.CurrentSceneMapSetting._maps[ViewingLayer]._map;
                float _ceil = 10000f;
                if (ViewingLayer < MapManager.CurrentSceneMapSetting._maps.Count - 1)
                {
                    _ceil = MapManager.CurrentSceneMapSetting._maps[ViewingLayer + 1]._height;
                }
                foreach (var _key in IconDic.Keys)
                {
                    IconDic[_key].Visible = (_key.position.y >= MapManager.CurrentSceneMapSetting._maps[ViewingLayer]._height && _key.position.y < _ceil);
                }
                PlayerIcon.GetComponent<RawImage>().color = new Color(1F, 1F, 1F,
                    MapManager.Player.position.y >= MapManager.CurrentSceneMapSetting._maps[ViewingLayer]._height && MapManager.Player.position.y < _ceil ? 1F : 0.2F);
            }
            NavLine.color = new Color(NavLine.color.r, NavLine.color.g, NavLine.color.b, PlayerIcon.GetComponent<RawImage>().color.a);
        }


        /// <summary>
        /// Toggles Player Orientation Mode, aligning the player¡¯s direction to always point toward the top of the interface.
        /// </summary>
        public void Compass()
        {
            MapManager.LockPlayerForwardToNorth = !MapManager.LockPlayerForwardToNorth;
        }

        /// <summary>
        /// Converts a world space position to a map space position. The resulting map position is 2D, so the Y-axis value will always be 0.  
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <returns></returns>
        public static Vector3 WorldToMapPosition(Vector3 _worldPos)
        {
            return new Vector3(_worldPos.x, _worldPos.z, 0f);
        }

        /// <summary>
        /// Converts a world space position to a map space position.  
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <returns></returns>
        public static Vector2 WorldToMapPositionVector2(Vector3 _worldPos)
        {
            return new Vector2(_worldPos.x, _worldPos.z);
        }

        /// <summary>
        /// Convert a world space position to a normalized map space position.  
        /// </summary>
        /// <param name="_worldPos"></param>
        /// <returns></returns>
        public static Vector2 WorldToNormalizedMapPosition(Vector3 _worldPos)
        {
            return (WorldToMapPositionVector2(_worldPos)-MapManager.GetCurrentMapRect().position)/ MapManager.GetCurrentMapRect().size;
        }

        /// <summary>
        /// Converts a map space position back to a world space position. The Y-axis value remains 0, as the map space is strictly 2D.
        /// </summary>
        /// <param name="_mapPos"></param>
        /// <returns></returns>
        public static Vector3 MapToWorldPosition(Vector3 _mapPos)
        {
            return new Vector3(_mapPos.x, 0f, _mapPos.y);
        }

        /// <summary>
        /// Transfrom mouse position to map position.
        /// </summary>
        /// <param name="_pos"></param>
        /// <param name="_parentTransform"></param>
        /// <returns></returns>
        public Vector3 TransferPos(Vector3 _pos, RectTransform _parentTransform)
        {
            Vector2 localPosition = Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentTransform,
                _pos,
                RootCanvas.renderMode == RenderMode.ScreenSpaceCamera ? RootCanvas.worldCamera : null,
                out localPosition);
            return _parentTransform.TransformPoint(localPosition);
        }

        /// <summary>
        /// Toggle the list of custom marker for player to select, by selecting from the list player will be able to place it on the map.
        /// </summary>
        public void ToggleCustomMarkerList()
        {
            if (Type != MapTypes.WorldMap) return;
            SelectCustomMarker("");
            CustomMarkerListCanvas.alpha = 1F- CustomMarkerListCanvas.alpha;
            CustomMarkerListCanvas.interactable = (CustomMarkerListCanvas.alpha == 1F);
            CustomMarkerListCanvas.blocksRaycasts = (CustomMarkerListCanvas.alpha == 1F);

        }

        public void SelectCustomMarker(string _uid)
        {
            SelectedCustomMarker = _uid;
            foreach (var obj in CustomMarkerList.Keys){
                CustomMarkerList[obj].alpha = (CustomMarkerList[obj].name == _uid ? 1F : 0.7F);
                CustomMarkerList[obj].interactable = (CustomMarkerList[obj].alpha != 1F);
            }
        }

        public void SelectCustomMarkerUI(GameObject _iconItem)
        {
            SelectCustomMarker(_iconItem.name);
        }
    }
}