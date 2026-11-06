namespace Gameplay.Skyrim
{
    using System;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Gameplay.Entities;
    using UnityEngine;

    /// <summary>
    /// Routine time-of-day phases for town NPCs.
    /// </summary>
    public enum CitizenSchedulePhase
    {
        WorkMorning,
        WanderAfternoon,
        TavernEvening,
        SleepNight
    }

    /// <summary>
    /// Implements realistic daily schedules and survival instincts modeled after
    /// "Immersive Citizens - AI Overhaul SE". Civilian NPCs follow daily work/tavern/sleep
    /// routines and panic/flee to shelter when danger strikes.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Skyrim/Immersive Citizen AI")]
    [RequireComponent(typeof(BaseActor))]
    public class ImmersiveCitizenAI : MonoBehaviour
    {
        #region Fields

        [Header("Citizen Configuration")]
        [SerializeField] private bool _isCivilian = true;
        [SerializeField] private float _moveSpeed = 2.2f;
        [SerializeField] private CitizenSchedulePhase _currentPhase = CitizenSchedulePhase.WorkMorning;

        [Header("Routine Waypoints")]
        [SerializeField] private Vector3 _workPosition;
        [SerializeField] private Vector3 _wanderPosition;
        [SerializeField] private Vector3 _tavernPosition;
        [SerializeField] private Vector3 _sleepPosition;
        [SerializeField] private Vector3 _shelterPosition;

        private bool _isPanicking;
        private float _panicTimer;
        private BaseActor _actor;

        #endregion

        #region Properties

        public bool IsCivilian => _isCivilian;
        public bool IsPanicking => _isPanicking;
        public CitizenSchedulePhase CurrentPhase => _currentPhase;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            if (_workPosition == Vector3.zero) _workPosition = transform.position;
            if (_wanderPosition == Vector3.zero) _wanderPosition = transform.position + new Vector3(2f, 0f, 2f);
            if (_tavernPosition == Vector3.zero) _tavernPosition = transform.position + new Vector3(-3f, 0f, 1f);
            if (_sleepPosition == Vector3.zero) _sleepPosition = transform.position + new Vector3(0f, 0f, -4f);
            if (_shelterPosition == Vector3.zero) _shelterPosition = transform.position + new Vector3(0f, 0f, -6f);
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
        }

        private void Update()
        {
            if (_isPanicking)
            {
                UpdatePanic(Time.deltaTime);
            }
            else
            {
                UpdateRoutine(Time.deltaTime);
            }
        }

        #endregion

        #region Routine & Survival Execution

        public void SetSchedulePhase(CitizenSchedulePhase phase)
        {
            _currentPhase = phase;
        }

        private void UpdateRoutine(float deltaTime)
        {
            Vector3 target = _currentPhase switch
            {
                CitizenSchedulePhase.WorkMorning => _workPosition,
                CitizenSchedulePhase.WanderAfternoon => _wanderPosition,
                CitizenSchedulePhase.TavernEvening => _tavernPosition,
                CitizenSchedulePhase.SleepNight => _sleepPosition,
                _ => _workPosition
            };

            MoveToward(target, _moveSpeed, deltaTime);
        }

        private void UpdatePanic(float deltaTime)
        {
            _panicTimer -= deltaTime;
            if (_panicTimer <= 0f)
            {
                _isPanicking = false;
                return;
            }

            MoveToward(_shelterPosition, _moveSpeed * 1.8f, deltaTime);
        }

        private void MoveToward(Vector3 target, float speed, float deltaTime)
        {
            Vector3 diff = target - transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(diff), 8f * deltaTime);
                transform.position = Vector3.MoveTowards(transform.position, target, speed * deltaTime);
            }
        }

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt == null || !_isCivilian) return;

            // If this civilian or a nearby ally takes damage, trigger survival flee
            float dist = Vector3.Distance(transform.position, evt.Target != null ? evt.Target.transform.position : transform.position);
            if (dist < 12f)
            {
                _isPanicking = true;
                _panicTimer = 8f;
                Debug.Log($"<color=yellow>[ImmersiveCitizens] {name} panicked and is fleeing to shelter!</color>");
            }
        }

        #endregion
    }
}
