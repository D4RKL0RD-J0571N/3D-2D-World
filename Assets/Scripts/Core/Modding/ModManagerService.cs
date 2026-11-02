namespace Core.Modding
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Core.Assets;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Ids;
    using Data.Config;
    using Domain.Grid;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using UnityEngine;

    /// <summary>
    /// Event published via <see cref="GameEventManager"/> whenever a <see cref="ModProfile"/> is activated or reloaded.
    /// </summary>
    public sealed class ModProfileActivatedEvent : GameEvent
    {
        public readonly string ProfileId;
        public readonly IReadOnlyList<string> LoadedModIds;
        public readonly int WarningCount;
        public readonly int ErrorCount;

        public ModProfileActivatedEvent(string profileId, IReadOnlyList<string> loadedModIds, int warningCount, int errorCount)
        {
            ProfileId = profileId;
            LoadedModIds = loadedModIds;
            WarningCount = warningCount;
            ErrorCount = errorCount;
        }
    }

    /// <summary>
    /// Contract for discovering, validating, sorting, and mounting external Content Packs and User Mods (<c>ARCH-04</c>).
    /// </summary>
    public interface IModManagerService : IGameService
    {
        ModDiagnosticReport LastDiagnosticReport { get; }
        IReadOnlyList<ModManifest> LoadedMods { get; }
        ModProfile ActiveProfile { get; }

        void AddSearchRoot(string directoryPath);
        void ClearSearchRoots();
        List<ModManifest> DiscoverManifests(ModDiagnosticReport report = null);
        bool ActivateProfile(ModProfile profile = null);
        bool ApplySingleModManifest(ModManifest manifest, ModDiagnosticReport report = null);
    }

    /// <summary>
    /// Concrete implementation of <see cref="IModManagerService"/>.
    /// Discovers <c>mod.json</c> packages from <c>StreamingAssets/ContentPacks/</c> and <c>persistentDataPath/Mods/</c>,
    /// resolves topological load order, mounts external asset directories into <see cref="IAssetProvider"/>,
    /// and applies <c>AddOnly</c>, RFC 7396 <c>MergePatch</c>, and <c>OverrideExisting</c> definitions into <see cref="IContentService"/>.
    /// </summary>
    public sealed class ModManagerService : IModManagerService
    {
        #region Fields

        private readonly List<string> _searchRoots = new();
        private readonly List<ModManifest> _loadedMods = new();
        private readonly List<UnityEngine.Object> _runtimeCreatedScriptableObjects = new();
        private readonly Dictionary<string, string> _propertyProvenance = new(StringComparer.Ordinal);
        private ModDiagnosticReport _lastReport = new();
        private ModProfile _activeProfile;

        #endregion

        #region Properties

        public ModDiagnosticReport LastDiagnosticReport => _lastReport;
        public IReadOnlyList<ModManifest> LoadedMods => _loadedMods;
        public ModProfile ActiveProfile => _activeProfile;
        public IReadOnlyDictionary<string, string> PropertyProvenance => _propertyProvenance;

        #endregion

        #region Constructors

        public ModManagerService(bool registerDefaultRoots = true)
        {
            if (registerDefaultRoots)
            {
                AddSearchRoot(Path.Combine(Application.streamingAssetsPath, "ContentPacks"));
                AddSearchRoot(Path.Combine(Application.persistentDataPath, "Mods"));
            }
        }

        #endregion

        #region IGameService Lifecycle

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            CleanupRuntimeScriptableObjects();
            _loadedMods.Clear();
            _propertyProvenance.Clear();
            _lastReport.Clear();
        }

        #endregion

        #region Search Roots & Discovery

        public void AddSearchRoot(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) return;
            string full = Path.GetFullPath(directoryPath);
            if (!_searchRoots.Contains(full))
            {
                _searchRoots.Add(full);
            }
        }

        public void ClearSearchRoots()
        {
            _searchRoots.Clear();
        }

        /// <summary>
        /// Scans all registered search roots for subdirectories containing a <c>mod.json</c> manifest.
        /// Malformed <c>mod.json</c> files are logged in <paramref name="report"/> and safely skipped.
        /// </summary>
        public List<ModManifest> DiscoverManifests(ModDiagnosticReport report = null)
        {
            var discovered = new List<ModManifest>();
            ModDiagnosticReport targetReport = report ?? _lastReport;

            foreach (string root in _searchRoots)
            {
                if (!Directory.Exists(root)) continue;

                string directManifestPath = Path.Combine(root, "mod.json");
                if (File.Exists(directManifestPath))
                {
                    if (TryParseManifestFile(directManifestPath, targetReport, out ModManifest directManifest))
                    {
                        discovered.Add(directManifest);
                    }
                }

                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(root);
                }
                catch (Exception ex)
                {
                    RecordDiagnostic(targetReport, "io", ModDiagnosticSeverity.Warning, "MOD-IO-ERROR", $"Failed to enumerate '{root}': {ex.Message}", root);
                    continue;
                }

                Array.Sort(subDirs, StringComparer.Ordinal);
                foreach (string subDir in subDirs)
                {
                    string manifestPath = Path.Combine(subDir, "mod.json");
                    if (!File.Exists(manifestPath)) continue;

                    if (TryParseManifestFile(manifestPath, targetReport, out ModManifest manifest))
                    {
                        discovered.Add(manifest);
                    }
                }
            }

            return discovered;
        }

        public bool TryParseManifestFile(string manifestFilePath, ModDiagnosticReport report, out ModManifest manifest)
        {
            manifest = null;
            if (!File.Exists(manifestFilePath))
            {
                return false;
            }

            try
            {
                string json = File.ReadAllText(manifestFilePath);
                manifest = JsonConvert.DeserializeObject<ModManifest>(json);
                if (manifest == null)
                {
                    RecordDiagnostic(report, Path.GetFileName(Path.GetDirectoryName(manifestFilePath)), ModDiagnosticSeverity.Error, "MOD-JSON-EMPTY", "Manifest deserialized to null.", manifestFilePath, 1);
                    return false;
                }

                manifest.RootDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(manifestFilePath));
                if (string.IsNullOrWhiteSpace(manifest.DisplayName))
                {
                    manifest.DisplayName = manifest.ModId;
                }

                return true;
            }
            catch (JsonReaderException jre)
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(manifestFilePath));
                RecordDiagnostic(
                    report,
                    folderName,
                    ModDiagnosticSeverity.Error,
                    "MOD-JSON-SYNTAX",
                    $"Syntax error in mod.json at line {jre.LineNumber}, pos {jre.LinePosition}: {jre.Message}",
                    manifestFilePath,
                    jre.LineNumber);
                return false;
            }
            catch (Exception ex)
            {
                string folderName = Path.GetFileName(Path.GetDirectoryName(manifestFilePath));
                RecordDiagnostic(
                    report,
                    folderName,
                    ModDiagnosticSeverity.Error,
                    "MOD-MANIFEST-READ",
                    $"Failed to read mod.json: {ex.Message}",
                    manifestFilePath,
                    0);
                return false;
            }
        }

        #endregion

        #region Profile Activation & Mounting

        /// <summary>
        /// Discovers all manifests across search roots, computes deterministic topological load order for <paramref name="profile"/>,
        /// mounts external assets into <see cref="IAssetProvider"/>, and loads/patches all definitions in <see cref="IContentService"/>.
        /// </summary>
        public bool ActivateProfile(ModProfile profile = null)
        {
            _lastReport = new ModDiagnosticReport();
            _loadedMods.Clear();
            _propertyProvenance.Clear();

            List<ModManifest> discovered = DiscoverManifests(_lastReport);
            _activeProfile = profile ?? ModProfile.CreateFromManifests(discovered);

            ModLoadOrderSolver.TrySolveLoadOrder(discovered, _lastReport, out List<ModManifest> ordered, _activeProfile);

            if (ServiceLocator.TryGet<IContentService>(out var contentService))
            {
                contentService.UnfreezeAll();
            }

            var loadedIds = new List<string>(ordered.Count);
            foreach (ModManifest manifest in ordered)
            {
                ApplySingleModManifest(manifest, _lastReport);
                _loadedMods.Add(manifest);
                loadedIds.Add(manifest.ModId);
            }

            GameEventManager.Publish(new ModProfileActivatedEvent(
                _activeProfile.ProfileId,
                loadedIds,
                _lastReport.WarningCount,
                _lastReport.ErrorCount));

            return !_lastReport.HasErrors;
        }

        /// <summary>
        /// Mounts a single validated <see cref="ModManifest"/>'s asset folders and loads its JSON definitions and patches.
        /// Malformed JSON files inside the mod are caught, logged to <paramref name="report"/>, and skipped without crashing.
        /// </summary>
        public bool ApplySingleModManifest(ModManifest manifest, ModDiagnosticReport report = null)
        {
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.ModId))
            {
                return false;
            }

            ModDiagnosticReport targetReport = report ?? _lastReport;

            if (!string.IsNullOrWhiteSpace(manifest.RootDirectoryPath) && Directory.Exists(manifest.RootDirectoryPath))
            {
                MountModAssets(manifest);

                if (ServiceLocator.TryGet<IContentService>(out var contentService))
                {
                    contentService.UnfreezeAll();
                    LoadModDefinitionsFromDisk(manifest, contentService, targetReport);
                    LoadModPatchesFromDisk(manifest, contentService, targetReport);
                }
            }

            return true;
        }

        private static void MountModAssets(ModManifest manifest)
        {
            if (!ServiceLocator.TryGet<IAssetProvider>(out var assetProvider))
            {
                return;
            }

            string root = manifest.RootDirectoryPath;
            string assetsRel = manifest.Mounts?.AssetsPath ?? "Assets";
            string assetsDir = Path.Combine(root, assetsRel);

            if (Directory.Exists(assetsDir))
            {
                assetProvider.MountExternalDirectory(manifest.ModId, assetsDir);
            }

            assetProvider.MountExternalDirectory(manifest.ModId, root);
        }

        #endregion

        #region JSON Definition Loading & RFC 7396 Patching

        private void LoadModDefinitionsFromDisk(ModManifest manifest, IContentService contentService, ModDiagnosticReport report)
        {
            string root = manifest.RootDirectoryPath;
            string defsRel = manifest.Mounts?.DefinitionsPath ?? "Data/Definitions";

            string[] baseDirs =
            {
                Path.Combine(root, defsRel),
                root
            };

            var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string baseDir in baseDirs)
            {
                if (!Directory.Exists(baseDir)) continue;

                LoadCategoryDirectory(manifest, Path.Combine(baseDir, "Items"), "item", contentService, report, visitedFiles);
                LoadCategoryDirectory(manifest, Path.Combine(baseDir, "Skills"), "skill", contentService, report, visitedFiles);
                LoadCategoryDirectory(manifest, Path.Combine(baseDir, "Factions"), "faction", contentService, report, visitedFiles);
                LoadCategoryDirectory(manifest, Path.Combine(baseDir, "Quests"), "quest_template", contentService, report, visitedFiles);
                LoadCategoryDirectory(manifest, Path.Combine(baseDir, "Blocks"), "block", contentService, report, visitedFiles);
            }
        }

        private void LoadCategoryDirectory(
            ModManifest manifest,
            string categoryDir,
            string defaultCategoryPrefix,
            IContentService contentService,
            ModDiagnosticReport report,
            HashSet<string> visitedFiles)
        {
            if (!Directory.Exists(categoryDir)) return;

            string[] files = Directory.GetFiles(categoryDir, "*.json", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
            {
                string fullPath = Path.GetFullPath(file);
                if (!visitedFiles.Add(fullPath)) continue;

                ProcessDefinitionJsonFile(manifest, fullPath, defaultCategoryPrefix, contentService, report);
            }
        }

        private void ProcessDefinitionJsonFile(
            ModManifest manifest,
            string filePath,
            string defaultCategoryPrefix,
            IContentService contentService,
            ModDiagnosticReport report)
        {
            JToken rootToken;
            try
            {
                string rawJson = File.ReadAllText(filePath);
                rootToken = JToken.Parse(rawJson);
            }
            catch (JsonReaderException jre)
            {
                RecordDiagnostic(
                    report,
                    manifest.ModId,
                    ModDiagnosticSeverity.Error,
                    "MOD-JSON-SYNTAX",
                    $"Malformed JSON in '{Path.GetFileName(filePath)}' at line {jre.LineNumber}, pos {jre.LinePosition}: {jre.Message}",
                    filePath,
                    jre.LineNumber);
                return;
            }
            catch (Exception ex)
            {
                RecordDiagnostic(
                    report,
                    manifest.ModId,
                    ModDiagnosticSeverity.Error,
                    "MOD-JSON-READ",
                    $"Failed to read '{Path.GetFileName(filePath)}': {ex.Message}",
                    filePath,
                    0);
                return;
            }

            if (rootToken is JArray array)
            {
                foreach (JToken element in array)
                {
                    if (element is JObject obj)
                    {
                        ApplyDefinitionJObject(manifest, obj, defaultCategoryPrefix, filePath, contentService, report);
                    }
                }
            }
            else if (rootToken is JObject singleObj)
            {
                ApplyDefinitionJObject(manifest, singleObj, defaultCategoryPrefix, filePath, contentService, report);
            }
        }

        public void ApplyDefinitionJObject(
            ModManifest manifest,
            JObject json,
            string categoryKind,
            string sourceFilePath,
            IContentService contentService,
            ModDiagnosticReport report = null)
        {
            ModDiagnosticReport targetReport = report ?? _lastReport;
            string rawId = JsonMergePatcher.ReadString(json, "id", "contentId", "targetId");
            if (string.IsNullOrWhiteSpace(rawId))
            {
                RecordDiagnostic(
                    targetReport,
                    manifest.ModId,
                    ModDiagnosticSeverity.Warning,
                    "MOD-DEF-MISSING-ID",
                    $"Definition in '{Path.GetFileName(sourceFilePath ?? "memory")}' is missing an 'id' field. Skipping.",
                    sourceFilePath);
                return;
            }

            if (!ResourceId.TryParse(rawId, out ResourceId contentId))
            {
                contentId = ResourceId.FromLegacyId(manifest.ModId, categoryKind, rawId);
            }

            ModConflictPolicy policy = manifest.ConflictPolicy;
            string opOverride = JsonMergePatcher.ReadString(json, "conflictPolicy", "operation");
            if (!string.IsNullOrWhiteSpace(opOverride))
            {
                if (string.Equals(opOverride, "AddOnly", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(opOverride, "add", StringComparison.OrdinalIgnoreCase))
                {
                    policy = ModConflictPolicy.AddOnly;
                }
                else if (string.Equals(opOverride, "MergePatch", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(opOverride, "merge", StringComparison.OrdinalIgnoreCase))
                {
                    policy = ModConflictPolicy.MergePatch;
                }
                else if (string.Equals(opOverride, "OverrideExisting", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(opOverride, "override", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(opOverride, "replace", StringComparison.OrdinalIgnoreCase))
                {
                    policy = ModConflictPolicy.OverrideExisting;
                }
            }

            JObject payloadObj = json["patch"] as JObject ?? json;

            switch (categoryKind.ToLowerInvariant())
            {
                case "item":
                case "items":
                case "framework:items":
                    ApplyToRegistry(
                        contentService.GetRegistry<ItemData>(),
                        contentId,
                        manifest.ModId,
                        policy,
                        payloadObj,
                        () =>
                        {
                            var inst = ScriptableObject.CreateInstance<ItemData>();
                            inst.name = contentId.Path;
                            inst.Id = contentId.Value;
                            inst.ContentId = contentId;
                            inst.SourceModId = manifest.ModId;
                            _runtimeCreatedScriptableObjects.Add(inst);
                            return inst;
                        },
                        targetReport,
                        alsoRegisterInEntityRegistry: contentService.GetRegistry<EntityData>());
                    break;

                case "block":
                case "blocks":
                case "framework:blocks":
                    LegacyDatabaseBridge.EnsureBlockSecondaryIndex(contentService);
                    ApplyToRegistry(
                        contentService.GetRegistry<BlockDefinition>(),
                        contentId,
                        manifest.ModId,
                        policy,
                        payloadObj,
                        () =>
                        {
                            var inst = ScriptableObject.CreateInstance<BlockDefinition>();
                            inst.name = contentId.Path;
                            inst.Id = contentId.Value;
                            inst.ContentId = contentId;
                            inst.SourceModId = manifest.ModId;
                            _runtimeCreatedScriptableObjects.Add(inst);
                            return inst;
                        },
                        targetReport,
                        alsoRegisterInEntityRegistry: contentService.GetRegistry<EntityData>());
                    break;

                case "skill":
                case "skills":
                case "framework:skills":
                    ApplyDynamicTypeToRegistry("Domain.Skyrim.SkillDefinition, Assembly-CSharp", contentService, contentId, manifest.ModId, policy, payloadObj, targetReport);
                    break;

                case "faction":
                case "factions":
                case "framework:factions":
                    ApplyDynamicTypeToRegistry("Domain.Skyrim.FactionDefinition, Assembly-CSharp", contentService, contentId, manifest.ModId, policy, payloadObj, targetReport);
                    break;

                case "quest":
                case "quests":
                case "quest_template":
                case "framework:quests":
                    ApplyDynamicTypeToRegistry("Domain.Skyrim.QuestTemplateDefinition, Assembly-CSharp", contentService, contentId, manifest.ModId, policy, payloadObj, targetReport);
                    break;
            }
        }

        private void ApplyDynamicTypeToRegistry(
            string assemblyQualifiedTypeName,
            IContentService contentService,
            ResourceId contentId,
            string modId,
            ModConflictPolicy policy,
            JObject payloadJson,
            ModDiagnosticReport report)
        {
            Type defType = Type.GetType(assemblyQualifiedTypeName);
            if (defType == null) return;

            MethodInfo genericApply = typeof(ModManagerService)
                .GetMethod(nameof(ApplyDynamicGenericHelper), BindingFlags.NonPublic | BindingFlags.Instance)
                ?.MakeGenericMethod(defType);

            genericApply?.Invoke(this, new object[] { contentService, contentId, modId, policy, payloadJson, report });
        }

        private void ApplyDynamicGenericHelper<T>(
            IContentService contentService,
            ResourceId contentId,
            string modId,
            ModConflictPolicy policy,
            JObject payloadJson,
            ModDiagnosticReport report)
            where T : class, IContentDefinition, new()
        {
            ApplyToRegistry(
                contentService.GetRegistry<T>(),
                contentId,
                modId,
                policy,
                payloadJson,
                () =>
                {
                    var inst = new T();
                    Type t = typeof(T);
                    t.GetProperty("ContentId")?.SetValue(inst, contentId);
                    t.GetProperty("DisplayName")?.SetValue(inst, contentId.Path);
                    t.GetProperty("SourceModId")?.SetValue(inst, modId);
                    return inst;
                },
                report);
        }

        private void ApplyToRegistry<T>(
            ContentRegistry<T> registry,
            ResourceId contentId,
            string modId,
            ModConflictPolicy policy,
            JObject payloadJson,
            Func<T> factory,
            ModDiagnosticReport report,
            ContentRegistry<EntityData> alsoRegisterInEntityRegistry = null)
            where T : class, IContentDefinition
        {
            if (registry.TryGetEntry(contentId, out ContentRegistryEntry<T> existingEntry))
            {
                if (policy == ModConflictPolicy.AddOnly)
                {
                    RecordDiagnostic(
                        report,
                        modId,
                        ModDiagnosticSeverity.Warning,
                        "MOD-CONFLICT-ADDONLY",
                        $"Mod '{modId}' (policy: AddOnly) attempted to define existing '{contentId}' (owned by '{existingEntry.SourceModId}'). Collision blocked.");
                    return;
                }

                if (policy == ModConflictPolicy.MergePatch)
                {
                    JObject mergedState = JsonMergePatcher.SnapshotToJObject(existingEntry.Definition);
                    JsonMergePatcher.ApplyMergePatch(
                        mergedState,
                        payloadJson,
                        contentId,
                        modId,
                        existingEntry.SourceModId,
                        _propertyProvenance,
                        report);

                    T cloned = JsonMergePatcher.CloneDefinition(existingEntry.Definition);
                    if (cloned is ScriptableObject soClone)
                    {
                        _runtimeCreatedScriptableObjects.Add(soClone);
                    }

                    JsonMergePatcher.ApplyJObjectToDefinition(cloned, mergedState);
                    registry.Register(cloned, modId, ContentRegistrationPolicy.OverrideExisting);

                    if (alsoRegisterInEntityRegistry != null && cloned is EntityData entityClone)
                    {
                        alsoRegisterInEntityRegistry.Register(entityClone, modId, ContentRegistrationPolicy.OverrideExisting);
                    }
                    return;
                }

                // OverrideExisting: replace entire definition with defaults + payloadJson
                T replacement = factory();
                JsonMergePatcher.ApplyJObjectToDefinition(replacement, payloadJson);
                registry.Register(replacement, modId, ContentRegistrationPolicy.OverrideExisting);

                if (alsoRegisterInEntityRegistry != null && replacement is EntityData entityReplacement)
                {
                    alsoRegisterInEntityRegistry.Register(entityReplacement, modId, ContentRegistrationPolicy.OverrideExisting);
                }
                return;
            }

            // New definition
            T created = factory();
            JsonMergePatcher.ApplyJObjectToDefinition(created, payloadJson);
            registry.Register(created, modId, ContentRegistrationPolicy.AddOnly);

            if (alsoRegisterInEntityRegistry != null && created is EntityData createdEntity)
            {
                alsoRegisterInEntityRegistry.Register(createdEntity, modId, ContentRegistrationPolicy.OverrideExisting);
            }
        }

        private void LoadModPatchesFromDisk(ModManifest manifest, IContentService contentService, ModDiagnosticReport report)
        {
            string root = manifest.RootDirectoryPath;
            string patchesRel = manifest.Mounts?.PatchesPath ?? "Data/Patches";
            string[] patchDirs =
            {
                Path.Combine(root, patchesRel),
                Path.Combine(root, "Patches")
            };

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dir in patchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                string[] files = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);

                foreach (string file in files)
                {
                    string full = Path.GetFullPath(file);
                    if (!visited.Add(full)) continue;

                    try
                    {
                        string raw = File.ReadAllText(full);
                        JToken token = JToken.Parse(raw);
                        if (token is JObject patchDoc)
                        {
                            ApplyPatchDocument(manifest, patchDoc, full, contentService, report);
                        }
                        else if (token is JArray patchArray)
                        {
                            foreach (JToken el in patchArray)
                            {
                                if (el is JObject obj)
                                {
                                    ApplyPatchDocument(manifest, obj, full, contentService, report);
                                }
                            }
                        }
                    }
                    catch (JsonReaderException jre)
                    {
                        RecordDiagnostic(
                            report,
                            manifest.ModId,
                            ModDiagnosticSeverity.Error,
                            "MOD-JSON-SYNTAX",
                            $"Malformed patch JSON in '{Path.GetFileName(full)}' at line {jre.LineNumber}: {jre.Message}",
                            full,
                            jre.LineNumber);
                    }
                    catch (Exception ex)
                    {
                        RecordDiagnostic(
                            report,
                            manifest.ModId,
                            ModDiagnosticSeverity.Error,
                            "MOD-PATCH-READ",
                            $"Failed to read patch '{Path.GetFileName(full)}': {ex.Message}",
                            full,
                            0);
                    }
                }
            }
        }

        private void ApplyPatchDocument(
            ModManifest manifest,
            JObject patchDoc,
            string filePath,
            IContentService contentService,
            ModDiagnosticReport report)
        {
            string targetRegistry = JsonMergePatcher.ReadString(patchDoc, "targetRegistry", "category") ?? "item";
            string targetId = JsonMergePatcher.ReadString(patchDoc, "targetId", "id", "contentId");

            if (!string.IsNullOrWhiteSpace(targetId) && ResourceId.TryParse(targetId, out ResourceId parsedId))
            {
                if (string.IsNullOrWhiteSpace(JsonMergePatcher.ReadString(patchDoc, "targetRegistry", "category")))
                {
                    int slash = parsedId.Path.IndexOf('/');
                    if (slash > 0) targetRegistry = parsedId.Path.Substring(0, slash);
                }
            }

            if (patchDoc["operation"] == null && patchDoc["conflictPolicy"] == null)
            {
                patchDoc["conflictPolicy"] = "MergePatch";
            }

            ApplyDefinitionJObject(manifest, patchDoc, targetRegistry, filePath, contentService, report);
        }

        #endregion

        #region Diagnostics & Cleanup

        private static void RecordDiagnostic(
            ModDiagnosticReport report,
            string modId,
            ModDiagnosticSeverity severity,
            string code,
            string message,
            string filePath = null,
            int lineNumber = 0)
        {
            report?.Add(modId, severity, code, message, filePath, lineNumber);

            if (ServiceLocator.TryGet<IDiagnosticService>(out var diag))
            {
                string formatted = $"[{code}][Mod:{modId}] {message}";
                if (severity == ModDiagnosticSeverity.Error)
                {
                    diag.RecordError("ModManager", formatted, logToUnityConsole: false);
                }
                else if (severity == ModDiagnosticSeverity.Warning)
                {
                    diag.RecordWarning("ModManager", formatted, logToUnityConsole: false);
                }
            }
        }

        private void CleanupRuntimeScriptableObjects()
        {
            for (int i = _runtimeCreatedScriptableObjects.Count - 1; i >= 0; i--)
            {
                UnityEngine.Object obj = _runtimeCreatedScriptableObjects[i];
                if (obj != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(obj);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
            _runtimeCreatedScriptableObjects.Clear();
        }

        #endregion
    }
}
