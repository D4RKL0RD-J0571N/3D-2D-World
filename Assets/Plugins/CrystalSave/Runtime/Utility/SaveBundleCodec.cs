#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using MemoryPack;
#if CRYSTALSAVE_NEWTONSOFT
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif
namespace Arawn.CrystalSave.Runtime
{
    internal sealed class DecodedSaveBundle
    {
        internal byte[] SaveBytes, Screenshot;
        internal string ScreenshotExtension;
        internal SaveBundleManifest Manifest;
    }
    internal static class SaveBundleCodec
    {
        static readonly byte[] Magic = Encoding.ASCII.GetBytes("CSAVBX");
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static byte[] Encode(SaveBundleManifest manifest, byte[] save, byte[] screenshot, string extension, SaveBundleOptions options)
        {
            var limits = SaveBundleOptions.Resolve(options);
            byte[] metadata = MemoryPackSerializer.Serialize(manifest);
            CheckLength(metadata.Length, limits.MaxManifestBytes);
            var entries = new Dictionary<string, byte[]> { ["manifest.bin"] = metadata, ["savedata.bin"] = save };
            if (screenshot != null && screenshot.Length > 0)
            {
                extension = NormalizeExtension(extension);
                entries.Add("screenshot" + extension, screenshot);
            }
            CheckLength(entries.Count, limits.MaxEntries);
            using var output = new MemoryStream();
            using (var bounded = new BudgetStream(output, limits.MaxCompressedBytes, true))
            using (var gz = new GZipStream(bounded, CompressionLevel.Optimal, true))
            using (var raw = new BudgetStream(gz, limits.MaxExpandedBytes, true))
            using (var writer = new BinaryWriter(raw, Utf8, true))
            {
                writer.Write(Magic); writer.Write((byte)2); writer.Write((ushort)entries.Count);
                foreach (var entry in entries)
                {
                    byte[] name = Utf8.GetBytes(entry.Key);
                    CheckLength(name.Length, limits.MaxNameBytes);
                    if (entry.Value == null) throw new InvalidDataException("Bundle entry is null.");
                    CheckLength(entry.Value.Length, limits.MaxEntryBytes);
                    writer.Write((ushort)name.Length); writer.Write(name); writer.Write(entry.Value.Length); writer.Write(entry.Value);
                }
            }
            return output.ToArray();
        }
        internal static DecodedSaveBundle Decode(byte[] bytes, SaveBundleOptions options)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            var limits = SaveBundleOptions.Resolve(options); CheckLength(bytes.Length, limits.MaxCompressedBytes);
            using var input = new MemoryStream(bytes, false);
            return Decode(input, limits);
        }
        internal static DecodedSaveBundle Decode(Stream input, SaveBundleOptions options)
        {
            var limits = SaveBundleOptions.Resolve(options);
            if (!input.CanSeek)
            {
                using var copy = new MemoryStream();
                using (var bounded = new BudgetStream(input, limits.MaxCompressedBytes, true)) bounded.CopyTo(copy);
                copy.Position = 0;
                return Decode(copy, limits);
            }
            CheckLength(input.Length - input.Position, limits.MaxCompressedBytes);
            if (input.Length - input.Position < 18) throw new InvalidDataException("Truncated gzip container.");
            using var compressed = new BudgetStream(input, limits.MaxCompressedBytes, true, exactEnd: true);
            using var gz = new GZipStream(compressed, CompressionMode.Decompress, true);
            using var expanded = new BudgetStream(gz, limits.MaxExpandedBytes, true);
            using var reader = new BinaryReader(expanded, Utf8, true);
            if (!ReadExactly(reader, 6).SequenceEqual(Magic)) throw new InvalidDataException("Invalid bundle magic.");
            int version = reader.ReadByte();
            if (version != 1 && version != 2) throw new InvalidDataException("Unsupported bundle version.");
            int count = reader.ReadUInt16();
            if (count < 1) throw new InvalidDataException("Empty bundle.");
            CheckLength(count, limits.MaxEntries);
            var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                int nameLength = reader.ReadUInt16(); CheckLength(nameLength, limits.MaxNameBytes);
                string name = Utf8.GetString(ReadExactly(reader, nameLength));
                if (name.Length == 0 || name.Contains("/") || name.Contains("\\") || name.Contains("..") || name.Any(char.IsControl))
                    throw new InvalidDataException("Invalid bundle entry name.");
                int length = reader.ReadInt32();
                CheckLength(length, limits.MaxEntryBytes);
                CheckLength(length, limits.MaxExpandedBytes - expanded.Count);
                if (name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || name.Equals("manifest.bin", StringComparison.OrdinalIgnoreCase))
                    CheckLength(length, limits.MaxManifestBytes);
                if (entries.ContainsKey(name)) throw new InvalidDataException("Duplicate bundle entry.");
                entries.Add(name, ReadExactly(reader, length));
            }
            if (reader.Read() != -1) throw new InvalidDataException("Trailing bundle data.");
            if (input.Position != input.Length) throw new InvalidDataException("Trailing compressed container data.");
            // Some runtimes accept a gzip stream without its complete trailer. Validate it explicitly.
            input.Position = input.Length - 8;
            using (var trailer = new BinaryReader(input, Utf8, true))
                if (trailer.ReadUInt32() != expanded.Crc32 || trailer.ReadUInt32() != (uint)expanded.Count)
                    throw new InvalidDataException("Invalid or truncated gzip trailer.");
            if (!entries.TryGetValue("savedata.bin", out var save) || save.Length == 0) throw new InvalidDataException("Missing save data.");
            bool json = entries.TryGetValue("manifest.json", out var jsonBytes);
            bool binary = entries.TryGetValue("manifest.bin", out var binaryBytes);
            if (json && binary || version == 2 && (!binary || json) || version == 1 && binary)
                throw new InvalidDataException("Conflicting or missing bundle manifest.");
            SaveBundleManifest manifest = version == 2 ? ReadBinaryManifest(binaryBytes) :
                json ? ReadLegacyManifest(Utf8.GetString(jsonBytes), limits) : new SaveBundleManifest();
            if (manifest == null) throw new InvalidDataException("Invalid bundle manifest.");
            var result = new DecodedSaveBundle { SaveBytes = save, Manifest = manifest };
            foreach (var entry in entries)
            {
                if (!entry.Key.StartsWith("screenshot", StringComparison.OrdinalIgnoreCase)) continue;
                if (result.Screenshot != null) throw new InvalidDataException("Multiple screenshots.");
                result.ScreenshotExtension = NormalizeExtension(Path.GetExtension(entry.Key));
                if (!entry.Key.Equals("screenshot" + result.ScreenshotExtension, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid screenshot name.");
                result.Screenshot = entry.Value;
            }
            return result;
        }
        static SaveBundleManifest ReadBinaryManifest(byte[] bytes)
        {
            SaveBundleManifest manifest = null;
            if (MemoryPackSerializer.Deserialize(bytes.AsSpan(), ref manifest) != bytes.Length)
                throw new InvalidDataException("Trailing binary manifest data.");
            return manifest;
        }
        internal static SaveBundleManifest ReadLegacyManifest(string json, SaveBundleOptions options)
        {
#if CRYSTALSAVE_NEWTONSOFT
            var limits = SaveBundleOptions.Resolve(options);
            CheckLength(Utf8.GetByteCount(json), limits.MaxManifestBytes);
            using var text = new StringReader(json);
            using var reader = new JsonTextReader(text) { MaxDepth = limits.MaxJsonDepth, DateParseHandling = DateParseHandling.None };
            var obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (obj["version"] != null && (obj["version"].Type != JTokenType.Integer || (int)obj["version"] != 1))
                throw new InvalidDataException("Unsupported legacy manifest version.");
            if (reader.Read()) throw new InvalidDataException("Trailing manifest content.");
            string StringValue(string name)
            {
                var value = obj[name];
                if (value == null || value.Type == JTokenType.Null) return null;
                if (value.Type != JTokenType.String) throw new InvalidDataException("Invalid manifest string: " + name);
                return (string)value;
            }
            var manifest = new SaveBundleManifest { SlotName = StringValue("slotName"), LastActiveScene = StringValue("lastActiveScene"), ScreenshotExtension = StringValue("screenshotExt") };
            var timestamp = StringValue("lastSaved");
            if (timestamp != null)
            {
                if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)) throw new InvalidDataException("Invalid save timestamp.");
                manifest.LastSaved = date;
            }
            manifest.CustomMetadata = new Dictionary<string, string>();
            var custom = obj["customMetadata"];
            if (custom != null && custom.Type != JTokenType.Null)
            {
                if (!(custom is JObject metadata)) throw new InvalidDataException("Invalid custom metadata.");
                foreach (var property in metadata.Properties())
                {
                    if (property.Value.Type != JTokenType.String && property.Value.Type != JTokenType.Null) throw new InvalidDataException("Metadata values must be strings.");
                    manifest.CustomMetadata.Add(property.Name, (string)property.Value);
                }
            }
            return manifest;
#else
            throw new NotSupportedException("Reading legacy JSON bundles requires Unity's Newtonsoft JSON package. Run Crystal Save's dependency installer.");
#endif
        }
        internal static string NormalizeExtension(string extension)
        {
            extension = (extension ?? ".png").ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") throw new InvalidDataException("Unsupported screenshot extension.");
            return extension;
        }
        internal static void CheckLength(long length, long maximum)
        { if (length < 0 || length > maximum) throw new InvalidDataException("Save bundle exceeds its configured size limit."); }
        static byte[] ReadExactly(BinaryReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException("Truncated bundle entry.");
            return bytes;
        }
        // Applies an allocation-independent byte budget to compression and decompression streams.
        internal sealed class BudgetStream : Stream
        {
            readonly Stream inner; readonly long maximum; readonly bool leaveOpen, exactEnd;
            internal long Count { get; private set; }
            uint crc = uint.MaxValue;
            internal uint Crc32 => crc ^ uint.MaxValue;
            static readonly uint[] CrcTable = BuildCrcTable();
            static uint[] BuildCrcTable()
            {
                var table = new uint[256];
                for (uint i = 0; i < 256; i++) { uint value = i; for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? (value >> 1) ^ 0xedb88320u : value >> 1; table[i] = value; }
                return table;
            }
            internal BudgetStream(Stream inner, long maximum, bool leaveOpen, bool exactEnd = false)
            { this.inner = inner; this.maximum = maximum; this.leaveOpen = leaveOpen; this.exactEnd = exactEnd; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                // Avoid gzip read-ahead hiding junk after its trailer. Only the final 32 bytes
                // need single-byte reads; large payloads retain normal buffered throughput.
                if (exactEnd && inner.CanSeek)
                {
                    long remaining = inner.Length - inner.Position;
                    count = (int)Math.Min(count, remaining > 32 ? remaining - 32 : 1);
                }
                int read = inner.Read(buffer, offset, (int)Math.Min(count, Math.Max(1, maximum - Count + 1)));
                Count += read; CheckLength(Count, maximum);
                for (int i = offset; i < offset + read; i++) crc = CrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8);
                return read;
            }
            public override void Write(byte[] buffer, int offset, int count) { CheckLength(Count + count, maximum); inner.Write(buffer, offset, count); Count += count; }
            public override void Flush() => inner.Flush();
            protected override void Dispose(bool disposing) { if (disposing && !leaveOpen) inner.Dispose(); base.Dispose(disposing); }
            public override bool CanRead => inner.CanRead; public override bool CanWrite => inner.CanWrite; public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => Count; set => throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
#endif
