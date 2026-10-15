namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    [DisallowMultipleComponent]
    public class BaseActor : BaseEntity
    {
        public ActorData ActorBlueprint => Blueprint as ActorData;

        /// <summary>
        /// Runtime room/area assignment used by area-scheduling systems.
        /// </summary>
        public string RoomId { get; set; }

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ActorData))
            {
                Debug.LogError($"[BaseActor] Cannot initialize with non-ActorData blueprint!");
                return;
            }

            base.Initialize(blueprint);
            // Broadcast actor-specific initialization
            SendMessage("OnActorInitialized", this, SendMessageOptions.DontRequireReceiver);
        }
    }
}
