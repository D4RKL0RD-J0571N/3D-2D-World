namespace Core.Modding
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Validates <see cref="ModManifest"/> dependency graphs, SemVer version ranges, and engine version compatibility,
    /// and computes a deterministic topological load order using Kahn's algorithm with profile-index tie-breaking (<c>ARCH-04</c>).
    /// </summary>
    public static class ModLoadOrderSolver
    {
        public static readonly SemVer CurrentEngineVersion = new(1, 0, 0);

        /// <summary>
        /// Attempts to solve a deterministic topological load order for the given manifests and optional <see cref="ModProfile"/>.
        /// Any manifest failing validation, missing a required dependency, or participating in a dependency cycle is reported in
        /// <paramref name="report"/> and excluded from <paramref name="orderedManifests"/>.
        /// </summary>
        public static bool TrySolveLoadOrder(
            IReadOnlyList<ModManifest> availableManifests,
            ModDiagnosticReport report,
            out List<ModManifest> orderedManifests,
            ModProfile profile = null,
            SemVer? engineVersionOverride = null)
        {
            orderedManifests = new List<ModManifest>();
            report ??= new ModDiagnosticReport();

            if (availableManifests == null || availableManifests.Count == 0)
            {
                return true;
            }

            SemVer activeEngineVersion = engineVersionOverride ?? CurrentEngineVersion;

            // 1. Filter by profile (if supplied) and validate basic manifest fields & engine version
            var enabledPriorityMap = new Dictionary<string, int>(StringComparer.Ordinal);
            if (profile?.Entries != null && profile.Entries.Count > 0)
            {
                for (int i = 0; i < profile.Entries.Count; i++)
                {
                    var entry = profile.Entries[i];
                    if (entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.ModId))
                    {
                        enabledPriorityMap[entry.ModId.Trim()] = i;
                    }
                }
            }

            var validByModId = new Dictionary<string, ModManifest>(StringComparer.Ordinal);
            var priorityOrder = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < availableManifests.Count; i++)
            {
                ModManifest m = availableManifests[i];
                if (m == null) continue;

                if (profile?.Entries != null && profile.Entries.Count > 0 && !enabledPriorityMap.ContainsKey(m.ModId ?? string.Empty))
                {
                    continue;
                }

                if (!ModManifest.IsValidModId(m.ModId))
                {
                    report.Add(
                        m.ModId ?? "<empty>",
                        ModDiagnosticSeverity.Error,
                        "MOD-ID-INVALID",
                        $"ModId '{m.ModId}' is invalid. Must match lowercase '^[a-z0-9_]+$'.",
                        m.RootDirectoryPath);
                    continue;
                }

                if (!SemVer.TryParse(m.Version, out SemVer modVer))
                {
                    report.Add(
                        m.ModId,
                        ModDiagnosticSeverity.Error,
                        "MOD-SEMVER-INVALID",
                        $"Mod '{m.ModId}' has invalid SemVer version '{m.Version}'.",
                        m.RootDirectoryPath);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(m.EngineVersion) && !activeEngineVersion.SatisfiesRange(m.EngineVersion))
                {
                    report.Add(
                        m.ModId,
                        ModDiagnosticSeverity.Error,
                        "MOD-ENGINE-INCOMPATIBLE",
                        $"Mod '{m.ModId}' requires engineVersion '{m.EngineVersion}', which is incompatible with current engine '{activeEngineVersion}'.",
                        m.RootDirectoryPath);
                    continue;
                }

                if (validByModId.ContainsKey(m.ModId))
                {
                    report.Add(
                        m.ModId,
                        ModDiagnosticSeverity.Warning,
                        "MOD-DUPLICATE-ID",
                        $"Duplicate modId '{m.ModId}' discovered; keeping first registration.",
                        m.RootDirectoryPath);
                    continue;
                }

                validByModId[m.ModId] = m;
                priorityOrder[m.ModId] = enabledPriorityMap.TryGetValue(m.ModId, out int pIdx) ? pIdx : (1000 + i);
            }

            // 2. Iteratively prune mods with missing or version-mismatched required dependencies
            bool removedAny = true;
            while (removedAny)
            {
                removedAny = false;
                var keys = validByModId.Keys.ToList();
                foreach (string modId in keys)
                {
                    ModManifest m = validByModId[modId];
                    if (m.Dependencies == null) continue;

                    foreach (ModDependencySpec dep in m.Dependencies)
                    {
                        if (dep == null || string.IsNullOrWhiteSpace(dep.ModId)) continue;
                        string targetId = dep.ModId.Trim();

                        if (!validByModId.TryGetValue(targetId, out ModManifest targetManifest))
                        {
                            if (!dep.Optional)
                            {
                                report.Add(
                                    m.ModId,
                                    ModDiagnosticSeverity.Error,
                                    "MOD-DEP-MISSING",
                                    $"Mod '{m.ModId}' is missing required dependency '{targetId}' ({dep.VersionRange ?? "*"}).",
                                    m.RootDirectoryPath);
                                validByModId.Remove(modId);
                                removedAny = true;
                                break;
                            }
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(dep.VersionRange) &&
                            SemVer.TryParse(targetManifest.Version, out SemVer targetVer) &&
                            !targetVer.SatisfiesRange(dep.VersionRange))
                        {
                            if (!dep.Optional)
                            {
                                report.Add(
                                    m.ModId,
                                    ModDiagnosticSeverity.Error,
                                    "MOD-DEP-VERSION",
                                    $"Mod '{m.ModId}' requires '{targetId}' version '{dep.VersionRange}', but found version '{targetManifest.Version}'.",
                                    m.RootDirectoryPath);
                                validByModId.Remove(modId);
                                removedAny = true;
                                break;
                            }
                        }
                    }
                }
            }

            // 3. Build directed graph: edge U -> V means U must load BEFORE V
            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var inDegree = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string modId in validByModId.Keys)
            {
                adjacency[modId] = new HashSet<string>(StringComparer.Ordinal);
                inDegree[modId] = 0;
            }

            void AddDirectedEdge(string beforeModId, string afterModId)
            {
                if (string.Equals(beforeModId, afterModId, StringComparison.Ordinal)) return;
                if (!validByModId.ContainsKey(beforeModId) || !validByModId.ContainsKey(afterModId)) return;

                if (adjacency[beforeModId].Add(afterModId))
                {
                    inDegree[afterModId]++;
                }
            }

            foreach (ModManifest m in validByModId.Values)
            {
                if (m.Dependencies != null)
                {
                    foreach (ModDependencySpec dep in m.Dependencies)
                    {
                        if (dep != null && !string.IsNullOrWhiteSpace(dep.ModId))
                        {
                            AddDirectedEdge(dep.ModId.Trim(), m.ModId);
                        }
                    }
                }

                foreach (string loadAfterId in m.GetEffectiveLoadAfter())
                {
                    AddDirectedEdge(loadAfterId, m.ModId);
                }

                foreach (string loadBeforeId in m.GetEffectiveLoadBefore())
                {
                    AddDirectedEdge(m.ModId, loadBeforeId);
                }
            }

            // 4. Kahn's topological sort with deterministic profile-priority tie-breaking
            var ready = validByModId.Keys
                .Where(id => inDegree[id] == 0)
                .OrderBy(id => priorityOrder[id])
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();

            while (ready.Count > 0)
            {
                string currentId = ready[0];
                ready.RemoveAt(0);
                orderedManifests.Add(validByModId[currentId]);

                foreach (string neighbor in adjacency[currentId])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        ready.Add(neighbor);
                    }
                }

                ready.Sort((a, b) =>
                {
                    int cmp = priorityOrder[a].CompareTo(priorityOrder[b]);
                    return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
                });
            }

            // 5. Detect cycles if not all valid manifests were emitted
            if (orderedManifests.Count < validByModId.Count)
            {
                var cycleParticipants = validByModId.Keys
                    .Where(id => inDegree[id] > 0)
                    .OrderBy(id => priorityOrder[id])
                    .ToList();

                string cycleChain = string.Join(" -> ", cycleParticipants);
                foreach (string cycleModId in cycleParticipants)
                {
                    report.Add(
                        cycleModId,
                        ModDiagnosticSeverity.Error,
                        "MOD-CYCLE",
                        $"Circular dependency or loadOrder cycle detected among mods: [{cycleChain}].",
                        validByModId[cycleModId].RootDirectoryPath);
                }

                return false;
            }

            return !report.HasErrors;
        }
    }
}
