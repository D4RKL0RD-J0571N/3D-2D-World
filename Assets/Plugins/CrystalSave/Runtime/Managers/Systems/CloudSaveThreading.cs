#if MEMORYPACK && ARAWN_REMEMBERME
using System;

namespace Arawn.CrystalSave.Runtime
{
    internal static class CloudSaveThreading
    {
        internal static void RequireBackgroundThread(string method)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new NotSupportedException("Synchronous cloud " + method + " is not supported on WebGL. Use " + method + "Async.");
#else
            if (UnityMainThreadDispatcher.IsMainThread)
                throw new NotSupportedException("Synchronous cloud " + method + " cannot block Unity's main thread. Use " + method + "Async.");
#endif
        }
    }
}
#endif
