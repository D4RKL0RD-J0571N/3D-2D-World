namespace Gameplay.Combat
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Defines a sequence of waypoints for AI patrol loops and sentry paths.
    /// Supports looping and ping-pong patrol modes.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Combat/Patrol Path")]
    public class PatrolPath : MonoBehaviour
    {
        #region Fields

        [Header("Waypoints")]
        [SerializeField] private List<Transform> _waypoints = new List<Transform>();
        [SerializeField] private bool _loop = true;
        [SerializeField] private float _waypointTolerance = 1.2f;

        private int _currentIndex;
        private int _direction = 1;

        #endregion

        #region Properties

        public IReadOnlyList<Transform> Waypoints => _waypoints;
        public float WaypointTolerance => _waypointTolerance;

        public Transform CurrentWaypoint
        {
            get
            {
                if (_waypoints == null || _waypoints.Count == 0) return null;
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _waypoints.Count - 1);
                return _waypoints[_currentIndex];
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Advances to the next waypoint in the patrol path.
        /// </summary>
        public Transform AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Count == 0) return null;

            if (_loop)
            {
                _currentIndex = (_currentIndex + 1) % _waypoints.Count;
            }
            else
            {
                _currentIndex += _direction;
                if (_currentIndex >= _waypoints.Count)
                {
                    _currentIndex = _waypoints.Count - 2;
                    _direction = -1;
                }
                else if (_currentIndex < 0)
                {
                    _currentIndex = 1;
                    _direction = 1;
                }
            }

            return CurrentWaypoint;
        }

        /// <summary>
        /// Adds a transform waypoint dynamically.
        /// </summary>
        public void AddWaypoint(Transform waypoint)
        {
            if (waypoint != null && !_waypoints.Contains(waypoint))
            {
                _waypoints.Add(waypoint);
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (_waypoints == null || _waypoints.Count < 2) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < _waypoints.Count; i++)
            {
                if (_waypoints[i] == null) continue;
                Gizmos.DrawWireSphere(_waypoints[i].position, 0.4f);

                int next = (i + 1) % _waypoints.Count;
                if (_waypoints[next] != null && (_loop || i < _waypoints.Count - 1))
                {
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[next].position);
                }
            }
        }

        #endregion
    }
}
