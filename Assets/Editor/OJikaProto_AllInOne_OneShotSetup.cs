// Assets/Editor/OJikaProto_AllInOne_OneShotSetup.cs
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
            // 1) New scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            EnsureFolder(AssetDir);

            // 2) Lighting / Environment
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.28f, 1f);

            // 3) Core Singletons
            new GameObject("GameBootstrapper").AddComponent<OJikaProto.GameBootstrapper>();
            new GameObject("EventBus").AddComponent<OJikaProto.EventBus>();
            new GameObject("RunLogManager").AddComponent<OJikaProto.RunLogManager>();
            new GameObject("InvestigationManager").AddComponent<OJikaProto.InvestigationManager>();
            var ruleMgr = new GameObject("RuleManager").AddComponent<OJikaProto.RuleManager>();
            new GameObject("NegotiationManager").AddComponent<OJikaProto.NegotiationManager>();

            // 4) Ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ApplyColor(ground, new Color(0.12f, 0.12f, 0.14f, 1f));

            // 5) Spawns
            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.position = new Vector3(0, 1, -2);

            var enemySpawn = new GameObject("EnemySpawn").transform;
            enemySpawn.position = new Vector3(0, 1, 3);

            // 6) Player
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = playerSpawn.position;
            ApplyColor(player, new Color(0.25f, 0.55f, 1.0f, 1f));

            // primitive has collider → use CharacterController. Remove primitive collider.
            var primCol = player.GetComponent<Collider>();
            if (primCol) Object.DestroyImmediate(primCol);

            player.AddComponent<CharacterController>();
            var pc = player.AddComponent<OJikaProto.PlayerController>();
            player.AddComponent<OJikaProto.PlayerHealth>();
            var pCombat = player.AddComponent<OJikaProto.PlayerCombat>();
            var lockOn = player.AddComponent<OJikaProto.LockOnController>();

            // 7) Camera
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
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);

            var rig = cam.GetComponent<OJikaProto.ThirdPersonCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<OJikaProto.ThirdPersonCameraRig>();
            rig.target = player.transform;

            // 8) HUD
            new GameObject("HUD").AddComponent<OJikaProto.DebugHUD>();

            // 9) CombatDirector（★ここが文字化けしていたので修正）
            var cd = new GameObject("CombatDirector").AddComponent<OJikaProto.CombatDirector>();
            cd.playerSpawn = playerSpawn;
            cd.enemySpawn = enemySpawn;

            // 10) Enemy Prefab (auto-create)
            var enemyPrefab = EnsureEnemyPrefab($"{AssetDir}/Enemy_Case01.prefab");

            // 11) NegotiationDefinition (auto-create + set)
            var neg = EnsureAsset<OJikaProto.NegotiationDefinition>(
                $"{AssetDir}/NegotiationDefinition_Case01.asset",
                () => ScriptableObject.CreateInstance<OJikaProto.NegotiationDefinition>()
            );
            EnsureNegotiationDefaults(neg);
            EditorUtility.SetDirty(neg);

            // 12) EpisodeDefinition (auto-create + wire combat phase)
            var ep = EnsureAsset<OJikaProto.EpisodeDefinition>(
                $"{AssetDir}/EpisodeDefinition_Case01.asset",
                () => ScriptableObject.CreateInstance<OJikaProto.EpisodeDefinition>()
            );

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

            // 13) Rules (auto-create)
            var ruleGaze = EnsureAsset<OJikaProto.RuleDefinition>(
                $"{AssetDir}/Rule_Gaze.asset",
                () =>
                {
                    var r = ScriptableObject.CreateInstance<OJikaProto.RuleDefinition>();
                    r.ruleType = OJikaProto.RuleType.GazeProhibition;
                    r.displayName = "視線を合わせるな";
                    r.gazeSecondsToViolate = 3.0f;
                    r.feedbackIntensity = 0.85f;
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
                    return r;
                });

            ruleMgr.activeRules.Clear();
            ruleMgr.activeRules.Add(ruleGaze);
            ruleMgr.activeRules.Add(ruleRepeat);

            // 14) Investigation points
            var ip1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ip1.name = "InvestigationPoint_A";
            ip1.transform.position = new Vector3(-2, 0.5f, -1);
            ApplyColor(ip1, new Color(0.95f, 0.85f, 0.25f, 1f));
            ip1.AddComponent<OJikaProto.InvestigationPoint>().evidenceTag = OJikaProto.EvidenceTag.CCTV_Loop;

            var ip2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ip2.name = "InvestigationPoint_B";
            ip2.transform.position = new Vector3(2, 0.5f, -1);
            ApplyColor(ip2, new Color(0.25f, 0.95f, 0.55f, 1f));
            ip2.AddComponent<OJikaProto.InvestigationPoint>().evidenceTag = OJikaProto.EvidenceTag.Clock_DeviceHint;

            // 15) EpisodeController
            var ec = new GameObject("EpisodeController").AddComponent<OJikaProto.EpisodeController>();
            ec.episode = ep;
            ec.combatDirector = cd;
            ec.autoStart = false; // Flowが開始

            // 16) Flow Controller (Title/Complete)
            var flow = new GameObject("GameFlow").AddComponent<OJikaProto.GameFlowController>();
            flow.episode = ec;
            flow.player = pc;
            flow.playerCombat = pCombat;
            flow.lockOn = lockOn;
            flow.cameraRig = rig;

            // 17) Save
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("OJikaProto: ALL-IN-ONE One Shot Setup completed. Press Play.");
        }

        // -------------------------
        // Helpers
        // -------------------------
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            if (parts.Length < 2) return;

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
            if (neg.options == null || neg.options.Length < 3)
            {
                neg.options = new OJikaProto.NegotiationOption[3]
                {
                    new OJikaProto.NegotiationOption{ label="停戦（期限付き）", baseChance=0.65f, success=OJikaProto.NegotiationOutcome.Truce },
                    new OJikaProto.NegotiationOption{ label="契約（協力）", baseChance=0.50f, success=OJikaProto.NegotiationOutcome.Contract },
                    new OJikaProto.NegotiationOption{ label="封印（儀式）", baseChance=0.45f, success=OJikaProto.NegotiationOutcome.Seal },
                };
            }

            // “調査が交渉に効く”が見えるように
            neg.options[0].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.CCTV_Loop };
            neg.options[1].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.StationStaff_Avoid, OJikaProto.EvidenceTag.Clock_DeviceHint };
            neg.options[2].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.TicketGate_MemoryLoss, OJikaProto.EvidenceTag.Clock_DeviceHint };
        }

        private static void ApplyColor(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;

            // Standard が無い環境もあるのでフォールバック
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
