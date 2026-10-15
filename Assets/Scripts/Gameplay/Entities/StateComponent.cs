namespace Gameplay.Entities
{
    using UnityEngine;
    using Domain.Entities;

    [RequireComponent(typeof(BaseActor))]
    public class StateComponent : MonoBehaviour
    {
        private BaseActor _actor;
        [SerializeField] private ActorState _currentState = ActorState.None;

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
        }

        public ActorState GetCurrentState() => _currentState;

        public void SetState(ActorState state)
        {
            ActorState oldState = _currentState;
            _currentState |= state;
            if (oldState != _currentState)
            {
                Core.GameEventManager.Publish(new EntityStateChangedEvent { Target = _actor, NewStates = _currentState });
            }
        }

        public void RemoveState(ActorState state)
        {
            ActorState oldState = _currentState;
            _currentState &= ~state;
            if (oldState != _currentState)
            {
                Core.GameEventManager.Publish(new EntityStateChangedEvent { Target = _actor, NewStates = _currentState });
            }
        }

        public bool HasState(ActorState state)
        {
            return (_currentState & state) != 0;
        }

        public void ClearStates()
        {
            if (_currentState != ActorState.None)
            {
                _currentState = ActorState.None;
                Core.GameEventManager.Publish(new EntityStateChangedEvent { Target = _actor, NewStates = _currentState });
            }
        }
    }
}
