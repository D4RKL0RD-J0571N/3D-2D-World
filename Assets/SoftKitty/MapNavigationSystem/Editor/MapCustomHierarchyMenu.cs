using UnityEngine;
using UnityEditor;

namespace SoftKitty.MasterNavigationMap
{
    public class MapCustomHierarchyMenu
    {
        [MenuItem("GameObject/Soft Kitty/Create Map Generator", false, 10)]
        static void CreateMapGenerator(MenuCommand menuCommand)
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript MMN_Settings_Inspector");
            string _packRootPath = "Assets/SoftKitty/MapNavigationSystem/";
            foreach (var obj in guids)
            {
                string _path = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (_path.Contains("SoftKitty"))
                {
                    _packRootPath = _path.Replace("Editor/MMN_Settings_Inspector.cs", "");
                    break;
                }
            }

            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_packRootPath + "Prefabs/MapGenerator.prefab");
            GameObject _tool = GameObject.Instantiate(_prefab) as GameObject;
            _tool.name = "Map Generator (MMN Tool)";
            Undo.RegisterCreatedObjectUndo(_prefab, "Create Map Generator");
            Selection.activeObject = _prefab;  
        }

        [MenuItem("GameObject/Soft Kitty/Create Map Generator", true)]
        static bool ValidateCreateMapGenerator()
        {
             
            return Selection.activeGameObject == null;
        }


    }
}
