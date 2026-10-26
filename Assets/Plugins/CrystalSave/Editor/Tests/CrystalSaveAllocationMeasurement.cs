#if UNITY_EDITOR && MEMORYPACK && ARAWN_REMEMBERME
using System;
using NUnit.Framework;
using Unity.Profiling;

namespace Arawn.CrystalSave.EditorTests
{
    internal static class CrystalSaveAllocationMeasurement
    {
        private static byte[] calibrationBuffer;

        internal static long Count(Action action)
        {
            // Unity's Mono thread-byte counter can return zero even for known allocations.
            // Use the same recorder as Unity's performance test package, and require both
            // allocation and empty controls on every measurement/editor configuration.
            using (var recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
            {
                Assert.That(recorder.Valid, Is.True, "A working GC.Alloc recorder is required; missing measurement cannot qualify as zero.");
                recorder.Start();
                recorder.Stop();
                recorder.Reset();
                recorder.Start();
                calibrationBuffer = new byte[4096];
                recorder.Stop();
                long control = Samples(recorder);
                GC.KeepAlive(calibrationBuffer);
                calibrationBuffer = null;
                Assert.That(control, Is.GreaterThan(0), "The recorder missed a known managed allocation.");
                recorder.Reset();
                recorder.Start();
                recorder.Stop();
                Assert.That(Samples(recorder), Is.Zero, "The empty measurement must not report allocations.");
                recorder.Reset();
                recorder.Start();
                try { action(); }
                finally { recorder.Stop(); }
                return Samples(recorder);
            }
        }

        private static long Samples(ProfilerRecorder recorder) => recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
    }
}
#endif
