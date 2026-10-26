using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace SoftKitty.MasterNavigationMap
{
    public class NavigationPath : MonoBehaviour
    {
        public static NavigationPath instance;
        public NavMeshAgent Agent;
        public GameObject Destination;
        [HideInInspector]
        public bool TaskOnGoing = false;
        [HideInInspector]
        public List<Vector3> mPath=new List<Vector3>();
        private Vector3 StartPoint;
        private Vector3 EndPoint;
        private Coroutine TaskCo;
        public NavMeshPathStatus PathState;
        private static Transform DestinationTransform;

        public static void Stop()
        {
            if(instance != null)
            {
                instance.TaskOnGoing = false;
                instance.NavigationPathUpdate(new List<Vector3>(), false);
                instance.Destination.SetActive(false);
                DestinationTransform = null;
            }
        }
        public static void GetPath(Transform _target)
        {
            if (instance == null)
            {
                instance = Instantiate(Resources.Load<GameObject>("MapNavigationSystem/NavigationPath"), null).GetComponent<NavigationPath>();
            }
            if (MapManager.NavmeshAreaMask == -1)
            {
                instance.Agent.areaMask = NavMesh.AllAreas;
            }
            else
            {
                instance.Agent.areaMask = MapManager.NavmeshAreaMask;
            }
            DestinationTransform = _target;
            instance.CalculatePath(_target);
        }

        public static bool isSameDestination(Transform _target)
        {
            if (instance != null && instance.Destination.transform == _target) return true;
            if (DestinationTransform == _target) return true;
            return false;
        }


        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(StartPoint,StartPoint+Vector3.up*10F);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(EndPoint, EndPoint + Vector3.up * 10F);
        }

        public void CalculatePath(Transform _target)
        {
            TaskOnGoing = true;
            if (TaskCo != null) StopCoroutine(TaskCo);
            TaskCo=StartCoroutine(CalculatePathCo(_target));
        }
        public void NavigationPathUpdate(List<Vector3> _path, bool _updateAll)
        {
            if (_path.Count == 0)
            {
                mPath.Clear();
            }
            else
            {
                if (_updateAll == true || mPath.Count==0)
                {
                    mPath.Clear();
                    mPath.AddRange(_path);
                }
                else
                {
                    mPath[0]= _path[0];
                    mPath[mPath.Count-1] = _path[_path.Count - 1];
                }
            }
            MapManager.NavigationPathCallback(_path, _updateAll);
        }

        private void OnDestroy()
        {
            if(Destination) Destroy(Destination);
        }

        private void Update()
        {
            if (TaskOnGoing && DestinationTransform && Destination && Destination.activeSelf) {
                Destination.transform.position = DestinationTransform.position;
            }
        }

        IEnumerator CalculatePathCo(Transform _target)
        {
            StartPoint = Vector3.zero;
            EndPoint = Vector3.zero;
            Destination.transform.SetParent(null);
            Destination.transform.position = _target.position;
            Destination.SetActive(true);
            while (TaskOnGoing == true)
            {
                if (Vector3.Distance(MapManager.Player.position, _target.position) < 5f) 
                {
                    TaskOnGoing = false;
                    NavigationPathUpdate(new List<Vector3>(), false);
                    Destination.SetActive(false);
                    DestinationTransform = null;
                    yield break;
                }

                if (Vector3.Distance(StartPoint, MapManager.Player.position) > 5f || Vector3.Distance(EndPoint, _target.position) > 5f)
                {
                    StartPoint = MapManager.Player.position;
                    EndPoint = _target.position;
                    
                    PathState = NavMeshPathStatus.PathInvalid;
                    NavMeshPath _path = new NavMeshPath();
                    List<Vector3> _pathResult = new List<Vector3>();
                    while (PathState == NavMeshPathStatus.PathInvalid)
                    {
                        Vector3 _start = GetNavMeshPoint(MapManager.Player.position);
                        Vector3 _end = GetNavMeshPoint(_target.position);

                        int _startLayer = MapManager.GetLayerByPosition(StartPoint);
                        int _endLayer = MapManager.GetLayerByPosition(EndPoint);
                        if (_endLayer != _startLayer)
                        {
                            MapPoint _entrance = MapManager.GetLayerCloestEntrance(_startLayer, _endLayer);
                            if (_entrance != null)
                            {
                                _end = GetNavMeshPoint(_entrance.transform.position);
                            }
                        }

                        transform.position = _start;
                        Agent.Warp(_start);
                        Agent.ResetPath();
                        Agent.CalculatePath(_end, _path);
                        yield return 1;
                        int _retry = 0;
                        while (_path.status == NavMeshPathStatus.PathPartial && _retry<5) {
                            Vector3[] m_pathCorners = new Vector3[512];
                            var numCorners = _path.GetCornersNonAlloc(m_pathCorners);
                            if ( numCorners > 0)
                            {
                                for (int i = 0; i < numCorners; i++)
                                {
                                   if(!_pathResult.Contains(m_pathCorners[i])) _pathResult.Add(m_pathCorners[i]);
                                }
                                _start = m_pathCorners[numCorners - 1];
                                transform.position = _start;
                                Agent.Warp(_start);
                                Agent.CalculatePath(_end, _path);
                            }
                            _retry++;
                            PathState = _path.status;
                            yield return 1;
                        }
                        if(_path.status == NavMeshPathStatus.PathComplete) _pathResult.AddRange(_path.corners);
                        PathState = _path.status;
                    }
                    if (_pathResult.Count > 1) NavigationPathUpdate(_pathResult, true);
                    yield return 1;
                }
                else
                {
                    List<Vector3> _result = new List<Vector3>();
                    _result.Add(MapManager.Player.position);
                    int _startLayer = MapManager.GetLayerByPosition(MapManager.Player.position);
                    int _endLayer = MapManager.GetLayerByPosition(_target.position);
                    if (_startLayer == _endLayer)
                    {
                        _result.Add(_target.position);
                    }
                    else
                    {
                        MapPoint _entrance = MapManager.GetLayerCloestEntrance(_startLayer, _endLayer);
                        if (_entrance != null)
                        {
                            _result.Add(GetNavMeshPoint(_entrance.transform.position));
                        }
                    }
                    NavigationPathUpdate(_result, false);
                    yield return 1;
                }
            }
         }
       

        public static Vector3 GetNavMeshPoint(Vector3 _pos)
        {
            NavMeshHit _hit = new NavMeshHit();
            if(NavMesh.SamplePosition(_pos, out _hit,50f, NavMesh.AllAreas))
            {
                return _hit.position;
            }
            else
            {
                return _pos;
            }
            
        }
     
    }
}
