// OJK_FixSceneMissingScripts.cs
// Place under: Assets/Editor/OJK_FixSceneMissingScripts.cs
// Menu: Tools > OJikaProto > Fix Missing Scripts in Scene (and Rebuild Verification)
//
// What it does:
// 1) Removes ALL missing-script components in the current scene (Unity null components).
// 2) Optionally rebuilds the verification hierarchy using OJK_BuildVerificationScene.Build().
//
// Use when you see "The associated script can not be loaded" on objects like EventBus, etc.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OJK_FixSceneMissingScripts
{
    [MenuItem("Tools/OJikaProto/Fix Missing Scripts in Scene (and Rebuild Verification)")]
    public static void FixAndRebuild()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[OJK] Active scene is invalid.");
            return;
        }

        int removedTotal = 0;

        // Remove missing scripts from all root objects.
        foreach (var root in scene.GetRootGameObjects())
        {
            removedTotal += RemoveMissingRecursive(root);
        }

        Debug.Log($"[OJK] Removed missing-script components: {removedTotal}");

        // Rebuild the verification scene structure (safe; reuses existing objects by name).
        try
        {
            OJK_BuildVerificationScene.Build();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[OJK] Rebuild failed. Ensure all OJikaProto scripts compile, then run again.\n" + ex);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[OJK] Scene fixed and saved.");
    }

    private static int RemoveMissingRecursive(GameObject go)
    {
        int removed = 0;

        // Unity provides an editor utility that removes all missing MonoBehaviours on a GameObject.
        // Returns number removed.
        int before = CountMissing(go);
        if (before > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            removed += before;
        }

        foreach (Transform child in go.transform)
        {
            removed += RemoveMissingRecursive(child.gameObject);
        }

        return removed;
    }

    private static int CountMissing(GameObject go)
    {
        int count = 0;
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) count++;
        }
        return count;
    }
}
#endif
