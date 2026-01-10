// Auto-updated: 2026-01-09
using System;

namespace OJikaProto
{
    /// <summary>
    /// AI-free recap templates. For prototype, keep it deterministic and short.
    /// This file is intentionally data-only so it can be reused in public demos without extra dependencies.
    /// </summary>
    public static class ProtoRecapDatabase
    {
        [Serializable]
        public class RecapContent
        {
            public string caseTitle;
            public string[] summaryLines; // ideally 3 lines
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
                objectiveLine = "次の目的：現場で証拠を集め、規約の手がかりを得る",
                controlHints = new[] { "調べる：E", "走る：Shift", "メニュー：Esc" },
                ruleTags = new string[0]
            };

            if (state == null) return c;

            switch (state.checkpointId)
            {
                case "EP1_INVEST":
                    c.summaryLines = new[]
                    {
                        "証拠を集め、規約の輪郭を掴んだ。",
                        "監視が強まり、現場は“異界化”へ傾く。",
                        "次は異界で原因を突き止め、交渉で収束させる。"
                    };
                    c.objectiveLine = "次の目的：異界に踏み込み、規約の条件を把握する";
                    c.controlHints = new[] { "調べる：E", "隠れる：C", "監視回避：視界外へ" };
                    break;

                case "EP1_BREAK":
                    c.summaryLines = new[]
                    {
                        "異界に踏み込めば、規約が戦闘ルールを変える。",
                        "規約を守って“崩し”に成功すると交渉に移行できる。",
                        "討伐以外でも決着できることが、この局の武器だ。"
                    };
                    c.objectiveLine = "次の目的：規約を破らずに崩し、交渉に持ち込む";
                    c.controlHints = new[] { "回避：Shift", "ガード：RMB", "交渉：ブレイク後F" };
                    c.ruleTags = (state.ruleTags != null && state.ruleTags.Length > 0)
                        ? state.ruleTags
                        : new[] { "視線NG", "反復NG" };
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

            // Normalize sizes (avoid UI edge cases)
            c.summaryLines ??= Array.Empty<string>();
            c.controlHints ??= Array.Empty<string>();
            c.ruleTags ??= Array.Empty<string>();

            return c;
        }
    }
}
