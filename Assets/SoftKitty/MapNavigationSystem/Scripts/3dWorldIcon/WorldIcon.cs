using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterNavigationMap
{
    public class WorldIcon : MapIcon
    {
        private bool outView = false;
        public RectTransform parentRoot;
        private Camera mainCamera
        {
            get
            {
                return MapManager.PlayerCamera;
            }
        }
        public override void Update()
        {
            if (point == null) return;
            float _distance = Vector3.Distance(point.position, MapManager.Player.position);
            if (_distance > data.IconDisappearDistanceIn3dWorld || !point.gameObject.activeInHierarchy)
            {
                if (Group.alpha > 0F)
                {
                    Group.alpha = Mathf.MoveTowards(Group.alpha, 0F, Time.deltaTime * 2F);
                }
                else
                {
                    return;
                }
            }
            else
            {
                float _maxDis = Mathf.Min(data.IconDisappearDistanceIn3dWorld, 500F);
                float _t = (_maxDis - _distance) / _maxDis * 0.5F + 0.5F;
                Group.alpha = Mathf.MoveTowards(Group.alpha, Mathf.Clamp01(outView ? 0F : _t * (isVisible() ? 1f : 0f) * 1.3F), Time.deltaTime * 2F);
                transform.localScale = Vector3.one * _t;
                SetPosition();
            }
            
        }

        void SetPosition()
        {
            Vector3 _pos = GetScreenPos(point.position, data.IconOffsetIn3dWorld);
            if (_pos == Vector3.zero)
            {
                outView = true;
                return;
            }
            outView = false;
            transform.localPosition = _pos;
        }

        Vector3 GetScreenPos(Vector3 _worldPos, float _yOffset)
        {
            Vector3 posDelta = _worldPos;
            posDelta.y += _yOffset;
            Vector3 pos = Vector3.zero;
            pos = mainCamera.WorldToViewportPoint(posDelta);
            if (pos.z < 0F)
            {
                return Vector3.zero;
            }
            pos.z = 0F;

            pos.x *= parentRoot.rect.width;
            pos.y *= parentRoot.rect.height;

            return new Vector3(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), 0F);
        }
    }
}
