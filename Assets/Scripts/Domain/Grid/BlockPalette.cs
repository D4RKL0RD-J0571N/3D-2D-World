namespace Domain.Grid
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    [Serializable]
    public struct BlockVariant
    {
        public BlockDefinition Block;
        [Range(1, 100)]
        public int Weight; // Relative weight in probability roll

        public BlockVariant(BlockDefinition block, int weight)
        {
            Block = block;
            Weight = weight;
        }
    }

    /// <summary>
    /// A curated material theme (Crypt, Ancient Wood, Industrial Tech, etc.)
    /// supporting procedural wear variations (cracked bricks, mossy stones, grates).
    /// </summary>
    [CreateAssetMenu(
        fileName = "BlockPalette",
        menuName = "3D2DWorld/Level Generation/Block Palette"
    )]
    public class BlockPalette : ScriptableObject
    {
        [Header("Identity")]
        public string PaletteId = "DefaultStone";
        public string DisplayName = "Classic Stone Dungeon";

        [Header("Primary Architecture")]
        public BlockDefinition DefaultFloor;
        public BlockDefinition DefaultWall;
        public BlockDefinition DefaultCeiling;
        public BlockDefinition DefaultPillar;
        public BlockDefinition DefaultSteps;

        [Header("Surface Wear & Texture Variants")]
        [Tooltip("Floor variants (e.g. 80% clean tiles, 15% cracked tiles, 5% mossy).")]
        public List<BlockVariant> FloorVariants = new();

        [Tooltip("Wall variants (e.g. 85% stone brick, 10% damaged brick, 5% wall relief).")]
        public List<BlockVariant> WallVariants = new();

        [Header("Liquid & Hazard Block (Future Water / Slime Pools)")]
        public BlockDefinition LiquidBlock;

        #region Public Methods

        /// <summary>
        /// Selects a floor block using procedural roll against the configured wear variants.
        /// </summary>
        public BlockDefinition PickFloorBlock(System.Random rng)
        {
            return PickFromVariants(FloorVariants, DefaultFloor, rng);
        }

        /// <summary>
        /// Selects a wall block using procedural roll against the configured wear variants.
        /// </summary>
        public BlockDefinition PickWallBlock(System.Random rng)
        {
            return PickFromVariants(WallVariants, DefaultWall, rng);
        }

        private static BlockDefinition PickFromVariants(List<BlockVariant> variants, BlockDefinition fallback, System.Random rng)
        {
            if (variants == null || variants.Count == 0 || rng == null)
                return fallback;

            int totalWeight = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].Block != null)
                {
                    totalWeight += Mathf.Max(1, variants[i].Weight);
                }
            }

            if (totalWeight <= 0) return fallback;

            int roll = rng.Next(totalWeight);
            int runningSum = 0;

            for (int i = 0; i < variants.Count; i++)
            {
                if (variants[i].Block == null) continue;

                runningSum += Mathf.Max(1, variants[i].Weight);
                if (roll < runningSum)
                {
                    return variants[i].Block;
                }
            }

            return fallback;
        }

        #endregion
    }
}
