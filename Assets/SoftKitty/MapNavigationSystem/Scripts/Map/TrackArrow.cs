using UnityEngine;
using UnityEngine.UI;

namespace SoftKitty.MasterNavigationMap
{
    public class TrackArrow : MonoBehaviour
    {
        public RectTransform DownLeft;
        public RectTransform TopRight;
        public RectTransform Root;
        public Image Arrow;
        public Transform TestTarget;
        public Transform Target
        {
            get
            {
                return target;
            }
        }
        private Transform target;
        private float angle;
        private Vector3 targetPos;
        private Vector2 mapSize;


        public void StartTrack(Transform _target)
        {
            target = _target;
        }

        public void EndTrack()
        {
            target = null;
        }
 
         
        void Update()
        {
            if (TestTarget != null) target = TestTarget;
            if (target == null || MapManager.Player==null)
            {
                if (Arrow.gameObject.activeSelf) Arrow.gameObject.SetActive(false);
                return;
            }
            mapSize.x =Mathf.Abs(TopRight.position.x - DownLeft.position.x)*0.5F;
            mapSize.y = Mathf.Abs(TopRight.position.y - DownLeft.position.y) * 0.5F;
            targetPos = target.position;
            targetPos.y = MapManager.Player.position.y;
            angle = Quaternion.LookRotation((targetPos - MapManager.Player.position).normalized, Vector3.up).eulerAngles.y;
            Root.localEulerAngles = new Vector3(0F,0F, -angle);
            Arrow.rectTransform.localPosition = new Vector3(0F,Mathf.Min(Vector3.Distance(targetPos, MapManager.Player.position), mapSize.x-15F, mapSize.y  - 15F),0F);
            Arrow.color = new Color(Arrow.color.r, Arrow.color.g, Arrow.color.b,Mathf.Clamp01((Vector3.Distance(targetPos, MapManager.Player.position)-10F)*0.01F));
            if (!Arrow.gameObject.activeSelf) Arrow.gameObject.SetActive(true);
        }
    }
}
