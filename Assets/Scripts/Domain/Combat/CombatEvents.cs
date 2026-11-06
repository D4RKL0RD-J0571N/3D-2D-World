namespace Domain.Combat
{
    using Core;
    using System.Collections.Generic;

    /// <summary>
    /// Published when an entity attempts a combat action without sufficient stamina.
    /// Used by UI to trigger exhaustion feedback.
    /// </summary>
    public sealed class PlayerStaminaExhaustedEvent : GameEvent
    {
    }

    /// <summary>
    /// Published when an attack lands while the attacker is undetected in stealth.
    /// Used for Skyrim-style critical damage multiplication and UI notifications.
    /// </summary>
    public sealed class SneakAttackTriggeredEvent : GameEvent
    {
        public UnityEngine.GameObject Attacker;
        public Gameplay.Entities.BaseActor Target;
        public float Multiplier;
        public int FinalDamage;

        public SneakAttackTriggeredEvent(UnityEngine.GameObject attacker, Gameplay.Entities.BaseActor target, float multiplier, int finalDamage)
        {
            Attacker = attacker;
            Target = target;
            Multiplier = multiplier;
            FinalDamage = finalDamage;
        }
    }

    /// <summary>
    /// Published when an entity attempts a magical cast without sufficient magicka.
    /// Used by UI to trigger exhaustion feedback.
    /// </summary>
    public sealed class PlayerMagickaExhaustedEvent : GameEvent
    {
    }

    /// <summary>
    /// Published when a spell is successfully cast by an actor.
    /// </summary>
    public sealed class SpellCastEvent : GameEvent
    {
        public UnityEngine.GameObject Caster;
        public Domain.Magic.SpellData Spell;

        public SpellCastEvent(UnityEngine.GameObject caster, Domain.Magic.SpellData spell)
        {
            Caster = caster;
            Spell = spell;
        }
    }

    /// <summary>
    /// Published when dialogue opens with an NPC.
    /// </summary>
    public sealed class NPCDialogueOpenedEvent : GameEvent
    {
        public string NpcName;
        public string Title;
        public string Greeting;
        public IReadOnlyList<Domain.Dialogue.DialogueTopic> Topics;

        public NPCDialogueOpenedEvent(string npcName, string title, string greeting, IReadOnlyList<Domain.Dialogue.DialogueTopic> topics)
        {
            NpcName = npcName;
            Title = title;
            Greeting = greeting;
            Topics = topics;
        }
    }

    /// <summary>
    /// Published when dialogue with an NPC is closed.
    /// </summary>
    public sealed class NPCDialogueClosedEvent : GameEvent
    {
    }
}
