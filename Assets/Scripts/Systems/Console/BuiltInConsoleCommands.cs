namespace Systems.Console
{
    using System;
    using Core;
    using Core.Content;
    using Core.Ids;
    using Data.Config;
    using Domain.Combat;
    using Domain.Entities;
    using Domain.Skyrim;
    using Gameplay.Combat;
    using Gameplay.Entities;
    using Gameplay.Player;
    using Gameplay.Skyrim;
    using Systems.Skyrim;
    using UnityEngine;

    public static class GodModeState
    {
        public static bool IsGodModeActive;
    }

    public static class NoClipState
    {
        public static bool IsNoClipActive;
    }

    #region God Mode & No Clip

    public class ToggleGodModeCommand : IConsoleCommand
    {
        public string Name => "tgm";
        public string Description => "Toggle God Mode (invulnerability, infinite stamina and magicka).";
        public string Usage => "tgm";

        public bool Execute(string[] args, Action<string> print)
        {
            GodModeState.IsGodModeActive = !GodModeState.IsGodModeActive;
            string state = GodModeState.IsGodModeActive ? "ENABLED" : "DISABLED";
            print?.Invoke($"<color=cyan>God Mode: {state}</color>");
            return true;
        }
    }

    public class ToggleCollisionCommand : IConsoleCommand
    {
        public string Name => "tcl";
        public string Description => "Toggle Collision / No-clip mode for the player.";
        public string Usage => "tcl";

        public bool Execute(string[] args, Action<string> print)
        {
            var player = SKSE.GetPlayer();
            if (player == null)
            {
                print?.Invoke("<color=red>Player not found in scene.</color>");
                return false;
            }

            NoClipState.IsNoClipActive = !NoClipState.IsNoClipActive;
            if (player.TryGetComponent<CharacterController>(out var cc))
            {
                cc.enabled = !NoClipState.IsNoClipActive;
            }

            string state = NoClipState.IsNoClipActive ? "ENABLED (Walk through walls)" : "DISABLED";
            print?.Invoke($"<color=cyan>Collision (tcl): {state}</color>");
            return true;
        }
    }

    #endregion

    #region Inventory & Attributes

    public class AddItemCommand : IConsoleCommand
    {
        public string Name => "player.additem";
        public string Description => "Adds an item to the player's inventory.";
        public string Usage => "player.additem <itemId> [count]";

        public bool Execute(string[] args, Action<string> print)
        {
            if (args.Length == 0)
            {
                print?.Invoke("<color=yellow>Usage: player.additem <itemId> [count]</color>");
                return false;
            }

            string itemId = args[0];
            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? Mathf.Max(1, c) : 1;

            var player = SKSE.GetPlayer();
            if (player == null || !player.TryGetComponent<InventoryComponent>(out var inv))
            {
                print?.Invoke("<color=red>Player or InventoryComponent not found.</color>");
                return false;
            }

            var resId = new ResourceId(itemId.Contains(":") ? itemId : $"nordic_rpg:item/{itemId.ToLowerInvariant()}");
            ItemData targetItem = null;

            if (ServiceLocator.TryGet<IContentService>(out var content))
            {
                content.GetRegistry<ItemData>().TryGet(resId, out targetItem);
            }

            if (targetItem == null)
            {
                targetItem = ScriptableObject.CreateInstance<ItemData>();
                targetItem.Id = itemId;
                targetItem.DisplayName = itemId.Replace('_', ' ');
                targetItem.Value = 25;
                targetItem.Weight = 0.5f;
                targetItem.MaxStackSize = 99;
                targetItem.IsStackable = true;
            }

            inv.AddItem(targetItem, count);
            print?.Invoke($"<color=green>Added {count}x '{targetItem.DisplayName}' to player inventory.</color>");
            return true;
        }
    }

    public class SetActorValueCommand : IConsoleCommand
    {
        public string Name => "player.setav";
        public string Description => "Sets an attribute (health, stamina, magicka) or skill level.";
        public string Usage => "player.setav <attribute/skill> <value>";

        public bool Execute(string[] args, Action<string> print)
        {
            if (args.Length < 2 || !int.TryParse(args[1], out int val))
            {
                print?.Invoke("<color=yellow>Usage: player.setav <attribute/skill> <value></color>");
                return false;
            }

            string target = args[0].ToLowerInvariant();
            var player = SKSE.GetPlayer();
            if (player != null && player.TryGetComponent<AttributeComponent>(out var attrs))
            {
                if (target == "health" || target == "stamina" || target == "magicka")
                {
                    string attrName = char.ToUpperInvariant(target[0]) + target[1..];
                    attrs.Heal(attrName, val);
                    print?.Invoke($"<color=green>Player attribute '{attrName}' set to {val}.</color>");
                    return true;
                }
            }

            var prog = ServiceLocator.Get<SkillProgressionService>();
            if (prog != null)
            {
                var skillId = new ResourceId("nordic_rpg", $"skill/{target}");
                if (prog.AllSkillsById.TryGetValue(skillId, out var rec))
                {
                    rec.Level = Mathf.Clamp(val, 1, 100);
                    print?.Invoke($"<color=green>Skill '{rec.DisplayName}' set to level {rec.Level}.</color>");
                    return true;
                }
            }

            print?.Invoke($"<color=yellow>Attribute or skill '{target}' not found.</color>");
            return false;
        }
    }

    public class AdvanceSkillCommand : IConsoleCommand
    {
        public string Name => "advskill";
        public string Description => "Advances a skill by granting direct experience points.";
        public string Usage => "advskill <skillName> <xpAmount>";

        public bool Execute(string[] args, Action<string> print)
        {
            if (args.Length < 2 || !float.TryParse(args[1], out float xp))
            {
                print?.Invoke("<color=yellow>Usage: advskill <skillName> <xpAmount></color>");
                return false;
            }

            var prog = ServiceLocator.Get<SkillProgressionService>();
            if (prog == null)
            {
                print?.Invoke("<color=red>SkillProgressionService not available.</color>");
                return false;
            }

            string skill = args[0].ToLowerInvariant();
            var skillId = new ResourceId("nordic_rpg", $"skill/{skill}");
            prog.AwardXP(skillId, xp);
            print?.Invoke($"<color=green>Awarded {xp} XP to skill '{skill}'.</color>");
            return true;
        }
    }

    #endregion

    #region World, Combat & Cheats

    public class KillCommand : IConsoleCommand
    {
        public string Name => "kill";
        public string Description => "Kills the selected reference or crosshair targeted actor.";
        public string Usage => "kill";

        public bool Execute(string[] args, Action<string> print)
        {
            var targetObj = ConsoleUtil.SelectedReference;
            BaseActor actor = targetObj != null ? targetObj.GetComponent<BaseActor>() : null;
            AttributeComponent attrs = targetObj != null ? targetObj.GetComponent<AttributeComponent>() : null;

            if (actor == null && attrs == null)
            {
                var player = SKSE.GetPlayer();
                if (player != null && player.TryGetComponent<PlayerInteractionController>(out var interactor))
                {
                    actor = interactor.CurrentTarget as BaseActor;
                    if (actor != null) attrs = actor.GetComponent<AttributeComponent>();
                }
            }

            if (actor == null && attrs == null)
            {
                print?.Invoke("<color=yellow>No valid actor selected or targeted in crosshairs.</color>");
                return false;
            }

            if (actor != null)
            {
                var dmg = new DamageInfo(99999, DamageType.Physical, null, actor.transform.position, Vector3.up);
                actor.TakeDamage(dmg);
            }
            if (attrs != null)
            {
                attrs.ApplyDamage("Health", 99999);
            }

            string targetName = actor != null ? actor.name : attrs.name;
            print?.Invoke($"<color=red>Killed entity: '{targetName}'.</color>");
            return true;
        }
    }

    public class KillAllCommand : IConsoleCommand
    {
        public string Name => "killall";
        public string Description => "Kills all hostile enemies currently active in the area.";
        public string Usage => "killall";

        public bool Execute(string[] args, Action<string> print)
        {
            var actors = UnityEngine.Object.FindObjectsByType<BaseActor>(FindObjectsSortMode.None);
            int killed = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor.CompareTag("Player")) continue;

                if (actor.name.Contains("Bandit") || actor.name.Contains("Draugr") || actor.name.Contains("Enemy"))
                {
                    actor.TakeDamage(new DamageInfo(99999, DamageType.Physical, null, actor.transform.position, Vector3.up));
                    killed++;
                }
            }

            print?.Invoke($"<color=green>killall: Dispatched {killed} hostile combatants.</color>");
            return true;
        }
    }

    public class ResurrectCommand : IConsoleCommand
    {
        public string Name => "resurrect";
        public string Description => "Resurrects and restores targeted entity to full health.";
        public string Usage => "resurrect";

        public bool Execute(string[] args, Action<string> print)
        {
            var targetObj = ConsoleUtil.SelectedReference;
            BaseActor actor = targetObj != null ? targetObj.GetComponent<BaseActor>() : null;
            AttributeComponent attrs = targetObj != null ? targetObj.GetComponent<AttributeComponent>() : null;

            if (actor == null && attrs == null)
            {
                var player = SKSE.GetPlayer();
                if (player != null && player.TryGetComponent<PlayerInteractionController>(out var interactor))
                {
                    actor = interactor.CurrentTarget as BaseActor;
                    if (actor != null) attrs = actor.GetComponent<AttributeComponent>();
                }
            }

            if (actor == null && attrs == null)
            {
                print?.Invoke("<color=yellow>No entity selected or targeted in crosshairs.</color>");
                return false;
            }

            if (attrs != null)
            {
                attrs.Heal("Health", (int)attrs.MaxHealth);
            }

            string targetName = actor != null ? actor.name : attrs.name;
            print?.Invoke($"<color=green>Resurrected entity: '{targetName}'.</color>");
            return true;
        }
    }

    public class DisableCommand : IConsoleCommand
    {
        public string Name => "disable";
        public string Description => "Hides/disables the currently selected reference in console mode.";
        public string Usage => "disable";

        public bool Execute(string[] args, Action<string> print)
        {
            var target = ConsoleUtil.SelectedReference;
            if (target == null)
            {
                print?.Invoke("<color=yellow>No reference currently selected in console mode.</color>");
                return false;
            }

            target.SetActive(false);
            print?.Invoke($"<color=yellow>Disabled reference: '{target.name}'</color>");
            return true;
        }
    }

    public class EnableCommand : IConsoleCommand
    {
        public string Name => "enable";
        public string Description => "Enables/un-hides the currently selected reference in console mode.";
        public string Usage => "enable";

        public bool Execute(string[] args, Action<string> print)
        {
            var target = ConsoleUtil.SelectedReference;
            if (target == null)
            {
                print?.Invoke("<color=yellow>No reference currently selected in console mode.</color>");
                return false;
            }

            target.SetActive(true);
            print?.Invoke($"<color=green>Enabled reference: '{target.name}'</color>");
            return true;
        }
    }

    public class SetScaleCommand : IConsoleCommand
    {
        public string Name => "setscale";
        public string Description => "Sets the scale factor of the currently selected reference.";
        public string Usage => "setscale <value>";

        public bool Execute(string[] args, Action<string> print)
        {
            if (args.Length == 0 || !float.TryParse(args[0], out float scale))
            {
                print?.Invoke("<color=yellow>Usage: setscale <value></color>");
                return false;
            }

            var target = ConsoleUtil.SelectedReference;
            if (target == null)
            {
                print?.Invoke("<color=yellow>No reference currently selected in console mode.</color>");
                return false;
            }

            scale = Mathf.Clamp(scale, 0.1f, 10f);
            target.transform.localScale = Vector3.one * scale;
            print?.Invoke($"<color=green>Set scale of '{target.name}' to {scale:F2}.</color>");
            return true;
        }
    }

    public class UnlockCommand : IConsoleCommand
    {
        public string Name => "unlock";
        public string Description => "Unlocks the currently selected locked chest or door.";
        public string Usage => "unlock";

        public bool Execute(string[] args, Action<string> print)
        {
            var target = ConsoleUtil.SelectedReference;
            if (target == null)
            {
                print?.Invoke("<color=yellow>No reference currently selected in console mode.</color>");
                return false;
            }

            if (target.TryGetComponent<Gameplay.Interactables.ChestInteractable>(out var chest))
            {
                chest.Unlock();
                print?.Invoke($"<color=green>Unlocked chest: '{target.name}'.</color>");
                return true;
            }

            print?.Invoke($"<color=yellow>Selected reference '{target.name}' has no lock to pick.</color>");
            return false;
        }
    }

    public class ToggleAICommand : IConsoleCommand
    {
        public string Name => "tai";
        public string Description => "Toggles AI processing on the selected actor or universally.";
        public string Usage => "tai";

        public bool Execute(string[] args, Action<string> print)
        {
            var target = ConsoleUtil.SelectedReference;
            if (target == null)
            {
                print?.Invoke("<color=yellow>No object selected.</color>");
                return false;
            }

            if (target.TryGetComponent<EnemyCombatAI>(out var combatAI))
            {
                combatAI.enabled = !combatAI.enabled;
                string state = combatAI.enabled ? "Enabled" : "Disabled";
                print?.Invoke($"<color=cyan>Combat AI for '{target.name}' is now {state}.</color>");
                return true;
            }

            if (target.TryGetComponent<RangedCombatAI>(out var rangedAI))
            {
                rangedAI.enabled = !rangedAI.enabled;
                string state = rangedAI.enabled ? "Enabled" : "Disabled";
                print?.Invoke($"<color=cyan>Ranged AI for '{target.name}' is now {state}.</color>");
                return true;
            }

            if (target.TryGetComponent<ImmersiveCitizenAI>(out var citizenAI))
            {
                citizenAI.enabled = !citizenAI.enabled;
                string state = citizenAI.enabled ? "Enabled" : "Disabled";
                print?.Invoke($"<color=cyan>Citizen AI for '{target.name}' is now {state}.</color>");
                return true;
            }

            print?.Invoke("<color=yellow>Selected reference has no active AI component.</color>");
            return false;
        }
    }

    public class MoveToPlayerCommand : IConsoleCommand
    {
        public string Name => "moveto";
        public string Description => "Teleports selected object directly in front of the player.";
        public string Usage => "moveto player";

        public bool Execute(string[] args, Action<string> print)
        {
            var target = ConsoleUtil.SelectedReference;
            if (target == null)
            {
                print?.Invoke("<color=yellow>No reference currently selected in console mode.</color>");
                return false;
            }

            var player = SKSE.GetPlayer();
            if (player == null)
            {
                print?.Invoke("<color=red>Player not found.</color>");
                return false;
            }

            Vector3 dest = player.transform.position;
            target.transform.position = dest;
            print?.Invoke($"<color=green>Teleported '{target.name}' to player position {dest}.</color>");
            return true;
        }
    }

    public class CenterOnCellCommand : IConsoleCommand
    {
        public string Name => "coc";
        public string Description => "Center on Cell - teleports player to a designated scene cell or location.";
        public string Usage => "coc <prison|riverwood|crypt|qasmoke>";

        public bool Execute(string[] args, Action<string> print)
        {
            if (args.Length == 0)
            {
                print?.Invoke("<color=yellow>Usage: coc <prison|riverwood|crypt|qasmoke></color>");
                return false;
            }

            var player = SKSE.GetPlayer();
            if (player == null)
            {
                print?.Invoke("<color=red>Player not found in scene.</color>");
                return false;
            }

            string cell = args[0].ToLowerInvariant();
            Vector3 dest = cell switch
            {
                "prison" => new Vector3(0f, 0.5f, 0f),
                "riverwood" => new Vector3(2f, 0.5f, 5f),
                "crypt" => new Vector3(0f, 0.5f, 22f),
                "qasmoke" => new Vector3(-15f, 0.5f, -15f),
                _ => Vector3.zero
            };

            if (cell != "prison" && dest == Vector3.zero)
            {
                print?.Invoke($"<color=yellow>Unknown cell '{cell}'. Available cells: prison, riverwood, crypt, qasmoke.</color>");
                return false;
            }

            if (player.TryGetComponent<CharacterController>(out var cc)) cc.enabled = false;
            player.transform.position = dest;
            if (cc != null) cc.enabled = true;

            print?.Invoke($"<color=green>Teleported player to cell '{cell}' at {dest}.</color>");
            return true;
        }
    }

    public class HelpCommand : IConsoleCommand
    {
        private readonly ConsoleCommandRegistry _registry;

        public string Name => "help";
        public string Description => "Displays list of all available console commands and descriptions.";
        public string Usage => "help [filter]";

        public HelpCommand(ConsoleCommandRegistry registry)
        {
            _registry = registry;
        }

        public bool Execute(string[] args, Action<string> print)
        {
            string filter = args.Length > 0 ? args[0] : null;
            print?.Invoke("<color=cyan>--- AVAILABLE DEVELOPER & CHEAT CONSOLE COMMANDS ---</color>");

            foreach (var cmd in _registry.AllCommands)
            {
                if (!string.IsNullOrEmpty(filter) && !cmd.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                print?.Invoke($"<color=white><b>{cmd.Name}</b></color>: {cmd.Description} <i>({cmd.Usage})</i>");
            }

            return true;
        }
    }

    public class QuitCommand : IConsoleCommand
    {
        public string Name => "qqq";
        public string Description => "Quits the game immediately.";
        public string Usage => "qqq";

        public bool Execute(string[] args, Action<string> print)
        {
            print?.Invoke("<color=yellow>Quitting game session...</color>");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return true;
        }
    }

    #endregion
}
