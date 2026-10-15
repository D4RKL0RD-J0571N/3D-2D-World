namespace Domain.LevelGeneration
{
    using Domain.Grid;

    using UnityEngine;

    [CreateAssetMenu(fileName = "LevelRenderConfig", menuName = "3D2DWorld/Level Generation/Level Render Config")]
    public sealed class LevelRenderConfig : ScriptableObject
    {
        [Header("Room Dimensions")]
        public float RoomFloorHeight = 0.1f;
        public float RoomCeilingHeight = 4.1f;

        [Header("Blocks (Grid Data)")]
        public Domain.Grid.BlockRegistry BlockRegistry;

        [Header("Archetypes & Elevation")]
        [Tooltip("Available room archetypes to pick from during procedural assignment.")]
        public System.Collections.Generic.List<RoomArchetype> Archetypes = new();

        [Tooltip("Height in voxel blocks for each floor tier (e.g. difference between Level 0 and Level 1).")]
        public int TierHeightInBlocks = 4;

        [Header("Material Palettes & Themes")]
        [Tooltip("Themed palettes available for generation (Stone Dungeon, Ancient Wood, Industrial Tech, etc.).")]
        public System.Collections.Generic.List<Domain.Grid.BlockPalette> Palettes = new();
    }
}
