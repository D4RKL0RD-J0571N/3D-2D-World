#if UNITY_EDITOR && UNITY_6000_6_OR_NEWER && ARAWN_REMEMBERME && MEMORYPACK
using System.Collections.Generic;
using Arawn.CrystalSave.Runtime;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>Unity 6.6's UI Toolkit hierarchy does not draw the legacy IMGUI overlays.</summary>
    [InitializeOnLoad]
    internal static class CrystalSaveHierarchyIcons
    {
        internal const string ContainerName = "crystal-save-hierarchy-icons";
        private static readonly Dictionary<VisualElement, EntityId> Rows = new();
        private static readonly Dictionary<string, Texture2D> Textures = new();
        private static readonly HashSet<string> LinkedIDs = new();
        private static bool dirty = true;
        private static bool wasEnabled;

        static CrystalSaveHierarchyIcons()
        {
            HierarchyWindow.BindViewItem += Bind;
            HierarchyWindow.UnbindViewItem += Unbind;
            EditorApplication.hierarchyChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += Invalidate;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Refresh;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void Bind(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            var container = item.RightCustomContainer;
            if (item.Handler is HierarchyGameObjectHandler handler)
            {
                var id = handler.GetEntityId(item.Node);
                Rows[container] = id;
                if (dirty) Refresh();
                UpdateIcons(container, EditorUtility.EntityIdToObject(id) as GameObject,
                    CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons, LinkedIDs);
            }
            else
            {
                Rows.Remove(container);
                UpdateIcons(container, null, false, LinkedIDs);
            }
        }

        private static void Unbind(HierarchyWindow window, HierarchyView view, HierarchyViewItem item)
        {
            Rows.Remove(item.RightCustomContainer);
            // Rows are recycled. Remove only our elements, leaving other extensions intact.
            item.RightCustomContainer.Q(ContainerName)?.RemoveFromHierarchy();
        }

        internal static void UpdateIcons(VisualElement container, GameObject go, bool enabled,
            ISet<string> linkedIDs)
        {
            container.Q(ContainerName)?.RemoveFromHierarchy();
            if (!enabled || !go) return;

            var icons = new VisualElement { name = ContainerName, pickingMode = PickingMode.Ignore };
            icons.style.flexDirection = FlexDirection.Row;
            if (go.GetComponent<RememberGameObject>() && go.TryGetComponent<UniqueID>(out var uid)
                && !string.IsNullOrEmpty(uid.ID) && linkedIDs.Contains(uid.ID))
                AddIcon(icons, "LinkedUniqueID", "Linked scene object");
            if (go.GetComponent<SaveablePrefab>())
                AddIcon(icons, "SaveablePrefab", "Saveable Prefab");
            if (go.GetComponent<RememberComposite>())
                AddIcon(icons, "RememberComposite", "Remember Composite");
            if (icons.childCount > 0) container.Add(icons);
        }

        private static void AddIcon(VisualElement container, string name, string tooltip)
        {
            if (!Textures.TryGetValue(name, out var texture) || !texture)
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Assets/Plugins/CrystalSave/Editor/Gizmos/{name}.png");
                Textures[name] = texture;
            }
            if (!texture) return;
            var icon = new Image { name = name, image = texture, tooltip = tooltip,
                pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 16;
            icon.style.height = 16;
            icon.style.marginLeft = 2;
            icon.style.flexShrink = 0;
            container.Add(icon);
        }

        private static void Invalidate() => dirty = true;
        private static void OnPlayModeChanged(PlayModeStateChange state) => Invalidate();

        private static void Refresh()
        {
            bool enabled = CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons;
            if (!dirty && enabled == wasEnabled) return;
            dirty = false;
            wasEnabled = enabled;
            LinkedIDs.Clear();
            if (enabled)
            {
                var registry = AssetProvider.Load<SceneObjectRegistry>("SceneObjectRegistry");
                if (registry != null)
                    foreach (var entry in registry.Entries)
                        if (!string.IsNullOrEmpty(entry.UniqueID)) LinkedIDs.Add(entry.UniqueID);
            }
            foreach (var row in Rows)
                UpdateIcons(row.Key, EditorUtility.EntityIdToObject(row.Value) as GameObject,
                    enabled, LinkedIDs);
        }

        private static void Dispose()
        {
            HierarchyWindow.BindViewItem -= Bind;
            HierarchyWindow.UnbindViewItem -= Unbind;
            EditorApplication.hierarchyChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            Undo.undoRedoPerformed -= Invalidate;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Refresh;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            foreach (var row in Rows) row.Key.Q(ContainerName)?.RemoveFromHierarchy();
            Rows.Clear();
            Textures.Clear();
            LinkedIDs.Clear();
        }
    }
}
#endif
