namespace Systems.Modules
{
    using Core.Modules;
    using Core.Scheduling;
    using Domain.Entities.StateMachine;
    using Domain.Grid;
    using Systems.Audio;
    using Systems.Crafting;
    using Systems.Creator;
    using Systems.Input;
    using Systems.Interaction;
    using Systems.Lab;
    using Systems.LevelGeneration;
    using Systems.Meshing;
    using Systems.Museum;
    using Systems.Persistence;
    using Systems.Player;
    using Systems.Pooling;
    using Systems.Scheduler;
    using Systems.Skyrim;
    using Systems.WorldMap;
    using UnityEngine;

    /// <summary>
    /// Layer A Engine Core module providing fundamental runtime infrastructure:
    /// state machine, input, object pooling, area scheduling, persistence, and audio.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: true)]
    public sealed class CoreInfrastructureModule : EngineModuleBase
    {
        public const string Id = "core:infrastructure";

        private GameStateMachine _stateMachine;

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Engine Core Infrastructure",
            "1.0.0");

        public override void RegisterServices(IModuleContext context)
        {
            _stateMachine = new GameStateMachine();
            context.RegisterService(_stateMachine);
            context.RegisterTickable(_stateMachine, TickGroup.Simulation);

            context.RegisterService(new InputManager());
            context.RegisterService(new PoolingManager());

            var areaScheduler = new AreaSchedulerService();
            context.RegisterService(areaScheduler);
            context.RegisterTickable(areaScheduler, TickGroup.WorldStreaming);

            context.RegisterService(new FileSaveService());
            context.RegisterService(new AudioService());
        }

