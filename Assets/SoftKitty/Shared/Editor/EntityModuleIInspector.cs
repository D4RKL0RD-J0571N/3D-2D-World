using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace SoftKitty
{
    public interface IEntityInspectorModule
    {
        bool DrawInspector(Entity entity, EntityManagerObject _myTarget);
        void DrawRuntimeInspector(EntityComponent myTarget);
    }

    public static class EntityInspectorRegistry
    {
        public static List<IEntityInspectorModule> Modules = new List<IEntityInspectorModule>();

        public static void Register(IEntityInspectorModule module)
        {
            if (!Modules.Any(m => m.GetType() == module.GetType()))
            {
                Modules.Add(module);
            }
        }
    }


}
