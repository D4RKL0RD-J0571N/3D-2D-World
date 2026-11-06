namespace Systems.WorldMap
{
    using UnityEngine;
    using Core;
    using Data.Config;

    /// <summary>
    /// Marks a world object as trackable on the world map / minimap.
    /// Any GameObject with this component is registered with <see cref="WorldMapService"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("3D2DWorld/World Map/Map Entity")]
    public sealed class MapEntity : MonoBehaviour
    {
        [SerializeField] private MapEntityType _type = MapEntityType.Custom;
        [SerializeField] private Sprite _customIcon;
        [SerializeField] private Color _customColor = Color.white;
        [SerializeField] private bool _showOnMinimap = true;
        [SerializeField] private bool _showOnWorldMap = true;
        [SerializeField] private float _iconSize = 8f;

        private WorldMapService _service;

        public MapEntityType Type => _type;

        public Sprite CustomIcon => _customIcon;

        public Color CustomColor => _customColor;

        public bool ShowOnMinimap => _showOnMinimap;

        public bool ShowOnWorldMap => _showOnWorldMap;

        public float IconSize => _iconSize;

        public Vector3 WorldPosition => transform.position;

        private void OnEnable()
        {
            if (ServiceLocator.TryGet<WorldMapService>(out _service))
            {
                _service.RegisterEntity(this);
            }
        }

        private void OnDisable()
        {
            _service?.UnregisterEntity(this);
            _service = null;
        }
    }
}
