#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace Arawn.CrystalSave.Runtime
{
    /// <summary>A recoverable native payload/metadata pair. Remote requests have their own failure boundary.</summary>
    internal static class SaveFileTransaction
    {
        static readonly object Gate = new object();
        internal static Action<string> FaultInjection;
        const string Suffix = ".csav-journal";
        const string CleanupSuffix = ".cleanup";
        internal static async Task CommitAsync(string root, int slot, string payloadPath, byte[] payload, string metadataPath, byte[] metadata)
        {
            SaveStoragePaths.ValidatePair(payloadPath, metadataPath);
#if UNITY_WEBGL && !UNITY_EDITOR
            byte[] oldPayload = File.Exists(payloadPath) ? File.ReadAllBytes(payloadPath) : null;
            byte[] oldMetadata = File.Exists(metadataPath) ? File.ReadAllBytes(metadataPath) : null;
            try
            {
                AtomicSaveFile.WriteAllBytes(payloadPath, payload);
                AtomicSaveFile.WriteAllBytes(metadataPath, metadata);
                FaultInjection?.Invoke("committed");
                await WebGLFileSystem.FlushAsync();
            }
            catch
            {
                if (oldPayload == null) AtomicSaveFile.Delete(payloadPath); else AtomicSaveFile.WriteAllBytes(payloadPath, oldPayload);
                if (oldMetadata == null) AtomicSaveFile.Delete(metadataPath); else AtomicSaveFile.WriteAllBytes(metadataPath, oldMetadata);
                try { await WebGLFileSystem.FlushAsync(); } catch { }
                throw;
            }
#else
            Commit(root, slot, payloadPath, payload, metadataPath, metadata);
            await Task.CompletedTask;
#endif
        }
        internal static void Commit(string root, int slot, string payloadPath, byte[] payload, string metadataPath, byte[] metadata)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException("Browser storage uses IDBFS synchronization, not native journals.");
#else
            lock (Gate)
            {
                SaveStoragePaths.ValidatePair(payloadPath, metadataPath);
                ValidatePath(root, payloadPath);
                ValidatePath(root, metadataPath);
                Directory.CreateDirectory(root);
                string journal = Path.Combine(root, slot + Suffix);
                Recover(journal, root);
                Directory.CreateDirectory(journal);
                FaultInjection?.Invoke("journal");
                string[] paths = { Path.GetFullPath(payloadPath), Path.GetFullPath(metadataPath) };
                byte[][] content = { payload, metadata };
                bool[] existed = new bool[2];
                for (int i = 0; i < 2; i++)
                {
                    ValidatePath(root, paths[i]);
                    existed[i] = AtomicSaveFile.Exists(paths[i]);
                    if (existed[i]) AtomicSaveFile.WriteAllBytes(Path.Combine(journal, i + ".old"), File.ReadAllBytes(paths[i]));
                    AtomicSaveFile.WriteAllBytes(Path.Combine(journal, i + ".new"), content[i]);
                }
                using (var buffer = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true))
                    {
                        writer.Write(1);
                        for (int i = 0; i < 2; i++) { writer.Write(paths[i]); writer.Write(existed[i]); }
                    }
                    AtomicSaveFile.WriteAllBytes(Path.Combine(journal, "prepared"), buffer.ToArray());
                }
                FaultInjection?.Invoke("prepared");
                for (int i = 0; i < 2; i++)
                {
                    AtomicSaveFile.WriteAllBytes(paths[i], content[i]);
                    FaultInjection?.Invoke(i == 0 ? "payload" : "metadata");
                }
                AtomicSaveFile.WriteAllBytes(Path.Combine(journal, "committed"), new byte[] { 1 });
                // From this marker onward the new pair is authoritative, even if cleanup must be retried.
                try { FaultInjection?.Invoke("committed"); FaultInjection?.Invoke("cleanup"); RetireJournal(journal); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) { }
            }
#endif
        }
        internal static void RecoverAll(string root)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            lock (Gate)
            {
                if (!Directory.Exists(root)) return;
                foreach (string retired in Directory.GetDirectories(root, "*" + Suffix + CleanupSuffix, SearchOption.TopDirectoryOnly)) CleanupRetiredJournal(retired);
                foreach (string journal in Directory.GetDirectories(root, "*" + Suffix, SearchOption.TopDirectoryOnly)) Recover(journal, root);
            }
#endif
        }
        internal static void RecoverSlot(string root, int slot)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            lock (Gate) Recover(Path.Combine(root, slot + Suffix), root);
#endif
        }
        static void Recover(string journal, string root)
        {
            CleanupRetiredJournal(journal + CleanupSuffix);
            if (!Directory.Exists(journal)) return;
            if (File.Exists(Path.Combine(journal, "prepared")) && !File.Exists(Path.Combine(journal, "committed")))
            {
                using var reader = new BinaryReader(File.OpenRead(Path.Combine(journal, "prepared")), Encoding.UTF8);
                if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported save journal.");
                var paths = new string[2];
                var previous = new byte[2][];
                for (int i = 0; i < 2; i++)
                {
                    paths[i] = reader.ReadString(); bool existed = reader.ReadBoolean();
                    ValidatePath(root, paths[i]);
                    if (existed) previous[i] = File.ReadAllBytes(Path.Combine(journal, i + ".old"));
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing save journal data.");
                SaveStoragePaths.ValidatePair(paths[0], paths[1]);
                for (int i = 0; i < 2; i++)
                {
                    if (previous[i] != null) AtomicSaveFile.WriteAllBytes(paths[i], previous[i]);
                    else AtomicSaveFile.Delete(paths[i]);
                    string recovery = paths[i] + AtomicSaveFile.PreviousSuffix;
                    if (File.Exists(recovery)) File.Delete(recovery);
                    FaultInjection?.Invoke("rollback-entry");
                }
            }
            RetireJournal(journal);
        }

        static void RetireJournal(string journal)
        {
            // Rename atomically before deleting any decision/backup file. Both committed
            // writes and completed rollbacks are now cleanup-only, even if deletion stops
            // immediately after unlinking the old committed marker. Legacy journals use
            // this same retirement path when first recovered by an updated build.
            string retired = journal + CleanupSuffix;
            Directory.Move(journal, retired);
            FaultInjection?.Invoke("cleanup-retired");
            CleanupRetiredJournal(retired);
        }

        static void CleanupRetiredJournal(string retired)
        {
            if (!Directory.Exists(retired)) return;
            foreach (string file in Directory.GetFiles(retired))
            {
                File.Delete(file);
                FaultInjection?.Invoke("cleanup-file:" + Path.GetFileName(file));
            }
            // Only our staged files occur here. Refuse an unexpected child directory
            // instead of recursively deleting an unrecognized tree.
            Directory.Delete(retired);
            FaultInjection?.Invoke("cleanup-directory");
        }
        static void ValidatePath(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Save journal path is outside its storage root.");
        }
    }
}
#endif
