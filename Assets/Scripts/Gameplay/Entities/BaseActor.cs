namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    /// <summary>
    /// Base actor shell coordinating peer components (attributes, stats, states) on an entity.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Base Actor")]
    [DisallowMultipleComponent]
    public class BaseActor : BaseEntity
    {
        #region Fields

        private AttributeComponent _attributeComponent;
        private StatsComponent _statsComponent;

        #endregion

        #region Properties

        public ActorData ActorBlueprint => Blueprint as ActorData;

        /// <summary>
        /// Runtime room/area assignment used by area-scheduling systems.
        /// </summary>
        public string RoomId { get; set; }

        #endregion

        #region Component Resolution

        private void EnsureComponentsCached()
        {
            if (_attributeComponent == null) _attributeComponent = GetComponent<AttributeComponent>();
            if (_statsComponent == null) _statsComponent = GetComponent<StatsComponent>();
        }

        #endregion

        #region Initialization

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ActorData))
            {
                Debug.LogError($"[BaseActor] Cannot initialize with non-ActorData blueprint on {name}!");
                return;
            }

            base.Initialize(blueprint);

            // Direct, lossless initialization of peer components
            EnsureComponentsCached();
            _attributeComponent?.Initialize(this);
            _statsComponent?.Initialize(this);
        }

        #endregion
    }
}
