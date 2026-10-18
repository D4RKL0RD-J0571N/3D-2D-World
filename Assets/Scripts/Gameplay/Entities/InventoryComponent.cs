namespace Gameplay.Entities
{
    using System.Collections.Generic;
    using UnityEngine;
    using Data.Config;
    using Domain.Inventory;
    using Core;

    /// <summary>
    /// Component managing an entity's item storage, stacking, additions, and removals.
    /// Publishes <see cref="InventoryChangedEvent"/> upon any modification.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Inventory Component")]
    [DisallowMultipleComponent]
    public class InventoryComponent : MonoBehaviour
    {
        #region Fields

        [SerializeField] private int _capacity = 20;
        [SerializeField] private List<InventorySlot> _slots = new List<InventorySlot>();

        #endregion

        #region Properties

        public int Capacity => _capacity;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        #endregion

        #region Inventory Management

        /// <summary>
        /// Adds an item and count to the inventory, stacking where appropriate.
        /// </summary>
        /// <param name="item">The item blueprint to add.</param>
        /// <param name="count">Quantity to add.</param>
        /// <returns>True if the entire quantity was successfully added; false if inventory was full.</returns>
        public bool AddItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            int remaining = count;

            // 1. Try stacking into existing non-full slots if stackable
            if (item.IsStackable)
            {
                remaining = StackIntoExistingSlots(item, remaining);
            }

            // 2. Put remainder into new or empty slots
            while (remaining > 0 && HasAvailableSlot())
            {
                int stackAmount = item.IsStackable ? Mathf.Min(remaining, item.MaxStackSize) : 1;
                InsertIntoFirstAvailableSlot(item, stackAmount);
                remaining -= stackAmount;
            }

            int added = count - remaining;
            if (added > 0)
            {
                GameEventManager.Publish(new InventoryChangedEvent
                {
                    Owner = gameObject,
                    Item = item,
                    CountDelta = added,
                    NewTotalCount = GetItemCount(item)
                });
            }

            return remaining == 0;
        }

        /// <summary>
        /// Removes a quantity of an item from the inventory.
        /// </summary>
        public bool RemoveItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0 || !HasItem(item, count)) return false;

            int remainingToRemove = count;

            for (int i = _slots.Count - 1; i >= 0 && remainingToRemove > 0; i--)
            {
                var slot = _slots[i];
                if (slot.Item == item)
                {
                    int take = Mathf.Min(slot.Count, remainingToRemove);
                    slot.Count -= take;
                    remainingToRemove -= take;

                    if (slot.Count <= 0)
                    {
                        _slots.RemoveAt(i);
                    }
                }
            }

            GameEventManager.Publish(new InventoryChangedEvent
            {
                Owner = gameObject,
                Item = item,
                CountDelta = -count,
                NewTotalCount = GetItemCount(item)
            });

            return true;
        }

        /// <summary>
        /// Checks whether the inventory contains at least the specified quantity of an item.
        /// </summary>
        public bool HasItem(ItemData item, int count = 1)
        {
            return GetItemCount(item) >= count;
        }

        /// <summary>
        /// Returns total count of a specific item across all slots.
        /// </summary>
        public int GetItemCount(ItemData item)
        {
            if (item == null) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Item == item)
                {
                    total += _slots[i].Count;
                }
            }
            return total;
        }

        #endregion

        #region Helpers

        private int StackIntoExistingSlots(ItemData item, int remaining)
        {
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.Item == item && slot.Count < item.MaxStackSize)
                {
                    int space = item.MaxStackSize - slot.Count;
                    int toAdd = Mathf.Min(space, remaining);
                    slot.Count += toAdd;
                    remaining -= toAdd;
                }
            }
            return remaining;
        }

        private bool HasAvailableSlot()
        {
            return _slots.Count < _capacity;
        }

        private void InsertIntoFirstAvailableSlot(ItemData item, int count)
        {
            _slots.Add(new InventorySlot(item, count));
        }

        #endregion
    }
}
