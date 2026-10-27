namespace Gameplay.UI
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Gameplay.Player;
    using Systems.Skyrim;

    /// <summary>
    /// UI Toolkit controller presenting real-time Skyrim-style player HUD:
    /// Status triangle (Health, Magicka, Stamina), 360° Compass with POI tracking,
    /// dynamic Sneak Eye reticle, and contextual interaction prompts.
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

        // Resource Bars
        private VisualElement _healthFill;
        private Label _healthLabel;
        private VisualElement _magickaFill;
        private Label _magickaLabel;
        private VisualElement _staminaFill;
        private VisualElement _staminaBar;
        private Label _staminaLabel;

        // Reticle & Sneak Eye
        private VisualElement _standardReticle;
        private VisualElement _sneakEyeContainer;
        private VisualElement _sneakEyePupil;
        private Label _sneakEyeStatus;

        // Contextual Interaction
        private VisualElement _promptContainer;
        private Label _promptLabel;

        // Skyrim Compass
        private VisualElement _compassTape;
        private VisualElement _compassMarkersLayer;
        private VisualElement _discoveryBanner;
        private Label _discoveryText;

        // Marker pool for compass pips
        private readonly List<VisualElement> _markerPips = new();

        // Player bindings
        private BaseActor _playerActor;
        private AttributeComponent _playerAttributes;
        private PlayerInteractionController _interactionController;

        // Coroutines
        private Coroutine _vignetteCoroutine;
        private Coroutine _exhaustionCoroutine;
        private Coroutine _discoveryCoroutine;

        private const string VignetteActiveClass = "damage-vignette--active";
        private const string StaminaExhaustedClass = "status-bar--exhausted";
        private const string DiscoveryBannerVisibleClass = "discovery-banner--visible";

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
            GameEventManager.AddListener<StealthStateChangedEvent>(OnStealthStateChanged);
            GameEventManager.AddListener<LocationDiscoveredEvent>(OnLocationDiscovered);

            if (_uiDocument != null && _uiDocument.rootVisualElement != null)
            {
                BindElements();
            }
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.RemoveListener<PlayerStaminaExhaustedEvent>(OnStaminaExhausted);
            GameEventManager.RemoveListener<StealthStateChangedEvent>(OnStealthStateChanged);
            GameEventManager.RemoveListener<LocationDiscoveredEvent>(OnLocationDiscovered);

            StopAllCoroutines();
            _vignetteCoroutine = null;
            _exhaustionCoroutine = null;
            _discoveryCoroutine = null;
        }

        private void Update()
        {
            EnsurePlayerBound();
            UpdateStatusBars();
            UpdateCompass();
            UpdateInteractionPrompt();
        }

        #endregion

        #region Setup & Binding

        private void SetupUIDocument()
        {
            if (_uiDocument == null) return;

            _uiDocument.sortingOrder = 10;
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

            // Status Bars
            _healthFill = _root.Q<VisualElement>("HealthBarFill");
            _healthLabel = _root.Q<Label>("HealthLabel");
            _magickaFill = _root.Q<VisualElement>("MagickaBarFill");
            _magickaLabel = _root.Q<Label>("MagickaLabel");
            _staminaFill = _root.Q<VisualElement>("StaminaBarFill");
            _staminaBar = _root.Q<VisualElement>("StaminaBar");
            _staminaLabel = _root.Q<Label>("StaminaLabel");

            // Reticle & Sneak Eye
            _standardReticle = _root.Q<VisualElement>("StandardReticle");
            _sneakEyeContainer = _root.Q<VisualElement>("SneakEyeContainer");
            _sneakEyePupil = _root.Q<VisualElement>("SneakEyePupil");
            _sneakEyeStatus = _root.Q<Label>("SneakEyeStatus");

            // Interaction
            _promptContainer = _root.Q<VisualElement>("PromptContainer");
            _promptLabel = _root.Q<Label>("PromptLabel");

            // Compass & Discovery
            _compassTape = _root.Q<VisualElement>("CompassTape");
            _compassMarkersLayer = _root.Q<VisualElement>("CompassMarkersLayer");
            _discoveryBanner = _root.Q<VisualElement>("DiscoveryBanner");
            _discoveryText = _root.Q<Label>("DiscoveryText");

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
            UpdateMagickaBar();
            UpdateStaminaBar();
        }

        private void UpdateHealthBar()
        {
            var health = _playerAttributes.GetAttribute("Health");
            if (health == null) return;

            float pct = Mathf.Clamp01((float)health.CurrentValue / Mathf.Max(1, health.MaximumValue)) * 100f;
            if (_healthFill != null) _healthFill.style.width = Length.Percent(pct);
            if (_healthLabel != null) _healthLabel.text = $"{health.CurrentValue}/{health.MaximumValue}";
        }

        private void UpdateMagickaBar()
        {
            var magicka = _playerAttributes.GetAttribute("Magicka") ?? _playerAttributes.GetAttribute("Mana");
            if (magicka == null) return;

            float pct = Mathf.Clamp01((float)magicka.CurrentValue / Mathf.Max(1, magicka.MaximumValue)) * 100f;
            if (_magickaFill != null) _magickaFill.style.width = Length.Percent(pct);
            if (_magickaLabel != null) _magickaLabel.text = $"{magicka.CurrentValue}/{magicka.MaximumValue}";
        }

        private void UpdateStaminaBar()
        {
            var stamina = _playerAttributes.GetAttribute("Stamina");
            if (stamina == null) return;

            float pct = Mathf.Clamp01((float)stamina.CurrentValue / Mathf.Max(1, stamina.MaximumValue)) * 100f;
            if (_staminaFill != null) _staminaFill.style.width = Length.Percent(pct);
            if (_staminaLabel != null) _staminaLabel.text = $"{stamina.CurrentValue}/{stamina.MaximumValue}";
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

        #region Compass Updates

        private void UpdateCompass()
        {
            if (!ServiceLocator.TryGet<CompassService>(out var compass)) return;

            // Slide cardinal tape based on player heading (360 degrees tape)
            if (_compassTape != null)
            {
                // Full tape spans 360 degrees across ~560 pixels
                float normalizedHeading = compass.CurrentHeading / 360f;
                float tapeOffset = -normalizedHeading * 320f;
                _compassTape.style.left = tapeOffset;
            }

            // Update POI Marker Pips on compass
            if (_compassMarkersLayer != null)
            {
                UpdateCompassMarkers(compass);
            }
        }

        private void UpdateCompassMarkers(CompassService compass)
        {
            var markers = compass.Markers;
            int neededPips = markers.Count;

            while (_markerPips.Count < neededPips)
            {
                var pip = new VisualElement();
                pip.AddToClassList("compass-marker-pip");
                _compassMarkersLayer.Add(pip);
                _markerPips.Add(pip);
            }

            float stripWidth = _compassMarkersLayer.resolvedStyle.width;
            if (stripWidth <= 0f) stripWidth = 440f;

            int index = 0;
            foreach (var marker in markers)
            {
                var pip = _markerPips[index];

                if (compass.TryGetNormalizedCompassOffset(marker.GetPosition(), out float normOffset, out float distance))
                {
                    // normOffset is -0.5 to +0.5 where 0 is center
                    float pipX = (normOffset + 0.5f) * stripWidth;
                    pip.style.left = pipX;
                    pip.style.display = DisplayStyle.Flex;

                    // Apply visual styling per marker type
                    ApplyMarkerPipStyle(pip, marker.Type);
                }
                else
                {
                    pip.style.display = DisplayStyle.None;
                }

                index++;
            }

            // Hide unused pooled pips
            for (int i = index; i < _markerPips.Count; i++)
            {
                _markerPips[i].style.display = DisplayStyle.None;
            }
        }

        private void ApplyMarkerPipStyle(VisualElement pip, CompassPOIType type)
        {
            pip.RemoveFromClassList("compass-marker-pip--quest");
            pip.RemoveFromClassList("compass-marker-pip--enemy");

            if (type == CompassPOIType.QuestMarker)
            {
                pip.AddToClassList("compass-marker-pip--quest");
            }
            else if (type == CompassPOIType.Enemy)
            {
                pip.AddToClassList("compass-marker-pip--enemy");
            }
        }

        #endregion

        #region Event Handling & Visual Feedback

        private void OnStealthStateChanged(StealthStateChangedEvent evt)
        {
            if (evt == null) return;

            if (evt.IsCrouching)
            {
                if (_standardReticle != null) _standardReticle.style.display = DisplayStyle.None;
                if (_sneakEyeContainer != null) _sneakEyeContainer.style.display = DisplayStyle.Flex;

                // Adjust pupil height based on detection ratio
                if (_sneakEyePupil != null)
                {
                    float pupilHeight = Mathf.Lerp(2f, 16f, evt.DetectionRatio);
                    _sneakEyePupil.style.height = pupilHeight;
                }

                if (_sneakEyeStatus != null)
                {
                    _sneakEyeStatus.text = evt.State switch
                    {
                        StealthState.Hidden => "HIDDEN",
                        StealthState.Searching => "SEARCHING",
                        StealthState.Detected => "DETECTED",
                        _ => "HIDDEN"
                    };
                }
            }
            else
            {
                if (_standardReticle != null) _standardReticle.style.display = DisplayStyle.Flex;
                if (_sneakEyeContainer != null) _sneakEyeContainer.style.display = DisplayStyle.None;
            }
        }

        private void OnLocationDiscovered(LocationDiscoveredEvent evt)
        {
            if (evt == null || _discoveryBanner == null || _discoveryText == null) return;

            if (_discoveryCoroutine != null)
            {
                StopCoroutine(_discoveryCoroutine);
            }
            _discoveryCoroutine = StartCoroutine(ShowDiscoveryBannerRoutine(evt.LocationName));
        }

        private IEnumerator ShowDiscoveryBannerRoutine(string locationName)
        {
            _discoveryText.text = $"✦ {locationName.ToUpperInvariant()} DISCOVERED ✦";
            _discoveryBanner.AddToClassList(DiscoveryBannerVisibleClass);

            // Play discovery notification chime
            if (ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.PlayInteract();
            }

            yield return new WaitForSeconds(3.5f);

            _discoveryBanner.RemoveFromClassList(DiscoveryBannerVisibleClass);
            _discoveryCoroutine = null;
        }

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
