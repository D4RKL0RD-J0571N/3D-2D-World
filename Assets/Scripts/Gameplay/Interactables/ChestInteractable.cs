namespace Gameplay.Interactables
{
    using System;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Core.Persistence;
    using Newtonsoft.Json;
    using Systems.Creator;
    using UnityEngine;

    // ── Difficulty enum ───────────────────────────────────────────────────────

    public enum LockDifficulty
    {
        Unlocked,
        Novice,
        Apprentice,
        Adept,
        Master
    }

    // ── Chest Event ───────────────────────────────────────────────────────────

    /// <summary>Fired when a chest is successfully opened.</summary>
    public class ChestOpenedEvent : GameEvent
    {
        public ChestInteractable Source;
        public string[] Loot;

        public ChestOpenedEvent(ChestInteractable source, string[] loot)
        {
            Source = source;
            Loot   = loot;
        }
    }

    // ── Component ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dungeon chest that holds loot, optionally requires lockpicking, and supports
    /// <see cref="ILinkReceiver"/> / <see cref="EditorPlacedEntityTag"/> wiring.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Chest")]
    public class ChestInteractable : MonoBehaviour, IInteractable, ISaveableComponent, ILinkReceiver
    {
        #region Serialized Fields

        [Header("Lock")]
        [SerializeField] private LockDifficulty _lockDifficulty = LockDifficulty.Unlocked;

        [Header("Loot Table")]
        [Tooltip("Item ID strings representing possible loot drops (prototype: all items drop).")]
        [SerializeField] private string[] _lootItemIds = { "ITM_Gold", "ITM_Potion_Health" };

        #endregion

        #region State & Properties

        private bool _isOpened = false;

        public bool IsLocked => _lockDifficulty != LockDifficulty.Unlocked;
        public bool IsOpened => _isOpened;
        public LockDifficulty Difficulty => _lockDifficulty;

        public void ConfigureChest(LockDifficulty difficulty, bool isOpened = false, string[] lootItemIds = null)
        {
            _lockDifficulty = difficulty;
            _isOpened = isOpened;
            if (lootItemIds != null)
            {
                _lootItemIds = lootItemIds;
            }
        }

        #endregion

        #region IInteractable

        public bool CanInteract => !_isOpened;

        public string InteractionPrompt
        {
            get
            {
                if (_isOpened) return "Empty Chest";
                if (IsLocked)
                {
                    int currentSkill = GetPlayerLockpickSkill();
                    int reqSkill = GetRequiredSkillLevel();
                    string status = currentSkill >= reqSkill ? "Pick Lock" : "Locked (Skill Too Low)";
                    return $"🔒 {status} [{_lockDifficulty}] (Req: {reqSkill})";
                }
                return "Open Chest";
            }
        }

        public void OnInteract(Entities.BaseActor interactor)
        {
            if (!CanInteract) return;

            if (IsLocked)
            {
                AttemptLockpick(interactor);
                return;
            }

            OpenChest(interactor);
        }

        #endregion

        #region ILinkReceiver Implementation

        public void ExecuteLinkAction(string actionName, GameObject sourceObject = null)
        {
            if (string.Equals(actionName, "Unlock", StringComparison.OrdinalIgnoreCase))
            {
                _lockDifficulty = LockDifficulty.Unlocked;
            }
            else if (string.Equals(actionName, "Lock", StringComparison.OrdinalIgnoreCase))
            {
                if (_lockDifficulty == LockDifficulty.Unlocked)
                {
                    _lockDifficulty = LockDifficulty.Novice;
                }
            }
            else if (string.Equals(actionName, "Open", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(actionName, "Activate", StringComparison.OrdinalIgnoreCase))
            {
                _lockDifficulty = LockDifficulty.Unlocked;
                OpenChest(null);
            }
        }

        #endregion

        #region Lockpicking & Opening

        private void AttemptLockpick(Entities.BaseActor interactor = null)
        {
            int currentSkill = GetPlayerLockpickSkill();
            int reqSkill = GetRequiredSkillLevel();

            if (currentSkill >= reqSkill)
            {
                _lockDifficulty = LockDifficulty.Unlocked;

                // Award Lockpicking XP based on lock tier
                if (ServiceLocator.TryGet<Systems.Skyrim.SkillProgressionService>(out var skills))
                {
                    float xp = 15f + reqSkill * 0.5f;
                    skills.AwardXP(Domain.Skyrim.SkyrimSkill.Lockpicking, xp);
                }

                if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                {
                    audio.PlayInteract();
                }

                Debug.Log($"<color=yellow>✦ Lock picked successfully! [Difficulty: {_lockDifficulty}]</color>");
                OpenChest(interactor);
            }
            else
            {
                if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                {
                    audio.PlayClick();
                }

                Debug.LogWarning($"[ChestInteractable] Lockpick failed. Skill {currentSkill} < Required {reqSkill}");
            }
        }

        private void OpenChest(Entities.BaseActor interactor = null)
        {
            _isOpened = true;

            // Play open animation if an Animator is present
            if (TryGetComponent<Animator>(out var anim) && anim != null && anim.runtimeAnimatorController != null)
            {
                anim.SetTrigger("Open");
            }

            if (interactor != null && interactor.TryGetComponent<Entities.InventoryComponent>(out var inventory))
            {
                DistributeLoot(inventory);
            }

            Debug.Log($"[ChestInteractable] {name} opened. Loot: {string.Join(", ", _lootItemIds)}");
            GameEventManager.Publish(new ChestOpenedEvent(this, _lootItemIds));

            var editorTag = GetComponent<EditorPlacedEntityTag>();
            if (editorTag != null)
            {
                editorTag.TriggerOutputPort("OnOpened");
            }
        }

        private void DistributeLoot(Entities.InventoryComponent inventory)
        {
            if (_lootItemIds == null || _lootItemIds.Length == 0 || inventory == null) return;

            IContentService contentService = ServiceLocator.TryGet<IContentService>(out var cs) ? cs : null;
            var itemRegistry = contentService?.GetRegistry<Data.Config.ItemData>();

            for (int i = 0; i < _lootItemIds.Length; i++)
            {
                string itemId = _lootItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId)) continue;

                Data.Config.ItemData item = null;
                if (itemRegistry != null)
                {
                    itemRegistry.TryGetByLegacyAlias(itemId, out item);
                }

                if (item == null)
                {
                    item = Resources.Load<Data.Config.ItemData>($"Items/{itemId}");
                }

                if (item != null)
                {
                    inventory.AddItem(item, 1);
                }
            }

            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.PlayLoot();
            }
        }

        #endregion

        #region Helpers

        private int GetRequiredSkillLevel() => _lockDifficulty switch
        {
            LockDifficulty.Novice     => 15,
            LockDifficulty.Apprentice => 25,
            LockDifficulty.Adept      => 50,
            LockDifficulty.Master     => 75,
            _                         => 0
        };

        private int GetPlayerLockpickSkill()
        {
            if (ServiceLocator.TryGet<Systems.Skyrim.SkillProgressionService>(out var skills))
            {
                return skills.GetSkillLevel(Domain.Skyrim.SkyrimSkill.Lockpicking);
            }
            return 15;
        }

        #endregion

        #region ISaveableComponent Implementation

        [Serializable]
        private sealed class ChestSaveDto
        {
            public LockDifficulty LockDifficulty;
            public bool IsOpened;
            public string[] LootItemIds;
        }

        public string ComponentSaveKey => "chest";

        public object CaptureComponentState()
        {
            return new ChestSaveDto
            {
                LockDifficulty = _lockDifficulty,
                IsOpened = _isOpened,
                LootItemIds = _lootItemIds
            };
        }

        public void RestoreComponentState(string serializedJsonState, IWorldInstanceResolver resolver)
        {
            if (string.IsNullOrWhiteSpace(serializedJsonState)) return;

            var settings = FantasyEngineJsonSettings.CreateDefault();
            var dto = JsonConvert.DeserializeObject<ChestSaveDto>(serializedJsonState, settings);
            if (dto == null) return;

            _lockDifficulty = dto.LockDifficulty;
            _isOpened = dto.IsOpened;
            if (dto.LootItemIds != null)
            {
                _lootItemIds = dto.LootItemIds;
            }
        }

        #endregion
    }
}
