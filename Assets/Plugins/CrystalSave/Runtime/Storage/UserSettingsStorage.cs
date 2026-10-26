#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    // Settings use their existing JSON schema and an independent single-file lifetime.
    // Serializing all settings I/O also covers replacement UserSettingsManager instances.
    internal static class UserSettingsStorage
    {
        static readonly AsyncOperationQueue operations = new();
        internal static Func<Task> FlushOverride;

        internal static string ResolvePath(SaveManager manager)
        {
            string root = manager != null ? manager.RootPath : null;
            if (string.IsNullOrEmpty(root))
            {
                // A settings component can Awake before SaveManager. Resolve the same
                // configured provider instead of reading an unrelated default folder.
                var settings = manager != null ? manager.SaveSettings : null;
                settings ??= AssetProvider.Load<SaveSettings>("SaveSettings");
                root = settings?.CreatePathProvider().GetRootPath() ?? Application.persistentDataPath;
            }
            return Path.Combine(root, "UserSettings.json");
        }

        internal static async Task<UserSettingsData> ReadAsync(string path)
        {
            using (await operations.EnterAsync())
            {
                string previous = path + AtomicSaveFile.PreviousSuffix;
                if (!File.Exists(path) && !File.Exists(previous)) return new UserSettingsData();
                try
                {
                    if (File.Exists(path)) return Decode(File.ReadAllText(path));
                }
                catch (Exception error) when (error is IOException || error is ArgumentException || error is FormatException)
                {
                    if (!File.Exists(previous)) throw;
                    Logger.Log("UserSettingsManager: Current settings are unreadable; reading the previous generation.", LogCategory.UserSettingsManager, LogLevel.Warning);
                }
                return Decode(File.ReadAllText(previous));
            }
        }

        internal static async Task WriteAsync(string path, string json)
        {
            // Validate before acquiring storage ownership or changing any existing file.
            Decode(json);
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            using (await operations.EnterAsync())
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                if (FlushOverride == null)
                {
                    AtomicSaveFile.WriteAllBytes(path, bytes);
                    return;
                }
#endif
                string previousPath = path + AtomicSaveFile.PreviousSuffix;
                byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
                byte[] previous = File.Exists(previousPath) ? File.ReadAllBytes(previousPath) : null;
                try
                {
                    AtomicSaveFile.WriteAllBytes(path, bytes);
                    await FlushAsync();
                }
                catch (Exception error)
                {
                    try
                    {
                        Restore(path, original);
                        Restore(previousPath, previous);
                        await FlushAsync();
                    }
                    catch (Exception rollbackError)
                    {
                        throw new AggregateException("User settings persistence and rollback failed.", error, rollbackError);
                    }
                    throw new IOException("User settings persistence failed; previous local settings were restored.", error);
                }
            }
        }

        static UserSettingsData Decode(string json)
        {
            // JsonUtility accepts some incomplete inputs and null. Check the document
            // shape first while retaining Unity's existing field/default interpretation.
            // The check is dependency-free so settings never require the optional
            // Newtonsoft package; malformed input throws FormatException and callers
            // fall back to the previous generation.
            ValidateJsonObject(json);
            return JsonUtility.FromJson<UserSettingsData>(json) ?? throw new InvalidDataException("User settings JSON contains no settings object.");
        }

        // Strict RFC 8259 object document: one JSON object with valid strings, numbers and
        // escapes, no comments and no trailing content. Mirrors what JObject.Parse accepted
        // as an object without depending on Newtonsoft JSON.
        static void ValidateJsonObject(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new FormatException("User settings JSON is empty.");
            int index = 0;
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '{') throw new FormatException("User settings JSON is not an object.");
            ValidateObject(json, ref index, 0);
            SkipWhitespace(json, ref index);
            if (index != json.Length) throw new FormatException("User settings JSON has trailing content.");
        }

        const int MaxJsonDepth = 64;

        static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && (json[index] == ' ' || json[index] == '\t' || json[index] == '\n' || json[index] == '\r')) index++;
        }

        static void ValidateObject(string json, ref int index, int depth)
        {
            if (depth >= MaxJsonDepth) throw new FormatException("User settings JSON is nested too deeply.");
            index++;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == '}') { index++; return; }
            while (true)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != '"') throw new FormatException("User settings JSON has an invalid member name.");
                ValidateString(json, ref index);
                SkipWhitespace(json, ref index);
                if (index >= json.Length || json[index] != ':') throw new FormatException("User settings JSON is missing ':'.");
                index++;
                SkipWhitespace(json, ref index);
                ValidateValue(json, ref index, depth + 1);
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("User settings JSON is truncated.");
                if (json[index] == '}') { index++; return; }
                if (json[index] != ',') throw new FormatException("User settings JSON is malformed.");
                index++;
            }
        }

        static void ValidateValue(string json, ref int index, int depth)
        {
            if (index >= json.Length) throw new FormatException("User settings JSON is truncated.");
            switch (json[index])
            {
                case '{': ValidateObject(json, ref index, depth); return;
                case '[': ValidateArray(json, ref index, depth); return;
                case '"': ValidateString(json, ref index); return;
                case 't': ValidateLiteral(json, ref index, "true"); return;
                case 'f': ValidateLiteral(json, ref index, "false"); return;
                case 'n': ValidateLiteral(json, ref index, "null"); return;
                default: ValidateNumber(json, ref index); return;
            }
        }

        static void ValidateArray(string json, ref int index, int depth)
        {
            if (depth >= MaxJsonDepth) throw new FormatException("User settings JSON is nested too deeply.");
            index++;
            SkipWhitespace(json, ref index);
            if (index < json.Length && json[index] == ']') { index++; return; }
            while (true)
            {
                SkipWhitespace(json, ref index);
                ValidateValue(json, ref index, depth + 1);
                SkipWhitespace(json, ref index);
                if (index >= json.Length) throw new FormatException("User settings JSON is truncated.");
                if (json[index] == ']') { index++; return; }
                if (json[index] != ',') throw new FormatException("User settings JSON is malformed.");
                index++;
            }
        }

        static void ValidateString(string json, ref int index)
        {
            index++;
            while (index < json.Length)
            {
                char character = json[index++];
                if (character == '"') return;
                if (character < 0x20) throw new FormatException("User settings JSON has an unescaped control character.");
                if (character != '\\') continue;
                if (index >= json.Length) break;
                char escape = json[index++];
                switch (escape)
                {
                    case '"': case '\\': case '/': case 'b': case 'f': case 'n': case 'r': case 't': continue;
                    case 'u':
                        for (int digit = 0; digit < 4; digit++)
                        {
                            if (index >= json.Length || !IsHexDigit(json[index])) throw new FormatException("User settings JSON has an invalid escape.");
                            index++;
                        }
                        continue;
                    default: throw new FormatException("User settings JSON has an invalid escape.");
                }
            }
            throw new FormatException("User settings JSON has an unterminated string.");
        }

        static void ValidateLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length || string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
                throw new FormatException("User settings JSON has an invalid literal.");
            index += literal.Length;
        }

        static void ValidateNumber(string json, ref int index)
        {
            int start = index;
            if (index < json.Length && json[index] == '-') index++;
            if (index >= json.Length || json[index] < '0' || json[index] > '9') throw new FormatException("User settings JSON has an invalid number.");
            if (json[index] == '0') index++;
            else while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
            if (index < json.Length && json[index] == '.')
            {
                index++;
                if (index >= json.Length || json[index] < '0' || json[index] > '9') throw new FormatException("User settings JSON has an invalid number.");
                while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
            }
            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-')) index++;
                if (index >= json.Length || json[index] < '0' || json[index] > '9') throw new FormatException("User settings JSON has an invalid number.");
                while (index < json.Length && json[index] >= '0' && json[index] <= '9') index++;
            }
            if (index == start) throw new FormatException("User settings JSON has an invalid number.");
        }

        static bool IsHexDigit(char character) => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f') || (character >= 'A' && character <= 'F');

        static void Restore(string path, byte[] bytes)
        {
            if (bytes == null) { if (File.Exists(path)) File.Delete(path); }
            else File.WriteAllBytes(path, bytes);
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
    }
}
#endif
