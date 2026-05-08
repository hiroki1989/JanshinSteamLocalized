using System;
using System.IO;
using UnityEngine;

public static class MatchSuspendService
{
    [Serializable]
    public class SuspendData
    {
        public int version = 1;
        public string savedAtIso;
        public string sceneName;

        public bool hasData;

        public string payloadJson;
    }

    private const string FileName = "match_suspend.json";

    private static string FilePath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }

    public static bool Exists()
    {
        if (!File.Exists(FilePath)) return false;
        try
        {
            var json = File.ReadAllText(FilePath);
            var data = JsonUtility.FromJson<SuspendData>(json);
            return data != null && data.hasData && !string.IsNullOrEmpty(data.payloadJson);
        }
        catch
        {
            return false;
        }
    }

    public static void SavePayload(string payloadJson, string sceneName)
    {
        var data = new SuspendData();
        data.savedAtIso = DateTime.UtcNow.ToString("o");
        data.sceneName = sceneName;
        data.hasData = true;
        data.payloadJson = payloadJson;

        var json = JsonUtility.ToJson(data);
        Directory.CreateDirectory(Application.persistentDataPath);
        File.WriteAllText(FilePath, json);
    }

    public static bool TryLoadPayload(out string payloadJson, out string sceneName)
    {
        payloadJson = null;
        sceneName = null;

        if (!File.Exists(FilePath)) return false;

        try
        {
            var json = File.ReadAllText(FilePath);
            var data = JsonUtility.FromJson<SuspendData>(json);

            if (data == null) return false;
            if (!data.hasData) return false;
            if (string.IsNullOrEmpty(data.payloadJson)) return false;

            payloadJson = data.payloadJson;
            sceneName = data.sceneName;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
        }
    }
}