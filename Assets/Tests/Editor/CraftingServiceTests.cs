namespace Tests.Editor
{
    using NUnit.Framework;
    using UnityEngine;
    using Data.Config;
    using Domain.Crafting;
    using Gameplay.Entities;
    using Systems.Crafting;

    [TestFixture]
    public class CraftingServiceTests
    {
        private GameObject _crafter;
        private InventoryComponent _inventory;
        private CraftingService _craftingService;
        private ItemData _woodLog;
        private ItemData _woodPlank;
        private CraftingRecipe _plankRecipe;

        [SetUp]
        public void SetUp()
        {
            _crafter = new GameObject("TestCrafter");
            _inventory = _crafter.AddComponent<InventoryComponent>();

            _craftingService = new CraftingService();
            _craftingService.Initialize();

            _woodLog = ScriptableObject.CreateInstance<ItemData>();
            _woodLog.name = "Test_WoodLog";
            _woodLog.DisplayName = "Wood Log";
            _woodLog.IsStackable = true;
            _woodLog.MaxStackSize = 64;

            _woodPlank = ScriptableObject.CreateInstance<ItemData>();
            _woodPlank.name = "Test_WoodPlank";
            _woodPlank.DisplayName = "Wood Plank";
            _woodPlank.IsStackable = true;
            _woodPlank.MaxStackSize = 99;

            _plankRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            var ingredientsField = typeof(CraftingRecipe).GetField("_ingredients", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resultItemField = typeof(CraftingRecipe).GetField("_resultItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var resultCountField = typeof(CraftingRecipe).GetField("_resultCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            ingredientsField?.SetValue(_plankRecipe, new System.Collections.Generic.List<CraftingIngredient>
            {
                new CraftingIngredient(_woodLog, 2)
            });
            resultItemField?.SetValue(_plankRecipe, _woodPlank);
            resultCountField?.SetValue(_plankRecipe, 4);
        }

        [TearDown]
        public void TearDown()
        {
            _craftingService.Shutdown();

            if (_crafter != null)
            {
                Object.DestroyImmediate(_crafter);
            }

            if (_woodLog != null) Object.DestroyImmediate(_woodLog);
            if (_woodPlank != null) Object.DestroyImmediate(_woodPlank);
            if (_plankRecipe != null) Object.DestroyImmediate(_plankRecipe);
        }

        [Test]
        public void TryCraft_WhenInsufficientIngredients_ReturnsFalse()
        {
            _inventory.AddItem(_woodLog, 1); // Only 1, needs 2

            bool success = _craftingService.TryCraft(_crafter, _plankRecipe);

            Assert.IsFalse(success);
            Assert.AreEqual(1, _inventory.GetItemCount(_woodLog));
            Assert.AreEqual(0, _inventory.GetItemCount(_woodPlank));
        }

        [Test]
        public void TryCraft_WhenIngredientsSufficient_ConsumesIngredientsAndAddsResult()
        {
            _inventory.AddItem(_woodLog, 5);

            bool success = _craftingService.TryCraft(_crafter, _plankRecipe);

            Assert.IsTrue(success);
            Assert.AreEqual(3, _inventory.GetItemCount(_woodLog), "2 logs should have been consumed.");
            Assert.AreEqual(4, _inventory.GetItemCount(_woodPlank), "4 planks should have been produced.");
        }

        [Test]
        public void InventoryComponent_GetSaveData_And_RestoreSaveData_RoundtripsAccurately()
        {
            _inventory.AddItem(_woodLog, 3);
            _inventory.AddItem(_woodPlank, 8);

            var savedSlots = _inventory.GetSaveData();
            Assert.IsNotNull(savedSlots);
            Assert.AreEqual(2, savedSlots.Length);

            // Create separate inventory component
            var otherGo = new GameObject("OtherCrafter");
            var otherInventory = otherGo.AddComponent<InventoryComponent>();

            otherInventory.RestoreSaveData(savedSlots, id =>
            {
                if (id == _woodLog.Id) return _woodLog;
                if (id == _woodPlank.Id) return _woodPlank;
                return null;
            });

            Assert.AreEqual(3, otherInventory.GetItemCount(_woodLog));
            Assert.AreEqual(8, otherInventory.GetItemCount(_woodPlank));

            Object.DestroyImmediate(otherGo);
        }
    }
}
