using System;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    public enum PlayerVisualState
    {
        Default,
        Hovered,
        Selected
    }

    public enum Faction
    {
        Player,
        Enemy
    }

    /// <summary>
    /// MVP player combat unit. Owns initiative, AP, per-round done state, and NavMeshAgent movement.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Player1Controller : MonoBehaviour, ICombatUnit
    {
        public event Action MovementStarted;
        public event Action StateChanged;

        [Header("Identity")]
        public string displayName = "Player";
        public int partySlot = 1;
        public Faction Faction { get; set; } = Faction.Player;

        [Header("HP")]
        public int maxHP = 100;
        public int currentHP = 100;

        [Header("Initiative")]
        public int initiative = 10;

        [Header("AP")]
        [Tooltip("Action Points granted per refill (and starting value).")]
        public int maxAP = 6;

        [Header("Move")]
        [Tooltip("Meters one AP can buy (path length, not straight line).")]
        [Min(0.01f)] public float moveSpeedMetersPerAp = 2f;

        [Tooltip("Animation/agent speed (meters/second). Independent from AP.")]
        [Min(0.1f)] public float agentSpeed = 4f;

        [Header("Debug")]
        public bool allowDebugRefill = true;
        public KeyCode refillKey = KeyCode.R;

        [Header("Visuals")]
        [SerializeField] private Color defaultColor = new Color(0.4f, 0.4f, 0.5f);
        [SerializeField] private Color hoveredColor = new Color(0.9f, 0.8f, 0.3f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.8f, 0.4f);

        public int CurrentAP { get; private set; }
        public int Initiative { get => initiative; set => initiative = value; }
        public int MaxAP { get => maxAP; set => maxAP = Mathf.Max(0, value); }
        public int PartySlot { get => partySlot; set => partySlot = value; }
        public string DisplayName { get => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName; set => displayName = value; }
        public float MoveSpeedMetersPerAp { get => moveSpeedMetersPerAp; set => moveSpeedMetersPerAp = Mathf.Max(0.01f, value); }
        public bool IsMoving { get; private set; }
        public bool HasEndedRound { get; private set; }
        public bool IsAlive => currentHP > 0;
        public int MaxHP => maxHP;
        public int CurrentHP { get => currentHP; set => currentHP = Mathf.Clamp(value, 0, maxHP); }
        public float UnpaidMoveDistance => _unpaidMoveDistance;
        public float RemainingMoveDistance => Mathf.Max(0f, CurrentAP * MoveSpeedMetersPerAp - _unpaidMoveDistance);

        private NavMeshAgent _agent;
        private MeshRenderer _meshRenderer;
        private Material _materialInstance;
        private Vector3 _lastMovementPosition;
        private float _unpaidMoveDistance;

        public NavMeshAgent Agent => _agent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = agentSpeed;
            _agent.stoppingDistance = 0.05f;
            _agent.autoBraking = true;
            CurrentAP = maxAP;

            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (_meshRenderer != null)
            {
                _materialInstance = _meshRenderer.material;
                ApplyColor(defaultColor);
            }
        }

        private void Update()
        {
            if (allowDebugRefill && Input.GetKeyDown(refillKey))
            {
                BeginRound();
                Debug.Log($"[{DisplayName}] AP refilled to {CurrentAP}");
            }

            if (!IsMoving) return;

            AccountMovementDistance();

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                IsMoving = false;
                StateChanged?.Invoke();
            }
        }

        public void BeginRound()
        {
            CurrentAP = maxAP;
            HasEndedRound = false;
            _unpaidMoveDistance = 0f;
            StateChanged?.Invoke();
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;

            HasEndedRound = true;
            CurrentAP = 0;
            _unpaidMoveDistance = 0f;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Start or retarget movement. AP is spent as traveled distance crosses each AP threshold.</summary>
        /// <returns>true if move started.</returns>
        public bool TryMove(NavMeshPath path, int apCost)
        {
            if (HasEndedRound) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (!CanMoveAlong(path)) return false;

            _agent.SetPath(path);
            IsMoving = true;
            _lastMovementPosition = transform.position;
            MovementStarted?.Invoke();
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Spend AP directly (e.g. for attack cost when already in range).</summary>
        public bool TrySpendAP(int amount)
        {
            if (amount <= 0 || amount > CurrentAP) return false;
            CurrentAP -= amount;
            StateChanged?.Invoke();
            return true;
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Max(0, currentHP - damage);
            StateChanged?.Invoke();
        }

        public void SetVisualState(PlayerVisualState state)
        {
            switch (state)
            {
                case PlayerVisualState.Default:
                    ApplyColor(defaultColor);
                    break;
                case PlayerVisualState.Hovered:
                    ApplyColor(hoveredColor);
                    break;
                case PlayerVisualState.Selected:
                    ApplyColor(selectedColor);
                    break;
            }
        }

        private void ApplyColor(Color color)
        {
            if (_materialInstance != null) _materialInstance.color = color;
        }

        public int PreviewMovementApCost(float pathLength)
        {
            if (pathLength <= 0f || MoveSpeedMetersPerAp <= 0f) return 0;

            float projectedDistance = _unpaidMoveDistance + pathLength;
            int cost = Mathf.FloorToInt((projectedDistance + 0.0001f) / MoveSpeedMetersPerAp);
            return Mathf.Clamp(cost, 0, CurrentAP);
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

            bool spentAp = false;
            while (_unpaidMoveDistance + 0.0001f >= MoveSpeedMetersPerAp && CurrentAP > 0)
            {
                _unpaidMoveDistance -= MoveSpeedMetersPerAp;
                CurrentAP--;
                spentAp = true;
            }

            if (spentAp)
            {
                StateChanged?.Invoke();
            }
        }
    }
}
