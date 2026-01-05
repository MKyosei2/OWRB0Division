using System;
using System.IO;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Simple JSON file save/load for prototype.
    /// </summary>
    public static class ProtoSaveSystem
    {
        private const string FileName = "ojika_proto_save.json";

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool HasSave()
        {
            try
            {
                return File.Exists(FilePath);
            }
            catch
            {
                return false;
            }
        }

        public static ProtoSaveState Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrEmpty(json)) return null;
                return JsonUtility.FromJson<ProtoSaveState>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProtoSaveSystem.Load failed: {e.Message}");
                return null;
            }
        }

        public static void Save(ProtoSaveState state)
        {
            try
            {
                if (state == null) return;
                state.savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var json = JsonUtility.ToJson(state, prettyPrint: true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProtoSaveSystem.Save failed: {e.Message}");
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProtoSaveSystem.Clear failed: {e.Message}");
            }
        }
    }
}
