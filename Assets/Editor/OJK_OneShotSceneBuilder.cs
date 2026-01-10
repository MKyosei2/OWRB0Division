// Assets/Editor/OJK_OneShotSceneBuilder.cs
// One-shot scene + assets builder for the prototype.
// - Creates Hierarchy
// - Wires Inspector refs
// - Generates minimal ScriptableObjects (Rule/Negotiation/Episode)
// - Generates simple Prefabs and Materials (colors)
//
// Run: Menu -> OJK -> One Shot -> Build Prototype Scene

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJikaProto;

public static class OJK_OneShotSceneBuilder
{
    private const string GenRoot = "Assets/OJKProtoGenerated";
    private const string GenMaterials = GenRoot + "/Materials";
    private const string GenSO = GenRoot + "/ScriptableObjects";
    private const string GenPrefabs = GenRoot + "/Prefabs";

    // Theme colors (wireframe-ish / hologram-ish)
    private static readonly Color Col_Back = new Color(0.05f, 0.06f, 0.08f, 1f);
    private static readonly Color Col_Panel = new Color(0.06f, 0.08f, 0.10f, 1f);
    private static readonly Color Col_Text = new Color(0.92f, 0.95f, 0.98f, 1f);
    private static readonly Color Col_Accent = new Color(0.25f, 0.90f, 0.85f, 1f);
    private static readonly Color Col_Enemy = new Color(1f, 0.35f, 0.35f, 1f);
    private static readonly Color Col_Evidence = new Color(0.90f, 0.70f, 0.25f, 1f);

