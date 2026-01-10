// Auto-updated: 2026-01-09
// Assets/Scripts/Proto_Narrative.cs
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Small narration helper used by the result/recap screen.
    /// Kept simple & deterministic for the prototype.
    /// </summary>
    public class OutcomeNarration : MonoBehaviour
    {
        [Header("Optional Data Asset (preferred for production)")]
        public OutcomeNarrationData data;

        [TextArea(3, 8)]
        public string truceText =
            "停戦は成立した。\nただし“期限付き”だ。\n駅の時計は、まだ正確には動いていない。";

        [TextArea(3, 8)]
        public string contractText =
            "契約は成立した。\n怪異は“次の条件”を提示して去った。\nあなたはそれが“次の事件”の入口だと理解する。";

        [TextArea(3, 8)]
        public string sealText =
            "封印は成功した。\nしかし“規約”は消えない。\n都市はそれを、忘れたふりをする。";

        [TextArea(3, 8)]
        public string slayText =
            "討伐は完了した。\nだが“対価”は残った。\nあなたは、勝利の重さを知る。";

        [TextArea(3, 8)]
        public string noneText =
            "事件は未解決のまま終わった。\n次の手がかりが必要だ。";

        public string GetText(NegotiationOutcome o)
        {
            if (data != null) return data.GetText(o);

            return o switch
            {
                NegotiationOutcome.Truce => truceText,
                NegotiationOutcome.Contract => contractText,
                NegotiationOutcome.Seal => sealText,
                NegotiationOutcome.Slay => slayText,
                _ => noneText
            };
        }
    }

    /// <summary>
    /// Optional data-driven container for localization / iteration.
    /// This avoids hard-coding strings in scene objects.
    /// </summary>
    [CreateAssetMenu(menuName = "OJK Proto/Outcome Narration Data", fileName = "OutcomeNarrationData")]
    public class OutcomeNarrationData : ScriptableObject
    {
        [TextArea(3, 8)] public string truceText =
            "停戦は成立した。\nただし“期限付き”だ。\n駅の時計は、まだ正確には動いていない。";
        [TextArea(3, 8)] public string contractText =
            "契約は成立した。\n怪異は“次の条件”を提示して去った。\nあなたはそれが“次の事件”の入口だと理解する。";
        [TextArea(3, 8)] public string sealText =
            "封印は成功した。\nしかし“規約”は消えない。\n都市はそれを、忘れたふりをする。";
        [TextArea(3, 8)] public string slayText =
            "討伐は完了した。\nだが“対価”は残った。\nあなたは、勝利の重さを知る。";
        [TextArea(3, 8)] public string noneText =
            "事件は未解決のまま終わった。\n次の手がかりが必要だ。";

        public string GetText(NegotiationOutcome o)
        {
            return o switch
            {
                NegotiationOutcome.Truce => truceText,
                NegotiationOutcome.Contract => contractText,
                NegotiationOutcome.Seal => sealText,
                NegotiationOutcome.Slay => slayText,
                _ => noneText
            };
        }
    }
}
