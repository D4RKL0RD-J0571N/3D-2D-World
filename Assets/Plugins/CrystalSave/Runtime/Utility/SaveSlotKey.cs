#if MEMORYPACK && ARAWN_REMEMBERME
namespace Arawn.CrystalSave.Runtime
{
    // One identity rule for storage and slot discovery; legacy reads remain supported.
    internal static class SaveSlotKey
    {
        internal static string Resolve(string pattern, int slot)
        {
            string key = pattern ?? string.Empty;
            return key.Contains("{n}") ? key.Replace("{n}", slot.ToString()) : key + slot;
        }
    }
}
#endif
