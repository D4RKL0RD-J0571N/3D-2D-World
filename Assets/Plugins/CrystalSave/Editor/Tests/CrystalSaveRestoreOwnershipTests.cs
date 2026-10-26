#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Reflection;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using static Arawn.CrystalSave.EditorTests.FeatureTestSupport;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class CrystalSaveRestoreOwnershipTests
    {
        [Test]
        public void ApplicationLifetimeBelongsToItsManagerWithoutASingleton()
        {
            using var settings = new FeatureSettingsScope();
            var first = new ComponentManager();
            var second = new ComponentManager();
            var firstHelper = new DeferredReceiver();
            var secondHelper = new DeferredReceiver();
            Call(first, "RestoreComponent", firstHelper, new byte[] { 11 }, firstHelper.UniqueIdentifier);
            Call(second, "RestoreComponent", secondHelper, new byte[] { 22 }, secondHelper.UniqueIdentifier);
            Call(second, "BeginFullLoadGeneration");
            Assert.That(firstHelper.IsCurrent, Is.True, "Another manager cannot retire this application.");
            Assert.That(secondHelper.IsCurrent, Is.False, "The originating manager must retire its ticket.");
            secondHelper.Complete();
            Assert.That(second.HasComponentDeserialized(secondHelper.UniqueIdentifier), Is.False);
            Assert.That(PendingBytes(first)[firstHelper.UniqueIdentifier], Is.EqualTo(new byte[] { 11 }));
            firstHelper.Complete();
            Assert.That(first.HasComponentDeserialized(firstHelper.UniqueIdentifier), Is.True);
            Assert.That(PendingBytes(first), Is.Empty);
            Assert.That(PendingBytes(second), Is.Empty);
        }

        [Test]
        public void SupersededInstanceCannotConsumeOrRetainReplacementBytes()
        {
            using var settings = new FeatureSettingsScope();
            var manager = new ComponentManager();
            var outgoing = new DeferredReceiver();
            var replacement = new DeferredReceiver();
            Call(manager, "RestoreComponent", outgoing, new byte[] { 11 }, outgoing.UniqueIdentifier);
            Call(manager, "RestoreComponent", replacement, new byte[] { 22 }, replacement.UniqueIdentifier);
            outgoing.Complete(new InvalidOperationException("Superseded application failed."));
            Assert.That(PendingBytes(manager)[replacement.UniqueIdentifier], Is.EqualTo(new byte[] { 22 }));
            Assert.That(manager.HasComponentDeserialized(replacement.UniqueIdentifier), Is.False);
            replacement.Complete();
            Assert.That(PendingBytes(manager), Is.Empty);
            Assert.That(manager.HasComponentDeserialized(replacement.UniqueIdentifier), Is.True);
        }

        sealed class DeferredReceiver : ISaveable
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object ticket;
            public string UniqueIdentifier => "isolated-object_component";

            public byte[] SaveData() => new byte[]
            {
                0
            };
            public void LoadData(byte[] data)
            {
                var scope = typeof(ComponentManager).Assembly.GetType("Arawn.CrystalSave.Runtime.ComponentOperationScope", true);
                ticket = Call(scope, "DeferApplication");
            }

            internal bool IsCurrent => (bool)ticket.GetType().GetProperty("IsCurrent", Flags).GetValue(ticket);

            internal void Complete(Exception error = null) => Call(ticket, "Complete", new object[] { error });
        }
    }
}
#endif

