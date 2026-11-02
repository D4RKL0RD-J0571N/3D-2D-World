namespace Tests.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Core;
    using Core.Content;
    using Core.Diagnostics;
    using Core.Ids;
    using Core.Persistence;
    using Data.Config;
    using Domain.Persistence;
    using Domain.Skyrim;
    using Gameplay.Entities;
    using Gameplay.Interactables;
    using NUnit.Framework;
    using Systems.Persistence;
    using Systems.Skyrim;
    using UnityEngine;

    /// <summary>
    /// Milestone 4 verification suite (`Domain 6: Persistence &amp; Voxel Compression Benchmarks`):
    /// - <c>SAV-01</c>: Bit-identical round-trip across <c>Raw</c>, <c>RLE16</c>, and <c>RLE16_Deflate</c>
    /// - <c>SAV-02</c>: Quantitative compression ratio (&gt;= 80% vs raw binary, &gt;= 95% vs JsonUtility) &amp; latency benchmark
    /// - <c>SAV-03</c>: IEEE 802.3 CRC-32 corruption detection (<see cref="SavePayloadCorruptedException"/>)
    /// - <c>SAV-04</c>: Full <see cref="SaveEnvelope"/> round-trip (Skills, Factions, Quests, Chest, Lever, Voxels)
    /// - <c>SAV-05</c>: Missing mod resilience via <see cref="UnknownContentPlaceholder"/> tombstones
    /// </summary>
    [TestFixture]
    public class Domain6_PersistenceAndVoxelCodecTests
    {
        private string _tempSaveDir;
        private ContentService _contentService;
        private DiagnosticService _diagnostics;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            GameEventManager.Clear();
            PersistentEntityComponent.ClearRegistry();

            _tempSaveDir = Path.Combine(Path.GetTempPath(), "FantasyEngine_SaveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempSaveDir);

            _diagnostics = new DiagnosticService();
            ServiceLocator.Register<IDiagnosticService, DiagnosticService>(_diagnostics);

            _contentService = new ContentService();
            ServiceLocator.Register<IContentService, ContentService>(_contentService);
        }

        [TearDown]
        public void TearDown()
        {
            PersistentEntityComponent.ClearRegistry();
            ServiceLocator.Clear();
            GameEventManager.Clear();

            if (!string.IsNullOrEmpty(_tempSaveDir) && Directory.Exists(_tempSaveDir))
            {
                try
                {
                    Directory.Delete(_tempSaveDir, recursive: true);
                }
                catch
                {
                    // Ignore transient OS temp file locks
                }
            }
        }

        #region SAV-01: Bit-Identical Round-Trip Across All Codecs

        [Test]
        public void SAV01_VoxelPayloadCodec_RoundTrip_BitIdenticalAcrossAllCodecs()
        {
            List<VoxelChunkPayload> structuredDungeon = GenerateStructured20RoomDungeon();
            List<VoxelChunkPayload> noisyChunks = GenerateHighEntropyNoisyChunk();

            VoxelCompressionCodec[] codecs =
            {
                VoxelCompressionCodec.Raw,
                VoxelCompressionCodec.RLE16,
                VoxelCompressionCodec.RLE16_Deflate
            };

            foreach (VoxelCompressionCodec codec in codecs)
            {
                // 1. Structured 20-room dungeon (92,160 voxels)
                byte[] encodedDungeon = VoxelPayloadCodec.Encode(structuredDungeon, codec);
                List<VoxelChunkPayload> decodedDungeon = VoxelPayloadCodec.Decode(encodedDungeon, out VoxelCompressionCodec readCodec);

                Assert.AreEqual(codec, readCodec);
                Assert.AreEqual(structuredDungeon.Count, decodedDungeon.Count);

                for (int i = 0; i < structuredDungeon.Count; i++)
                {
                    Assert.AreEqual(structuredDungeon[i].WorldPosition, decodedDungeon[i].WorldPosition);
                    Assert.AreEqual(structuredDungeon[i].Size, decodedDungeon[i].Size);
                    CollectionAssert.AreEqual(structuredDungeon[i].Voxels, decodedDungeon[i].Voxels, $"Mismatch in chunk {i} with codec {codec}");
                }

                // 2. High-entropy noisy chunk
                byte[] encodedNoisy = VoxelPayloadCodec.Encode(noisyChunks, codec);
                List<VoxelChunkPayload> decodedNoisy = VoxelPayloadCodec.Decode(encodedNoisy);
                Assert.AreEqual(1, decodedNoisy.Count);
                CollectionAssert.AreEqual(noisyChunks[0].Voxels, decodedNoisy[0].Voxels, $"Noisy chunk mismatch with codec {codec}");
            }
        }

        #endregion

        #region SAV-02: Voxel Compression Size & Latency Benchmark

        [Test]
        public void SAV02_VoxelPayloadCodec_Benchmark_MeetsSizeAndLatencyTargets()
        {
            List<VoxelChunkPayload> dungeon20Rooms = GenerateStructured20RoomDungeon();

            // Baseline 1: Raw binary size (92,160 * 2 = 184,320 bytes)
            int rawVoxelBytes = 20 * (24 * 8 * 24) * sizeof(ushort);
            Assert.AreEqual(184_320, rawVoxelBytes);

            // Baseline 2: Legacy JsonUtilitypretty-printed JSON size
            var legacyWrapper = new LevelSaveData
            {
                Grids = new RoomSaveData[dungeon20Rooms.Count]
            };
            for (int i = 0; i < dungeon20Rooms.Count; i++)
            {
                legacyWrapper.Grids[i] = new RoomSaveData
                {
                    WorldPosition = dungeon20Rooms[i].WorldPosition,
                    Size = dungeon20Rooms[i].Size,
                    VoxelData = dungeon20Rooms[i].Voxels
                };
            }
            string legacyJson = JsonUtility.ToJson(legacyWrapper, true);
            int legacyJsonBytes = System.Text.Encoding.UTF8.GetByteCount(legacyJson);

            // Codec 0: Raw .vxlbin
            byte[] rawBin = VoxelPayloadCodec.Encode(dungeon20Rooms, VoxelCompressionCodec.Raw);

            // Codec 1: RLE16 .vxlbin
            var swEncodeRle = Stopwatch.StartNew();
            byte[] rleBin = VoxelPayloadCodec.Encode(dungeon20Rooms, VoxelCompressionCodec.RLE16);
            swEncodeRle.Stop();

            var swDecodeRle = Stopwatch.StartNew();
            _ = VoxelPayloadCodec.Decode(rleBin);
            swDecodeRle.Stop();

            // Codec 2: RLE16_Deflate .vxlbin
            var swEncodeDeflate = Stopwatch.StartNew();
            byte[] deflateBin = VoxelPayloadCodec.Encode(dungeon20Rooms, VoxelCompressionCodec.RLE16_Deflate);
            swEncodeDeflate.Stop();

            var swDecodeDeflate = Stopwatch.StartNew();
            _ = VoxelPayloadCodec.Decode(deflateBin);
            swDecodeDeflate.Stop();

            UnityEngine.Debug.Log(
                $"[SAV-02 Benchmark] 20-Room Dungeon (92,160 voxels): " +
                $"JsonUtility={legacyJsonBytes / 1024f:F1} KB | " +
                $"RawBin={rawBin.Length / 1024f:F1} KB | " +
                $"RLE16={rleBin.Length / 1024f:F1} KB (enc {swEncodeRle.Elapsed.TotalMilliseconds:F2}ms, dec {swDecodeRle.Elapsed.TotalMilliseconds:F2}ms) | " +
                $"RLE16_Deflate={deflateBin.Length / 1024f:F1} KB (enc {swEncodeDeflate.Elapsed.TotalMilliseconds:F2}ms, dec {swDecodeDeflate.Elapsed.TotalMilliseconds:F2}ms)");

            // Assertions from ADR-009 & SAV-02:
            Assert.Less(rleBin.Length, 36 * 1024, "RLE16 must reduce 184.3 KB dungeon below 36 KB (>= 80% reduction).");
            Assert.Less(deflateBin.Length, rleBin.Length, "RLE16_Deflate must compress further than RLE16 alone.");
            Assert.Less(deflateBin.Length, legacyJsonBytes * 0.05, "RLE16_Deflate must reduce size by >= 95% compared to JsonUtility.");
            Assert.Less(swDecodeRle.Elapsed.TotalMilliseconds, 25.0, "RLE16 decode must complete rapidly.");
            Assert.Less(swDecodeDeflate.Elapsed.TotalMilliseconds, 25.0, "RLE16_Deflate decode must complete rapidly.");
        }

        #endregion

        #region SAV-03: Corrupted Byte Checksum Detection

        [Test]
        public void SAV03_VoxelPayloadCodec_CorruptedByte_ThrowsChecksumMismatchException()
        {
            List<VoxelChunkPayload> dungeon = GenerateStructured20RoomDungeon();
            byte[] validBuffer = VoxelPayloadCodec.Encode(dungeon, VoxelCompressionCodec.RLE16_Deflate);

            // Flip 1 bit inside the payload body (past the 24-byte header)
            int bodyIndex = VoxelPayloadCodec.HeaderByteSize + (validBuffer.Length - VoxelPayloadCodec.HeaderByteSize) / 2;
            validBuffer[bodyIndex] ^= 0x01;

            var ex = Assert.Throws<SavePayloadCorruptedException>(() => VoxelPayloadCodec.Decode(validBuffer));
            Assert.AreNotEqual(ex.ExpectedCrc32, ex.ActualCrc32);
        }

        #endregion

        #region SAV-04: Full SaveEnvelope Round-Trip

        [Test]
        public void SAV04_SaveEnvelope_FullRoundTrip_RestoresSkillsFactionsQuestsAndEntities()
        {
            var skills = new SkillProgressionService();
            var factions = new FactionService();
            var quests = new RadiantQuestService();
            var saveService = new FileSaveService { SaveDirectory = _tempSaveDir };

            ServiceLocator.Register(skills);
            ServiceLocator.Register(factions);
            ServiceLocator.Register(quests);
            ServiceLocator.Register(saveService);

            skills.Initialize();
            factions.Initialize();
            quests.Initialize();
            saveService.Initialize();

            // Create persistent Chest and Lever in the world
            var chestGo = new GameObject("TestDungeonChest");
            var chestPec = chestGo.AddComponent<PersistentEntityComponent>();
            var chest = chestGo.AddComponent<ChestInteractable>();
            InstanceGuid chestGuid = chestPec.InstanceId;
            chest.ConfigureChest(LockDifficulty.Master, isOpened: false);

            var leverGo = new GameObject("TestDungeonLever");
            var leverPec = leverGo.AddComponent<PersistentEntityComponent>();
            var lever = leverGo.AddComponent<LeverInteractable>();
            InstanceGuid leverGuid = leverPec.InstanceId;

            List<VoxelChunkPayload> savedChunks = GenerateStructured20RoomDungeon();
            List<VoxelChunkPayload> restoredChunks = null;
            saveService.RegisterVoxelProvider(
                () => savedChunks,
                chunks => restoredChunks = new List<VoxelChunkPayload>(chunks));

            try
            {
                // 1. Mutate session state across all services & entities
                skills.RestoreSkillState(SkillProgressionService.OneHandedSkillId, level: 28, xp: 140f);
                factions.SetReputation(FactionService.GuardsFactionId, 65);
                factions.AddBounty(FactionService.BanditsFactionId, 500);

                QuestInstance bountyQuest = quests.GenerateQuest(QuestType.BountyHunt, "Bleak Falls Barrow");
                quests.AcceptQuest(bountyQuest);

                chest.ConfigureChest(LockDifficulty.Unlocked, isOpened: true);
                lever.SetActivated(true);

                // 2. Save to disk ("slot_roundtrip")
                SaveEnvelope writtenEnvelope = saveService.SaveSlot("slot_roundtrip");
                Assert.IsNotNull(writtenEnvelope);
                Assert.IsTrue(File.Exists(saveService.GetSlotJsonPath("slot_roundtrip")));

                // 3. Reset all services & world components to default values
                skills.RestoreSkillState(SkillProgressionService.OneHandedSkillId, level: 15, xp: 0f);
                factions.SetReputation(FactionService.GuardsFactionId, 0);
                factions.ClearBounty(FactionService.BanditsFactionId);
                quests.Shutdown();
                quests.Initialize();

                chest.ConfigureChest(LockDifficulty.Master, isOpened: false);
                lever.SetActivated(false);

                Assert.AreEqual(15, skills.GetSkillLevel(SkyrimSkill.OneHanded));
                Assert.AreEqual(0, factions.GetReputation(FactionService.GuardsFactionId));
                Assert.AreEqual(0, quests.ActiveQuests.Count);
                Assert.IsTrue(chest.IsLocked);
                Assert.IsFalse(chest.IsOpened);
                Assert.IsFalse(lever.IsActivated);

                // 4. Load from disk ("slot_roundtrip")
                SaveEnvelope loadedEnvelope = saveService.LoadSlot("slot_roundtrip");
                Assert.IsNotNull(loadedEnvelope);

                // 5. Verify 100% state restoration
                Assert.AreEqual(28, skills.GetSkillLevel(SkyrimSkill.OneHanded));
                Assert.AreEqual(65, factions.GetReputation(FactionService.GuardsFactionId));
                Assert.AreEqual(500, factions.GetBounty(FactionService.BanditsFactionId));
                Assert.AreEqual(1, quests.ActiveQuests.Count);
                Assert.AreEqual("Bleak Falls Barrow", quests.ActiveQuests[0].LocationName);

                Assert.AreEqual(chestGuid, chestPec.InstanceId);
                Assert.IsFalse(chest.IsLocked, "Chest should be restored to Unlocked.");
                Assert.IsTrue(chest.IsOpened, "Chest should be restored to Opened.");

                Assert.AreEqual(leverGuid, leverPec.InstanceId);
                Assert.IsTrue(lever.IsActivated, "Lever should be restored to Activated.");

                Assert.IsNotNull(restoredChunks);
                Assert.AreEqual(20, restoredChunks.Count);
                CollectionAssert.AreEqual(savedChunks[0].Voxels, restoredChunks[0].Voxels);
            }
            finally
            {
                saveService.Shutdown();
                quests.Shutdown();
                factions.Shutdown();
                skills.Shutdown();
                UnityEngine.Object.DestroyImmediate(chestGo);
                UnityEngine.Object.DestroyImmediate(leverGo);
            }
        }

        #endregion

        #region SAV-05: Missing Mod Resilience & UnknownContentPlaceholders

        [Test]
        public void SAV05_SaveEnvelope_LoadWithRemovedMod_PreservesUnknownPlaceholdersWithoutCrash()
        {
            var saveService = new FileSaveService { SaveDirectory = _tempSaveDir };
            ServiceLocator.Register(saveService);
            saveService.Initialize();

            // Create a Player with an InventoryComponent
            var playerGo = new GameObject("TestPlayer");
            var playerPec = playerGo.AddComponent<PersistentEntityComponent>();
            var playerInv = playerGo.AddComponent<InventoryComponent>();

            // Register 1 valid base item and 1 temporary mod item
            var ironSword = ScriptableObject.CreateInstance<ItemData>();
            ironSword.Id = "ITM_0001";
            ironSword.DisplayName = "Iron Sword";
            ironSword.ContentId = new ResourceId("nordic_rpg", "item/iron_sword");

            var dragonHalberd = ScriptableObject.CreateInstance<ItemData>();
            dragonHalberd.Id = "ITM_9999";
            dragonHalberd.DisplayName = "Dragon Halberd";
            dragonHalberd.ContentId = new ResourceId("deleted_mod", "item/dragon_halberd");

            // Also create a modded entity in the scene from 'deleted_mod'
            var fireWyrmGo = new GameObject("ModdedFireWyrm");
            var fireWyrmPec = fireWyrmGo.AddComponent<PersistentEntityComponent>();
            fireWyrmPec.DefinitionId = new ResourceId("deleted_mod", "actor/fire_wyrm");
            fireWyrmPec.IsDynamicallySpawned = true;

            try
            {
                _contentService.GetRegistry<ItemData>().Register(ironSword, "nordic_rpg");
                _contentService.GetRegistry<ItemData>().Register(dragonHalberd, "deleted_mod");

                playerInv.AddItem(ironSword, 1);
                playerInv.AddItem(dragonHalberd, 2);

                // 1. Save while 'deleted_mod' is installed
                saveService.SaveSlot("slot_missing_mod");

                // 2. Simulate uninstalling 'deleted_mod':
                //    Destroy the modded FireWyrm object and rebuild ContentService with ONLY 'nordic_rpg'
                UnityEngine.Object.DestroyImmediate(fireWyrmGo);
                fireWyrmGo = null;

                _contentService = new ContentService();
                ServiceLocator.Register<IContentService, ContentService>(_contentService);
                _contentService.GetRegistry<ItemData>().Register(ironSword, "nordic_rpg");

                // Clear player inventory before loading
                playerInv.RemoveItem(ironSword, 1);
                playerInv.RemoveItem(dragonHalberd, 2);
                _diagnostics.ClearEntries();

                // 3. Load save after 'deleted_mod' was removed — must NOT throw!
                SaveEnvelope loaded = saveService.LoadSlot("slot_missing_mod");
                Assert.IsNotNull(loaded);

                // Base item ('iron_sword') is restored normally
                Assert.AreEqual(1, playerInv.GetItemCount(ironSword));

                // Missing mod item ('deleted_mod:item/dragon_halberd') and entity ('deleted_mod:actor/fire_wyrm')
                // are preserved inside UnknownContentPlaceholders!
                Assert.AreEqual(1, playerInv.UnresolvedSlots.Count);
                Assert.AreEqual(new ResourceId("deleted_mod", "item/dragon_halberd"), playerInv.UnresolvedSlots[0].MissingId);
                Assert.AreEqual(2, playerInv.UnresolvedSlots[0].Count);

                Assert.GreaterOrEqual(loaded.UnknownContentPlaceholders.Count, 2, "Both the missing entity and missing item must be recorded in UnknownContentPlaceholders.");
                Assert.GreaterOrEqual(_diagnostics.WarningCount, 2, "DiagnosticService must record warnings for the unresolved mod content.");
            }
            finally
            {
                saveService.Shutdown();
                if (fireWyrmGo != null) UnityEngine.Object.DestroyImmediate(fireWyrmGo);
                UnityEngine.Object.DestroyImmediate(playerGo);
                UnityEngine.Object.DestroyImmediate(ironSword);
                UnityEngine.Object.DestroyImmediate(dragonHalberd);
            }
        }

        #endregion

        #region Helpers

        private static List<VoxelChunkPayload> GenerateStructured20RoomDungeon()
        {
            const int roomCount = 20;
            var size = new Vector3Int(24, 8, 24); // 4,608 voxels per room -> 92,160 voxels total (184,320 bytes)
            var chunks = new List<VoxelChunkPayload>(roomCount);

            for (int r = 0; r < roomCount; r++)
            {
                var voxels = new ushort[size.x * size.y * size.z];
                int idx = 0;
                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        for (int z = 0; z < size.z; z++)
                        {
                            // Floor at y=0, ceiling at y=7, perimeter walls, air inside
                            if (y == 0) voxels[idx] = 1;
                            else if (y == size.y - 1) voxels[idx] = 3;
                            else if (x == 0 || x == size.x - 1 || z == 0 || z == size.z - 1) voxels[idx] = 2;
                            else voxels[idx] = 0;
                            idx++;
                        }
                    }
                }

                chunks.Add(new VoxelChunkPayload(new Vector3(r * 30f, 0f, 0f), size, voxels));
            }

            return chunks;
        }

        private static List<VoxelChunkPayload> GenerateHighEntropyNoisyChunk()
        {
            var size = new Vector3Int(16, 8, 16);
            var voxels = new ushort[size.x * size.y * size.z];
            var rng = new System.Random(1337);

            for (int i = 0; i < voxels.Length; i++)
            {
                voxels[i] = (ushort)rng.Next(0, 64);
            }

            return new List<VoxelChunkPayload>
            {
                new VoxelChunkPayload(Vector3.zero, size, voxels)
            };
        }

        #endregion
    }
}
