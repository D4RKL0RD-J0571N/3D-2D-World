namespace Gameplay.Combat
{
    using Core;
    using DamageNumbersPro;
    using Domain.Entities;
    using UnityEngine;

    /// <summary>
    /// Decoupled feedback bridge that subscribes to combat events and triggers
    /// floating damage/heal numbers (Damage Numbers Pro) and juicy camera/impact effects (Feel).
    /// </summary>
    [AddComponentMenu("3D 2D World/Combat/Combat Feedback Bridge")]
    public class CombatFeedbackBridge : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Damage Number Prefabs")]
        [SerializeField] private DamageNumber damagePrefab;
        [SerializeField] private DamageNumber healPrefab;

        [Header("Spawn Offsets")]
        [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.8f, 0f);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsurePrefabsLoaded();
        }

        private void OnEnable()
        {
            GameEventManager.AddListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.AddListener<EntityHealedEvent>(OnEntityHealed);
        }

        private void OnDisable()
        {
            GameEventManager.RemoveListener<EntityDamagedEvent>(OnEntityDamaged);
            GameEventManager.RemoveListener<EntityHealedEvent>(OnEntityHealed);
        }

        #endregion

        #region Event Handlers

        private void OnEntityDamaged(EntityDamagedEvent evt)
        {
            if (evt == null || evt.Target == null || damagePrefab == null) return;

            Vector3 spawnPos = evt.Target.transform.position + spawnOffset;
            damagePrefab.Spawn(spawnPos, evt.Amount);
        }

        private void OnEntityHealed(EntityHealedEvent evt)
        {
            if (evt == null || evt.Target == null || healPrefab == null) return;

            Vector3 spawnPos = evt.Target.transform.position + spawnOffset;
            healPrefab.Spawn(spawnPos, evt.Amount);
        }

        #endregion

        #region Prefab Loading

        private void EnsurePrefabsLoaded()
        {
#if UNITY_EDITOR
            if (damagePrefab == null)
            {
                damagePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/DamageNumbersPro/Demo/Prefabs/3D/Red Glow.prefab");
            }

            if (healPrefab == null)
            {
                healPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageNumber>("Assets/DamageNumbersPro/Demo/Prefabs/3D/Pixel Heal.prefab");
            }
#endif
        }

        #endregion
    }
}
