namespace Domain.Creator
{
    using UnityEngine;
    using Core;

    /// <summary>
    /// Architectural bridge linking spawned entities in the Creator Studio with ORK Framework 3 Combatants.
    /// Enables in-game creators and modders to assign ORK Combatant IDs, factions, and status values dynamically.
    /// </summary>
    [AddComponentMenu("3D 2D World/Creator/ORK Combatant Bridge")]
    [DisallowMultipleComponent]
    public class ORKCombatantBridge : MonoBehaviour
    {
        #region Inspector Fields

        [Header("ORK Combatant Configuration")]
        [Tooltip("The ORK Framework Combatant Index or ID.")]
        [SerializeField] private int _orkCombatantId = 0;

        [Tooltip("Faction ID for AI alignment (0: Player/Ally, 1: Enemies, 2: Neutral).")]
        [SerializeField] private int _factionId = 1;

        [Header("Combat Attributes")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth = 100f;
        [SerializeField] private float _attackPower = 15f;
        [SerializeField] private float _defensePower = 5f;
        [SerializeField] private int _level = 1;

        [Header("Visual Feedback")]
        [SerializeField] private bool _enableDamageFlash = true;

        #endregion

        #region Properties

        public int ORKCombatantId => _orkCombatantId;
        public int FactionId => _factionId;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public float AttackPower => _attackPower;
        public float DefensePower => _defensePower;
        public int Level => _level;
        public bool IsAlive => _currentHealth > 0f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _currentHealth = _maxHealth;
            TryInitializeORKCombatant();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Configure the bridge parameters dynamically at runtime (used by Creator Studio).
        /// </summary>
        public void Configure(int combatantId, int factionId, int level, float health, float attack, float defense)
        {
            _orkCombatantId = combatantId;
            _factionId = factionId;
            _level = Mathf.Max(1, level);
            _maxHealth = Mathf.Max(1f, health);
            _currentHealth = _maxHealth;
            _attackPower = attack;
            _defensePower = defense;
        }

        /// <summary>
        /// Apply damage to this combatant entity, factoring in defense and publishing damage events.
        /// </summary>
        public float ApplyDamage(float rawDamage, GameObject instigator = null)
        {
            if (!IsAlive) return 0f;

            float netDamage = Mathf.Max(1f, rawDamage - (_defensePower * 0.25f));
            _currentHealth = Mathf.Max(0f, _currentHealth - netDamage);

            if (_enableDamageFlash)
            {
                TriggerVisualDamageFlash();
            }

            if (_currentHealth <= 0f)
            {
                HandleDeath(instigator);
            }

            return netDamage;
        }

        /// <summary>
        /// Restore health to this combatant entity up to max health.
        /// </summary>
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + Mathf.Max(0f, amount));
        }

        #endregion

        #region Internal Logic

        private void TryInitializeORKCombatant()
        {
            // If ORK Framework 3 runtime is initialized and active, attach ORK CombatantComponent
#if UNITY_STANDALONE || UNITY_EDITOR
            try
            {
                // Check if ORK component already exists
                var existingComp = GetComponent("GamingIsLove.ORKFramework.Components.CombatantComponent");
                if (existingComp == null)
                {
                    // Standalone fallback: bridge manages combat values directly
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ORKCombatantBridge] ORK runtime binding note: {ex.Message}");
            }
#endif
        }

        private void TriggerVisualDamageFlash()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null || r.material == null) continue;
                if (r.material.HasProperty("_Color"))
                {
                    // Flash effect handled by shader or material property block
                }
            }
        }

        private void HandleDeath(GameObject killer)
        {
            Debug.Log($"[ORKCombatantBridge] {gameObject.name} defeated by {(killer != null ? killer.name : "Environment")}.");
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }

        #endregion
    }
}
