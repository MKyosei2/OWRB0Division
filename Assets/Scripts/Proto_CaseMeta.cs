using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Step6: 結末（契約/停戦/封印/討伐）が“次の周回”に影響する最低限のメタ。
    /// 目的：勝ち方の差を「次の行動の差」として体験させる。
    ///
    /// 仕様（プロト）：
    /// - 契約：影の遮蔽（Q）を解禁（捜査/戦闘で使える）
    /// - 停戦：期限（TruceDebt）+1 → 監視が厳しくなる
    /// - 封印：期限（TruceDebt）-1
    /// - 討伐：後味の悪さ（監視強化）として期限 +1
    /// </summary>
    public class CaseMetaManager : SimpleSingleton<CaseMetaManager>
    {
        [Header("Carryover")]
        [Tooltip("契約で得られるボーナス（影の遮蔽）が有効か")]
        public bool hasContractBoon = false;

        [Tooltip("期限付き停戦/討伐による『期限』。大きいほど監視が厳しい")]
        [Range(0, 3)]
        public int truceDebt = 0;

        [Tooltip("停戦で得られる『暫定許可証』。戦闘中に緊急介入（R）や『緊急停戦（パス消費）』を解禁")]
        [Range(0, 3)]
        public int arbitrationPasses = 0;

        [Tooltip("討伐で増える『歪み』。大きいほど規約が増える/厳格になる")]
        [Range(0, 3)]
        public int distortion = 0;

        [Tooltip("デバッグ：Play中にメタをリセットするキー")]
        public KeyCode debugResetKey = KeyCode.F12;

        protected override void Awake()
        {
            base.Awake();
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            
            if (ProtoBuildConfig.ShouldSuppressDebugInRuntime()) return;
if (Input.GetKeyDown(debugResetKey))
            {
                ResetMeta();
                EventBus.Instance?.Toast("META RESET");
            }
        }

        public void ResetMeta()
        {
            hasContractBoon = false;
            truceDebt = 0;
            arbitrationPasses = 0;
            distortion = 0;
        }

        public bool HasArbitrationPass => arbitrationPasses > 0;

        public bool ConsumeArbitrationPass()
        {
            if (arbitrationPasses <= 0) return false;
            arbitrationPasses = Mathf.Max(0, arbitrationPasses - 1);
            return true;
        }

        public void AddTruceDebt(int delta)
        {
            truceDebt = Mathf.Clamp(truceDebt + delta, 0, 3);
        }

        public void ApplyOutcome(NegotiationOutcome outcome)
        {
            switch (outcome)
            {
                case NegotiationOutcome.Contract:
                    hasContractBoon = true;
                    // 契約で多少は期限も緩む（協力で監視をいなす）
                    truceDebt = Mathf.Max(0, truceDebt - 1);
                    // 味方がつくと歪みも多少は沈静化する
                    distortion = Mathf.Max(0, distortion - 1);
                    break;

                case NegotiationOutcome.Seal:
                    truceDebt = Mathf.Max(0, truceDebt - 1);
                    distortion = Mathf.Max(0, distortion - 1);
                    break;

                case NegotiationOutcome.Truce:
                    truceDebt = Mathf.Min(3, truceDebt + 1);
                    arbitrationPasses = Mathf.Min(3, arbitrationPasses + 1);
                    break;

                case NegotiationOutcome.Slay:
                    // 片付けたが後味が悪い（監視/再発リスクが上がる扱い）
                    truceDebt = Mathf.Min(3, truceDebt + 1);
                    distortion = Mathf.Min(3, distortion + 1);
                    break;
            }
        }

        /// <summary>潜入の厳しさ補正（1.0=通常、最大1.6程度）</summary>
        public float GetSecurityMultiplier() => 1.0f + 0.20f * truceDebt + 0.15f * distortion;

        public string GetCarryoverText()
        {
            string boon = hasContractBoon ? "契約ボーナス：影の遮蔽(Q)" : "契約ボーナス：なし";
            string debt = truceDebt <= 0 ? "期限：なし" : $"期限：{truceDebt}";
            string pass = arbitrationPasses <= 0 ? "許可証：なし" : $"許可証：{arbitrationPasses}";
            string dist = distortion <= 0 ? "歪み：なし" : $"歪み：{distortion}";
            string sec = $"監視：x{GetSecurityMultiplier():0.0}";
            return $"{boon} / {debt} / {pass} / {dist} / {sec}";
        }
    }
}