    [MenuItem("OJK/One Shot/Build Prototype Scene")]
    public static void Build()
    {
        EnsureFolders();

        // Optional: start from a clean empty scene (comment out if you want to build into current scene)
        var scene = EnsureSceneReady();

        // --- Materials (colors) ---
        var matBack = CreateOrLoadMaterial(GenMaterials + "/MAT_Back.mat", Col_Panel);
        var matPlayer = CreateOrLoadMaterial(GenMaterials + "/MAT_Player.mat", Col_Text);
        var matEnemy = CreateOrLoadMaterial(GenMaterials + "/MAT_Enemy.mat", Col_Enemy);
        var matAccent = CreateOrLoadMaterial(GenMaterials + "/MAT_Accent.mat", Col_Accent);
        var matEvidence = CreateOrLoadMaterial(GenMaterials + "/MAT_Evidence.mat", Col_Evidence);

        // --- ScriptableObjects ---
        var ruleGaze = CreateOrLoadRule(
            GenSO + "/Rule_Gaze.asset",
            RuleType.GazeProhibition,
            "規約：視線禁止",
            startHidden: true,
            hint: "視線の気配",
            confirmReq: 2,
            clueTags: new[] { EvidenceTag.CCTV_Loop },
            gazeSeconds: 3.0f,
            repeatCount: 3,
            repeatWindow: 2.0f
        );

        var ruleRepeat = CreateOrLoadRule(
            GenSO + "/Rule_Repeat.asset",
            RuleType.RepeatAttackProhibition,
            "規約：連打禁止",
            startHidden: true,
            hint: "同じ打撃が続く",
            confirmReq: 2,
            clueTags: new[] { EvidenceTag.TicketGate_MemoryLoss },
            gazeSeconds: 3.0f,
            repeatCount: 3,
            repeatWindow: 2.0f
        );

        var negDef = CreateOrLoadNegotiation(GenSO + "/Negotiation_Case01.asset");
        var enemyPrefab = CreateOrLoadEnemyPrefab(GenPrefabs + "/Enemy_Case01.prefab", matEnemy, matAccent);
        var epDef = CreateOrLoadEpisode(GenSO + "/Episode_Case01.asset", negDef, enemyPrefab);

        // --- Root ---
        var root = GetOrCreate("OJK_ProtoScene");
        root.transform.SetAsFirstSibling();

        // --- System group ---
        var sys = GetOrCreate("_System", root.transform);

        // Managers (explicit in hierarchy; scripts also have runtime-ensure, but we make it visible and deterministic)
        var eventBus = EnsureComp<EventBus>(GetOrCreate("EventBus", sys.transform));
        var runLog = EnsureComp<RunLogManager>(GetOrCreate("RunLogManager", sys.transform));
        var invMgr = EnsureComp<InvestigationManager>(GetOrCreate("InvestigationManager", sys.transform));
        var ruleMgr = EnsureComp<RuleManager>(GetOrCreate("RuleManager", sys.transform));
        var negMgr = EnsureComp<NegotiationManager>(GetOrCreate("NegotiationManager", sys.transform));
        var infilMgr = EnsureComp<InfiltrationManager>(GetOrCreate("InfiltrationManager", sys.transform));
        var metaMgr = EnsureComp<CaseMetaManager>(GetOrCreate("CaseMetaManager", sys.transform));
        var subMgr = EnsureComp<SubtitleManager>(GetOrCreate("SubtitleManager", sys.transform));
        var feedback = EnsureComp<FeedbackManager>(GetOrCreate("FeedbackManager", sys.transform));
        EnsureComp<AudioSource>(feedback.gameObject);

        // Debug HUD
        var debugHud = EnsureComp<DebugHUD>(GetOrCreate("DebugHUD", sys.transform));
        EnsureComp<AudioSource>(debugHud.gameObject);

        // Flow + Episode + CombatDirector
        var flowGO = GetOrCreate("GameFlow", sys.transform);
        var flow = EnsureComp<GameFlowController>(flowGO);

        var episodeGO = GetOrCreate("Episode", sys.transform);
        var episode = EnsureComp<EpisodeController>(episodeGO);

        var combatGO = GetOrCreate("CombatDirector", sys.transform);
        var combat = EnsureComp<CombatDirector>(combatGO);

        // --- Camera group ---
        var camGroup = GetOrCreate("_Camera", root.transform);
        var camGO = GetOrCreate("Main Camera", camGroup.transform);
        var cam = EnsureComp<Camera>(camGO);
        cam.tag = "MainCamera";
        EnsureComp<AudioListener>(camGO);

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Col_Back;

        var camRig = EnsureComp<ThirdPersonCameraRig>(camGO);
        camRig.distance = 5.2f;
        camRig.pivotOffset = new Vector3(0f, 1.55f, 0f);
        camRig.minPitch = -20f;
        camRig.maxPitch = 62f;

        // --- Player group ---
        var playerGroup = GetOrCreate("_Player", root.transform);
        var playerGO = GetOrCreate("Player", playerGroup.transform);
        playerGO.transform.position = new Vector3(0f, 1f, -2f);

        // Visual
        var playerVis = EnsurePrimitiveChild(playerGO, "Model", PrimitiveType.Capsule);
        ApplyMat(playerVis, matPlayer);

        // Controller stack
        EnsureCharacterController(playerGO);
        var pc = EnsureComp<PlayerController>(playerGO);
        var pCombat = EnsureComp<PlayerCombat>(playerGO);
        var pHP = EnsureComp<PlayerHealth>(playerGO);
        var lockOn = EnsureComp<LockOnController>(playerGO);

        // camera target
        camRig.target = playerGO.transform;
        pc.cameraRoot = camGO.transform;

        // --- Spawns ---
        var spawns = GetOrCreate("_Spawns", root.transform);
        var playerSpawn = GetOrCreate("PlayerSpawn", spawns.transform);
        var enemySpawn = GetOrCreate("EnemySpawn", spawns.transform);

        playerSpawn.transform.position = new Vector3(0f, 0f, -2f);
        playerSpawn.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        enemySpawn.transform.position = new Vector3(0f, 0f, 2.6f);
        enemySpawn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // Wire CombatDirector inspector
        combat.playerSpawn = playerSpawn.transform;
        combat.enemySpawn = enemySpawn.transform;
        combat.negotiationRange = 2.2f;

        // --- Stage ---
        var stage = GetOrCreate("_Stage", root.transform);

        // Ground
        var ground = GetOrCreate("Ground", stage.transform);
        EnsureMeshPlane(ground);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(6f, 1f, 6f);
        ApplyMat(ground, matBack);
        EnsureCollider(ground);

        // Obstacles (simple pillars to break LoS for infiltration)
        var obs = GetOrCreate("Obstacles", stage.transform);
        CreatePillar(obs.transform, new Vector3(-2.0f, 0f, 0.2f), matBack);
        CreatePillar(obs.transform, new Vector3( 2.0f, 0f, 0.2f), matBack);
        CreatePillar(obs.transform, new Vector3( 0.0f, 0f, 1.0f), matBack);

        // --- Investigation points ---
        var invGroup = GetOrCreate("_Investigation", root.transform);

        var ev1 = CreateInvestigationPoint(invGroup.transform, "Evidence_CCTV_Loop", EvidenceTag.CCTV_Loop, new Vector3(-2.2f, 0.6f, -0.2f), matEvidence, requiresInfiltration: true);
        var ev2 = CreateInvestigationPoint(invGroup.transform, "Evidence_TicketGate_MemoryLoss", EvidenceTag.TicketGate_MemoryLoss, new Vector3( 2.2f, 0.6f, -0.2f), matEvidence, requiresInfiltration: false);

        // --- Security camera ---
        var secGroup = GetOrCreate("_Security", root.transform);
        var camSec = GetOrCreate("SecurityCam_01", secGroup.transform);
        camSec.transform.position = new Vector3(0f, 2.2f, -0.8f);
        camSec.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        var secVis = EnsurePrimitiveChild(camSec, "Model", PrimitiveType.Cube);
        secVis.transform.localScale = new Vector3(0.35f, 0.20f, 0.35f);
        ApplyMat(secVis, matAccent);

        var cone = EnsureComp<SecurityCameraCone>(camSec);
        cone.viewDistance = 7.5f;
        cone.viewAngle = 80f;
        cone.sweep = true;
        cone.sweepAngle = 110f;
        cone.sweepSpeed = 1.9f;
        cone.obstacleMask = ~0; // prototype: everything blocks LoS
        cone.reason = "Camera";
        cone.seenIntensity01 = 1.0f;

        // --- Prefab assets are already created; now wire Episode + Flow inspector ---
        episode.episode = epDef;
        episode.combatDirector = combat;
        episode.autoStart = false;

        flow.episode = episode;
        flow.player = pc;
        flow.playerCombat = pCombat;
        flow.lockOn = lockOn;
        flow.cameraRig = camRig;

        flow.gameTitle = "異界規約局 0時課";
        flow.subtitle = "CASE 01 : 終電のいない駅";
        flow.conceptLine = "“規約”が戦闘ルールを変える / 調査が交渉を変える";

        // --- Wire RuleManager inspector ---
        ruleMgr.activeRules = new List<RuleDefinition> { ruleGaze, ruleRepeat };

        // Light (optional but helps)
        EnsureDefaultLight(root.transform);

        // Mark dirty + save
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);

