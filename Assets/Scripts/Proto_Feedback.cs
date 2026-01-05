using System.Collections;
using UnityEngine;

namespace OJikaProto
{
    /// <summary>
    /// ✅ このプロジェクトの唯一の FeedbackManager 実装（FeedbackManager.cs は削除する前提）
    /// Proto_Negotiation.cs が呼ぶ以下を必ず提供する：
    /// - OnNegotiationOpen()
    /// - OnNegotiationSuccess()
    /// - OnNegotiationFail()
    ///
    /// 既存互換：
    /// - OnPlayerDamaged()
    /// - OnPlayerAttackHit(AttackType, Vector3)
    /// - OnEnemyBroken(Vector3)
    /// - OnOutcomeResolved(NegotiationOutcome, Vector3)
    /// - Shake(float, float)
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public class FeedbackManager : MonoBehaviour
    {
        public static FeedbackManager Instance { get; private set; }

        [Header("Toggles")]
        public bool enableAudio = true;
        public bool enableVfx = true;
        public bool enableScreenFx = true;
        public bool enableShake = true;

        [Header("Audio")]
        [Range(0f, 1f)] public float masterSfx = 0.9f;
        public float distanceAttenuation = 0.06f; // 0に近いほど減衰しない

        [Header("Screen FX")]
        [Range(0f, 1f)] public float flashFadeSpeed = 3.2f;

        [Header("VFX")]
        public float vfxLifetime = 0.25f;
        public float vfxScale = 0.9f;

        // runtime
        private AudioSource _sfx;
        private Camera _cam;

        private Texture2D _flat;
        private float _flashA;
        private Color _flashColor;

        private float _shakeT;
        private float _shakeAmp;
        private Vector3 _shakeOffset;

        // wipe
        private float _wipeU;
        private Color _wipeColor;

        // Runtime generated clips（アセット無しでも成立）
        private AudioClip _clipLight;
        private AudioClip _clipHeavy;
        private AudioClip _clipSeal;
        private AudioClip _clipBreak;

        private AudioClip _clipUIOpen;
        private AudioClip _clipUISuccess;
        private AudioClip _clipUIFail;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _sfx = GetComponent<AudioSource>();
            if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            _cam = Camera.main;

            _flat = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _flat.SetPixel(0, 0, Color.white);
            _flat.Apply();

            _flashColor = new Color(0.25f, 0.90f, 0.85f, 1f);
            _wipeColor = _flashColor;

            // combat
            _clipLight = MakeClick(240f, 0.05f);
            _clipHeavy = MakeThunk(110f, 0.08f);
            _clipSeal = MakeTone(210f, 0.07f);
            _clipBreak = MakeWhoosh(160f, 0.14f);

            // UI / negotiation
            _clipUIOpen = MakeTone(420f, 0.08f);
            _clipUISuccess = MakeTone(540f, 0.14f);
            _clipUIFail = MakeThunk(160f, 0.10f);
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;

            if (_flashA > 0f)
                _flashA = Mathf.MoveTowards(_flashA, 0f, flashFadeSpeed * Time.unscaledDeltaTime);

            if (_shakeT > 0f)
                _shakeT -= Time.unscaledDeltaTime;

            if (_shakeT > 0f && enableShake)
            {
                float n1 = Mathf.Sin(Time.unscaledTime * 22f);
                float n2 = Mathf.Cos(Time.unscaledTime * 18f);
                _shakeOffset = new Vector3(n1, n2, 0f) * _shakeAmp;
            }
            else _shakeOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            // ThirdPersonCameraRig後に足す
            if (!enableShake) return;
            if (_cam == null) return;
            if (_shakeOffset == Vector3.zero) return;
            _cam.transform.position += _shakeOffset;
        }

