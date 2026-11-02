namespace Gameplay.Entities
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Data.Config;
    using Domain.Inventory;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Core.Persistence;
    using Newtonsoft.Json;

    /// <summary>
    /// Component managing an entity's item storage, stacking, additions, and removals.
    /// Publishes <see cref="InventoryChangedEvent"/> upon any modification and implements <see cref="ISaveableComponent"/>.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Entities/Inventory Component")]
    [DisallowMultipleComponent]
    public class InventoryComponent : MonoBehaviour, ISaveableComponent
    {
        #region Fields

        [SerializeField] private int _capacity = 20;
        [SerializeField] private List<InventorySlot> _slots = new List<InventorySlot>();
        private readonly List<UnknownContentPlaceholder> _unresolvedSlots = new();

        #endregion

        #region Properties

        public int Capacity => _capacity;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        /// <summary>Tombstone records for items from uninstalled mods preserved during save load (<c>SAV-05</c>).</summary>
        public IReadOnlyList<UnknownContentPlaceholder> UnresolvedSlots => _unresolvedSlots;

        #endregion

        #region Inventory Management

        /// <summary>
        /// Adds an item and count to the inventory, stacking where appropriate.
        /// </summary>
        /// <param name="item">The item blueprint to add.</param>
        /// <param name="count">Quantity to add.</param>
        /// <returns>True if the entire quantity was successfully added; false if inventory was full.</returns>
        public bool AddItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return false;

            int remaining = count;

            // 1. Try stacking into existing non-full slots if stackable
            if (item.IsStackable)
            {
                remaining = StackIntoExistingSlots(item, remaining);
            }

            // 2. Put remainder into new or empty slots
            while (remaining > 0 && HasAvailableSlot())
            {
                int stackAmount = item.IsStackable ? Mathf.Min(remaining, item.MaxStackSize) : 1;
                InsertIntoFirstAvailableSlot(item, stackAmount);
                remaining -= stackAmount;
            }

            int added = count - remaining;
            if (added > 0)
            {
                GameEventManager.Publish(new InventoryChangedEvent
                {
                    Owner = gameObject,
                    Item = item,
                    CountDelta = added,
                    NewTotalCount = GetItemCount(item)
                });
            }

            return remaining == 0;
        }

        /// <summary>
        /// Removes a quantity of an item from the inventory.
        /// </summary>
        public bool RemoveItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0 || !HasItem(item, count)) return false;

            int remainingToRemove = count;

            for (int i = _slots.Count - 1; i >= 0 && remainingToRemove > 0; i--)
            {
                var slot = _slots[i];
                if (slot.Item == item)
                {
                    int take = Mathf.Min(slot.Count, remainingToRemove);
                    slot.Count -= take;
                    remainingToRemove -= take;

                    if (slot.Count <= 0)
                    {
                        _slots.RemoveAt(i);
                    }
                }
            }

            GameEventManager.Publish(new InventoryChangedEvent
            {
                Owner = gameObject,
                Item = item,
                CountDelta = -count,
                NewTotalCount = GetItemCount(item)
            });

            return true;
        }

        /// <summary>
        /// Checks whether the inventory contains at least the specified quantity of an item.
        /// </summary>
        public bool HasItem(ItemData item, int count = 1)
        {
            return GetItemCount(item) >= count;
        }

        /// <summary>
        /// Returns total count of a specific item across all slots.
        /// </summary>
        public int GetItemCount(ItemData item)
        {
            if (item == null) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Item == item)
                {
                    total += _slots[i].Count;
                }
            }
            return total;
        }

        #endregion

        #region Helpers

        private int StackIntoExistingSlots(ItemData item, int remaining)
        {
            for (int i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var slot = _slots[i];
                if (slot.Item == item && slot.Count < item.MaxStackSize)
                {
                    int space = item.MaxStackSize - slot.Count;
                    int toAdd = Mathf.Min(space, remaining);
                    slot.Count += toAdd;
                    remaining -= toAdd;
                }
            }
            return remaining;
        }

        private bool HasAvailableSlot()
        {
            return _slots.Count < _capacity;
        }

        private void InsertIntoFirstAvailableSlot(ItemData item, int count)
        {
            _slots.Add(new InventorySlot(item, count));
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Serializes the inventory contents into an array of <see cref="Domain.Persistence.InventorySlotSaveData"/>.
        /// </summary>
        public Domain.Persistence.InventorySlotSaveData[] GetSaveData()
        {
            var result = new List<Domain.Persistence.InventorySlotSaveData>();
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot != null && slot.Item != null && slot.Count > 0)
                {
                    result.Add(new Domain.Persistence.InventorySlotSaveData
                    {
                        ItemId = slot.Item.Id,
                        Count = slot.Count
                    });
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Restores inventory slots from save data using the item registry/database for lookup.
        /// </summary>
        public void RestoreSaveData(Domain.Persistence.InventorySlotSaveData[] savedSlots, System.Func<string, ItemData> itemLookup)
        {
            _slots.Clear();
            if (savedSlots == null || itemLookup == null) return;

            foreach (var savedSlot in savedSlots)
            {
                if (string.IsNullOrEmpty(savedSlot.ItemId) || savedSlot.Count <= 0) continue;

                var item = itemLookup(savedSlot.ItemId);
                if (item != null)
                {
                    _slots.Add(new InventorySlot(item, savedSlot.Count));
                }
                else
                {
                    Debug.LogWarning($"[InventoryComponent] Could not resolve Item ID {savedSlot.ItemId} during restore!");
                }
            }
        }

        #endregion

        #region ISaveableComponent Implementation

        [Serializable]
        private sealed class InventorySaveDto
        {
            public List<SavedSlotRecord> Slots = new();
        }

        [Serializable]
        private sealed class SavedSlotRecord
        {
            public string ContentId;
            public string LegacyId;
            public int Count;
        }

        public string ComponentSaveKey => "inventory";

        public object CaptureComponentState()
        {
            var dto = new InventorySaveDto();
            for (int i = 0; i < _slots.Count; i++)
            {
                InventorySlot slot = _slots[i];
                if (slot != null && slot.Item != null && slot.Count > 0)
                {
                    dto.Slots.Add(new SavedSlotRecord
                    {
                        ContentId = slot.Item.ContentId.Value,
                        LegacyId = slot.Item.Id,
                        Count = slot.Count
                    });
                }
            }

            // Preserve any unresolved mod item tombstones across re-saves!
            for (int i = 0; i < _unresolvedSlots.Count; i++)
            {
                UnknownContentPlaceholder unresolved = _unresolvedSlots[i];
                if (unresolved != null && unresolved.MissingId.IsValid && unresolved.Count > 0)
                {
                    dto.Slots.Add(new SavedSlotRecord
                    {
                        ContentId = unresolved.MissingId.Value,
                        LegacyId = unresolved.MissingId.Value,
                        Count = unresolved.Count
                    });
                }
            }

            return dto;
        }

        public void RestoreComponentState(string serializedJsonState, IWorldInstanceResolver resolver)
        {
            _slots.Clear();
            _unresolvedSlots.Clear();
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<InventorySaveDto>(serializedJsonState, settings);
            if (dto?.Slots == null) return;

            IContentService content = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;
            InstanceGuid ownerGuid = TryGetComponent<PersistentEntityComponent>(out var pec) ? pec.InstanceId : InstanceGuid.Empty;

            for (int i = 0; i < dto.Slots.Count; i++)
            {
                SavedSlotRecord saved = dto.Slots[i];
                if (saved == null || saved.Count <= 0) continue;

                ItemData resolvedItem = null;
                if (content != null)
                {
                    if (!string.IsNullOrWhiteSpace(saved.ContentId) &&
                        ResourceId.TryParse(saved.ContentId, out ResourceId cid) &&
                        content.TryResolve(cid, out ItemData byCid))
                    {
                        resolvedItem = byCid;
                    }
                    else if (!string.IsNullOrWhiteSpace(saved.LegacyId) &&
                             content.TryResolveByLegacyAlias(saved.LegacyId, out ItemData byAlias))
                    {
                        resolvedItem = byAlias;
                    }
                }

                if (resolvedItem != null)
                {
                    _slots.Add(new InventorySlot(resolvedItem, saved.Count));
                }
                else
                {
                    ResourceId missingId = ResourceId.TryParse(saved.ContentId, out ResourceId parsedCid)
                        ? parsedCid
                        : ResourceId.FromLegacyId("unknown_mod", "item", saved.LegacyId ?? "unknown");

                    var placeholder = new UnknownContentPlaceholder
                    {
                        Category = "inventory_item",
                        MissingId = missingId,
                        OwnerGuid = ownerGuid,
                        Count = saved.Count,
                        RawPayloadJson = JsonConvert.SerializeObject(saved, settings)
                    };

                    _unresolvedSlots.Add(placeholder);
                    resolver?.RecordUnknownContentPlaceholder(placeholder);
                }
            }
        }

        #endregion
    }
}
