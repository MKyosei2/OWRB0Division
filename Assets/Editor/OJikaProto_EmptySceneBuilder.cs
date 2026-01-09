#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

public static class OJikaProto_EmptySceneBuilder
{
    private const string RootName = "__OJI-KA_PROTO__";

    [MenuItem("Tools/OJika Proto/Build Empty Scene Hierarchy (Case01)")]
    public static void Build()
    {
        // If the current scene is dirty, save prompt helps avoid accidental loss.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Make sure we're working in the active scene
        var scene = EditorSceneManager.GetActiveScene();

        // Root group
        var root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Proto Root");
        }

        EnsureEventSystem(root.transform);

        // --- World basics ---
        var light = FindOrCreateGO(root.transform, "Directional Light");
        EnsureComponent(light, "UnityEngine.Light");
        var l = light.GetComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        var ground = FindOrCreateGO(root.transform, "Ground");
        if (ground.GetComponent<MeshRenderer>() == null)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            Undo.RegisterCreatedObjectUndo(plane, "Create Ground");
            plane.transform.SetParent(root.transform, false);
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(8, 1, 8);
            ground = plane;
        }

        // Spawns
        var playerSpawn = FindOrCreateGO(root.transform, "PlayerSpawn");
        playerSpawn.transform.position = new Vector3(0, 0.01f, -4);

        var enemySpawn = FindOrCreateGO(root.transform, "EnemySpawn");
        enemySpawn.transform.position = new Vector3(0, 0.01f, 3);

        // --- Player ---
        var player = FindOrCreateGO(root.transform, "Player");
        player.transform.position = playerSpawn.transform.position;
        player.transform.rotation = Quaternion.identity;

        EnsureComponent(player, "UnityEngine.CharacterController");
        var cc = player.GetComponent<CharacterController>();
        cc.center = new Vector3(0, 0.95f, 0);
        cc.height = 1.9f;
        cc.radius = 0.35f;

        var playerController = EnsureComponent(player, "OJikaProto.PlayerController");
        var playerHealth     = EnsureComponent(player, "OJikaProto.PlayerHealth");
        var playerCombat     = EnsureComponent(player, "OJikaProto.PlayerCombat");
        var lockOn           = EnsureComponent(player, "OJikaProto.LockOnController");

        // --- Camera ---
        var camGO = FindOrCreateGO(root.transform, "Main Camera");
        var cam = EnsureComponent(camGO, "UnityEngine.Camera");
        EnsureComponent(camGO, "UnityEngine.AudioListener");
        camGO.tag = "MainCamera";
        camGO.transform.position = new Vector3(0, 1.8f, -7.5f);
        camGO.transform.rotation = Quaternion.Euler(12, 0, 0);

        var camRig = EnsureComponent(camGO, "OJikaProto.ThirdPersonCameraRig");
        // Assign camera target
        SetFieldIfExists(camRig, "target", player.transform);

        // Assign PlayerController cameraRoot
        SetFieldIfExists(playerController, "cameraRoot", camGO.transform);

        // --- Core singletons (match sample scene layout; safe even if CoreEnsure also spawns them) ---
        EnsureSingletonLike(root.transform, "EventBus", "OJikaProto.EventBus");
        EnsureSingletonLike(root.transform, "RunLogManager", "OJikaProto.RunLogManager");
        EnsureSingletonLike(root.transform, "RuleManager", "OJikaProto.RuleManager");
        EnsureSingletonLike(root.transform, "CaseMetaManager", "OJikaProto.CaseMetaManager");
        EnsureSingletonLike(root.transform, "InfiltrationManager", "OJikaProto.InfiltrationManager");
        var negotiationManager = EnsureSingletonLike(root.transform, "NegotiationManager", "OJikaProto.NegotiationManager");
        var investigationManager = EnsureSingletonLike(root.transform, "InvestigationManager", "OJikaProto.InvestigationManager");

        // --- Combat Director ---
        var combatDirectorGO = FindOrCreateGO(root.transform, "CombatDirector");
        var combatDirector = EnsureComponent(combatDirectorGO, "OJikaProto.CombatDirector");
        SetFieldIfExists(combatDirector, "playerSpawn", playerSpawn.transform);
        SetFieldIfExists(combatDirector, "enemySpawn", enemySpawn.transform);

        // --- Episode Controller ---
        var episodeGO = FindOrCreateGO(root.transform, "EpisodeController");
        var episodeController = EnsureComponent(episodeGO, "OJikaProto.EpisodeController");
        SetFieldIfExists(episodeController, "combatDirector", combatDirector);

        // Load EpisodeDefinition asset by name (Case01)
        var episodeAsset = FindAssetByName("EpisodeDefinition_Case01");
        if (episodeAsset != null)
        {
            SetFieldIfExists(episodeController, "episode", episodeAsset);
        }

        // --- Phase system / HUD ---
        var hudGO = FindOrCreateGO(root.transform, "HUD");
        var phaseHud = EnsureComponent(hudGO, "OJikaProto.Proto_PhaseHUD");
        InvokeIfExists(phaseHud, "EnsureMinimalUI");

        var phaseDirectorGO = FindOrCreateGO(root.transform, "PhaseDirector");
        var phaseDirector = EnsureComponent(phaseDirectorGO, "OJikaProto.ProtoPhaseDirector");

        SetFieldIfExists(phaseDirector, "player", playerController);
        SetFieldIfExists(phaseDirector, "playerCombat", playerCombat);
        SetFieldIfExists(phaseDirector, "lockOn", lockOn);
        SetFieldIfExists(phaseDirector, "cameraRig", camRig);

        // --- Flow Controller ---
        var flowGO = FindOrCreateGO(root.transform, "GameFlow");
        var flow = EnsureComponent(flowGO, "OJikaProto.GameFlowController");
        // Optional: start button helper
        EnsureComponent(flowGO, "OJikaProto.Proto_OneButtonStart");

        SetFieldIfExists(flow, "episode", episodeController);
        SetFieldIfExists(flow, "player", playerController);
        SetFieldIfExists(flow, "playerCombat", playerCombat);
        SetFieldIfExists(flow, "lockOn", lockOn);
        SetFieldIfExists(flow, "cameraRig", camRig);

        // --- Investigation Points (A/B) ---
        CreateOrUpdateInvestigationPoint(root.transform, "InvestigationPoint_A", new Vector3(-2, 0.5f, -1), "CCTV_Loop");
        CreateOrUpdateInvestigationPoint(root.transform, "InvestigationPoint_B", new Vector3(2, 0.5f, 0), "TicketGate_MemoryLoss");

        // Mark scene dirty so user can save
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = root;

        EditorUtility.DisplayDialog(
            "OJika Proto",
            "空シーン向けの Hierarchy を生成しました。\n" +
            "次の確認:\n" +
            "1) EpisodeDefinition_Case01 が見つかっているか\n" +
            "2) Play で OneButtonStart から開始\n",
            "OK"
        );
    }

    // ---------------- helpers ----------------

    private static void EnsureEventSystem(Transform parent)
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        es.transform.SetParent(parent, false);
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private static GameObject FindOrCreateGO(Transform parent, string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Component EnsureComponent(GameObject go, string typeName)
    {
        var t = FindType(typeName);
        if (t == null)
        {
            Debug.LogWarning($"[OJikaProto_EmptySceneBuilder] Type not found: {typeName}");
            return null;
        }

        var c = go.GetComponent(t);
        if (c == null) c = Undo.AddComponent(go, t);
        return c;
    }

    private static Component EnsureSingletonLike(Transform parent, string goName, string typeName)
    {
        var go = FindOrCreateGO(parent, goName);
        return EnsureComponent(go, typeName);
    }

    private static void CreateOrUpdateInvestigationPoint(Transform parent, string name, Vector3 pos, string evidenceTagName)
    {
        var go = GameObject.Find(name);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.name = name;
            go.transform.SetParent(parent, false);
        }

        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);

        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var ip = EnsureComponent(go, "OJikaProto.InvestigationPoint");
        // evidenceTag is enum EvidenceTag in OJikaProto
        if (ip != null)
        {
            var ipType = ip.GetType();
            var f = ipType.GetField("evidenceTag", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType.IsEnum)
            {
                try
                {
                    var enumVal = Enum.Parse(f.FieldType, evidenceTagName);
                    f.SetValue(ip, enumVal);
                }
                catch { /* ignore */ }
            }
        }
    }

    private static UnityEngine.Object FindAssetByName(string nameWithoutExt)
    {
        // Find by filename-ish match
        var guids = AssetDatabase.FindAssets(nameWithoutExt);
        if (guids == null || guids.Length == 0) return null;

        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
            if (System.IO.Path.GetFileNameWithoutExtension(path) != nameWithoutExt) continue;
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
        }

        // fallback: first asset result
        var p0 = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p0);
    }

    private static Type FindType(string typeName)
    {
        // Allow short names (e.g., "OJikaProto.PlayerController" or "PlayerController")
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t != null)
            .ToArray();

        // Exact full name
        var t1 = types.FirstOrDefault(t => t.FullName == typeName);
        if (t1 != null) return t1;

        // Exact name
        var shortName = typeName.Contains(".") ? typeName.Split('.').Last() : typeName;
        var t2 = types.FirstOrDefault(t => t.Name == shortName);
        if (t2 != null) return t2;

        return null;
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch { return Array.Empty<Type>(); }
    }

    private static void SetFieldIfExists(object instance, string fieldName, object value)
    {
        if (instance == null || value == null) return;

        var t = instance.GetType();
        var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f == null) return;

        if (f.FieldType.IsAssignableFrom(value.GetType()))
        {
            f.SetValue(instance, value);
            EditorUtility.SetDirty((UnityEngine.Object)instance);
            return;
        }

        // Allow assigning Component/Transform to fields typed as those base types
        if (value is Component c && f.FieldType.IsAssignableFrom(c.GetType()))
        {
            f.SetValue(instance, c);
            EditorUtility.SetDirty((UnityEngine.Object)instance);
        }
        else if (value is Transform tr && f.FieldType == typeof(Transform))
        {
            f.SetValue(instance, tr);
            EditorUtility.SetDirty((UnityEngine.Object)instance);
        }
    }

    private static void InvokeIfExists(object instance, string methodName)
    {
        if (instance == null) return;
        var t = instance.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var m = t.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
        if (m != null) m.Invoke(instance, null);
    }
}
#endif
