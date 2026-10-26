#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Linq;
namespace Arawn.CrystalSave.Runtime
{
    internal readonly struct SaveWritePlan
    {
        internal readonly bool Local, Remote;
        SaveWritePlan(bool cloud, bool mirror, bool signedIn)
        { Remote = cloud && signedIn; Local = mirror || (cloud && !signedIn); }

        internal static SaveWritePlan Capture(object provider, bool cloud, bool mirror, bool signedIn)
        {
            var context = SaveIoScope.Current.Value;
            if (context == null) return new SaveWritePlan(cloud, mirror, signedIn);
            lock (context.WritePlans)
            {
                if (context.Ended) throw new OperationCanceledException("The save operation has ended.");
                if (!context.WritePlans.TryGetValue(provider, out var plan))
                    context.WritePlans.Add(provider, plan = new SaveWritePlan(cloud, mirror, signedIn));
                return plan;
            }
        }

        internal void RequireRemoteAvailable(bool available)
        {
            if (Remote && !available)
                throw new InvalidOperationException("Cloud authentication became unavailable during save; the planned cloud write cannot fall back to local storage.");
        }
    }

    internal interface ISaveWritePathValidator
    {
        void ValidateWritePaths(SaveSlot slot);
    }

    public partial class SaveSystem : ISaveVerificationOwner, ISaveWritePathValidator
    {
        bool ISaveVerificationOwner.WritesLocalSaves => WritesLocalSave;
        void ISaveWritePathValidator.ValidateWritePaths(SaveSlot slot) => ValidateSlotFilePaths(slot);
        SaveVerificationTransaction verification;
        SaveVerificationTransaction Verification => verification ??= new SaveVerificationTransaction(persistentPath);
        SaveVerificationTransaction ISaveVerificationOwner.Verification => Verification;
        void ISaveVerificationOwner.PrepareRollback(SaveSlot slot) => PrepareRollbackSlot(slot);
        byte[] ISaveVerificationOwner.ReadVerificationPayload(SaveSlot slot)
        {
            // Read the generation just committed, including a signed-out cloud
            // fallback. Public Load retains its configured mirror policy.
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string key = ResolveSaveKey(slot.SlotNumber);
                return UnityEngine.PlayerPrefs.HasKey(key) ? Convert.FromBase64String(UnityEngine.PlayerPrefs.GetString(key)) : null;
            }
            return ConflictCandidateReader.FileBytes(Path.Combine(SlotFolder(slot.SlotNumber), ResolveSaveFileStem(slot) + ".sav"));
        }
        byte[] ISaveVerificationOwner.ReadVerificationMetadata(SaveSlot slot)
        {
            if (settings.saveMethod == SaveMethod.PlayerPrefs)
            {
                string key = settings.saveKey + "_Metadata_" + slot.SlotNumber;
                return UnityEngine.PlayerPrefs.HasKey(key) ? Convert.FromBase64String(UnityEngine.PlayerPrefs.GetString(key)) : null;
            }
            return ConflictCandidateReader.FileBytes(Path.Combine(SlotFolder(slot.SlotNumber), GetSlotMetadataFileName(slot)));
        }
    }
    public partial class MySqlSaveSystem : ISaveVerificationOwner
    {
        bool ISaveVerificationOwner.WritesLocalSaves => LocalWritesEnabled();
        SaveVerificationTransaction verification;
        SaveVerificationTransaction Verification => verification ??= new SaveVerificationTransaction(persistentPath);
        SaveVerificationTransaction ISaveVerificationOwner.Verification => Verification;
        void ISaveVerificationOwner.PrepareRollback(SaveSlot slot) => PrepareRollbackSlot(slot);
        byte[] ISaveVerificationOwner.ReadVerificationMetadata(SaveSlot slot) => ConflictCandidateReader.FileBytes(MetaLocalPath(slot.SlotNumber));
        byte[] ISaveVerificationOwner.ReadVerificationPayload(SaveSlot slot) => ConflictCandidateReader.FileBytes(Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"));
    }
    public partial class FirebaseSaveSystem : ISaveVerificationOwner
    {
        bool ISaveVerificationOwner.WritesLocalSaves => LocalWritesEnabled();
        SaveVerificationTransaction verification;
        SaveVerificationTransaction Verification => verification ??= new SaveVerificationTransaction(persistentPath);
        SaveVerificationTransaction ISaveVerificationOwner.Verification => Verification;
        void ISaveVerificationOwner.PrepareRollback(SaveSlot slot) => PrepareRollbackSlot(slot);
        byte[] ISaveVerificationOwner.ReadVerificationMetadata(SaveSlot slot) => ConflictCandidateReader.FileBytes(MetaLocalPath(slot.SlotNumber));
        byte[] ISaveVerificationOwner.ReadVerificationPayload(SaveSlot slot) => ConflictCandidateReader.FileBytes(Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"));
    }
    public partial class SupabaseSaveSystem : ISaveVerificationOwner
    {
        bool ISaveVerificationOwner.WritesLocalSaves => LocalWritesEnabled();
        SaveVerificationTransaction verification;
        SaveVerificationTransaction Verification => verification ??= new SaveVerificationTransaction(persistentPath);
        SaveVerificationTransaction ISaveVerificationOwner.Verification => Verification;
        void ISaveVerificationOwner.PrepareRollback(SaveSlot slot) => PrepareRollbackSlot(slot);
        byte[] ISaveVerificationOwner.ReadVerificationMetadata(SaveSlot slot) => ConflictCandidateReader.FileBytes(MetaLocalPath(slot.SlotNumber));
        byte[] ISaveVerificationOwner.ReadVerificationPayload(SaveSlot slot) => ConflictCandidateReader.FileBytes(Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"));
    }
    public partial class SaveSystem
    {
        string PayloadPattern
        {
            get
            {
                string pattern = settings.saveFileName ?? string.Empty;
                return (pattern.Contains("{n}") ? pattern : pattern + "{n}") + ".sav";
            }
        }

        string MetadataPattern => string.IsNullOrWhiteSpace(settings.metadataFileNamePattern) ||
            !settings.metadataFileNamePattern.Contains("{n}") ? "Slot{n}_Meta.bin" : settings.metadataFileNamePattern;

        readonly struct LocalFileGeneration
        {
            internal readonly string Path, PrimaryPath;
            internal LocalFileGeneration(string path, string primaryPath)
            { Path = path; PrimaryPath = primaryPath; }
        }

        static LocalFileGeneration[] GenerationFiles(string folder, string glob, bool includeBackups)
        {
            if (!Directory.Exists(folder)) return Array.Empty<LocalFileGeneration>();
            var suffixes = includeBackups
                ? new[] { "", AtomicSaveFile.PreviousSuffix, ".bak", ".bak" + AtomicSaveFile.PreviousSuffix }
                : new[] { "", AtomicSaveFile.PreviousSuffix };
            return suffixes.SelectMany(suffix => Directory.GetFiles(folder, glob + suffix)
                .Select(path => new LocalFileGeneration(path, path.Substring(0, path.Length - suffix.Length))))
                .GroupBy(file => file.Path).Select(group => group.First()).ToArray();
        }

        SaveSlot ReadOwnedHeader(string path, int slotNumber)
        {
            try
            {
                var header = ConflictCandidateReader.Metadata(File.ReadAllBytes(path));
                return header?.SlotNumber == slotNumber ? header : null;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException && ex is not OperationCanceledException)
            {
                // A wildcard match in the shared legacy root is not evidence of ownership.
                // Leave unidentifiable files intact; exact reads still report invalid data.
                return null;
            }
        }

        LocalFileGeneration[] LocalMetadataFiles(SaveSlot slot, bool includeBackups)
        {
            string glob = PatternToGlob(MetadataPattern, slot.SlotNumber);
            var owned = GenerationFiles(SlotFolder(slot.SlotNumber), glob, includeBackups);
            var legacy = GenerationFiles(persistentPath, glob, includeBackups)
                .Concat(GenerationFiles(persistentPath, $"Slot{slot.SlotNumber}_Meta.bin", includeBackups))
                .GroupBy(file => file.Path).Select(group => group.First())
                .Where(file => ReadOwnedHeader(file.Path, slot.SlotNumber) != null);
            return owned.Concat(legacy).GroupBy(file => file.Path).Select(group => group.First())
                .Select(file => ResolveMetadataGeneration(file, slot.SlotNumber, includeBackups))
                .Where(file => file.HasValue).Select(file => file.Value).ToArray();
        }

        LocalFileGeneration? ResolveMetadataGeneration(LocalFileGeneration file, int slotNumber, bool includeBackups)
        {
            // A trailing metadata wildcard also matches its own .bak/.previous files.
            // Resolve their owner from the stored header instead of treating the wildcard
            // match as another live file. A genuinely configured .bak name remains valid.
            var header = ReadOwnedHeader(file.Path, slotNumber);
            if (header == null) return file;
            string primary = Path.Combine(Path.GetDirectoryName(file.Path), GetSlotMetadataFileName(header));
            foreach (string suffix in new[] { "", AtomicSaveFile.PreviousSuffix, ".bak", ".bak" + AtomicSaveFile.PreviousSuffix })
            {
                if (file.Path != primary + suffix) continue;
                if (!includeBackups && suffix.StartsWith(".bak", StringComparison.Ordinal)) return null;
                return new LocalFileGeneration(file.Path, primary);
            }
            // Retain legacy/default discovery and exact-read error reporting.
            return file;
        }

        LocalFileGeneration[] LocalPayloadFiles(SaveSlot slot, bool includeBackups)
        {
            string glob = PatternToGlob(PayloadPattern, slot.SlotNumber);
            var owned = GenerationFiles(SlotFolder(slot.SlotNumber), glob, includeBackups);
            var rootFiles = GenerationFiles(persistentPath, glob, includeBackups);
            if (rootFiles.Length == 0) return owned;
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            // A metadata-free pattern maps a slot directly to one filename. Metadata wildcards
            // can overlap other slots (including 1/11), so legacy-root variants require a header.
            if (!PayloadPattern.Contains("{meta:")) names.Add(ResolveSaveFileStem(slot) + ".sav");
            else foreach (var file in LocalMetadataFiles(slot, includeBackups))
            {
                var header = ReadOwnedHeader(file.Path, slot.SlotNumber);
                if (header != null) names.Add(ResolveSaveFileStem(header) + ".sav");
            }
            var legacy = rootFiles
                .Where(file => names.Contains(Path.GetFileName(file.PrimaryPath)));
            return owned.Concat(legacy).GroupBy(file => file.Path).Select(group => group.First()).ToArray();
        }

        string SelectCommittedPath(LocalFileGeneration[] files, string folder)
        {
            // Sort the existing generation before recovery changes its destination's timestamp.
            // Do not recover every obsolete variant merely to discover the newest one.
            string directory = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var file in files.Where(file => Path.GetFullPath(Path.GetDirectoryName(file.Path))
                         .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) == directory &&
                         (file.Path == file.PrimaryPath || !File.Exists(file.PrimaryPath)))
                         .OrderByDescending(file => File.GetLastWriteTimeUtc(file.Path)))
            {
                if (FileExists(file.PrimaryPath)) return file.PrimaryPath;
                // Browser writes use acknowledged IDBFS rather than native replacement,
                // so FileExists does not recreate .previous files. Still read existing
                // legacy recovery generations without turning a read into a browser write.
                if (file.Path != file.PrimaryPath && File.Exists(file.Path)) return file.Path;
            }
            return null;
        }

        string FindCommittedMetadataPath(SaveSlot slot)
        {
            var candidates = LocalMetadataFiles(slot, false);
            foreach (string folder in new[] { SlotFolder(slot.SlotNumber), persistentPath }.Distinct())
            {
                string path = SelectCommittedPath(candidates, folder);
                if (path != null) return path;
            }
            return null;
        }

        string FindCommittedPayloadPath(SaveSlot slot)
        {
            string name = ResolveSaveFileStem(slot) + ".sav";
            LocalFileGeneration[] variants = null;
            foreach (string folder in new[] { SlotFolder(slot.SlotNumber), persistentPath }.Distinct())
            {
                string exact = Path.Combine(folder, name);
                if (FileExists(exact)) return exact;
                variants ??= LocalPayloadFiles(slot, false);
                string variant = SelectCommittedPath(variants, folder);
                if (variant != null) return variant;
            }
            return null;
        }
    }
}
#endif
