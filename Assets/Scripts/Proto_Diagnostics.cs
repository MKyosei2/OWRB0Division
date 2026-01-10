// Auto-updated: 2026-01-10
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// Lightweight diagnostics + tiny telemetry for prototype stability.
    /// Goals:
    /// - Catch missing references early ("UI is blank", "stuck", etc.).
    /// - Provide basic run metrics for tuning (violations, retries, etc.).
    /// - Stay quiet in public builds.
    /// </summary>
    public static class ProtoDiagnostics
    {
        /// <summary>Master switch. Defaults to Debug build state.</summary>
        public static bool Enabled = Debug.isDebugBuild && !ProtoBuildConfig.ShouldSuppressDebugInRuntime();

        /// <summary>If true, logs include frame/time prefix.</summary>
        public static bool Prefix = true;

        private static readonly Dictionary<string, float> _nextAllowed = new Dictionary<string, float>(64);

        // ---------------- Telemetry (very small, JSON flush) ----------------
        [Serializable]
        private class TelemetrySnapshot
        {
            public long utcUnix;
            public string build;
            public string scene;
            public List<TelemetryCounter> counters = new();
        }

        [Serializable]
        private class TelemetryCounter
        {
            public string key;
            public int value;
        }

        private static readonly Dictionary<string, int> _counters = new Dictionary<string, int>(64);
        private static bool _runtimeHookInstalled = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntimeHook()
        {
            if (_runtimeHookInstalled) return;
            _runtimeHookInstalled = true;

            // Spawn a tiny runner to flush telemetry on quit.
            var go = new GameObject("ProtoDiagnosticsRuntime");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<ProtoDiagnosticsRuntime>();
        }

        private sealed class ProtoDiagnosticsRuntime : MonoBehaviour
        {
            private void OnApplicationQuit()
            {
                // In public builds we stay quiet (no disk writes).
                if (ProtoBuildConfig.ShouldSuppressDebugInRuntime()) return;
                FlushTelemetryToDisk("quit");
            }

            private void OnApplicationPause(bool pause)
            {
                if (!pause) return;
                if (ProtoBuildConfig.ShouldSuppressDebugInRuntime()) return;
                FlushTelemetryToDisk("pause");
            }
        }

        public static void TrackCounter(string key, int delta = 1)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!_counters.TryGetValue(key, out var v)) v = 0;
            _counters[key] = v + delta;
        }

        public static void FlushTelemetryToDisk(string reason = "")
        {
            if (!Enabled) return;
            if (ProtoBuildConfig.QuietLogs) return;

            try
            {
                var snap = new TelemetrySnapshot
                {
                    utcUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    build = Application.version,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                };

                foreach (var kv in _counters)
                    snap.counters.Add(new TelemetryCounter { key = kv.Key, value = kv.Value });

                string json = JsonUtility.ToJson(snap, prettyPrint: true);

                // Write under persistentDataPath
                string safeReason = string.IsNullOrWhiteSpace(reason) ? "run" : reason.Trim();
                string file = $"ojika_proto_telemetry_{safeReason}_{snap.utcUnix}.json";
                string path = Path.Combine(Application.persistentDataPath, file);
                File.WriteAllText(path, json);

                Log("telemetry.flush", $"Telemetry flushed: {file}");
            }
            catch (Exception e)
            {
                Warn("telemetry.flush.fail", $"Telemetry flush failed: {e.Message}");
            }
        }

        // ---------------- Logging ----------------
        public static void Log(string key, string message, UnityEngine.Object context = null, float throttleSeconds = 0.25f)
        {
            if (!Enabled) return;
            if (ProtoBuildConfig.QuietLogs) return;
            if (!PassThrottle(key, throttleSeconds)) return;

            string prefix = Prefix ? $"[OJK:DIAG f{Time.frameCount} t{Time.realtimeSinceStartup:0.00}] " : "[OJK:DIAG] ";
            if (context != null) Debug.Log(prefix + message, context);
            else Debug.Log(prefix + message);
        }

        public static void Warn(string key, string message, UnityEngine.Object context = null, float throttleSeconds = 0.5f)
        {
            if (!Enabled) return;
            if (ProtoBuildConfig.QuietLogs) return;
            if (!PassThrottle(key, throttleSeconds)) return;

            string prefix = Prefix ? $"[OJK:DIAG f{Time.frameCount} t{Time.realtimeSinceStartup:0.00}] " : "[OJK:DIAG] ";
            if (context != null) Debug.LogWarning(prefix + message, context);
            else Debug.LogWarning(prefix + message);
        }

        private static bool PassThrottle(string key, float throttleSeconds)
        {
            if (string.IsNullOrEmpty(key)) return true;
            var now = Time.realtimeSinceStartup;
            if (_nextAllowed.TryGetValue(key, out var next) && now < next) return false;
            _nextAllowed[key] = now + Mathf.Max(0f, throttleSeconds);
            return true;
        }

        // ---------------- Reference validation ----------------
        public static bool Require(UnityEngine.Object obj, string label, UnityEngine.Object context = null)
        {
            if (obj != null) return true;
            Warn($"ref.missing.{label}", $"Missing reference: {label}", context);
            return false;
        }

        public static void RequireAnyPlayer(UnityEngine.Object context = null)
        {
            // Many scripts assume a PlayerController exists.
            var pc = UnityEngine.Object.FindObjectOfType<PlayerController>();
            if (pc == null)
                Warn("ref.missing.player", "PlayerController not found in scene", context);
        }
    }
}
