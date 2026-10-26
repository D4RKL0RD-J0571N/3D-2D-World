#if MEMORYPACK && ARAWN_REMEMBERME
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    [CreateAssetMenu(fileName = "SyncSettings", menuName = "Crystal Save/Settings/Sync Settings", order = 820)]
    public class SyncSettings : ScriptableObject
    {
        [Tooltip("Enable snapshot syncing through registered transports.")]
        public bool enableSnapshots = true;

        [Tooltip("Enable diff syncing through registered transports.")]
        public bool enableDiffs = false;

        [Tooltip("Seconds between snapshot syncs.")]
        public float snapshotIntervalSeconds = 300f;

        [Tooltip("Seconds between diff syncs (for high-frequency transport).")]
        public float diffIntervalSeconds = 0.25f;

        [Tooltip("Auto-connect transports on startup.")]
        public bool autoConnectTransports = true;

        [Tooltip("Registered sync transports (network-agnostic).")]
        public List<SyncTransportAsset> transports = new();

        [Header("Late-join world snapshot")]
        [Tooltip("Allow clients to request and apply the server's current world without saving to disk.")]
        public bool enableLateJoinSnapshots;

        [Tooltip("Request on connection/reconnection. Disable when your game must wait for network scenes or object identities, then request explicitly.")]
        public bool autoRequestLateJoinSnapshot = true;

        [Tooltip("Use LZ4 instead of GZip for outgoing late-join snapshots. Receivers support both. Disk compression is unchanged.")]
        public bool lateJoinUseLz4;

        [Range(512, 16384)] public int lateJoinChunkBytes = 8192;
        [Min(1024)] public int lateJoinMaxSnapshotBytes = 16 * 1024 * 1024;
        [Min(1)] public float lateJoinTimeoutSeconds = 30f;
        [Range(1, 16)] public int lateJoinMaxConcurrentTransfers = 4;
    }
}
#endif
