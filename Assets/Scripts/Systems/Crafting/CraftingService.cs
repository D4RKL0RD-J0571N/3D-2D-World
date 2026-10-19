namespace Systems.Crafting
{
    using UnityEngine;
    using Core;
    using Domain.Crafting;
    using Gameplay.Entities;

    /// <summary>
    /// Service responsible for validating crafting recipe requirements, consuming input items,
    /// and depositing crafted output items into the crafter's inventory.
    /// </summary>
    public class CraftingService : IGameService
    {
        #region IGameService Lifecycle

        public void Initialize()
        {
            GameEventManager.AddListener<CraftItemRequestedEvent>(OnCraftItemRequested);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<CraftItemRequestedEvent>(OnCraftItemRequested);
        }

        #endregion

        #region Crafting Logic

        /// <summary>
        /// Attempts to craft a recipe using the given crafter GameObject's inventory.
        /// </summary>
        /// <param name="crafter">The GameObject owning the InventoryComponent.</param>
        /// <param name="recipe">The recipe to execute.</param>
        /// <returns>True if crafted successfully, false otherwise.</returns>
        public bool TryCraft(GameObject crafter, CraftingRecipe recipe)
        {
            if (crafter == null || recipe == null) return false;

            if (!crafter.TryGetComponent<InventoryComponent>(out var inventory))
            {
                Debug.LogWarning($"[CraftingService] Crafter {crafter.name} does not have an InventoryComponent!");
                return false;
            }

            if (!recipe.CanCraft(inventory))
            {
                Debug.LogWarning($"[CraftingService] Cannot craft {recipe.DisplayName} - insufficient ingredients!");
                return false;
            }

            // 1. Consume ingredients
            foreach (var ingredient in recipe.Ingredients)
            {
                bool removed = inventory.RemoveItem(ingredient.Item, ingredient.Count);
                if (!removed)
                {
                    Debug.LogError($"[CraftingService] Failed to remove ingredient {ingredient.Item.DisplayName} during crafting!");
                    return false;
                }
            }

            // 2. Add output item
            bool added = inventory.AddItem(recipe.ResultItem, recipe.ResultCount);
            if (!added)
            {
                Debug.LogWarning($"[CraftingService] Output {recipe.ResultItem.DisplayName} could not fully fit in inventory!");
            }

            // 3. Publish success event
            GameEventManager.Publish(new ItemCraftedEvent
            {
                Crafter = crafter,
                Recipe = recipe,
                OutputItem = recipe.ResultItem,
                OutputCount = recipe.ResultCount
            });

            Debug.Log($"[CraftingService] Successfully crafted {recipe.ResultCount}x {recipe.ResultItem.DisplayName} for {crafter.name}.");
            return true;
        }

        private void OnCraftItemRequested(CraftItemRequestedEvent evt)
        {
            TryCraft(evt.Crafter, evt.Recipe);
        }

        #endregion
    }
}
