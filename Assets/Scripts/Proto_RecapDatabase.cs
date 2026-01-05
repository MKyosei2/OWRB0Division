using System;

namespace OJikaProto
{
    /// <summary>
    /// AI-free recap templates. For prototype, keep it deterministic and short.
    /// </summary>
    public static class ProtoRecapDatabase
    {
        [Serializable]
        public class RecapContent
        {
            public string caseTitle;
            public string[] summaryLines; // 3 lines
            public string objectiveLine;  // 1 line
            public string[] controlHints; // up to 3
            public string[] ruleTags;     // up to 2
        }

        public static RecapContent GetRecap(ProtoSaveState state)
        {
            // Fallback
            var c = new RecapContent
            {
                caseTitle = "CASE 01 : 終電のいない駅",
                summaryLines = new[]
                {
                    "駅で“終電が来ない”という通報が入った。",
                    "監視映像に不可解な欠損があり、規約の気配がある。",
                    "次は異界で原因を突き止め、交渉で収束させる。"
                },
                objectiveLine = "次の目的：異界で規約を破らずに崩し、交渉に持ち込む",
                controlHints = new[] { "回避：Shift", "ガード：RMB", "ロックオン：MMB" },
                ruleTags = new string[0],
            };

            if (state == null) return c;

            // Use checkpoint-specific templates
            switch (state.checkpointId)
            {
                case "EP1_INVEST":
                    c.summaryLines = new[]
                    {
                        "駅で“終電が来ない”という通報が入った。",
                        "現場には“規約”の痕跡があり、手がかりが必要だ。",
                        "証拠を集め、異界突入の条件を満たす。"
                    };
                    c.objectiveLine = "次の目的：調査ポイントで証拠を集める（必要数を揃える）";
                    c.controlHints = new[] { "調査：E", "移動：WASD", "カメラ：マウス" };
                    c.ruleTags = new[] { "規約：？？？" };
                    break;

                case "EP1_BREAK":
                    c.summaryLines = new[]
                    {
                        "監視映像の欠損から、怪異の“規約”を特定した。",
                        "異界に踏み込めば、規約が戦闘ルールを変える。",
                        "崩しに成功すると交渉に移行し、討伐以外で決着できる。"
                    };
                    c.objectiveLine = "次の目的：異界で規約を破らずに崩し、交渉に持ち込む";
                    c.controlHints = new[] { "回避：Shift", "ガード：RMB", "交渉：ブレイク後F" };
                    c.ruleTags = (state.ruleTags != null && state.ruleTags.Length > 0) ? state.ruleTags : new[] { "視線NG", "反復NG" };
                    break;

                case "EP1_END":
                    c.summaryLines = new[]
                    {
                        "第1話は収束した。",
                        "結末は記録され、次の現場へ繋がる。",
                        "“期限付き停戦”の猶予は短い。"
                    };
                    c.objectiveLine = "次の目的：第2話（次の現場）へ";
                    c.controlHints = new[] { "タイトルへ戻る", "結果を確認", "次回フックを読む" };
                    c.ruleTags = new string[0];
                    break;
            }

            // Override objective if provided
            if (!string.IsNullOrEmpty(state.nextObjective))
                c.objectiveLine = "次の目的：" + state.nextObjective;

            return c;
        }
    }
}
