#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Arawn.CrystalSave.Runtime
{
    public static class SyncDeltaCodec
    {
        // Large existing snapshots remain supported. Larger growth uses a full snapshot,
        // so a tiny malformed delta cannot request an unbounded allocation at the receiver.
        const int MaximumExpandedDeltaBytes = 64 * 1024 * 1024;
        public static bool TryCreateDelta(byte[] baseData, byte[] newData, out SyncDelta delta)
        {
            delta = null;
            if (baseData == null || newData == null) return false;

            var chunks = new List<SyncDeltaChunk>();
            int baseLen = baseData.Length;
            int newLen = newData.Length;
            if (newLen > Math.Max(MaximumExpandedDeltaBytes, baseLen)) return false;

            int i = 0;
            while (i < newLen)
            {
                byte baseByte = i < baseLen ? baseData[i] : (byte)0;
                if (baseByte == newData[i])
                {
                    i++;
                    continue;
                }

                int start = i;
                int end = i + 1;
                while (end < newLen)
                {
                    byte b = end < baseLen ? baseData[end] : (byte)0;
                    if (b == newData[end]) break;
                    end++;
                }

                int length = end - start;
                var segment = new byte[length];
                Buffer.BlockCopy(newData, start, segment, 0, length);
                chunks.Add(new SyncDeltaChunk { offset = start, data = segment });
                i = end;
            }

            if (chunks.Count == 0) return false;

            int diffBytes = 0;
            foreach (var c in chunks) diffBytes += c.data != null ? c.data.Length : 0;

            if (diffBytes >= newLen * 0.9f)
            {
                return false;
            }

            delta = new SyncDelta
            {
                baseHash = ComputeHash(baseData),
                baseLength = baseLen,
                newLength = newLen,
                chunks = chunks
            };
            return true;
        }

        public static byte[] ApplyDelta(byte[] baseData, SyncDelta delta)
        {
            if (baseData == null || delta == null) return null;
            if (delta.baseLength != baseData.Length || delta.newLength < 0 ||
                delta.newLength > Math.Max(MaximumExpandedDeltaBytes, baseData.Length)) return null;
            if (!string.Equals(ComputeHash(baseData), delta.baseHash, StringComparison.Ordinal))
                return null;

            // Validate the complete candidate before allocating or applying any chunk.
            // Clamping malformed offsets silently reconstructs a different snapshot.
            if (delta.chunks != null)
                foreach (var chunk in delta.chunks)
                    if (chunk?.data == null || chunk.offset < 0 || chunk.offset > delta.newLength ||
                        chunk.data.Length > delta.newLength - chunk.offset)
                        return null;

            var result = new byte[delta.newLength];
            int copyLen = Math.Min(baseData.Length, result.Length);
            Buffer.BlockCopy(baseData, 0, result, 0, copyLen);

            if (delta.chunks != null)
            {
                foreach (var chunk in delta.chunks)
                {
                    Buffer.BlockCopy(chunk.data, 0, result, chunk.offset, chunk.data.Length);
                }
            }

            return result;
        }

        public static string ComputeHash(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(data);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
#endif