        private void OnGUI()
        {
            if (!enableScreenFx) return;

            // flash
            if (_flashA > 0.001f)
            {
                var prev = GUI.color;
                GUI.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flashA);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _flat);
                GUI.color = prev;
            }

            // wipe (左→右)
            if (_wipeU > 0.001f)
            {
                float w = Screen.width * Mathf.Clamp01(_wipeU);
                var prev = GUI.color;
                GUI.color = new Color(_wipeColor.r, _wipeColor.g, _wipeColor.b, 0.35f);
                GUI.DrawTexture(new Rect(0, 0, w, Screen.height), _flat);
                GUI.color = prev;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ===================== Negotiation (Proto_Negotiation.cs が要求) =====================

        public void OnNegotiationOpen()
        {
            // UIを開いた“決定音”
            Flash(new Color(0.25f, 0.90f, 0.85f, 1f), 0.08f);
            PlaySfx(_clipUIOpen, Random.Range(0.98f, 1.06f), 0.65f);
            Shake(0.06f, 0.012f);
        }

        public void OnNegotiationSuccess()
        {
            // 成功：青緑寄りで“勝ち”
            Flash(new Color(0.20f, 0.95f, 0.85f, 1f), 0.16f);
            PlaySfx(_clipUISuccess, Random.Range(0.98f, 1.05f), 0.95f);
            Shake(0.12f, 0.026f);
            StartCoroutine(WipeCo(new Color(0.20f, 0.95f, 0.85f, 1f), 0.40f));
        }

        public void OnNegotiationFail()
        {
            // 失敗：赤寄り（やりすぎない）
            Flash(new Color(1f, 0.25f, 0.25f, 1f), 0.12f);
            PlaySfx(_clipUIFail, Random.Range(0.90f, 1.00f), 0.85f);
            Shake(0.10f, 0.022f);
        }

        // ===================== Combat / Outcome (既存互換) =====================

        public void OnPlayerDamaged()
        {
            Flash(new Color(1f, 0.2f, 0.2f, 1f), 0.14f);
            Shake(0.06f, 0.015f);
        }

        public void OnPlayerAttackHit(AttackType type, Vector3 hitPos)
        {
            if (type == AttackType.Seal)
            {
                SpawnHitVfx(hitPos, new Color(0.20f, 0.95f, 0.85f, 1f), 1.15f);
                Flash(new Color(0.20f, 0.95f, 0.85f, 1f), 0.10f);
                Shake(0.08f, 0.020f);
                PlaySfx(_clipSeal, Random.Range(0.92f, 1.06f), VolumeByDistance(hitPos, 0.85f));
            }
            else if (type == AttackType.Heavy)
            {
                SpawnHitVfx(hitPos, new Color(1f, 1f, 1f, 1f), 1.05f);
                Flash(new Color(1f, 1f, 1f, 1f), 0.08f);
                Shake(0.10f, 0.028f);
                PlaySfx(_clipHeavy, Random.Range(0.86f, 0.98f), VolumeByDistance(hitPos, 0.95f));
            }
            else
            {
                SpawnHitVfx(hitPos, new Color(1f, 1f, 1f, 1f), 0.92f);
                Shake(0.06f, 0.014f);
                PlaySfx(_clipLight, Random.Range(1.00f, 1.12f), VolumeByDistance(hitPos, 0.65f));
            }
        }

        
        /// <summary>
        /// Enemy is about to attack (telegraph). Used by Proto_Combat AI to make attacks readable.
        /// </summary>
        public void OnEnemyAttackTelegraph(Vector3 atPos)
        {
            // Subtle cue: small flash + tiny shake + soft whoosh.
            Flash(new Color(1f, 1f, 1f, 1f), 0.04f);
            Shake(0.05f, 0.010f);
            PlaySfx(_clipBreak, Random.Range(0.95f, 1.08f), VolumeByDistance(atPos, 0.40f));
        }

        /// <summary>
        /// Enemy attack connected (hit). Used by Proto_Combat.
        /// </summary>
        public void OnEnemyAttackHit(Vector3 atPos)
        {
            Flash(new Color(1f, 0.25f, 0.25f, 1f), 0.10f);
            Shake(0.10f, 0.024f);
            PlaySfx(_clipHeavy, Random.Range(0.84f, 0.96f), VolumeByDistance(atPos, 0.75f));
        }

        public void OnEnemyBroken(Vector3 atPos)
        {
            SpawnBreakVfx(atPos);
            Flash(new Color(0.25f, 0.90f, 0.85f, 1f), 0.18f);
            Shake(0.18f, 0.040f);
            PlaySfx(_clipBreak, Random.Range(0.92f, 1.02f), VolumeByDistance(atPos, 1.0f));
        }

        public void OnOutcomeResolved(NegotiationOutcome outcome, Vector3 atPos)
        {
            // 交渉決着/討伐決着の共通演出（Outcome色）
            Color c = outcome switch
            {
                NegotiationOutcome.Truce => new Color(0.25f, 0.90f, 0.85f, 1f),
                NegotiationOutcome.Contract => new Color(0.90f, 0.70f, 0.25f, 1f),
                NegotiationOutcome.Seal => new Color(0.20f, 0.95f, 0.85f, 1f),
                NegotiationOutcome.Slay => new Color(1f, 1f, 1f, 1f),
                _ => new Color(0.25f, 0.90f, 0.85f, 1f)
            };

            Flash(c, 0.14f);
            Shake(0.14f, 0.028f);
            StartCoroutine(WipeCo(c, 0.45f));
        }

        public void Shake(float duration, float amplitude)
        {
            if (!enableShake) return;
            _shakeT = Mathf.Max(_shakeT, duration);
            _shakeAmp = Mathf.Max(_shakeAmp, amplitude);
            StartCoroutine(ShakeDecayCo(duration));
        }

        // ===================== Internals =====================

        private void Flash(Color c, float amount)
        {
            if (!enableScreenFx) return;
            _flashColor = c;
            _flashA = Mathf.Clamp01(_flashA + amount);
        }

        private IEnumerator ShakeDecayCo(float d)
        {
            float t = 0f;
            float start = _shakeAmp;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.001f, d));
                _shakeAmp = Mathf.Lerp(0f, start, a);
                yield return null;
            }
            _shakeAmp = 0f;
        }

        private void PlaySfx(AudioClip clip, float pitch, float vol)
        {
            if (!enableAudio) return;
            if (clip == null) return;
            if (_sfx == null) return;

            float v = Mathf.Clamp01(masterSfx) * Mathf.Clamp01(vol);
            if (v <= 0.001f) return;

            _sfx.pitch = pitch;
            _sfx.PlayOneShot(clip, v);
        }

        private float VolumeByDistance(Vector3 pos, float baseVol)
        {
            if (_cam == null) return baseVol;
            float d = Vector3.Distance(_cam.transform.position, pos);
            float att = 1f / (1f + d * distanceAttenuation);
            return baseVol * att;
        }

        private void SpawnHitVfx(Vector3 pos, Color c, float scaleMul)
        {
            if (!enableVfx) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "_VFX_Hit";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * (vfxScale * 0.22f * scaleMul);
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            var r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = MakeUnlitMat(c);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "_VFX_Ring";
            ring.transform.position = pos;
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(0.02f, 0.001f, 0.02f) * (vfxScale * 14f * scaleMul);
            var col2 = ring.GetComponent<Collider>();
            if (col2) Destroy(col2);

            var rr = ring.GetComponent<Renderer>();
            if (rr) rr.sharedMaterial = MakeUnlitMat(new Color(c.r, c.g, c.b, 0.85f));

            StartCoroutine(VfxLifeCo(go, ring, vfxLifetime));
        }

        private void SpawnBreakVfx(Vector3 pos)
        {
            if (!enableVfx) return;

            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "_VFX_BREAK";
            burst.transform.position = pos;
            burst.transform.localScale = Vector3.one * (vfxScale * 0.65f);
            var col = burst.GetComponent<Collider>();
            if (col) Destroy(col);

            var r = burst.GetComponent<Renderer>();
            if (r) r.sharedMaterial = MakeUnlitMat(new Color(0.25f, 0.90f, 0.85f, 1f));

            StartCoroutine(VfxLifeCo(burst, null, 0.32f));
        }

        private IEnumerator VfxLifeCo(GameObject a, GameObject b, float life)
        {
            float t = 0f;
            Renderer ra = a ? a.GetComponent<Renderer>() : null;
            Renderer rb = b ? b.GetComponent<Renderer>() : null;
            Vector3 a0 = a ? a.transform.localScale : Vector3.zero;
            Vector3 b0 = b ? b.transform.localScale : Vector3.zero;

            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.001f, life));
                float s = Mathf.Lerp(1f, 1.65f, u);

                if (a) a.transform.localScale = a0 * s;
                if (b) b.transform.localScale = b0 * Mathf.Lerp(1f, 1.9f, u);

                float alpha = 1f - u;
                if (ra) SetMatAlpha(ra, alpha);
                if (rb) SetMatAlpha(rb, alpha);

                yield return null;
            }

            if (a) Destroy(a);
            if (b) Destroy(b);
        }

        private IEnumerator WipeCo(Color c, float dur)
        {
            if (!enableScreenFx) yield break;

            _wipeColor = c;
            _wipeU = 0f;

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _wipeU = Mathf.Clamp01(t / Mathf.Max(0.001f, dur));

                // ワイプ中は薄くフラッシュ維持
                _flashColor = c;
                _flashA = Mathf.Max(_flashA, 0.06f);

                yield return null;
            }

            _wipeU = 0f;
        }

        // ---- Materials ----
        private static Material MakeUnlitMat(Color c)
        {
            Shader sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Standard");

            var m = new Material(sh);
            if (m.HasProperty("_Color")) m.color = c;
            else if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        private static void SetMatAlpha(Renderer r, float a)
        {
            if (!r || !r.sharedMaterial) return;
            var m = r.sharedMaterial;

            if (m.HasProperty("_Color"))
            {
                var c = m.color; c.a = a; m.color = c;
            }
            else if (m.HasProperty("_BaseColor"))
            {
                var c = m.GetColor("_BaseColor"); c.a = a; m.SetColor("_BaseColor", c);
            }
        }

        // ---- Runtime SFX (assetless) ----
        private static AudioClip MakeClick(float freq, float len)
        {
            int sr = 44100;
            int n = Mathf.Max(256, Mathf.FloorToInt(sr * len));
            var clip = AudioClip.Create("_SFX_Click", n, 1, sr, false);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 55f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeThunk(float freq, float len)
        {
            int sr = 44100;
            int n = Mathf.Max(512, Mathf.FloorToInt(sr * len));
            var clip = AudioClip.Create("_SFX_Thunk", n, 1, sr, false);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t * 22f);
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f;
                s += (Random.value * 2f - 1f) * 0.08f;
                data[i] = s * env * 0.9f;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeTone(float freq, float len)
        {
            int sr = 44100;
            int n = Mathf.Max(512, Mathf.FloorToInt(sr * len));
            var clip = AudioClip.Create("_SFX_Tone", n, 1, sr, false);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.SmoothStep(1f, 0f, t / len) * Mathf.SmoothStep(0f, 1f, t / (len * 0.18f));
                float s = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
                s += Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.12f;
                data[i] = s * env;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeWhoosh(float freq, float len)
        {
            int sr = 44100;
            int n = Mathf.Max(1024, Mathf.FloorToInt(sr * len));
            var clip = AudioClip.Create("_SFX_Whoosh", n, 1, sr, false);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.SmoothStep(0f, 1f, t / (len * 0.15f)) * Mathf.SmoothStep(1f, 0f, t / len);
                float sweep = Mathf.Lerp(freq * 0.7f, freq * 1.25f, t / len);
                float s = Mathf.Sin(2f * Mathf.PI * sweep * t) * 0.35f;
                s += (Random.value * 2f - 1f) * 0.12f;
                data[i] = s * env;
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
