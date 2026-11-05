namespace Gameplay.Magic
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Magic;
    using Gameplay.Entities;

    /// <summary>
    /// Player spellcasting controller managing Destruction and Restoration arts.
    /// Handles Magicka depletion, cooldown gating, fireball projectile launch, and healing auras.
    /// </summary>
    [AddComponentMenu("3D2DWorld/Magic/Player Magic Controller")]
    [RequireComponent(typeof(BaseActor))]
    [RequireComponent(typeof(AttributeComponent))]
    public class PlayerMagicController : MonoBehaviour
    {
        #region Fields

        [Header("Equipped Spells")]
        [SerializeField] private SpellData _activeSpell;
        [SerializeField] private List<SpellData> _knownSpells = new List<SpellData>();

        [Header("Cast Offsets")]
        [SerializeField] private Vector3 _castOffset = new Vector3(-0.3f, -0.2f, 0.6f);

        [Header("Feedback Prefabs")]
        [SerializeField] private GameObject _fireballPrefab;
        [SerializeField] private GameObject _healingAuraPrefab;
        [SerializeField] private AudioClip _fireCastClip;
        [SerializeField] private AudioClip _healCastClip;
        [SerializeField] private AudioClip _fizzleClip;

        private BaseActor _actor;
        private AttributeComponent _attributes;
        private UnityEngine.Camera _cachedCamera;
        private float _lastCastTime = -10f;
        private bool _isCasting;

        #endregion

        #region Properties

        public SpellData ActiveSpell
        {
            get
            {
                EnsureInitialized();
                return _activeSpell;
            }
            set => _activeSpell = value;
        }

        public IReadOnlyList<SpellData> KnownSpells
        {
            get
            {
                EnsureInitialized();
                return _knownSpells;
            }
        }

        public bool CanCast
        {
            get
            {
                EnsureInitialized();
                if (_isCasting || _activeSpell == null) return false;
                if (Time.time < _lastCastTime + _activeSpell.Cooldown) return false;

                var magicka = _attributes?.GetAttribute("Magicka") ?? _attributes?.GetAttribute("Mana");
                if (magicka == null || magicka.CurrentValue < _activeSpell.MagickaCost) return false;

                return true;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _actor = GetComponent<BaseActor>();
            _attributes = GetComponent<AttributeComponent>();
            EnsureDefaultAssets();
            InitializeDefaultSpells();
        }

        private void Start()
        {
            ResolveCamera();
        }

        private void Update()
        {
            HandleSpellSelectionInput();
            HandleCastInput();
        }

        #endregion

        #region Input & Selection

        private void HandleSpellSelectionInput()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame && _knownSpells.Count > 0)
            {
                _activeSpell = _knownSpells[0];
            }
            else if (keyboard.digit2Key.wasPressedThisFrame && _knownSpells.Count > 1)
            {
                _activeSpell = _knownSpells[1];
            }
            else if (keyboard.qKey.wasPressedThisFrame && _knownSpells.Count > 1)
            {
                CycleNextSpell();
            }
        }

        private void HandleCastInput()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked)
            {
                TryCastActiveSpell();
            }
        }

        private void CycleNextSpell()
        {
            if (_knownSpells.Count <= 1) return;
            int idx = _knownSpells.IndexOf(_activeSpell);
            _activeSpell = _knownSpells[(idx + 1) % _knownSpells.Count];
        }

        #endregion

        #region Casting Logic

        public void EnsureInitialized()
        {
            if (_actor == null) _actor = GetComponent<BaseActor>();
            if (_attributes == null) _attributes = GetComponent<AttributeComponent>();
            if (_knownSpells == null) _knownSpells = new List<SpellData>();
            if (_knownSpells.Count == 0) InitializeDefaultSpells();
            if (_activeSpell == null && _knownSpells.Count > 0) _activeSpell = _knownSpells[0];
        }

        /// <summary>
        /// Attempts to cast the currently active spell, consuming Magicka and triggering effects.
        /// </summary>
        public bool TryCastActiveSpell()
        {
            EnsureInitialized();

            if (_activeSpell == null) return false;

            var magicka = _attributes?.GetAttribute("Magicka") ?? _attributes?.GetAttribute("Mana");
            if (magicka == null || magicka.CurrentValue < _activeSpell.MagickaCost)
            {
                PlayFizzleFeedback();
                GameEventManager.Publish(new PlayerMagickaExhaustedEvent());
                return false;
            }

            if (Time.time < _lastCastTime + _activeSpell.Cooldown)
                return false;

            ExecuteCast();
            return true;
        }

        private void ExecuteCast()
        {
            _lastCastTime = Time.time;
            _attributes.ApplyDamage("Magicka", _activeSpell.MagickaCost);
            GameEventManager.Publish(new SpellCastEvent(gameObject, _activeSpell));

            if (_activeSpell.School == SpellSchool.Destruction)
            {
                CastDestructionSpell();
            }
            else if (_activeSpell.School == SpellSchool.Restoration)
            {
                CastRestorationSpell();
            }
        }

        private void CastDestructionSpell()
        {
            PlayAudio2D(_fireCastClip, 0.85f);
            ResolveCamera();

            Vector3 origin = _cachedCamera != null ? _cachedCamera.transform.position : transform.position + Vector3.up * 1.5f;
            Vector3 forward = _cachedCamera != null ? _cachedCamera.transform.forward : transform.forward;
            Vector3 spawnPos = origin + forward * 0.8f;

            GameObject fireball = null;
            if (_fireballPrefab != null)
            {
                fireball = Instantiate(_fireballPrefab, spawnPos, Quaternion.LookRotation(forward));
            }
            else
            {
                fireball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fireball.transform.localScale = Vector3.one * 0.4f;
                fireball.transform.position = spawnPos;
            }

            var proj = fireball.GetComponent<SpellProjectile>() ?? fireball.AddComponent<SpellProjectile>();
            proj.Damage = _activeSpell.Power;
            proj.Caster = gameObject;
        }

        private void CastRestorationSpell()
        {
            PlayAudio2D(_healCastClip, 0.9f);

            int healAmount = _activeSpell.Power;
            _attributes.Heal("Health", healAmount);

            if (_healingAuraPrefab != null)
            {
                GameObject aura = Instantiate(_healingAuraPrefab, transform.position, Quaternion.identity);
                aura.transform.parent = transform;
                Destroy(aura, 2.5f);
            }
        }

        private void PlayFizzleFeedback()
        {
            PlayAudio2D(_fizzleClip, 0.7f);
        }

        private void PlayAudio2D(AudioClip clip, float vol)
        {
            if (clip != null && ServiceLocator.TryGet<Systems.Audio.AudioService>(out var audio))
            {
                audio.Play2D(clip, vol);
            }
        }

        private void ResolveCamera()
        {
            if (_cachedCamera == null)
            {
                _cachedCamera = GetComponentInChildren<UnityEngine.Camera>() ?? UnityEngine.Camera.main;
            }
        }

        #endregion

        #region Default Spells & Asset Wiring

        private void InitializeDefaultSpells()
        {
            if (_knownSpells.Count == 0)
            {
                var fireball = ScriptableObject.CreateInstance<SpellData>();
                fireball.Configure("spell_fireball", "Fireball", SpellSchool.Destruction, 30, 40, 0.8f);

                var heal = ScriptableObject.CreateInstance<SpellData>();
                heal.Configure("spell_fast_healing", "Fast Healing", SpellSchool.Restoration, 25, 35, 1.2f);

                _knownSpells.Add(fireball);
                _knownSpells.Add(heal);
                if (_activeSpell == null)
                {
                    _activeSpell = fireball;
                }
            }
        }

        private void EnsureDefaultAssets()
        {
#if UNITY_EDITOR
            if (_fireCastClip == null)
            {
                _fireCastClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckWoosh.wav");
            }
            if (_healCastClip == null)
            {
                _healCastClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Barbarians/Sounds/FeelBarbarianThunder.wav");
            }
            if (_fizzleClip == null)
            {
                _fizzleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Feel/FeelDemos/Duck/Sounds/FeelDuckDenied.wav");
            }
            if (_fireballPrefab == null)
            {
                _fireballPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/FX/FX_Fire_01.prefab");
            }
            if (_healingAuraPrefab == null)
            {
                _healingAuraPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonFantasyKingdom/Prefabs/FX/FX_Magic_Lights_01.prefab");
            }
#endif
        }

        #endregion
    }
}
