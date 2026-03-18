using System;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON-based save/load system for persistent game data.
/// Saves to Application.persistentDataPath/save.json.
/// </summary>
public static class SaveSystem
{
    private const string FILE_NAME = "save.json";

    [System.Serializable]
    public class SaveData
    {
        public int highScore;
        public int totalRuns;
        public int bestRoundScore;    // highest single-round score
        public string lastPlayed;     // ISO 8601 date string

        public SaveData()
        {
            highScore = 0;
            totalRuns = 0;
            bestRoundScore = 0;
            lastPlayed = DateTime.UtcNow.ToString("o");
        }
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    /// <summary>Load save data. Returns default if file missing or corrupt.</summary>
    public static SaveData Load()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path))
                return new SaveData();

            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            return data ?? new SaveData();
        }
        catch (Exception e)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SaveSystem] Failed to load: {e.Message}");
#endif
            return new SaveData();
        }
    }

    /// <summary>Persist save data to disk.</summary>
    public static void Save(SaveData data)
    {
        try
        {
            data.lastPlayed = DateTime.UtcNow.ToString("o");
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[SaveSystem] Failed to save: {e.Message}");
#endif
        }
    }
}
