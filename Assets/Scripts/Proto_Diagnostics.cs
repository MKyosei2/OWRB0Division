using UnityEngine;
using System.Collections.Generic;

namespace OJikaProto
{
    /// <summary>
    /// Lightweight diagnostic logger to help pinpoint "can't move / only camera works / stuck phase" issues.
    /// Toggle Enabled at runtime if you want to reduce logs.
    /// </summary>
    public static class ProtoDiagnostics
    {
        /// <summary>Master switch. Defaults to Debug build state.</summary>
        public static bool Enabled = Debug.isDebugBuild;

        /// <summary>If true, logs include frame/time prefix.</summary>
        public static bool Prefix = true;

        // Simple per-key throttling to avoid log spam.
        private static readonly Dictionary<string, float> _nextAllowed = new Dictionary<string, float>(64);

        public static void Log(string key, string message, Object context = null, float throttleSeconds = 0.25f)
        {
            if (!Enabled) return;

            if (!string.IsNullOrEmpty(key))
            {
                var now = Time.realtimeSinceStartup;
                if (_nextAllowed.TryGetValue(key, out var next) && now < next) return;
                _nextAllowed[key] = now + Mathf.Max(0f, throttleSeconds);
            }

            string prefix = Prefix ? $"[OJK:DIAG f{Time.frameCount} t{Time.realtimeSinceStartup:0.00}] " : "[OJK:DIAG] ";
            if (context != null) Debug.Log(prefix + message, context);
            else Debug.Log(prefix + message);
        }

        public static void Warn(string key, string message, Object context = null, float throttleSeconds = 0.5f)
        {
            if (!Enabled) return;

            if (!string.IsNullOrEmpty(key))
            {
                var now = Time.realtimeSinceStartup;
                if (_nextAllowed.TryGetValue(key, out var next) && now < next) return;
                _nextAllowed[key] = now + Mathf.Max(0f, throttleSeconds);
            }

            string prefix = Prefix ? $"[OJK:DIAG f{Time.frameCount} t{Time.realtimeSinceStartup:0.00}] " : "[OJK:DIAG] ";
            if (context != null) Debug.LogWarning(prefix + message, context);
            else Debug.LogWarning(prefix + message);
        }
    }
}
