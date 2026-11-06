namespace Domain.Crafting
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Data.Config;
    using UnityEngine;

    /// <summary>
    /// ScriptableObject defining a deterministic recipe that consumes ingredients to produce an output item.
    /// Implements <see cref="IContentDefinition"/> for registration in <see cref="ContentRegistry{T}"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCraftingRecipe", menuName = "3D2DWorld/Crafting/Crafting Recipe")]
    public class CraftingRecipe : ScriptableObject, IContentDefinition
    {
        #region Fields

        [Header("Identity")]
        [SerializeField] private string _recipeId = "Recipe_Planks";
        [SerializeField] private string _displayName = "Craft Wooden Planks";
        [SerializeField] private ResourceId _contentId;
        [SerializeField] private string _sourceModId = ResourceId.DefaultNamespace;

        [Header("Recipe Definition")]
        [SerializeField] private List<CraftingIngredient> _ingredients = new List<CraftingIngredient>();
        [SerializeField] private ItemData _resultItem;
        [SerializeField] private int _resultCount = 4;
        [SerializeField] private float _craftTimeSeconds = 0f;

        #endregion

        #region Properties & IContentDefinition

        public string RecipeId => _recipeId;
        public string DisplayName => _displayName;
        public IReadOnlyList<CraftingIngredient> Ingredients => _ingredients;
        public ItemData ResultItem => _resultItem;
        public int ResultCount => _resultCount;
        public float CraftTimeSeconds => _craftTimeSeconds;

        public ResourceId ContentId
        {
            get
            {
                if (!_contentId.IsEmpty)
                {
                    return _contentId;
                }

                string raw = !string.IsNullOrWhiteSpace(_recipeId) ? _recipeId : name;
                return ResourceId.FromLegacyId(_sourceModId, "recipe", raw);
            }
            set => _contentId = value;
        }

        public string SourceModId
        {
            get => string.IsNullOrWhiteSpace(_sourceModId) ? ResourceId.DefaultNamespace : _sourceModId;
            set => _sourceModId = value;
        }

        string IIdentifiable.Id => _recipeId;

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
