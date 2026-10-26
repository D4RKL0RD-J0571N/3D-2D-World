#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SaveLogger = Arawn.CrystalSave.Runtime.Logger;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest] public IEnumerator DeletionDurability_AcknowledgementIncludesImageAndRecovery() => DeletionDurabilityScenario(false, true);
        [UnityTest] public IEnumerator DeletionDurability_RejectedFlushRestoresSlotAndImage() => DeletionDurabilityScenario(true, true);
        [UnityTest] public IEnumerator DeletionDurability_DisabledScreenshotsRetainImages() => DeletionDurabilityScenario(false, false);
        [UnityTest] public IEnumerator DeletionDurability_PlayerPrefsRejectedFlushRestoresKeysAndImage() => DeletionDurabilityScenario(true, true, true);
        [UnityTest] public IEnumerator DeletionDurability_PlayerPrefsAcknowledgementIncludesKeysAndImage() => DeletionDurabilityScenario(false, true, true);
#if !REMEMBERME_CLOUDSAVE_PRESENT
        [UnityTest] public IEnumerator DeletionDurability_BinaryFallbackRejectedFlushRestoresPayload() => DeletionDurabilityScenario(true, true, false, CloudSaveTransport.Binary);
        [UnityTest] public IEnumerator DeletionDurability_JsonFallbackRejectedFlushRestoresPayload() => DeletionDurabilityScenario(true, true, false, CloudSaveTransport.JSON);
