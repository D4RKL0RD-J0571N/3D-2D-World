using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterNavigationMap
{
    public class NavigationBarIcon : MapIcon
    {
        private bool outView = false;
        public override void Update()
        {
            if (point == null) return;
            float _distance = Vector3.Distance(point.position, MapManager.Player.position);
            if (_distance > data.DisapperDistance || !point.gameObject.activeInHierarchy)
            {
                if (Group.alpha > 0F)
                {
                    Group.alpha = Mathf.MoveTowards(Group.alpha,0F,Time.deltaTime*2F);
                }
                else
                {
                    return;
                }
            }
            else
            {
                float _maxDis =data.DisapperDistance;
                float _t = Mathf.Max(0F,_maxDis - _distance) / _maxDis * 0.5F + 0.5F;
                if (data.AlwaysKeepSizeInNavigagtionBar) _t = 1F;
                Group.alpha = Mathf.MoveTowards(Group.alpha,Mathf.Clamp01(outView?0F:_t * (isVisible() ? 1f : 0f) * 1.3F), Time.deltaTime * 2F);
                transform.localScale = Vector3.one * _t;
            }
            Vector3 _direction = (point.position - MapManager.Player.position);
            _direction.y = 0F;
            _direction.Normalize();
            Vector3 _right = MapManager.PlayerCamera.transform.right;
            Vector3 _forward = MapManager.PlayerCamera.transform.forward;
            _right.y = 0F;
            _forward.y = 0F;
            float _pos = Vector3.Dot(_direction, _right.normalized);
            if (Vector3.Dot(_direction, _forward.normalized) < 0f)
            {
                if (_pos == 0f)
                {
                    _pos = 1f;
                }
                else
                {
                    _pos = _pos<0F?-1F:1F;
                }
            }
            outView = Mathf.Abs(_pos) == 1F;
            float _targetPos = _pos * NavigationBar.Instance.BarWidth * 0.5F;
            if (Mathf.Abs(RectTrans.anchoredPosition.x - _targetPos) > NavigationBar.Instance.BarWidth * 0.9F) RectTrans.anchoredPosition = new Vector2(_targetPos,0F);
            RectTrans.anchoredPosition = new Vector2(Mathf.Lerp(RectTrans.anchoredPosition.x, _targetPos,Time.deltaTime*10F) , 0F);

           
        }

       
    }
}