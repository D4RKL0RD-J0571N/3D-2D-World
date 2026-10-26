using UnityEngine;
using UnityEditor;

namespace SoftKitty
{
    public class CustomHierarchyMenu
    {
        [MenuItem("GameObject/Soft Kitty/Create Entity Instance", false, 10)]
        static void CreateEntityInstance(MenuCommand menuCommand)
        {
            GameObject go = new GameObject("New Entity");
            go.AddComponent<EntityComponent>();
            Undo.RegisterCreatedObjectUndo(go, "Create Entity Instance");
            Selection.activeObject = go;  
        }

        [MenuItem("GameObject/Soft Kitty/Create Entity Instance", true)]
        static bool ValidateCreateEntityInstance()
        {
             
            return Selection.activeGameObject == null;
        }


        [MenuItem("GameObject/Soft Kitty/Attach Entity Instance", false, 10)]
        static void AttachEntityInstance(MenuCommand menuCommand)
        {
            EntityComponent go= Selection.activeGameObject.AddComponent<EntityComponent>();
            Undo.RegisterCreatedObjectUndo(go, "Attach Entity Instance");
        }

        [MenuItem("GameObject/Soft Kitty/Attach Entity Instance", true)]
        static bool ValidateAttachEntityInstance()
        {

            return Selection.activeGameObject != null;
        }
    }
}
