// Auto-updated: 2026-01-10
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJikaProto
{
    /// <summary>
    /// Build-time / runtime switches to keep "public/prod demo" builds clean.
    /// Define scripting symbol: PROTO_PUBLIC_BUILD to disable debug hotkeys / overlays.
    /// </summary>
    public static class ProtoBuildConfig
    {
#if PROTO_PUBLIC_BUILD
        public const bool PublicBuild = true;
#else
        public const bool PublicBuild = false;
#endif

        /// <summary>
        /// Convenience: treat non-editor builds as "public" unless explicitly opted out.
        /// </summary>
        public static bool ShouldSuppressDebugInRuntime()
        {
#if UNITY_EDITOR
            return false;
#else
            return PublicBuild;
#endif
        }

        /// <summary>Debug tools/hotkeys/OnGUI overlays are allowed?</summary>
        public static bool AllowDebugHotkeys =>
            !ShouldSuppressDebugInRuntime() && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>Demo-only helper UI (e.g. "Press Enter to Start") is allowed?</summary>
        public static bool AllowDemoAssistUI =>
            !ShouldSuppressDebugInRuntime() && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>Capture helpers are allowed?</summary>
        public static bool AllowCaptureTools =>
            !ShouldSuppressDebugInRuntime() && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>True when we should avoid noisy logs.</summary>
        public static bool QuietLogs =>
            ShouldSuppressDebugInRuntime() && !Application.isEditor;

        /// <summary>
        /// Some developer helpers (AutoPilot/DemoMacro/CameraRoute etc.) should only run in demo scenes.
        /// If allowedSceneNames is null/empty, this returns true.
        /// </summary>
        public static bool IsSceneAllowed(string[] allowedSceneNames)
        {
            if (allowedSceneNames == null || allowedSceneNames.Length == 0) return true;

            string scene = SceneManager.GetActiveScene().name;
            for (int i = 0; i < allowedSceneNames.Length; i++)
            {
                var n = allowedSceneNames[i];
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (string.Equals(scene, n.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
