// OJK_BuildVerificationScene.cs
// Place this file under: Assets/Editor/OJK_BuildVerificationScene.cs
// Then run: Tools > OJikaProto > Build Verification Scene (from empty)
//
// This is a one-off scene builder to validate the prototype scripts in an empty project scene.
// It creates a minimal "Case01" playable setup: player + camera + investigation points + security camera + combat director + episode/negotiation assets.
//
// NOTE:
// - It will create assets under Assets/_OJK_AutoSetup/ (safe to delete later).
// - It does NOT require any art assets; uses Unity primitives.
// - It avoids overwriting existing assets/objects unless names collide exactly (it reuses if found).

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJikaProto;

public static class OJK_BuildVerificationScene
{
    private const string RootFolder = "Assets/_OJK_AutoSetup";
    private const string AssetFolder = RootFolder + "/Assets";
    private const string PrefabFolder = RootFolder + "/Prefabs";
    private const string SceneFolder = RootFolder + "/Scenes";

    private const string ScenePath = SceneFolder + "/OJK_Verification.unity";

    [MenuItem("Tools/OJikaProto/Build Verification Scene (from empty)")]
    public static void Build()
    {
        EnsureFolders();

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("Active scene is invalid.");
            return;
        }

        // ---------------- Root containers ----------------
        var root = GetOrCreate("OJK_Verification_Root");
        var systems = GetOrCreate("Systems", root.transform);
        var world = GetOrCreate("World", root.transform);
        var gameplay = GetOrCreate("Gameplay", root.transform);

        // ---------------- World basics ----------------
        SetupLighting(world.transform);
        var ground = SetupGround(world.transform);

        // ---------------- Assets (ScriptableObjects / Prefabs) ----------------
        var negotiationDef = EnsureNegotiationAsset();
        var enemyPrefab = EnsureEnemyPrefab();
        var ruleAssets = EnsureRuleAssets();
        var episodeDef = EnsureEpisodeAsset(enemyPrefab, negotiationDef);

        // ---------------- Managers / Systems ----------------
        // Create managers explicitly so hierarchy is clear (CoreEnsure can also spawn them, but that creates clutter roots).
        var bus = EnsureSingletonGO<EventBus>("EventBus", systems.transform);
        var runLog = EnsureSingletonGO<RunLogManager>("RunLogManager", systems.transform);
        var invest = EnsureSingletonGO<InvestigationManager>("InvestigationManager", systems.transform);
        var ruleMgrGO = EnsureSingletonGO<RuleManager>("RuleManager", systems.transform);
        var nego = EnsureSingletonGO<NegotiationManager>("NegotiationManager", systems.transform);
        var infil = EnsureSingletonGO<InfiltrationManager>("InfiltrationManager", systems.transform);
        var meta = EnsureSingletonGO<CaseMetaManager>("CaseMetaManager", systems.transform);

        // Assign rule list into RuleManager (Inspector equivalent)
        ApplyRuleList(ruleMgrGO.GetComponent<RuleManager>(), ruleAssets);

        // Recap UI + Feedback + Subtitles
        EnsureComponentOnGO<ProtoRecapUI>(GetOrCreate("ProtoRecapUI", systems.transform));
        EnsureComponentOnGO<FeedbackManager>(GetOrCreate("FeedbackManager", systems.transform));
        EnsureComponentOnGO<SubtitleManager>(GetOrCreate("SubtitleManager", systems.transform));
        EnsureComponentOnGO<DebugHUD>(GetOrCreate("DebugHUD", systems.transform));

        // Bootstrapper (optional, but keeps ordering deterministic)
        EnsureComponentOnGO<GameBootstrapper>(GetOrCreate("GameBootstrapper", systems.transform));

        // ---------------- Player + Camera ----------------
        var player = EnsurePlayer(gameplay.transform, ground.transform.position);
        var camRig = EnsureCameraRig(gameplay.transform, player.transform);

        // ---------------- Combat director + spawns ----------------
        var combatDirectorGO = GetOrCreate("CombatDirector", gameplay.transform);
        var combatDirector = EnsureComponentOnGO<CombatDirector>(combatDirectorGO);

