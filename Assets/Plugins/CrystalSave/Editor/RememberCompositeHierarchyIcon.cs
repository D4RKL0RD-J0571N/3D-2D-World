#if UNITY_EDITOR && !UNITY_6000_6_OR_NEWER && ARAWN_REMEMBERME && MEMORYPACK
using Arawn.CrystalSave.Runtime;
using UnityEngine;
using UnityEditor;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>
    /// Adds a custom icon to the Hierarchy window for GameObjects with a RememberComposite component.
    [InitializeOnLoad]
    static class RememberCompositeHierarchyIcon
    {
        // Path must match whatever you passed to [Icon(...)] in RememberComposite.
        private const string k_IconPath = "Assets/Plugins/CrystalSave/Editor/Gizmos/RememberComposite.png";

        // Which “slot” from the far-right this icon should occupy:
        //   slot 0 → flush right  (xMax - iconSize - padding)
        //   slot 1 → one icon-width + padding to the left of that, etc.
        private const int k_SlotIndex = 1;

#pragma warning disable UDR0001 // Editor-only hierarchy icon asset reference; no scene object or runtime callback state is retained.
        private static readonly Texture2D s_Icon;
#pragma warning restore UDR0001

        static RememberCompositeHierarchyIcon()
        {
            // Load the icon once when the Editor loads
            s_Icon = AssetDatabase.LoadAssetAtPath<Texture2D>(k_IconPath);
            RegisterCallbacks();
        }

        private static void RegisterCallbacks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= UnregisterCallbacks;
#pragma warning disable UDR0001 // Editor-only hierarchy callback is unregister-before-register and removed on assembly reload.
            AssemblyReloadEvents.beforeAssemblyReload += UnregisterCallbacks;
            #if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyGUIByEntityId;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUIByEntityId;
            #else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            #endif
#pragma warning restore UDR0001
        }

        private static void UnregisterCallbacks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= UnregisterCallbacks;
            #if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= OnHierarchyGUIByEntityId;
            #else
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyGUI;
            #endif
        }

        #if UNITY_6000_4_OR_NEWER
        private static void OnHierarchyGUIByEntityId(UnityEngine.EntityId entityId, Rect selectionRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.Repaint)
                return;

            if (!CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons)
                return;

            if (s_Icon == null)
                return;

            GameObject go = EditorUtility.EntityIdToObject(entityId) as GameObject;
            if (go == null)
                return;

            RememberComposite rememberComp = go.GetComponent<RememberComposite>();
            if (rememberComp == null)
                return;

            float iconSize = 16f;
            float rightMargin = 0f;

            Rect iconRect = new Rect(
                selectionRect.xMax - iconSize - rightMargin,
                selectionRect.y,
                iconSize,
                iconSize
            );

            GUI.DrawTexture(iconRect, s_Icon);
        }
        #endif

        #if !UNITY_6000_4_OR_NEWER
        private static void OnHierarchyGUI(int instanceID, Rect selectionRect)
        {
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.Repaint)
                return;

            if (!CrystalSaveEditorAutomationSettings.ShowEditorHierarchyIcons)
                return;

            if (s_Icon == null)
                return;

            // Convert instanceID → GameObject
            #if UNITY_6000_3_OR_NEWER
            GameObject go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            #else
            GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            #endif
            if (go == null)
                return;

            // If it has a RememberComposite, draw the icon in our chosen slot
            if (go.GetComponent<RememberComposite>() != null)
            {
                const float iconSize = 16f;
                const float padding = 2f; // gap between icon slots

                // Compute X position based on slot index:
                //   x = xMax - (iconSize + padding) * (slotIndex + 1) + padding
                float xPos = selectionRect.xMax
                           - (iconSize + padding) * (k_SlotIndex + 1)
                           + padding;

                var iconRect = new Rect(
                    xPos,
                    selectionRect.yMin + (selectionRect.height - iconSize) * 0.5f,
                    iconSize,
                    iconSize
                );

                GUI.DrawTexture(iconRect, s_Icon);
            }
        }
        #endif
    }
}
#endif
