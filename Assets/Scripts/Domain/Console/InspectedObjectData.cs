namespace Domain.Console
{
    using System;
    using System.Collections.Generic;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Encapsulates rich debugging, reference, and gameplay metadata for an inspected object,
    /// emulating Skyrim's "More Informative Console" (Nexus mod 19250).
    /// </summary>
    public class InspectedObjectData
    {
        #region Properties

        public GameObject Target { get; private set; }
        public string Name { get; private set; }
        public uint FormId { get; private set; }
        public string FormIdHex => $"0x{FormId:X8}";
        public uint BaseFormId { get; private set; }
        public string BaseFormIdHex => $"0x{BaseFormId:X8}";
        public string EditorId { get; private set; }
        public string DefiningMod { get; private set; }
        public string LastModifiedBy { get; private set; }
        
        public Vector3 Position { get; private set; }
        public Vector3 Rotation { get; private set; }
        public Vector3 Scale { get; private set; }
        public string LayerName { get; private set; }
        public string TagName { get; private set; }
        public float DistanceFromPlayer { get; private set; }

        public bool IsActor { get; private set; }
        public InspectedObjectData ActorStats => this;
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Magicka { get; private set; }
        public int Stamina { get; private set; }
        public string FactionName { get; private set; }
        public string AIRoutine { get; private set; }
        public bool IsDead { get; private set; }

        public bool HasInventory { get; private set; }
        public int ItemSlotCount { get; private set; }
        public float TotalWeight { get; private set; }
        public List<string> InventorySummary { get; } = new();

        public bool IsInteractable { get; private set; }
        public string InteractableType { get; private set; }
        public string LockDifficultyStr { get; private set; }
        public string LockDifficulty => LockDifficultyStr;
        public string LockState { get; private set; }

        public List<string> ComponentNames { get; } = new();

        #endregion

        #region Extraction API

        /// <summary>
        /// Inspects a target GameObject and extracts all available reference, transform,
        /// entity, inventory, and component metadata.
        /// </summary>
        public static InspectedObjectData FromGameObject(GameObject target, GameObject player = null)
        {
            if (target == null) return null;

            var data = new InspectedObjectData
            {
                Target = target,
                Name = target.name,
                Position = target.transform.position,
                Rotation = target.transform.eulerAngles,
                Scale = target.transform.localScale,
                LayerName = LayerMask.LayerToName(target.layer),
                TagName = target.tag
            };

            data.ComputeFormIds(target);
            data.ExtractTransformAndComponents(target, player);
            data.ExtractActorData(target);
            data.ExtractInventoryData(target);
            data.ExtractInteractableData(target);

            return data;
        }

        #endregion

        #region Helper Extraction Methods

        private void ComputeFormIds(GameObject target)
        {
            int hash = target.name.GetHashCode();
            FormId = 0xFF000000 | (uint)(Math.Abs(hash) & 0x00FFFFFF);
            BaseFormId = 0x00010000 | (uint)(Math.Abs(target.GetInstanceID()) & 0x0000FFFF);
            EditorId = target.name.Replace(" ", "").Replace("(Clone)", "");
            DefiningMod = target.name.Contains("Mod") ? "CommunityMod.esp" : "Skyrim.esm";
            LastModifiedBy = target.name.Contains("Patch") ? "Update.esm" : DefiningMod;
        }

        private void ExtractTransformAndComponents(GameObject target, GameObject player)
        {
            if (player != null)
            {
                DistanceFromPlayer = Vector3.Distance(player.transform.position, target.transform.position);
            }

            var components = target.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    ComponentNames.Add(components[i].GetType().Name);
                }
            }
        }

        private void ExtractActorData(GameObject target)
        {
            if (!target.TryGetComponent<BaseActor>(out _)) return;

            IsActor = true;

            if (target.TryGetComponent<AttributeComponent>(out var attrs))
            {
                var healthAttr = attrs.GetAttribute("Health");
                Health = healthAttr != null ? healthAttr.CurrentValue : 100;
                MaxHealth = healthAttr != null ? healthAttr.MaximumValue : 100;
                Magicka = attrs.GetAttribute("Magicka")?.CurrentValue ?? 100;
                Stamina = attrs.GetAttribute("Stamina")?.CurrentValue ?? 100;
                IsDead = Health <= 0;
            }
            else
            {
                Health = 100;
                MaxHealth = 100;
                Magicka = 100;
                Stamina = 100;
                IsDead = false;
            }

            if (target.TryGetComponent<FactionComponent>(out var faction))
            {
                FactionName = faction.Faction.ToString();
            }

            if (target.TryGetComponent<ImmersiveCitizenAI>(out var citizen))
            {
                AIRoutine = citizen.CurrentPhase.ToString();
            }
            else
            {
                AIRoutine = IsDead ? "Dead" : "Idle";
            }
        }

        private void ExtractInventoryData(GameObject target)
        {
            if (!target.TryGetComponent<InventoryComponent>(out var inv)) return;

            HasInventory = true;
            ItemSlotCount = inv.Slots.Count;
            float weight = 0f;

            for (int i = 0; i < inv.Slots.Count; i++)
            {
                var slot = inv.Slots[i];
                if (slot.Item != null)
                {
                    weight += slot.Item.Weight * slot.Count;
                    InventorySummary.Add($"{slot.Item.DisplayName} x{slot.Count}");
                }
            }

            TotalWeight = weight;
        }

        private void ExtractInteractableData(GameObject target)
        {
            if (target.TryGetComponent<ChestInteractable>(out var chest))
            {
                IsInteractable = true;
                InteractableType = "Chest";
                LockDifficultyStr = chest.Difficulty.ToString();
                LockState = chest.IsLocked ? $"LOCKED ({chest.Difficulty})" : "UNLOCKED";
            }
            else if (target.TryGetComponent<IInteractable>(out var interactable))
            {
                IsInteractable = true;
                InteractableType = interactable.GetType().Name;
                LockDifficultyStr = "None";
                LockState = "UNLOCKED";
            }
        }

        #endregion
    }
}
