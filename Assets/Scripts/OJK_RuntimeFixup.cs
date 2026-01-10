// OJK_RuntimeFixup.cs
// Drop under: Assets/Scripts/OJK_RuntimeFixup.cs
//
// Purpose (one-off):
// - In an empty scene, automatically wires the prototype so the player can MOVE.
// - Fixes the common "camera works but player can't move" scenario caused by missing refs / timeScale pause / wrong enable states.
//
// Safe to delete after verification.

using UnityEngine;

namespace OJikaProto
{
    [DefaultExecutionOrder(-9000)]
    public sealed class OJK_RuntimeFixup : MonoBehaviour
    {
        private static OJK_RuntimeFixup _instance;
        private float _nextRewireAt;
        private int _rewireCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;

            var go = new GameObject("OJK_RuntimeFixup");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OJK_RuntimeFixup>();
        }

        private void Awake()
        {
            // If Recap paused us, unpause immediately (PhaseDirector also has a failsafe, but we ensure it here too).
            ForceTimeNormal();
        }

        private void Update()
        {
            // Keep time flowing. This alone can make movement return if only pause was the issue.
            if (Time.timeScale == 0f) ForceTimeNormal();

            // Rewire a few times after load to handle bootstrap order (PhaseDirector may run before Player exists).
            if (Time.unscaledTime < _nextRewireAt) return;
            _nextRewireAt = Time.unscaledTime + 0.25f;

            Rewire();
        }

        private static void ForceTimeNormal()
        {
            Time.timeScale = 1f;
            // Do NOT override fixedDeltaTime globally; prototype scripts manage it too.
        }

        private void Rewire()
        {
            _rewireCount++;

            // 1) Ensure Player exists and has required components
            var player = FindInLoadedScene<PlayerController>();
            if (player == null)
            {
                // If there is a GameObject named Player but missing scripts/components, repair it.
                var playerGO = GameObject.Find("Player");
                if (playerGO == null)
                {
                    playerGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    playerGO.name = "Player";
                    playerGO.transform.position = new Vector3(0f, 1f, -2f);
                }

                EnsureCharacterController(playerGO);
                player = Ensure<PlayerController>(playerGO);
                Ensure<PlayerCombat>(playerGO);
                Ensure<PlayerHealth>(playerGO);
                Ensure<LockOnController>(playerGO);
            }

            // Ensure enabled (some phases may disable it; we want it enabled during control phases)
            if (player != null && !player.enabled) player.enabled = true;

            // 2) Ensure CameraRig exists and targets the player
            var camRig = FindInLoadedScene<ThirdPersonCameraRig>();
            if (camRig == null)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    var camGO = new GameObject("Main Camera");
                    cam = camGO.AddComponent<Camera>();
                    camGO.AddComponent<AudioListener>();
                    cam.tag = "MainCamera";
                }
                camRig = Ensure<ThirdPersonCameraRig>(cam.gameObject);
            }
            if (camRig != null && camRig.target == null && player != null)
                camRig.target = player.transform;

            // 3) Wire GameFlowController if present
            var flow = FindInLoadedScene<GameFlowController>();
            if (flow != null && player != null)
            {
                if (flow.player == null) flow.player = player;
                if (flow.playerCombat == null) flow.playerCombat = player.GetComponent<PlayerCombat>();
                if (flow.lockOn == null) flow.lockOn = player.GetComponent<LockOnController>();
                if (flow.cameraRig == null) flow.cameraRig = camRig;
            }

            // 4) Wire ProtoPhaseDirector (input gating uses these refs!)
            var pd = ProtoPhaseDirector.Instance != null ? ProtoPhaseDirector.Instance : FindInLoadedScene<ProtoPhaseDirector>();
            if (pd != null && player != null)
            {
                if (pd.player == null) pd.player = player;
                if (pd.playerCombat == null) pd.playerCombat = player.GetComponent<PlayerCombat>();
                if (pd.lockOn == null) pd.lockOn = player.GetComponent<LockOnController>();
                if (pd.cameraRig == null) pd.cameraRig = camRig;

                // In case it already gated input while refs were null, re-apply for the current phase
                pd.ForceReapplyInputGate();
            }

            // 5) Ensure cursor policy for control phases (camera-only symptom sometimes comes from UI still holding cursor)
            // If a recap UI exists and is visible, user should dismiss it; otherwise lock cursor for play.
            var recap = FindInLoadedScene<ProtoRecapUI>();
            if (recap == null)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        private static void EnsureCharacterController(GameObject go)
        {
            if (go.GetComponent<CharacterController>() != null) return;

            var cap = go.GetComponent<CapsuleCollider>();
            if (cap != null) Object.Destroy(cap);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
        }

        private static T FindInLoadedScene<T>() where T : Object
        {
            // Unity 2020+ supports FindObjectOfType<T>(true). To stay compatible, use Resources.
            var all = Resources.FindObjectsOfTypeAll<T>();
            foreach (var a in all)
            {
                if (a == null) continue;
                var comp = a as Component;
                if (comp == null) continue;
                if (!comp.gameObject.scene.IsValid() || !comp.gameObject.scene.isLoaded) continue;
                // ignore hidden editor-only objects
#if UNITY_EDITOR
                if (UnityEditor.EditorUtility.IsPersistent(comp.gameObject)) continue;
#endif
                return a;
            }
            return null;
        }
    }

    public static class OJK_ProtoPhaseDirectorExtensions
    {
        // Minimal hook to re-apply gating without needing internal access.
        public static void ForceReapplyInputGate(this ProtoPhaseDirector pd)
        {
            if (pd == null) return;
            // CurrentPhase is public; call SetPhase with showCard=false to re-apply gates.
            try
            {
                pd.SetPhase(pd.CurrentPhase, pd.CurrentObjective, showCard: false);
            }
            catch
            {
                // ignore if signature differs
            }
        }
    }
}
