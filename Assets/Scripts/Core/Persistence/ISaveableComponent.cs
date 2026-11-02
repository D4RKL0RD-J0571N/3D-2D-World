namespace Core.Persistence
{
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Resolves persistent <see cref="InstanceGuid"/>s to live <see cref="GameObject"/>s during two-pass save restoration
    /// and collects tombstones for content from uninstalled mods.
    /// </summary>
    public interface IWorldInstanceResolver
    {
        bool TryResolveGameObject(InstanceGuid instanceId, out GameObject instance);
        bool TryResolveComponent<T>(InstanceGuid instanceId, out T component) where T : Component;
        void RecordUnknownContentPlaceholder(UnknownContentPlaceholder placeholder);
    }

    /// <summary>
    /// Implemented by any <see cref="MonoBehaviour"/> on an entity, actor, container, or interactable
    /// whose state is persisted inside <see cref="EntitySaveRecord.ComponentStates"/>.
    /// </summary>
    public interface ISaveableComponent
    {
        /// <summary>Unique component key within an <see cref="EntitySaveRecord"/> (e.g., <c>"attributes"</c>, <c>"inventory"</c>, <c>"chest"</c>).</summary>
        string ComponentSaveKey { get; }

        /// <summary>Captures a serializable DTO representing this component's current state.</summary>
        object CaptureComponentState();

        /// <summary>Restores this component's state from its serialized JSON payload.</summary>
        void RestoreComponentState(string serializedJsonState, IWorldInstanceResolver resolver);
    }
}
