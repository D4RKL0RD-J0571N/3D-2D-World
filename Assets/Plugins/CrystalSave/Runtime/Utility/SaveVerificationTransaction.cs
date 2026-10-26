#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    internal interface ISaveVerificationOwner
    {
        bool WritesLocalSaves { get; }
        SaveVerificationTransaction Verification { get; }
        void PrepareRollback(SaveSlot slot);
        byte[] ReadVerificationPayload(SaveSlot slot);
        byte[] ReadVerificationMetadata(SaveSlot slot);
    }

    // This record outlives the short payload/metadata commit journal. It retains the
    // actual previous paths until verification has been acknowledged durably.
    internal sealed class SaveVerificationTransaction
    {
        const string Suffix = ".csav-verify";
        const string BatchSuffix = ".csav-verified-batch";
        readonly string root;
        readonly HashSet<int> active = new();
        internal static Action<string> FaultInjection;

        internal sealed class Entry
        {
            internal string Path;
            internal string RecoveryPrimary;
            internal bool Preferences, Existed, Obsolete, RecoverySibling;
            internal byte[] Digest = Array.Empty<byte>();
            internal string Backup => (string.IsNullOrEmpty(RecoveryPrimary) || RecoverySibling ? Path : RecoveryPrimary) + (Preferences ? "_bak" : ".bak");
        }
        sealed class Record
        {
            internal bool Verified;
            internal readonly List<Entry> Entries = new();
        }
        internal SaveVerificationTransaction(string root) { this.root = root; }
        string Manifest(int slot) => Path.Combine(root, slot + Suffix);
        internal bool IsActive(int slot) => active.Contains(slot);

        internal void BeginFiles(int slot, string previousPayload, string previousMetadata, string incomingPayload, string incomingMetadata)
            => BeginFileGenerations(slot, previousPayload, previousPayload, previousMetadata, previousMetadata, incomingPayload, incomingMetadata);

        internal void BeginFileGenerations(int slot, string previousPayload, string previousPayloadPrimary,
            string previousMetadata, string previousMetadataPrimary, string incomingPayload, string incomingMetadata)
        {
            SaveStoragePaths.ValidateGenerations(new[] { previousPayloadPrimary, incomingPayload },
                new[] { previousMetadataPrimary, incomingMetadata });
            foreach (string path in new[] { previousPayload, previousPayloadPrimary, previousMetadata, previousMetadataPrimary, incomingPayload, incomingMetadata })
                if (!string.IsNullOrEmpty(path)) ValidatePath(path);
            var record = new Record();
            foreach (string path in new[] { previousPayload, previousMetadata, incomingPayload, incomingMetadata }.Where(p => !string.IsNullOrEmpty(p)).Distinct())
            {
                string primary = path == previousPayload ? previousPayloadPrimary : path == previousMetadata ? previousMetadataPrimary : path;
                record.Entries.Add(new Entry { Path = path, RecoveryPrimary = path == primary ? null : primary,
                    Existed = File.Exists(path), Obsolete = path != incomingPayload && path != incomingMetadata });
            }
            ValidateEntries(record);
            foreach (var entry in record.Entries.Where(entry => !string.IsNullOrEmpty(entry.RecoveryPrimary)))
                if (File.Exists(entry.RecoveryPrimary)) throw new InvalidDataException("A recovery-only generation already has a primary file.");
            Recover();
            foreach (var entry in record.Entries) entry.Existed = File.Exists(entry.Path);
            // A live file can already have an older atomic recovery generation.
            // The next replacement overwrites that sibling, so retain it separately
            // from both the live-file backup and recovery-only provenance.
            foreach (var entry in record.Entries.Where(entry => string.IsNullOrEmpty(entry.RecoveryPrimary)).ToArray())
            {
                string recovery = entry.Path + AtomicSaveFile.PreviousSuffix;
                if (File.Exists(recovery) && !record.Entries.Any(other => other.Path == recovery))
                    record.Entries.Add(new Entry { Path = recovery, RecoveryPrimary = entry.Path,
                        RecoverySibling = true, Existed = true, Obsolete = entry.Obsolete });
            }
            ValidateEntries(record);
            Begin(slot, record);
        }

        internal void BeginPreferences(int slot, string previousPayload, string incomingPayload, string metadata)
        {
            Recover();
            var record = new Record();
            foreach (var key in new[] { previousPayload, incomingPayload, metadata }.Distinct())
                record.Entries.Add(new Entry { Path = key, Preferences = true, Existed = PlayerPrefs.HasKey(key), Obsolete = key != incomingPayload && key != metadata });
            Begin(slot, record);
        }

        void Begin(int slot, Record record)
        {
            if (active.Contains(slot)) throw new InvalidOperationException("Verification is already active for this slot.");
            try
            {
                foreach (var entry in record.Entries)
                {
                    if (!entry.Existed) continue;
                    if (entry.Preferences) PlayerPrefs.SetString(entry.Backup, PlayerPrefs.GetString(entry.Path));
                    else AtomicSaveFile.Copy(entry.Path, entry.Backup, true);
                    using var hash = SHA256.Create();
                    entry.Digest = hash.ComputeHash(entry.Preferences
                        ? Encoding.UTF8.GetBytes(PlayerPrefs.GetString(entry.Backup)) : File.ReadAllBytes(entry.Backup));
                }
                if (record.Entries.Any(e => e.Preferences)) PlayerPrefs.Save();
                Write(slot, record);
                active.Add(slot);
                FaultInjection?.Invoke("prepared");
            }
            catch
            {
                active.Remove(slot);
                // A published record owns its backups until recovery. Before publication
                // no payload has changed, so only temporary backups need removing.
                if (!File.Exists(Manifest(slot)))
                    foreach (var entry in record.Entries) Delete(entry, true);
                throw;
            }
        }

        internal void Recover()
        {
            if (!Directory.Exists(root)) return;
            foreach (string batch in Directory.GetFiles(root, "*" + BatchSuffix))
                FinishVerifiedBatch(batch);
            foreach (string path in Directory.GetFiles(root, "*" + Suffix))
            {
                if (!int.TryParse(Path.GetFileName(path).Replace(Suffix, ""), out int slot)) throw new InvalidDataException("Invalid verification record name.");
                if (active.Contains(slot)) continue;
                var record = Read(slot);
                if (record.Verified) Cleanup(slot, record);
                else Rollback(slot, record);
            }
        }

        internal bool Restore(int slot)
        {
            if (!File.Exists(Manifest(slot))) return false;
            var previous = SaveIoScope.Current.Value;
            SaveIoScope.Current.Value = null; // Rollback must finish even when pre-commit work was cancelled.
            try { Rollback(slot, Read(slot)); }
            finally { SaveIoScope.Current.Value = previous; active.Remove(slot); }
            return true;
        }

        internal async Task RestoreAsync(int slot)
        {
            if (!Restore(slot)) throw new IOException("No verification recovery record exists.");
#if UNITY_WEBGL && !UNITY_EDITOR
            await WebGLFileSystem.FlushAsync();
#else
            await Task.CompletedTask;
#endif
        }

        internal void Complete(int slot)
        {
            if (!File.Exists(Manifest(slot))) throw new IOException("Verification recovery record is missing.");
            var record = Read(slot);
            record.Verified = true;
            Write(slot, record);
            active.Remove(slot);
            TryCleanup(slot, record);
        }

        internal async Task CompleteAsync(int slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var record = Read(slot);
            record.Verified = true;
            Write(slot, record);
            try { await WebGLFileSystem.FlushAsync(); }
            catch
            {
                record.Verified = false;
                Write(slot, record);
                throw;
            }
            active.Remove(slot);
            TryCleanup(slot, record);
            try { await WebGLFileSystem.FlushAsync(); }
            catch (Exception ex) { Logger.Log("Save verified; browser cleanup will be retried: " + ex.Message, LogLevel.Warning); }
#else
            Complete(slot);
            await Task.CompletedTask;
#endif
        }

        // A single durable decision covers every local slot in a rotating save.
        // Cleanup may stop after any entry; restart must never roll back the remaining
        // entries of an already committed batch independently.
        internal async Task CompleteBatchAsync(IEnumerable<int> slots)
        {
            int[] numbers = slots.Distinct().ToArray();
            if (numbers.Length == 0) return;
            foreach (int number in numbers)
                if (!active.Contains(number) || !File.Exists(Manifest(number)))
                    throw new IOException("A rotating save recovery record is missing.");
            string path = Path.Combine(root, Guid.NewGuid().ToString("N") + BatchSuffix);
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(1);
                    writer.Write(numbers.Length);
                    foreach (int number in numbers) writer.Write(number);
                }
                AtomicSaveFile.WriteAllBytes(path, stream.ToArray());
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            try { await WebGLFileSystem.FlushAsync(); }
            catch
            {
                AtomicSaveFile.Delete(path);
                throw;
            }
#else
            await Task.CompletedTask;
#endif
            // The marker is authoritative now. Cleanup errors are not a failed save.
            foreach (int number in numbers) active.Remove(number);
            try
            {
                FaultInjection?.Invoke("batch-verified");
                FinishVerifiedBatch(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { Logger.Log("Rotating save committed; batch cleanup will be retried: " + ex.Message, LogLevel.Warning); }
#if UNITY_WEBGL && !UNITY_EDITOR
            try { await WebGLFileSystem.FlushAsync(); }
            catch (Exception ex) { Logger.Log("Rotating save committed; browser batch cleanup will be retried: " + ex.Message, LogLevel.Warning); }
#endif
        }

        void FinishVerifiedBatch(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported rotating save recovery record.");
            int count = reader.ReadInt32();
            if (count < 1 || count > 100000 || stream.Length != 8L + count * 4L)
                throw new InvalidDataException("Invalid rotating save recovery record.");
            var numbers = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int number = reader.ReadInt32();
                if (number < 0 || !numbers.Add(number)) throw new InvalidDataException("Invalid rotating save slot identity.");
            }
            foreach (int number in numbers)
            {
                if (!File.Exists(Manifest(number))) continue;
                var record = Read(number);
                record.Verified = true;
                Write(number, record);
                active.Remove(number);
                Cleanup(number, record);
            }
            AtomicSaveFile.Delete(path);
        }

        internal void Discard(int slot)
        {
            if (File.Exists(Manifest(slot)))
            {
                var record = Read(slot);
                foreach (var entry in record.Entries) Delete(entry, true);
                AtomicSaveFile.Delete(Manifest(slot));
            }
            active.Remove(slot);
        }

        internal IEnumerable<string> DeletionFiles(int slot)
        {
            if (!File.Exists(Manifest(slot))) return Array.Empty<string>();
            return Read(slot).Entries.Where(entry => !entry.Preferences)
                .SelectMany(entry => new[] { entry.Path, entry.Backup })
                .Append(Manifest(slot)).ToArray();
        }

        internal IEnumerable<string> DeletionPreferenceKeys(int slot)
        {
            if (!File.Exists(Manifest(slot))) return Array.Empty<string>();
            return Read(slot).Entries.Where(entry => entry.Preferences)
                .SelectMany(entry => new[] { entry.Path, entry.Backup }).ToArray();
        }

        void Rollback(int slot, Record record)
        {
            // Validate/read every backup first. A missing backup must not leave a half-restored pair.
            var bytes = new Dictionary<Entry, byte[]>();
            foreach (var entry in record.Entries.Where(e => e.Existed))
            {
                if (entry.Preferences)
                {
                    if (!PlayerPrefs.HasKey(entry.Backup)) throw new IOException("Required verification backup is missing: " + entry.Backup);
                    bytes[entry] = Encoding.UTF8.GetBytes(PlayerPrefs.GetString(entry.Backup));
                }
                else bytes[entry] = File.ReadAllBytes(entry.Backup);
                using var hash = SHA256.Create();
                if (!hash.ComputeHash(bytes[entry]).SequenceEqual(entry.Digest))
                    throw new IOException("Required verification backup is corrupt: " + entry.Backup);
            }
            FaultInjection?.Invoke("rollback");
            var preservedFiles = new HashSet<string>(record.Entries.Where(entry => !entry.Preferences && entry.Existed).Select(entry => entry.Path));
            foreach (var entry in record.Entries)
            {
                if (!entry.Existed) Delete(entry, false, preservedFiles);
                else if (entry.Preferences) PlayerPrefs.SetString(entry.Path, Encoding.UTF8.GetString(bytes[entry]));
                else
                {
                    if (!File.Exists(entry.Path) || !File.ReadAllBytes(entry.Path).SequenceEqual(bytes[entry]))
                        AtomicSaveFile.WriteAllBytes(entry.Path, bytes[entry]);
                    string recovery = entry.Path + AtomicSaveFile.PreviousSuffix;
                    if (!preservedFiles.Contains(recovery) && File.Exists(recovery)) File.Delete(recovery);
                }
                FaultInjection?.Invoke("rollback-entry");
            }
            if (record.Entries.Any(e => e.Preferences)) PlayerPrefs.Save();
            // Atomically mark rollback complete before deleting its required backup files.
            record.Verified = true;
            foreach (var entry in record.Entries) entry.Obsolete = false;
            Write(slot, record);
            Cleanup(slot, record);
        }

        void TryCleanup(int slot, Record record)
        {
            try { FaultInjection?.Invoke("verified"); Cleanup(slot, record); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            { Logger.Log("Save verified; cleanup will be retried: " + ex.Message, LogLevel.Warning); }
        }

        void Cleanup(int slot, Record record)
        {
            FaultInjection?.Invoke("cleanup");
            foreach (var entry in record.Entries)
            {
                if (entry.Obsolete) Delete(entry, false);
                Delete(entry, true);
                FaultInjection?.Invoke("cleanup-entry");
            }
            if (record.Entries.Any(e => e.Preferences)) PlayerPrefs.Save();
            AtomicSaveFile.Delete(Manifest(slot));
        }

        static void Delete(Entry entry, bool backup, HashSet<string> preservedFiles = null)
        {
            string path = backup ? entry.Backup : entry.Path;
            if (entry.Preferences) PlayerPrefs.DeleteKey(path);
            else if (preservedFiles != null && preservedFiles.Contains(path + AtomicSaveFile.PreviousSuffix))
            {
                // The absent incoming primary may own an original recovery-only file.
                // Do not let AtomicSaveFile.Delete remove that separately recorded state.
                if (File.Exists(path)) File.Delete(path);
            }
            else AtomicSaveFile.Delete(path);
        }

        void Write(int slot, Record record)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                int version = record.Entries.Any(entry => entry.RecoverySibling) ? 3 :
                    record.Entries.Any(entry => !string.IsNullOrEmpty(entry.RecoveryPrimary)) ? 2 : 1;
                writer.Write(version); writer.Write(record.Verified); writer.Write(record.Entries.Count);
                foreach (var entry in record.Entries)
                {
                    writer.Write(entry.Path); writer.Write(entry.Preferences); writer.Write(entry.Existed); writer.Write(entry.Obsolete);
                    if (version >= 2) writer.Write(entry.RecoveryPrimary ?? string.Empty);
                    if (version >= 3) writer.Write(entry.RecoverySibling);
                    writer.Write(entry.Digest.Length); writer.Write(entry.Digest);
                }
            }
            AtomicSaveFile.WriteAllBytes(Manifest(slot), stream.ToArray());
        }

        Record Read(int slot)
        {
            using var stream = File.OpenRead(Manifest(slot));
            if (stream.Length > 65536) throw new InvalidDataException("Oversized verification record.");
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            int version = reader.ReadInt32();
            if (version != 1 && version != 2 && version != 3) throw new InvalidDataException("Unsupported verification record.");
            var record = new Record { Verified = reader.ReadBoolean() };
            int count = reader.ReadInt32();
            if (count < 1 || count > (version >= 3 ? 8 : 4)) throw new InvalidDataException("Invalid verification entry count.");
            for (int i = 0; i < count; i++)
            {
                var entry = new Entry { Path = reader.ReadString(), Preferences = reader.ReadBoolean(), Existed = reader.ReadBoolean(), Obsolete = reader.ReadBoolean() };
                if (version >= 2) entry.RecoveryPrimary = reader.ReadString();
                if (version >= 3) entry.RecoverySibling = reader.ReadBoolean();
                int hashLength = reader.ReadInt32();
                if (hashLength != (entry.Existed ? 32 : 0)) throw new InvalidDataException("Invalid verification backup digest.");
                entry.Digest = reader.ReadBytes(hashLength);
                if (entry.Digest.Length != hashLength) throw new EndOfStreamException();
                if (!entry.Preferences) ValidatePath(entry.Path);
                record.Entries.Add(entry);
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Trailing verification record data.");
            ValidateEntries(record);
            return record;
        }

        void ValidateEntries(Record record)
        {
            if (record.Entries.Select(entry => entry.Path).Distinct().Count() != record.Entries.Count)
                throw new InvalidDataException("Duplicate verification entry.");
            if (record.Entries.Any(entry => entry.RecoverySibling && string.IsNullOrEmpty(entry.RecoveryPrimary)))
                throw new InvalidDataException("Invalid verification recovery sibling.");
            foreach (var entry in record.Entries.Where(entry => !string.IsNullOrEmpty(entry.RecoveryPrimary)))
            {
                ValidatePath(entry.RecoveryPrimary);
                if (entry.Preferences || !entry.Existed || entry.Path != entry.RecoveryPrimary + AtomicSaveFile.PreviousSuffix ||
                    (!entry.RecoverySibling && record.Entries.Any(other => other.Path == entry.RecoveryPrimary && other.Existed)) ||
                    (entry.RecoverySibling && !record.Entries.Any(other => other.Path == entry.RecoveryPrimary && string.IsNullOrEmpty(other.RecoveryPrimary))))
                    throw new InvalidDataException("Invalid verification recovery provenance.");
            }
            SaveStoragePaths.ValidateIndependentFiles(record.Entries.Where(entry => !entry.Preferences)
                    .Select(entry => string.IsNullOrEmpty(entry.RecoveryPrimary) ? entry.Path : entry.RecoveryPrimary),
                record.Entries.Where(entry => !string.IsNullOrEmpty(entry.RecoveryPrimary)).Select(entry => entry.RecoveryPrimary),
                record.Entries.Where(entry => entry.RecoverySibling).Select(entry => entry.RecoveryPrimary));
        }

        void ValidatePath(string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Verification path is outside the save root.");
        }
    }
}
#endif
