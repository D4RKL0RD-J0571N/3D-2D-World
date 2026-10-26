#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed class SaveSettingsStorageFeatureTests
    {
        private string root;
        private SaveSettings settings;
        private TestSecret secret;
        [SetUp] public void SetUp()
        {
            root=Path.Combine(Path.GetTempPath(),"CrystalSaveTests",Guid.NewGuid().ToString("N"));
            settings=ScriptableObject.CreateInstance<SaveSettings>();
            settings.enableCloudSave=false; settings.enableScreenshots=false;
            settings.saveKey="CrystalSaveTests_"+Guid.NewGuid().ToString("N")+"_{n}";
            secret=ScriptableObject.CreateInstance<TestSecret>(); settings.masterSecretProvider=secret;
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(settings); Object.DestroyImmediate(secret);
            if(Directory.Exists(root)) Directory.Delete(root,true);
        }

        [TestCase(SaveMethod.BinaryFileFormat,false,false)] [TestCase(SaveMethod.BinaryFileFormat,true,false)]
        [TestCase(SaveMethod.BinaryFileFormat,false,true)] [TestCase(SaveMethod.BinaryFileFormat,true,true)]
        [TestCase(SaveMethod.PlayerPrefs,false,false)] [TestCase(SaveMethod.PlayerPrefs,true,false)]
        [TestCase(SaveMethod.PlayerPrefs,false,true)] [TestCase(SaveMethod.PlayerPrefs,true,true)]
        public async Task LocalBackendsRoundTripCompressionAndEncryption(SaveMethod backend,bool compress,bool encrypt)
        {
            settings.saveMethod=backend; settings.enableCompression=compress; settings.enableEncryption=encrypt;
            var compression=new CompressionService(settings); await compression.InitializeAsync();
            var encryption=new EncryptionService(settings); await encryption.InitializeAsync("test-user");
            var system=new SaveSystem(settings,root); var slot=new SaveSlot(3,"Test",DateTime.UtcNow,"","Area");
            var plain=Enumerable.Repeat((byte)42,2048).ToArray();
            try
            {
                var encoded=encryption.MaybeEncrypt(compression.MaybeCompress(plain));
                system.Save(encoded,slot);
                var loaded=system.Load(slot);
                Assert.That(compression.MaybeDecompress(encryption.MaybeDecrypt(loaded)), Is.EqualTo(plain));
                if(compress || encrypt) Assert.That(loaded, Is.Not.EqualTo(plain));
            }
            finally { system.Delete(slot); }
        }

        [TestCase(SaveMethod.BinaryFileFormat)] [TestCase(SaveMethod.PlayerPrefs)]
        public void VerificationBackupRestoresPreviousBytesAndCanBeDeleted(SaveMethod backend)
        {
            settings.saveMethod=backend; settings.enableSaveFileVerification=true;
            var system=new SaveSystem(settings,root); var slot=new SaveSlot(3,"Test",DateTime.UtcNow,"","Area");
            try
            {
                system.Save(new byte[]{1,2,3},slot); system.BackupSlot(slot);
                system.Save(new byte[]{9},slot); system.RestoreBackup(slot);
                Assert.That(system.Load(slot),Is.EqualTo(new byte[]{1,2,3}));
                system.DeleteBackup(slot);
                system.Save(new byte[]{4},slot); system.RestoreBackup(slot);
                Assert.That(system.Load(slot),Is.EqualTo(new byte[]{4}));
            }
            finally
            {
                system.DeleteBackup(slot); system.Delete(slot);
                PlayerPrefs.DeleteKey(settings.saveKey.Replace("{n}","3"));
                PlayerPrefs.DeleteKey(settings.saveKey.Replace("{n}","3")+"_bak");
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void PlayerPrefsSyncApiUsesConfiguredSlotKey(bool placeholder)
        {
            if(!placeholder) settings.saveKey=settings.saveKey.Replace("{n}","");
            settings.saveMethod=SaveMethod.PlayerPrefs; var system=new SaveSystem(settings,root);
            var slot=new SaveSlot(3,"Test",DateTime.UtcNow,"","Area");
            string key=placeholder ? settings.saveKey.Replace("{n}","3") : settings.saveKey+"3";
            try
            {
                system.Save(new byte[]{5},slot);
                Assert.That(PlayerPrefs.HasKey(key),Is.True);
                Assert.That(Convert.FromBase64String(PlayerPrefs.GetString(key)),Is.EqualTo(new byte[]{5}));
                Assert.That(system.Load(slot),Is.EqualTo(new byte[]{5}));
                system.Delete(slot);
                Assert.That(PlayerPrefs.HasKey(key),Is.False);
            }
            finally { system.Delete(slot); PlayerPrefs.DeleteKey(key); }
        }

        [Test]
        public void LegacyPlayerPrefsPlaceholderSavesRemainReadableAndDeleteRemovesBothKeys()
        {
            settings.saveMethod=SaveMethod.PlayerPrefs; var system=new SaveSystem(settings,root);
            var slot=new SaveSlot(3,"Legacy",DateTime.UtcNow,"","Area");
            string legacy=settings.saveKey+3; string current=settings.saveKey.Replace("{n}","3");
            try
            {
                PlayerPrefs.SetString(legacy,Convert.ToBase64String(new byte[]{7}));
                Assert.That(system.Load(slot),Is.EqualTo(new byte[]{7}));
                system.Save(new byte[]{8},slot);
                Assert.That(system.Load(slot),Is.EqualTo(new byte[]{8}),"Current key takes precedence.");
                system.Delete(slot);
                Assert.That(PlayerPrefs.HasKey(legacy) || PlayerPrefs.HasKey(current),Is.False);
            }
            finally { PlayerPrefs.DeleteKey(legacy); PlayerPrefs.DeleteKey(current); PlayerPrefs.Save(); }
        }

        [TestCase(true)] [TestCase(false)]
        public async Task EncryptionUserBindingHonorsSaveSettings(bool bindUser)
        {
            settings.enableEncryption=true; settings.useUserIdForEncryption=bindUser;
            var a=new EncryptionService(settings); var b=new EncryptionService(settings);
            await a.InitializeAsync("Alice"); await b.InitializeAsync("Bob");
            var payload=a.MaybeEncrypt(new byte[]{1,2,3});
            if(bindUser)
            {
                // Test the authenticated primitive directly: rejection is expected, not a Unity error log.
                var key=SaveCrypto.DeriveKey("Bob",await secret.GetMasterSecretAsync());
                Assert.That(SaveCrypto.TryDecrypt(payload,key,5,out _), Is.False);
            }
            else Assert.That(b.MaybeDecrypt(payload), Is.EqualTo(new byte[]{1,2,3}));
        }

        [Test]
        public async Task AuthenticatedEncryptionRejectsTamperingAndUsesFreshNonce()
        {
            settings.enableEncryption=true; var e=new EncryptionService(settings); await e.InitializeAsync("user");
            var plain=new byte[]{4,5,6}; var a=e.MaybeEncrypt(plain); var b=e.MaybeEncrypt(plain);
            Assert.That(a, Is.Not.EqualTo(b)); a[a.Length-1]^=1;
            var key=SaveCrypto.DeriveKey("user",await secret.GetMasterSecretAsync());
            Assert.That(SaveCrypto.TryDecrypt(a,key,5,out _), Is.False);
        }

        [Test]
        public async Task CompressionLoadsOlderCompressedSaveAfterToggleIsDisabled()
        {
            settings.enableCompression=true; var on=new CompressionService(settings); await on.InitializeAsync();
            var payload=on.MaybeCompress(new byte[]{1,2,3});
            settings.enableCompression=false; var off=new CompressionService(settings); await off.InitializeAsync();
            Assert.That(off.MaybeDecompress(payload), Is.EqualTo(new byte[]{1,2,3}));
            Assert.That(off.MaybeDecompress(new byte[]{9}), Is.EqualTo(new byte[]{9}));
        }
    }
    internal sealed class TestSecret : ScriptableObject, IMasterSecretProvider
    {
        public ValueTask<byte[]> GetMasterSecretAsync() => new ValueTask<byte[]>(Enumerable.Range(1,32).Select(i=>(byte)i).ToArray());
    }
}
#endif
