#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Arawn.CrystalSave.Runtime
{
    // Validate concrete paths, not extensions: a metadata file named Slot1.bak is
    // valid unless another live file actually owns that name as its backup.
    internal static class SaveStoragePaths
    {
        static readonly StringComparer Comparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        internal static void ValidatePair(string payload, string metadata, string temporary = null)
            => ValidateGenerations(new[] { payload }, new[] { metadata }, temporary);

        internal static void ValidateGenerations(IEnumerable<string> payloads, IEnumerable<string> metadata,
            string temporary = null)
        {
            var reservations = new Dictionary<string, (string Owner, string Kind)>(Comparer);
            Reserve(reservations, payloads, "payload", true);
            Reserve(reservations, metadata, "metadata", true);
            Reserve(reservations, new[] { temporary }, "temporary verification file", false);
        }

        internal static void ValidateIndependentFiles(IEnumerable<string> files, IEnumerable<string> recoveryPrimaries = null,
            IEnumerable<string> recoverySiblingPrimaries = null)
        {
            var reservations = new Dictionary<string, (string Owner, string Kind)>(Comparer);
            Reserve(reservations, files, "save generation", true);
            // Restoring a recovery file can briefly create its own .previous file.
            // Existing recovery siblings also need an independent durable backup.
            var recoveryPaths = (recoveryPrimaries ?? Array.Empty<string>())
                .Select(primary => (Primary: primary, Suffix: ".previous.previous"))
                .Concat((recoverySiblingPrimaries ?? Array.Empty<string>()).SelectMany(primary =>
                    new[] { ".previous.bak", ".previous.bak.previous" }.Select(suffix => (Primary: primary, Suffix: suffix))));
            foreach (var recovery in recoveryPaths)
            {
                string primary = Path.GetFullPath(recovery.Primary);
                string path = primary + recovery.Suffix;
                if (reservations.TryGetValue(path, out var existing) && !Comparer.Equals(existing.Owner, primary))
                    throw new InvalidOperationException("Crystal Save filename collision at recovery backup '" + path + "'.");
                reservations[path] = (primary, "save generation");
            }
        }

        static void Reserve(Dictionary<string, (string Owner, string Kind)> reservations,
            IEnumerable<string> paths, string kind, bool backup)
        {
            foreach (string path in paths.Where(path => !string.IsNullOrEmpty(path)).Select(Path.GetFullPath).Distinct(Comparer))
            {
                string[] suffixes = backup
                    ? new[] { "", AtomicSaveFile.PreviousSuffix, ".bak", ".bak" + AtomicSaveFile.PreviousSuffix }
                    : new[] { "", AtomicSaveFile.PreviousSuffix };
                foreach (string suffix in suffixes)
                {
                    string reserved = path + suffix;
                    if (reservations.TryGetValue(reserved, out var existing))
                    {
                        if (Comparer.Equals(existing.Owner, path) && existing.Kind == kind) continue;
                        throw new InvalidOperationException($"Crystal Save filename collision at '{reserved}': " +
                            $"{existing.Kind} '{existing.Owner}' and {kind} '{path}' share a live or recovery path. " +
                            "Choose distinct save and metadata filename patterns in SaveSettings.");
                    }
                    reservations.Add(reserved, (path, kind));
                }
            }
        }
    }
}
#endif
