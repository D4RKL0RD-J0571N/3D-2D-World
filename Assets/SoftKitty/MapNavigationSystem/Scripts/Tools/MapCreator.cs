using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoftKitty.MasterNavigationMap
{
    [System.Serializable]
    public class MapCreatorTagData
    {
        public string _name;
        public LayerMask _layer;
        public string _tag;
        public Color _color;
        public int _id;
        public bool _expand = false;
    }

    public class MapCreator : MonoBehaviour
    {
        public enum MapSize
        {
            Square_512,
            Square_1024,
            Square_2048,
            Square_4096,
            Square_8192
        }
        [HideInInspector]
        public string FilePath = "E:/map.png";
        [HideInInspector]
        public MapSize Size= MapSize.Square_1024;
        [HideInInspector]
        public LayerMask GroundLayer;
        [HideInInspector]
        public Color GroundColor;
        [HideInInspector]
        public Color MountainColor;
        [HideInInspector]
        public Color WaterColor;
        [HideInInspector]
        public Color BorderColor;
        [HideInInspector]
        public Texture2D PatternTexture;
        [HideInInspector]
        public bool UseBackGroundTexture;
        [HideInInspector]
        public Texture2D BackGroundTexture;

        public Transform BottomLeft;
        public Transform TopRight;

        [HideInInspector]
        public List<MapCreatorTagData> CustomColors = new List<MapCreatorTagData>();
        [HideInInspector]
        public float Height = 500F;
        [HideInInspector]
        public float Lowest = -10F;
        [HideInInspector]
        public int[,] _ids = new int[4096, 4096];
        [HideInInspector]
        public float[,] _heights = new float[4096, 4096];
        [HideInInspector]
        public Vector3[,] _normals = new Vector3[4096, 4096];
        [HideInInspector]
        public float[,] _outNavmesh = new float[4096, 4096];
        [HideInInspector]
        public float MountainAngleThreshold = 10F;
        [HideInInspector]
        public float MountainStepHeightThreshold = 0.3F;
        [HideInInspector]
        public float GroundDetailIntensity = 0.3F;
        [HideInInspector]
        public float GroupEdgeIntensity = 0.5F;
        [HideInInspector]
        public float AoIntensity = 0.5F;
        [HideInInspector]
        public int SampleRadius = 30;
        [HideInInspector]
        public Color InNavmeshColor = Color.white;
        [HideInInspector]
        public Color OutsideNavmeshColor = Color.white;

        [HideInInspector]
        public SceneMapSetting CurrentMap;
        [HideInInspector]
        public int CurrentLayer = 0;
        [HideInInspector]
        public bool BakeForSubMap = false;
        [HideInInspector]
        public int SubMapIndex = 0;
        [HideInInspector]
        public bool Preview = true;
        public void Init()
        {
            var _sceneMaps = SGD_Settings.Instance.GetData<MMN_Settings>().SceneMaps;
            Dictionary<string, SceneMapSetting> _sceneMapDic = new Dictionary<string, SceneMapSetting>();
            for (int i = 0; i < _sceneMaps.Count; i++)
            {
                _sceneMapDic.Add(_sceneMaps[i]._sceneName, _sceneMaps[i]);
            }
            string _scene = SceneManager.GetActiveScene().name;
            if (_sceneMapDic.ContainsKey(_scene))
            {
                CurrentMap = _sceneMapDic[_scene];
                if (BakeForSubMap)
                {
                    if (SubMapIndex< CurrentMap._subMaps.Count) {
                        if (CurrentLayer < CurrentMap._subMaps[SubMapIndex]._maps.Count)
                        {
                            Lowest = CurrentMap._subMaps[SubMapIndex]._maps[CurrentLayer]._height;
                            Height = CurrentMap._subMaps[SubMapIndex]._maps[CurrentLayer]._ceil;
                        }
                        BottomLeft.transform.position = CurrentMap._subMaps[SubMapIndex]._bottomLeft;
                        TopRight.transform.position = CurrentMap._subMaps[SubMapIndex]._topRight;
                    }
                }
                else
                {
                    if (CurrentLayer < CurrentMap._maps.Count)
                    {
                        Lowest = CurrentMap._maps[CurrentLayer]._height;
                        Height = CurrentMap._maps[CurrentLayer]._ceil;
                    }
                    BottomLeft.transform.position = CurrentMap._bottomLeft;
                    TopRight.transform.position = CurrentMap._topRight;
                }
            }
            else
            {
                CurrentMap = null;
            }
        }

        private void Start()
        {
            if (Application.isPlaying) gameObject.SetActive(false);
        }



        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            if (UnityEditor.SceneView.lastActiveSceneView.camera.orthographic) return;
            if (BottomLeft != null && TopRight != null)
            {
                if (CurrentMap != null && CurrentLayer != 0)
                {
                    if (BakeForSubMap)
                    {
                        BottomLeft.position = new Vector3(CurrentMap._subMaps[SubMapIndex]._bottomLeft.x, Lowest, CurrentMap._subMaps[SubMapIndex]._bottomLeft.z);
                        TopRight.position = new Vector3(CurrentMap._subMaps[SubMapIndex]._topRight.x, Lowest, CurrentMap._subMaps[SubMapIndex]._topRight.z);
                    }
                    else
                    {
                        BottomLeft.position = new Vector3(CurrentMap._bottomLeft.x, Lowest, CurrentMap._bottomLeft.z);
                        TopRight.position = new Vector3(CurrentMap._topRight.x, Lowest, CurrentMap._topRight.z);
                    }
                }
                else
                {
                    Vector3 _bPos = BottomLeft.position;
                    Vector3 _tPos = TopRight.position;
                    _bPos.y = Lowest;
                    _tPos.y = Lowest;
                    if (BakeForSubMap)
                    {
                        _bPos.x = Mathf.Max(CurrentMap._bottomLeft.x, _bPos.x);
                        _bPos.z = Mathf.Max(CurrentMap._bottomLeft.z, _bPos.z);
                        _tPos.x = Mathf.Min(CurrentMap._topRight.x, _tPos.x);
                        _tPos.z = Mathf.Min(CurrentMap._topRight.z, _tPos.z);
                    }
                    BottomLeft.position = _bPos;
                    TopRight.position = _tPos;
                }
                float _heightRange = Height - Lowest;

                float _width = TopRight.position.x - BottomLeft.position.x;
                float _height= TopRight.position.z - BottomLeft.position.z;
                float _size = Mathf.Min(_width,_height);
                Vector3 _bottomRight= BottomLeft.position + Vector3.right * _size;
                Vector3 _topRight = _bottomRight + Vector3.forward * _size;
                Vector3 _topLeft = BottomLeft.position + Vector3.forward * _size;


                Gizmos.color = Color.green;
                Gizmos.DrawLine(BottomLeft.position, BottomLeft.position + Vector3.up * _heightRange);
                Gizmos.DrawLine(TopRight.position, TopRight.position + Vector3.up * _heightRange);
                Gizmos.DrawSphere(TopRight.position + Vector3.up * _heightRange, 20F);
                Gizmos.DrawSphere(BottomLeft.position + Vector3.up * _heightRange, 20F);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_topRight, _topRight + Vector3.up * _heightRange);
                Gizmos.DrawLine(_bottomRight, _bottomRight + Vector3.up * _heightRange);
                Gizmos.DrawLine(_topLeft, _topLeft + Vector3.up * _heightRange);
                
                Gizmos.DrawLine(_topLeft, BottomLeft.position);
                Gizmos.DrawLine(_topLeft, _topRight);
                Gizmos.DrawLine(_bottomRight, BottomLeft.position);
                Gizmos.DrawLine(_bottomRight, _topRight);

                Gizmos.DrawLine(_topLeft + Vector3.up * _heightRange, BottomLeft.position + Vector3.up * _heightRange);
                Gizmos.DrawLine(_topLeft + Vector3.up * _heightRange, _topRight + Vector3.up * _heightRange);
                Gizmos.DrawLine(_bottomRight + Vector3.up * _heightRange, BottomLeft.position + Vector3.up * _heightRange);
                Gizmos.DrawLine(_bottomRight + Vector3.up * _heightRange, _topRight + Vector3.up * _heightRange);

                if (_size < 0F)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(_topLeft + Vector3.up * _heightRange, _bottomRight + Vector3.up * _heightRange);
                    Gizmos.DrawLine(_topRight + Vector3.up * _heightRange, BottomLeft.position + Vector3.up * _heightRange);
                    Gizmos.DrawLine(_topLeft , _bottomRight );
                    Gizmos.DrawLine(_topRight, BottomLeft.position);
                }

                if (CurrentMap != null)
                {
                    if (CurrentLayer == 0)
                    {
                        if (BakeForSubMap)
                        {
                            if (SubMapIndex < CurrentMap._subMaps.Count)
                            {
                                CurrentMap._subMaps[SubMapIndex]._bottomLeft = BottomLeft.position;
                                CurrentMap._subMaps[SubMapIndex]._topRight = _topRight;
                            }
                        }
                        else
                        {
                            CurrentMap._bottomLeft = BottomLeft.position;
                            CurrentMap._topRight = _topRight;
                        }
                    }
                }
                else
                {
                    Init();
                }
            }
#endif
        }

    }
}
