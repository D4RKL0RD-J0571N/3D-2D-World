#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Arawn.CrystalSave.EditorTests
{
	public class CrystalSaveSaveSystemIsolationTests
	{
		private string rootPath;
		private SaveSettings settings;
		private SaveSystem saveSystem;

		[SetUp]
		public void SetUp()
		{
			rootPath = Path.Combine(
				Path.GetTempPath(),
				"CrystalSaveTests",
				"SaveSystemIsolation_" + Guid.NewGuid().ToString("N"));

			settings = ScriptableObject.CreateInstance<SaveSettings>();
			settings.saveMethod = SaveMethod.BinaryFileFormat;
			settings.saveFileName = "Slot_{n}";
			settings.saveKey = "Slot_{n}";
			settings.slotFolderNamePattern = "slot{n}";
			settings.metadataFileNamePattern = "Slot{n}_Meta.bin";
			settings.enableCloudSave = false;
			settings.keepLocalMirror = true;

			saveSystem = new SaveSystem(settings, rootPath);
		}

		[TearDown]
		public void TearDown()
		{
			if (settings != null)
				UnityEngine.Object.DestroyImmediate(settings);

			DeleteTestRoot(rootPath);
		}

		[Test]
		public void BinarySaveSystemRoundTripsBytesInsideIsolatedRoot()
		{
			SaveSlot slot = CreateSlot(1);
			byte[] expected = { 1, 2, 3, 5, 8, 13 };

			saveSystem.Save(expected, slot);

			byte[] actual = saveSystem.Load(slot);

			CollectionAssert.AreEqual(expected, actual);
			Assert.IsTrue(File.Exists(Path.Combine(rootPath, "slot1", "Slot_1.sav")));
			Assert.IsTrue(File.Exists(Path.Combine(rootPath, "slot1", "Slot1_Meta.bin")));
		}

		[Test]
		public async Task BinarySaveSystemRoundTripsSlotMetadataInsideIsolatedRoot()
		{
			SaveSlot slot = CreateSlot(2);
			slot.CustomMetadata["profile"] = "qa";
			slot.CustomMetadata["branch"] = "tests";

			await saveSystem.SaveSlotMetadataAsync(slot);

			SaveSlot loaded = await saveSystem.LoadSlotMetadataAsync(slot.SlotNumber);

			Assert.IsNotNull(loaded);
			Assert.AreEqual(slot.SlotNumber, loaded.SlotNumber);
			Assert.AreEqual(slot.SlotName, loaded.SlotName);
			Assert.AreEqual(slot.LastActiveScene, loaded.LastActiveScene);
			Assert.AreEqual("qa", loaded.CustomMetadata["profile"]);
			Assert.AreEqual("tests", loaded.CustomMetadata["branch"]);
			Assert.IsTrue(File.Exists(Path.Combine(rootPath, "slot2", "Slot2_Meta.bin")));
		}

		[Test]
		public void BinarySaveSystemDeleteRemovesSavedBytesAndMetadata()
		{
			SaveSlot slot = CreateSlot(3);
			byte[] payload = { 21, 34, 55 };

			saveSystem.Save(payload, slot);
			Assert.IsNotNull(saveSystem.Load(slot));

			saveSystem.Delete(slot);

			Assert.IsNull(saveSystem.Load(slot));
			Assert.IsFalse(File.Exists(Path.Combine(rootPath, "slot3", "Slot_3.sav")));
			Assert.IsFalse(File.Exists(Path.Combine(rootPath, "slot3", "Slot3_Meta.bin")));
		}

		[Test]
		public void BinarySaveSystemRemovesStaleMetadataNamedSaveVariants()
		{
			settings.saveFileName = "Slot_{n}_{meta:profile}";

			SaveSlot oldSlot = CreateSlot(4);
			oldSlot.CustomMetadata["profile"] = "old";
			SaveSlot newSlot = CreateSlot(4);
			newSlot.CustomMetadata["profile"] = "new";

			saveSystem.Save(new byte[] { 1 }, oldSlot);
			string oldPath = Path.Combine(rootPath, "slot4", "Slot_4_old.sav");
			Assert.IsTrue(File.Exists(oldPath));

			saveSystem.Save(new byte[] { 2 }, newSlot);
			string newPath = Path.Combine(rootPath, "slot4", "Slot_4_new.sav");

			Assert.IsFalse(File.Exists(oldPath), "Old metadata-named save variant should be removed.");
			Assert.IsTrue(File.Exists(newPath), "New metadata-named save variant should exist.");
			CollectionAssert.AreEqual(new byte[] { 2 }, saveSystem.Load(newSlot));
		}

		private static SaveSlot CreateSlot(int slotNumber)
		{
			return new SaveSlot(
				slotNumber,
				$"Test Slot {slotNumber}",
				new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc),
				string.Empty,
				"CrystalSaveTestScene");
		}

		private static void DeleteTestRoot(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;

			string fullPath = Path.GetFullPath(path);
			if (!fullPath.Contains("CrystalSaveTests", StringComparison.Ordinal))
				throw new InvalidOperationException("Refusing to delete a non-test Crystal Save path: " + fullPath);

			if (Directory.Exists(fullPath))
				Directory.Delete(fullPath, true);
		}
	}
}
#endif
