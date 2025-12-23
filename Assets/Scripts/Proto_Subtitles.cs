using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    public class SubtitleManager : SimpleSingleton<SubtitleManager>
    {
        private struct Line
        {
            public string text;
            public float timeLeft;
        }

        [Header("Style")]
        public bool enabledSubtitles = true;
        public int maxLines = 2;

        [Header("Timing")]
        public float defaultSeconds = 1.8f;

        private readonly List<Line> _lines = new();

        private void Start()
        {
            // EventBus‚ÌƒƒbƒZ[ƒW‚ðŽš–‹‚É‚à‰ñ‚·iŽB‰e‚Å•Ö—˜j
            if (EventBus.Instance != null)
            {
                EventBus.Instance.OnRuleViolation += (sig) =>
                {
                    Add($"y‹K–ñˆá”½z{sig.ruleName}F{sig.reason}", 2.0f);
                };
                EventBus.Instance.OnToast += (msg) =>
                {
                    // Toast‚Í’Z‚ß‚É
                    Add(msg, 1.2f);
                };
                EventBus.Instance.OnEpisodeComplete += (o) =>
                {
                    Add($"yŒ‹––z{OutcomeText(o)}", 2.2f);
                };
            }
        }

        private void Update()
        {
            if (_lines.Count == 0) return;

            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                var l = _lines[i];
                l.timeLeft -= Time.unscaledDeltaTime;
                if (l.timeLeft <= 0f) _lines.RemoveAt(i);
                else _lines[i] = l;
            }
        }

        public void Add(string text, float seconds = -1f)
        {
            if (!enabledSubtitles) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            float t = (seconds > 0f) ? seconds : defaultSeconds;

            _lines.Insert(0, new Line { text = text, timeLeft = t });

            // Å‘ås”‚ÉŽû‚ß‚é
            while (_lines.Count > Mathf.Max(1, maxLines))
                _lines.RemoveAt(_lines.Count - 1);
        }

        public IReadOnlyList<string> GetLinesNewestFirst()
        {
            _tmp.Clear();
            for (int i = 0; i < _lines.Count; i++) _tmp.Add(_lines[i].text);
            return _tmp;
        }

        private readonly List<string> _tmp = new();

        private static string OutcomeText(NegotiationOutcome o)
        {
            return o switch
            {
                NegotiationOutcome.Truce => "’âíiŠúŒÀ•t‚«j",
                NegotiationOutcome.Contract => "Œ_–ñi‹¦—Íj",
                NegotiationOutcome.Seal => "••ˆó",
                NegotiationOutcome.Slay => "“¢”°",
                _ => "–¢Šm’è"
            };
        }
    }
}
