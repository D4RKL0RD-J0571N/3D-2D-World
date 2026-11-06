namespace Tests.Editor
{
    using Domain.World;
    using NUnit.Framework;
    using UnityEngine;

    /// <summary>
    /// Domain 12: validates the deterministic landscape height field that drives the ground mesh and prop scatter.
    /// </summary>
    [TestFixture]
    public class Domain12_WorldDesignTests
    {
        [Test]
        public void WLD01_VillageBasin_IsPerfectlyFlat()
        {
            for (float angle = 0f; angle < 360f; angle += 30f)
            {
                float rad = angle * Mathf.Deg2Rad;
                float x = WorldHeightField.Center.x + Mathf.Cos(rad) * (WorldHeightField.FlatRadius - 0.5f);
                float z = WorldHeightField.Center.y + Mathf.Sin(rad) * (WorldHeightField.FlatRadius - 0.5f);
                Assert.AreEqual(0f, WorldHeightField.Evaluate(x, z), 0.0001f, $"Basin not flat at {x:0.0},{z:0.0}");
            }
        }

        [Test]
        public void WLD02_HeightField_IsDeterministic()
        {
            Assert.AreEqual(WorldHeightField.Evaluate(63.2f, -41.7f), WorldHeightField.Evaluate(63.2f, -41.7f));
        }

        [Test]
        public void WLD03_OuterHills_RiseAboveVillageAndStayBounded()
        {
            float sum = 0f;
            int samples = 0;
            for (float angle = 0f; angle < 360f; angle += 15f)
            {
                float rad = angle * Mathf.Deg2Rad;
                float h = WorldHeightField.Evaluate(WorldHeightField.Center.x + Mathf.Cos(rad) * 85f, WorldHeightField.Center.y + Mathf.Sin(rad) * 85f);
                Assert.GreaterOrEqual(h, 0f);
                Assert.LessOrEqual(h, WorldHeightField.MaxHillHeight + 2f);
                sum += h;
                samples++;
            }
            Assert.Greater(sum / samples, 3f, "Outer ring should form noticeable hills.");
        }

        [Test]
        public void WLD04_IsInsideBasin_MatchesFlatRadius()
        {
            Assert.IsTrue(WorldHeightField.IsInsideBasin(0f, 5f));
            Assert.IsFalse(WorldHeightField.IsInsideBasin(0f, 5f + WorldHeightField.FlatRadius + 1f));
        }
    }
}
