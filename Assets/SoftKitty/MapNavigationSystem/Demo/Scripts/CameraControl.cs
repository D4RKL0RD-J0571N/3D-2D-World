using UnityEngine;
using UnityEngine.EventSystems;

namespace SoftKitty.MasterNavigationMap
{
    public class CameraControl : MonoBehaviour
    {
        public LayerMask layerMask;
        public Transform Player;
        public Transform RotY;
        public Transform RotX;
        public Camera Cam;
        float angleY = 0F;
        float angleX = 20F;
        float offsetY = 1.5F;
        float zoom = 1F;

        float _angleY = 0F;
        float _angleX = 20F;
        float _offsetY = 1.5F;
        float _zoom = 1F;
        float _zoomBlock = 1F;
        RaycastHit _hit;

        void Start()
        {

        }


        private float FormatAngle(float _angle)
        {
            _angle = _angle % 360F;
            if (_angle < -180F) _angle += 360F;
            if (_angle > 180F) _angle -= 360F;
            return _angle;
        }

        void Update()
        {
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                if (Input.GetMouseButton(1))
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    angleY = angleY + Input.GetAxis("Mouse X") * Time.deltaTime * 150F;
                    angleX = Mathf.Clamp(angleX - Input.GetAxis("Mouse Y") * Time.deltaTime * 50F, 0F, 89F);
                }
                else
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                zoom = Mathf.Clamp(zoom - Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * 100F, 0.3F, 1F);
            }

            _zoom = Mathf.Lerp(_zoom, zoom,Time.deltaTime*2F);
            _offsetY= Mathf.Lerp(_offsetY, offsetY, Time.deltaTime * 5F);
            _angleY = Mathf.Lerp(_angleY, angleY, Time.deltaTime * 10F);
            _angleX = Mathf.Lerp(_angleX, angleX, Time.deltaTime * 10F);
            if (Physics.Linecast(RotY.position- Cam.transform.forward, RotY.position - Cam.transform.forward* Mathf.Lerp(1F, 30F, zoom), out _hit, layerMask, QueryTriggerInteraction.Ignore))
            {
                _zoomBlock = (_hit.distance-0.5F) / 29F;
            }
            else
            {
                _zoomBlock = _zoom;
            }
            if (_zoom > _zoomBlock) _zoom = _zoomBlock;
        }

        private void LateUpdate()
        {
            transform.position = Player.transform.position;
            RotY.localPosition = new Vector3(0F,_offsetY,0F);
            RotY.localEulerAngles = new Vector3(0F, _angleY,0F);
            RotX.localEulerAngles = new Vector3(_angleX, 0F, 0F);
            Cam.transform.localPosition = new Vector3(0F,0F,-Mathf.Lerp(1F,30F, _zoom));
        }
       

    }
}
