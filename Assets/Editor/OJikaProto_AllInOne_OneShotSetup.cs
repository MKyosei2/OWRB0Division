#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJikaProto.EditorTools
{
    public static class OJikaProto_AllInOne_OneShotSetup
    {
        private const string AssetDir = "Assets/OJikaProtoAssets";

        [MenuItem("Tools/OJikaProto/ALL-IN-ONE One Shot Setup (Rebuild Scene)")]
        public static void RebuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            EnsureFolder(AssetDir);

            // ----- Lighting / Ambient -----
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.28f, 1f);

            // ----- Core managers -----
            new GameObject("EventBus").AddComponent<OJikaProto.EventBus>();
            new GameObject("RunLogManager").AddComponent<OJikaProto.RunLogManager>();
            new GameObject("InvestigationManager").AddComponent<OJikaProto.InvestigationManager>();

            var ruleMgr = new GameObject("RuleManager").AddComponent<OJikaProto.RuleManager>();
            new GameObject("NegotiationManager").AddComponent<OJikaProto.NegotiationManager>();
            new GameObject("CaseMetaManager").AddComponent<OJikaProto.CaseMetaManager>();
            new GameObject("InfiltrationManager").AddComponent<OJikaProto.InfiltrationManager>();

            // ----- Ground -----
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ApplyColor(ground, new Color(0.12f, 0.12f, 0.14f, 1f));

            // ----- Spawns -----
            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.position = new Vector3(0, 1, -2);

            var enemySpawn = new GameObject("EnemySpawn").transform;
            enemySpawn.position = new Vector3(0, 1, 3);

            // ----- Player -----
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = playerSpawn.position;
            ApplyColor(player, new Color(0.25f, 0.55f, 1.0f, 1f));

            // capsule colliderはCharacterControllerに置き換える
            var primCol = player.GetComponent<Collider>();
            if (primCol) Object.DestroyImmediate(primCol);

            player.AddComponent<CharacterController>();
            var pc = player.AddComponent<OJikaProto.PlayerController>();
            player.AddComponent<OJikaProto.PlayerHealth>();
            var pCombat = player.AddComponent<OJikaProto.PlayerCombat>();
            var lockOn = player.AddComponent<OJikaProto.LockOnController>();
            player.AddComponent<OJikaProto.ContractBoonAbility>();

            // ----- Camera -----
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
                camGO.tag = "MainCamera";
            }

            cam.gameObject.name = "Main Camera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.04f, 1f);

            // 追従リグ
            var rig = cam.GetComponent<OJikaProto.ThirdPersonCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<OJikaProto.ThirdPersonCameraRig>();
            rig.target = player.transform;

            // デモ用カット割り（存在する場合のみ）
            if (cam.GetComponent<OJikaProto.Proto_CameraDirector>() == null)
                cam.gameObject.AddComponent<OJikaProto.Proto_CameraDirector>();

            // ✅ 撮影救済：F11でルート固定
            if (cam.GetComponent<OJikaProto.Proto_CameraRoute>() == null)
                cam.gameObject.AddComponent<OJikaProto.Proto_CameraRoute>();

            // ----- HUD -----
            var hud = new GameObject("HUD");
            hud.AddComponent<OJikaProto.DebugHUD>();

            // ✅ 重要：FeedbackManager は Proto_Feedback.cs 内で実装されているが、
            // クラス名/名前空間は OJikaProto.FeedbackManager なのでここはこれでOK
            hud.AddComponent<OJikaProto.FeedbackManager>();

            hud.AddComponent<OJikaProto.SubtitleManager>();
            hud.AddComponent<OJikaProto.Proto_DebugTools>();
            hud.AddComponent<OJikaProto.Proto_DemoMacro>();
            hud.AddComponent<OJikaProto.Proto_AutoPilot>();

            // ✅ 録画向けHUD + ガード
            hud.AddComponent<OJikaProto.Proto_CaptureHUD>();
            hud.AddComponent<OJikaProto.Proto_CaptureGuard>();

            // ----- CombatDirector -----
            var cd = new GameObject("CombatDirector").AddComponent<OJikaProto.CombatDirector>();
            cd.playerSpawn = playerSpawn;
            cd.enemySpawn = enemySpawn;

            // ----- Enemy prefab -----
            var enemyPrefab = EnsureEnemyPrefab($"{AssetDir}/Enemy_Case01.prefab");

            // ----- Negotiation asset -----
            var neg = EnsureAsset<OJikaProto.NegotiationDefinition>(
                $"{AssetDir}/NegotiationDefinition_Case01.asset",
                () => ScriptableObject.CreateInstance<OJikaProto.NegotiationDefinition>());

            EnsureNegotiationDefaults(neg);
            EditorUtility.SetDirty(neg);

            // ----- Episode asset -----
            var ep = EnsureAsset<OJikaProto.EpisodeDefinition>(
                $"{AssetDir}/EpisodeDefinition_Case01.asset",
                () => ScriptableObject.CreateInstance<OJikaProto.EpisodeDefinition>());

            // Combatフェーズに prefab/neg を紐付け
            if (ep.phases != null)
            {
                for (int i = 0; i < ep.phases.Length; i++)
                {
                    if (ep.phases[i].phaseType == OJikaProto.EpisodePhaseType.Combat)
                    {
                        ep.phases[i].enemyPrefab = enemyPrefab;
                        ep.phases[i].negotiationDef = neg;
                    }
                }
            }
            EditorUtility.SetDirty(ep);

            // ----- Rules (ScriptableObject) -----
            var ruleGaze = EnsureAsset<OJikaProto.RuleDefinition>(
                $"{AssetDir}/Rule_Gaze.asset",
                () =>
                {
                    var r = ScriptableObject.CreateInstance<OJikaProto.RuleDefinition>();
                    r.ruleType = OJikaProto.RuleType.GazeProhibition;
                    r.displayName = "視線を合わせるな";
                    r.gazeSecondsToViolate = 3.0f;
                    r.feedbackIntensity = 0.85f;
                    r.startHidden = true;
                    r.hiddenLabel = "？？？";
                    r.hintText = "（視線/監視の気配）";
                    r.confirmPointsRequired = 2;
                    r.clueEvidenceTags = new[] { OJikaProto.EvidenceTag.StationStaff_Avoid };
                    return r;
                });

            var ruleRepeat = EnsureAsset<OJikaProto.RuleDefinition>(
                $"{AssetDir}/Rule_Repeat.asset",
                () =>
                {
                    var r = ScriptableObject.CreateInstance<OJikaProto.RuleDefinition>();
                    r.ruleType = OJikaProto.RuleType.RepeatAttackProhibition;
                    r.displayName = "同じ手を続けるな";
                    r.repeatCountToViolate = 3;
                    r.repeatWindowSeconds = 2.0f;
                    r.feedbackIntensity = 0.85f;
                    r.startHidden = true;
                    r.hiddenLabel = "？？？";
                    r.hintText = "（反復/時計の違和感）";
                    r.confirmPointsRequired = 2;
                    r.clueEvidenceTags = new[] { OJikaProto.EvidenceTag.CCTV_Loop, OJikaProto.EvidenceTag.Clock_DeviceHint };
                    return r;
                });

            // Step1: 規約は調査で特定（伏せ字＋解析）
            ruleGaze.startHidden = true;
            ruleGaze.hiddenLabel = "？？？";
            ruleGaze.hintText = "（視線/監視の気配）";
            ruleGaze.confirmPointsRequired = 2;
            ruleGaze.clueEvidenceTags = new[] { OJikaProto.EvidenceTag.StationStaff_Avoid };
            EditorUtility.SetDirty(ruleGaze);

            ruleRepeat.startHidden = true;
            ruleRepeat.hiddenLabel = "？？？";
            ruleRepeat.hintText = "（反復/時計の違和感）";
            ruleRepeat.confirmPointsRequired = 2;
            ruleRepeat.clueEvidenceTags = new[] { OJikaProto.EvidenceTag.CCTV_Loop, OJikaProto.EvidenceTag.Clock_DeviceHint };
            EditorUtility.SetDirty(ruleRepeat);

            // RuleManagerへ登録（初期ルール）
            ruleMgr.activeRules.Clear();
            ruleMgr.activeRules.Add(ruleGaze);
            ruleMgr.activeRules.Add(ruleRepeat);

            // ----- Investigation points -----
            var ip1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ip1.name = "InvestigationPoint_A";
            ip1.transform.position = new Vector3(-2, 0.5f, -1);
            ApplyColor(ip1, new Color(0.95f, 0.85f, 0.25f, 1f));
            var ip1p = ip1.AddComponent<OJikaProto.InvestigationPoint>();
            ip1p.evidenceTag = OJikaProto.EvidenceTag.CCTV_Loop;
            ip1p.requiresInfiltration = true;

            var ip2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ip2.name = "InvestigationPoint_B";
            ip2.transform.position = new Vector3(2, 0.5f, -1);
            ApplyColor(ip2, new Color(0.25f, 0.95f, 0.55f, 1f));
            ip2.AddComponent<OJikaProto.InvestigationPoint>().evidenceTag = OJikaProto.EvidenceTag.Clock_DeviceHint;

            // ----- Security Cameras (Step5) -----
            var camA = new GameObject("SecurityCamera_A");
            camA.transform.position = new Vector3(-3.5f, 2.2f, -0.2f);
            camA.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var coneA = camA.AddComponent<OJikaProto.SecurityCameraCone>();
            coneA.head = camA.transform;
            coneA.viewDistance = 8f;
            coneA.viewAngle = 80f;
            coneA.reason = "Camera A";
            coneA.seenIntensity01 = 1f;

            var camB = new GameObject("SecurityCamera_B");
            camB.transform.position = new Vector3(3.5f, 2.2f, -0.2f);
            camB.transform.rotation = Quaternion.Euler(0f, -45f, 0f);
            var coneB = camB.AddComponent<OJikaProto.SecurityCameraCone>();
            coneB.head = camB.transform;
            coneB.viewDistance = 7f;
            coneB.viewAngle = 75f;
            coneB.reason = "Camera B";
            coneB.seenIntensity01 = 0.9f;

            // 遮蔽物（影の遮蔽/Qの効果が分かりやすいよう、柱も置く）
            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "Pillar";
            pillar.transform.position = new Vector3(-2.6f, 1.0f, -0.5f);
            pillar.transform.localScale = new Vector3(0.5f, 1.0f, 0.5f);
            ApplyColor(pillar, new Color(0.18f, 0.18f, 0.20f, 1f));

            // ----- EpisodeController -----
            var ec = new GameObject("EpisodeController").AddComponent<OJikaProto.EpisodeController>();
            ec.episode = ep;
            ec.combatDirector = cd;
            ec.autoStart = false;

            // ----- Flow -----
            var flow = new GameObject("GameFlow").AddComponent<OJikaProto.GameFlowController>();
            flow.episode = ec;
            flow.player = pc;
            flow.playerCombat = pCombat;
            flow.lockOn = lockOn;
            flow.cameraRig = rig;

            // 保存
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("OJikaProto: ALL-IN-ONE One Shot Setup completed. Press Play.");
        }

        // ---------- helpers ----------
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static T EnsureAsset<T>(string assetPath, System.Func<T> create) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (a != null) return a;
            a = create();
            AssetDatabase.CreateAsset(a, assetPath);
            return a;
        }

        private static GameObject EnsureEnemyPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null) return prefab;

            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            temp.name = "Enemy_Case01_Temp";
            ApplyColor(temp, new Color(1.0f, 0.28f, 0.28f, 1f));

            temp.AddComponent<OJikaProto.Damageable>();
            temp.AddComponent<OJikaProto.Breakable>();
            temp.AddComponent<OJikaProto.EnemyController>();

            prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static void EnsureNegotiationDefaults(OJikaProto.NegotiationDefinition neg)
        {
            // optionsが未設定なら最低3択を生成
            if (neg.options == null || neg.options.Length < 3)
            {
                neg.options = new OJikaProto.NegotiationOption[3]
                {
                    new OJikaProto.NegotiationOption
                    {
                        label = "停戦（期限付き）",
                        baseChance = 0.65f,
                        success = OJikaProto.NegotiationOutcome.Truce
                    },
                    new OJikaProto.NegotiationOption
                    {
                        label = "契約（協力）",
                        baseChance = 0.50f,
                        success = OJikaProto.NegotiationOutcome.Contract
                    },
                    new OJikaProto.NegotiationOption
                    {
                        label = "封印（儀式）",
                        baseChance = 0.45f,
                        success = OJikaProto.NegotiationOutcome.Seal
                    },
                };
            }

            // 有利証拠のサンプル
            neg.options[0].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.CCTV_Loop };
            neg.options[1].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.StationStaff_Avoid, OJikaProto.EvidenceTag.Clock_DeviceHint };
            neg.options[2].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.TicketGate_MemoryLoss, OJikaProto.EvidenceTag.Clock_DeviceHint };

            // Step3: 成立条件（最低証拠数）
            neg.options[0].minEvidenceToSucceed = 1;
            neg.options[1].minEvidenceToSucceed = 2;
            neg.options[2].minEvidenceToSucceed = 2;

            // Step4: 封印は儀式（入力ミニゲーム）
            neg.sealRitualEnabled = true;
            neg.sealRitualSteps = 4;
            neg.sealStepTimeSeconds = 1.1f;
            neg.sealFailAdminCost = 0.06f;
        }

        private static void ApplyColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (!r) return;

            Shader sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return;

            var mat = new Material(sh);
            mat.color = c;
            r.sharedMaterial = mat;
        }
    }
}
#endif
