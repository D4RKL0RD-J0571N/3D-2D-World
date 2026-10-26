using UnityEngine;

namespace SoftKitty.MasterNavigationMap
{
    public class MapLayerEntrance : MonoBehaviour
    {
        public float TeleportDistance = 3F;
        bool teleported = false;
        MapPoint mPoint;
        void Start()
        {
            if (GetComponent<MapPoint>())
            {
                mPoint = GetComponent<MapPoint>();
            } 
            else if (GetComponentInParent<MapPoint>()) {
                mPoint = GetComponentInParent<MapPoint>();
            }
            else if (GetComponentInChildren<MapPoint>(true))
            {
                mPoint = GetComponentInChildren<MapPoint>(true);
            }
        }

         
        void Update()
        {
            if (MapManager.Player != null)
            {
                if (Vector3.Distance(MapManager.Player.position, transform.position) < TeleportDistance)
                {
                    if (teleported) return;
                    MapPoint _target = MapManager.GetLayerCloestEntrance(mPoint.EntranceOfLayer, MapManager.GetLayerByPosition(transform.position));
                    if (_target != null)
                    {
                        Vector3 _pos = _target.transform.position + _target.transform.forward * 15F;
                        RaycastHit hit;
                        if (Physics.Raycast(_pos + Vector3.up * 30F, Vector3.down, out hit, 20F, SGD_Settings.Instance.GroundLayer, QueryTriggerInteraction.Ignore))
                        {
                            _pos.y = hit.point.y + 0.1F;
                        }
                        if (MapManager.Player.GetComponent<CharacterController>()) MapManager.Player.GetComponent<CharacterController>().enabled = false;
                        MapManager.Player.position = _pos;
                        if (MapManager.Player.GetComponent<CharacterController>()) MapManager.Player.GetComponent<CharacterController>().enabled = true;
                        teleported = true;
                    }
                }
                else
                {
                    teleported = false;
                }
            }
        }
    }
}
