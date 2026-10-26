namespace Domain.Creator
{
    using System;
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
    /// </summary>
    [Serializable]
    public class CreatorItemDefinition
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
    }
}
