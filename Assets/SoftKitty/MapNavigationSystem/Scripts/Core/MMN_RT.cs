using UnityEngine;

namespace SoftKitty.MasterNavigationMap
{
    public class MMN_RT : MonoBehaviour
    {
        public Camera RtCamera;
        public CustomRenderTexture CustomRt;
        public RenderTexture SourceRt;
        public static MMN_RT instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Instantiate(Resources.Load<GameObject>("MapNavigationSystem/MMN_RT")).GetComponent<MMN_RT>();

                }
                return _instance;
            }
        }
        private static MMN_RT _instance;
        private float _cameraDepth;
        private float _cameraHeight;
        private MMN_Settings.TextureSize _rtSize;
        private LayerMask _rtLayerMask;
        private bool _stylized;
        private bool _depthOnly;


        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            if (isSettingChanged())
            {
                ValueApply();
            }
        }

        public Texture GetTexture(float _size)
        {
            RtCamera.orthographicSize = _size;
            ValueApply();
            Render();
            //Debug.Log("RT render!");
            return _stylized?(Texture)CustomRt:(Texture)SourceRt;
        }

        public void MoveToPosition(Vector3 _pos)
        {
            transform.position = _pos;
        }

        public bool MoveToPosition2D(Vector3 _pos)
        {
            Vector3 _newPos= new Vector3(_pos.x, transform.position.y, _pos.y);
            if (transform.position != _newPos)
            {
                transform.position = _newPos;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Render()
        {
            RtCamera.Render();
            CustomRt.Update();
        }

        private void ValueApply()
        {
            _cameraDepth = MapManager.CurrentSetting.RtCameraDepth;
            _cameraHeight = MapManager.CurrentSetting.RtCameraHeight;
            _rtSize = MapManager.CurrentSetting.RtSize;
            _rtLayerMask = MapManager.CurrentSetting.RtLayerMask;
            _stylized= MapManager.CurrentSetting.RtStylizedRendering;
            _depthOnly = MapManager.CurrentSetting.RtDepthOnly;

            RtCamera.transform.localPosition = new Vector3(0F, _cameraHeight, 0F);
            RtCamera.farClipPlane = _cameraDepth;
            RtCamera.cullingMask = _rtLayerMask;
            int _size= 1024 * Mathf.FloorToInt(Mathf.Pow(2, (int)_rtSize));

            if (SourceRt.IsCreated()) SourceRt.Release();
            if (!SourceRt.IsCreated())
            {
                SourceRt.format = (!_stylized || !_depthOnly) ?RenderTextureFormat.ARGB64 : RenderTextureFormat.Depth;
                SourceRt.depthStencilFormat = (!_stylized || !_depthOnly) ? UnityEngine.Experimental.Rendering.GraphicsFormat.None : UnityEngine.Experimental.Rendering.GraphicsFormat.D32_SFloat;
                SourceRt.width = _size;
                SourceRt.height = _size;
                SourceRt.Create();
            }

            if (CustomRt.IsCreated()) CustomRt.Release();
            if (!CustomRt.IsCreated())
            {
                CustomRt.format = RenderTextureFormat.ARGB64;
                CustomRt.depthStencilFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                CustomRt.width = _size;
                CustomRt.height = _size;
                CustomRt.initializationSource = _stylized ? CustomRenderTextureInitializationSource.Material : CustomRenderTextureInitializationSource.TextureAndColor;
                CustomRt.initializationMaterial = _stylized ? MapManager.CurrentSetting.RtStylizedeMaterial : null;
                CustomRt.material = _stylized ? MapManager.CurrentSetting.RtStylizedeMaterial : null;
                CustomRt.Create();
            }
 
        }

        private bool isSettingChanged() {
            return _cameraDepth != MapManager.CurrentSetting.RtCameraDepth
                || _cameraHeight != MapManager.CurrentSetting.RtCameraHeight
                || _rtSize != MapManager.CurrentSetting.RtSize
                || _rtLayerMask != MapManager.CurrentSetting.RtLayerMask
                || _stylized != MapManager.CurrentSetting.RtStylizedRendering
                || _depthOnly != MapManager.CurrentSetting.RtDepthOnly
                ;
        }


    }
}
