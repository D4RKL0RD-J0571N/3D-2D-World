#if MEMORYPACK && ARAWN_REMEMBERME
using System;
namespace Arawn.CrystalSave.Runtime
{
    /// <summary>Per-call limits for portable saves and bundles. Values are copied when a request is made.</summary>
    public sealed class SaveBundleOptions
    {
        public int MaxCompressedBytes { get; set; } = 64 * 1024 * 1024;
        public int MaxExpandedBytes { get; set; } = 128 * 1024 * 1024;
        public int MaxEntryBytes { get; set; } = 64 * 1024 * 1024;
        public int MaxManifestBytes { get; set; } = 1024 * 1024;
        public int MaxEntries { get; set; } = 16;
        public int MaxNameBytes { get; set; } = 256;
        public int MaxJsonDepth { get; set; } = 64;
        internal SaveBundleOptions Snapshot()
        {
            if (MaxCompressedBytes <= 0 || MaxExpandedBytes <= 0 || MaxEntryBytes <= 0 || MaxManifestBytes <= 0 ||
                MaxEntries <= 0 || MaxEntries > ushort.MaxValue || MaxNameBytes <= 0 || MaxNameBytes > ushort.MaxValue || MaxJsonDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(SaveBundleOptions), "Bundle limits must be positive and fit the container format.");
            return (SaveBundleOptions)MemberwiseClone();
        }
        internal static SaveBundleOptions Resolve(SaveBundleOptions options) => (options ?? new SaveBundleOptions()).Snapshot();
    }
}
#endif
