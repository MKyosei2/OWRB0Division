using UnityEngine;

namespace OJikaProto
{
    public enum AttackType { None, Light, Heavy, Seal }

    public class Damageable : MonoBehaviour
    {
        public float maxHp = 100f;
        public float hp;
        public bool IsDead => hp <= 0f;

        private void Awake() => hp = maxHp;
        public void ResetHP() => hp = maxHp;

        public void ApplyDamage(float amount)
        {
            if (IsDead) return;
            hp -= Mathf.Max(0f, amount);
            if (hp < 0f) hp = 0f;
        }
    }

    public class Breakable : MonoBehaviour
    {
        public float maxBreak = 100f;
        public float breakValue;
        public float brokenDuration = 6f;

        public bool IsBroken { get; private set; }
        private float _t;

        private void Awake() => breakValue = maxBreak;

        public void ResetBreak()
        {
            breakValue = maxBreak;
            IsBroken = false;
            _t = 0f;
        }

        public void ApplyBreakDamage(float amount)
        {
            if (IsBroken) return;
            breakValue -= Mathf.Max(0f, amount);
            if (breakValue <= 0f)
            {
                breakValue = 0f;
                IsBroken = true;
                _t = brokenDuration;

                // ✅ BREAK演出（画と音）
                FeedbackManager.Instance?.OnEnemyBroken(transform.position);
            }
        }

        private void Update()
        {
            if (!IsBroken) return;

            float mul = 1f;
            if (RunLogManager.Instance != null)
                mul = RunLogManager.Instance.GetBreakRecoverMultiplier();

            _t -= Time.deltaTime * mul;

            if (_t <= 0f)
            {
                IsBroken = false;
                breakValue = maxBreak;
            }
        }
    }

    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5.5f;
        public float gravity = -25f;
        public float jumpSpeed = 6f;
        public Transform cameraRoot;

        [Header("External Control (AutoPilot)")]
        public bool externalControl = false;
        public Vector3 externalMoveWorld = Vector3.zero;     // world-space direction
        public Vector3 externalLookDirWorld = Vector3.zero;  // world-space direction
        public bool externalJump = false;

