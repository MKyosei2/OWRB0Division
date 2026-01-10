// Assets/Scripts/Proto_Narrative.cs
using UnityEngine;

namespace OJikaProto
{
    public class OutcomeNarration : MonoBehaviour
    {
        [TextArea(3, 8)]
        public string truceText =
            "停戦は成立した。\nただし“期限付き”だ。\n駅の時計は、まだ正確には動いていない。";

        [TextArea(3, 8)]
        public string contractText =
            "契約は成立した。\n怪異は“次の条件”を提示して去った。\nあなたはそれが“次の事件”の入口だと理解する。";

        [TextArea(3, 8)]
        public string sealText =
            "封印は完了した。\nしかし封は“薄い”。\n次に破れるのは、時間の問題かもしれない。";

        [TextArea(3, 8)]
        public string slayText =
            "討伐で決着した。\n駅は静かになったが、証拠が一つだけ残っている。\n——終電の“行き先”は、どこだった？";

        [TextArea(2, 6)]
        public string noneText =
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
