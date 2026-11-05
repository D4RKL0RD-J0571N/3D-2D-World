namespace Gameplay.Combat
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Core;
    using Domain.Entities;
    using Gameplay.Entities;

    /// <summary>
    /// Spawns and manages enemy groups within an encounter perimeter.
    /// Respawns fallen enemies after a configurable timer, maintaining active combat pressure.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Combat/Enemy Spawner")]
    public class EnemySpawner : MonoBehaviour
    {
        #region Fields

        [Header("Spawn Configuration")]
        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private int _maxActiveEnemies = 2;
        [SerializeField] private float _spawnRadius = 4f;
        [SerializeField] private float _respawnDelay = 15f;

        [Header("Patrol Association")]
        [SerializeField] private PatrolPath _patrolPath;

        private readonly List<GameObject> _activeEnemies = new List<GameObject>();

        #endregion

        #region Properties

        public int ActiveEnemyCount => _activeEnemies.Count;
        public int MaxActiveEnemies { get => _maxActiveEnemies; set => _maxActiveEnemies = value; }
        public GameObject EnemyPrefab { get => _enemyPrefab; set => _enemyPrefab = value; }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            InitialSpawn();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDiedEvent>(OnEntityDied);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDiedEvent>(OnEntityDied);
            StopAllCoroutines();
        }

        #endregion

        #region Spawning Logic

        private void InitialSpawn()
        {
            if (_enemyPrefab == null) return;

            while (_activeEnemies.Count < _maxActiveEnemies)
            {
                SpawnEnemyInstance();
            }
        }

        public GameObject SpawnEnemyInstance()
        {
            if (_enemyPrefab == null) return null;

            Vector2 circle = Random.insideUnitCircle * _spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(circle.x, 0f, circle.y);

            GameObject enemy = Instantiate(_enemyPrefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            enemy.transform.parent = transform;

            // Wire patrol path if present
            if (_patrolPath != null)
            {
                var rangedAI = enemy.GetComponent<RangedCombatAI>();
                if (rangedAI != null) rangedAI.AssignedPatrolPath = _patrolPath;
            }

            _activeEnemies.Add(enemy);
            return enemy;
        }

        private void OnEntityDied(EntityDiedEvent evt)
        {
            if (evt == null || evt.Victim == null) return;

            if (_activeEnemies.Contains(evt.Victim.gameObject))
            {
                _activeEnemies.Remove(evt.Victim.gameObject);
                StartCoroutine(RespawnRoutine());
            }
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnDelay);

            if (_activeEnemies.Count < _maxActiveEnemies)
            {
                SpawnEnemyInstance();
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        }

        #endregion
    }
}
