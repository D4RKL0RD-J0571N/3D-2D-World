namespace Gameplay.Entities
{
    using UnityEngine;
    using System.Collections.Generic;
    using Data.Config;

    public class BaseContainer : BaseEntity
    {
        public ContainerData ContainerBlueprint => Blueprint as ContainerData;

        // Runtime inventory representation
        // For now, a simple list of ItemData. Later, an InventorySlot class with quantities.
        [SerializeField] private List<ItemData> _inventory = new List<ItemData>();

        public override void Initialize(EntityData blueprint)
        {
            if (!(blueprint is ContainerData))
            {
                Debug.LogError($"[BaseContainer] Cannot initialize with non-ContainerData blueprint!");
                return;
            }

            base.Initialize(blueprint);
            
            // Broadcast container-specific initialization
            SendMessage("OnContainerInitialized", this, SendMessageOptions.DontRequireReceiver);
        }

        public override void OnActivate()
        {
            base.OnActivate();
            
            if (ContainerBlueprint != null && ContainerBlueprint.IsLocked)
            {
                Debug.Log($"[BaseContainer] {ContainerBlueprint.DisplayName} is locked!");
                return;
            }

            Debug.Log($"[BaseContainer] Opened {ContainerBlueprint?.DisplayName}. Contains {_inventory.Count} items.");
            // Publish an event here later to open UI, e.g., Core.GameEventManager.Publish(new ContainerOpenedEvent { Container = this });
        }

        public void AddItem(ItemData item)
        {
            if (_inventory.Count < (ContainerBlueprint?.SlotCount ?? 10))
            {
                _inventory.Add(item);
            }
            else
            {
                Debug.LogWarning($"[BaseContainer] {ContainerBlueprint?.DisplayName} is full!");
            }
        }
    }
}
