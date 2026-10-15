namespace Domain.LevelGeneration
{
    using System;
    using System.Collections.Generic;
    using Domain.Grid;
    using UnityEngine;

    public enum RoomElevationMode
    {
        StandardFlat,      // Standard flat floor
        SunkenPit,         // Perimeter walkway with stairs down to sunken center
        RaisedDais,        // Central raised platform with stairs leading up
        StaircaseConnector // Vertical transit room connecting different floor levels
    }

    [CreateAssetMenu(
        fileName = "RoomArchetype",
        menuName = "3D2DWorld/Level Generation/Room Archetype"
    )]
    public class RoomArchetype : ScriptableObject
    {
        [Header("Identity")]
        public string ArchetypeId = "StandardHall";
        public string DisplayName = "Standard Hall";

        [Header("Elevation Profile")]
        public RoomElevationMode ElevationMode = RoomElevationMode.StandardFlat;
        [Range(1, 5)]
        public int ElevationStepHeight = 2; // Height offset in blocks

        [Header("Blocks (Override)")]
        [Tooltip("If specified, overrides the global palette for this room archetype.")]
        public Domain.Grid.BlockPalette ThemePalette;
        [Tooltip("If specified, overrides the global floor block for this room type.")]
        public BlockDefinition CustomFloorBlock;
        [Tooltip("If specified, overrides the global wall block for this room type.")]
        public BlockDefinition CustomWallBlock;
        [Tooltip("If specified, overrides the global ceiling block for this room type.")]
        public BlockDefinition CustomCeilingBlock;
        [Tooltip("Block used for steps and platform borders.")]
        public BlockDefinition StepBlock;

        [Header("Features & Columns")]
        public bool HasPillars = true;
        [Range(1, 4)]
        public int PillarThickness = 1;
        [Range(2, 6)]
        public int PillarInset = 3;

        [Header("Spawning Weights")]
        [Range(0, 10)]
        public int MaxEnemySpawns = 3;
        [Range(0, 5)]
        public int MaxChestSpawns = 1;
        public bool AllowNpcSpawn = false;
    }
}
