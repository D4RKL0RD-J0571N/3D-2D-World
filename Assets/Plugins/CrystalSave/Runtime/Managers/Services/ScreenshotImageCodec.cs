#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    // Authenticated image bytes and decoded textures have separate lifetimes.
    // Callers provide the originating encryption service and own successful textures.
    internal static class ScreenshotImageCodec
    {
        private static byte[] DecodeBytes(byte[] encoded, EncryptionService encryption)
        {
            if (encoded == null || encoded.Length == 0) return null;
            if (!SaveCrypto.HasEnvelope(encoded)) return encoded;
            return encryption?.MaybeDecrypt(encoded) ?? throw new InvalidDataException("Screenshot authentication failed.");
        }

        /// <summary>Decode an encrypted or legacy image for display without changing its stored bytes.</summary>
        internal static Texture2D DecodeTexture(byte[] encoded, EncryptionService encryption)
        {
            Texture2D texture = null;
            try
            {
                byte[] plain = DecodeBytes(encoded, encryption);
                if (plain == null) return null;
                texture = new Texture2D(2, 2);
                if (texture.LoadImage(plain)) return texture;
            }
            catch (Exception ex)
            {
                Logger.Log($"Screenshot could not be decoded: {ex.Message}", LogCategory.ScreenshotManager, LogLevel.Warning);
            }
            ReleaseTexture(texture);
            return null;
        }

        internal static void ReleaseTexture(Texture2D texture)
        {
            if (texture == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(texture);
            else
#endif
                UnityEngine.Object.Destroy(texture);
        }

        internal static byte[] PrepareCacheBytes(byte[] encoded, SaveSettings settings, EncryptionService encryption)
        {
            byte[] plain = DecodeBytes(encoded, encryption);
            if (plain == null) throw new InvalidDataException("Screenshot data is empty.");
            // Authenticate an existing envelope, retaining its exact bytes. Old plaintext
            // images are readable, but a new cache/copy follows the current encryption setting.
            if (SaveCrypto.HasEnvelope(encoded)) return encoded;
            if (settings == null || !settings.enableEncryption || !settings.encryptScreenshots ||
                settings.enableCloudSave && settings.cloudCryptoMode == CloudCryptoMode.ServerSide)
                return plain;
            if (encryption == null) throw new InvalidOperationException("Screenshot encryption service is unavailable.");
            return encryption.MaybeEncrypt(plain);
        }

    }
}
#endif