        private CharacterController _cc;
        private Vector3 _vel;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraRoot == null && Camera.main != null) cameraRoot = Camera.main.transform;
        }

        private void Update()
        {
            Vector3 moveDir = Vector3.zero;

            if (externalControl)
            {
                moveDir = externalMoveWorld;
                moveDir.y = 0f;
                if (moveDir.magnitude > 1f) moveDir.Normalize();
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1);

                Vector3 fwd = cameraRoot ? Vector3.Scale(cameraRoot.forward, new Vector3(1f, 0f, 1f)).normalized : Vector3.forward;
                Vector3 right = cameraRoot ? cameraRoot.right : Vector3.right;
                moveDir = (fwd * input.z + right * input.x);
            }

            _cc.Move(moveDir * moveSpeed * Time.deltaTime);

            if (_cc.isGrounded)
            {
                if (_vel.y < 0f) _vel.y = -2f;

                bool jump = externalControl ? externalJump : Input.GetKeyDown(KeyCode.Space);
                if (jump) _vel.y = jumpSpeed;
                externalJump = false;
            }

            _vel.y += gravity * Time.deltaTime;
            _cc.Move(_vel * Time.deltaTime);

            // rotation
            Vector3 look = Vector3.zero;
            if (externalControl && externalLookDirWorld.sqrMagnitude > 0.01f)
            {
                look = externalLookDirWorld;
                look.y = 0f;
            }
            else if (moveDir.sqrMagnitude > 0.02f)
            {
                look = moveDir;
                look.y = 0f;
            }

            if (look.sqrMagnitude > 0.02f)
            {
                var rot = Quaternion.LookRotation(look.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 12f * Time.deltaTime);
            }
        }
    }

    public class PlayerHealth : MonoBehaviour
    {
        public float maxHp = 120f;
        public float hp;
        public bool IsDead => hp <= 0f;

        private void Awake() => hp = maxHp;
        public void ResetHP() => hp = maxHp;

        public void ApplyDamage(float dmg)
        {
            if (IsDead) return;

            hp -= Mathf.Max(0f, dmg);
            RunLogManager.Instance?.LogPlayerDamaged(dmg);

            FeedbackManager.Instance?.OnPlayerDamaged();

            if (hp <= 0f)
            {
                hp = 0f;
                EventBus.Instance?.Toast("Player Down");
                EventBus.Instance?.PlayerDied();
            }
        }
    }

    public class LockOnController : MonoBehaviour
    {
        public float range = 12f;
        public Transform Target { get; private set; }
        public bool IsLockedOn => Target != null;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                Toggle();
            if (Target != null)
            {
                var d = Target.GetComponentInParent<Damageable>();
                if (d != null && d.IsDead) Target = null;
            }
        }

        public void Toggle()
        {
            if (Target == null) AcquireNearest();
            else Target = null;
        }

        public void Clear() => Target = null;

        public void AcquireNearest()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            Transform best = null;
            float bestD = float.MaxValue;

            foreach (var e in enemies)
            {
                if (!e) continue;
                var hp = e.GetComponent<Damageable>();
                if (hp != null && hp.IsDead) continue;

                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestD && d <= range) { bestD = d; best = e.transform; }
            }

            Target = best;
        }
    }

    public class PlayerCombat : MonoBehaviour
    {
        public float lightDamage = 12f, heavyDamage = 22f, sealDamage = 6f;
        public float lightBreak = 10f, heavyBreak = 20f, sealBreak = 35f;

        public float range = 1.6f;
        public LayerMask hitMask = ~0;

        public AttackType LastAttackType { get; private set; } = AttackType.None;
        public float LastAttackTime { get; private set; } = -999f;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) PerformAttack(AttackType.Light);
            if (Input.GetMouseButtonDown(1)) PerformAttack(AttackType.Heavy);
            if (Input.GetKeyDown(KeyCode.E)) PerformAttack(AttackType.Seal);
        }

        public void PerformAttack(AttackType type) => Attack(type);

        private void Attack(AttackType type)
        {
            float dmg = type == AttackType.Light ? lightDamage : type == AttackType.Heavy ? heavyDamage : sealDamage;
            float brk = type == AttackType.Light ? lightBreak : type == AttackType.Heavy ? heavyBreak : sealBreak;

            LastAttackType = type;
            LastAttackTime = Time.time;

            Vector3 center = transform.position + transform.forward * (range * 0.9f) + Vector3.up * 1.0f;
            float radius = 0.7f;

            var hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);

            bool hitAny = false;
            Vector3 hitPos = center;

            foreach (var h in hits)
            {
                var d = h.GetComponentInParent<Damageable>();
                if (d == null) continue;
                if (d.gameObject == gameObject) continue;

                d.ApplyDamage(dmg);

                var b = h.GetComponentInParent<Breakable>();
                if (b) b.ApplyBreakDamage(brk);

                hitAny = true;
                hitPos = h.ClosestPoint(center);
            }

            if (hitAny)
            {
                FeedbackManager.Instance?.OnPlayerAttackHit(type, hitPos);
            }
        }
    }

    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(Breakable))]
    public class EnemyController : MonoBehaviour
    {
        public float moveSpeed = 3.2f;
        public float attackRange = 1.8f;
        public float attackDamage = 14f;
        public float attackCooldown = 1.2f;

        public float enrageMultiplier = 1.25f;
        public float enrageDuration = 8f;

        private Damageable _hp;
        private Breakable _brk;

        private Transform _player;
        private PlayerHealth _playerHp;

        private float _cd;
        private float _enrageT;
        public bool IsEnraged => _enrageT > 0f;

        private void Awake()
        {
            _hp = GetComponent<Damageable>();
            _brk = GetComponent<Breakable>();
        }

        private void Start()
        {
            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;
            _playerHp = pc ? pc.GetComponent<PlayerHealth>() : null;
        }

        private void Update()
        {
            if (_hp.IsDead) return;
            if (_player == null) return;

            if (_enrageT > 0f) _enrageT -= Time.deltaTime;
            if (_brk.IsBroken) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > attackRange)
            {
                Vector3 dir = (_player.position - transform.position);
                dir.y = 0f;
                dir = dir.normalized;

                float spd = moveSpeed * (IsEnraged ? enrageMultiplier : 1f);
                transform.position += dir * spd * Time.deltaTime;
            }
            else
            {
                _cd -= Time.deltaTime;
                if (_cd <= 0f)
                {
                    _cd = attackCooldown / (IsEnraged ? enrageMultiplier : 1f);
                    if (_playerHp) _playerHp.ApplyDamage(attackDamage);
                }
            }
        }

        public void Enrage()
        {
            _enrageT = enrageDuration;
            EventBus.Instance?.Toast("Enemy Enraged");
        }
    }

    public class CombatDirector : MonoBehaviour
    {
        public Transform playerSpawn;
        public Transform enemySpawn;
        public float negotiationRange = 2.2f;

        private GameObject _enemy;
        private EnemyController _enemyCtrl;
        private Damageable _enemyHp;
        private Breakable _enemyBrk;

        private EpisodeController _episode;
        private NegotiationDefinition _neg;

        private Transform _player;
        private PlayerHealth _playerHp;

        private void Awake()
        {
            CoreEnsure.EnsureAll();

            var pc = FindObjectOfType<PlayerController>();
            _player = pc ? pc.transform : null;
            _playerHp = pc ? pc.GetComponent<PlayerHealth>() : null;

            if (EventBus.Instance != null)
                EventBus.Instance.OnPlayerDied += OnPlayerDied;
        }

        private void OnDestroy()
        {
            if (EventBus.Instance != null)
                EventBus.Instance.OnPlayerDied -= OnPlayerDied;
        }

        public void BeginCombat(GameObject enemyPrefab, NegotiationDefinition negotiationDef, EpisodeController episode)
        {
            _episode = episode;
            _neg = negotiationDef;

            RunLogManager.Instance?.StartRun();
            RuleManager.Instance?.ClearRuntime();
            NegotiationManager.Instance?.ResetCooldown();

            if (_player && playerSpawn) { _player.position = playerSpawn.position; _player.rotation = playerSpawn.rotation; }
            if (_playerHp) _playerHp.ResetHP();

            if (_enemy) Destroy(_enemy);

            if (enemyPrefab == null) { EventBus.Instance?.Toast("EnemyPrefab is NULL"); return; }

            Vector3 pos = enemySpawn ? enemySpawn.position : Vector3.zero;
            Quaternion rot = enemySpawn ? enemySpawn.rotation : Quaternion.identity;
            _enemy = Instantiate(enemyPrefab, pos, rot);

            _enemyCtrl = _enemy.GetComponent<EnemyController>();
            _enemyHp = _enemy.GetComponent<Damageable>();
            _enemyBrk = _enemy.GetComponent<Breakable>();
        }

        private void Update()
        {
            if (_episode == null || _enemy == null) return;

            if (Input.GetKeyDown(KeyCode.F) && _enemyBrk != null && _enemyBrk.IsBroken)
            {
                if (_player && Vector3.Distance(_player.position, _enemy.transform.position) <= negotiationRange)
                    NegotiationManager.Instance?.Begin(_neg, _episode, _enemyCtrl, this);
            }

            if (_enemyHp != null && _enemyHp.IsDead)
            {
                // ✅ 討伐決着演出
                FeedbackManager.Instance?.OnOutcomeResolved(NegotiationOutcome.Slay, _enemy.transform.position);

                CleanupEnemy();
                _episode.OnCombatResolved(NegotiationOutcome.Slay);
            }
        }

        public void ResolveByNegotiation(NegotiationOutcome outcome)
        {
            // ✅ 交渉決着演出
            Vector3 p = (_enemy != null) ? _enemy.transform.position : Vector3.zero;
            FeedbackManager.Instance?.OnOutcomeResolved(outcome, p);

            CleanupEnemy();
            _episode.OnCombatResolved(outcome);
        }

        private void CleanupEnemy()
        {
            if (_enemy) Destroy(_enemy);
            _enemy = null; _enemyCtrl = null; _enemyHp = null; _enemyBrk = null;
        }

        private void OnPlayerDied()
        {
            if (_playerHp) _playerHp.ResetHP();
            if (_player && playerSpawn) { _player.position = playerSpawn.position; _player.rotation = playerSpawn.rotation; }

            if (_enemyHp) _enemyHp.ResetHP();
            if (_enemyBrk) _enemyBrk.ResetBreak();
            if (_enemy && enemySpawn) { _enemy.transform.position = enemySpawn.position; _enemy.transform.rotation = enemySpawn.rotation; }

            RuleManager.Instance?.ClearRuntime();
            NegotiationManager.Instance?.ResetCooldown();
            EventBus.Instance?.Toast("Retry");
        }
    }
}
