namespace Domain.Crafting
{
    using System;
    using Data.Config;

    /// <summary>
    /// Represents an input ingredient required for a crafting recipe.
    /// </summary>
    [Serializable]
    public struct CraftingIngredient
    {
        public ItemData Item;
        public int Count;

        public CraftingIngredient(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
