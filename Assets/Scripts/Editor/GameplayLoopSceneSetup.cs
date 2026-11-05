#if UNITY_EDITOR
namespace Studio.Editor
{
    using Gameplay.Combat;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Editor utility to configure and persist the Proving Grounds gameplay loop components
    /// on scene GameObjects (Draugr Boss combat AI, Blacksmith Anvil crafting station).
    /// </summary>
    public static class GameplayLoopSceneSetup
    {
        [MenuItem("3D2DWorld/Setup Proving Grounds Gameplay Loop", false, 100)]
        public static void SetupGameplayLoop()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != "RPGProvingGrounds")
            {
                scene = EditorSceneManager.OpenScene("Assets/Scenes/RPGProvingGrounds.unity");
            }

            SetupDraugrBoss();
            SetupBlacksmithAnvil();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=green>✦ Proving Grounds Gameplay Loop configured and scene saved successfully!</color>");
        }

        private static void SetupDraugrBoss()
        {
            GameObject draugr = GameObject.Find("Draugr_Wight_Boss");
            if (draugr == null) return;

            if (!draugr.TryGetComponent<AttributeComponent>(out var attr))
            {
                attr = draugr.AddComponent<AttributeComponent>();
            }

            if (!draugr.TryGetComponent<EnemyCombatAI>(out var ai))
            {
                ai = draugr.AddComponent<EnemyCombatAI>();
            }

            EditorUtility.SetDirty(draugr);
            Debug.Log("[GameplayLoopSceneSetup] Draugr_Wight_Boss wired with AttributeComponent and EnemyCombatAI.");
        }

        private static void SetupBlacksmithAnvil()
        {
            GameObject anvil = GameObject.Find("Prop_Blacksmith_Anvil");
            if (anvil == null) return;

            if (!anvil.TryGetComponent<CraftingStationInteractable>(out var station))
            {
                station = anvil.AddComponent<CraftingStationInteractable>();
            }

            EditorUtility.SetDirty(anvil);
            Debug.Log("[GameplayLoopSceneSetup] Prop_Blacksmith_Anvil wired with CraftingStationInteractable.");
        }
    }
}
#endif
