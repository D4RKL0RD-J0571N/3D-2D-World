namespace Gameplay.UI
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Gameplay.Entities;
    using Gameplay.Player;

    /// <summary>
    /// UI Toolkit controller presenting real-time player status (Health, Stamina),
    /// combat reticle, contextual interaction prompts, and damage feedback vignette.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("3D2DWorld/UI/Player HUD (UI Toolkit)")]
    public class PlayerHUD : MonoBehaviour
    {
        #region Fields

        [Header("Assets")]
        [SerializeField] private VisualTreeAsset _hudUxml;
        [SerializeField] private StyleSheet _hudUss;

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _damageVignette;
        private VisualElement _healthFill;
        private Label _healthLabel;
        private VisualElement _staminaFill;
        private VisualElement _staminaBar;
        private Label _staminaLabel;
        private VisualElement _promptContainer;
        private Label _promptLabel;

        private BaseActor _playerActor;
        private AttributeComponent _playerAttributes;
        private PlayerInteractionController _interactionController;

        private Coroutine _vignetteCoroutine;
        private Coroutine _exhaustionCoroutine;

        private const string VignetteActiveClass = "damage-vignette--active";
        private const string StaminaExhaustedClass = "status-bar--exhausted";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            SetupUIDocument();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.AddListener<PlayerStaminaExhaustedEvent>(OnStaminaExhausted);

            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
            {
                BindElements();
            }
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.RemoveListener<PlayerStaminaExhaustedEvent>(OnStaminaExhausted);

            StopAllCoroutines();
            _vignetteCoroutine = null;
            _exhaustionCoroutine = null;
        }

        private void Update()
        {
            EnsurePlayerBound();
            UpdateStatusBars();
            UpdateInteractionPrompt();
        }

        #endregion

        #region Setup & Binding

        private void SetupUIDocument()
        {
            if (_uiDocument == null) return;

            ResolveAssets();

            if (_hudUxml != null)
            {
                _uiDocument.visualTreeAsset = _hudUxml;
            }

            _root = _uiDocument.rootVisualElement;
            if (_root != null && _hudUss != null && !_root.styleSheets.Contains(_hudUss))
            {
                _root.styleSheets.Add(_hudUss);
            }

            BindElements();
        }

        private void ResolveAssets()
        {
            if (_hudUxml == null)
            {
                _hudUxml = Resources.Load<VisualTreeAsset>("UI/PlayerHUD");
#if UNITY_EDITOR
                if (_hudUxml == null)
                {
                    _hudUxml = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/PlayerHUD.uxml");
                }
#endif
            }

            if (_hudUss == null)
            {
                _hudUss = Resources.Load<StyleSheet>("UI/PlayerHUD");
#if UNITY_EDITOR
                if (_hudUss == null)
                {
                    _hudUss = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/PlayerHUD.uss");
                }
#endif
            }

            if (_uiDocument.panelSettings == null)
            {
                var panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
#if UNITY_EDITOR
                if (panelSettings == null)
                {
                    panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/PanelSettings.asset");
                }
#endif
                if (panelSettings != null)
                {
                    _uiDocument.panelSettings = panelSettings;
                }
            }
        }

        private void BindElements()
        {
            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            _damageVignette = _root.Q<VisualElement>("DamageVignette");
            _healthFill = _root.Q<VisualElement>("HealthBarFill");
            _healthLabel = _root.Q<Label>("HealthLabel");
            _staminaFill = _root.Q<VisualElement>("StaminaBarFill");
            _staminaBar = _root.Q<VisualElement>("StaminaBar");
            _staminaLabel = _root.Q<Label>("StaminaLabel");
            _promptContainer = _root.Q<VisualElement>("PromptContainer");
            _promptLabel = _root.Q<Label>("PromptLabel");

            if (_promptContainer != null)
            {
                _promptContainer.style.display = DisplayStyle.None;
            }
        }

        #endregion

        #region Player Resolution

        private void EnsurePlayerBound()
        {
            if (_playerActor != null) return;

            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) return;

            _playerActor = playerObj.GetComponent<BaseActor>();
            _playerAttributes = playerObj.GetComponent<AttributeComponent>();
            _interactionController = playerObj.GetComponent<PlayerInteractionController>();
        }

        #endregion

        #region Status Updates

        private void UpdateStatusBars()
        {
            if (_playerAttributes == null) return;

            UpdateHealthBar();
            UpdateStaminaBar();
        }

        private void UpdateHealthBar()
        {
            var health = _playerAttributes.GetAttribute("Health");
            if (health == null) return;

            float pct = Mathf.Clamp01((float)health.CurrentValue / Mathf.Max(1, health.MaximumValue)) * 100f;
            if (_healthFill != null)
            {
                _healthFill.style.width = Length.Percent(pct);
            }

            if (_healthLabel != null)
            {
                _healthLabel.text = $"{health.CurrentValue} / {health.MaximumValue}";
            }
        }

        private void UpdateStaminaBar()
        {
            var stamina = _playerAttributes.GetAttribute("Stamina");
            if (stamina == null) return;

            float pct = Mathf.Clamp01((float)stamina.CurrentValue / Mathf.Max(1, stamina.MaximumValue)) * 100f;
            if (_staminaFill != null)
            {
                _staminaFill.style.width = Length.Percent(pct);
            }

            if (_staminaLabel != null)
            {
                _staminaLabel.text = $"{stamina.CurrentValue} / {stamina.MaximumValue}";
            }
        }

        private void UpdateInteractionPrompt()
        {
            if (_promptContainer == null || _promptLabel == null) return;

            string prompt = _interactionController != null ? _interactionController.CurrentPrompt : string.Empty;

            if (!string.IsNullOrEmpty(prompt))
            {
                _promptLabel.text = prompt;
                _promptContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                _promptContainer.style.display = DisplayStyle.None;
            }
        }

        #endregion

        #region Event Handling & Visual Feedback

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt == null || evt.Target == null || evt.Target != _playerActor) return;

            if (_vignetteCoroutine != null)
            {
                StopCoroutine(_vignetteCoroutine);
            }
            _vignetteCoroutine = StartCoroutine(FlashDamageVignetteRoutine());
        }

        private void OnStaminaExhausted(PlayerStaminaExhaustedEvent evt)
        {
            if (_exhaustionCoroutine != null)
            {
                StopCoroutine(_exhaustionCoroutine);
            }
            _exhaustionCoroutine = StartCoroutine(FlashStaminaExhaustionRoutine());
        }

        private IEnumerator FlashDamageVignetteRoutine()
        {
            if (_damageVignette == null) yield break;

            _damageVignette.AddToClassList(VignetteActiveClass);
            yield return new WaitForSeconds(0.18f);
            _damageVignette.RemoveFromClassList(VignetteActiveClass);
            _vignetteCoroutine = null;
        }

        private IEnumerator FlashStaminaExhaustionRoutine()
        {
            if (_staminaBar == null) yield break;

            _staminaBar.AddToClassList(StaminaExhaustedClass);
            yield return new WaitForSeconds(0.2f);
            _staminaBar.RemoveFromClassList(StaminaExhaustedClass);
            _exhaustionCoroutine = null;
        }

        #endregion
    }
}
