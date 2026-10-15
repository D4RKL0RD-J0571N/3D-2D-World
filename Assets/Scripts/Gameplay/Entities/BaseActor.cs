namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    [DisallowMultipleComponent]
    public class BaseActor : BaseEntity
    {
        public ActorData ActorBlueprint => Blueprint as ActorData;

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
