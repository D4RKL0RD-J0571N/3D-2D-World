namespace Domain.Magic
{
    using UnityEngine;

    /// <summary>
    /// School of magic in the Skyrim-style magical arts.
    /// </summary>
    public enum SpellSchool
    {
        Destruction,
        Restoration,
        Alteration,
        Illusion,
        Conjuration
    }

    /// <summary>
    /// Data-driven definition for castable spells.
    /// Defines magicka consumption, cooldowns, projectile prefabs, and impact VFX.
    /// </summary>
    [CreateAssetMenu(fileName = "Spell_", menuName = "3D2DWorld/Magic/Spell Data")]
    public class SpellData : ScriptableObject
    {
        #region Serialized Fields

        [Header("Identity")]
        [SerializeField] private string _spellId = "spell_fireball";
        [SerializeField] private string _displayName = "Fireball";
        [TextArea(2, 4)]
        [SerializeField] private string _description = "A fiery bolt that explodes on impact, dealing area damage.";
        [SerializeField] private SpellSchool _school = SpellSchool.Destruction;

        [Header("Costs & Timing")]
        [SerializeField] private int _magickaCost = 30;
        [SerializeField] private int _power = 40;
        [SerializeField] private float _castTime = 0.4f;
        [SerializeField] private float _cooldown = 0.8f;
        [SerializeField] private float _range = 30f;

        [Header("Prefabs & Visuals")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private GameObject _impactVfxPrefab;
        [SerializeField] private Color _spellColor = new Color(1f, 0.4f, 0.1f);

        [Header("Audio")]
        [SerializeField] private AudioClip _castClip;
        [SerializeField] private AudioClip _impactClip;

        #endregion

        #region Properties

        public string SpellId => _spellId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public SpellSchool School => _school;
        public int MagickaCost => _magickaCost;
        public int Power => _power;
        public float CastTime => _castTime;
        public float Cooldown => _cooldown;
        public float Range => _range;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public GameObject ImpactVfxPrefab => _impactVfxPrefab;
        public Color SpellColor => _spellColor;
        public AudioClip CastClip => _castClip;
        public AudioClip ImpactClip => _impactClip;

        #endregion

        #region Configuration Helpers

        /// <summary>
        /// Fluent setter for unit tests and programmatic configuration.
        /// </summary>
        public void Configure(string id, string name, SpellSchool school, int magickaCost, int power, float cooldown)
        {
            _spellId = id;
            _displayName = name;
            _school = school;
            _magickaCost = magickaCost;
            _power = power;
            _cooldown = cooldown;
        }

        #endregion
    }
}
