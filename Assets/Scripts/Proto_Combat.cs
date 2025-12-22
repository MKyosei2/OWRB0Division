// Assets/Scripts/Proto_Combat.cs
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
            }
        }

        private void Update()
        {
            if (!IsBroken) return;
            _t -= Time.deltaTime;
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

        private CharacterController _cc;
        private Vector3 _vel;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraRoot == null && Camera.main != null) cameraRoot = Camera.main.transform;
        }

        private void Update()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 input = Vector3.ClampMagnitude(new Vector3(h, 0f, v), 1f);

            Vector3 fwd = cameraRoot ? Vector3.Scale(cameraRoot.forward, new Vector3(1f, 0f, 1f)).normalized : Vector3.forward;
            Vector3 right = cameraRoot ? cameraRoot.right : Vector3.right;
            Vector3 move = fwd * input.z + right * input.x;

            _cc.Move(move * moveSpeed * Time.deltaTime);

            if (_cc.isGrounded)
            {
                if (_vel.y < 0f) _vel.y = -2f;
                if (Input.GetKeyDown(KeyCode.Space)) _vel.y = jumpSpeed;
            }
            _vel.y += gravity * Time.deltaTime;
            _cc.Move(_vel * Time.deltaTime);

            if (move.sqrMagnitude > 0.02f)
            {
                var rot = Quaternion.LookRotation(move.normalized, Vector3.up);
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
            {
                if (Target == null) AcquireNearest();
                else Target = null;
            }

            if (Target != null)
            {
                var dmg = Target.GetComponentInParent<Damageable>();
                if (dmg != null && dmg.IsDead) Target = null;
            }
        }

        private void AcquireNearest()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            Transform best = null;
            float bestD = float.MaxValue;

            foreach (var e in enemies)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Damageable>();
                if (hp != null && hp.IsDead) continue;

                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestD && d <= range)
                {
                    bestD = d;
                    best = e.transform;
                }
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
            if (Input.GetMouseButtonDown(0)) Attack(AttackType.Light);
            if (Input.GetMouseButtonDown(1)) Attack(AttackType.Heavy);
            if (Input.GetKeyDown(KeyCode.E)) Attack(AttackType.Seal);
        }

        private void Attack(AttackType type)
        {
            float dmg = type == AttackType.Light ? lightDamage : type == AttackType.Heavy ? heavyDamage : sealDamage;
            float brk = type == AttackType.Light ? lightBreak : type == AttackType.Heavy ? heavyBreak : sealBreak;

            LastAttackType = type;
            LastAttackTime = Time.time;

            Vector3 center = transform.position + transform.forward * (range * 0.9f) + Vector3.up * 1.0f;
            float radius = 0.7f;

            var hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var d = h.GetComponentInParent<Damageable>();
                if (d == null) continue;
                if (d.gameObject == gameObject) continue; // 自分は除外

                var b = h.GetComponentInParent<Breakable>();
                d.ApplyDamage(dmg);
                if (b) b.ApplyBreakDamage(brk);
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

            if (enemyPrefab == null)
            {
                EventBus.Instance?.Toast("EnemyPrefab is NULL");
                return;
            }

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

            // 交渉開始：Broken中＆近距離＆F
            if (Input.GetKeyDown(KeyCode.F) && _enemyBrk != null && _enemyBrk.IsBroken)
            {
                if (_player && Vector3.Distance(_player.position, _enemy.transform.position) <= negotiationRange)
                    NegotiationManager.Instance?.Begin(_neg, _episode, _enemyCtrl, this);
            }

            // 討伐決着
            if (_enemyHp != null && _enemyHp.IsDead)
            {
                CleanupEnemy();
                _episode.OnCombatResolved(NegotiationOutcome.Slay);
            }
        }

        public void ResolveByNegotiation(NegotiationOutcome outcome)
        {
            CleanupEnemy();
            _episode.OnCombatResolved(outcome);
        }

        private void CleanupEnemy()
        {
            if (_enemy) Destroy(_enemy);
            _enemy = null;
            _enemyCtrl = null;
            _enemyHp = null;
            _enemyBrk = null;
        }

        private void OnPlayerDied()
        {
            // 即リトライ（難易度選択なしの核）
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
