#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveFormatHardeningTests
    {
        static byte[] Key => Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine("ci", "fixtures", "legacy-v1", name));
        static object Call(string type, string method, params object[] arguments)
        {
            var target = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime." + type, true);
            var candidate = target.GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(m => m.Name == method && m.GetParameters().Length == arguments.Length && m.GetParameters().Select((p, i) => arguments[i] == null || p.ParameterType.IsInstanceOfType(arguments[i])).All(v => v));
            try { return candidate.Invoke(null, arguments); }
            catch (TargetInvocationException ex) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
        }
        static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        static object Decode(byte[] bytes, SaveBundleOptions options = null) => Call("SaveBundleCodec", "Decode", bytes, options);
        [TestCase("gcm.csav")] [TestCase("cbc.csav")]
        public void FrozenLegacyEncryptionRemainsReadable(string name)
            => Assert.That(SaveCrypto.Decrypt(Fixture(name), Key, 5), Is.EqualTo(Fixture("plain.bin")));
        [Test]
        public void V2WritesGcmWithFreshNonceAndAuthenticatedHeader()
        {
            var plain = new byte[128];
            var first = SaveCrypto.EncryptV2(plain, Key); var second = SaveCrypto.EncryptV2(plain, Key);
            Assert.That(first.Take(6), Is.EqualTo(new byte[] { 67, 83, 65, 86, 2, 1 }));
            Assert.That(first.Length, Is.EqualTo(6 + 12 + plain.Length + 16));
            Assert.That(first.Skip(6).Take(12), Is.Not.EqualTo(second.Skip(6).Take(12)));
            Assert.That(first.Skip(18).Take(32), Is.Not.EqualTo(second.Skip(18).Take(32)));
            Assert.That(SaveCrypto.Decrypt(first, Key, 5), Is.EqualTo(plain));
        }
        [TestCase(0)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(18)] [TestCase(49)]
        public void TamperedV2CannotDecrypt(int index)
        {
            var encrypted = SaveCrypto.EncryptV2(new byte[16], Key); encrypted[index] ^= 1;
            Assert.That(SaveCrypto.TryDecrypt(encrypted, Key, 5, out _), Is.False);
        }
        [Test]
        public void EveryTruncatedEnvelopeAndWrongKeyIsRejected()
        {
            foreach (var bytes in new[] { SaveCrypto.EncryptV2(Fixture("plain.bin"), Key), Fixture("gcm.csav"), Fixture("cbc.csav") })
            {
                for (int n = 0; n < bytes.Length; n++) Assert.That(SaveCrypto.TryDecrypt(bytes.Take(n).ToArray(), Key, 5, out _), Is.False, "length " + n);
                Assert.That(SaveCrypto.TryDecrypt(bytes, new byte[32], 5, out _), Is.False);
            }
        }
        [Test]
        public void LegacyBundleReadsEscapesBracesAndUnicodeAndReplacesMetadata()
        {
            var decoded = Decode(Fixture("bundle.crystalsave"));
            Assert.That(Field<byte[]>(decoded, "SaveBytes"), Is.EqualTo(Fixture("savedata.bin")));
            var manifest = Field<SaveBundleManifest>(decoded, "Manifest");
            Assert.That(manifest.SlotName, Is.EqualTo("Legacy\nSlot"));
            Assert.That(manifest.CustomMetadata["note"], Is.EqualTo("Quotes: \"yes\" and } and 雪"));
        }
        [Test]
        public void V2ManifestRoundTripsWithoutJsonAndPreservesAllFields()
        {
            var manifest = new SaveBundleManifest { SourceSlotNumber = 2, SlotName = "\"\\\n\t雪😀}", LastSaved = DateTime.UtcNow, LastActiveScene = "Scene", CustomMetadata = new Dictionary<string, string> { ["}雪"] = "\r\n\t\"\\" } };
            var bytes = (byte[])Call("SaveBundleCodec", "Encode", manifest, Fixture("savedata.bin"), null, null, null);
            var loaded = Field<SaveBundleManifest>(Decode(bytes), "Manifest");
            Assert.That(loaded.SlotName, Is.EqualTo(manifest.SlotName));
            Assert.That(loaded.CustomMetadata, Is.EqualTo(manifest.CustomMetadata));
            Assert.That(loaded.LastSaved, Is.EqualTo(manifest.LastSaved));
        }
        static byte[] Container(Action<BinaryWriter> entries, int count = 1, byte version = 2)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, true))
            using (var writer = new BinaryWriter(gzip)) { writer.Write(Encoding.ASCII.GetBytes("CSAVBX")); writer.Write(version); writer.Write((ushort)count); entries(writer); }
            return output.ToArray();
        }
        static void Entry(BinaryWriter writer, string name, byte[] bytes, int? length = null)
        { var encoded = Encoding.UTF8.GetBytes(name); writer.Write((ushort)encoded.Length); writer.Write(encoded); writer.Write(length ?? bytes.Length); writer.Write(bytes); }
        [TestCase("savedata.bin", -1)] [TestCase("savedata.bin", int.MaxValue)] [TestCase("../savedata.bin", 1)] [TestCase("", 1)]
        public void InvalidEntryNamesAndLengthsAreRejectedBeforeAllocation(string name, int length)
            => Assert.Throws<InvalidDataException>(() => Decode(Container(w => Entry(w, name, new byte[1], length))));
        [Test]
        public void TruncatedEntryAndDuplicateEntryAreRejected()
        {
            Assert.Throws<EndOfStreamException>(() => Decode(Container(w => Entry(w, "savedata.bin", new byte[1], 5))));
            Assert.Throws<InvalidDataException>(() => Decode(Container(w => { Entry(w, "savedata.bin", new byte[1]); Entry(w, "SAVEDATA.BIN", new byte[1]); }, 2)));
        }
        [Test]
        public void LimitsBoundInputEntryCountNamesManifestAndExpansion()
        {
            var bytes = Fixture("bundle.crystalsave");
            foreach (var limits in new[] { new SaveBundleOptions { MaxCompressedBytes = 16 }, new SaveBundleOptions { MaxExpandedBytes = 32 }, new SaveBundleOptions { MaxManifestBytes = 16 }, new SaveBundleOptions { MaxEntries = 1 }, new SaveBundleOptions { MaxNameBytes = 4 }, new SaveBundleOptions { MaxEntryBytes = 8 } })
                Assert.Throws<InvalidDataException>(() => Decode(bytes, limits));
            var bomb = Container(w => Entry(w, "savedata.bin", new byte[1024 * 1024]));
            Assert.Throws<InvalidDataException>(() => Decode(bomb, new SaveBundleOptions { MaxExpandedBytes = 4096 }));
        }
        [Test]
        public void TrailingContainerDataAndUnsupportedVersionsAreRejected()
        {
            Assert.Throws<InvalidDataException>(() => Decode(Container(w => { Entry(w, "savedata.bin", new byte[1]); w.Write((byte)7); })));
            Assert.Throws<InvalidDataException>(() => Decode(Container(w => { }, 1, 3)));
            Assert.Throws<InvalidDataException>(() => Decode(Container(w => { Entry(w, "savedata.bin", new byte[1]); Entry(w, "manifest.json", Encoding.UTF8.GetBytes("{}")); }, 2)));
        }

        [Test]
        public void EveryTruncatedGzipTrailerAndAppendedGarbageIsRejected()
        {
            var bytes = Fixture("bundle.crystalsave");
            for (int missing = 1; missing <= 8; missing++)
                Assert.That(() => Decode(bytes.Take(bytes.Length - missing).ToArray()), Throws.Exception);
            Assert.That(() => Decode(bytes.Concat(new byte[] { 9 }).ToArray()), Throws.Exception);
            // A copied, otherwise valid trailer must not conceal appended container data.
            Assert.That(() => Decode(bytes.Concat(bytes.Skip(bytes.Length - 8)).ToArray()), Throws.Exception);
        }
        [Test]
        public void BinaryManifestMustConsumeItsEntireEntry()
        {
            var manifest = MemoryPack.MemoryPackSerializer.Serialize(new SaveBundleManifest());
            var bytes = Container(w => { Entry(w, "savedata.bin", Fixture("savedata.bin")); Entry(w, "manifest.bin", manifest.Concat(new byte[] { 9 }).ToArray()); }, 2);
            Assert.Throws<InvalidDataException>(() => Decode(bytes));
        }
        [Test]
        public void LegacyJsonHasStrictDepthTypesDuplicatesAndVersion()
        {
            foreach (var json in new[] { "{\"version\":2}", "{\"slotName\":1}", "{\"slotName\":\"a\",\"slotName\":\"b\"}", "{\"extra\":{\"deep\":{}}}" })
                Assert.That(() => Call("SaveBundleCodec", "ReadLegacyManifest", json, new SaveBundleOptions { MaxJsonDepth = 2 }), Throws.Exception);
            var safe = (SaveBundleManifest)Call("SaveBundleCodec", "ReadLegacyManifest", "{\"$type\":\"Untrusted.Type, Assembly\",\"slotName\":\"safe\"}", null);
            Assert.That(safe.SlotName, Is.EqualTo("safe"));
        }
        [Test]
        public void ConfiguredEncryptionCannotFallBackToPlaintext()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<SaveSettings>(); settings.enableEncryption = true;
            try { Assert.Throws<CryptographicException>(() => new EncryptionService(settings).MaybeEncrypt(Fixture("plain.bin"))); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [Test]
        public void ServerSideMetadataKeepsItsExistingProviderProtocol()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableEncryption = true; settings.encryptSlotMetadata = true;
            settings.enableCloudSave = true; settings.cloudCryptoMode = CloudCryptoMode.ServerSide;
            try
            {
                var bytes = (byte[])Call("SaveSystem", "SerializeSlotMetadata", settings, new SaveSlot(1, "opaque provider", DateTime.MinValue, null, null));
                Assert.That(SaveCrypto.HasEnvelope(bytes), Is.False);
                Assert.That(SaveDataSerializer.Instance.Deserialize<SaveSlot>(bytes).SlotName, Is.EqualTo("opaque provider"));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }
        [TestCase("journal", false)] [TestCase("prepared", false)] [TestCase("payload", false)] [TestCase("metadata", false)] [TestCase("committed", true)] [TestCase("cleanup", true)] [TestCase("cleanup-permission", true)]
        public void InterruptedPairRecoversOneCompleteGeneration(string boundary, bool committed)
        {
            string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string payload = Path.Combine(root, "Slot1.sav"), metadata = Path.Combine(root, "Slot1_Meta.bin");
            var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction").GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
            try
            {
                File.WriteAllBytes(payload, new byte[] { 1 }); File.WriteAllBytes(metadata, new byte[] { 1 });
                hook.SetValue(null, (Action<string>)(stage =>
                {
                    if (boundary == "cleanup-permission" && stage == "cleanup") throw new UnauthorizedAccessException("Injected cleanup denial");
                    if (stage == boundary) throw new IOException("Injected storage interruption");
                }));
                try { Call("SaveFileTransaction", "Commit", root, 1, payload, new byte[] { 2 }, metadata, new byte[] { 2 }); }
                catch (IOException) { Assert.That(committed, Is.False); }
                hook.SetValue(null, null);
                Call("SaveFileTransaction", "RecoverAll", root);
                Assert.That(File.ReadAllBytes(payload), Is.EqualTo(new byte[] { (byte)(committed ? 2 : 1) }));
                Assert.That(File.ReadAllBytes(metadata), Is.EqualTo(File.ReadAllBytes(payload)));
                Assert.That(Directory.GetDirectories(root), Is.Empty);
                Call("AtomicSaveFile", "Delete", payload); Call("AtomicSaveFile", "Delete", metadata);
                Call("SaveFileTransaction", "RecoverAll", root);
                Assert.That(Directory.GetFiles(root), Is.Empty);
            }
            finally { hook.SetValue(null, null); Directory.Delete(root, true); }
        }
    }
}
#endif
