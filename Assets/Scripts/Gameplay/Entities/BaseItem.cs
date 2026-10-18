namespace Gameplay.Entities
{
    using UnityEngine;
    using Data.Config;

    /// <summary>
    /// Physical world representation of an item that can be picked up into an entity's inventory.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Base Item")]
    [DisallowMultipleComponent]
    public class BaseItem : BaseEntity
    {
        #region Properties

        public ItemData ItemBlueprint => Blueprint as ItemData;

        public override string InteractionPrompt => Blueprint != null
            ? $"Press E to pick up {Blueprint.DisplayName}"
            : "Press E to pick up item";

        #endregion

        #region Initialization

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ItemData))
            {
                Debug.LogError($"[BaseItem] Cannot initialize with non-ItemData blueprint on {name}!");
                return;
            }

            base.Initialize(blueprint);
        }

        #endregion

        #region Interaction

        public override void OnInteract(GameObject user)
        {
            if (user != null && ItemBlueprint != null)
            {
                if (user.TryGetComponent<InventoryComponent>(out var inventory))
                {
                    bool added = inventory.AddItem(ItemBlueprint, 1);
                    if (!added)
                    {
                        Debug.LogWarning($"[BaseItem] Could not add {ItemBlueprint.DisplayName} - inventory full!");
                        return;
                    }
                }
            }

            Debug.Log($"[BaseItem] Picked up {ItemBlueprint?.DisplayName}.");
            Destroy(gameObject);
        }

        public override void OnActivate()
        {
            base.OnActivate();
            OnInteract(null);
        }

        #endregion
    }
}
