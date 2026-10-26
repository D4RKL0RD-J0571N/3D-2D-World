#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Text;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Commits local files without truncating the last successful generation. The previous
    /// generation is separate from the optional, short-lived verification .bak file.
    /// Browser durability remains the responsibility of WebGLFileSystem/IDBFS sync.
    /// </summary>
    internal static class AtomicSaveFile
    {
        internal const string PreviousSuffix = ".previous";

        internal static void WriteAllBytes(string path, byte[] data, Action<string> validate = null)
        {
            SaveIoScope.CheckCancellation();
            if (data == null) throw new ArgumentNullException(nameof(data));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
#if UNITY_WEBGL && !UNITY_EDITOR
            File.WriteAllBytes(path, data);
#else
            string temporary = path + ".writing-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }
                validate?.Invoke(temporary);
                if (File.Exists(path))
                    File.Replace(temporary, path, path + PreviousSuffix);
                else
                    File.Move(temporary, path);
                // Do not fall back to delete-then-move if a filesystem cannot replace atomically.
                // A failed save must leave the previous destination intact.
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
#endif
        }

        internal static void WriteAllText(string path, string text)
            => WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text ?? string.Empty));

        internal static bool Exists(string path)
        {
            if (File.Exists(path)) return true;
#if !UNITY_WEBGL || UNITY_EDITOR
            if (File.Exists(path + PreviousSuffix))
            {
                // Recover only a missing destination, never replace an existing save silently.
                WriteAllBytes(path, File.ReadAllBytes(path + PreviousSuffix));
                return true;
            }
#endif
            return false;
        }

        internal static byte[] ReadAllBytes(string path)
        {
            Exists(path);
            return File.ReadAllBytes(path);
        }

        internal static string ReadAllText(string path)
        {
            Exists(path);
            return File.ReadAllText(path);
        }

        internal static void Copy(string source, string destination, bool overwrite)
        {
            if (!overwrite && File.Exists(destination)) throw new IOException("Destination already exists: " + destination);
            WriteAllBytes(destination, ReadAllBytes(source));
        }

        internal static void Delete(string path)
        {
            // Delete the recovery copy first; interruption cannot resurrect an intentionally deleted save.
            if (File.Exists(path + PreviousSuffix)) File.Delete(path + PreviousSuffix);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
#endif
