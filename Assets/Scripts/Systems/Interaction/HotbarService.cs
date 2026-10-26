namespace Systems.Interaction
{
    using Core;
    using Data.Config;
    using Domain.Grid;
    using Domain.Inventory;
    using Gameplay.Entities;
    using UnityEngine;

    /// <summary>Published when the hotbar selection or slot contents change.</summary>
    public sealed class HotbarStateChangedEvent : GameEvent
    {
        public int SelectedSlotIndex;
        public ItemData SelectedItem;
        public ItemData[] Slots;
    }

    /// <summary>
    /// Service managing the player's 9-slot item quickbar.
    /// When the player has inventory items, slots reflect those items.
    /// When inventory is empty, slots fall back to the BlockRegistry palette
    /// so block placement always works even at game start.
    /// </summary>
    public sealed class HotbarService : IGameService
    {
        public const int SlotCount = 9;

        #region Fields
        private readonly ItemData[] _slots = new ItemData[SlotCount];
        private int _selectedSlotIndex;
        private BlockRegistry _registry;
        private bool _usingRegistryFallback;
        #endregion

        #region Properties
        public int SelectedSlotIndex => _selectedSlotIndex;
        public ItemData SelectedItem  => _slots[_selectedSlotIndex];
        #endregion

        #region IGameService
        public void Initialize()
        {
            GameEventManager.AddListener<PlayerHotbarSlotSelectedEvent>(OnSlotSelected);
            GameEventManager.AddListener<InventoryChangedEvent>(OnInventoryChanged);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<PlayerHotbarSlotSelectedEvent>(OnSlotSelected);
            GameEventManager.RemoveListener<InventoryChangedEvent>(OnInventoryChanged);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Assigns the block registry used for the fallback palette and for
        /// resolving which block to activate during placement.
        /// </summary>
        public void SetRegistry(BlockRegistry registry)
        {
            _registry = registry;
            PopulateFromRegistryFallback();
        }

        /// <summary>Returns the item in the given slot index, or null if empty.</summary>
        public ItemData GetItemAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;
            return _slots[slotIndex];
        }

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            _selectedSlotIndex = slotIndex;
            SyncBlockPlacement();
            NotifyStateChanged();
        }

        public void CycleSlot(int delta)
        {
            int next = (_selectedSlotIndex + delta) % SlotCount;
            if (next < 0) next += SlotCount;
            SelectSlot(next);
        }

        /// <summary>
        /// Populates hotbar slots from the player's inventory.
        /// Falls back to the block registry palette when inventory is empty.
        /// </summary>
        public void PopulateFromInventory(InventoryComponent inventory)
        {
            if (inventory == null) return;

            var inventorySlots = inventory.Slots;
            bool hasItems = inventorySlots.Count > 0;

            if (hasItems)
            {
                _usingRegistryFallback = false;
                for (int i = 0; i < SlotCount; i++)
                    _slots[i] = i < inventorySlots.Count ? inventorySlots[i].Item : null;
            }
            else
            {
                // Inventory is now empty — revert to block palette
                PopulateFromRegistryFallback();
                return;
            }

            SyncBlockPlacement();
            NotifyStateChanged();
        }
        #endregion

        #region Event Handlers
        private void OnSlotSelected(PlayerHotbarSlotSelectedEvent evt)
        {
            SelectSlot(evt.SlotIndex);
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            if (evt?.Owner == null) return;
            if (!evt.Owner.CompareTag("Player")) return;

            var inventory = evt.Owner.GetComponent<InventoryComponent>();
            if (inventory == null) return;

            PopulateFromInventory(inventory);
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Populates slots from the BlockRegistry as a fallback palette.
        /// Each block is wrapped in a transient ItemData-like reference via
        /// block DisplayName matching for placement resolution.
        /// When using the registry fallback, slots hold null ItemData but
        /// SelectedBlock on BlockPlacementService is set directly from the registry.
        /// </summary>
        private void PopulateFromRegistryFallback()
        {
            if (_registry == null) return;

            _usingRegistryFallback = true;
            var blocks = _registry.GetAllBlocks();

            // Clear item slots — placement goes through SelectedBlock directly
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = null;

            // Set the initially selected block
            SyncBlockPlacement();
            NotifyStateChanged();
        }

        private void SyncBlockPlacement()
        {
            if (!ServiceLocator.TryGet<BlockPlacementService>(out var placement)) return;

            if (_usingRegistryFallback && _registry != null)
            {
                // In fallback mode, drive block selection directly from registry index
                var blocks = _registry.GetAllBlocks();
                placement.SelectedBlock = _selectedSlotIndex < blocks.Count
                    ? blocks[_selectedSlotIndex]
                    : null;
            }
            else
            {
                // In inventory mode, resolve block from item display name
                placement.SelectedBlock = ResolveBlock(_slots[_selectedSlotIndex]);
            }
        }

        private BlockDefinition ResolveBlock(ItemData item)
        {
            if (item == null || _registry == null) return null;
            foreach (var block in _registry.GetAllBlocks())
            {
                if (block != null && block.DisplayName == item.DisplayName)
                    return block;
            }
            return null;
        }

        private void NotifyStateChanged()
        {
            GameEventManager.Publish(new HotbarStateChangedEvent
            {
                SelectedSlotIndex = _selectedSlotIndex,
                SelectedItem = _slots[_selectedSlotIndex],
                Slots = (ItemData[])_slots.Clone()
            });
        }
        #endregion
    }
}
