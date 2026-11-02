namespace Domain.Creator
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// ScriptableObject registry that catalogs all spawnable items for the In-Game Creator Studio.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatorCatalog", menuName = "3D 2D World/Creator/Catalog")]
    public class CreatorCatalogSO : ScriptableObject
    {
        #region Inspector Fields

        [Header("Catalog Items")]
        [SerializeField] private List<CreatorItemDefinition> items = new List<CreatorItemDefinition>();

        #endregion

        #region Properties

        public IReadOnlyList<CreatorItemDefinition> Items => items;

        #endregion

        #region Public Methods

        public CreatorItemDefinition GetItemById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return items.Find(x => x.id == id);
        }

        public List<CreatorItemDefinition> GetItemsByCategory(CreatorCategory category)
        {
            return items.FindAll(x => x.category == category);
        }

        public void AddItem(CreatorItemDefinition item)
        {
            if (item != null && !items.Exists(x => x.id == item.id))
            {
                items.Add(item);
            }
        }

        public void ClearItems()
        {
            items.Clear();
        }

        public int RemoveWhere(Predicate<CreatorItemDefinition> match)
        {
            if (match == null) return 0;
            return items.RemoveAll(match);
        }

        #endregion
    }
}
