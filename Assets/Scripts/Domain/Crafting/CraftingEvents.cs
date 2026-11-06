namespace Domain.Crafting
{
    using UnityEngine;
    using Core;
    using Data.Config;

    /// <summary>
    /// Event published whenever a crafting recipe is executed.
    /// </summary>
    public sealed class ItemCraftedEvent : GameEvent
    {
        public GameObject Crafter;
        public CraftingRecipe Recipe;
        public ItemData OutputItem;
        public int OutputCount;
    }

    /// <summary>
    /// Event requested to initiate crafting an item recipe.
    /// </summary>
    public sealed class CraftItemRequestedEvent : GameEvent
    {
        public GameObject Crafter;
        public CraftingRecipe Recipe;
    }
}
