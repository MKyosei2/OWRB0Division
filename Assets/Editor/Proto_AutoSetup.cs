// Assets/Editor/Proto_AutoSetup.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJikaProto.EditorTools
{
    public static class ProtoAutoSetup
    {
        [MenuItem("Tools/OJikaProto/Create Full Prototype Scene")]
        public static void CreateFullPrototypeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            const string assetDir = "Assets/OJikaProtoAssets";
            if (!AssetDatabase.IsValidFolder(assetDir))
                AssetDatabase.CreateFolder("Assets", "OJikaProtoAssets");

            new GameObject("GameBootstrapper").AddComponent<OJikaProto.GameBootstrapper>();
            new GameObject("EventBus").AddComponent<OJikaProto.EventBus>();
            new GameObject("RunLogManager").AddComponent<OJikaProto.RunLogManager>();
            new GameObject("InvestigationManager").AddComponent<OJikaProto.InvestigationManager>();
            var rm = new GameObject("RuleManager").AddComponent<OJikaProto.RuleManager>();
            new GameObject("NegotiationManager").AddComponent<OJikaProto.NegotiationManager>();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 1, -2);
            Object.DestroyImmediate(player.GetComponent<Collider>());
            player.AddComponent<CharacterController>();
            player.AddComponent<OJikaProto.PlayerController>();
            player.AddComponent<OJikaProto.PlayerHealth>();
            player.AddComponent<OJikaProto.PlayerCombat>();
            player.AddComponent<OJikaProto.LockOnController>();

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<AudioListener>();
            }
            cam.gameObject.name = "Main Camera";
            var rig = cam.gameObject.GetComponent<OJikaProto.ThirdPersonCameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<OJikaProto.ThirdPersonCameraRig>();
            rig.target = player.transform;

            var playerSpawn = new GameObject("PlayerSpawn").transform;
            playerSpawn.position = new Vector3(0, 1, -2);

            var enemySpawn = new GameObject("EnemySpawn").transform;
            enemySpawn.position = new Vector3(0, 1, 3);

            string enemyPrefabPath = $"{assetDir}/Enemy_Case01.prefab";
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPath);
            if (enemyPrefab == null)
            {
                var enemyTemp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemyTemp.name = "Enemy_Case01_Temp";
                enemyTemp.AddComponent<OJikaProto.Damageable>();
                enemyTemp.AddComponent<OJikaProto.Breakable>();
                enemyTemp.AddComponent<OJikaProto.EnemyController>();

                enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemyTemp, enemyPrefabPath);
                Object.DestroyImmediate(enemyTemp);
            }

            // HUD
            new GameObject("HUD").AddComponent<OJikaProto.DebugHUD>();

            // CombatDirector
            var cd = new GameObject("CombatDirector").AddComponent<OJikaProto.CombatDirector>();
            cd.playerSpawn = playerSpawn;
            cd.enemySpawn = enemySpawn;

            // Negotiation asset
            var negPath = $"{assetDir}/NegotiationDefinition_Case01.asset";
            var neg = AssetDatabase.LoadAssetAtPath<OJikaProto.NegotiationDefinition>(negPath);
            if (neg == null)
            {
                neg = ScriptableObject.CreateInstance<OJikaProto.NegotiationDefinition>();
                AssetDatabase.CreateAsset(neg, negPath);
            }

            if (neg.options == null || neg.options.Length < 3)
            {
                neg.options = new OJikaProto.NegotiationOption[3]
                {
                    new OJikaProto.NegotiationOption{ label="停戦（期限付き）", baseChance=0.65f, success=OJikaProto.NegotiationOutcome.Truce },
                    new OJikaProto.NegotiationOption{ label="契約（協力）", baseChance=0.50f, success=OJikaProto.NegotiationOutcome.Contract },
                    new OJikaProto.NegotiationOption{ label="封印（儀式）", baseChance=0.45f, success=OJikaProto.NegotiationOutcome.Seal },
                };
            }
            neg.options[0].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.CCTV_Loop };
            neg.options[1].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.StationStaff_Avoid, OJikaProto.EvidenceTag.Clock_DeviceHint };
            neg.options[2].evidenceBonusTags = new[] { OJikaProto.EvidenceTag.TicketGate_MemoryLoss, OJikaProto.EvidenceTag.Clock_DeviceHint };
            EditorUtility.SetDirty(neg);

            // Episode asset
            var epPath = $"{assetDir}/EpisodeDefinition_Case01.asset";
            var ep = AssetDatabase.LoadAssetAtPath<OJikaProto.EpisodeDefinition>(epPath);
            if (ep == null)
            {
                ep = ScriptableObject.CreateInstance<OJikaProto.EpisodeDefinition>();
                AssetDatabase.CreateAsset(ep, epPath);
            }

            for (int i = 0; i < ep.phases.Length; i++)
            {
                if (ep.phases[i].phaseType == OJikaProto.EpisodePhaseType.Combat)
                {
                    ep.phases[i].enemyPrefab = enemyPrefab;
                    ep.phases[i].negotiationDef = neg;
                }
            }
            EditorUtility.SetDirty(ep);

            // Rules
            var ruleAPath = $"{assetDir}/Rule_Gaze.asset";
            var ruleA = AssetDatabase.LoadAssetAtPath<OJikaProto.RuleDefinition>(ruleAPath);
            if (ruleA == null)
            {
                ruleA = ScriptableObject.CreateInstance<OJikaProto.RuleDefinition>();
                ruleA.ruleType = OJikaProto.RuleType.GazeProhibition;
                ruleA.displayName = "視線を合わせるな";
                ruleA.gazeSecondsToViolate = 3.0f;
                AssetDatabase.CreateAsset(ruleA, ruleAPath);
            }

            var ruleBPath = $"{assetDir}/Rule_Repeat.asset";
            var ruleB = AssetDatabase.LoadAssetAtPath<OJikaProto.RuleDefinition>(ruleBPath);
            if (ruleB == null)
            {
                ruleB = ScriptableObject.CreateInstance<OJikaProto.RuleDefinition>();
                ruleB.ruleType = OJikaProto.RuleType.RepeatAttackProhibition;
                ruleB.displayName = "同じ手を続けるな";
                ruleB.repeatCountToViolate = 3;
                ruleB.repeatWindowSeconds = 2.0f;
                AssetDatabase.CreateAsset(ruleB, ruleBPath);
            }

            rm.activeRules.Clear();
            rm.activeRules.Add(ruleA);
            rm.activeRules.Add(ruleB);

            // Investigation points（2個）
            var ip1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ip1.name = "InvestigationPoint_A";
            ip1.transform.position = new Vector3(-2, 0.5f, -1);
            ip1.AddComponent<OJikaProto.InvestigationPoint>().evidenceTag = OJikaProto.EvidenceTag.CCTV_Loop;

            var ip2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ip2.name = "InvestigationPoint_B";
            ip2.transform.position = new Vector3(2, 0.5f, -1);
            ip2.AddComponent<OJikaProto.InvestigationPoint>().evidenceTag = OJikaProto.EvidenceTag.Clock_DeviceHint;

            // EpisodeController
            var ec = new GameObject("EpisodeController").AddComponent<OJikaProto.EpisodeController>();
            ec.episode = ep;
            ec.combatDirector = cd;
            ec.autoStart = false; // ✅ Flowが開始する

            // ✅ Flow Controller（タイトル/完了画面）
            var flow = new GameObject("GameFlow").AddComponent<OJikaProto.GameFlowController>();
            flow.episode = ec;
            flow.player = player.GetComponent<OJikaProto.PlayerController>();
            flow.playerCombat = player.GetComponent<OJikaProto.PlayerCombat>();
            flow.lockOn = player.GetComponent<OJikaProto.LockOnController>();
            flow.cameraRig = rig;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("OJikaProto: Full Prototype Scene created. Press Play.");
        }
    }
}
#endif
