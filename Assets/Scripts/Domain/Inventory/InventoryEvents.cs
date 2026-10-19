namespace Domain.Inventory
{
    using Core;
    using Data.Config;
    using UnityEngine;

    /// <summary>
    /// Published when an entity's inventory is modified (item added, removed, or stack changed).
    /// </summary>
    public sealed class InventoryChangedEvent : GameEvent
    {
        public GameObject Owner;
        public ItemData Item;
        public int CountDelta;
        public int NewTotalCount;
    }
}
