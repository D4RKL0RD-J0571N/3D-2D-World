#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
#if BOUNCYCASTLE
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
#endif
namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Authenticated save envelopes. V1 readers preserve both historically shipped ciphers.</summary>
    public static class SaveCrypto
    {
        const int NonceLength = 12, TagLength = 16;
        public const string GlobalUserId = "crystalsave-global";
        public static bool HasEnvelope(ReadOnlySpan<byte> data) => data.Length >= 4 &&
            data[0] == 'C' && data[1] == 'S' && data[2] == 'A' && data[3] == 'V';

        // Preserve the shipped derivation byte-for-byte: changing it would invalidate existing keys.
        public static byte[] DeriveKey(string uid, byte[] masterSecret)
        {
            using var hmac = new HMACSHA256(masterSecret);
            if (string.IsNullOrEmpty(uid)) uid = GlobalUserId;
            byte[] prk = hmac.ComputeHash(Encoding.UTF8.GetBytes(uid));
            return hmac.ComputeHash(prk.Concat(new byte[] { 1 }).ToArray());
        }

        public static byte[] EncryptV2(byte[] plain, byte[] key)
            => Encrypt(plain, key, new byte[] { 67, 83, 65, 86, 2, 1 });

        // Retained for callers explicitly producing the legacy GCM envelope. CBC is read-only.
        public static byte[] Encrypt(byte[] plain, byte[] key, Span<byte> header)
        {
            if (plain == null) throw new ArgumentNullException(nameof(plain));
            ValidateKey(key);
            bool v2 = HasEnvelope(header) && header.Length >= 5 && header[4] == 2;
            if (v2 && (header.Length != 6 || header[5] != 1))
                throw new CryptographicException("Unsupported CSAV cipher header.");
            byte[] nonce = new byte[NonceLength];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(nonce);
            byte[] aad = v2 ? header.ToArray() : Array.Empty<byte>();
            byte[] body = Gcm(true, plain, key, nonce, aad);
            return header.ToArray().Concat(nonce).Concat(body).ToArray();
        }

        public static byte[] Decrypt(ReadOnlySpan<byte> blob, byte[] key, int headerLen)
        {
            ValidateKey(key);
            bool legacy = false;
            byte[] aad = Array.Empty<byte>();
            if (HasEnvelope(blob))
            {
                if (blob.Length < 5) throw new CryptographicException("Truncated CSAV header.");
                switch (blob[4])
                {
                    case 1: headerLen = 5; legacy = true; break;
                    case 2:
                        if (blob.Length < 6 || blob[5] != 1) throw new CryptographicException("Unsupported CSAV cipher.");
                        headerLen = 6; aad = blob.Slice(0, 6).ToArray(); break;
                    default: throw new CryptographicException("Unsupported CSAV version.");
                }
            }
            if (headerLen < 0 || blob.Length - headerLen < NonceLength + TagLength)
                throw new CryptographicException("Truncated encrypted payload.");
            byte[] nonce = blob.Slice(headerLen, NonceLength).ToArray();
            byte[] body = blob.Slice(headerLen + NonceLength).ToArray();
            try { return Gcm(false, body, key, nonce, aad); }
            catch (CryptographicException) when (legacy) { return DecryptLegacyCbc(body, key, nonce); }
            catch (PlatformNotSupportedException) when (legacy) { return DecryptLegacyCbc(body, key, nonce); }
        }

        public static bool TryDecrypt(ReadOnlySpan<byte> blob, byte[] key, int headerLen, out byte[] plain)
        {
            plain = null;
            try { plain = Decrypt(blob, key, headerLen); return true; }
            catch (CryptographicException) { return false; }
            catch (ArgumentException) { return false; }
            catch (PlatformNotSupportedException) { return false; }
        }

        static void ValidateKey(byte[] key)
        {
            if (key == null || key.Length != 32) throw new CryptographicException("A 32-byte encryption key is required.");
        }

        static byte[] Gcm(bool encrypt, byte[] input, byte[] key, byte[] nonce, byte[] aad)
        {
#if BOUNCYCASTLE && (UNITY_WEBGL || UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX)
            return ManagedGcm(encrypt, input, key, nonce, aad);
#else
            try
            {
                using var aes = new AesGcm(key);
                if (encrypt)
                {
                    var ciphertext = new byte[input.Length]; var tag = new byte[TagLength];
                    aes.Encrypt(nonce, input, ciphertext, tag, aad);
                    return ciphertext.Concat(tag).ToArray();
                }
                var plain = new byte[input.Length - TagLength];
                aes.Decrypt(nonce, input.AsSpan(0, plain.Length), input.AsSpan(plain.Length), plain, aad);
                return plain;
            }
#if BOUNCYCASTLE
            catch (PlatformNotSupportedException) { return ManagedGcm(encrypt, input, key, nonce, aad); }
#else
            catch (PlatformNotSupportedException)
            { throw new PlatformNotSupportedException("AES-GCM is unavailable. Install Crystal Save's BouncyCastle dependency before enabling encryption."); }
#endif
#endif
        }
#if BOUNCYCASTLE
        internal static byte[] ManagedGcm(bool encrypt, byte[] input, byte[] key, byte[] nonce, byte[] aad)
        {
            try
            {
                var cipher = new GcmBlockCipher(new AesEngine());
                cipher.Init(encrypt, new AeadParameters(new KeyParameter(key), 128, nonce, aad));
                var output = new byte[cipher.GetOutputSize(input.Length)];
                int written = cipher.ProcessBytes(input, 0, input.Length, output, 0);
                written += cipher.DoFinal(output, written);
                if (written != output.Length) Array.Resize(ref output, written);
                return output;
            }
            catch (InvalidCipherTextException ex) { throw new CryptographicException("Authentication failed.", ex); }
        }
#endif
        static byte[] DecryptLegacyCbc(byte[] body, byte[] key, byte[] nonce)
        {
            int length = body.Length - TagLength;
            if (length <= 0 || length % 16 != 0) throw new CryptographicException("Invalid legacy ciphertext length.");
            byte[] macKey;
            using (var sha = SHA256.Create()) macKey = sha.ComputeHash(nonce.Concat(key).ToArray());
            byte[] mac;
            using (var hmac = new HMACSHA256(macKey)) mac = hmac.ComputeHash(body, 0, length);
            int difference = 0;
            for (int i = 0; i < TagLength; i++) difference |= mac[i] ^ body[length + i];
            if (difference != 0) throw new CryptographicException("Authentication failed.");
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = new byte[16]; // Shipped V1 used a zero IV; retain only for reading.
            aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(body, 0, length);
        }
    }
}
#endif
