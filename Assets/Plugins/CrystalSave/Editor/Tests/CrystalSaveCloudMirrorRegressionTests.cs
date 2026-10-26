#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME && CRYSTALSAVE_NEWTONSOFT
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;
using Object = UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator FinalReview_ServerCryptoMirrorCanSaveTwice()
            => FinalReviewServerCrypto(true);

        [UnityTest] public IEnumerator FinalReview_ServerCryptoWithoutMirrorControl()
            => FinalReviewServerCrypto(false);

        [UnityTest] public IEnumerator ReleaseReview_OpaqueMirrorVerificationRejectsCorruption()
            => FinalReviewServerCrypto(true, true, true);

        [UnityTest] public IEnumerator ReleaseReview_OpaqueTemporaryVerificationRejectsCorruption()
            => FinalReviewServerCrypto(false, true, true);

        [UnityTest] public IEnumerator ReleaseReview_OpaqueVerificationHealthyMirror()
            => FinalReviewServerCrypto(true, true);

        [UnityTest] public IEnumerator ReleaseReview_OpaqueVerificationHealthyTemporary()
            => FinalReviewServerCrypto(false, true);

        IEnumerator FinalReviewServerCrypto(bool mirror, bool verify = false, bool corrupt = false)
        {
            using var server = new CloudRefreshServer();
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var cfg = RestorationCloudSettings(server.Url);
            cfg.keepLocalMirror = mirror;
            cfg.enableSaveFileVerification = verify;
            cfg.enableEncryption = true;
            cfg.cloudCryptoMode = CloudCryptoMode.ServerSide;
            var provider = ScriptableObject.CreateInstance<FinalReviewOpaqueProvider>();
            cfg.cloudCryptoProvider = provider;
            var storage = new FirebaseSaveSystem(cfg, CloudRefreshRoot("server-crypto"));
            Set(storage, "url", server.Url);
            var settings = manager.SaveSettings;
            bool oldCloud = settings.enableCloudSave, oldEncryption = settings.enableEncryption, oldMirror = settings.keepLocalMirror;
            bool oldVerification = settings.enableSaveFileVerification;
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                .GetField("FaultInjection", BindingFlags.NonPublic | BindingFlags.Static);
            var oldBackend = settings.backend;
            var oldMode = settings.cloudCryptoMode;
            var oldProvider = settings.cloudCryptoProvider;
            try
            {
                Set(manager, "saveSystem", storage);
                settings.enableCloudSave = true;
                settings.backend = SaveBackend.Firebase;
                settings.keepLocalMirror = mirror;
                settings.enableSaveFileVerification = verify;
                settings.enableEncryption = true;
                settings.cloudCryptoMode = CloudCryptoMode.ServerSide;
                settings.cloudCryptoProvider = provider;
                yield return Await(manager.SaveImmediateAsync(1));
                Assert.That(provider.EncryptCalls, Is.EqualTo(1), "first save is the control");
                var copies = storage.ReadCandidatesAsync(manager.GetSlotMetadata(1), CancellationToken.None);
                yield return Await(copies);
                Assert.That(copies.Result.Remote.Payload.Take(4), Is.EqualTo(FinalReviewOpaqueProvider.Prefix));
                Assert.That(copies.Result.Local != null, Is.EqualTo(mirror));
                if (corrupt && mirror)
                    hook.SetValue(null, (Action<string>)(stage =>
                    {
                        if (stage == "committed")
                            File.WriteAllBytes(Directory.GetFiles(storage.GetSaveGamesPath(), "*.sav").Single(), new byte[] { 0x7f });
                    }));
                provider.CorruptEncryption = corrupt && !mirror;
                LogAssert.ignoreFailingMessages = true;
                var second = manager.SaveImmediateAsync(1);
                yield return Until(() => second.IsCompleted, "second opaque save", 15);
                TestContext.WriteLine("mirror=" + mirror + ", second=" + second.Status + ", decryptCalls=" + provider.DecryptCalls + ", error=" + second.Exception);
                hook.SetValue(null, null);
                Assert.That(second.IsFaulted, Is.EqualTo(corrupt), "Configured verification must decode opaque bytes through their provider: " + second.Exception);
                Assert.That(provider.EncryptCalls, Is.EqualTo(2));
                if (corrupt)
                {
                    var retained = storage.ReadCandidatesAsync(manager.GetSlotMetadata(1), CancellationToken.None);
                    yield return Await(retained);
                    var expected = mirror ? copies.Result.Local : copies.Result.Remote;
                    var actual = mirror ? retained.Result.Local : retained.Result.Remote;
                    Assert.That(actual.Payload, Is.EqualTo(expected.Payload));
                    Assert.That(actual.Metadata.LastSaved, Is.EqualTo(expected.Metadata.LastSaved));
                    Assert.That(actual.Metadata.SlotName, Is.EqualTo(expected.Metadata.SlotName));
                    provider.CorruptEncryption = false;
                    yield return Await(manager.SaveImmediateAsync(1));
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                hook.SetValue(null, null);
                Set(manager, "saveSystem", original);
                settings.enableCloudSave = oldCloud;
                settings.enableEncryption = oldEncryption;
                settings.keepLocalMirror = oldMirror;
                settings.enableSaveFileVerification = oldVerification;
                settings.backend = oldBackend;
                settings.cloudCryptoMode = oldMode;
                settings.cloudCryptoProvider = oldProvider;
                Object.Destroy(provider);
                Object.Destroy(cfg);
            }
        }

        [UnityTest] public IEnumerator FinalReview_FirebaseRenameKeepsLocalGenerationMetadata()
            => FinalReviewRename(false);

        [UnityTest] public IEnumerator FinalReview_SupabaseRenameKeepsLocalGenerationMetadata()
            => FinalReviewRename(true);

        [UnityTest] public IEnumerator FinalReview_FirebaseRenameKeepsOlderRemoteGeneration()
            => FinalReviewRename(false, false);

        [UnityTest] public IEnumerator FinalReview_SupabaseRenameKeepsOlderRemoteGeneration()
            => FinalReviewRename(true, false);

        IEnumerator FinalReviewRename(bool supabase, bool remoteNewer = true)
        {
            using var firebaseServer = new CloudRefreshServer();
            using var supabaseServer = new RestorationCloudServer();
            string url = supabase ? supabaseServer.Url : firebaseServer.Url;
            var manager = SaveManager.Instance;
            var original = manager.SaveSystem;
            var originalService = Get<CloudSaveService>(manager, "cloudSaveService");
            var cfg = RestorationCloudSettings(url);
            cfg.keepLocalMirror = true;
            cfg.backend = supabase ? SaveBackend.Supabase : SaveBackend.Firebase;
            ISaveSystem storage = supabase
                ? new SupabaseSaveSystem(cfg, CloudRefreshRoot("rename-mirror"))
                : new FirebaseSaveSystem(cfg, CloudRefreshRoot("rename-mirror"));
            if (!supabase) Set(storage, "url", url);
            var remoteCfg = Object.Instantiate(cfg);
            remoteCfg.keepLocalMirror = false;
            ISaveSystem remoteStorage = supabase
                ? new SupabaseSaveSystem(remoteCfg, CloudRefreshRoot("remote-writer"))
                : new FirebaseSaveSystem(remoteCfg, CloudRefreshRoot("remote-writer"));
            if (!supabase) Set(remoteStorage, "url", url);
            var localData = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(remoteNewer ? 2024 : 2026, 1, 1), LastActiveScene = "A" };
            var remoteData = new SaveData(manager.SaveSettings.version) { LastSaved = new DateTime(2025, 1, 1), LastActiveScene = "B" };
            var localSlot = new SaveSlot(1, "Local", localData.LastSaved, "", "A");
            var remoteSlot = new SaveSlot(1, "Remote", remoteData.LastSaved, "", "B");
            localSlot.CustomMetadata["generation"] = "local";
            localSlot.CustomMetadata["escaped\nkey"] = "line\nnext\t\0雪";
            localSlot.CustomMetadata["null"] = null;
            remoteSlot.CustomMetadata["generation"] = "remote";
            bool oldCloud = manager.SaveSettings.enableCloudSave, oldMirror = manager.SaveSettings.keepLocalMirror;
            var oldBackend = manager.SaveSettings.backend;
            try
            {
                yield return Await(storage.SaveAsync(manager.Serializer.Serialize(localData), localSlot));
                yield return Await(remoteStorage.SaveAsync(manager.Serializer.Serialize(remoteData), remoteSlot));
                Set(manager, "saveSystem", storage);
                Set(manager, "cloudSaveService", new CloudSaveService(manager));
                manager.SaveSettings.enableCloudSave = true;
                manager.SaveSettings.keepLocalMirror = true;
                manager.SaveSettings.backend = cfg.backend;
                Call(manager, "SyncSlotToInternalList", localSlot);
                yield return Await(manager.RefreshRemoteSlotsAsync());
                Assert.That(manager.GetSlotMetadata(1).CustomMetadata["generation"], Is.EqualTo(remoteNewer ? "remote" : "local"), "newer header is published");
                var before = ((ISaveConflictSource)storage).ReadCandidatesAsync(remoteSlot, CancellationToken.None);
                yield return Await(before);
                Assert.That(before.Result.Local.Metadata.CustomMetadata["generation"], Is.EqualTo("local"), "refresh alone preserves the local generation");
                var rename = manager.RenameSaveSlotAsync(1, "Player's name");
                yield return Await(rename);
                Assert.That(rename.Result, Is.True);
                var after = ((ISaveConflictSource)storage).ReadCandidatesAsync(remoteSlot, CancellationToken.None);
                yield return Await(after);
                var payload = manager.Serializer.Deserialize<SaveData>(after.Result.Local.Payload);
                Assert.That(payload.LastSaved, Is.EqualTo(localData.LastSaved), "local payload has not been replaced");
                TestContext.WriteLine("backend=" + cfg.backend + ", payload=" + payload.LastSaved + ", header=" + after.Result.Local.Metadata.LastSaved + ", generation=" + after.Result.Local.Metadata.CustomMetadata["generation"]);
                Assert.That(after.Result.Local.Metadata.CustomMetadata["generation"], Is.EqualTo("local"), "Renaming a remote generation must not copy its gameplay metadata onto an older local payload");
                Assert.That(after.Result.Local.Metadata.LastSaved, Is.EqualTo(payload.LastSaved));
                Assert.That(after.Result.Remote.Metadata.CustomMetadata["generation"], Is.EqualTo("remote"));
                Assert.That(after.Result.Remote.Metadata.LastSaved, Is.EqualTo(remoteData.LastSaved));
                Assert.That(after.Result.Remote.Metadata.SlotName, Is.EqualTo("Player's name"));
                Assert.That(after.Result.Local.Metadata.SlotName, Is.EqualTo("Player's name"));

                manager.SaveSettings.autoResolveConflicts = true;
                manager.SaveSettings.autoConflictPolicy = AutoConflictPolicy.LocalWins;
                bool loggedIn = manager.IsSupabaseCustomLoggedIn;
                Set(manager, "isSupabaseCustomLoggedIn", true);
                try { yield return Await(manager.SaveImmediateAsync(1)); }
                finally { Set(manager, "isSupabaseCustomLoggedIn", loggedIn); }
                var selected = ((ISaveConflictSource)storage).ReadCandidatesAsync(remoteSlot, CancellationToken.None);
                yield return Await(selected);
                Assert.That(selected.Result.Remote.Metadata.CustomMetadata["generation"], Is.EqualTo("local"), "LocalWins retains local gameplay metadata after rename");
                Assert.That(selected.Result.Remote.Metadata.CustomMetadata["escaped\nkey"], Is.EqualTo(localSlot.CustomMetadata["escaped\nkey"]));
                Assert.That(selected.Result.Remote.Metadata.CustomMetadata["null"], Is.Null);
                Assert.That(manager.Serializer.Deserialize<SaveData>(selected.Result.Remote.Payload).LastSaved, Is.EqualTo(localData.LastSaved));
            }
            finally
            {
                Set(manager, "saveSystem", original);
                Set(manager, "cloudSaveService", originalService);
                manager.SaveSettings.enableCloudSave = oldCloud;
                manager.SaveSettings.keepLocalMirror = oldMirror;
                manager.SaveSettings.backend = oldBackend;
                Object.Destroy(cfg);
                Object.Destroy(remoteCfg);
            }
        }

        [UnityTest] public IEnumerator FinalReview_NativeSupabaseDeleteRemovesMirrorControl()
        {
            using var server = new RestorationCloudServer();
            var cfg = RestorationCloudSettings(server.Url);
            cfg.keepLocalMirror = true;
            string root = CloudRefreshRoot("delete-control");
            var storage = new SupabaseSaveSystem(cfg, root);
            var slot = new SaveSlot(1, "Delete control", DateTime.UtcNow, "", "A");
            try
            {
                yield return Await(storage.SaveAsync(new byte[] { 4, 5 }, slot));
                string payload = Directory.GetFiles(root, "*.sav", SearchOption.AllDirectories).Single();
                string metadata = Directory.GetFiles(root, "*.bin", SearchOption.AllDirectories).Single();
                yield return Await(storage.DeleteAsync(slot));
                Assert.That(File.Exists(payload) || File.Exists(metadata), Is.False);
            }
            finally { Object.Destroy(cfg); }
        }
    }

    public sealed class FinalReviewOpaqueProvider : ScriptableObject, ICloudCryptoProvider
    {
        internal static readonly byte[] Prefix = { 0xff, 0x51, 0x7f, 0x55 };
        internal int EncryptCalls, DecryptCalls;
        internal bool CorruptEncryption;
        public ValueTask<byte[]> EncryptForCloudAsync(byte[] plain)
        {
            EncryptCalls++;
            if (CorruptEncryption) return new ValueTask<byte[]>(new byte[] { 0x7f });
            return new ValueTask<byte[]>(Prefix.Concat(plain).ToArray());
        }
        public ValueTask<byte[]> DecryptFromCloudAsync(byte[] blob)
        {
            DecryptCalls++;
            if (!blob.Take(Prefix.Length).SequenceEqual(Prefix)) throw new InvalidDataException("Wrong test-provider envelope");
            return new ValueTask<byte[]>(blob.Skip(Prefix.Length).ToArray());
        }
    }
}
#endif
