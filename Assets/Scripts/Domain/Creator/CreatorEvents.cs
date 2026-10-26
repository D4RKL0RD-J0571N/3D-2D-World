namespace Domain.Creator
{
    using Core;
    using UnityEngine;

    /// <summary>
    /// Fired when creator studio mode changes between PlayMode and BuildMode.
    /// </summary>
    public class CreatorModeChangedEvent : GameEvent
    {
        public bool IsBuildMode;

        public CreatorModeChangedEvent(bool isBuildMode)
        {
            IsBuildMode = isBuildMode;
        }
    }

    /// <summary>
    /// Fired when the active creation item is selected in the palette.
    /// </summary>
    public class CreatorItemSelectedEvent : GameEvent
    {
        public CreatorItemDefinition Item;

        public CreatorItemSelectedEvent(CreatorItemDefinition item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// Fired when an object is placed in the world.
    /// </summary>
    public class CreatorObjectPlacedEvent : GameEvent
    {
        public string ItemId;
        public Vector3 Position;
        public Quaternion Rotation;

        public CreatorObjectPlacedEvent(string itemId, Vector3 position, Quaternion rotation)
        {
            ItemId = itemId;
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// Fired when an object is deleted or demolished.
    /// </summary>
    public class CreatorObjectRemovedEvent : GameEvent
    {
        public GameObject RemovedObject;

        public CreatorObjectRemovedEvent(GameObject removedObject)
        {
            RemovedObject = removedObject;
        }
    }
}
