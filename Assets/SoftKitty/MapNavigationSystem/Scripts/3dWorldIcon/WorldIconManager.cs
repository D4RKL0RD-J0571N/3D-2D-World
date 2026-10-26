using UnityEngine;
using System.Collections.Generic;

namespace SoftKitty.MasterNavigationMap
{
    public class WorldIconManager : MonoBehaviour
    {
        public int MaximumAllowedIcons = 20;
        public GameObject IconPrefab;
        private Dictionary<Transform, MapPointPackage> IconDataDic = new Dictionary<Transform, MapPointPackage>();
        private Dictionary<Transform, WorldIcon> IconDic = new Dictionary<Transform, WorldIcon>();
        private List<Transform> Icons = new List<Transform>();
        private List<WorldIcon> PoolObjects = new List<WorldIcon>();
        private List<Transform> AvailableIcons = new List<Transform>();

        private void Awake()
        {
            MapManager.SceneCheck();
            MapManager.MapPointStateChangeCallback += OnMapStateChange;
            MapManager.MapPointCreateCallback += OnMapPointCreate;
            MapManager.MapPointRemoveCallback += OnMapPointRemove;
        }

        private void OnEnable()
        {
            foreach (Transform t in MapManager.MapPointDic.Keys)
            {
                if (!IconDataDic.ContainsKey(t))
                {
                    OnMapPointCreate(t, MapManager.MapPointDic[t], t.GetComponent<MapPoint>().State);
                }
            }
        }

        private void CreateIcon(Transform _key, MapPointPackage _package)
        {
            if (_package.Data.Icon != null)
            {
                if (!IconDic.ContainsKey(_key))
                {
                    if (PoolObjects.Count > 0)
                    {
                        PoolObjects[0].SetData(_key, _package.Data, _package.State);
                        PoolObjects[0].gameObject.SetActive(true);
                        IconDic.Add(_key, PoolObjects[0]);
                        PoolObjects.RemoveAt(0);
                    }
                    else
                    {
                        GameObject _newIcon = Instantiate(IconPrefab, IconPrefab.transform.parent);
                        _newIcon.GetComponent<WorldIcon>().SetData(_key, _package.Data, _package.State);
                        _newIcon.SetActive(true);
                        IconDic.Add(_key, _newIcon.GetComponent<WorldIcon>());
                    }
                }
                if (!Icons.Contains(_key)) Icons.Add(_key);
            }
        }

        private void RemoveIcon(Transform _key)
        {
            if (IconDic.ContainsKey(_key))
            {
                IconDic[_key].gameObject.SetActive(false);
                PoolObjects.Add(IconDic[_key]);
                IconDic.Remove(_key);
            }
            if (Icons.Contains(_key)) Icons.Remove(_key);
        }

        public void OnMapStateChange(Transform _key, int _state)
        {
            if (IconDataDic.ContainsKey(_key))
            {
                IconDataDic[_key].State = _state;
                if (IconDic.ContainsKey(_key))
                {
                    IconDic[_key].SetState(_state);
                }
            }
        }

        public void OnMapPointCreate(Transform _key, MapPointData _data, int _state)
        {
            if (_data.ShowIconIn3dWorld && _data.Icon != null && !IconDataDic.ContainsKey(_key))
            {
                IconDataDic.Add(_key, new MapPointPackage(_data, _state));
            }
        }
        public void OnMapPointRemove(Transform _key)
        {
            if (IconDataDic.ContainsKey(_key)) IconDataDic.Remove(_key);
            RemoveIcon(_key);
        }

        private void OnDestroy()
        {
            MapManager.MapPointStateChangeCallback -= OnMapStateChange;
            MapManager.MapPointCreateCallback -= OnMapPointCreate;
            MapManager.MapPointRemoveCallback -= OnMapPointRemove;
        }

        void Update()
        {
            if (Time.frameCount % 60 == 0)
            {
                AvailableIcons.Clear();
                foreach (var key in IconDataDic.Keys)
                {
                    if (isInView(key, IconDataDic[key].Data.IconDisappearDistanceIn3dWorld)) AvailableIcons.Add(key);
                }
                AvailableIcons.Sort(SortByDistanceReverse);
                if (AvailableIcons.Count > MaximumAllowedIcons) AvailableIcons.RemoveRange(MaximumAllowedIcons, AvailableIcons.Count - MaximumAllowedIcons);
                List<Transform> _iconToRemove = new List<Transform>();
                foreach (var key in IconDic.Keys)
                {
                    if (!AvailableIcons.Contains(key)) _iconToRemove.Add(key);
                }
                foreach (var key in _iconToRemove) RemoveIcon(key);
                for (int i = 0; i < AvailableIcons.Count; i++)
                {
                    CreateIcon(AvailableIcons[i], IconDataDic[AvailableIcons[i]]);
                }

                Icons.Sort(SortByDistance);
                for (int i = 0; i < Icons.Count; i++)
                {
                    if (IconDic.ContainsKey(Icons[i])) IconDic[Icons[i]].transform.SetAsLastSibling();
                }
            }
        }

        private bool isInView(Transform _point, float _distance)
        {
            if (Vector3.Distance(MapManager.Player.position, _point.position) >= _distance) return false;
            Vector3 _dir = _point.position - MapManager.PlayerCamera.transform.position;
            Vector3 _forward = MapManager.PlayerCamera.transform.forward;
            return Vector3.Angle(_dir.normalized, _forward.normalized) <90F;
        }

        static int SortByDistance(Transform p1, Transform p2)
        {
            return Vector3.Distance(p2.position, MapManager.Player.position).CompareTo(Vector3.Distance(p1.position, MapManager.Player.position));
        }

        static int SortByDistanceReverse(Transform p1, Transform p2)
        {
            return Vector3.Distance(p1.position, MapManager.Player.position).CompareTo(Vector3.Distance(p2.position, MapManager.Player.position));
        }
    }
}