#endif

        [UnityTest]
        public IEnumerator DeletionDurability_LocalDeleteCannotResurrectInterruptedPair()
        {
            foreach (string stage in new[] { "prepared", "payload", "metadata" })
            {
                var settings = ScriptableObject.CreateInstance<SaveSettings>();
                settings.enableCloudSave = settings.enableEncryption = settings.enableSaveFileVerification = settings.enableScreenshots = false;
                settings.saveMethod = SaveMethod.BinaryFileFormat;
                settings.saveKey = "DeletionJournal_" + Guid.NewGuid().ToString("N");
                string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
                var fault = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.SaveFileTransaction")
                    .GetField("FaultInjection", BindingFlags.Static | BindingFlags.NonPublic);
                object previousFault = fault.GetValue(null);
                var previousLog = SaveLogger.LogThreshold;
                try
                {
                    var storage = new SaveSystem(settings, root);
                    var own = new SaveSlot(1, "Delete", new DateTime(2025, 1, 1), null, "Scene");
                    var other = new SaveSlot(11, "Preserve", own.LastSaved, null, "Scene");
                    storage.Save(new byte[] { 1 }, own);
                    storage.Save(new byte[] { 11 }, other);
                    SaveLogger.LogThreshold = LogLevel.Off;
                    fault.SetValue(null, (Action<string>)(boundary => { if (boundary == stage) throw new IOException("Interrupted pair commit"); }));
                    Assert.Throws<IOException>(() => storage.Save(new byte[] { 2 }, own));
                    fault.SetValue(null, previousFault);
                    Assert.That(Directory.Exists(Path.Combine(root, "1.csav-journal")), Is.True);
                    yield return Await(storage.DeleteLocalSlotAsync(own));
                    Assert.That(Directory.Exists(Path.Combine(root, "1.csav-journal")), Is.False);
                    var restarted = new SaveSystem(settings, root);
                    Assert.That(restarted.Load(own), Is.Null, stage);
                    Assert.That(restarted.Load(other), Is.EqualTo(new byte[] { 11 }));
                }
                finally
                {
                    fault.SetValue(null, previousFault);
                    SaveLogger.LogThreshold = previousLog;
                    UnityEngine.Object.DestroyImmediate(settings);
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            }
        }

        IEnumerator DeletionDurabilityScenario(bool reject, bool screenshots, bool preferences = false, CloudSaveTransport? fallback = null)
        {
            foreach (bool localOnly in new[] { false, true })
            {
                var settings = ScriptableObject.CreateInstance<SaveSettings>();
                settings.enableCloudSave = settings.enableEncryption = settings.enableSaveFileVerification = false;
                settings.saveMethod = preferences ? SaveMethod.PlayerPrefs : SaveMethod.BinaryFileFormat;
                settings.enableScreenshots = screenshots;
                settings.saveFileName = "Delete{n}";
                settings.metadataFileNamePattern = "Slot{n}_Meta.bin";
                settings.saveKey = "DeletionDurability_" + Guid.NewGuid().ToString("N") + "_{n}";
                string root = Path.Combine(Path.GetTempPath(), "CrystalSaveTests", Guid.NewGuid().ToString("N"));
                var storage = new SaveSystem(settings, root);
                var slot = new SaveSlot(1, "Delete", new DateTime(2025, 1, 1), "owned.png", "Scene");
                string payload = Path.Combine(root, settings.ResolveSlotFolderName(1), "Delete1.sav");
                string metadata = Path.Combine(root, settings.ResolveSlotFolderName(1), "Slot1_Meta.bin");
                string image = Path.Combine(root, settings.screenshotFolderName, slot.ScreenshotFileName);
                string neighbor = Path.Combine(root, "neighbor.png");
                string mainKey = settings.saveKey.Replace("{n}", "1");
                string legacyKey = settings.saveKey + "1";
                string metaKey = settings.saveKey + "_Metadata_1";
                string[] preferenceKeys = { mainKey, legacyKey, metaKey, mainKey + "_bak", legacyKey + "_bak", metaKey + "_bak" };
                var hook = typeof(SaveManager).Assembly.GetType("Arawn.CrystalSave.Runtime.CloudMirrorFiles")
                    .GetField("FlushOverride", BindingFlags.Static | BindingFlags.NonPublic);
                var previousHook = hook.GetValue(null);
                var previousLog = SaveLogger.LogThreshold;
                TaskCompletionSource<bool> acknowledgement = null;
                try
                {
                    storage.Save(new byte[] { 1, 2, 3 }, slot);
                    Directory.CreateDirectory(Path.GetDirectoryName(payload));
                    Directory.CreateDirectory(Path.GetDirectoryName(image));
                    File.WriteAllBytes(image, new byte[] { 4, 5 });
                    File.WriteAllBytes(image + ".previous", new byte[] { 6, 7 });
                    File.WriteAllBytes(payload + ".previous", new byte[] { 8, 9 });
                    if (File.Exists(metadata)) File.WriteAllBytes(metadata + ".previous", File.ReadAllBytes(metadata));
                    File.WriteAllBytes(neighbor, new byte[] { 10 });
                    var affected = new List<string> { payload, payload + ".previous", metadata, metadata + ".previous", image, image + ".previous" };
                    if (fallback.HasValue)
                    {
                        string fallbackPath = Path.Combine(root, settings.ResolveSlotFolderName(1), mainKey + (fallback == CloudSaveTransport.Binary ? ".sav" : ".json"));
                        File.WriteAllBytes(fallbackPath, new byte[] { 11, 12 });
                        affected.Add(fallbackPath);
                        settings.enableCloudSave = true;
                        settings.cloudSaveTransport = fallback.Value;
                    }
                    var original = affected.Where(File.Exists).ToDictionary(path => path, File.ReadAllBytes);
                    if (preferences)
                    {
                        PlayerPrefs.SetString(legacyKey, "legacy payload");
                        PlayerPrefs.SetString(legacyKey + "_bak", "");
                        PlayerPrefs.SetString(metaKey + "_bak", "backup header");
                        PlayerPrefs.Save();
                    }
                    var originalPreferences = preferenceKeys.ToDictionary(key => key,
                        key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null);
                    acknowledgement = new TaskCompletionSource<bool>();
                    int flushes = 0;
                    bool absentAtAcknowledgement = false;
                    hook.SetValue(null, (Func<Task>)(() =>
                    {
                        flushes++;
                        if (flushes != 1) return Task.CompletedTask;
                        absentAtAcknowledgement = !File.Exists(payload) && !File.Exists(metadata) &&
                            (screenshots ? !File.Exists(image) && !File.Exists(image + ".previous") : File.Exists(image)) &&
                            (!preferences || preferenceKeys.All(key => !PlayerPrefs.HasKey(key)));
                        return acknowledgement.Task;
                    }));
                    SaveLogger.LogThreshold = LogLevel.Error;
                    if (reject && !localOnly) LogAssert.Expect(LogType.Error, new Regex("Failed to delete slot 1: Local mirror persistence failed"));
                    Task deletion = localOnly ? storage.DeleteLocalSlotAsync(slot) : storage.DeleteAsync(slot);
                    yield return Until(() => flushes != 0, "slot deletion persistence acknowledgement", 5);
                    bool completedBeforeAcknowledgement = deletion.IsCompleted;
                    if (reject) acknowledgement.SetException(new IOException("Injected deletion flush failure"));
                    else acknowledgement.SetResult(true);
                    yield return Until(() => deletion.IsCompleted, "slot deletion outcome", 5);
                    Assert.That(completedBeforeAcknowledgement, Is.False, "Deletion must await persistence.");
                    Assert.That(deletion.IsFaulted, Is.EqualTo(reject));
                    Assert.That(flushes, Is.EqualTo(reject ? 2 : 1));
                    if (reject)
                    {
                        foreach (var entry in original) Assert.That(File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value), entry.Key);
                        foreach (var entry in originalPreferences)
                        {
                            Assert.That(PlayerPrefs.HasKey(entry.Key), Is.EqualTo(entry.Value != null), entry.Key);
                            if (entry.Value != null) Assert.That(PlayerPrefs.GetString(entry.Key), Is.EqualTo(entry.Value), entry.Key);
                        }
                    }
                    else
                    {
                        Assert.That(new SaveSystem(settings, root).Load(slot), Is.Null);
                        Assert.That(File.Exists(image), Is.EqualTo(!screenshots));
                        Assert.That(File.Exists(image + ".previous"), Is.EqualTo(!screenshots));
                    }
                    Assert.That(absentAtAcknowledgement, Is.True, "The acknowledged mutation includes the screenshot, recovery copy and affected preferences.");
                    Assert.That(File.ReadAllBytes(neighbor), Is.EqualTo(new byte[] { 10 }));
                }
                finally
                {
                    acknowledgement?.TrySetResult(true);
                    hook.SetValue(null, previousHook);
                    SaveLogger.LogThreshold = previousLog;
                    foreach (string key in preferenceKeys) PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                    UnityEngine.Object.DestroyImmediate(settings);
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            }
        }
    }
}
#endif
