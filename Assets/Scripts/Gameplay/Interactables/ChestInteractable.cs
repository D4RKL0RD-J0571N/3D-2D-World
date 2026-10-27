namespace Gameplay.Interactables
{
    using Core;
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
    /// Dungeon chest that holds loot and optionally requires lockpicking.
    /// Implements IInteractable so PlayerInteractionController can detect it.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Interactables/Chest")]
    public class ChestInteractable : MonoBehaviour, IInteractable
    {
        #region Serialized Fields

        [Header("Lock")]
        [SerializeField] private LockDifficulty _lockDifficulty = LockDifficulty.Unlocked;

        [Header("Loot Table")]
        [Tooltip("Item ID strings representing possible loot drops (prototype: all items drop).")]
        [SerializeField] private string[] _lootItemIds = { "ITM_Gold", "ITM_Potion_Health" };

        #endregion

        #region State

        private bool _isOpened = false;

        #endregion

        #region IInteractable

        public bool CanInteract => !_isOpened && !IsLocked;

        public string InteractionPrompt
        {
            get
            {
                if (_isOpened)         return "Empty Chest";
                if (IsLocked)          return $"🔒 Locked Chest  [{_lockDifficulty}]";
                return "Open Chest";
            }
        }

        public void OnInteract(Entities.BaseActor interactor)
        {
            if (!CanInteract) return;

            _isOpened = true;

            // Play open animation if an Animator is present
            GetComponent<Animator>()?.SetTrigger("Open");

            Debug.Log($"[ChestInteractable] {name} opened. Loot: {string.Join(", ", _lootItemIds)}");
            GameEventManager.Publish(new ChestOpenedEvent(this, _lootItemIds));
        }

        #endregion

        #region Helpers

        private bool IsLocked => _lockDifficulty != LockDifficulty.Unlocked;

        #endregion
    }
}
