namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    /// <summary>
    /// Abstract foundation for any entity in the world instantiated from an <see cref="EntityData"/> blueprint.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Base Entity")]
    [DisallowMultipleComponent]
    public abstract class BaseEntity : MonoBehaviour
    {
        #region Fields

        [SerializeField] private EntityData _blueprint;

        #endregion

        #region Properties

        public EntityData Blueprint
        {
            get => _blueprint;
            set => _blueprint = value;
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
            _blueprint = blueprint;
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
