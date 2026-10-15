namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    public class BaseItem : BaseEntity
    {
        public ItemData ItemBlueprint => Blueprint as ItemData;

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ItemData))
            {
                Debug.LogError($"[BaseItem] Cannot initialize with non-ItemData blueprint!");
                return;
            }

            base.Initialize(blueprint);
        }

        public override void OnActivate()
        {
            base.OnActivate();

            Debug.Log($"[BaseItem] Picked up {ItemBlueprint?.DisplayName}.");
            // Publish event to add to player inventory, then destroy this GameObject.

            Destroy(gameObject);
        }
    }
}
