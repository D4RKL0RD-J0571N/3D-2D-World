namespace Systems.Skyrim
{
    using Core;
    using Domain.Skyrim;
    using Gameplay.Player;
    using UnityEngine;

    /// <summary>
    /// Service evaluating Skyrim-style line-of-sight and awareness metrics
    /// between nearby hostile actors and the player. Drives the Sneak Eye HUD.
    /// </summary>
    public class StealthDetectionService : IGameService, ITickable
    {
        #region Fields

        private Transform _playerTransform;
        private Player_Movement _playerMovement;
        private float _currentDetectionRatio = 0f;
        private StealthState _currentState = StealthState.Hidden;
        private float _lastDispatchedRatio = -1f;

        private const float MaxAwarenessDistance = 28f;
        private const float DetectionBuildRate = 0.85f;
        private const float DetectionDecayRate = 0.45f;

        #endregion

        #region Properties

        public StealthState CurrentState => _currentState;
        public float DetectionRatio => _currentDetectionRatio;

        #endregion

        #region IGameService & ITickable

        /// <inheritdoc />
        public void Initialize()
        {
            ResolvePlayer();
        }

        /// <inheritdoc />
        public void Shutdown()
        {
            _playerTransform = null;
            _playerMovement = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null) return;
            }

            bool isCrouching = _playerMovement != null && _playerMovement.IsCrouching;
            bool enemyHasLineOfSight = CheckEnemyLineOfSight(out float closestDistance);

            // Calculate awareness build-up or decay
            if (enemyHasLineOfSight)
            {
                float proximityBonus = Mathf.Clamp01(1f - (closestDistance / MaxAwarenessDistance));
                float buildSpeed = DetectionBuildRate * (1f + proximityBonus);
                if (!isCrouching) buildSpeed *= 2.0f; // Standing up gets spotted much faster

                _currentDetectionRatio = Mathf.Clamp01(_currentDetectionRatio + buildSpeed * deltaTime);
            }
            else
            {
                _currentDetectionRatio = Mathf.Clamp01(_currentDetectionRatio - DetectionDecayRate * deltaTime);
            }

            // Determine State
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

            // Dispatch update if changed
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

        private bool CheckEnemyLineOfSight(out float closestDistance)
        {
            closestDistance = float.MaxValue;
            if (_playerTransform == null) return false;

            Vector3 playerEyePos = _playerTransform.position + Vector3.up * 1.5f;

            // Find hostile actors nearby (tagged Enemy or Target)
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            bool anySpotted = false;

            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.activeInHierarchy) continue;

                Vector3 enemyEyePos = enemy.transform.position + Vector3.up * 1.5f;
                Vector3 toPlayer = playerEyePos - enemyEyePos;
                float distance = toPlayer.magnitude;

                if (distance > MaxAwarenessDistance) continue;

                // Check field of view cone (120 degrees forward)
                Vector3 enemyForward = enemy.transform.forward;
                float angle = Vector3.Angle(enemyForward, toPlayer.normalized);
                if (angle > 65f) continue;

                // Raycast check for visual blockage
                if (!Physics.Linecast(enemyEyePos, playerEyePos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    anySpotted = true;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                    }
                }
                else if (hit.transform == _playerTransform)
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
