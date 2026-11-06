namespace Core.Modding
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Extension capability tiers defined in <c>MODDING_ARCHITECTURE.md</c> (<c>ARCH-04</c>).
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModTier
    {
        DataOnly = 0,
        AssetMod = 1,
        WorldAndAssetMod = 2,
        DeclarativeScriptedMod = 3,
        TrustedAssembly = 4
    }

    /// <summary>
    /// Default conflict resolution policy when a mod registers or patches a definition whose <c>ResourceId</c> already exists.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModConflictPolicy
    {
        AddOnly = 0,
        MergePatch = 1,
        OverrideExisting = 2
    }

    /// <summary>
    /// Declares a required or optional dependency on another content pack or mod.
    /// </summary>
    [Serializable]
    public sealed class ModDependencySpec
    {
        [JsonProperty("modId")]
        public string ModId { get; set; }

        [JsonProperty("versionRange")]
        public string VersionRange { get; set; } = "*";

        [JsonProperty("optional")]
        public bool Optional { get; set; }
    }

    /// <summary>
    /// Explicit ordering hints relative to other mods.
    /// </summary>
    [Serializable]
    public sealed class ModLoadOrderSpec
    {
        [JsonProperty("loadAfter")]
        public List<string> LoadAfter { get; set; } = new();

        [JsonProperty("loadBefore")]
        public List<string> LoadBefore { get; set; } = new();
    }

    /// <summary>
    /// Relative directory paths inside a mod root folder.
    /// </summary>
    [Serializable]
    public sealed class ModMountSpec
    {
        [JsonProperty("definitionsPath")]
        public string DefinitionsPath { get; set; } = "Data/Definitions";

        [JsonProperty("patchesPath")]
        public string PatchesPath { get; set; } = "Data/Patches";

        [JsonProperty("assetsPath")]
        public string AssetsPath { get; set; } = "Assets";

        [JsonProperty("assetBundlesPath")]
        public string AssetBundlesPath { get; set; } = "Bundles";

        [JsonProperty("worldsPath")]
        public string WorldsPath { get; set; } = "Worlds";
    }

    /// <summary>
    /// Canonical <c>mod.json</c> manifest model for Content Packs and User Mods (<c>ARCH-04</c>).
    /// </summary>
    [Serializable]
    public sealed class ModManifest
    {
        private static readonly Regex ValidModIdPattern = new(@"^[a-z0-9_]+$", RegexOptions.Compiled);

        [JsonProperty("$schema")]
        public string Schema { get; set; } = "https://fantasyengine.local/schemas/mod_manifest.v1.json";

        [JsonProperty("manifestVersion")]
        public int ManifestVersion { get; set; } = 1;

        [JsonProperty("modId")]
        public string ModId { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; } = "Unknown";

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("engineVersion")]
        public string EngineVersion { get; set; } = ">=0.5.0 <2.0.0";

        [JsonProperty("tier")]
        public ModTier Tier { get; set; } = ModTier.DataOnly;

        [JsonProperty("conflictPolicy")]
        public ModConflictPolicy ConflictPolicy { get; set; } = ModConflictPolicy.MergePatch;

        [JsonProperty("requiresTrustedExecution")]
        public bool RequiresTrustedExecution { get; set; }

        [JsonProperty("dependencies")]
        public List<ModDependencySpec> Dependencies { get; set; } = new();

        [JsonProperty("loadOrder")]
        public ModLoadOrderSpec LoadOrder { get; set; } = new();

        /// <summary>Optional root-level shorthand for <c>loadOrder.loadAfter</c>.</summary>
        [JsonProperty("loadAfter")]
        public List<string> RootLoadAfter { get; set; } = new();

        /// <summary>Optional root-level shorthand for <c>loadOrder.loadBefore</c>.</summary>
        [JsonProperty("loadBefore")]
        public List<string> RootLoadBefore { get; set; } = new();

        [JsonProperty("mounts")]
        public ModMountSpec Mounts { get; set; } = new();

        /// <summary>Runtime directory path where this manifest was loaded from.</summary>
        [JsonIgnore]
        public string RootDirectoryPath { get; set; }

        /// <summary>Returns true if <see cref="ModId"/> matches <c>^[a-z0-9_]+$</c>.</summary>
        public static bool IsValidModId(string modId)
        {
            return !string.IsNullOrWhiteSpace(modId) && ValidModIdPattern.IsMatch(modId);
        }

        /// <summary>Returns all combined <c>loadAfter</c> mod IDs from nested and root properties.</summary>
        public IEnumerable<string> GetEffectiveLoadAfter()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (LoadOrder?.LoadAfter != null)
            {
                foreach (var id in LoadOrder.LoadAfter)
                {
                    if (!string.IsNullOrWhiteSpace(id)) set.Add(id.Trim());
                }
            }
            if (RootLoadAfter != null)
            {
                foreach (var id in RootLoadAfter)
                {
                    if (!string.IsNullOrWhiteSpace(id)) set.Add(id.Trim());
                }
            }
            return set;
        }

        /// <summary>Returns all combined <c>loadBefore</c> mod IDs from nested and root properties.</summary>
        public IEnumerable<string> GetEffectiveLoadBefore()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (LoadOrder?.LoadBefore != null)
            {
                foreach (var id in LoadOrder.LoadBefore)
                {
                    if (!string.IsNullOrWhiteSpace(id)) set.Add(id.Trim());
                }
            }
            if (RootLoadBefore != null)
            {
                foreach (var id in RootLoadBefore)
                {
                    if (!string.IsNullOrWhiteSpace(id)) set.Add(id.Trim());
                }
            }
            return set;
        }
    }

    /// <summary>
    /// Lightweight, deterministic Semantic Version (<c>Major.Minor.Patch</c>) parser and range evaluator.
    /// Supports <c>*</c>, <c>^1.0.0</c>, <c>~1.2.0</c>, <c>&gt;=1.0.0</c>, <c>&lt;2.0.0</c>, and space-separated compound ranges.
    /// </summary>
    public readonly struct SemVer : IComparable<SemVer>, IEquatable<SemVer>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;

        public SemVer(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public static bool TryParse(string text, out SemVer version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string cleaned = text.Trim();
            if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(1);
            }

            int dashIdx = cleaned.IndexOf('-');
            if (dashIdx >= 0) cleaned = cleaned.Substring(0, dashIdx);

            string[] parts = cleaned.Split('.');
            if (parts.Length < 2 || parts.Length > 3) return false;

            if (!int.TryParse(parts[0], out int major) || major < 0) return false;
            if (!int.TryParse(parts[1], out int minor) || minor < 0) return false;

            int patch = 0;
            if (parts.Length == 3 && (!int.TryParse(parts[2], out patch) || patch < 0))
            {
                return false;
            }

            version = new SemVer(major, minor, patch);
            return true;
        }

        public bool SatisfiesRange(string rangeExpression)
        {
            if (string.IsNullOrWhiteSpace(rangeExpression) || rangeExpression.Trim() == "*")
            {
                return true;
            }

            string[] tokens = rangeExpression.Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawToken in tokens)
            {
                if (!SatisfiesSingleComparator(rawToken.Trim()))
                {
                    return false;
                }
            }

            return true;
        }

        private bool SatisfiesSingleComparator(string token)
        {
            if (string.IsNullOrEmpty(token) || token == "*") return true;

            if (token.StartsWith("^", StringComparison.Ordinal))
            {
                if (!TryParse(token.Substring(1), out SemVer min)) return false;
                SemVer max = min.Major > 0
                    ? new SemVer(min.Major + 1, 0, 0)
                    : new SemVer(0, min.Minor + 1, 0);
                return CompareTo(min) >= 0 && CompareTo(max) < 0;
            }

            if (token.StartsWith("~", StringComparison.Ordinal))
            {
                if (!TryParse(token.Substring(1), out SemVer min)) return false;
                var max = new SemVer(min.Major, min.Minor + 1, 0);
                return CompareTo(min) >= 0 && CompareTo(max) < 0;
            }

            if (token.StartsWith(">=", StringComparison.Ordinal))
            {
                return TryParse(token.Substring(2), out SemVer target) && CompareTo(target) >= 0;
            }

            if (token.StartsWith("<=", StringComparison.Ordinal))
            {
                return TryParse(token.Substring(2), out SemVer target) && CompareTo(target) <= 0;
            }

            if (token.StartsWith(">", StringComparison.Ordinal))
            {
                return TryParse(token.Substring(1), out SemVer target) && CompareTo(target) > 0;
            }

            if (token.StartsWith("<", StringComparison.Ordinal))
            {
                return TryParse(token.Substring(1), out SemVer target) && CompareTo(target) < 0;
            }

            if (token.StartsWith("=", StringComparison.Ordinal))
            {
                return TryParse(token.Substring(1), out SemVer target) && Equals(target);
            }

            return TryParse(token, out SemVer exact) && Equals(exact);
        }

        public int CompareTo(SemVer other)
        {
            int c = Major.CompareTo(other.Major);
            if (c != 0) return c;
            c = Minor.CompareTo(other.Minor);
            if (c != 0) return c;
            return Patch.CompareTo(other.Patch);
        }

        public bool Equals(SemVer other) => Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        public override bool Equals(object obj) => obj is SemVer other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }

    /// <summary>
    /// Severity level for entries in a <see cref="ModDiagnosticReport"/>.
    /// </summary>
    public enum ModDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// Single structured diagnostic entry emitted during mod discovery, load-order solving, JSON parsing, or conflict resolution.
    /// </summary>
    [Serializable]
    public sealed class ModDiagnosticEntry
    {
        public string ModId { get; set; }
        public ModDiagnosticSeverity Severity { get; set; }
        public string Code { get; set; }
        public string FilePath { get; set; }
        public int LineNumber { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            string loc = !string.IsNullOrEmpty(FilePath)
                ? (LineNumber > 0 ? $" ({FilePath}:{LineNumber})" : $" ({FilePath})")
                : string.Empty;
            return $"[{Severity}][{Code}][Mod:{ModId}]{loc} {Message}";
        }
    }

    /// <summary>
    /// Aggregated diagnostic report produced by <see cref="ModManagerService"/> and <see cref="ModLoadOrderSolver"/>.
    /// </summary>
    public sealed class ModDiagnosticReport
    {
        private readonly List<ModDiagnosticEntry> _entries = new();

        public IReadOnlyList<ModDiagnosticEntry> Entries => _entries;
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void Add(
            string modId,
            ModDiagnosticSeverity severity,
            string code,
            string message,
            string filePath = null,
            int lineNumber = 0)
        {
            if (severity == ModDiagnosticSeverity.Error) ErrorCount++;
            else if (severity == ModDiagnosticSeverity.Warning) WarningCount++;

            _entries.Add(new ModDiagnosticEntry
            {
                ModId = modId ?? "unknown",
                Severity = severity,
                Code = code ?? "MOD-GEN",
                Message = message ?? string.Empty,
                FilePath = filePath,
                LineNumber = lineNumber
            });
        }

        public void Clear()
        {
            _entries.Clear();
            ErrorCount = 0;
            WarningCount = 0;
        }
    }
}
