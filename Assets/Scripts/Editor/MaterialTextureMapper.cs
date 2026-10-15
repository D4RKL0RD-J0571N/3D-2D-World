using UnityEditor;
using UnityEngine;
using System.IO;

public static class MaterialTextureMapper
{
    [MenuItem("Tools/Antigravity/Map Material Textures")]
    public static void MapTextures()
    {
        string materialFolder = "Assets/Materials/Generated";
        string spriteFolder = "Assets/Sprites";

        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { materialFolder });
        int updatedCount = 0;

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string matName = mat.name.Replace("_Material", "");

            // Find Base Map
            string baseGuid = FindTextureGuid(spriteFolder, matName);
            if (!string.IsNullOrEmpty(baseGuid))
            {
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(baseGuid)));
                mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(baseGuid)));
            }

            // Find Normal Map
            string normalGuid = FindTextureGuid(spriteFolder, matName + "_NORMAL_MAP");
            if (!string.IsNullOrEmpty(normalGuid))
            {
                Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(normalGuid));
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Find Height Map
            string heightGuid = FindTextureGuid(spriteFolder, matName + "_HEIGHT_MAP");
            if (!string.IsNullOrEmpty(heightGuid))
            {
                Texture2D heightTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(heightGuid));
                mat.SetTexture("_ParallaxMap", heightTex);
                mat.EnableKeyword("_PARALLAXMAP");
            }

            // Set Specular workflow, smoothness, and height values based on user request
            mat.SetFloat("_WorkflowMode", 0f); // 0 = Specular, 1 = Metallic in URP Lit
            mat.EnableKeyword("_SPECULAR_SETUP"); // Ensure specular keyword is active
            
            mat.SetFloat("_Smoothness", 0.32f);
            mat.SetFloat("_Parallax", 0.02f);

            EditorUtility.SetDirty(mat);
            updatedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MaterialTextureMapper] Successfully mapped textures for {updatedCount} materials.");
    }

    private static string FindTextureGuid(string searchFolder, string textureName)
    {
        // Search for textures with exact name
        string[] guids = AssetDatabase.FindAssets(textureName + " t:Texture2D", new[] { searchFolder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Equals(textureName, System.StringComparison.OrdinalIgnoreCase))
            {
                return guid;
            }
        }
        return null;
    }
}
