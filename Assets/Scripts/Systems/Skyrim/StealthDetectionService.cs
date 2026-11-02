namespace Systems.Skyrim
{
    using System.Collections.Generic;
    using Core;
    using Core.Scheduling;
    using Domain.Skyrim;
    using Gameplay.Player;
    using Gameplay.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Service evaluating Skyrim-style line-of-sight and awareness metrics
    /// between nearby hostile actors and the player. Drives the Sneak Eye HUD.
    /// Implements <see cref="IScheduledTickable"/> (<see cref="TickGroup.AI"/>, 10 Hz)
    /// and caches active observers to avoid per-frame <c>FindGameObjectsWithTag</c> GC allocations.
    /// </summary>
    public class StealthDetectionService : IGameService, IScheduledTickable
    {
        #region Constants

        private const float MaxAwarenessDistance = 28f;
        private const float DetectionBuildRate = 0.85f;
        private const float DetectionDecayRate = 0.45f;
        private const float FallbackDiscoveryInterval = 2.0f;

        #endregion

        #region Fields

        private readonly List<Transform> _trackedObservers = new();
        private readonly HashSet<Transform> _trackedSet = new();

        private Transform _playerTransform;
        private Player_Movement _playerMovement;
        private float _currentDetectionRatio;
        private StealthState _currentState = StealthState.Hidden;
        private float _lastDispatchedRatio = -1f;
        private float _fallbackDiscoveryTimer;

        #endregion

        #region Properties

        public StealthState CurrentState => _currentState;
        public float DetectionRatio => _currentDetectionRatio;

        public TickGroup Group => TickGroup.AI;
        public float TickIntervalSeconds => 0.1f;

        #endregion

        #region IGameService & IScheduledTickable

        /// <inheritdoc />
        public void Initialize()
        {
            _trackedObservers.Clear();
            _trackedSet.Clear();
            ResolvePlayer();
            DiscoverTaggedEnemiesOnce();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _trackedObservers.Clear();
            _trackedSet.Clear();
            _playerTransform = null;
            _playerMovement = null;
        }

        /// <summary>Registers an active potential observer (e.g. from <c>BaseActor.OnEnable</c>).</summary>
        public void RegisterObserver(Transform observer)
        {
            if (observer == null || observer.CompareTag("Player")) return;
            if (_trackedSet.Add(observer))
            {
                _trackedObservers.Add(observer);
            }
        }

        /// <summary>Unregisters an observer when disabled or destroyed.</summary>
        public void UnregisterObserver(Transform observer)
        {
            if (observer == null) return;
            if (_trackedSet.Remove(observer))
            {
                _trackedObservers.Remove(observer);
            }
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null) return;
            }

            if (_trackedObservers.Count == 0)
            {
                _fallbackDiscoveryTimer -= deltaTime;
                if (_fallbackDiscoveryTimer <= 0f)
                {
                    _fallbackDiscoveryTimer = FallbackDiscoveryInterval;
                    DiscoverTaggedEnemiesOnce();
                }
            }

            bool isCrouching = _playerMovement != null && _playerMovement.IsCrouching;
            bool enemyHasLineOfSight = CheckEnemyLineOfSight(out float closestDistance);

            if (enemyHasLineOfSight)
            {
                float proximityBonus = Mathf.Clamp01(1f - (closestDistance / MaxAwarenessDistance));
                float buildSpeed = DetectionBuildRate * (1f + proximityBonus);
                if (!isCrouching) buildSpeed *= 2.0f;

                _currentDetectionRatio = Mathf.Clamp01(_currentDetectionRatio + buildSpeed * deltaTime);
            }
            else
            {
                _currentDetectionRatio = Mathf.Clamp01(_currentDetectionRatio - DetectionDecayRate * deltaTime);
            }

            StealthState newState;
            if (_currentDetectionRatio >= 0.95f)
            {
                newState = StealthState.Detected;
            }
            else if (_currentDetectionRatio > 0.05f)
            {
                newState = StealthState.Searching;
            }
            else
            {
                newState = StealthState.Hidden;
            }

            if (newState != _currentState || Mathf.Abs(_currentDetectionRatio - _lastDispatchedRatio) > 0.02f)
            {
                _currentState = newState;
                _lastDispatchedRatio = _currentDetectionRatio;
                GameEventManager.Publish(new StealthStateChangedEvent(_currentState, _currentDetectionRatio, isCrouching));
            }
        }

        #endregion

        #region Line of Sight & Perception

        private void ResolvePlayer()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                _playerMovement = playerObj.GetComponent<Player_Movement>();
            }
        }

        private void DiscoverTaggedEnemiesOnce()
        {
            GameObject[] tagged = GameObject.FindGameObjectsWithTag("Enemy");
            for (int i = 0; i < tagged.Length; i++)
            {
                if (tagged[i] != null)
                {
                    RegisterObserver(tagged[i].transform);
                }
            }
        }

        private bool CheckEnemyLineOfSight(out float closestDistance)
        {
            closestDistance = float.MaxValue;
            if (_playerTransform == null) return false;

            Vector3 playerEyePos = _playerTransform.position + Vector3.up * 1.5f;
            FactionService factionService = ServiceLocator.TryGet<FactionService>(out var fs) ? fs : null;
            bool anySpotted = false;

            for (int i = _trackedObservers.Count - 1; i >= 0; i--)
            {
                Transform enemyTransform = _trackedObservers[i];
                if (enemyTransform == null)
                {
                    _trackedObservers.RemoveAt(i);
                    continue;
                }

                GameObject enemyObj = enemyTransform.gameObject;
                if (!enemyObj.activeInHierarchy) continue;

                // Respect FactionComponent hostility if present; otherwise require "Enemy" tag
                if (enemyObj.TryGetComponent<FactionComponent>(out var factionComp) && factionService != null)
                {
                    if (!factionService.IsHostile(factionComp.FactionId, FactionService.PlayerFactionId))
                    {
                        continue;
                    }
                }
                else if (!enemyObj.CompareTag("Enemy"))
                {
                    continue;
                }

                Vector3 enemyEyePos = enemyTransform.position + Vector3.up * 1.5f;
                Vector3 toPlayer = playerEyePos - enemyEyePos;
                float distance = toPlayer.magnitude;

                if (distance > MaxAwarenessDistance) continue;

                Vector3 enemyForward = enemyTransform.forward;
                float angle = Vector3.Angle(enemyForward, toPlayer.normalized);
                if (angle > 65f) continue;

                if (!Physics.Linecast(enemyEyePos, playerEyePos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) ||
                    hit.transform == _playerTransform)
                {
                    anySpotted = true;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                    }
                }
            }

            return anySpotted;
        }

        #endregion
    }
}
