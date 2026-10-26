#if MEMORYPACK && ARAWN_REMEMBERME
using System.IO;
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
    // A local-only query must not download cloud data or allocate a full payload.
    // Kept optional so third-party ISaveSystem implementations remain compatible.
    internal interface ILocalSavePresence
    {
        bool HasLocalSave(SaveSlot slot);
    }

    public partial class SaveSystem : ILocalSavePresence
    {
        bool ILocalSavePresence.HasLocalSave(SaveSlot slot)
        {
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            if (UseLocalMirror)
            {
                if (settings.saveMethod == SaveMethod.PlayerPrefs &&
                    PlayerPrefs.HasKey(ResolveExistingPlayerPrefsKey(slot.SlotNumber))) return true;
                if (settings.saveMethod == SaveMethod.BinaryFileFormat && FindCommittedPayloadPath(slot) != null)
                    return true;
            }
            if (!settings.enableCloudSave) return false;
            string key = Sanitize(ResolveSaveKey(slot.SlotNumber));
            string folder = SlotFolder(slot.SlotNumber);
            return AtomicSaveFile.Exists(Path.Combine(folder, key + ".sav")) ||
                   AtomicSaveFile.Exists(Path.Combine(folder, key + ".json"));
        }
    }

    public partial class MySqlSaveSystem : ILocalSavePresence
    {
        bool ILocalSavePresence.HasLocalSave(SaveSlot slot)
        {
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            return LocalWritesEnabled() && CloudMirrorFiles.FindCommittedPath(Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav")) != null;
        }
    }

    public sealed partial class FirebaseSaveSystem : ILocalSavePresence
    {
        bool ILocalSavePresence.HasLocalSave(SaveSlot slot)
        {
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            return LocalWritesEnabled() && CloudMirrorFiles.FindCommittedPath(Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav")) != null;
        }
    }

    public sealed partial class SupabaseSaveSystem : ILocalSavePresence
    {
        bool ILocalSavePresence.HasLocalSave(SaveSlot slot)
        {
            SaveIoScope.CheckCancellation();
            SaveFileTransaction.RecoverAll(persistentPath);
            Verification.Recover();
            return LocalWritesEnabled() && CloudMirrorFiles.FindCommittedPath(Path.Combine(persistentPath, $"{localFileStem}{slot.SlotNumber}.sav")) != null;
        }
    }
}
#endif
