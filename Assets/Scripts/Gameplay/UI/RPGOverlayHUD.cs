namespace Gameplay.UI
{
    using System.Collections;
    using Core;
    using Domain.Skyrim;
    using Systems.Skyrim;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>
    /// Displays skill level-up notifications and a compact quest objective tracker
    /// in a Skyrim-style side panel (right edge of screen).
    /// Uses UI Toolkit.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("3D2DWorld/UI/RPG Overlay HUD (UI Toolkit)")]
    public class RPGOverlayHUD : MonoBehaviour
    {
        #region Fields

        private UIDocument _uiDocument;
        private VisualElement _root;

        // Skill level-up toast
        private VisualElement _skillToastContainer;
        private Label _skillToastLabel;
        private Coroutine _toastCoroutine;

        // Quest tracker panel
        private VisualElement _questTracker;
        private Label _questTitle;
        private Label _questObjective;
        private Label _questProgress;

        // Character level badge
        private Label _charLevelLabel;

        private const string ToastVisibleClass = "skill-toast--visible";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            SetupUIDocument();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameEventManager.AddListener<PlayerCharacterLeveledUpEvent>(OnCharacterLeveledUp);
            GameEventManager.AddListener<QuestAcceptedEvent>(OnQuestAccepted);
            GameEventManager.AddListener<QuestObjectiveUpdatedEvent>(OnQuestObjectiveUpdated);
            GameEventManager.AddListener<QuestCompletedEvent>(OnQuestCompleted);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<SkillLeveledUpEvent>(OnSkillLeveledUp);
            GameEventManager.RemoveListener<PlayerCharacterLeveledUpEvent>(OnCharacterLeveledUp);
            GameEventManager.RemoveListener<QuestAcceptedEvent>(OnQuestAccepted);
            GameEventManager.RemoveListener<QuestObjectiveUpdatedEvent>(OnQuestObjectiveUpdated);
            GameEventManager.RemoveListener<QuestCompletedEvent>(OnQuestCompleted);
            StopAllCoroutines();
        }

        #endregion

        #region Setup

        private void SetupUIDocument()
        {
            if (_uiDocument == null) return;
            _uiDocument.sortingOrder = 15; // Above PlayerHUD (10), below WorldMap (30)
            ResolveAssets();
            BindElements();
        }

        private void ResolveAssets()
        {
            var uxml = Resources.Load<VisualTreeAsset>("UI/RPGOverlay");
#if UNITY_EDITOR
            if (uxml == null)
                uxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/RPGOverlay.uxml");
#endif
            if (uxml != null)
                _uiDocument.visualTreeAsset = uxml;

            var uss = Resources.Load<StyleSheet>("UI/RPGOverlay");
#if UNITY_EDITOR
            if (uss == null)
                uss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/RPGOverlay.uss");
#endif
            if (uss != null && _uiDocument.rootVisualElement != null)
                _uiDocument.rootVisualElement.styleSheets.Add(uss);
        }

        private void BindElements()
        {
            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            _skillToastContainer = _root.Q<VisualElement>("SkillToastContainer");
            _skillToastLabel     = _root.Q<Label>("SkillToastLabel");
            _questTracker        = _root.Q<VisualElement>("QuestTracker");
            _questTitle          = _root.Q<Label>("QuestTitle");
            _questObjective      = _root.Q<Label>("QuestObjective");
            _questProgress       = _root.Q<Label>("QuestProgress");
            _charLevelLabel      = _root.Q<Label>("CharLevelLabel");

            if (_questTracker != null)
                _questTracker.style.display = DisplayStyle.None;
        }

        #endregion

        #region Skill Events

        private void OnSkillLeveledUp(SkillLeveledUpEvent evt)
        {
            if (evt == null) return;

            string message = $"✦  {FormatSkillName(evt.Skill)}  ▲  Level {evt.NewLevel}";
            ShowSkillToast(message);

            // Play level-up chime
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
                audio.PlayInteract();
        }

        private void OnCharacterLeveledUp(PlayerCharacterLeveledUpEvent evt)
        {
            if (evt == null) return;

            if (_charLevelLabel != null)
                _charLevelLabel.text = $"Lvl {evt.NewLevel}";

            ShowSkillToast($"⬆  CHARACTER LEVEL {evt.NewLevel}  |  {evt.PerkPoints} Perk Point(s)");
        }

        private void ShowSkillToast(string message)
        {
            if (_skillToastLabel == null || _skillToastContainer == null) return;

            _skillToastLabel.text = message;

            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ToastRoutine());
        }

        private IEnumerator ToastRoutine()
        {
            _skillToastContainer.AddToClassList(ToastVisibleClass);
            yield return new WaitForSeconds(3f);
            _skillToastContainer.RemoveFromClassList(ToastVisibleClass);
            _toastCoroutine = null;
        }

        #endregion

        #region Quest Events

        private void OnQuestAccepted(QuestAcceptedEvent evt)
        {
            if (evt?.Quest == null) return;
            RefreshQuestTracker(evt.Quest);
        }

        private void OnQuestObjectiveUpdated(QuestObjectiveUpdatedEvent evt)
        {
            if (evt?.Quest == null) return;
            RefreshQuestTracker(evt.Quest);
        }

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            if (_questTracker != null)
                _questTracker.style.display = DisplayStyle.None;

            ShowSkillToast($"✔  QUEST COMPLETE — +{evt?.GoldReward}g");
        }

        private void RefreshQuestTracker(Domain.Skyrim.QuestInstance quest)
        {
            if (_questTracker == null) return;

            _questTracker.style.display = DisplayStyle.Flex;

            if (_questTitle != null)     _questTitle.text     = quest.Title.ToUpperInvariant();

            // Show first incomplete objective
            foreach (var obj in quest.Objectives)
            {
                if (obj.IsComplete) continue;
                if (_questObjective != null)
                    _questObjective.text = obj.Description;
                if (_questProgress != null)
                    _questProgress.text  = $"{obj.CurrentCount} / {obj.RequiredCount}";
                break;
            }
        }

        #endregion

        #region Helpers

        private static string FormatSkillName(SkyrimSkill skill) => skill switch
        {
            SkyrimSkill.OneHanded   => "One-Handed",
            SkyrimSkill.TwoHanded   => "Two-Handed",
            SkyrimSkill.LightArmor  => "Light Armor",
            SkyrimSkill.HeavyArmor  => "Heavy Armor",
            _                       => skill.ToString()
        };

        #endregion
    }
}