        var spawns = GetOrCreate("Spawns", gameplay.transform);
        var playerSpawn = GetOrCreate("PlayerSpawn", spawns.transform);
        playerSpawn.transform.position = player.transform.position;
        playerSpawn.transform.rotation = Quaternion.identity;

        var enemySpawn = GetOrCreate("EnemySpawn", spawns.transform);
        enemySpawn.transform.position = player.transform.position + new Vector3(0f, 0f, 8f);
        enemySpawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        combatDirector.playerSpawn = playerSpawn.transform;
        combatDirector.enemySpawn = enemySpawn.transform;

        // Optional checkpoint warper
        var warperGO = GetOrCreate("ProtoCheckpointWarp", gameplay.transform);
        var warper = EnsureComponentOnGO<ProtoCheckpointWarp>(warperGO);
        warper.combatStartPoint = playerSpawn.transform;

        // ---------------- Episode + Flow ----------------
        var episodeGO = GetOrCreate("EpisodeController", gameplay.transform);
        var episode = EnsureComponentOnGO<EpisodeController>(episodeGO);
        episode.episode = episodeDef;
        episode.combatDirector = combatDirector;
        episode.autoStart = false; // Flow handles start

        var flowGO = GetOrCreate("GameFlowController", gameplay.transform);
        var flow = EnsureComponentOnGO<GameFlowController>(flowGO);
        flow.episode = episode;
        flow.player = player.GetComponent<PlayerController>();
        flow.playerCombat = player.GetComponent<PlayerCombat>();
        flow.lockOn = player.GetComponent<LockOnController>();
        flow.cameraRig = camRig;

        // ---------------- Investigation points + Security camera ----------------
        var invRoot = GetOrCreate("Investigation", gameplay.transform);
        var p1 = CreateInvestigationPoint(invRoot.transform, "EvidencePoint_A", new Vector3(2.5f, 0.5f, 2.0f), EvidenceTag.CCTV_Loop, requiresInfiltration: false);
        var p2 = CreateInvestigationPoint(invRoot.transform, "EvidencePoint_B", new Vector3(-2.5f, 0.5f, 2.0f), EvidenceTag.TicketGate_MemoryLoss, requiresInfiltration: true);
        warper.investigationPoint = p1.transform;

        var secRoot = GetOrCreate("Security", gameplay.transform);
        CreateSecurityCamera(secRoot.transform, "SecurityCamera_01", new Vector3(0f, 2.2f, 1.5f), lookAt: p2.transform);

        // ---------------- Final touches ----------------
        // Ensure player cameraRoot is assigned for movement.
        var pc = player.GetComponent<PlayerController>();
        if (pc != null && pc.cameraRoot == null)
        {
            pc.cameraRoot = Camera.main != null ? Camera.main.transform : camRig.transform;
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        // Auto-save the scene to a safe path (first run)
        var active = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(active.path))
        {
            EditorSceneManager.SaveScene(active, ScenePath);
            Debug.Log($"[OJK] Verification scene saved: {ScenePath}");
        }
        else
        {
            EditorSceneManager.SaveScene(active);
            Debug.Log($"[OJK] Verification scene updated: {active.path}");
        }

