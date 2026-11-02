namespace Gameplay.Entities
{
    using Core.Ids;
    using Core.Persistence;
    using Data.Config;
    using UnityEngine;

    /// <summary>
    /// Abstract foundation for any entity in the world instantiated from an <see cref="EntityData"/> blueprint.
    /// Automatically maintains a <see cref="PersistentEntityComponent"/> for deterministic save/load identity.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Base Entity")]
    [DisallowMultipleComponent]
    public abstract class BaseEntity : MonoBehaviour
    {
        #region Fields

        [SerializeField] private EntityData _blueprint;
        private PersistentEntityComponent _persistentIdentity;

        #endregion

        #region Properties

        public EntityData Blueprint
        {
            get => _blueprint;
            set
            {
                _blueprint = value;
                if (_blueprint != null && _blueprint.ContentId.IsValid)
                {
                    PersistentIdentity.DefinitionId = _blueprint.ContentId;
                }
            }
        }

        /// <summary>Gets or attaches the <see cref="PersistentEntityComponent"/> for this entity.</summary>
        public PersistentEntityComponent PersistentIdentity
        {
            get
            {
                if (_persistentIdentity == null)
                {
                    if (!TryGetComponent(out _persistentIdentity))
                    {
                        _persistentIdentity = gameObject.AddComponent<PersistentEntityComponent>();
                    }
                    if (_blueprint != null && _blueprint.ContentId.IsValid && _persistentIdentity.DefinitionId.IsEmpty)
                    {
                        _persistentIdentity.DefinitionId = _blueprint.ContentId;
                    }
                }
                return _persistentIdentity;
            }
        }

        /// <summary>Gets or sets the persistent <see cref="InstanceGuid"/> of this entity.</summary>
        public InstanceGuid InstanceId
        {
            get => PersistentIdentity.InstanceId;
            set => PersistentIdentity.InstanceId = value;
        }

        /// <summary>
        /// Contextual prompt shown to the player when looking at this entity.
        /// </summary>
        public virtual string InteractionPrompt => Blueprint != null
            ? $"Press E to interact with {Blueprint.DisplayName}"
            : "Press E to interact";

        #endregion

        #region Initialization

        public virtual void Initialize(EntityData blueprint)
        {
            Blueprint = blueprint;
            PersistentIdentity.EnsureRegistered();
        }

        #endregion

        #region Interaction & Activation

        /// <summary>
        /// Invoked when a user (e.g. player) directly interacts with this entity in the world.
        /// </summary>
        /// <param name="user">The initiating GameObject (e.g. player actor).</param>
        public virtual void OnInteract(GameObject user)
        {
            OnActivate();
        }

        public virtual void OnActivate()
        {
            // Default activation behavior
        }

        #endregion
    }
}
