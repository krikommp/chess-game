using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour, ICombatUnit
    {
        [Header("Identity")]
        public string displayName = "Enemy";

        [Header("HP")]
        public int maxHP = 100;
        public int currentHP = 100;

        [Header("Initiative")]
        public int initiative = 5;

        [Header("AP")]
        [Tooltip("Action Points granted at the start of each round.")]
        public int maxAP = 6;

        [Header("Move")]
        [Tooltip("Meters one AP can buy (path length, not straight line).")]
        [Min(0.01f)] public float moveSpeedMetersPerAp = 2f;

        [Tooltip("Animation/agent speed (meters/second). Independent from AP.")]
        [Min(0.1f)] public float agentSpeed = 3.5f;

        [Header("Visuals")]
        public Color defaultColor = new Color(0.7f, 0.2f, 0.2f);
        public Color actingColor = new Color(1f, 0.5f, 0f);

        // ICombatUnit implementation
        public Faction Faction => Faction.Enemy;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public int Initiative => initiative;
        public int MaxAP => maxAP;
        public int CurrentAP { get; private set; }
        public int MaxHP => maxHP;
        public int CurrentHP { get => currentHP; set => currentHP = Mathf.Clamp(value, 0, maxHP); }
        public bool IsAlive => currentHP > 0;
        public bool IsMoving { get; private set; }
        public bool HasEndedRound { get; private set; }
        public float MoveSpeedMetersPerAp => moveSpeedMetersPerAp;
        public float RemainingMoveDistance => Mathf.Max(0f, CurrentAP * MoveSpeedMetersPerAp - _unpaidMoveDistance);
        public bool HasPath => _agent != null && _agent.enabled && _agent.isOnNavMesh && _agent.hasPath;
        public float RemainingPathDistance => _agent != null && _agent.enabled && _agent.isOnNavMesh ? _agent.remainingDistance : 0f;

        private NavMeshAgent _agent;
        private MeshRenderer _meshRenderer;
        private Material _materialInstance;
        private Vector3 _lastMovementPosition;
        private float _unpaidMoveDistance;

        private void Awake()
        {
            currentHP = maxHP;
            CurrentAP = maxAP;
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = agentSpeed;
            _agent.stoppingDistance = 0.05f;
            _agent.autoBraking = true;

            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                // TODO(Docs/06_MAP_SPEC.md §2): Revisit dynamic unit blocking when the shared MovementController exists.
                obstacle.enabled = false;
            }

            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void Update()
        {
            if (!IsMoving) return;

            AccountMovementDistance();

            if (!_agent.pathPending
                && (!_agent.hasPath || _agent.remainingDistance <= _agent.stoppingDistance + 0.1f))
            {
                StopMovement();
            }
        }

        private void Start()
        {
            if (_meshRenderer != null)
            {
                _materialInstance = _meshRenderer.material;
                ApplyColor(defaultColor);
            }
        }

        public void BeginRound()
        {
            CurrentAP = maxAP;
            HasEndedRound = false;
            _unpaidMoveDistance = 0f;
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;

            HasEndedRound = true;
            CurrentAP = 0;
            _unpaidMoveDistance = 0f;
            return true;
        }

        public bool TryMove(NavMeshPath path)
        {
            if (HasEndedRound) return false;
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (!CanMoveAlong(path)) return false;

            _agent.SetPath(path);
            IsMoving = true;
            _lastMovementPosition = transform.position;
            return true;
        }

        public bool TrySpendAP(int amount)
        {
            if (amount <= 0 || amount > CurrentAP) return false;
            CurrentAP -= amount;
            return true;
        }

        public void StopMovement()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            IsMoving = false;
        }

        public int PreviewMovementApCost(float pathLength)
        {
            if (pathLength <= 0f || MoveSpeedMetersPerAp <= 0f) return 0;

            float projectedDistance = _unpaidMoveDistance + pathLength;
            int cost = Mathf.FloorToInt((projectedDistance + 0.0001f) / MoveSpeedMetersPerAp);
            return Mathf.Clamp(cost, 0, CurrentAP);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Max(0, currentHP - damage);

            if (!IsAlive)
            {
                Destroy(gameObject);
            }
        }

        public void SetVisualState(PlayerVisualState state) { }

        public void FlashTurn()
        {
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            ApplyColor(actingColor);
            yield return new WaitForSeconds(0.5f);
            ApplyColor(defaultColor);
        }

        private void ApplyColor(Color color)
        {
            if (_materialInstance != null) _materialInstance.color = color;
        }

        private bool CanMoveAlong(NavMeshPath path)
        {
            if (CurrentAP <= 0) return false;
            if (path.corners == null || path.corners.Length < 2) return false;

            float pathLength = PathCostCalculator.PathLength(path.corners);
            return pathLength > 0.001f && pathLength <= RemainingMoveDistance + 0.001f;
        }

        private void AccountMovementDistance()
        {
            Vector3 currentPosition = transform.position;
            float delta = Vector3.Distance(_lastMovementPosition, currentPosition);
            _lastMovementPosition = currentPosition;

            if (delta <= 0.0001f || MoveSpeedMetersPerAp <= 0f) return;

            _unpaidMoveDistance += delta;

            while (_unpaidMoveDistance + 0.0001f >= MoveSpeedMetersPerAp && CurrentAP > 0)
            {
                _unpaidMoveDistance -= MoveSpeedMetersPerAp;
                CurrentAP--;
            }
        }
    }
}
