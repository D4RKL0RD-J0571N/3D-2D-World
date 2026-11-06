namespace Gameplay.Interactables
{
    using System.Collections.Generic;
    using Core;
    using Core.Content;
    using Domain.Crafting;
    using Gameplay.Entities;
    using Systems.Crafting;
    using UnityEngine;

    /// <summary>
    /// Interactive world crafting station (e.g. Blacksmith Anvil, Forge, Workbench).
    /// Raycasting player interaction attempts to forge available recipes from inventory materials.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Crafting Station")]
    public class CraftingStationInteractable : MonoBehaviour, IInteractable
    {
        #region Inspector Fields

        [Header("Station Configuration")]
        [SerializeField] private string _stationName = "Blacksmith Anvil";
        [SerializeField] private List<CraftingRecipe> _stationRecipes = new();

        #endregion

        #region IInteractable Properties

        public bool CanInteract => true;

        public string InteractionPrompt => $"🔨 Use {_stationName} (Craft)";

        public string StationName
        {
            get => _stationName;
            set => _stationName = value;
        }

        public void SetStationName(string name) => _stationName = name;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            EnsureRecipesLoaded();
        }

        #endregion

        #region Setup & Recipe Resolution

        private void EnsureRecipesLoaded()
        {
            if (_stationRecipes.Count > 0) return;

            // 1. Try resolving recipes from ContentRegistry
            if (ServiceLocator.TryGet<IContentService>(out var contentService))
            {
                var reg = contentService.GetRegistry<CraftingRecipe>();
                if (reg != null)
                {
                    foreach (var recipe in reg.GetAll())
                    {
                        if (recipe != null && !_stationRecipes.Contains(recipe))
                        {
                            _stationRecipes.Add(recipe);
                        }
                    }
                }
            }

            // 2. Fall back to Resources
            if (_stationRecipes.Count == 0)
            {
                var loaded = Resources.LoadAll<CraftingRecipe>(string.Empty);
                for (int i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] != null && !_stationRecipes.Contains(loaded[i]))
                    {
                        _stationRecipes.Add(loaded[i]);
                    }
                }
            }
        }

        #endregion

        #region Interaction Handling

        public void OnInteract(BaseActor interactor)
        {
            if (interactor == null) return;

            if (!interactor.TryGetComponent<InventoryComponent>(out var inventory))
            {
                Debug.LogWarning($"[CraftingStationInteractable] Interactor {interactor.name} has no inventory!");
                return;
            }

            if (!ServiceLocator.TryGet<CraftingService>(out var craftingService))
            {
                Debug.LogWarning("[CraftingStationInteractable] CraftingService not registered in ServiceLocator!");
                return;
            }

            EnsureRecipesLoaded();
            TryExecuteCrafting(interactor.gameObject, inventory, craftingService);
        }

        private void TryExecuteCrafting(GameObject interactorObj, InventoryComponent inventory, CraftingService craftingService)
        {
            bool anyCrafted = false;

            for (int i = 0; i < _stationRecipes.Count; i++)
            {
                CraftingRecipe recipe = _stationRecipes[i];
                if (recipe == null) continue;

                if (recipe.CanCraft(inventory))
                {
                    bool success = craftingService.TryCraft(interactorObj, recipe);
                    if (success)
                    {
                        PlayCraftingFeedback();
                        anyCrafted = true;
                        break;
                    }
                }
            }

            if (!anyCrafted)
            {
                LogInsufficientMaterialsFeedback();
            }
        }

        private void PlayCraftingFeedback()
        {
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.PlayInteract();
            }
        }

        private void LogInsufficientMaterialsFeedback()
        {
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.PlayClick();
            }

            Debug.Log($"<color=orange>[{_stationName}] Insufficient materials to craft. Gather Wood Logs or Stone Chunks first!</color>");
        }

        #endregion
    }
}
