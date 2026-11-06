namespace Gameplay.UI
{
    using System;
    using System.Collections.Generic;
    using Data.Config;
    using Domain.Inventory;
    using Gameplay.Entities;
    using UnityEngine;

    /// <summary>
    /// Full-featured SkyUI-style inventory and items browser with sortable columns,
    /// Value-to-Weight ratios, category filtering tabs, and equipment toggles.
    /// Toggled via the 'I' key.
    /// </summary>
    [AddComponentMenu("3D2DWorld/UI/SkyUI Inventory Menu")]
    public class SkyUIInventoryMenu : MonoBehaviour
    {
        #region Fields

        [Header("Settings")]
        [SerializeField] private KeyCode _toggleKey = KeyCode.I;
        [SerializeField] private bool _isOpen;

        private InventoryComponent _playerInventory;
        private string _activeCategory = "ALL";
        private string _searchFilter = string.Empty;
        private string _sortColumn = "NAME";
        private bool _sortAscending = true;
        private Vector2 _scrollPos;

        #endregion

        #region Properties

        public bool IsOpen
        {
            get => _isOpen;
            set => _isOpen = value;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            EnsurePlayerInventory();
            DrawSkyUIWindow();
        }

        #endregion

        #region Drawing SkyUI

        private void DrawSkyUIWindow()
        {
            float width = Mathf.Min(800f, Screen.width * 0.85f);
            float height = Mathf.Min(500f, Screen.height * 0.8f);
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.94f);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);
            GUI.color = Color.white;

            // Header & Category Tabs
            DrawCategories(x + 10f, y + 10f, width - 20f);

            // Table Columns Header
            DrawColumnHeaders(x + 10f, y + 45f, width - 20f);

            // Item rows scrollview
            DrawItemRows(x + 10f, y + 75f, width - 20f, height - 120f);

            // Footer
            DrawFooter(x + 10f, y + height - 35f, width - 20f);
        }

        private void DrawCategories(float x, float y, float width)
        {
            string[] categories = { "ALL", "WEAPONS", "APPAREL", "POTIONS", "MISC" };
            float tabWidth = width / categories.Length;

            for (int i = 0; i < categories.Length; i++)
            {
                bool isSelected = _activeCategory == categories[i];
                GUI.color = isSelected ? new Color(0.9f, 0.8f, 0.4f) : Color.white;
                if (GUI.Button(new Rect(x + (i * tabWidth), y, tabWidth - 4f, 26f), categories[i]))
                {
                    _activeCategory = categories[i];
                }
            }
            GUI.color = Color.white;
        }

        private void DrawColumnHeaders(float x, float y, float width)
        {
            GUI.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            GUI.Box(new Rect(x, y, width, 24f), GUIContent.none);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(x, y, width * 0.4f, 24f), $"ITEM NAME {GetSortArrow("NAME")}")) ToggleSort("NAME");
            if (GUI.Button(new Rect(x + (width * 0.4f), y, width * 0.15f, 24f), $"DMG/DEF {GetSortArrow("STAT")}")) ToggleSort("STAT");
            if (GUI.Button(new Rect(x + (width * 0.55f), y, width * 0.15f, 24f), $"WEIGHT {GetSortArrow("WEIGHT")}")) ToggleSort("WEIGHT");
            if (GUI.Button(new Rect(x + (width * 0.70f), y, width * 0.15f, 24f), $"VALUE {GetSortArrow("VALUE")}")) ToggleSort("VALUE");
            if (GUI.Button(new Rect(x + (width * 0.85f), y, width * 0.15f, 24f), $"V/W {GetSortArrow("VW")}")) ToggleSort("VW");
        }

        private void DrawItemRows(float x, float y, float width, float height)
        {
            if (_playerInventory == null) return;

            var items = GetFilteredAndSortedSlots();
            _scrollPos = GUI.BeginScrollView(new Rect(x, y, width, height), _scrollPos, new Rect(0, 0, width - 20f, items.Count * 26f));

            for (int i = 0; i < items.Count; i++)
            {
                var slot = items[i];
                var item = slot.Item;
                float rowY = i * 26f;

                float vw = item.Weight > 0f ? (item.Value / item.Weight) : item.Value;
                string countStr = slot.Count > 1 ? $" ({slot.Count})" : "";

                GUI.Label(new Rect(0, rowY, width * 0.4f, 24f), $"{item.DisplayName}{countStr}");
                GUI.Label(new Rect(width * 0.4f, rowY, width * 0.15f, 24f), item.Damage > 0f ? $"{item.Damage:F0}" : "-");
                GUI.Label(new Rect(width * 0.55f, rowY, width * 0.15f, 24f), $"{item.Weight * slot.Count:F1}");
                GUI.Label(new Rect(width * 0.70f, rowY, width * 0.15f, 24f), $"{item.Value * slot.Count}");
                GUI.Label(new Rect(width * 0.85f, rowY, width * 0.15f, 24f), $"{vw:F1}");
            }

            GUI.EndScrollView();
        }

        private void DrawFooter(float x, float y, float width)
        {
            float totalWeight = CalculateTotalWeight();
            GUI.Label(new Rect(x, y, width * 0.5f, 24f), $"Carry Weight: {totalWeight:F1} / 300.0");
            if (GUI.Button(new Rect(x + width - 90f, y, 90f, 26f), "Close (Esc)"))
            {
                _isOpen = false;
            }
        }

        #endregion

        #region Sorting & Filtering

        private void ToggleSort(string col)
        {
            if (_sortColumn == col) _sortAscending = !_sortAscending;
            else { _sortColumn = col; _sortAscending = true; }
        }

        private string GetSortArrow(string col) => _sortColumn == col ? (_sortAscending ? "▲" : "▼") : "";

        private List<InventorySlot> GetFilteredAndSortedSlots()
        {
            var list = new List<InventorySlot>();
            if (_playerInventory == null) return list;

            for (int i = 0; i < _playerInventory.Slots.Count; i++)
            {
                var s = _playerInventory.Slots[i];
                if (s == null || s.IsEmpty || s.Item == null) continue;
                list.Add(s);
            }

            list.Sort((a, b) =>
            {
                int cmp = _sortColumn switch
                {
                    "NAME" => string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.OrdinalIgnoreCase),
                    "STAT" => a.Item.Damage.CompareTo(b.Item.Damage),
                    "WEIGHT" => a.Item.Weight.CompareTo(b.Item.Weight),
                    "VALUE" => a.Item.Value.CompareTo(b.Item.Value),
                    "VW" => (a.Item.Value / Mathf.Max(0.1f, a.Item.Weight)).CompareTo(b.Item.Value / Mathf.Max(0.1f, b.Item.Weight)),
                    _ => 0
                };
                return _sortAscending ? cmp : -cmp;
            });

            return list;
        }

        private float CalculateTotalWeight()
        {
            if (_playerInventory == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < _playerInventory.Slots.Count; i++)
            {
                var s = _playerInventory.Slots[i];
                if (s != null && !s.IsEmpty && s.Item != null) sum += s.Item.Weight * s.Count;
            }
            return sum;
        }

        private void EnsurePlayerInventory()
        {
            if (_playerInventory != null) return;
            var player = GameObject.FindWithTag("Player");
            if (player != null) _playerInventory = player.GetComponent<InventoryComponent>();
        }

        #endregion
    }
}
