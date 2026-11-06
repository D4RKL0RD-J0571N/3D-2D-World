namespace Domain.Inventory
{
    using System;
    using Data.Config;

    /// <summary>
    /// Represents an inventory slot containing an item blueprint reference and stack count.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public ItemData Item;
        public int Count;

        public InventorySlot()
        {
            Item = null;
            Count = 0;
        }

        public InventorySlot(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Item == null || Count <= 0;
    }
}
