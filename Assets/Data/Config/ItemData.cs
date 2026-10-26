namespace Data.Config
{
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
        public float Weight = 1.0f;
        public int Value = 10;
        public bool IsStackable = true;
        public int MaxStackSize = 99;

        [Header("Presentation")]
        public Sprite Icon;
    }
}
