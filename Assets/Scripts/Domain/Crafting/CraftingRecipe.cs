namespace Domain.Crafting
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Data.Config;

    /// <summary>
    /// ScriptableObject defining a deterministic recipe that consumes ingredients to produce an output item.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCraftingRecipe", menuName = "3D2DWorld/Crafting/Crafting Recipe")]
    public class CraftingRecipe : ScriptableObject
    {
        #region Fields

        [Header("Identity")]
        [SerializeField] private string _recipeId = "Recipe_Planks";
        [SerializeField] private string _displayName = "Craft Wooden Planks";

        [Header("Recipe Definition")]
        [SerializeField] private List<CraftingIngredient> _ingredients = new List<CraftingIngredient>();
        [SerializeField] private ItemData _resultItem;
        [SerializeField] private int _resultCount = 4;
        [SerializeField] private float _craftTimeSeconds = 0f;

        #endregion

        #region Properties

        public string RecipeId => _recipeId;
        public string DisplayName => _displayName;
        public IReadOnlyList<CraftingIngredient> Ingredients => _ingredients;
        public ItemData ResultItem => _resultItem;
        public int ResultCount => _resultCount;
        public float CraftTimeSeconds => _craftTimeSeconds;

        #endregion

        #region Validation

        public bool CanCraft(Gameplay.Entities.InventoryComponent inventory)
        {
            if (inventory == null || _resultItem == null || _ingredients == null || _ingredients.Count == 0)
                return false;

            foreach (var ingredient in _ingredients)
            {
                if (ingredient.Item == null || ingredient.Count <= 0)
                    return false;

                if (!inventory.HasItem(ingredient.Item, ingredient.Count))
                    return false;
            }

            return true;
        }

        #endregion
    }
}
