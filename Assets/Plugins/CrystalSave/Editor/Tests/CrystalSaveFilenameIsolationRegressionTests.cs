#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveFilenameIsolationRegressionTests
    {
        string root;
        SaveSettings settings;
        SaveSystem storage;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
            settings = ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave = false;
            settings.enableEncryption = false;
            settings.enableScreenshots = false;
            settings.enableSaveFileVerification = false;
            settings.saveMethod = SaveMethod.BinaryFileFormat;
            settings.metadataFileNamePattern = "Slot{n}_{meta:profile}_Meta.bin";
            storage = new SaveSystem(settings, root);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        void SetPattern(bool explicitSlot) => settings.saveFileName = explicitSlot
            ? "Save_{meta:profile}_{n}" : "Save_{meta:profile}";

        static SaveSlot Slot(int number, string profile)
        {
            var slot = new SaveSlot(number, profile, new DateTime(2025, 1, number), null, "Scene");
            slot.CustomMetadata["profile"] = profile;
            return slot;
        }

        void MoveToLegacyRoot(int slotNumber, bool recoveryOnly)
        {
            string folder = Path.Combine(root, settings.ResolveSlotFolderName(slotNumber));
            foreach (string path in Directory.GetFiles(folder))
                File.Move(path, Path.Combine(root, Path.GetFileName(path)) + (recoveryOnly ? ".previous" : ""));
        }

        Dictionary<string, byte[]> Snapshot(int slotNumber, bool legacyRoot)
        {
            string folder = legacyRoot ? root : Path.Combine(root, settings.ResolveSlotFolderName(slotNumber));
            return Directory.GetFiles(folder).ToDictionary(path => path, File.ReadAllBytes);
        }

        static void AssertFilesPreserved(Dictionary<string, byte[]> files)
        {
            foreach (var file in files)
            {
                Assert.That(File.Exists(file.Key), Is.True, "Another slot's file was deleted: " + file.Key);
                Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), "Another slot's file changed: " + file.Key);
            }
        }

        // Promotes the R41 review probe and its explicit-{n} healthy control. Slot 11 also
        // catches a superficially narrower '*1.sav' glob still matching another slot.
        [TestCase(false, false, false)] [TestCase(true, false, false)]
        [TestCase(false, true, false)] [TestCase(true, true, false)]
        [TestCase(false, true, true)] [TestCase(true, true, true)]
        public void SavingOneSlotPreservesOtherSlotGenerations(bool explicitSlot, bool legacyRoot, bool recoveryOnly)
        {
            SetPattern(explicitSlot);
            var other = Slot(11, "old");
            storage.Save(new byte[] { 11, 22 }, other);
            if (legacyRoot) MoveToLegacyRoot(11, recoveryOnly);
            var before = Snapshot(11, legacyRoot);
            storage.Save(new byte[] { 1, 2 }, Slot(1, "new"));
            AssertFilesPreserved(before);
            Assert.That(storage.Load(other), Is.EqualTo(new byte[] { 11, 22 }));
        }

        [TestCase(false, false)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(true, true)]
        public void DeleteRemovesOnlyOwnedLegacyGenerationsAndCannotResurrectThem(bool explicitSlot, bool recoveryOnly)
        {
            SetPattern(explicitSlot);
            var other = Slot(11, "other");
            storage.Save(new byte[] { 11 }, other);
            MoveToLegacyRoot(11, recoveryOnly);
            var before = Snapshot(11, true);
            var deleted = Slot(1, "before");
            storage.Save(new byte[] { 1 }, deleted);
            MoveToLegacyRoot(1, recoveryOnly);
            storage.Delete(Slot(1, "changed"));
            AssertFilesPreserved(before);
            storage = new SaveSystem(settings, root);
            Assert.That(storage.Load(deleted), Is.Null, "Deleted .previous files must not resurrect the slot.");
            Assert.That(storage.Load(other), Is.EqualTo(new byte[] { 11 }));
        }

        [TestCase(false, false)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(true, true)]
        public async Task ChangedMetadataDiscoversAndCleansItsOwnLegacyRecoveryFiles(bool explicitSlot, bool recoveryOnly)
        {
            SetPattern(explicitSlot);
            var original = Slot(1, "before");
            storage.Save(new byte[] { 1, 2, 3 }, original);
            MoveToLegacyRoot(1, recoveryOnly);
            var changed = Slot(1, "after");
            Assert.That(storage.Load(changed), Is.EqualTo(new byte[] { 1, 2, 3 }));
            var header = await storage.LoadSlotMetadataAsync(1);
            Assert.That(header.CustomMetadata["profile"], Is.EqualTo("before"));
            storage.Save(new byte[] { 4 }, changed);
            Assert.That(Directory.GetFiles(root, "*.sav*"), Is.Empty, "Committed replacement must retire its own legacy generation.");
            Assert.That(Directory.GetFiles(root, "*.bin*"), Is.Empty);
            Assert.That(new SaveSystem(settings, root).Load(changed), Is.EqualTo(new byte[] { 4 }));
        }

        [TestCase(false)] [TestCase(true)]
        public void VerificationRollbackRetainsOtherSlotsAndPreviousMetadataDerivedPaths(bool explicitSlot)
        {
            SetPattern(explicitSlot);
            settings.enableSaveFileVerification = true;
            var other = Slot(11, "other");
            storage.Save(new byte[] { 11 }, other);
            MoveToLegacyRoot(11, true);
            var before = Snapshot(11, true);
            var previous = Slot(1, "before");
            storage.Save(new byte[] { 1, 2 }, previous);
            MoveToLegacyRoot(1, true);
            var incoming = Slot(1, "after");
            storage.BackupSlot(incoming);
            storage.Save(new byte[] { 9 }, incoming);
            storage = new SaveSystem(settings, root);
            AssertFilesPreserved(before);
            Assert.That(storage.Load(incoming), Is.EqualTo(new byte[] { 1, 2 }));
            storage.Delete(incoming);
            Assert.That(new SaveSystem(settings, root).Load(previous), Is.Null);
            Assert.That(storage.Load(other), Is.EqualTo(new byte[] { 11 }));
        }

        [TestCase(".bak")] [TestCase(".previous")] [TestCase(".record")]
        public async Task CustomMetadataExtensionsAreNotMistakenForRecoverySuffixes(string extension)
        {
            SetPattern(false);
            settings.metadataFileNamePattern = "Slot{n}" + extension;
            var previous = Slot(1, "before");
            storage.Save(new byte[] { 1 }, previous);
            MoveToLegacyRoot(1, true);
            var header = await storage.LoadSlotMetadataAsync(1);
            Assert.That(header.CustomMetadata["profile"], Is.EqualTo("before"));
            Assert.That(storage.Load(Slot(1, "after")), Is.EqualTo(new byte[] { 1 }));
            storage.Save(new byte[] { 2 }, Slot(1, "after"));
            var current = await storage.LoadSlotMetadataAsync(1);
            Assert.That(current.CustomMetadata["profile"], Is.EqualTo("after"));
            Assert.That(storage.Load(current), Is.EqualTo(new byte[] { 2 }));
        }

        [Test]
        public async Task DiscoveryDoesNotPromoteAnOlderRecoveryCopyOverTheCommittedVariant()
        {
            SetPattern(false);
            Directory.CreateDirectory(root);
            foreach (string profile in new[] { "older", "newer" })
            {
                bool older = profile == "older";
                string suffix = older ? ".previous" : "";
                var slot = Slot(1, profile);
                string payload = Path.Combine(root, "Save_" + profile + "1.sav") + suffix;
                string metadata = Path.Combine(root, "Slot1_" + profile + "_Meta.bin") + suffix;
                File.WriteAllBytes(payload, new byte[] { older ? (byte)1 : (byte)2 });
                File.WriteAllBytes(metadata, SaveDataSerializer.Instance.Serialize(slot));
                File.SetLastWriteTimeUtc(payload, new DateTime(older ? 2024 : 2025, 1, 1));
                File.SetLastWriteTimeUtc(metadata, new DateTime(older ? 2024 : 2025, 1, 1));
            }
            Assert.That(storage.Load(Slot(1, "unknown")), Is.EqualTo(new byte[] { 2 }));
            var header = await storage.LoadSlotMetadataAsync(1);
            Assert.That(header.CustomMetadata["profile"], Is.EqualTo("newer"));
            Assert.That(File.Exists(Path.Combine(root, "Save_older1.sav")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "Slot1_older_Meta.bin")), Is.False);
        }

        [TestCase(false, false)] [TestCase(true, false)]
        [TestCase(false, true)] [TestCase(true, true)]
        public void MetadataFreeLegacyNamesRemainReadableAndDeletableWithoutHeaders(bool explicitSlot, bool recoveryOnly)
        {
            settings.saveFileName = explicitSlot ? "Save_{n}" : "Save_";
            Directory.CreateDirectory(root);
            string suffix = recoveryOnly ? ".previous" : "";
            string own = Path.Combine(root, "Save_1.sav");
            string other = Path.Combine(root, "Save_11.sav") + suffix;
            File.WriteAllBytes(own + suffix, new byte[] { 1 });
            File.WriteAllBytes(other, new byte[] { 11 });
            var slot = Slot(1, "unused");
            Assert.That(storage.Load(slot), Is.EqualTo(new byte[] { 1 }));
            storage.Delete(slot);
            Assert.That(new SaveSystem(settings, root).Load(slot), Is.Null);
            Assert.That(File.ReadAllBytes(other), Is.EqualTo(new byte[] { 11 }));
        }

        [TestCase(false)] [TestCase(true)]
        public void UnidentifiedSharedRootWildcardsAreNeitherLoadedNorDeleted(bool explicitSlot)
        {
            SetPattern(explicitSlot);
            Directory.CreateDirectory(root);
            string unknown = Path.Combine(root, explicitSlot ? "Save_unknown_1.sav" : "Save_unknown11.sav");
            File.WriteAllBytes(unknown, new byte[] { 42 });
            var requested = Slot(1, "requested");
            Assert.That(storage.Load(requested), Is.Null);
            storage.Save(new byte[] { 1 }, requested);
            storage.Delete(requested);
            Assert.That(File.ReadAllBytes(unknown), Is.EqualTo(new byte[] { 42 }), "No header established ownership of this wildcard match.");
        }
    }
}
#endif