        Debug.Log("OJK OneShot: Prototype scene + assets built.");
    }

    // -------------------- Scene / Folders --------------------
    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "OJKProtoGenerated");
        EnsureFolder(GenRoot, "Materials");
        EnsureFolder(GenRoot, "ScriptableObjects");
        EnsureFolder(GenRoot, "Prefabs");
    }

    private static void EnsureFolder(string parent, string child)
    {
        var path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Scene EnsureSceneReady()
    {
        // Create a new empty scene (safe for one-shot)
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "OJK_ProtoScene";
        return scene;
    }

    // -------------------- Materials --------------------
    private static Material CreateOrLoadMaterial(string assetPath, Color c)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (m != null)
        {
            SetMatColor(m, c);
            EditorUtility.SetDirty(m);
            return m;
        }

        Shader sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Standard");

        m = new Material(sh);
        SetMatColor(m, c);
        AssetDatabase.CreateAsset(m, assetPath);
        return m;
    }

    private static void SetMatColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_Color")) m.color = c;
        else if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    private static void ApplyMat(GameObject go, Material mat)
    {
        if (!go || !mat) return;
        var r = go.GetComponentInChildren<Renderer>();
        if (r) r.sharedMaterial = mat;
    }

    // -------------------- ScriptableObjects --------------------
    private static RuleDefinition CreateOrLoadRule(
        string assetPath,
        RuleType type,
        string displayName,
        bool startHidden,
        string hint,
        int confirmReq,
        EvidenceTag[] clueTags,
        float gazeSeconds,
        int repeatCount,
        float repeatWindow)
    {
        var r = AssetDatabase.LoadAssetAtPath<RuleDefinition>(assetPath);
        if (r == null)
        {
            r = ScriptableObject.CreateInstance<RuleDefinition>();
            AssetDatabase.CreateAsset(r, assetPath);
        }

        r.ruleType = type;
        r.displayName = displayName;
        r.feedbackIntensity = 0.85f;

        r.startHidden = startHidden;
        r.hiddenLabel = "？？？";
        r.hintText = hint ?? "";
        r.confirmPointsRequired = Mathf.Max(1, confirmReq);
        r.clueEvidenceTags = clueTags ?? new EvidenceTag[0];

        // Type params
        r.gazeSecondsToViolate = Mathf.Max(0.2f, gazeSeconds);
        r.repeatCountToViolate = Mathf.Max(2, repeatCount);
        r.repeatWindowSeconds = Mathf.Max(0.25f, repeatWindow);

        EditorUtility.SetDirty(r);
        return r;
    }

    private static NegotiationDefinition CreateOrLoadNegotiation(string assetPath)
    {
        var d = AssetDatabase.LoadAssetAtPath<NegotiationDefinition>(assetPath);
        if (d == null)
        {
            d = ScriptableObject.CreateInstance<NegotiationDefinition>();
            AssetDatabase.CreateAsset(d, assetPath);
        }

        d.title = "停戦交渉";
        d.prompt = "怪異は崩れている。今なら条件次第で収束できる。";

        d.cooldownSeconds = 12f;
        d.failEnrage = true;

        d.sealRitualEnabled = true;
        d.sealRitualSteps = 4;
        d.sealStepTimeSeconds = 1.1f;
        d.sealFailAdminCost = 0.06f;

        // Gate design (prototype):
        // - Truce needs 1 evidence
        // - Contract needs 2 evidences
        // - Seal needs 2 evidences + ritual
        d.options = new NegotiationOption[]
        {
            new NegotiationOption{
                label="停戦（期限付き）",
                description="期限付きの停戦を提案する（次周回メリット：暫定許可証）",
                baseChance=1f,
                evidenceBonusTags=new[]{ EvidenceTag.CCTV_Loop, EvidenceTag.TicketGate_MemoryLoss },
                minEvidenceToSucceed=1,
                success=NegotiationOutcome.Truce,
                isEmergencyOption=false,
                consumesArbitrationPass=false,
                extraAdminCost=0.12f,
                extraTruceDebt=1,
            },
            new NegotiationOption{
                label="契約（協力）",
                description="条件付きの協力を取り付ける（次周回メリット：影の遮蔽）",
                baseChance=1f,
                evidenceBonusTags=new[]{ EvidenceTag.CCTV_Loop, EvidenceTag.TicketGate_MemoryLoss },
                minEvidenceToSucceed=2,
                success=NegotiationOutcome.Contract,
                isEmergencyOption=false,
                consumesArbitrationPass=false,
                extraAdminCost=0.22f,
                extraTruceDebt=0,
            },
            new NegotiationOption{
                label="封印（儀式）",
                description="儀式で封を結ぶ（入力ミニゲームあり）",
                baseChance=1f,
                evidenceBonusTags=new[]{ EvidenceTag.CCTV_Loop, EvidenceTag.TicketGate_MemoryLoss },
                minEvidenceToSucceed=2,
                success=NegotiationOutcome.Seal,
                isEmergencyOption=false,
                consumesArbitrationPass=false,
                extraAdminCost=0.10f,
                extraTruceDebt=-1,
            },
        };

        EditorUtility.SetDirty(d);
        return d;
    }

    private static EpisodeDefinition CreateOrLoadEpisode(string assetPath, NegotiationDefinition neg, GameObject enemyPrefab)
    {
        var e = AssetDatabase.LoadAssetAtPath<EpisodeDefinition>(assetPath);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<EpisodeDefinition>();
            AssetDatabase.CreateAsset(e, assetPath);
        }

        e.episodeName = "終電のいない駅（プロト）";

        e.phases = new EpisodePhase[]
        {
            new EpisodePhase{ phaseType=EpisodePhaseType.Intro, title="導入", description="Enterで開始" },
            new EpisodePhase{ phaseType=EpisodePhaseType.Investigation, title="調査", description="調査ポイントに近づいてEで証拠を取得。証拠が揃ったらEnter。", targetEvidenceCount=2 },
            new EpisodePhase{ phaseType=EpisodePhaseType.Combat, title="収束作戦", description="規約→崩し→交渉（F）で決着へ。", enemyPrefab=enemyPrefab, negotiationDef=neg },
            new EpisodePhase{ phaseType=EpisodePhaseType.Outro, title="後日談", description="Enterで完了" },
        };

        // keep default outro texts in script (asset can keep existing)
        if (e.outroTexts == null || e.outroTexts.Length == 0)
        {
            e.outroTexts = new OutroTextSet[]
            {
                new OutroTextSet{ outcome=NegotiationOutcome.Truce,   line1="駅員は『何も起きていない』と言い張った。", line2="あなたはメモに“期限”とだけ書き残す。", line3="次の終電が来るまで、猶予は短い。" },
                new OutroTextSet{ outcome=NegotiationOutcome.Contract, line1="怪異は条件付きで協力を受け入れた。", line2="代償は“見ないこと”。あなたは頷く。", line3="駅の影が、味方になった気がした。" },
                new OutroTextSet{ outcome=NegotiationOutcome.Seal,    line1="封印は完了した。空気が軽くなる。", line2="ただし“封”は永久ではないと直感する。", line3="あなたは次の場所へ向かう準備を始めた。" },
                new OutroTextSet{ outcome=NegotiationOutcome.Slay,    line1="討伐。静寂だけが残った。", line2="監視映像は、肝心な瞬間だけ欠けていた。", line3="胸に、割り切れない違和感が残る。" },
            };
        }

        EditorUtility.SetDirty(e);
        return e;
    }

    // -------------------- Prefabs --------------------
    private static GameObject CreateOrLoadEnemyPrefab(string prefabPath, Material matEnemy, Material matAccent)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "Enemy_Case01";
        enemy.transform.position = new Vector3(0f, 0.9f, 2.6f);
        enemy.transform.localScale = new Vector3(1.05f, 1.1f, 1.05f);

        // Make collider non-trigger
        var col = enemy.GetComponent<CapsuleCollider>();
        col.isTrigger = false;

        // Add required components
        var dmg = EnsureComp<Damageable>(enemy);
        dmg.maxHp = 140f;

        var brk = EnsureComp<Breakable>(enemy);
        brk.maxBreak = 110f;
        brk.brokenDuration = 6f;

        var ai = EnsureComp<EnemyController>(enemy);
        ai.moveSpeed = 3.2f;
        ai.preferredRange = 2.6f;
        ai.attackRange = 1.8f;
        ai.attackDamage = 14f;

        ApplyMat(enemy, matEnemy);

        // Accent child (weak point marker)
        var weak = EnsurePrimitiveChild(enemy, "WeakPoint", PrimitiveType.Sphere);
        weak.transform.localPosition = new Vector3(0f, 1.4f, 0.35f);
        weak.transform.localScale = Vector3.one * 0.22f;
        var weakCol = weak.GetComponent<SphereCollider>();
        if (weakCol) weakCol.enabled = false;
        ApplyMat(weak, matAccent);

        // Save as prefab
        var prefab = PrefabUtility.SaveAsPrefabAsset(enemy, prefabPath);
        Object.DestroyImmediate(enemy);
        return prefab;
    }

    // -------------------- Helpers (GameObjects) --------------------
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
            if (t != null) go = t.gameObject;
        }

        if (go == null)
        {
            go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
        }
        return go;
    }

    private static T EnsureComp<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = go.AddComponent<T>();
        return c;
    }

    private static void EnsureCharacterController(GameObject go)
    {
        var cc = go.GetComponent<CharacterController>();
        if (cc != null) return;

        // Remove primitive colliders that conflict with CC
        var cap = go.GetComponent<CapsuleCollider>();
        if (cap != null) Object.DestroyImmediate(cap);

        cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
    }

    private static GameObject EnsurePrimitiveChild(GameObject parent, string childName, PrimitiveType type)
    {
        var t = parent.transform.Find(childName);
        if (t != null) return t.gameObject;

        var prim = GameObject.CreatePrimitive(type);
        prim.name = childName;
        prim.transform.SetParent(parent.transform, false);

        // primitives come with colliders; for visuals we usually disable to avoid odd hits
        var c = prim.GetComponent<Collider>();
        if (c) c.enabled = false;

        return prim;
    }

    private static void EnsureMeshPlane(GameObject go)
    {
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mf == null || mr == null)
        {
            var prim = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var mesh = prim.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(prim);

            mf = EnsureComp<MeshFilter>(go);
            mr = EnsureComp<MeshRenderer>(go);
            mf.sharedMesh = mesh;
        }
    }

    private static void EnsureCollider(GameObject go)
    {
        if (go.GetComponent<Collider>() == null)
            go.AddComponent<BoxCollider>();
    }

    private static void CreatePillar(Transform parent, Vector3 pos, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Pillar";
        go.transform.SetParent(parent, false);
        go.transform.position = pos + new Vector3(0f, 1.0f, 0f);
        go.transform.localScale = new Vector3(0.6f, 2.0f, 0.6f);
        ApplyMat(go, mat);
    }

    private static GameObject CreateInvestigationPoint(Transform parent, string name, EvidenceTag tag, Vector3 pos, Material mat, bool requiresInfiltration)
    {
        var go = GetOrCreate(name, parent);
        go.transform.position = pos;

        // Visual
        var model = EnsurePrimitiveChild(go, "Model", PrimitiveType.Cube);
        model.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        ApplyMat(model, mat);

        // Collider for trigger (script sets isTrigger=true in Awake but we also ensure one exists)
        var col = go.GetComponent<Collider>();
        if (col == null) col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;

        var ip = EnsureComp<InvestigationPoint>(go);
        ip.evidenceTag = tag;
        ip.interactRadius = 1.6f;
        ip.requiresInfiltration = requiresInfiltration;
        ip.blockedDuringLockdown = true;

        return go;
    }

    private static void EnsureDefaultLight(Transform root)
    {
        // Create a simple directional light if none exists
        var existing = Object.FindObjectOfType<Light>();
        if (existing != null) return;

        var go = new GameObject("Directional Light");
        go.transform.SetParent(root, false);
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        l.color = new Color(0.95f, 0.98f, 1f, 1f);
    }
}
#endif
