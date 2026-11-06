namespace Tests.Editor
{
    using System;
    using System.Reflection;
    using Core.Content;
    using Core.Modules;
    using Domain.Skyrim;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Editor menu runner that executes all Fantasy Engine EditMode test fixtures synchronously
    /// and reports a consolidated verification summary to the Unity Console.
    /// </summary>
    public static class FantasyEngineTestSuiteRunner
    {
        [MenuItem("Tools/FantasyEngine/Run All EditMode Tests")]
        public static void RunAllEditModeTests()
        {
            Debug.unityLogger.filterLogType = LogType.Log;

            Type[] fixtures =
            {
                typeof(Domain1_EngineIndependenceTests),
                typeof(Domain2_ModuleLifecycleTests),
                typeof(Domain3_ContentRegistryTests),
                typeof(Domain4_ModLoaderAndPatchTests),
                typeof(Domain5_InGameEditorTests),
                typeof(Domain6_PersistenceAndVoxelCodecTests),
                typeof(Domain7_ProvingGroundsRegressionTests),
                typeof(Domain8_CoreGameplayLoopTests),
                typeof(Domain9_CombatAndMagicTests),
                typeof(Domain10_ModdedSkyrimAndConsoleTests),
                typeof(Domain11_MoreInformativeConsoleAndSceneTests),
                typeof(Domain12_WorldDesignTests),
                typeof(CraftingServiceTests),
                typeof(HarvestableNodeTests)
            };

            int passed = 0;
            int failed = 0;

            foreach (Type fixtureType in fixtures)
            {
                object instance = Activator.CreateInstance(fixtureType);
                MethodInfo setUp = fixtureType.GetMethod("SetUp", BindingFlags.Instance | BindingFlags.Public);
                MethodInfo tearDown = fixtureType.GetMethod("TearDown", BindingFlags.Instance | BindingFlags.Public);

                foreach (MethodInfo method in fixtureType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (method.GetCustomAttribute<TestAttribute>() == null) continue;

                    try
                    {
                        setUp?.Invoke(instance, null);
                        method.Invoke(instance, null);
                        passed++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Exception inner = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                        Debug.LogError($"[FantasyEngineTestSuiteRunner] FAILED {fixtureType.Name}.{method.Name}: {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
                    }
                    finally
                    {
                        try
                        {
                            tearDown?.Invoke(instance, null);
                        }
                        catch (Exception tdEx)
                        {
                            Debug.LogError($"[FantasyEngineTestSuiteRunner] TearDown error in {fixtureType.Name}.{method.Name}: {tdEx.Message}");
                        }
                    }
                }
            }

            // Full 11-Module Boot & Registry Hydration Verification
            var host = new ModuleHost();
            host.DiscoverAndAddModules();
            host.Boot(hydrateProjectAssets: true);
            int skillsCount = host.Content.GetRegistry<SkillDefinition>().Count;
            int factionsCount = host.Content.GetRegistry<FactionDefinition>().Count;
            int questsCount = host.Content.GetRegistry<QuestTemplateDefinition>().Count;
            host.Shutdown();

            Debug.Log($"[FantasyEngineTestSuiteRunner] SUMMARY: {passed} passed, {failed} failed. Full Boot Registries -> Skills: {skillsCount}, Factions: {factionsCount}, Quests: {questsCount}");
        }
    }
}
