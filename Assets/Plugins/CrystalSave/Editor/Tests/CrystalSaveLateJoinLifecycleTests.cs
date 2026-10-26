#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using Arawn.CrystalSave.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Arawn.CrystalSave.EditorTests
{
    public sealed partial class CrystalSaveLifecycleTests
    {
        [UnityTest]
        public IEnumerator LateJoin_WorldCaptureAndRestoreIncludePrefabBlobsAndDelayedTransformsWithoutDiskWrites()
        {
            yield return LoadScene("A");var manager=SaveManager.Instance;
            var helper=SceneComponent("LateJoinScene");helper.transform.position=new Vector3(4,5,6);
            var prefab=Spawn();prefab.transform.position=new Vector3(11,12,13);
            var child=prefab.GetComponentInChildren<RememberTransform>();child.transform.localPosition=new Vector3(2,3,4);
            yield return null;
            string id=prefab.UniqueID;var previous=manager.CurrentSaveData;
            string root=manager.SaveSystem.GetSaveGamesPath();var before=LateJoinDiskFiles(root);
            var capture=manager.CaptureWorldSnapshotAsync();yield return Await(capture);
            Assert.That(manager.CurrentSaveData,Is.SameAs(previous),"Capturing for a peer cannot replace the host's current snapshot.");
            helper.transform.position=Vector3.zero;prefab.transform.position=Vector3.zero;child.transform.localPosition=Vector3.zero;
            var restore=manager.ApplyWorldSnapshotAsync(capture.Result);yield return Await(restore);
            Assert.That(helper.transform.position,Is.EqualTo(new Vector3(4,5,6)),"Task must include delayed transform application.");
            var restored=manager.FindGameObjectByUniqueID(id);
            Assert.That(restored,Is.Not.Null);Assert.That(restored.transform.position,Is.EqualTo(new Vector3(11,12,13)));
            Assert.That(restored.GetComponentInChildren<RememberTransform>(true).transform.localPosition,Is.EqualTo(new Vector3(2,3,4)));
            Assert.That(LateJoinDiskFiles(root),Is.EqualTo(before));
        }
        [UnityTest]
        public IEnumerator LateJoin_InvalidAndCancelledCandidatesPreserveWorldAndPendingGeneration()
        {
            var manager=SaveManager.Instance;var helper=SceneComponent("LateJoinRejected");helper.transform.position=new Vector3(8,9,10);
            yield return null;var capture=manager.CaptureWorldSnapshotAsync();yield return Await(capture);
            var current=manager.CurrentSaveData;var pending=FeatureTestSupport.PendingBytes(manager.ComponentManager);
            var malformed=manager.ApplyWorldSnapshotAsync(new byte[]{1,2,3,4});
            yield return Until(()=>malformed.IsCompleted,"Malformed late-join candidate");
            Assert.That(malformed.IsFaulted,Is.True);
            Assert.That(manager.CurrentSaveData,Is.SameAs(current));Assert.That(FeatureTestSupport.PendingBytes(manager.ComponentManager),Is.SameAs(pending));
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();var rejected=manager.ApplyWorldSnapshotAsync(capture.Result,cancelled.Token);
                yield return Until(()=>rejected.IsCompleted,"Cancelled late-join candidate");Assert.That(rejected.IsCanceled,Is.True);
            }
            Assert.That(helper.transform.position,Is.EqualTo(new Vector3(8,9,10)));
            Assert.That(manager.CurrentSaveData,Is.SameAs(current));
        }
        [UnityTest]
        public IEnumerator LateJoin_RequiredCaptureFailureCannotPublishSnapshot()
        {
            var manager=SaveManager.Instance;var probe=new LateJoinFailingCapture();
            manager.ComponentManager.RegisterSaveableComponent(probe);var before=manager.CurrentSaveData;
            LogAssert.ignoreFailingMessages=true;
            try
            {
                var task=manager.CaptureWorldSnapshotAsync();yield return Until(()=>task.IsCompleted,"Rejected late-join capture");
                Assert.That(task.IsFaulted,Is.True);Assert.That(manager.CurrentSaveData,Is.SameAs(before));
            }
            finally{manager.ComponentManager.UnregisterSaveableComponent(probe);LogAssert.ignoreFailingMessages=false;}
        }
        static string[] LateJoinDiskFiles(string root)=>Directory.Exists(root)?Directory.GetFiles(root,"*",SearchOption.AllDirectories)
            .OrderBy(x=>x).Select(p=>p+":"+SyncDeltaCodec.ComputeHash(File.ReadAllBytes(p))).ToArray():Array.Empty<string>();
        sealed class LateJoinFailingCapture:ISaveable
        {
            public string UniqueIdentifier=>"late-join-capture_failure";
            public byte[] SaveData()=>throw new InvalidOperationException("Late-join capture fixture failure");
            public void LoadData(byte[] bytes){}
        }
    }
}
#endif
