namespace Domain.Skyrim
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Scenario definition for the "Alternate Start - Live Another Life" system.
    /// Defines starting spawn location, narrative introduction, starter gear, and starter spells.
    /// </summary>
    [Serializable]
    public class AlternateStartScenario
    {
        public string ScenarioId;
        public string Title;
        public string MaraPrompt;
        public string Description;
        public Vector3 SpawnPosition;
        public Vector3 SpawnRotationEuler;
        public List<string> StarterItems = new();
        public List<string> StarterSpells = new();
        public int StarterGold;

        public AlternateStartScenario() { }

        public AlternateStartScenario(
            string scenarioId,
            string title,
            string maraPrompt,
            string description,
            Vector3 spawnPos,
            Vector3 spawnRot,
            int starterGold = 0)
        {
            ScenarioId = scenarioId;
            Title = title;
            MaraPrompt = maraPrompt;
            Description = description;
            SpawnPosition = spawnPos;
            SpawnRotationEuler = spawnRot;
            StarterGold = starterGold;
        }

        /// <summary>
        /// Generates the canonical Skyrim Alternate Start choices.
        /// </summary>
        public static IReadOnlyList<AlternateStartScenario> GetDefaultScenarios()
        {
            return new List<AlternateStartScenario>
            {
                new AlternateStartScenario(
                    "border_crossing",
                    "Caught Crossing the Border (Default Helgen Start)",
                    "I was caught crossing the border into Skyrim...",
                    "You awaken alongside prisoners bound for Helgen, destined to discover your dragon heritage.",
                    new Vector3(0f, 0.5f, 0f),
                    new Vector3(0f, 0f, 0f),
                    10)
                {
                    StarterItems = { "nordic_rpg:item/iron_sword", "nordic_rpg:item/roughspun_tunic" },
                    StarterSpells = { "Flames", "Fast Healing" }
                },
                new AlternateStartScenario(
                    "patron_inn",
                    "Patron at an Inn (Riverwood Sleeping Giant)",
                    "I am a patron staying at the Sleeping Giant Inn in Riverwood...",
                    "You have rented a warm bed at Delphine's tavern in Riverwood with coin in your purse.",
                    new Vector3(2f, 0.5f, 5f),
                    new Vector3(0f, 90f, 0f),
                    75)
                {
                    StarterItems = { "nordic_rpg:item/iron_dagger", "nordic_rpg:item/fine_clothes", "nordic_rpg:item/ale" },
                    StarterSpells = { "Fast Healing" }
                },
                new AlternateStartScenario(
                    "camping_woods",
                    "Camping in the Woods (Wilderness Hunter)",
                    "I am camping out in the wilderness...",
                    "You have set up camp under the pines with a hunting bow and leather armor.",
                    new Vector3(12f, 0.5f, 15f),
                    new Vector3(0f, 220f, 0f),
                    25)
                {
                    StarterItems = { "nordic_rpg:item/hunting_bow", "nordic_rpg:item/iron_arrows", "nordic_rpg:item/leather_armor" }
                },
                new AlternateStartScenario(
                    "necromancer_lair",
                    "Necromancer in a Hidden Lair",
                    "I practice the dark arts in a secluded ruin...",
                    "You dwell in an ancient crypt surrounded by the skeletal dead.",
                    new Vector3(0f, 0.5f, 25f),
                    new Vector3(0f, 180f, 0f),
                    50)
                {
                    StarterItems = { "nordic_rpg:item/necromancer_robes", "nordic_rpg:item/soul_gem" },
                    StarterSpells = { "Fireball", "Raise Zombie" }
                },
                new AlternateStartScenario(
                    "college_apprentice",
                    "Apprentice at the College",
                    "I am an aspiring student of the arcane...",
                    "You have arrived to study magic with novice robes and spell tomes.",
                    new Vector3(-8f, 0.5f, 12f),
                    new Vector3(0f, 45f, 0f),
                    100)
                {
                    StarterItems = { "nordic_rpg:item/mage_robes_novice", "nordic_rpg:item/potion_magicka" },
                    StarterSpells = { "Fireball", "Fast Healing", "Muffle" }
                }
            };
        }
    }
}
