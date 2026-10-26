#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace Arawn.CrystalSave.Runtime
{
    internal static class ConflictCandidateReader
    {
        internal static SaveSlot Metadata(byte[] bytes)
        {
            if (bytes == null) return null;
            if (SaveCrypto.HasEnvelope(bytes)) bytes = SaveManager.Instance?.EncryptionService?.MaybeDecrypt(bytes)
                ?? throw new InvalidDataException("Conflict metadata authentication failed.");
            return SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes)
                ?? throw new InvalidDataException("Conflict metadata is invalid.");
        }
        internal static byte[] FileBytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        internal static SaveConflictCandidate Local(string payload, string metadata, string legacyMetadata, int slotNumber)
        {
            var bytes = CloudMirrorFiles.ReadCommittedBytes(payload);
            if (bytes == null) return null;
            var header = Metadata(CloudMirrorFiles.ReadCommittedBytes(metadata) ?? CloudMirrorFiles.ReadCommittedBytes(legacyMetadata));
            if (header != null && header.SlotNumber != slotNumber)
                throw new InvalidDataException("Local conflict metadata does not match the requested slot.");
            return new SaveConflictCandidate(bytes, header);
        }
    }
    public partial class SaveSystem
    {
        public async Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken cancellationToken)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            var local = await LoadLocalCandidateBytesAsync(slot, Sanitize(ResolveSaveKey(slot.SlotNumber)));
            var localMeta = local == null ? null : await LoadLocalSlotMetadataOnlyAsync(slot.SlotNumber);
            cancellationToken.ThrowIfCancellationRequested();
            if (settings.enableCloudSave && !SignedIn) throw new InvalidOperationException("Cloud conflict reads require authentication.");
            var remote = settings.enableCloudSave ? await LoadCloudBytesAsync(ResolveCloudKey(slot)) : null;
            var remoteMeta = remote == null ? null : await LoadCloudSlotMetadataOnlyAsync(slot.SlotNumber);
            cancellationToken.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(local == null ? null : new SaveConflictCandidate(local, localMeta),
                remote == null ? null : new SaveConflictCandidate(remote, remoteMeta));
        }
    }
    public partial class MySqlSaveSystem
    {
        public async Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken cancellationToken)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(cancellationToken);
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            cancellationToken.ThrowIfCancellationRequested();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            var local = LocalWritesEnabled() ? ConflictCandidateReader.Local(
                Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"), MetaLocalPath(slot.SlotNumber), LegacyMetaLocalPath(slot.SlotNumber), slot.SlotNumber) : null;
            var remote = await LoadAsync(slot);
            var metadata = remote == null ? null : await ReadRemoteConflictMetadataAsync(slot.SlotNumber);
            cancellationToken.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(local, remote == null ? null : new SaveConflictCandidate(remote, metadata));
        }
        async Task<SaveSlot> ReadRemoteConflictMetadataAsync(int num)
        {
            string url = $"{apiUrl}/metadata?uid={WebUtility.UrlEncode(UserId)}&slot={num}";
#if UNITY_WEBGL && !UNITY_EDITOR
            var (ok, code, text, err) = await GetTextWebGLAsync(url);
            if (code == 404) return null;
            if (!ok)
                throw new IOException($"GET {url} → {code} {err}");
            string json = text;
#else
            using var resp = await httpClient.GetAsync(url, SaveIoScope.Token).ConfigureAwait(SaveIoScope.PreserveUnityContext);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return null;
            if (!resp.IsSuccessStatusCode)
                throw new IOException($"GET {url} → {(int)resp.StatusCode} {resp.ReasonPhrase}");
            string json = await resp.Content.ReadAsStringAsync().ConfigureAwait(SaveIoScope.PreserveUnityContext);
#endif
            SaveIoScope.CheckCancellation();
            return ConvertRemoteMetadata(CloudMetadataJson.Decode<SlotMeta>(json), num);
        }
    }
    public sealed partial class FirebaseSaveSystem
    {
        public async Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken cancellationToken)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(cancellationToken);
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            cancellationToken.ThrowIfCancellationRequested();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            var local = LocalWritesEnabled() ? ConflictCandidateReader.Local(
                Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"), MetaLocalPath(slot.SlotNumber), LegacyMetaLocalPath(slot.SlotNumber), slot.SlotNumber) : null;
            var remote = await LoadAsync(slot);
            SaveSlot metadata = null;
            if (remote != null)
            {
                var bytes = await DownloadObject(PathFor(slot.SlotNumber, META_EXT));
                metadata = CloudSlotMetadata.Decode(bytes, slot.SlotNumber);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(local, remote == null ? null : new SaveConflictCandidate(remote, metadata));
        }
    }
    public sealed partial class SupabaseSaveSystem
    {
        public async Task<SaveConflictCandidates> ReadCandidatesAsync(SaveSlot slot, CancellationToken cancellationToken)
        {
            using var cancellation = SaveIoScope.WithReadCancellation(cancellationToken);
            using var identity = new CloudRequestScope(this, await CaptureRequestIdentityAsync());
            cancellationToken.ThrowIfCancellationRequested();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            var local = LocalWritesEnabled() ? ConflictCandidateReader.Local(
                Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav"), MetaLocalPath(slot.SlotNumber), LegacyMetaLocalPath(slot.SlotNumber), slot.SlotNumber) : null;
            var remote = await LoadAsync(slot);
            SaveSlot metadata = null;
            if (remote != null)
            {
                var bytes = await DownloadObject(PathFor(slot.SlotNumber, META_EXT));
                metadata = CloudSlotMetadata.Decode(bytes, slot.SlotNumber);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new SaveConflictCandidates(local, remote == null ? null : new SaveConflictCandidate(remote, metadata));
        }
    }
}
#endif
