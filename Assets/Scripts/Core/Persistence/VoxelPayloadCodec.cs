namespace Core.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using Core.Content;
    using Core.Ids;
    using Domain.Grid;
    using UnityEngine;

    /// <summary>
    /// Pluggable compression codec identifier stored in the 24-byte <c>.vxlbin</c> binary header (<c>ADR-009</c>).
    /// </summary>
    public enum VoxelCompressionCodec : ushort
    {
        /// <summary>Uncompressed little-endian <c>ushort[]</c> stream.</summary>
        Raw = 0,

        /// <summary>16-bit Run-Length Encoding (<c>[ushort runLength, ushort blockId]</c> pairs).</summary>
        RLE16 = 1,

        /// <summary>16-bit Run-Length Encoding compressed with <see cref="DeflateStream"/>.</summary>
        RLE16_Deflate = 2
    }

    /// <summary>
    /// Exception thrown when a <c>.vxlbin</c> binary voxel payload fails header validation or IEEE 802.3 CRC-32 verification.
    /// </summary>
    public sealed class SavePayloadCorruptedException : IOException
    {
        public uint ExpectedCrc32 { get; }
        public uint ActualCrc32 { get; }

        public SavePayloadCorruptedException(string message, uint expectedCrc = 0u, uint actualCrc = 0u)
            : base(message)
        {
            ExpectedCrc32 = expectedCrc;
            ActualCrc32 = actualCrc;
        }
    }

    /// <summary>
    /// Represents a single 3D room or chunk voxel buffer inside a <c>.vxlbin</c> binary sidecar file.
    /// </summary>
    [Serializable]
    public sealed class VoxelChunkPayload
    {
        public Vector3 WorldPosition;
        public Vector3Int Size;
        public ushort[] Voxels;

        public VoxelChunkPayload() { }

        public VoxelChunkPayload(Vector3 worldPosition, Vector3Int size, ushort[] voxels)
        {
            WorldPosition = worldPosition;
            Size = size;
            Voxels = voxels ?? Array.Empty<ushort>();
        }
    }

    /// <summary>
    /// Binary serializer/deserializer for <c>.vxlbin</c> voxel sidecar files per <c>ADR-009</c>.
    /// Header layout (24 bytes, little-endian):
    /// - [0..3]   uint   Magic = 0x4C565846 ("FXVL")
    /// - [4..5]   ushort FormatVersion = 1
    /// - [6..7]   ushort CodecId (0 = Raw, 1 = RLE16, 2 = RLE16_Deflate)
    /// - [8..11]  uint   ChunkCount
    /// - [12..15] uint   UncompressedByteLength
    /// - [16..19] uint   PayloadCrc32 (IEEE 802.3 CRC-32 of bytes [24..end])
    /// - [20..23] uint   Reserved = 0
    /// </summary>
    public static class VoxelPayloadCodec
    {
        #region Constants & CRC-32 Table

        public const uint MagicBytes = 0x4C565846u; // ASCII "FXVL" in little-endian
        public const ushort CurrentFormatVersion = 1;
        public const int HeaderByteSize = 24;

        private static readonly uint[] Crc32Table = BuildCrc32Table();

        #endregion

        #region Public Encode / Decode API

        /// <summary>
        /// Encodes a collection of <see cref="VoxelChunkPayload"/>s into a self-contained <c>.vxlbin</c> byte array
        /// with a 24-byte header and IEEE 802.3 CRC-32 checksum.
        /// </summary>
        public static byte[] Encode(
            IReadOnlyList<VoxelChunkPayload> chunks,
            VoxelCompressionCodec codec = VoxelCompressionCodec.RLE16_Deflate)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));

            uint uncompressedVoxelBytes = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                if (chunks[i]?.Voxels != null)
                {
                    uncompressedVoxelBytes += (uint)(chunks[i].Voxels.Length * sizeof(ushort));
                }
            }

            byte[] uncompressedStream = SerializeChunksWithCodec(
                chunks,
                codec == VoxelCompressionCodec.Raw ? VoxelCompressionCodec.Raw : VoxelCompressionCodec.RLE16);

            byte[] finalBody;
            if (codec == VoxelCompressionCodec.RLE16_Deflate)
            {
                using var compressedMs = new MemoryStream(uncompressedStream.Length / 2 + 64);
                using (var deflate = new DeflateStream(compressedMs, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                {
                    deflate.Write(uncompressedStream, 0, uncompressedStream.Length);
                }
                finalBody = compressedMs.ToArray();
            }
            else
            {
                finalBody = uncompressedStream;
            }

            uint crc32 = ComputeCrc32(finalBody, 0, finalBody.Length);

            var output = new byte[HeaderByteSize + finalBody.Length];
            using (var ms = new MemoryStream(output))
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(MagicBytes);
                writer.Write(CurrentFormatVersion);
                writer.Write((ushort)codec);
                writer.Write((uint)chunks.Count);
                writer.Write(uncompressedVoxelBytes);
                writer.Write(crc32);
                writer.Write(0u); // Reserved
                writer.Write(finalBody, 0, finalBody.Length);
            }

            return output;
        }

        /// <summary>
        /// Decodes a <c>.vxlbin</c> byte array after verifying the 24-byte header and CRC-32 checksum.
        /// Throws <see cref="SavePayloadCorruptedException"/> if the header or checksum is invalid.
        /// </summary>
        public static List<VoxelChunkPayload> Decode(byte[] binaryData)
        {
            return Decode(binaryData, out _);
        }

        /// <summary>
        /// Decodes a <c>.vxlbin</c> byte array and outputs the <see cref="VoxelCompressionCodec"/> recorded in the header.
        /// </summary>
        public static List<VoxelChunkPayload> Decode(byte[] binaryData, out VoxelCompressionCodec usedCodec)
        {
            if (binaryData == null || binaryData.Length < HeaderByteSize)
            {
                throw new SavePayloadCorruptedException("Voxel payload is null or shorter than the 24-byte '.vxlbin' header.");
            }

            uint magic;
            ushort version;
            ushort codecRaw;
            uint chunkCount;
            uint uncompressedByteLength;
            uint expectedCrc32;

            using (var headerMs = new MemoryStream(binaryData, 0, HeaderByteSize, writable: false))
            using (var headerReader = new BinaryReader(headerMs))
            {
                magic = headerReader.ReadUInt32();
                version = headerReader.ReadUInt16();
                codecRaw = headerReader.ReadUInt16();
                chunkCount = headerReader.ReadUInt32();
                uncompressedByteLength = headerReader.ReadUInt32();
                expectedCrc32 = headerReader.ReadUInt32();
                _ = headerReader.ReadUInt32(); // Reserved
            }

            if (magic != MagicBytes)
            {
                throw new SavePayloadCorruptedException($"Invalid '.vxlbin' magic header: expected 0x{MagicBytes:X8} ('FXVL'), got 0x{magic:X8}.");
            }

            if (version != CurrentFormatVersion)
            {
                throw new SavePayloadCorruptedException($"Unsupported '.vxlbin' format version {version} (expected {CurrentFormatVersion}).");
            }

            int bodyLength = binaryData.Length - HeaderByteSize;
            uint actualCrc32 = ComputeCrc32(binaryData, HeaderByteSize, bodyLength);
            if (actualCrc32 != expectedCrc32)
            {
                throw new SavePayloadCorruptedException(
                    $"Voxel payload CRC-32 checksum mismatch (expected 0x{expectedCrc32:X8}, actual 0x{actualCrc32:X8}). File is corrupted.",
                    expectedCrc32,
                    actualCrc32);
            }

            usedCodec = (VoxelCompressionCodec)codecRaw;

            byte[] streamBytes;
            int streamOffset;
            int streamCount;

            if (usedCodec == VoxelCompressionCodec.RLE16_Deflate)
            {
                using var compressedInput = new MemoryStream(binaryData, HeaderByteSize, bodyLength, writable: false);
                using var deflate = new DeflateStream(compressedInput, CompressionMode.Decompress);
                using var decompressedOut = new MemoryStream((int)Math.Max(uncompressedByteLength / 4, 256));
                deflate.CopyTo(decompressedOut);
                streamBytes = decompressedOut.ToArray();
                streamOffset = 0;
                streamCount = streamBytes.Length;
            }
            else
            {
                streamBytes = binaryData;
                streamOffset = HeaderByteSize;
                streamCount = bodyLength;
            }

            bool isRle = usedCodec == VoxelCompressionCodec.RLE16 || usedCodec == VoxelCompressionCodec.RLE16_Deflate;
            return DeserializeChunksFromStream(streamBytes, streamOffset, streamCount, chunkCount, isRle);
        }

        #endregion

        #region Voxel Palette Mapping Helpers

        /// <summary>
        /// Builds a mapping of runtime <c>ushort</c> block IDs to canonical <see cref="ResourceId"/> strings
        /// from <paramref name="contentService"/>.
        /// </summary>
        public static Dictionary<ushort, string> BuildPaletteMap(IContentService contentService)
        {
            var map = new Dictionary<ushort, string>();
            if (contentService == null) return map;

            var blockRegistry = contentService.GetRegistry<BlockDefinition>();
            foreach (BlockDefinition block in blockRegistry.GetAll())
            {
                if (block != null && block.RuntimeId > 0 && block.ContentId.IsValid)
                {
                    map[block.RuntimeId] = block.ContentId.Value;
                }
            }

            return map;
        }

        /// <summary>
        /// Remaps a loaded <c>ushort[]</c> voxel buffer if a block's runtime <c>ushort</c> ID changed between save and load.
        /// </summary>
        public static void RemapVoxelBufferIfNeeded(
            ushort[] voxels,
            IReadOnlyDictionary<ushort, string> savedPalette,
            IContentService contentService)
        {
            if (voxels == null || savedPalette == null || savedPalette.Count == 0 || contentService == null)
            {
                return;
            }

            var blockRegistry = contentService.GetRegistry<BlockDefinition>();
            var remapTable = new Dictionary<ushort, ushort>();
            bool requiresRemap = false;

            foreach (var kvp in savedPalette)
            {
                ushort oldRuntimeId = kvp.Key;
                if (ResourceId.TryParse(kvp.Value, out ResourceId blockResourceId) &&
                    blockRegistry.TryGet(blockResourceId, out BlockDefinition currentBlock))
                {
                    remapTable[oldRuntimeId] = currentBlock.RuntimeId;
                    if (currentBlock.RuntimeId != oldRuntimeId)
                    {
                        requiresRemap = true;
                    }
                }
            }

            if (!requiresRemap) return;

            for (int i = 0; i < voxels.Length; i++)
            {
                ushort v = voxels[i];
                if (v != 0 && remapTable.TryGetValue(v, out ushort mapped))
                {
                    voxels[i] = mapped;
                }
            }
        }

        #endregion

        #region Internal Stream Encoding / Decoding

        private static byte[] SerializeChunksWithCodec(IReadOnlyList<VoxelChunkPayload> chunks, VoxelCompressionCodec innerCodec)
        {
            using var ms = new MemoryStream(4096);
            using var writer = new BinaryWriter(ms);

            for (int c = 0; c < chunks.Count; c++)
            {
                VoxelChunkPayload chunk = chunks[c];
                Vector3 pos = chunk?.WorldPosition ?? Vector3.zero;
                Vector3Int size = chunk?.Size ?? Vector3Int.zero;
                ushort[] voxels = chunk?.Voxels ?? Array.Empty<ushort>();

                writer.Write(pos.x);
                writer.Write(pos.y);
                writer.Write(pos.z);
                writer.Write(size.x);
                writer.Write(size.y);
                writer.Write(size.z);
                writer.Write(voxels.Length);

                if (innerCodec == VoxelCompressionCodec.Raw)
                {
                    var rawBytes = new byte[voxels.Length * sizeof(ushort)];
                    Buffer.BlockCopy(voxels, 0, rawBytes, 0, rawBytes.Length);
                    writer.Write(rawBytes);
                }
                else
                {
                    WriteRle16(writer, voxels);
                }
            }

            return ms.ToArray();
        }

        private static void WriteRle16(BinaryWriter writer, ushort[] voxels)
        {
            if (voxels == null || voxels.Length == 0)
            {
                writer.Write(0); // 0 runs
                return;
            }

            // Two-pass or buffered run count write
            var runs = new List<(ushort count, ushort value)>(Math.Min(voxels.Length, 512));
            ushort currentVal = voxels[0];
            ushort runLength = 1;

            for (int i = 1; i < voxels.Length; i++)
            {
                ushort val = voxels[i];
                if (val == currentVal && runLength < ushort.MaxValue)
                {
                    runLength++;
                }
                else
                {
                    runs.Add((runLength, currentVal));
                    currentVal = val;
                    runLength = 1;
                }
            }

            runs.Add((runLength, currentVal));

            writer.Write(runs.Count);
            for (int i = 0; i < runs.Count; i++)
            {
                writer.Write(runs[i].count);
                writer.Write(runs[i].value);
            }
        }

        private static List<VoxelChunkPayload> DeserializeChunksFromStream(
            byte[] buffer,
            int offset,
            int count,
            uint chunkCount,
            bool isRle)
        {
            var result = new List<VoxelChunkPayload>((int)chunkCount);

            using var ms = new MemoryStream(buffer, offset, count, writable: false);
            using var reader = new BinaryReader(ms);

            for (uint c = 0; c < chunkCount; c++)
            {
                var pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                var size = new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
                int voxelLength = reader.ReadInt32();

                if (voxelLength < 0 || voxelLength > 16_777_216)
                {
                    throw new SavePayloadCorruptedException($"Invalid voxel array length {voxelLength} in chunk {c}.");
                }

                var voxels = new ushort[voxelLength];

                if (!isRle)
                {
                    int byteCount = voxelLength * sizeof(ushort);
                    byte[] rawBytes = reader.ReadBytes(byteCount);
                    if (rawBytes.Length != byteCount)
                    {
                        throw new SavePayloadCorruptedException($"Unexpected end of stream reading raw voxel chunk {c}.");
                    }
                    Buffer.BlockCopy(rawBytes, 0, voxels, 0, byteCount);
                }
                else
                {
                    int runCount = reader.ReadInt32();
                    int writeIdx = 0;
                    for (int r = 0; r < runCount; r++)
                    {
                        ushort runLen = reader.ReadUInt16();
                        ushort blockVal = reader.ReadUInt16();

                        if (writeIdx + runLen > voxelLength)
                        {
                            throw new SavePayloadCorruptedException($"RLE16 run overflow in chunk {c} ({writeIdx + runLen} > {voxelLength}).");
                        }

                        for (int k = 0; k < runLen; k++)
                        {
                            voxels[writeIdx++] = blockVal;
                        }
                    }

                    if (writeIdx != voxelLength)
                    {
                        throw new SavePayloadCorruptedException($"RLE16 decoded length mismatch in chunk {c} (expected {voxelLength}, got {writeIdx}).");
                    }
                }

                result.Add(new VoxelChunkPayload(pos, size, voxels));
            }

            return result;
        }

        #endregion

        #region IEEE 802.3 CRC-32 Implementation

        public static uint ComputeCrc32(byte[] buffer, int offset, int count)
        {
            if (buffer == null) return 0u;

            uint crc = 0xFFFFFFFFu;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                byte index = (byte)((crc ^ buffer[i]) & 0xFF);
                crc = (crc >> 8) ^ Crc32Table[index];
            }

            return ~crc;
        }

        private static uint[] BuildCrc32Table()
        {
            const uint polynomial = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : (crc >> 1);
                }
                table[i] = crc;
            }
            return table;
        }

        #endregion
    }
}
