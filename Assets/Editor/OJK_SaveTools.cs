#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace OJikaProto
{
    public static class OJK_SaveTools
    {
        [MenuItem("OJK/Save/Clear Proto Save")]
        public static void ClearProtoSave()
        {
            var path = Path.Combine(Application.persistentDataPath, "ojika_proto_save.json");

            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log($"OJK: Save deleted -> {path}");
            }
            else
            {
                Debug.Log($"OJK: Save not found -> {path}");
            }

            // ついでに PlayerPrefs もクリア（使ってた場合の保険）
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.Log("OJK: PlayerPrefs cleared.");
        }

        [MenuItem("OJK/Save/Open Persistent Data Path")]
        public static void OpenPersistentPath()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
    }
}
#endif
