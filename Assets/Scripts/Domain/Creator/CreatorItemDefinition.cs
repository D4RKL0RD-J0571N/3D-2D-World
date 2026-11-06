namespace Domain.Creator
{
    using System;
    using Core;
    using Core.Content;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Category for spawnable items in the In-Game Creator Studio.
    /// </summary>
    public enum CreatorCategory
    {
        Weapons = 0,
        Combatants = 1,
        Structures = 2,
        Props = 3,
        Items = 4
    }

    /// <summary>
    /// Definition of an entity, prop, or block available in the In-Game Creator Studio palette.
    /// Implements <see cref="IContentDefinition"/> for unified lookup in <see cref="ContentRegistry{T}"/>.
    /// </summary>
    [Serializable]
    public class CreatorItemDefinition : IContentDefinition
    {
        public string id;
        public string displayName;
        public CreatorCategory category;
        public string description;
        public GameObject prefab;
        public Sprite icon;
        public Vector3 placementOffset = Vector3.zero;
        public Vector3 defaultScale = Vector3.one;
        public float gridSnapSize = 1.0f;

        [SerializeField]
        private ResourceId _contentId;

        [SerializeField]
        private string _sourceModId = ResourceId.DefaultNamespace;

        public ResourceId ContentId
        {
            get
            {
                if (!_contentId.IsEmpty)
                {
                    return _contentId;
                }

                return ResourceId.FromLegacyId(_sourceModId, "placeable", !string.IsNullOrWhiteSpace(id) ? id : displayName);
            }
            set => _contentId = value;
        }

        public string SourceModId
        {
            get => string.IsNullOrWhiteSpace(_sourceModId) ? ResourceId.DefaultNamespace : _sourceModId;
            set => _sourceModId = value;
        }

        string IIdentifiable.Id => id;

        string IContentDefinition.DisplayName => displayName;
    }
}