        Debug.Log("[OJK] Verification scene build complete. Press Play, then press Enter to start Case01.");
    }

    // =============================== Helpers ===============================

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "_OJK_AutoSetup");
        EnsureFolder(RootFolder, "Assets");
        EnsureFolder(RootFolder, "Prefabs");
        EnsureFolder(RootFolder, "Scenes");
    }

    private static void EnsureFolder(string parent, string name)
    {
        var path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject GetOrCreate(string name, Transform parent = null)
    {
        GameObject go = null;
        if (parent == null)
        {
            go = GameObject.Find(name);
        }
        else
        {
            var t = parent.Find(name);
            go = t != null ? t.gameObject : null;
        }

        if (go == null)
        {
            go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent);
        }
        return go;
    }

    private static T EnsureComponentOnGO<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private static GameObject EnsureSingletonGO<T>(string name, Transform parent) where T : Component
    {
        // If already exists anywhere, reuse it but re-parent for clarity.
        var existing = Object.FindObjectOfType<T>();
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null) go = GetOrCreate(name, parent);
        go.name = name;
        go.transform.SetParent(parent, worldPositionStays: true);
        EnsureComponentOnGO<T>(go);
        return go;
    }

    private static void SetupLighting(Transform parent)
    {
        // Directional light
        var lightGO = GetOrCreate("Directional Light", parent);
        var light = EnsureComponentOnGO<Light>(lightGO);
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Ambient
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.65f, 0.7f, 0.75f, 1f);
    }

    private static GameObject SetupGround(Transform parent)
    {
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }
        ground.transform.SetParent(parent, true);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        return ground;
    }

    private static GameObject EnsurePlayer(Transform parent, Vector3 groundPos)
    {
        var go = GameObject.Find("Player");
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
        }
        go.transform.SetParent(parent, true);
        go.transform.position = groundPos + new Vector3(0f, 1.0f, -2f);
        go.transform.rotation = Quaternion.identity;

        // CharacterController (required by PlayerController)
        if (go.GetComponent<CharacterController>() == null)
        {
            Object.DestroyImmediate(go.GetComponent<CapsuleCollider>()); // avoid double collider
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
        }

        EnsureComponentOnGO<PlayerController>(go);
        EnsureComponentOnGO<PlayerCombat>(go);
        EnsureComponentOnGO<PlayerHealth>(go);
        EnsureComponentOnGO<LockOnController>(go);

        return go;
    }

    private static ThirdPersonCameraRig EnsureCameraRig(Transform parent, Transform target)
    {
        // Use Main Camera if exists, else create one.
        Camera cam = Camera.main;
        GameObject camGO;
        if (cam == null)
        {
            camGO = new GameObject("Main Camera");
            cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            cam.tag = "MainCamera";
        }
        else camGO = cam.gameObject;

        camGO.transform.SetParent(parent, true);

        var rig = EnsureComponentOnGO<ThirdPersonCameraRig>(camGO);
        rig.target = target;

        // Default camera tune for readability
        rig.distance = 6.0f;
        rig.pivotOffset = new Vector3(0f, 1.45f, 0f);
        rig.minPitch = -25f;
        rig.maxPitch = 60f;

        return rig;
    }

    private static GameObject CreateInvestigationPoint(Transform parent, string name, Vector3 pos, EvidenceTag tag, bool requiresInfiltration)
    {
        var go = parent.Find(name)?.gameObject;
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
        }
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        var ip = EnsureComponentOnGO<InvestigationPoint>(go);
        ip.evidenceTag = tag;
        ip.interactRadius = 1.8f;
        ip.requiresInfiltration = requiresInfiltration;
        ip.blockedDuringLockdown = true;

        var col = go.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        return go;
    }

    private static void CreateSecurityCamera(Transform parent, string name, Vector3 pos, Transform lookAt)
    {
        var go = parent.Find(name)?.gameObject;
        if (go == null) go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;

        // head pivot (optional)
        var head = GetOrCreate("Head", go.transform);
        head.transform.localPosition = Vector3.zero;

        var cone = EnsureComponentOnGO<SecurityCameraCone>(go);
        cone.head = head.transform;
        cone.viewDistance = 9f;
        cone.viewAngle = 80f;
        cone.sweep = true;
        cone.sweepAngle = 90f;
        cone.sweepSpeed = 1.6f;
        cone.reason = "Camera";

        if (lookAt != null)
        {
            var dir = (lookAt.position - go.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }

    // =============================== Assets ===============================

    private static NegotiationDefinition EnsureNegotiationAsset()
    {
        var path = AssetFolder + "/NegotiationDefinition_Case01.asset";
        var asset = AssetDatabase.LoadAssetAtPath<NegotiationDefinition>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<NegotiationDefinition>();
        asset.title = "停戦交渉";
        asset.prompt = "怪異は崩れている。今なら条件次第で収束できる。";

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static GameObject EnsureEnemyPrefab()
    {
        var path = PrefabFolder + "/Enemy_Proto.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null) return prefab;

        // Build a simple enemy from primitives
        var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        temp.name = "Enemy_Proto";
        temp.transform.position = new Vector3(0f, 1f, 8f);

        // Ensure required components
        EnsureComponentOnGO<Damageable>(temp);
        EnsureComponentOnGO<Breakable>(temp);
        EnsureComponentOnGO<EnemyController>(temp);

        // Collider is fine. (EnemyController moves via transform.position)
        var saved = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);

        AssetDatabase.SaveAssets();
        return saved;
    }

    private static List<RuleDefinition> EnsureRuleAssets()
    {
        var rules = new List<RuleDefinition>();

        // Gaze rule
        rules.Add(EnsureRuleAsset(
            "Rule_GazeProhibition",
            RuleType.GazeProhibition,
            displayName: "規約：視線固定禁止",
            hint: "視線が吸われている",
            confirmPointsRequired: 2,
            clueTags: new[] { EvidenceTag.CCTV_Loop }
        ));

        // Repeat attack rule
        rules.Add(EnsureRuleAsset(
            "Rule_RepeatAttackProhibition",
            RuleType.RepeatAttackProhibition,
            displayName: "規約：連撃反復禁止",
            hint: "同じ手が続くと歪む",
            confirmPointsRequired: 2,
            clueTags: new[] { EvidenceTag.TicketGate_MemoryLoss }
        ));

        AssetDatabase.SaveAssets();
        return rules;
    }

    private static RuleDefinition EnsureRuleAsset(
        string fileNameNoExt,
        RuleType type,
        string displayName,
        string hint,
        int confirmPointsRequired,
        EvidenceTag[] clueTags)
    {
        var path = AssetFolder + "/" + fileNameNoExt + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<RuleDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<RuleDefinition>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.ruleType = type;
        asset.displayName = displayName;
        asset.startHidden = true;
        asset.hiddenLabel = "？？？";
        asset.hintText = hint;
        asset.confirmPointsRequired = Mathf.Max(1, confirmPointsRequired);
        asset.clueEvidenceTags = clueTags ?? new EvidenceTag[0];

        // Default thresholds
        asset.gazeSecondsToViolate = 2.6f;
        asset.repeatCountToViolate = 3;
        asset.repeatWindowSeconds = 2.0f;

        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static EpisodeDefinition EnsureEpisodeAsset(GameObject enemyPrefab, NegotiationDefinition negotiationDef)
    {
        var path = AssetFolder + "/EpisodeDefinition_Case01.asset";
        var asset = AssetDatabase.LoadAssetAtPath<EpisodeDefinition>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<EpisodeDefinition>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.episodeName = "終電のいない駅（検証シーン）";

        // Ensure phases array (4 phases expected)
        if (asset.phases == null || asset.phases.Length < 4)
        {
            asset.phases = new EpisodePhase[4]
            {
                new EpisodePhase{ phaseType=EpisodePhaseType.Intro, title="導入", description="Enterで開始" },
                new EpisodePhase{ phaseType=EpisodePhaseType.Investigation, title="調査", description="調査ポイントに近づいてEで証拠を取得。証拠が揃ったらEnter。", targetEvidenceCount=2 },
                new EpisodePhase{ phaseType=EpisodePhaseType.Combat, title="収束作戦", description="規約→崩し→交渉（F）で決着へ。" },
                new EpisodePhase{ phaseType=EpisodePhaseType.Outro, title="後日談", description="Enterで完了" },
            };
        }

        // Bind combat phase
        for (int i = 0; i < asset.phases.Length; i++)
        {
            if (asset.phases[i].phaseType == EpisodePhaseType.Combat)
            {
                asset.phases[i].enemyPrefab = enemyPrefab;
                asset.phases[i].negotiationDef = negotiationDef;
            }
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void ApplyRuleList(RuleManager mgr, List<RuleDefinition> rules)
    {
        if (mgr == null) return;
        if (rules == null) return;

        // Use SerializedObject so it persists like Inspector
        var so = new SerializedObject(mgr);
        var prop = so.FindProperty("activeRules");
        if (prop != null)
        {
            prop.ClearArray();
            for (int i = 0; i < rules.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = rules[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mgr);
        }
        else
        {
            // Fallback
            mgr.activeRules = rules;
            EditorUtility.SetDirty(mgr);
        }
    }
}
#endif
