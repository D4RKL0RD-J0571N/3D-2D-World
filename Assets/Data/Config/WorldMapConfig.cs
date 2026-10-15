namespace Data.Config
{
    using UnityEngine;

    public enum MapEntityType
    {
        Player,
        Enemy,
        Npc,
        Interactable,
        Custom
    }

    [CreateAssetMenu(fileName = "WorldMapConfig", menuName = "3D2DWorld/World Map Config")]
    public class WorldMapConfig : ScriptableObject
    {
        [Header("Rendering")]
        [Tooltip("Pixels per world unit on the map texture.")]
        public int PixelsPerUnit = 2;

        [Tooltip("Color used for floor/walkable areas.")]
        public Color FloorColor = new(0.35f, 0.35f, 0.38f, 1f);

        [Tooltip("Color used for walls and obstacles.")]
        public Color WallColor = new(0.18f, 0.18f, 0.20f, 1f);

        [Tooltip("Color used for unexplored areas.")]
        public Color UnexploredColor = new(0.05f, 0.05f, 0.06f, 0.95f);

        [Tooltip("Color used for corridor connections.")]
        public Color CorridorColor = new(0.28f, 0.28f, 0.30f, 1f);

        [Header("Entity Icons")]
        public Sprite PlayerIcon;
        public Sprite EnemyIcon;
        public Sprite NpcIcon;
        public Sprite InteractableIcon;
        public Sprite CustomIcon;

        [Header("Minimap")]
        public int MinimapSize = 200;
        public Vector2 MinimapAnchor = new(1f, 1f); // 0,0 bottom-left ; 1,1 top-right
        public Vector2 MinimapOffset = new(-20f, -20f);
        public float MinimapZoom = 0.5f;

        [Header("World Map")]
        public float WorldMapDefaultZoom = 1f;
        public float WorldMapMinZoom = 0.25f;
        public float WorldMapMaxZoom = 4f;

        [Header("Input")]
        public KeyCode ToggleWorldMapKey = KeyCode.M;
        public KeyCode ToggleMinimapKey = KeyCode.Tab;

        [Header("Performance")]
        [Tooltip("Update entity icon positions every N frames. 1 = every frame.")]
        public int EntityUpdateInterval = 2;

        [Tooltip("Reveal neighboring rooms up to this Manhattan distance on entry.")]
        public int RevealNeighborDistance = 1;

        public Sprite GetIconForType(MapEntityType type)
        {
            return type switch
            {
                MapEntityType.Player => PlayerIcon,
                MapEntityType.Enemy => EnemyIcon,
                MapEntityType.Npc => NpcIcon,
                MapEntityType.Interactable => InteractableIcon,
                _ => CustomIcon,
            };
        }
    }
}
