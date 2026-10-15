namespace Systems.Interaction
{
    using System;
    using System.Collections.Generic;
    using Core;
    using Domain.Grid;
    using UnityEngine;

    public class HotbarStateChangedEvent : GameEvent
    {
        public int SelectedSlotIndex;
        public BlockDefinition SelectedBlock;
        public BlockDefinition[] Slots;
    }

    /// <summary>
    /// Service managing the player's 9-slot block hotbar palette.
    /// Handles slot switching via keyboard numbers or mouse wheel.
    /// </summary>
    public sealed class HotbarService : IGameService
    {
        public const int SlotCount = 9;

        private readonly BlockDefinition[] _slots = new BlockDefinition[SlotCount];
        private int _selectedSlotIndex = 0;
        private BlockRegistry _registry;

        public int SelectedSlotIndex => _selectedSlotIndex;
        public BlockDefinition SelectedBlock => _slots[_selectedSlotIndex];

        public void Initialize()
        {
            GameEventManager.AddListener<PlayerHotbarSlotSelectedEvent>(OnSlotSelected);
            GameEventManager.AddListener<Gameplay.World.LevelGenerationCompletedEvent>(OnLevelGenerated);
        }

        public void Shutdown()
        {
            GameEventManager.RemoveListener<PlayerHotbarSlotSelectedEvent>(OnSlotSelected);
            GameEventManager.RemoveListener<Gameplay.World.LevelGenerationCompletedEvent>(OnLevelGenerated);
        }

        public void SetRegistry(BlockRegistry registry)
        {
            _registry = registry;
            PopulateFromRegistry();
        }

        public BlockDefinition GetBlockAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return null;

            return _slots[slotIndex];
        }

        public void SelectSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                return;

            _selectedSlotIndex = slotIndex;

            if (ServiceLocator.TryGet<BlockPlacementService>(out var placementService))
            {
                placementService.SelectedBlock = _slots[_selectedSlotIndex];
            }

            NotifyStateChanged();
        }

        public void CycleSlot(int delta)
        {
            int next = (_selectedSlotIndex + delta) % SlotCount;
            if (next < 0) next += SlotCount;
            SelectSlot(next);
        }

        private void OnSlotSelected(PlayerHotbarSlotSelectedEvent evt)
        {
            SelectSlot(evt.SlotIndex);
        }

        private void OnLevelGenerated(Gameplay.World.LevelGenerationCompletedEvent evt)
        {
            if (_registry == null)
            {
                var levelRenderer = UnityEngine.Object.FindFirstObjectByType<Gameplay.World.LevelLayoutRenderer>();
                if (levelRenderer != null)
                {
                    // If registry is accessible through render config
                }
            }
        }

        private void PopulateFromRegistry()
        {
            if (_registry == null) return;

            var blocks = _registry.GetAllBlocks();
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = i < blocks.Count ? blocks[i] : null;
            }

            if (ServiceLocator.TryGet<BlockPlacementService>(out var placementService))
            {
                placementService.SelectedBlock = _slots[_selectedSlotIndex];
            }

            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            GameEventManager.Publish(new HotbarStateChangedEvent
            {
                SelectedSlotIndex = _selectedSlotIndex,
                SelectedBlock = _slots[_selectedSlotIndex],
                Slots = (BlockDefinition[])_slots.Clone()
            });
        }
    }
}
