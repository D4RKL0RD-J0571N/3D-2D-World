using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    /// <summary>
    /// Moves existing data from the previous persistent path to the new one.
    /// </summary>
    public static class PersistentPathMigration
    {
        const string MarkerFile = ".migrated";
        internal static Action<string> FaultInjection;
        internal static Func<Task> FlushOverride;
        static readonly AsyncOperationQueue operations = new();

        public static void TryMigrate(string oldPath, string newPath) => Observe(TryMigrateAsync(oldPath, newPath));

        static async void Observe(Task<bool> operation) { await operation; }

        /// <summary>Verifies copied data and acknowledges browser persistence before removing originals.</summary>
        public static async Task<bool> TryMigrateAsync(string oldPath, string newPath)
        {
            using var ownership = await operations.EnterAsync();
            try
            {
                if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                    return false;
                oldPath = Normalize(oldPath);
                newPath = Normalize(newPath);
                if (string.Equals(oldPath, newPath, PathComparison))
                    return true;
                if (!Directory.Exists(oldPath))
                    return true;
                string marker = Path.Combine(newPath, MarkerFile);
                if (File.Exists(marker))
                    return true;

                ValidateDirectoryPath(oldPath);
                ValidateDirectoryPath(newPath);
                var files = new List<(string source, string destination)>();
                var directories = new List<string>();
                Collect(oldPath, oldPath, newPath, files, directories);

                foreach (string directory in directories)
                {
                    string target = Destination(directory, oldPath, newPath);
                    if (File.Exists(target)) throw new IOException("Migration directory collides with a file: " + target);
                    ValidateDirectoryPath(target);
                }

                // Validate the entire merge before adding any destination files. A previous
                // interrupted copy is resumable, but a different save must never be replaced.
                foreach (var file in files)
                {
                    ValidateDestination(file.destination);
                    if (File.Exists(file.destination) && !FilesEqual(file.source, file.destination))
                        throw new IOException("Migration destination already contains different data: " + file.destination);
                }

                foreach (var file in files)
                {
                    if (File.Exists(file.destination)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(file.destination)!);
                    CopyNewFile(file.source, file.destination);
                    FaultInjection?.Invoke("copied");
                }
                foreach (string directory in directories)
                    Directory.CreateDirectory(Destination(directory, oldPath, newPath));
                foreach (var file in files)
                    if (!FilesEqual(file.source, file.destination))
                        throw new IOException("Migration verification failed: " + file.destination);

                Directory.CreateDirectory(newPath);
                // Original files remain available until every destination has been checked.
                // Never recursively delete the old root: the usual custom root is inside it.
                await FlushAsync();
                File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
                FaultInjection?.Invoke("published");
                var destinations = new HashSet<string>(PathComparer);
                foreach (var file in files) destinations.Add(file.destination);
                foreach (var file in files)
                {
                    if (destinations.Contains(file.source)) continue;
                    if (FilesEqual(file.source, file.destination)) File.Delete(file.source);
                }
                for (int i = directories.Count - 1; i >= 0; i--)
                {
                    string directory = directories[i];
                    if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
                        Directory.Delete(directory);
                }
                await FlushAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"PersistentPathMigration failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogLevel.Warning);
                return false;
            }
        }

        static Task FlushAsync()
        {
            if (FlushOverride != null) return FlushOverride();
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebGLFileSystem.FlushAsync();
#else
            return Task.CompletedTask;
#endif
        }

        // Preserve the historical case-insensitive self-migration guard. In particular,
        // case aliases on macOS must never be treated as independent copies to delete.
        static StringComparison PathComparison => StringComparison.OrdinalIgnoreCase;
        static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;

        static string Normalize(string path)
        {
            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full);
            return full.Length == root.Length ? full : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        static void RejectLink(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path)) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Migration cannot follow a symbolic link: " + path);
        }

        static void Collect(string directory, string sourceRoot, string destinationRoot,
            List<(string source, string destination)> files, List<string> directories)
        {
            // Exclude the destination and its contents when moving into a child directory.
            if (string.Equals(directory, destinationRoot, PathComparison)) return;
            RejectLink(directory);
            directories.Add(directory);
            foreach (string file in Directory.GetFiles(directory))
            {
                RejectLink(file);
                if (string.Equals(file, Path.Combine(sourceRoot, MarkerFile), PathComparison)) continue;
                files.Add((file, Destination(file, sourceRoot, destinationRoot)));
            }
            foreach (string child in Directory.GetDirectories(directory))
                Collect(child, sourceRoot, destinationRoot, files, directories);
        }

        static string Destination(string source, string sourceRoot, string destinationRoot)
            => Path.Combine(destinationRoot, source.Substring(sourceRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        static void ValidateDestination(string path)
        {
            if (Directory.Exists(path)) throw new IOException("Migration file collides with a directory: " + path);
            RejectLink(path);
            ValidateDirectoryPath(Path.GetDirectoryName(path));
        }

        static void ValidateDirectoryPath(string directory)
        {
            while (!string.IsNullOrEmpty(directory))
            {
                if (File.Exists(directory)) throw new IOException("Migration directory collides with a file: " + directory);
                RejectLink(directory);
                directory = Path.GetDirectoryName(directory);
            }
        }

        static void CopyNewFile(string source, string destination)
        {
            string temporary = destination + ".migrating-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var input = File.OpenRead(source))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
                File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        static bool FilesEqual(string left, string right)
        {
            if (!File.Exists(left) || !File.Exists(right)) return false;
            using var first = File.OpenRead(left);
            using var second = File.OpenRead(right);
            if (first.Length != second.Length) return false;
            var firstBuffer = new byte[8192];
            var secondBuffer = new byte[8192];
            int count;
            while ((count = first.Read(firstBuffer, 0, firstBuffer.Length)) != 0)
            {
                int read = 0;
                while (read < count)
                {
                    int next = second.Read(secondBuffer, read, count - read);
                    if (next == 0) return false;
                    read += next;
                }
                for (int i = 0; i < count; i++)
                    if (firstBuffer[i] != secondBuffer[i]) return false;
            }
            return true;
        }
    }
}