        public override void OnActivate(IModuleContext context)
        {
            if (_stateMachine != null)
            {
                _ = _stateMachine.TransitionToAsync(new Playing());
            }
        }
    }

    /// <summary>
    /// Layer A Engine Core module providing the In-Game Editor / Creator Studio runtime service.
    /// Can operate independently of the Fantasy Framework.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: true)]
    public sealed class EditorStudioModule : EngineModuleBase
    {
        public const string Id = "core:editor_studio";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "In-Game Editor & Creator Studio",
            "1.0.0",
            dependencies: new[] { CoreInfrastructureModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            context.RegisterService(new CreatorStudioService());
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework foundation module for entities, player spawning, and world map tracking.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class FantasyEntitiesModule : EngineModuleBase
    {
        public const string Id = "framework:entities";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Fantasy Entities & World Map",
            "1.0.0",
            dependencies: new[] { CoreInfrastructureModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            context.RegisterService(new PlayerSpawnService());

            var worldMapService = new WorldMapService();
            context.RegisterService(worldMapService);
            context.RegisterTickable(worldMapService, TickGroup.UI);
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework module for procedural voxel dungeon generation, Burst greedy meshing,
    /// grid raycasting, and runtime block placement.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class VoxelDungeonModule : EngineModuleBase
    {
        public const string Id = "framework:voxel_dungeon";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Voxel Dungeon Generation & Burst Meshing",
            "1.0.0",
            dependencies: new[] { CoreInfrastructureModule.Id, FantasyEntitiesModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            context.RegisterService(new LevelGenerationService());
            context.RegisterService(new GridInteractionService());
            context.RegisterService(new GreedyMeshingService());
            context.RegisterService(new BlockPlacementService());
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework module for inventory quickbars and recipe crafting.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class FantasyInventoryModule : EngineModuleBase
    {
        public const string Id = "framework:inventory";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Fantasy Inventory & Crafting",
            "1.0.0",
            dependencies: new[] { FantasyEntitiesModule.Id, VoxelDungeonModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            context.RegisterService(new HotbarService());
            context.RegisterService(new CraftingService());
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework module for faction standings, reputation, and hostility matrices.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class FantasyFactionsModule : EngineModuleBase
    {
        public const string Id = "framework:factions";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Fantasy Factions & Reputation",
            "1.0.0",
            dependencies: new[] { FantasyEntitiesModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            context.RegisterService(new FactionService());
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework module for skill XP progression, character leveling, and perks.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class FantasyProgressionModule : EngineModuleBase
    {
        public const string Id = "framework:progression";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Fantasy Skill Progression & Perks",
            "1.0.0",
            dependencies: new[] { FantasyEntitiesModule.Id, FantasyInventoryModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            var skillService = new SkillProgressionService();
            context.RegisterService(skillService);
            context.RegisterTickable(skillService, TickGroup.Simulation);
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework module for stealth detection meters, line-of-sight checks, and sneak multipliers.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class FantasyCombatStealthModule : EngineModuleBase
    {
        public const string Id = "framework:combat_stealth";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Fantasy Combat & Stealth Detection",
            "1.0.0",
            dependencies: new[] { FantasyEntitiesModule.Id, FantasyFactionsModule.Id, FantasyProgressionModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            var stealthService = new StealthDetectionService();
            context.RegisterService(stealthService);
            context.RegisterTickable(stealthService, TickGroup.AI);
        }
    }

    /// <summary>
    /// Layer B Fantasy Framework module for Radiant quests, objectives, and compass navigation markers.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class FantasyQuestsNavModule : EngineModuleBase
    {
        public const string Id = "framework:quests_nav";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Fantasy Quests & Compass Navigation",
            "1.0.0",
            dependencies: new[] { FantasyFactionsModule.Id, FantasyProgressionModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            var compassService = new CompassService();
            context.RegisterService(compassService);
            context.RegisterTickable(compassService, TickGroup.UI);

            context.RegisterService(new RadiantQuestService());
            context.RegisterService(new Gameplay.Skyrim.AlternateStartService());
        }
    }

    /// <summary>
    /// Optional Showcase & Asset Lab module for museum exhibits and isolated parameter testing.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class ShowcaseModule : EngineModuleBase
    {
        public const string Id = "framework:showcase";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Museum Curator & Asset Lab Showcase",
            "1.0.0",
            dependencies: new[] { CoreInfrastructureModule.Id });

        public override void RegisterServices(IModuleContext context)
        {
            context.RegisterService(new LabService());
            context.RegisterService(new CuratorService());
        }
    }

    /// <summary>
    /// Presentation HUD bootstrapper module that ensures runtime UI overlays exist when playing a scene.
    /// </summary>
    [BuiltInEngineModule(isCoreModule: false)]
    public sealed class PresentationHudModule : EngineModuleBase
    {
        public const string Id = "framework:presentation_hud";

        public override ModuleDescriptor Descriptor { get; } = new(
            Id,
            "Presentation HUD & Combat Feedback",
            "1.0.0",
            dependencies: new[]
            {
                EditorStudioModule.Id,
                FantasyInventoryModule.Id,
                FantasyCombatStealthModule.Id,
                FantasyQuestsNavModule.Id,
                ShowcaseModule.Id
            });

        public override void OnActivate(IModuleContext context)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureHUD<Gameplay.UI.HotbarHUD>("HotbarHUD");
            EnsureHUD<Gameplay.UI.PlayerHUD>("PlayerHUD");
            EnsureHUD<Gameplay.UI.WorldMapHUD>("WorldMapHUD");
            EnsureHUD<Gameplay.UI.LabHUD>("LabHUD");
            EnsureHUD<Gameplay.UI.CuratorHUD>("CuratorHUD");
            EnsureHUD<Gameplay.UI.CreatorStudioHUD>("CreatorStudioHUD");
            EnsureHUD<Gameplay.Combat.CombatFeedbackBridge>("CombatFeedbackBridge");
            EnsureHUD<Gameplay.UI.RPGOverlayHUD>("RPGOverlayHUD");
            EnsureHUD<Gameplay.UI.CommandConsoleHUD>("CommandConsoleHUD");
            EnsureHUD<Gameplay.UI.SkyUIActiveEffectsHUD>("SkyUIActiveEffectsHUD");
            EnsureHUD<Gameplay.UI.SkyUIInventoryMenu>("SkyUIInventoryMenu");
            EnsureHUD<Gameplay.UI.MCMMenu>("MCMMenu");
            EnsureHUD<Gameplay.UI.DialogueHUD>("DialogueHUD");
        }

        private static void EnsureHUD<T>(string goName) where T : Component
        {
            if (Object.FindFirstObjectByType<T>() == null)
            {
                GameObject prefab = Resources.Load<GameObject>($"Prefabs/UI/{goName}");
#if UNITY_EDITOR
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/UI/{goName}.prefab");
                }
#endif
                if (prefab != null)
                {
                    var instance = Object.Instantiate(prefab);
                    instance.name = goName;
                }
                else
                {
                    var go = new GameObject(goName);
                    go.AddComponent<T>();
                }
            }
        }
    }
}
