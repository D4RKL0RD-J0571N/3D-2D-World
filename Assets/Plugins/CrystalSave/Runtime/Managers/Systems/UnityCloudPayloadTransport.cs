#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Threading.Tasks;

namespace Arawn.CrystalSave.Runtime
{
    // Binary payloads use the Data API on WebGL and the Files API elsewhere.
    // Resolve that choice once for all operations, including existence reads.
    internal sealed class UnityCloudPayloadTransport
    {
        internal static bool IsWebGLPlayer
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        readonly CloudSaveTransport transport;
        readonly Func<CloudSaveTransport, string, byte[], Task> save;
        readonly Func<CloudSaveTransport, string, Task<byte[]>> load;
        readonly Func<CloudSaveTransport, string, Task> delete;
        readonly Func<Exception, bool> isNotFound;

        internal UnityCloudPayloadTransport(CloudSaveTransport configuredTransport, bool webGLPlayer,
            Func<CloudSaveTransport, string, byte[], Task> save,
            Func<CloudSaveTransport, string, Task<byte[]>> load,
            Func<CloudSaveTransport, string, Task> delete,
            Func<Exception, bool> isNotFound)
        {
            if (configuredTransport != CloudSaveTransport.Binary && configuredTransport != CloudSaveTransport.JSON)
                throw new ArgumentOutOfRangeException(nameof(configuredTransport));
            transport = webGLPlayer ? CloudSaveTransport.JSON : configuredTransport;
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.load = load ?? throw new ArgumentNullException(nameof(load));
            this.delete = delete ?? throw new ArgumentNullException(nameof(delete));
            this.isNotFound = isNotFound ?? throw new ArgumentNullException(nameof(isNotFound));
        }

        internal Task SaveAsync(string key, byte[] payload) => save(transport, key, payload);

        internal async Task<byte[]> LoadAsync(string key)
        {
            try { return await load(transport, key); }
            catch (Exception ex) when (isNotFound(ex)) { return null; }
        }

        internal async Task DeleteAsync(string key)
        {
            try { await delete(transport, key); }
            catch (Exception ex) when (isNotFound(ex)) { }
        }
    }
}
#endif
