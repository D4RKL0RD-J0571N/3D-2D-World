namespace Domain.Grid
{
    using Data.Config;
    using System.Collections.Generic;
    using UnityEngine;

    public enum BlockRenderType
    {
        SolidMesh,      // Gets baked by Greedy Meshing (walls, floors)
        TransparentMesh, // Baked, but separate pass (glass, water)
        Prefab          // Doesn't get meshed; spawns a GameObject (furniture, chests)
    }

    public enum SurfaceType
    {
        Stone,
        Wood,
        Metal,
        Dirt,
        Water,
        Slime
    }

    public enum BlockRole
    {
        Floor,
        Wall,
        Ceiling,
        Pillar,
        Accent,
        Liquid
    }

    [CreateAssetMenu(fileName = "Block", menuName = "3D2DWorld/Entities/Grid/Block Definition")]
    public sealed class BlockDefinition : EntityData
    {
        [Header("Identity")]
        public ushort RuntimeId;
        [Header("Rendering")]
        public BlockRenderType RenderType;

        [Tooltip("Material used for meshing. Ignored if RenderType is Prefab.")]
        public Material BlockMaterial;

        [Tooltip("Prefab spawned at this grid cell. Ignored if RenderType is SolidMesh.")]
        public GameObject Prefab;

        [Header("Properties")]
        public bool IsSolid = true;
        public bool IsLiquid = false;
        public SurfaceType Surface = SurfaceType.Stone;

        [Header("Roles")]
        public List<BlockRole> Roles = new();

        // This allows the EntityData system to categorize these automatically
        public override string CategoryPrefix => "BLCK";

#if UNITY_EDITOR

        protected override void OnIdAssigned()
        {
            SynchronizeRuntimeId();
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(Id))
                SynchronizeRuntimeId();
        }

        private void SynchronizeRuntimeId()
        {
            int underscoreIndex = Id.LastIndexOf('_');

            if (underscoreIndex < 0)
            {
                RuntimeId = 0;
                return;
            }

            if (!ushort.TryParse(
                    Id[(underscoreIndex + 1)..],
                    out ushort runtimeId))
            {
                RuntimeId = 0;
                return;
            }

            RuntimeId = runtimeId;
        }

#endif
    }
}
