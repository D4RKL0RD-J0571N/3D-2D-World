using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SoftKitty.MasterNavigationMap
{
    [System.Serializable]
    public class FogData
    {
        public int width;
        public int height;
        public byte[] data;
    }
    public class FogOfWar : MonoBehaviour
    {
        public static FogOfWar instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject _newInstance = Instantiate(Resources.Load<GameObject>("MapNavigationSystem/FogOfWar"));
                    _instance = _newInstance.GetComponent<FogOfWar>();
                }
                return _instance;
            }
        }
        private static FogOfWar _instance;
        public List<RawImage> rawImages=new List<RawImage>();
        public Material revealMaterial;
        public Material mapMaskMaterial;
        private Material runtimeMaterial;
        private Vector2 lastPos=new Vector2(99999F,99999F);
        private string lastSubMap = "";
        private bool inited = false;
        private Vector4 maskArea = new Vector4(0F,0F,1F,1F);
        private bool[,] maskBuffer;
        RenderTexture fogA;
        RenderTexture fogB;


        private void Awake()
        {
            _instance = this;
            
        }

        public void FindRawImages()
        {
            rawImages.Clear();
            MapInteractive[] maps = Object.FindObjectsByType<MapInteractive>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var map in maps) {
                if ((map.Type== MapInteractive.MapTypes.WorldMap && MapManager.CurrentSceneMapSetting._fogOfWarWorldMap) ||
                   (map.Type == MapInteractive.MapTypes.MiniMap && MapManager.CurrentSceneMapSetting._fogOfWarMiniMap))
                {
                    if(map.MapImage != null) rawImages.Add(map.MapImage);
                }
            }
            lastSubMap = "";
            UpdateRawImages();
        }

        public void UpdateRawImages()
        {
            runtimeMaterial = Instantiate(mapMaskMaterial);
            runtimeMaterial.SetTexture("_MaskTex", fogA);
            runtimeMaterial.SetColor("_MaskColor", MapManager.CurrentSceneMapSetting._fogOfWarColor);
            runtimeMaterial.SetVector("_MaskArea", maskArea);
            foreach (var obj in rawImages) {
                obj.material = runtimeMaterial;
            }
        }

        public void InitFog()
        {
            fogA = new RenderTexture(MapManager.CurrentSceneMapSetting._fogOfWarWidth, MapManager.CurrentSceneMapSetting._fogOfWarHeight, 0, RenderTextureFormat.RG32);
            fogB = new RenderTexture(MapManager.CurrentSceneMapSetting._fogOfWarWidth, MapManager.CurrentSceneMapSetting._fogOfWarHeight, 0, RenderTextureFormat.RG32);
            fogA.filterMode = FilterMode.Bilinear;
            fogA.wrapMode = TextureWrapMode.Clamp;
            fogB.filterMode = FilterMode.Bilinear;
            fogB.wrapMode = TextureWrapMode.Clamp;
            fogA.Create();
            fogB.Create();
            revealMaterial.SetTexture("_MainTex", fogA);
            FindRawImages();
            maskBuffer = new bool[Mathf.CeilToInt(1F/MapManager.CurrentSceneMapSetting._fogOfWarRadius) +1,Mathf.CeilToInt(1F/ MapManager.CurrentSceneMapSetting._fogOfWarRadius) +1];
            inited = true;
        }

        public string SaveFog()
        {
            if (!inited) return "";
            FogData _data = new FogData();
            _data.width = fogA.width;
            _data.height = fogA.width;
            _data.data = new byte[_data.width* _data.height];
            Texture2D _bufferTex = new Texture2D(_data.width, _data.height, TextureFormat.RG32, false);
            RenderTexture.active = fogA;
            _bufferTex.ReadPixels(new Rect(0, 0, _data.width, _data.height), 0, 0, false);
            _bufferTex.Apply(false);
            for (int x = 0; x < _data.width; x++)
            {
                for (int y = 0; y < _data.height; y++)
                {
                    _data.data[_data.width * y + x] = (byte)Mathf.FloorToInt(_bufferTex.GetPixel(x,y).r*255);
                }
            }
            RenderTexture.active = null;
            return JsonUtility.ToJson(_data);
        }

        public void LoadFog(string _json)
        {
            if (!inited) return;
            FogData _data = JsonUtility.FromJson<FogData>(_json);
            if (_data!=null) {
                Texture2D _bufferTex = new Texture2D(_data.width, _data.height, TextureFormat.RG32, false);
                for (int x = 0; x < _data.width; x++)
                {
                    for (int y = 0; y < _data.height; y++)
                    {
                        float _c = _data.data[_data.width * y + x] / 255F;
                        _bufferTex.SetPixel(x, y,new Color(_c, _c, _c, _c));
                        maskBuffer[Mathf.FloorToInt(x * 1F / _data.width), Mathf.FloorToInt(y * 1F / _data.width)] = _c > 0.1F;
                    }
                }
                _bufferTex.Apply(false);
                fogA = new RenderTexture(_data.width, _data.height, 0, RenderTextureFormat.RG32);
                fogB = new RenderTexture(_data.width, _data.height, 0, RenderTextureFormat.RG32);
                Graphics.Blit(_bufferTex, fogA);
                Graphics.Blit(_bufferTex, fogB);
                UpdateRawImages();
                revealMaterial.SetTexture("_MainTex", fogA);
            }
        }

        public void RemoveFog(Vector2 uv, float radius)
        {
            if (!inited) return;
            if (Vector2.Distance(lastPos, uv) < 0.01F) return;
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            maskBuffer[Mathf.FloorToInt(uv.x * (maskBuffer.GetLength(0) - 1)), Mathf.FloorToInt(uv.y * (maskBuffer.GetLength(1) - 1))] = true;
            lastPos = uv;
            revealMaterial.SetFloat("_Mode",0);
            UpdateFog(uv, radius);
        }

        public void AddFog(Vector2 uv, float radius)
        {
            if (!inited) return;
            if (Vector2.Distance(lastPos, uv) < 0.01F) return;
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            maskBuffer[Mathf.FloorToInt(uv.x * (maskBuffer.GetLength(0) - 1)), Mathf.FloorToInt(uv.y * (maskBuffer.GetLength(1) - 1))] = false;
            lastPos = uv;
            revealMaterial.SetFloat("_Mode", 1);
            UpdateFog(uv, radius);
        }

        public void ClearFog()
        {
            if (!inited) return;
            for (int x = 0; x < maskBuffer.GetLength(0); x++)
            {
                for (int y = 0; y < maskBuffer.GetLength(1); y++)
                {
                    maskBuffer[x, y] = true;
                }
            }
            revealMaterial.SetFloat("_Mode", 3);
            UpdateFog(Vector2.zero, 0.1F);
        }

        public void FillFog()
        {
            if (!inited) return;
            for (int x = 0; x < maskBuffer.GetLength(0); x++)
            {
                for (int y = 0; y < maskBuffer.GetLength(1); y++)
                {
                    maskBuffer[x, y] = false;
                }
            }
            revealMaterial.SetFloat("_Mode", 2);
            UpdateFog(Vector2.zero, 0.1F);
        }

        private void UpdateFog(Vector2 uv, float radius)
        {
            if (!inited) return;
            radius = Mathf.Max(radius, 0.01F);
            revealMaterial.SetVector("_RevealUV", uv);
            revealMaterial.SetFloat("_Radius", radius);
            Graphics.Blit(fogB, fogA, revealMaterial);
            Graphics.Blit(fogA, fogB);
        }

        public bool isInFog(Vector3 _worldPos)
        {
            if (fogA == null || runtimeMaterial==null || !inited) return false;
            Vector2 _normalizedPos = MapInteractive.WorldToNormalizedMapPosition(_worldPos);
            _normalizedPos.x = Mathf.Clamp01(_normalizedPos.x);
            _normalizedPos.y = Mathf.Clamp01(_normalizedPos.y);
            return !maskBuffer[Mathf.FloorToInt(_normalizedPos.x * (maskBuffer.GetLength(0)-1)), Mathf.FloorToInt(_normalizedPos.y * (maskBuffer.GetLength(1) - 1))];
        }

        void SubmapUpdate()
        {
            if (MapManager.CurrentSubMapSetting != null)
            {
                if (lastSubMap != MapManager.CurrentSubMapSetting.uid)
                {
                    Vector3 _mapSize = MapManager.CurrentSceneMapSetting._topRight - MapManager.CurrentSceneMapSetting._bottomLeft;
                    maskArea = new Vector4(
                            (MapManager.CurrentSubMapSetting._bottomLeft.x - MapManager.CurrentSceneMapSetting._bottomLeft.x) / _mapSize.x,
                            (MapManager.CurrentSubMapSetting._bottomLeft.z - MapManager.CurrentSceneMapSetting._bottomLeft.z) / _mapSize.z,
                            (MapManager.CurrentSubMapSetting._topRight.x - MapManager.CurrentSubMapSetting._bottomLeft.x) / _mapSize.x,
                           (MapManager.CurrentSubMapSetting._topRight.z - MapManager.CurrentSubMapSetting._bottomLeft.z) / _mapSize.z
                            );

                    UpdateRawImages();
                }
                lastSubMap = MapManager.CurrentSubMapSetting.uid;
            }
            else
            {
                if (lastSubMap != "")
                {
                    maskArea = new Vector4(0F, 0F, 1F, 1F);
                    UpdateRawImages();
                }
                lastSubMap = "";
            }
        }

         
        void Update()
        {
            if (!inited) return;
            RemoveFog(MapInteractive.WorldToNormalizedMapPosition(MapManager.Player.position),MapManager.CurrentSceneMapSetting._fogOfWarRadius);
            SubmapUpdate();
        }
    }
}
