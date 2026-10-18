namespace Gameplay.Entities
{
    using UnityEngine;
    using System.Collections.Generic;
    using Data.Config;

    /// <summary>
    /// Chest, crate, or container in the world that stores items and can be looted/opened by a user.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Base Container")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InventoryComponent))]
    public class BaseContainer : BaseEntity
    {
        #region Fields

        private InventoryComponent _inventory;

        #endregion

        #region Properties

        public ContainerData ContainerBlueprint => Blueprint as ContainerData;

        public InventoryComponent Inventory => _inventory;

        public override string InteractionPrompt
        {
            get
            {
                if (ContainerBlueprint != null && ContainerBlueprint.IsLocked)
                    return $"{ContainerBlueprint.DisplayName} (Locked)";

                return Blueprint != null
                    ? $"Press E to open {Blueprint.DisplayName}"
                    : "Press E to open container";
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _inventory = GetComponent<InventoryComponent>();
        }

        #endregion

        #region Initialization

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ContainerData))
            {
                Debug.LogError($"[BaseContainer] Cannot initialize with non-ContainerData blueprint on {name}!");
                return;
            }

            base.Initialize(blueprint);
        }

        #endregion

        #region Interaction

        public override void OnInteract(GameObject user)
        {
            if (ContainerBlueprint != null && ContainerBlueprint.IsLocked)
            {
                Debug.Log($"[BaseContainer] {ContainerBlueprint.DisplayName} is locked!");
                return;
            }

            if (user != null && user.TryGetComponent<InventoryComponent>(out var userInventory))
            {
                TransferItemsTo(userInventory);
            }

            Debug.Log($"[BaseContainer] Opened {ContainerBlueprint?.DisplayName ?? name}.");
        }

        public override void OnActivate()
        {
            base.OnActivate();
            OnInteract(null);
        }

        private void TransferItemsTo(InventoryComponent targetInventory)
        {
            if (_inventory == null || targetInventory == null) return;

            var slots = new List<Domain.Inventory.InventorySlot>(_inventory.Slots);
            foreach (var slot in slots)
            {
                if (slot != null && !slot.IsEmpty)
                {
                    bool added = targetInventory.AddItem(slot.Item, slot.Count);
                    if (added)
                    {
                        _inventory.RemoveItem(slot.Item, slot.Count);
                    }
                }
            }
        }

        #endregion
    }
}
