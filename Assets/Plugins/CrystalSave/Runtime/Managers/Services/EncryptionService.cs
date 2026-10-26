#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
        public class EncryptionService
        {
                private readonly SaveSettings settings;
                private IMasterSecretProvider secretProvider;
                private byte[] masterSecret;
                private byte[] cryptoKey;

                public bool UseEncryption { get; private set; }

                public EncryptionService(SaveSettings settings)
                {
                        this.settings = settings;
                }

                public async Task InitializeAsync(string userId)
                {
                        UseEncryption = settings.enableEncryption;
                        if (UseEncryption && settings.masterSecretProvider == null)
                        {
                                Logger.Log(
                                        "Encryption enabled but no Master-Secret Provider assigned – encrypted writes will fail until corrected.",
                                        LogLevel.Warning);
                                UseEncryption = false;
                                return;
                        }

                        if (UseEncryption)
                        {
                                secretProvider = settings.masterSecretProvider as IMasterSecretProvider;
                                if (secretProvider == null)
                                {
                                        Logger.Log(
                                                "Master-Secret Provider does not implement IMasterSecretProvider – encrypted writes will fail until corrected.",
                                                LogLevel.Warning);
                                        UseEncryption = false;
                                        return;
                                }
                                masterSecret   = await secretProvider.GetMasterSecretAsync();
                                if (masterSecret?.Length != 32)
                                {
                                        Logger.Log(
                                                "Master-secret must be exactly 32 bytes – encrypted writes will fail until corrected.",
                                                LogLevel.Warning);
                                        UseEncryption  = false;
                                        secretProvider = null;
                                        masterSecret   = null;
                                        cryptoKey      = null;
                                }
                                else
                                {
                                        string effectiveUserId = settings.useUserIdForEncryption
                                                ? userId
                                                : SaveCrypto.GlobalUserId;
                                        cryptoKey = SaveCrypto.DeriveKey(effectiveUserId, masterSecret);
                                }
                        }
                }

                public byte[] MaybeEncrypt(byte[] plain)
                {
                        if (!UseEncryption)
                        {
                            if (settings.enableEncryption)
                                throw new System.Security.Cryptography.CryptographicException("Encryption is configured but its key/provider is unavailable.");
                            return plain;
                        }
                        return SaveCrypto.EncryptV2(plain, cryptoKey);
                }

                public byte[] MaybeDecrypt(byte[] blob)
                {
                        bool looksEncrypted = SaveCrypto.HasEnvelope(blob);

                        // If the payload isn't tagged as encrypted, pass through unchanged
                        if (!looksEncrypted) return blob;

                        // If encryption is disabled, don't attempt to decrypt old encrypted data
                        if (!UseEncryption)
                        {
                                Logger.Log("Encountered encrypted save data while encryption is disabled; treating as unavailable.", LogCategory.Cryptography, LogLevel.Info);
                                return null;
                        }

                        // Guard against missing/invalid key to avoid noisy errors
                        if (cryptoKey == null || cryptoKey.Length != 32)
                        {
                                Logger.Log("Encryption key unavailable; cannot decrypt encrypted save data.", LogCategory.Cryptography, LogLevel.Warning);
                                return null;
                        }

                        if (SaveCrypto.TryDecrypt(blob, cryptoKey, 5, out var plain))
                                return plain;

                        Logger.Log("Load failed: save file was encrypted with a different master secret.",
                                LogLevel.Error);
                        return null;
                }
        }
}
#endif
