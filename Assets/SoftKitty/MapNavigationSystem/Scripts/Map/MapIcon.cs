using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SoftKitty.MasterNavigationMap
{
    public class MapIcon : MonoBehaviour,IPointerClickHandler
    {
        public enum IconType
        {
            NavigationBar,
            MiniMap,
            WorldMap,
            GameWorld
        }
        public IconType Type;
        public RawImage MainIcon;
        public RawImage StateIcon;
        public Image Range;
        public Text IconText;
        public RectTransform RectTrans;
        public CanvasGroup Group;
        private MapInteractive ParentScript;

        public bool Visible
        {
            set
            {
                _visible = value;
                Group.alpha = value ? 1f : 0f;
            }
            get
            {
                return _visible;
            }
        }
        private bool _visible = true;
        protected Transform point;
        protected MapPointData data;
        protected Transform Player
        {
            get
            {
                if (MapManager.CurrentSetting.PlayerOrientation == MMN_Settings.PlayerOrientations.PlayerTransform)
                    return MapManager.Player;
                else
                    return MapManager.PlayerCamera.transform;
            }
        }
        public virtual void Start()
        {

        }

        protected bool isVisible()
        {
            bool _fogVisible = true;
            if (MapManager.CurrentSceneMapSetting._fogOfWarEnable && MapManager.CurrentSceneMapSetting._hideIconsInFog && FogOfWar.instance!=null)
            {
                _fogVisible = !FogOfWar.instance.isInFog(point.position);
            }
            return  data.Visible && _fogVisible && MapManager.IconCategoryVisible[data.GroupId] && Visible && point.gameObject.activeInHierarchy && ((Type!= IconType.NavigationBar && Type != IconType.GameWorld) || MapManager.GetLayerByPosition(point.position)==MapManager.CurrentMapLayer);
        }
        public virtual void Update()
        {
            if (ParentScript != null && ParentScript.ControllerMappingScript != null)
            {
                if (ParentScript.ControllerMappingScript.UseVirtualCursor)
                {
                    if (ParentScript.ControllerMappingScript.isVirtualCursorOver(RectTrans))
                    {
                        if (ParentScript.ControllerMappingScript.GetRemoveMarkerKey())
                            MapManager.MapIconClickCallback(point.GetComponent<MapPoint>(), (int)MapManager.CurrentSetting.MapMarkerButton);
                        else if (ParentScript.ControllerMappingScript.GetNavigateKey())
                            MapManager.MapIconClickCallback(point.GetComponent<MapPoint>(), (int)MapManager.CurrentSetting.StartNavigationMouseButton);
                    }
                }
            }

            if (point != null)
            {
                RectTrans.localPosition = MapInteractive.WorldToMapPosition(point.position);
                if(Group.alpha!= (isVisible() ? 1f : 0f)) Group.alpha = Mathf.MoveTowards(Group.alpha, isVisible() ? 1f:0f, Time.deltaTime * 2F);
                if(MainIcon)MainIcon.raycastTarget= isVisible();
            }

           
            if (ParentScript != null)
            {
                if (ParentScript.Type == MapInteractive.MapTypes.MiniMap)
                {
                    if (MapManager.CurrentSetting.MiniLockIconScale || data.AlwaysKeepSize)
                    {
                        transform.localScale = Vector3.one * (GetScale() / ParentScript.CurrentMapScale);
                    }
                    else
                    {
                        transform.localScale = Vector3.one * 0.5F * GetScale();
                    }
                    if (MapManager.CurrentSetting.MiniMapRotateWithCamera && MapManager.PlayerCamera != null)
                    {
                        if (transform.localEulerAngles.z != MapManager.FormAngle(-MapManager.PlayerCamera.transform.eulerAngles.y)) transform.localEulerAngles = new Vector3(0F, 0F, MapManager.FormAngle(-MapManager.PlayerCamera.transform.eulerAngles.y));
                    }
                    else if (MapManager.LockPlayerForwardToNorth)
                    {
                        if (transform.localEulerAngles.z != MapManager.FormAngle(-Player.eulerAngles.y)) transform.localEulerAngles = new Vector3(0F, 0F, MapManager.FormAngle(-Player.eulerAngles.y));
                    }
                    else
                    {
                        if (transform.localEulerAngles.z != 0F) transform.localEulerAngles = Vector3.zero;
                    }
                }
                else
                {
                    if (MapManager.CurrentSetting.LockIconScale || data.AlwaysKeepSize)
                    {
                        transform.localScale = Vector3.one * (GetScale() / ParentScript.CurrentMapScale);
                    }
                    else
                    {
                        transform.localScale = Vector3.one *0.5F* GetScale();
                    }
                }
            }
     
        }


        private float GetScale()
        {
            return data.Poping ? Mathf.Sin(Time.time * 5F) * 0.3F + 0.7F : 1F;
        }

        public void DestroyMe()
        {
            if (this != null)
            {
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(DestroyCo());
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        IEnumerator DestroyCo()
        {
            while (Group.alpha > 0F)
            {
                Group.alpha = Mathf.MoveTowards(Group.alpha, 0F, Time.deltaTime * 4F);
                yield return 1;
            }
            Destroy(gameObject);
        }

        public void SetParent(MapInteractive _parent)
        {
            ParentScript = _parent;
        }


        public void SetState(int _state)
        {
            StateIcon.gameObject.SetActive(_state > -1);
            if (_state > -1) StateIcon.texture = data.StateIcons[Mathf.Clamp(_state, 0, data.StateIcons.Length - 1)];
        }


        public void UpdateData(MapPointData _data)
        {
            data = _data;
            if (data.DisapperDistance == 0) data.DisapperDistance = 100F;
            if (data.Icon != null || data.DisplayIcon)
            {
                MainIcon.enabled = true;
                MainIcon.texture = data.Icon;
                MainIcon.color = data.IconColor;
                MainIcon.rectTransform.sizeDelta = data.IconSize * Vector2.one;
                if (Type != IconType.NavigationBar && Type != IconType.GameWorld) MainIcon.rectTransform.anchoredPosition = data.IconOffset;
            }
            else
            {
                MainIcon.enabled = false;
            }
            if (GetComponent<HintText>()) GetComponent<HintText>().HintString = data.HintText;
            if (Range != null)
            {
                if (data.Range > 0f)
                {
                    Range.gameObject.SetActive(true);
                    Range.transform.localScale = data.Range * Vector3.one;
                    Range.color = data.RangeColor;
                }
                else
                {
                    Range.gameObject.SetActive(false);
                }
            }

            if (IconText != null)
            {
                if (data.Text != "" &&
                    (
                    (data.DisplayTextInMiniMap && Type == IconType.MiniMap)
                    || (data.DisplayTextInNavigationBar && Type == IconType.NavigationBar)
                    || (data.DisplayTextIn3dWorld && Type == IconType.GameWorld)
                    || (data.DisplayTextInWorldMap && Type == IconType.WorldMap)
                    ))
                {
                    IconText.text = data.Text;
                    IconText.gameObject.SetActive(true);
                    IconText.color = data.TextColor;
                    IconText.GetComponent<RectTransform>().anchoredPosition = data.TextOffset - (Type == IconType.NavigationBar ? data.IconOffset : Vector2.zero);
                    IconText.fontSize = data.FontSize;
                }
                else
                {
                    IconText.gameObject.SetActive(false);
                }
            }
        }

        public void SetData(Transform _point, MapPointData _data, int _state)
        {
            point = _point;
            Group.alpha = 0F;
            UpdateData(_data);
            SetState(_state);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            MapManager.MapIconClickCallback(point.GetComponent<MapPoint>(), (int)eventData.button);
        }
    }
}
