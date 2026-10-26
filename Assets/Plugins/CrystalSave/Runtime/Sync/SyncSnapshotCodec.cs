#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Arawn.CrystalSave.Runtime
{
    public enum SyncSnapshotCompression { GZip = 0, Lz4 = 1 }

    /// <summary>Network-only compression. The transfer carries codec, sizes and SHA-256 separately.</summary>
    public static class SyncSnapshotCodec
    {
        public const int MaximumSnapshotBytes = 64 * 1024 * 1024;

        public static byte[] Compress(byte[] snapshot, SyncSnapshotCompression compression)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Length > MaximumSnapshotBytes) throw new InvalidDataException("Snapshot exceeds the network limit.");
            if (compression == SyncSnapshotCompression.Lz4) return CompressLz4(snapshot);
            if (compression != SyncSnapshotCompression.GZip) throw new InvalidDataException("Unknown snapshot codec.");
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true))
                gzip.Write(snapshot, 0, snapshot.Length);
            return output.ToArray();
        }

        public static byte[] Decompress(byte[] compressed, SyncSnapshotCompression compression, int expectedBytes,
            int maximumBytes = MaximumSnapshotBytes)
        {
            if (compressed == null) throw new ArgumentNullException(nameof(compressed));
            if (expectedBytes < 0 || maximumBytes < 0 || expectedBytes > Math.Min(maximumBytes, MaximumSnapshotBytes) ||
                compressed.Length > MaximumSnapshotBytes + MaximumSnapshotBytes / 255 + 1024)
                throw new InvalidDataException("Invalid snapshot size.");
            if (compression == SyncSnapshotCompression.Lz4) return DecompressLz4(compressed, expectedBytes);
            if (compression != SyncSnapshotCompression.GZip) throw new InvalidDataException("Unknown snapshot codec.");
            using var input = new MemoryStream(compressed, false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            var output = new byte[expectedBytes];
            int position = 0;
            while (position < output.Length)
            {
                int read = gzip.Read(output, position, output.Length - position);
                if (read == 0) throw new InvalidDataException("Truncated snapshot.");
                position += read;
            }
            if (gzip.ReadByte() != -1) throw new InvalidDataException("Snapshot exceeds its declared size.");
            return output;
        }

        // Managed LZ4 block format: https://github.com/lz4/lz4/blob/dev/doc/lz4_Block_format.md
        // No native plug-in, optional networking SDK or unsafe/platform-specific code is required.
        static byte[] CompressLz4(byte[] input)
        {
            var output = new List<byte>(input.Length + input.Length / 255 + 16);
            var positions = new int[16384];
            for (int n = 0; n < positions.Length; n++) positions[n] = -1;
            int cursor = 0, anchor = 0;
            while (cursor <= input.Length - 12)
            {
                uint value = Read32(input, cursor);
                int hash = (int)(unchecked(value * 2654435761u) >> 18);
                int match = positions[hash];
                positions[hash] = cursor;
                if (match < 0 || cursor - match > 65535 || Read32(input, match) != value)
                { cursor++; continue; }
                int length = 4;
                while (cursor + length < input.Length - 5 && input[match + length] == input[cursor + length]) length++;
                int literals = cursor - anchor;
                output.Add((byte)((Math.Min(literals, 15) << 4) | Math.Min(length - 4, 15)));
                if (literals >= 15) WriteLength(output, literals - 15);
                for (int i = anchor; i < cursor; i++) output.Add(input[i]);
                int offset = cursor - match;
                output.Add((byte)offset); output.Add((byte)(offset >> 8));
                if (length >= 19) WriteLength(output, length - 19);
                cursor += length; anchor = cursor;
                if (cursor >= 2 && cursor <= input.Length - 12)
                    positions[(int)(unchecked(Read32(input, cursor - 2) * 2654435761u) >> 18)] = cursor - 2;
            }
            int tail = input.Length - anchor;
            output.Add((byte)(Math.Min(tail, 15) << 4));
            if (tail >= 15) WriteLength(output, tail - 15);
            for (int i = anchor; i < input.Length; i++) output.Add(input[i]);
            return output.ToArray();
        }

        static uint Read32(byte[] data, int i) => (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24);
        static void WriteLength(List<byte> output, int length)
        {
            while (length >= 255) { output.Add(255); length -= 255; }
            output.Add((byte)length);
        }
        static int ReadLength(byte[] input, ref int cursor, int length, int limit)
        {
            if (length != 15) return length;
            byte next;
            do
            {
                if (cursor >= input.Length) throw new InvalidDataException("Truncated LZ4 length.");
                next = input[cursor++];
                if (length > limit - next) throw new InvalidDataException("LZ4 length exceeds snapshot size.");
                length += next;
            } while (next == 255);
            return length;
        }
        static byte[] DecompressLz4(byte[] input, int expectedBytes)
        {
            var output = new byte[expectedBytes];
            int cursor = 0, position = 0;
            while (cursor < input.Length)
            {
                byte token = input[cursor++];
                int literals = ReadLength(input, ref cursor, token >> 4, output.Length - position);
                if (literals > input.Length - cursor || literals > output.Length - position)
                    throw new InvalidDataException("Invalid LZ4 literal length.");
                Buffer.BlockCopy(input, cursor, output, position, literals);
                cursor += literals; position += literals;
                if (cursor == input.Length)
                {
                    if (position != output.Length) throw new InvalidDataException("Truncated LZ4 snapshot.");
                    return output;
                }
                if (input.Length - cursor < 2) throw new InvalidDataException("Truncated LZ4 offset.");
                int offset = input[cursor] | input[cursor + 1] << 8; cursor += 2;
                if (offset == 0 || offset > position) throw new InvalidDataException("Invalid LZ4 offset.");
                int length = ReadLength(input, ref cursor, token & 15, output.Length - position - 4) + 4;
                if (length > output.Length - position) throw new InvalidDataException("Invalid LZ4 match length.");
                // Expand overlapping matches from the bytes already produced, doubling each copy.
                // This also makes long repeated runs cheap without unsafe code or native plug-ins.
                int copied = Math.Min(offset, length);
                Buffer.BlockCopy(output, position - offset, output, position, copied);
                while (copied < length)
                {
                    int next = Math.Min(copied, length - copied);
                    Buffer.BlockCopy(output, position, output, position + copied, next);
                    copied += next;
                }
                position += length;
            }
            throw new InvalidDataException("Missing final LZ4 literal sequence.");
        }
    }
}
#endif
