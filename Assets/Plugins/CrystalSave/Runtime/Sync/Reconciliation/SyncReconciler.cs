#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;

namespace Arawn.CrystalSave.Runtime
{
    public sealed class SyncReconciler
    {
        private sealed class SyncState
        {
            public byte[] lastSnapshot;
            public string lastHash;
            public int lastSequence;
        }

        private readonly Dictionary<string, SyncState> states = new();
        private readonly ISyncSerializer serializer;

        public event Action<string> OnRequireSnapshot;

        public SyncReconciler(ISyncSerializer serializer)
        {
            this.serializer = serializer ?? new SyncJsonSerializer();
        }

        public bool TryApplyEnvelope(SyncEnvelope envelope, out byte[] snapshot)
        {
            snapshot = null;
            if (envelope.messageType != SyncMessageType.Snapshot && envelope.messageType != SyncMessageType.Diff)
                return false;
            byte[] payload = DecodePayload(envelope.payloadBase64);
            if (payload == null || (!string.IsNullOrEmpty(envelope.payloadHash) &&
                !string.Equals(SyncDeltaCodec.ComputeHash(payload), envelope.payloadHash, StringComparison.Ordinal)))
                return false;
            string slotId = string.IsNullOrEmpty(envelope.slotId) ? "default" : envelope.slotId;
            var state = GetState(slotId);

            if (envelope.sequence <= state.lastSequence && envelope.sequence != 0)
                return false;

            if (envelope.messageType == SyncMessageType.Snapshot)
            {
                byte[] data = payload;
                state.lastSnapshot = data;
                state.lastHash = SyncDeltaCodec.ComputeHash(data);
                state.lastSequence = envelope.sequence;
                snapshot = (byte[])data.Clone();
                return true;
            }

            if (envelope.messageType == SyncMessageType.Diff)
            {
                if (state.lastSnapshot == null)
                {
                    OnRequireSnapshot?.Invoke(slotId);
                    return false;
                }

                SyncDelta delta;
                try { delta = serializer.Deserialize<SyncDelta>(payload); }
                catch (Exception error) when (error is ArgumentException || error is FormatException || error is InvalidOperationException ||
#if CRYSTALSAVE_NEWTONSOFT
                    error is Newtonsoft.Json.JsonException ||
#endif
                    error is MemoryPack.MemoryPackSerializationException)
                { OnRequireSnapshot?.Invoke(slotId); return false; }
                if (delta == null)
                {
                    OnRequireSnapshot?.Invoke(slotId);
                    return false;
                }

                byte[] reconstructed = SyncDeltaCodec.ApplyDelta(state.lastSnapshot, delta);
                if (reconstructed == null)
                {
                    OnRequireSnapshot?.Invoke(slotId);
                    return false;
                }

                state.lastSnapshot = reconstructed;
                state.lastHash = SyncDeltaCodec.ComputeHash(reconstructed);
                state.lastSequence = envelope.sequence;
                snapshot = (byte[])reconstructed.Clone();
                return true;
            }

            return false;
        }

        private SyncState GetState(string slotId)
        {
            if (!states.TryGetValue(slotId, out var state))
            {
                state = new SyncState();
                states[slotId] = state;
            }
            return state;
        }

        private static byte[] DecodePayload(string payloadBase64)
        {
            if (payloadBase64 == null) return null;
            try { return Convert.FromBase64String(payloadBase64); }
            catch { return null; }
        }
    }
}
#endif
