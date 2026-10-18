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

        #endregion

        #region Initialization

        public virtual void Initialize(EntityData blueprint)
        {
            _blueprint = blueprint;
        }

        #endregion

        #region Activation

        public virtual void OnActivate()
        {
            // Default activation behavior (e.g. interacting with it)
        }

        #endregion
    }
}
