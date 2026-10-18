namespace Tests.Editor
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEngine;
    using Domain.LevelGeneration;

    [TestFixture]
    public class RoomGraphGeneratorTests
    {
        [Test]
        public void Generate_WithSameSeed_ProducesIdenticalLayout()
        {
            // Arrange
            var generator = new RoomGraphGenerator();
            var parameters1 = new LevelGenerationParameters
            {
                Seed = 42,
                MainPathRooms = 6,
                BranchRooms = 3,
                CycleChance = 0.3f
            };
            var parameters2 = new LevelGenerationParameters
            {
                Seed = 42,
                MainPathRooms = 6,
                BranchRooms = 3,
                CycleChance = 0.3f
            };

            // Act
            var layout1 = generator.Generate(parameters1);
            var layout2 = generator.Generate(parameters2);

            // Assert
            Assert.IsNotNull(layout1);
            Assert.IsNotNull(layout2);
            Assert.AreEqual(layout1.Rooms.Count, layout2.Rooms.Count, "Room counts must match for identical seed.");
            Assert.AreEqual(layout1.Connections.Count, layout2.Connections.Count, "Connection counts must match for identical seed.");

            for (int i = 0; i < layout1.Rooms.Count; i++)
            {
                var r1 = layout1.Rooms[i];
                var r2 = layout2.Rooms[i];

                Assert.AreEqual(r1.Id, r2.Id, $"Room {i} Id mismatch.");
                Assert.AreEqual(r1.GridPosition, r2.GridPosition, $"Room {i} GridPosition mismatch.");
                Assert.AreEqual(r1.FloorLevel, r2.FloorLevel, $"Room {i} FloorLevel mismatch.");
                Assert.AreEqual(r1.Size, r2.Size, $"Room {i} Size mismatch.");
                Assert.AreEqual(r1.IsExit, r2.IsExit, $"Room {i} IsExit mismatch.");
            }
        }

        [Test]
        public void Generate_CreatesValidConnectedGraphWithStartAndExit()
        {
            // Arrange
            var generator = new RoomGraphGenerator();
            var parameters = new LevelGenerationParameters
            {
                Seed = 1337,
                MainPathRooms = 8,
                BranchRooms = 4,
                CycleChance = 0.25f
            };

            // Act
            var layout = generator.Generate(parameters);

            // Assert
            Assert.IsNotNull(layout.StartRoom, "Start room must be assigned.");
            Assert.AreEqual(Vector2Int.zero, layout.StartRoom.GridPosition, "Start room must be at (0,0).");

            int exitCount = 0;
            var positions = new HashSet<Vector2Int>();

            foreach (var room in layout.Rooms)
            {
                Assert.IsFalse(positions.Contains(room.GridPosition), $"Duplicate room position at {room.GridPosition}");
                positions.Add(room.GridPosition);

                if (room.IsExit) exitCount++;
            }

            Assert.AreEqual(1, exitCount, "Exactly one exit room must be designated.");
            Assert.GreaterOrEqual(layout.Connections.Count, layout.Rooms.Count - 1, "Graph must have at least N-1 connections.");
        }
    }
}
