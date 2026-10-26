#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using Arawn.CrystalSave.Runtime;
using UnityEditor;
using UnityEngine;

namespace Arawn.CrystalSave.Editor
{
    /// <summary>Small authoring layer: runtime types own policy; this layer owns Undo and helper creation.</summary>
    internal static class CrystalSavePersistenceSetup
    {
        private static readonly string[] ComponentModes = { "Apply setup…", "Scene object", "Persistent object", "Home scene", "Shared component state", "Deferred scene object" };
        private static readonly string[] PrefabModes = { "Apply setup…", "Scene prefab", "Persistent prefab", "Home scene", "Scene-baked state handoff", "Deferred prefab" };

        internal static void Draw(Component target)
        {
            EditorGUILayout.HelpBox(Describe(target), MessageType.None);
            bool canConfigure = CanConfigure(target);
            using (new EditorGUI.DisabledScope(!canConfigure || Application.isPlaying))
            {
                if (GUILayout.Button("Apply persistence setup…", EditorStyles.miniButton))
                {
                    var menu = new GenericMenu();
                    var modes = target is SaveablePrefab ? PrefabModes : ComponentModes;
                    for (int index = 1; index < modes.Length; index++)
                    {
                        int mode = index;
                        var label = new GUIContent(modes[index]);
                        if (mode == 2 && target.transform.parent != null) menu.AddDisabledItem(label);
                        else menu.AddItem(label, false, () => { if (target) Apply(target, mode); });
                    }
                    menu.ShowAsContext();
                }
            }
        }

        private static bool CanConfigure(Component target)
        {
            if (target is SaveablePrefab) return true;
            if (target.GetComponentInParent<SaveablePrefab>(true)) return false;
            var helpers = target.GetComponents<SaveableComponent>();
            SaveableComponent designated = helpers.Length > 0 ? helpers[0] : null;
            foreach (var helper in helpers)
                if (helper.isActiveAndEnabled) { designated = helper; break; }
            return designated == target;
        }

        internal static string Describe(Component target)
        {
            if (target is SaveablePrefab prefab)
            {
                string mode = prefab.KeepAcrossScenes ? "Same live instance across scenes" :
                    prefab.RememberHomeScene ? "Restore in its home scene" :
                    prefab.EnableCrossSceneStateHandoff ? "Transfer to a matching scene-baked prefab" : "Scene prefab";
                return mode + ". " + (prefab.DeferLowPriorityUntilRequested ? "Restore when requested." : "Restore during loading.") +
                    (prefab.RegisterWithSaveSystem ? "" : " Saving is disabled.");
            }
            var component = (SaveableComponent)target;
            var owner = component.GetComponentInParent<SaveablePrefab>(true);
            string route = owner && owner.TrackComponentBlobs ? "Saved with prefab '" + owner.name + "'." : "Saved as an individual component.";
            string scene = component.KeepAcrossScenes ? "Same live object across scenes." :
                component.RememberHomeScene ? (component.ShareStateAcrossScenes ? "Home scene with shared-state fallback." : "Restore in its home scene.") :
                component.ShareStateAcrossScenes ? "Share state with matching scene copies." : "Scene component.";
            if (owner) scene = Describe(owner);
            return route + " " + scene + (component.DeferLowPriorityUntilRequested ? " Restore when requested." : " Restore during loading.");
        }

        internal static void Apply(Component target, int mode)
        {
            if (mode < 1 || mode > 5 || !CanConfigure(target)) return;
            if (mode == 2 && target.transform.parent != null) return;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Crystal Save persistence setup");
            Undo.RecordObject(target, "Apply Crystal Save persistence setup");
            if (target is SaveablePrefab prefab)
            {
                prefab.KeepAcrossScenes = false; prefab.RememberHomeScene = false; prefab.EnableCrossSceneStateHandoff = false;
                prefab.DeferLowPriorityUntilRequested = mode == 5;
                if (mode == 2) prefab.KeepAcrossScenes = true;
                if (mode == 3) prefab.RememberHomeScene = true;
                if (mode == 4) { prefab.EnableCrossSceneStateHandoff = true; prefab.HandoffToSceneBakedPrefabs = true; }
                prefab.ValidatePersistenceSettings();
            }
            else if (target is SaveableComponent component)
            {
                component.KeepAcrossScenes = false; component.RememberHomeScene = false; component.ShareStateAcrossScenes = false;
                component.DeferLowPriorityUntilRequested = mode == 5;
                if (mode == 2) component.KeepAcrossScenes = true;
                if (mode == 3) component.RememberHomeScene = true;
                if (mode == 4) component.ShareStateAcrossScenes = true;
                component.ValidatePersistenceSettings();
            }
            var visibility = target.GetComponent<PersistentVisibilityController>();
            if (mode == 2 && !visibility) visibility = Undo.AddComponent<PersistentVisibilityController>(target.gameObject);
            if (mode != 2 && visibility) Undo.DestroyObjectImmediate(visibility);
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
        }
    }
}
#endif
