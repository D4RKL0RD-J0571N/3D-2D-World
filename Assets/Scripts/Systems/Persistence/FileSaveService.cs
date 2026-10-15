namespace Systems.Persistence
{
    using System.IO;
    using UnityEngine;
    using Core;
    using Domain.Persistence;

    public class FileSaveService : IGameService
    {
        private string SavePath => Path.Combine(Application.persistentDataPath, "save_game.json");

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        public bool HasSaveFile()
        {
            return File.Exists(SavePath);
        }

        public void WriteData(LevelSaveData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SavePath, json);
                Debug.Log($"[FileSaveService] Game saved successfully to {SavePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FileSaveService] Failed to save game: {ex.Message}");
            }
        }

        public LevelSaveData ReadData()
        {
            if (!HasSaveFile()) return null;

            try
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<LevelSaveData>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FileSaveService] Failed to load game: {ex.Message}");
                return null;
            }
        }

        public void DeleteSave()
        {
            if (HasSaveFile())
            {
                File.Delete(SavePath);
                Debug.Log("[FileSaveService] Save file deleted.");
            }
        }
    }
}
