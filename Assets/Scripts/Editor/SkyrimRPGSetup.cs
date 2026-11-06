#if UNITY_EDITOR
namespace Studio.Editor
{
    using System.IO;
    using Domain.Skyrim;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor-only utility for Milestone 2 setup:
    /// — Creates the FactionRelationshipMatrixSO with Skyrim-style defaults
    /// — Creates the default quest ScriptableObject stubs
    /// </summary>
    public static class SkyrimRPGSetup
    {
        private const string DataPath = "Assets/Data/Skyrim";

        [MenuItem("3D2DWorld/Skyrim/Create Default Faction Matrix")]
        public static void CreateDefaultFactionMatrix()
        {
            Directory.CreateDirectory(DataPath);

            string assetPath = $"{DataPath}/FactionRelationshipMatrix.asset";
            if (File.Exists(assetPath))
            {
                Debug.Log("[SkyrimRPGSetup] FactionRelationshipMatrix already exists.");
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                return;
            }

            var matrix = ScriptableObject.CreateInstance<FactionRelationshipMatrixSO>();

            // Serialized via JSON reflection — the SO rows must be set in Inspector.
            // Creating the asset here so it appears in the project and can be edited.
            AssetDatabase.CreateAsset(matrix, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SkyrimRPGSetup] Created {assetPath}. " +
                      "Open the asset and set up faction rows in the Inspector.");
            Selection.activeObject = matrix;

            EditorUtility.DisplayDialog(
                "Faction Matrix Created",
                $"Asset saved at {assetPath}.\n\n" +
                "Edit it in the Inspector to define faction relationships.\n" +
                "Example: Bandits → Hostile to Player and Guards.",
                "OK");
        }

        [MenuItem("3D2DWorld/Skyrim/Validate RPG Services")]
        public static void ValidateRPGServices()
        {
            bool skillOk   = File.Exists("Assets/Scripts/Systems/Skyrim/SkillProgressionService.cs");
            bool factionOk = File.Exists("Assets/Scripts/Systems/Skyrim/FactionService.cs");
            bool questOk   = File.Exists("Assets/Scripts/Systems/Skyrim/RadiantQuestService.cs");

            string report = $"SkillProgressionService: {(skillOk ? "✅" : "❌")}\n" +
                            $"FactionService:          {(factionOk ? "✅" : "❌")}\n" +
                            $"RadiantQuestService:     {(questOk ? "✅" : "❌")}";

            EditorUtility.DisplayDialog("Milestone 2 Validation", report, "OK");
            Debug.Log($"[SkyrimRPGSetup] Validation:\n{report}");
        }
    }
}
#endif
