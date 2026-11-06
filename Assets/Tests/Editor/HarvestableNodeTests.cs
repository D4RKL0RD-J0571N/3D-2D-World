namespace Tests.Editor
{
    using NUnit.Framework;
    using UnityEngine;
    using Domain.Combat;
    using Gameplay.Entities;

    [TestFixture]
    public class HarvestableNodeTests
    {
        private GameObject _nodeObject;
        private HarvestableNode _node;

        [SetUp]
        public void SetUp()
        {
            _nodeObject = new GameObject("TestTree");
            _nodeObject.AddComponent<BoxCollider>();
            _node = _nodeObject.AddComponent<HarvestableNode>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_nodeObject != null)
            {
                Object.DestroyImmediate(_nodeObject);
            }
        }

        [Test]
        public void TakeDamage_ReducesIntegrityCorrectly()
        {
            // Initial integrity is 50
            int initialIntegrity = _node.CurrentIntegrity;
            Assert.AreEqual(50, initialIntegrity);

            var dmg = new DamageInfo(15, DamageType.Physical);
            _node.TakeDamage(dmg);

            Assert.AreEqual(35, _node.CurrentIntegrity);
            Assert.IsFalse(_node.IsDepleted);
        }

        [Test]
        public void TakeDamage_UponDepletion_SetsIsDepletedAndDisablesCollider()
        {
            var col = _nodeObject.GetComponent<Collider>();
            Assert.IsTrue(col.enabled);

            // Deal lethal damage
            var lethalDmg = new DamageInfo(60, DamageType.Physical);
            _node.TakeDamage(lethalDmg);

            Assert.AreEqual(0, _node.CurrentIntegrity);
            Assert.IsTrue(_node.IsDepleted);
            Assert.IsFalse(col.enabled, "Collider must be disabled immediately to prevent duplicate hits.");

            // Repeated damage should do nothing
            _node.TakeDamage(new DamageInfo(25, DamageType.Physical));
            Assert.AreEqual(0, _node.CurrentIntegrity);
        }
    }
}
