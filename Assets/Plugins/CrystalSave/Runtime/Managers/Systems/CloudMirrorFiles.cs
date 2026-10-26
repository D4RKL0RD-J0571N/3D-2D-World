#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    // Local mirror operations share native atomic replacement and browser acknowledgement.
    // These helpers cannot roll back an already completed remote request.
    internal static class CloudMirrorFiles
    {
        // Tests can exercise the acknowledged browser boundary using real native files.
        internal static Func<Task> FlushOverride;

        // Discover without promoting a recovery copy: browser reads cannot publish a
        // filesystem mutation, and rollback must retain the exact original generation.
        internal static string FindCommittedPath(string primary)
        {
            if (string.IsNullOrEmpty(primary)) return null;
            if (File.Exists(primary)) return primary;
            string recovery = primary + AtomicSaveFile.PreviousSuffix;
            return File.Exists(recovery) ? recovery : null;
        }

        internal static byte[] ReadCommittedBytes(string primary)
        {
            string path = FindCommittedPath(primary);
            return path == null ? null : File.ReadAllBytes(path);
        }

        internal static void PrepareRollback(SaveVerificationTransaction verification, int slot,
            string payload, string metadata, string legacyMetadata)
        {
            string previousPayload = FindCommittedPath(payload);
            string previousMetadata = FindCommittedPath(metadata);
            string metadataPrimary = metadata;
            if (previousMetadata == null)
            {
                previousMetadata = FindCommittedPath(legacyMetadata);
                metadataPrimary = legacyMetadata;
            }
            verification.BeginFileGenerations(slot, previousPayload, payload,
                previousMetadata, metadataPrimary, payload, metadata);
        }

        internal static string LegacyScreenshotPath(string root, string fileName)
        {
            if (!ScreenshotManager.IsValidScreenshotFileName(fileName)) return null;
            string extension = Path.GetExtension(fileName);
            // Legacy images shared the mirror root with payloads and headers. A header
            // may name only an image there, never another slot's .sav or metadata file.
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(root, fileName) : null;
        }

        internal static Task WriteMetadataAsync(string path, byte[] bytes)
            => ApplyAsync(new[] { path }, () => AtomicSaveFile.WriteAllBytes(path, bytes));

        internal static Task DeleteAsync(IEnumerable<string> paths, Action beforeDelete = null, Action rollback = null)
        {
            var files = paths.Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray();
            return ApplyAsync(files, () =>
            {
                beforeDelete?.Invoke();
                foreach (var path in files) AtomicSaveFile.Delete(path);
            }, rollback);
        }

        static async Task ApplyAsync(string[] paths, Action commit, Action rollback = null)
        {
            SaveIoScope.CheckCancellation();
#if !UNITY_WEBGL || UNITY_EDITOR
            if (FlushOverride == null)
            {
                try { commit(); }
                catch (Exception error)
                {
                    try { rollback?.Invoke(); }
                    catch (Exception rollbackError)
                    {
                        throw new AggregateException("Local mutation and preference rollback failed.", error, rollbackError);
                    }
                    throw;
                }
                return;
            }
#endif
            // Deletion and native test writes also touch recovery copies. Snapshot only
            // the exact mutation's paths, never neighboring slots or the entire root.
            var previous = paths.SelectMany(path => new[] { path, path + AtomicSaveFile.PreviousSuffix })
                .Distinct().ToDictionary(path => path,
                path => File.Exists(path) ? File.ReadAllBytes(path) : null);
            try
            {
                commit();
                await FlushAsync();
            }
            catch (Exception error)
            {
                var rollbackErrors = new List<Exception>();
                try { rollback?.Invoke(); }
                catch (Exception rollbackError) { rollbackErrors.Add(rollbackError); }
                try
                {
                    foreach (var entry in previous)
                    {
                        if (entry.Value == null) { if (File.Exists(entry.Key)) File.Delete(entry.Key); }
                        else
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(entry.Key));
                            File.WriteAllBytes(entry.Key, entry.Value);
                        }
                    }
                    await FlushAsync();
                }
                catch (Exception rollbackError)
                {
                    rollbackErrors.Add(rollbackError);
                }
                if (rollbackErrors.Count != 0)
                {
                    rollbackErrors.Insert(0, error);
                    throw new AggregateException("Local mirror persistence and rollback failed; remote requests may already have completed.", rollbackErrors);
                }
                throw new IOException("Local mirror persistence failed; previous local files were restored. Remote requests may already have completed.", error);
            }
        }

        internal static Task FlushAsync()
        {
            if (FlushOverride != null) return FlushOverride();
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebGLFileSystem.FlushAsync();
#else
            return Task.CompletedTask;
#endif
        }
    }
}
#endif
