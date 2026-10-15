namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    [DisallowMultipleComponent]
    public abstract class BaseEntity : MonoBehaviour
    {
        [SerializeField] private EntityData _blueprint;

        public EntityData Blueprint => _blueprint;

        public virtual void Initialize(EntityData blueprint)
        {
            _blueprint = blueprint;

            // Broadcast initialization to all peer components on this GameObject
            SendMessage("OnEntityInitialized", this, SendMessageOptions.DontRequireReceiver);
        }

        public virtual void OnActivate()
        {
            // Default activation behavior (e.g. interacting with it)
        }
    }
}
