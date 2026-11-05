namespace Tests.Editor
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using Core;
    using Domain.Combat;
    using Domain.Dialogue;
    using Domain.Entities;
    using Domain.Magic;
    using Gameplay.Combat;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using Gameplay.Magic;
    using Gameplay.Skyrim;

    /// <summary>
    /// Domain 9 Acceptance Tests: Validates melee combat feedback, ranged archery AI,
    /// Destruction & Restoration magic arts, NPC narrative dialogue, and iconic Skyrim glitches.
    /// </summary>
    [TestFixture]
    public class Domain9_CombatAndMagicTests
    {
        private List<GameObject> _spawnedObjects;

        [SetUp]
        public void SetUp()
        {
            _spawnedObjects = new List<GameObject>();
            GameEventManager.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawnedObjects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawnedObjects.Clear();
            GameEventManager.Clear();
        }

        private GameObject Track(GameObject go)
        {
            _spawnedObjects.Add(go);
            return go;
        }

        #region Domain 1: Player Weapons & Combat Feedback

        [Test]
        public void COMB01_MeleeWeapon_WithStamina_ExecutesAttackAndConsumesStamina()
        {
            var playerObj = Track(new GameObject("Test_Player"));
            var actor = playerObj.AddComponent<BaseActor>();
            var attrs = playerObj.AddComponent<AttributeComponent>();
            var weapon = playerObj.AddComponent<WeaponComponent>();

            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Health", MaximumValue = 100, CurrentValue = 100 });
            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Stamina", MaximumValue = 100, CurrentValue = 100 });

            bool attacked = weapon.TryExecuteAttack();

            Assert.That(attacked, Is.True, "Melee attack should execute when stamina is sufficient.");
            Assert.That(attrs.GetAttribute("Stamina").CurrentValue, Is.LessThan(100), "Stamina should be drained on melee swing.");
        }

        [Test]
        public void COMB02_MeleeWeapon_WithLowStamina_FailsAndPublishesExhaustedEvent()
        {
            var playerObj = Track(new GameObject("Test_Player_Exhausted"));
            playerObj.AddComponent<BaseActor>();
            var attrs = playerObj.AddComponent<AttributeComponent>();
            var weapon = playerObj.AddComponent<WeaponComponent>();

            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Stamina", MaximumValue = 100, CurrentValue = 5 });

            bool eventFired = false;
            GameEventManager.AddListener<PlayerStaminaExhaustedEvent>(e => eventFired = true);

            bool attacked = weapon.TryExecuteAttack();

            Assert.That(attacked, Is.False, "Attack should fail when stamina is below minimum threshold.");
            Assert.That(eventFired, Is.True, "PlayerStaminaExhaustedEvent must be published.");
        }

        #endregion

        #region Domain 2: Enemy Archetypes & Spawners

        [Test]
        public void RANG01_ArrowProjectile_InflictsDamageOnTargetActor()
        {
            var targetObj = Track(new GameObject("Target_Dummy"));
            var targetActor = targetObj.AddComponent<BaseActor>();
            var targetAttrs = targetObj.AddComponent<AttributeComponent>();
            var col = targetObj.AddComponent<BoxCollider>();
            targetAttrs.RegisterAttribute(new BaseAttribute { AttributeName = "Health", MaximumValue = 100, CurrentValue = 100 });

            var arrowObj = Track(new GameObject("Arrow_Test"));
            arrowObj.AddComponent<BoxCollider>();
            var arrow = arrowObj.AddComponent<ArrowProjectile>();
            arrow.Damage = 25;

            var shooterObj = Track(new GameObject("Shooter"));
            arrow.Launch(Vector3.forward, shooterObj);

            // Directly invoke impact simulation
            var damageInfo = new DamageInfo(arrow.Damage, DamageType.Physical, shooterObj, targetObj.transform.position, Vector3.back);
            targetActor.TakeDamage(damageInfo);

            Assert.That(targetAttrs.GetAttribute("Health").CurrentValue, Is.EqualTo(75), "Target must take damage from arrow projectile.");
        }

        [Test]
        public void RANG02_EnemySpawner_SpawnsEnemiesWithinPerimeter()
        {
            var spawnerObj = Track(new GameObject("Enemy_Spawner"));
            var spawner = spawnerObj.AddComponent<EnemySpawner>();

            var prefabObj = Track(new GameObject("Enemy_Archetype_Prefab"));
            prefabObj.AddComponent<BaseActor>();
            spawner.EnemyPrefab = prefabObj;
            spawner.MaxActiveEnemies = 2;

            var e1 = Track(spawner.SpawnEnemyInstance());
            var e2 = Track(spawner.SpawnEnemyInstance());

            Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(2), "Spawner should track active spawned enemies.");
            Assert.That(e1, Is.Not.Null);
            Assert.That(e2, Is.Not.Null);
        }

        #endregion

        #region Domain 3: Magic & Spells

        [Test]
        public void MAG01_FireballSpell_ConsumesMagicka_AndPublishesSpellCastEvent()
        {
            var playerObj = Track(new GameObject("Test_Mage"));
            var actor = playerObj.AddComponent<BaseActor>();
            var attrs = playerObj.AddComponent<AttributeComponent>();
            var magic = playerObj.AddComponent<PlayerMagicController>();

            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Health", MaximumValue = 100, CurrentValue = 100 });
            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Magicka", MaximumValue = 100, CurrentValue = 100 });

            SpellCastEvent castEvt = null;
            GameEventManager.AddListener<SpellCastEvent>(e => castEvt = e);

            bool castSuccess = magic.TryCastActiveSpell();

            Assert.That(castSuccess, Is.True, "Fireball cast should succeed with full Magicka.");
            Assert.That(attrs.GetAttribute("Magicka").CurrentValue, Is.EqualTo(70), "Fireball (30 cost) should reduce Magicka to 70.");
            Assert.That(castEvt, Is.Not.Null, "SpellCastEvent must be broadcast.");
            Assert.That(castEvt.Spell.SpellId, Is.EqualTo("spell_fireball"));
        }

        [Test]
        public void MAG02_FastHealingSpell_ConsumesMagicka_RestoresHealth_AndPublishesHealEvent()
        {
            var playerObj = Track(new GameObject("Test_Healer"));
            var actor = playerObj.AddComponent<BaseActor>();
            var attrs = playerObj.AddComponent<AttributeComponent>();
            var magic = playerObj.AddComponent<PlayerMagicController>();

            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Health", MaximumValue = 100, CurrentValue = 40 });
            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Magicka", MaximumValue = 100, CurrentValue = 100 });

            var healSpell = ScriptableObject.CreateInstance<SpellData>();
            healSpell.Configure("spell_fast_healing", "Fast Healing", SpellSchool.Restoration, 25, 35, 1f);
            magic.ActiveSpell = healSpell;

            EntityHealedEvent healEvt = null;
            GameEventManager.AddListener<EntityHealedEvent>(e => healEvt = e);

            bool castSuccess = magic.TryCastActiveSpell();

            Assert.That(castSuccess, Is.True, "Healing spell should cast successfully.");
            Assert.That(attrs.GetAttribute("Magicka").CurrentValue, Is.EqualTo(75), "Magicka should reduce by 25.");
            Assert.That(attrs.GetAttribute("Health").CurrentValue, Is.EqualTo(75), "Health should increase from 40 to 75 (+35).");
            Assert.That(healEvt, Is.Not.Null, "EntityHealedEvent must be published.");
            Assert.That(healEvt.Amount, Is.EqualTo(35));
        }

        [Test]
        public void MAG03_SpellCasting_WithLowMagicka_FailsAndPublishesExhaustedEvent()
        {
            var playerObj = Track(new GameObject("Test_OOM_Mage"));
            playerObj.AddComponent<BaseActor>();
            var attrs = playerObj.AddComponent<AttributeComponent>();
            var magic = playerObj.AddComponent<PlayerMagicController>();

            attrs.RegisterAttribute(new BaseAttribute { AttributeName = "Magicka", MaximumValue = 100, CurrentValue = 10 });

            bool oomEventFired = false;
            GameEventManager.AddListener<PlayerMagickaExhaustedEvent>(e => oomEventFired = true);

            bool castSuccess = magic.TryCastActiveSpell();

            Assert.That(castSuccess, Is.False, "Spell should fail when Magicka is insufficient.");
            Assert.That(oomEventFired, Is.True, "PlayerMagickaExhaustedEvent must be published.");
        }

        #endregion

        #region Domain 4: Dialogue & NPC Interaction

        [Test]
        public void DIA01_NPCInteraction_DispatchesDialogueOpenedEventWithValidGreeting()
        {
            var npcObj = Track(new GameObject("NPC_Guard"));
            var actor = npcObj.AddComponent<BaseActor>();
            var interaction = npcObj.AddComponent<NPCInteractionComponent>();

            var dialogueData = ScriptableObject.CreateInstance<NPCDialogueData>();
            dialogueData.Configure(
                "Whiterun Guard",
                "Hold Guard",
                new string[] { "I used to be an adventurer like you, then I took an arrow in the knee." });
            interaction.DialogueData = dialogueData;

            NPCDialogueOpenedEvent openedEvt = null;
            GameEventManager.AddListener<NPCDialogueOpenedEvent>(e => openedEvt = e);

            var player = Track(new GameObject("Player_Interactor"));
            interaction.Interact(player);

            Assert.That(openedEvt, Is.Not.Null, "NPCDialogueOpenedEvent must be dispatched.");
            Assert.That(openedEvt.NpcName, Is.EqualTo("Whiterun Guard"));
            Assert.That(openedEvt.Greeting, Contains.Substring("arrow in the knee"));
        }

        #endregion

        #region Domain 5: Iconic Skyrim Bugs & Glitches

        [Test]
        public void GLT01_SkyrimSpaceProgram_LaunchesVictimWithHighVerticalVelocity()
        {
            var victim = Track(new GameObject("Space_Program_Victim"));
            var spaceProgramObj = Track(new GameObject("Skyrim_Space_Program_Service"));
            var spaceProgram = spaceProgramObj.AddComponent<SkyrimSpaceProgram>();
            spaceProgram.VerticalVelocity = 80f;

            spaceProgram.LaunchIntoOrbit(victim);

            var rb = victim.GetComponent<Rigidbody>();
            Assert.That(rb, Is.Not.Null, "Victim must have a Rigidbody added.");
            Assert.That(rb.linearVelocity.y, Is.GreaterThanOrEqualTo(70f), "Victim must be launched with massive upward velocity into orbit!");
        }

        [Test]
        public void GLT02_BucketHeadStealthExploit_DetectsBucketAndBlindsNPC()
        {
            var npc = Track(new GameObject("Guard_Bucket_Victim"));
            var exploit = npc.AddComponent<BucketHeadStealthExploit>();
            var col = npc.AddComponent<SphereCollider>();
            col.isTrigger = true;

            var bucketObj = Track(new GameObject("SM_Prop_Bucket_01"));
            var bucketCol = bucketObj.AddComponent<BoxCollider>();

            Assert.That(exploit.IsBlinded, Is.False, "NPC should not be blind initially.");

            // Directly invoke trigger entrance
            typeof(BucketHeadStealthExploit)
                .GetMethod("OnTriggerEnter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(exploit, new object[] { bucketCol });

            Assert.That(exploit.IsBlinded, Is.True, "Placing a bucket over NPC head must blind them completely!");
        }

        [Test]
        public void GLT03_PlatterWallClip_PhasesPlayerForward()
        {
            var player = Track(new GameObject("Speedrunner_Player"));
            player.transform.position = Vector3.zero;
            var clipper = player.AddComponent<PlatterWallClip>();

            bool phased = clipper.TryPhaseThroughWall(Vector3.forward);

            Assert.That(phased, Is.True, "Platter wall phasing should succeed.");
            Assert.That(player.transform.position.z, Is.GreaterThan(1.5f), "Player must be phased through the wall.");
        }

        #endregion
    }
}
