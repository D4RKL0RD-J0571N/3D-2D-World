namespace Data.Config
{
    using System;
    using Core;
    using Core.Content;
    using Core.Ids;
    using UnityEngine;

    /// <summary>
    /// Base ScriptableObject definition for all identifiable game entities (Actors, Items, Containers, Blocks).
    /// Implements <see cref="IContentDefinition"/> to bridge legacy sequential string IDs (<c>ITM_0001</c>)
    /// with canonical namespaced <see cref="ResourceId"/>s (<c>nordic_rpg:item/itm_0001</c>).
    /// </summary>
    public abstract class EntityData : ScriptableObject, IContentDefinition
    {
        #region Serialized Fields

        [Header("Base Entity Details")]
        [Tooltip("Auto-generated legacy ID. Preserved for backward compatibility.")]
        public string Id;

        public string DisplayName;

        [Header("Canonical Content Identity")]
        [Tooltip("Optional explicit ResourceId (namespace:path). If left empty, synthesized deterministically from SourceModId, CategoryPrefix, and Id.")]
        [SerializeField]
        private ResourceId _contentId;

        [SerializeField]
        private string _sourceModId = ResourceId.DefaultNamespace;

        #endregion

        #region Properties & IContentDefinition

        public abstract string CategoryPrefix { get; }

        /// <summary>Gets or sets the explicit or synthesized canonical <see cref="ResourceId"/>.</summary>
        public ResourceId ContentId
        {
            get
            {
                if (!_contentId.IsEmpty)
                {
                    return _contentId;
                }

                string rawIdentifier = !string.IsNullOrWhiteSpace(Id) ? Id : name;
                return ResourceId.FromLegacyId(_sourceModId, GetCategoryFolder(), rawIdentifier);
            }
            set => _contentId = value;
        }

        /// <summary>Gets or sets the originating mod or content pack identifier.</summary>
        public string SourceModId
        {
            get => string.IsNullOrWhiteSpace(_sourceModId) ? ResourceId.DefaultNamespace : _sourceModId;
            set => _sourceModId = value;
        }

        string IIdentifiable.Id => Id;

        string IContentDefinition.DisplayName => DisplayName;

        #endregion

        #region Lifecycle, Validation & Editor ID Assignment

        protected virtual void OnEnable()
        {
            if (string.IsNullOrEmpty(Id))
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorUtility.IsPersistent(this))
                {
                    Id = $"{CategoryPrefix}_{Math.Abs(GetInstanceID())}";
                }
#else
                Id = $"{CategoryPrefix}_{Math.Abs(GetInstanceID())}";
#endif
            }
        }

        /// <summary>
        /// Maps the legacy uppercase <see cref="CategoryPrefix"/> to a clean lowercase category folder for <see cref="ResourceId"/> synthesis.
        /// </summary>
        protected virtual string GetCategoryFolder()
        {
            return CategoryPrefix switch
            {
                "ITM" => "item",
                "ACT" => "actor",
                "CONT" => "container",
                "BLCK" => "block",
                _ => CategoryPrefix?.ToLowerInvariant() ?? "entity"
            };
        }

        /// <summary>
        /// Validates entity identity fields. Marked <c>protected virtual</c> so derived classes (such as <c>BlockDefinition</c>)
        /// can override and invoke <c>base.OnValidate()</c> without shadowing base validation logic.
        /// </summary>
        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(Id))
            {
                // Delay the call because AssetDatabase operations inside OnValidate can cause Unity warnings
                UnityEditor.EditorApplication.delayCall -= AssignNewId;
                UnityEditor.EditorApplication.delayCall += AssignNewId;
            }
#endif
        }

#if UNITY_EDITOR
        private void AssignNewId()
        {
            UnityEditor.EditorApplication.delayCall -= AssignNewId;
            if (this == null) return; // Asset was deleted before delay call
            if (!string.IsNullOrEmpty(Id)) return;

            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(EntityData)}");
            int maxId = 0;

            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EntityData>(path);

                if (asset != null && asset != this && asset.CategoryPrefix == this.CategoryPrefix)
                {
                    if (!string.IsNullOrEmpty(asset.Id) && asset.Id.StartsWith(CategoryPrefix + "_"))
                    {
                        string numberPart = asset.Id.Substring(CategoryPrefix.Length + 1);
                        if (int.TryParse(numberPart, out int parsedNum))
                        {
                            if (parsedNum > maxId) maxId = parsedNum;
                        }
                    }
                }
            }

            Id = $"{CategoryPrefix}_{(maxId + 1):D4}";
            OnIdAssigned();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        protected virtual void OnIdAssigned()
        {
        }

        #endregion
    }
}
