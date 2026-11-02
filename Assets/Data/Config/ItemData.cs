namespace Data.Config
{
    using Core.Ids;
    using Domain.Grid;
    using UnityEngine;

    /// <summary>
    /// ScriptableObject blueprint for any item in the game world.
    /// Extend into WeaponData, ConsumableData, etc. for typed sub-categories.
    /// </summary>
    [CreateAssetMenu(fileName = "NewItemData", menuName = "3D2DWorld/Entities/Item Data")]
    public class ItemData : EntityData
    {
        public override string CategoryPrefix => "ITM";

        [Header("Item Properties")]
        public float Damage = 0.0f;
        public float Weight = 1.0f;
        public int Value = 10;
        public bool IsStackable = true;
        public int MaxStackSize = 99;
        public string Description = string.Empty;

        [Header("Presentation")]
        public Sprite Icon;

        [Header("Voxel / Block Placement")]
        [Tooltip("Typed reference to the BlockDefinition placed when this item is used from the hotbar.")]
        public DefinitionRef<BlockDefinition> PlaceableBlock;
    }
}
