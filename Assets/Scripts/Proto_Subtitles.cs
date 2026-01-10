// Auto-updated: 2026-01-10
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Minimal subtitle manager for prototype.
    /// - Keeps the last N lines.
    /// - Auto-expires lines over time.
    /// - Designed to be UI-framework agnostic (Canvas/UITK can subscribe via OnChanged).
    /// </summary>
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

        /// <summary>Raised whenever visible subtitle lines may have changed.</summary>
        public event Action OnChanged;

        private readonly List<Line> _lines = new(8);
        private readonly List<string> _tmp = new(8);

        private void Update()
        {
            if (_lines.Count == 0) return;

            bool changed = false;
            // Subtitles should not freeze when timeScale changes (demo playback, pause, etc.)
            float dt = Time.unscaledDeltaTime;

            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                var l = _lines[i];
                l.timeLeft -= dt;
                if (l.timeLeft <= 0f)
                {
                    _lines.RemoveAt(i);
                    changed = true;
                }
                else
                {
                    _lines[i] = l;
                }
            }

            if (changed) OnChanged?.Invoke();
        }

        public void SetEnabled(bool enabled)
        {
            if (enabledSubtitles == enabled) return;
            enabledSubtitles = enabled;
            OnChanged?.Invoke();
        }

        public void Add(string text, float seconds = -1f)
        {
            if (!enabledSubtitles) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            // Avoid spam of the exact same line.
            if (_lines.Count > 0 && _lines[_lines.Count - 1].text == text)
            {
                var last = _lines[_lines.Count - 1];
                last.timeLeft = Mathf.Max(last.timeLeft, (seconds <= 0f) ? defaultSeconds : seconds);
                _lines[_lines.Count - 1] = last;
                OnChanged?.Invoke();
                return;
            }

            float t = (seconds <= 0f) ? defaultSeconds : seconds;

            _lines.Add(new Line { text = text, timeLeft = t });
            while (_lines.Count > Mathf.Max(1, maxLines))
                _lines.RemoveAt(0);

            OnChanged?.Invoke();
        }

        public void ClearAll()
        {
            if (_lines.Count == 0) return;
            _lines.Clear();
            OnChanged?.Invoke();
        }

        public void AddOutcome(NegotiationOutcome outcome, float seconds = -1f)
        {
            Add($"結果：{OutcomeText(outcome)}", seconds);
        }

        public IReadOnlyList<string> GetLinesNewestFirst()
        {
            _tmp.Clear();
            for (int i = _lines.Count - 1; i >= 0; i--)
                _tmp.Add(_lines[i].text);
            return _tmp;
        }

        private static string OutcomeText(NegotiationOutcome o)
        {
            return o switch
            {
                NegotiationOutcome.Truce => "停戦（期限付き）",
                NegotiationOutcome.Contract => "契約（協力）",
                NegotiationOutcome.Seal => "封印",
                NegotiationOutcome.Slay => "討伐",
                _ => "未確定"
            };
        }
    }
}
