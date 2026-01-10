using UnityEngine;

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

        /// <summary>Convenience: treat non-editor builds as "public" unless explicitly opted out.</summary>
        public static bool ShouldSuppressDebugInRuntime()
        {
#if UNITY_EDITOR
            return false;
#else
            return PublicBuild;
#endif
        }
    }
}
