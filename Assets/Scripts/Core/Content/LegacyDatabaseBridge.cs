namespace Core.Content
{
    using System.Collections.Generic;
    using Core.Assets;
    using Core.Ids;
    using Data.Config;
    using Domain.Crafting;
    using Domain.Creator;
    using Domain.Entities;
    using Domain.Grid;
    using UnityEngine;

    /// <summary>
    /// Transitional bridge that imports existing ScriptableObject registries (<see cref="EntityRegistry"/>,
    /// <see cref="BlockRegistry"/>, <see cref="CreatorCatalogSO"/>) and standalone <see cref="EntityData"/> / <see cref="CraftingRecipe"/>
    /// assets into the unified <see cref="IContentService"/> (<see cref="ContentRegistry{T}"/>) with both canonical
    /// <see cref="ResourceId"/> keys and legacy string ID aliases (<c>ACT_0001</c>, <c>ITM_0001</c>, <c>BLCK_0010</c>).
    /// </summary>
    public static class LegacyDatabaseBridge
    {
        public const string BlockRuntimeIdIndex = "RuntimeId";

        #region Individual & Batch Importers

        /// <summary>
        /// Imports a single <see cref="EntityData"/> instance into both the base <see cref="ContentRegistry{EntityData}"/>
        /// and its concrete subtype registry (<see cref="ItemData"/>, <see cref="ActorData"/>, <see cref="ContainerData"/>, or <see cref="BlockDefinition"/>).
        /// </summary>
        public static void ImportEntity(IContentService contentService, EntityData entity, string modId = ResourceId.DefaultNamespace)
        {
            if (contentService == null || entity == null)
            {
                return;
            }

            string alias = !string.IsNullOrWhiteSpace(entity.Id) ? entity.Id : entity.name;
            contentService.GetRegistry<EntityData>().Register(entity, modId, ContentRegistrationPolicy.OverrideExisting, alias);

            ServiceLocator.TryGet<IAssetProvider>(out var assets);

            switch (entity)
            {
                case ItemData item:
                    contentService.GetRegistry<ItemData>().Register(item, modId, ContentRegistrationPolicy.OverrideExisting, alias);
                    if (assets != null && item.ContentId.IsValid && item.Icon != null)
                    {
                        assets.RegisterBuiltInAsset(new ResourceId(item.ContentId.Namespace, $"icon/{item.ContentId.Path}"), item.Icon);
                    }
                    break;
                case ActorData actor:
                    contentService.GetRegistry<ActorData>().Register(actor, modId, ContentRegistrationPolicy.OverrideExisting, alias);
                    break;
                case ContainerData container:
                    contentService.GetRegistry<ContainerData>().Register(container, modId, ContentRegistrationPolicy.OverrideExisting, alias);
                    break;
                case BlockDefinition block:
                    EnsureBlockSecondaryIndex(contentService);
                    contentService.GetRegistry<BlockDefinition>().Register(block, modId, ContentRegistrationPolicy.OverrideExisting, alias);
                    break;
            }
        }

        /// <summary>Imports a collection of <see cref="EntityData"/> definitions into <paramref name="contentService"/>.</summary>
        public static int ImportEntities(IContentService contentService, IEnumerable<EntityData> entities, string modId = ResourceId.DefaultNamespace)
        {
            if (contentService == null || entities == null)
            {
                return 0;
            }

            int count = 0;
            foreach (var entity in entities)
            {
                if (entity == null) continue;
                ImportEntity(contentService, entity, modId);
                count++;
            }

            return count;
        }

        /// <summary>Imports all entries from a legacy <see cref="EntityRegistry"/> ScriptableObject.</summary>
        public static int ImportEntityRegistry(IContentService contentService, EntityRegistry registry, string modId = ResourceId.DefaultNamespace)
        {
            if (contentService == null || registry == null || registry.Entities == null)
            {
                return 0;
            }

            return ImportEntities(contentService, registry.Entities, modId);
        }

        /// <summary>Imports all entries from a <see cref="BlockRegistry"/> and configures O(1) <c>ushort RuntimeId</c> indexing.</summary>
        public static int ImportBlockRegistry(IContentService contentService, BlockRegistry registry, string modId = ResourceId.DefaultNamespace)
        {
            if (contentService == null || registry == null)
            {
                return 0;
            }

            EnsureBlockSecondaryIndex(contentService);
            var blocks = registry.GetAllBlocks();
            return ImportEntities(contentService, blocks, modId);
        }

        /// <summary>Imports all placeable items from a <see cref="CreatorCatalogSO"/> into <see cref="ContentRegistry{CreatorItemDefinition}"/>.</summary>
        public static int ImportCreatorCatalog(IContentService contentService, CreatorCatalogSO catalog, string modId = ResourceId.DefaultNamespace)
        {
            if (contentService == null || catalog == null || catalog.Items == null)
            {
                return 0;
            }

            var targetRegistry = contentService.GetRegistry<CreatorItemDefinition>();
            int count = 0;

            foreach (var item in catalog.Items)
            {
                if (item == null) continue;
                targetRegistry.Register(item, modId, ContentRegistrationPolicy.OverrideExisting, item.id);
                count++;
            }

            return count;
        }

        /// <summary>Imports a collection of <see cref="CraftingRecipe"/> definitions into <see cref="ContentRegistry{CraftingRecipe}"/>.</summary>
        public static int ImportCraftingRecipes(IContentService contentService, IEnumerable<CraftingRecipe> recipes, string modId = ResourceId.DefaultNamespace)
        {
            if (contentService == null || recipes == null)
            {
                return 0;
            }

            var targetRegistry = contentService.GetRegistry<CraftingRecipe>();
            int count = 0;

            foreach (var recipe in recipes)
            {
                if (recipe == null) continue;
                targetRegistry.Register(recipe, modId, ContentRegistrationPolicy.OverrideExisting, recipe.RecipeId);
                count++;
            }

            return count;
        }

        #endregion

        #region Automatic Project Hydration

        /// <summary>
        /// Hydrates <paramref name="contentService"/> with all built-in project definitions found in <c>Resources</c>
        /// and (when running in the Unity Editor) <c>Assets/Data/</c>.
        /// </summary>
        public static void PopulateFromProjectAssets(IContentService contentService)
        {
            if (contentService == null)
            {
                return;
            }

            EnsureBlockSecondaryIndex(contentService);

            // 1. Load any definitions placed inside Resources folders (works in both Standalone Builds and Editor)
            ImportEntities(contentService, Resources.LoadAll<EntityData>(string.Empty));
            ImportCraftingRecipes(contentService, Resources.LoadAll<CraftingRecipe>(string.Empty));

            foreach (var blockReg in Resources.LoadAll<BlockRegistry>(string.Empty))
            {
                ImportBlockRegistry(contentService, blockReg);
            }

            foreach (var entReg in Resources.LoadAll<EntityRegistry>(string.Empty))
            {
                ImportEntityRegistry(contentService, entReg);
            }

            foreach (var catalog in Resources.LoadAll<CreatorCatalogSO>(string.Empty))
            {
                ImportCreatorCatalog(contentService, catalog);
            }

#if UNITY_EDITOR
            // 2. In Editor mode, also bridge assets under Assets/Data/ that haven't been moved to ContentPacks yet
            ImportEditorAssets<EntityData>(contentService, "t:EntityData", (cs, asset) => ImportEntity(cs, asset));
            ImportEditorAssets<BlockRegistry>(contentService, "t:BlockRegistry", (cs, asset) => ImportBlockRegistry(cs, asset));
            ImportEditorAssets<EntityRegistry>(contentService, "t:EntityRegistry", (cs, asset) => ImportEntityRegistry(cs, asset));
            ImportEditorAssets<CreatorCatalogSO>(contentService, "t:CreatorCatalogSO", (cs, asset) => ImportCreatorCatalog(cs, asset));
            ImportEditorAssets<CraftingRecipe>(
                contentService,
                "t:CraftingRecipe",
                (cs, asset) => cs.GetRegistry<CraftingRecipe>().Register(asset, ResourceId.DefaultNamespace, ContentRegistrationPolicy.OverrideExisting, asset.RecipeId));
#endif
        }

        public static void EnsureBlockSecondaryIndex(IContentService contentService)
        {
            var blockRegistry = contentService.GetRegistry<BlockDefinition>();
            blockRegistry.RegisterSecondaryIndex<ushort>(BlockRuntimeIdIndex, b => b.RuntimeId);
        }

#if UNITY_EDITOR
        private static void ImportEditorAssets<TAsset>(
            IContentService contentService,
            string filter,
            System.Action<IContentService, TAsset> importAction) where TAsset : UnityEngine.Object
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(filter, new[] { "Assets/Data" });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                TAsset asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset != null)
                {
                    importAction(contentService, asset);
                }
            }
        }
#endif

        #endregion
    }
}
