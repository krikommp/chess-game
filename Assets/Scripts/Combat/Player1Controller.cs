using System;
using MiniChess.Combat.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    public enum EPlayerVisualState
    {
        Default,
        Hovered,
        Selected
    }

    public enum EFaction
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
        [SerializeField] private string m_displayName = "Player";
        [SerializeField] private int m_partySlot = 1;
        [SerializeField] private EFaction m_faction = EFaction.Player;

        [Header("HP")]
        [SerializeField] private int m_maxHP = 100;
        [SerializeField] private int m_currentHP = 100;

        [Header("Initiative")]
        [SerializeField] private int m_initiative = 10;

        [Header("AP")]
        [Tooltip("Action Points granted per refill (and starting value).")]
        [SerializeField] private int m_maxAP = 6;

        [Header("Move")]
        [Tooltip("Meters one AP can buy (path length, not straight line).")]
        [SerializeField, Min(0.01f)] private float m_moveSpeedMetersPerAp = 2f;

        [Tooltip("Animation/agent speed (meters/second). Independent from AP.")]
        [SerializeField, Min(0.1f)] private float m_agentSpeed = 4f;

        [Header("Debug")]
        [SerializeField] private bool m_allowDebugRefill = true;
        [SerializeField] private KeyCode m_refillKey = KeyCode.R;

        [Header("Visuals")]
        [SerializeField] private Color m_defaultColor = new Color(0.4f, 0.4f, 0.5f);
        [SerializeField] private Color m_hoveredColor = new Color(0.9f, 0.8f, 0.3f);
        [SerializeField] private Color m_selectedColor = new Color(0.3f, 0.8f, 0.4f);

        public int CurrentAP { get; private set; }
        public EFaction Faction { get => m_faction; set => m_faction = value; }
        public int Initiative { get => m_initiative; set => m_initiative = value; }
        public int MaxAP { get => m_maxAP; set => m_maxAP = Mathf.Max(0, value); }
        public int PartySlot { get => m_partySlot; set => m_partySlot = value; }
        public string DisplayName { get => string.IsNullOrWhiteSpace(m_displayName) ? gameObject.name : m_displayName; set => m_displayName = value; }
        public float MoveSpeedMetersPerAp { get => m_moveSpeedMetersPerAp; set => m_moveSpeedMetersPerAp = Mathf.Max(0.01f, value); }
        public bool IsMoving { get; private set; }
        public bool HasEndedRound { get; private set; }
        public bool IsAlive => m_currentHP > 0;
        public int MaxHP => m_maxHP;
        public int CurrentHP { get => m_currentHP; set => m_currentHP = Mathf.Clamp(value, 0, m_maxHP); }
        public float UnpaidMoveDistance => m_unpaidMoveDistance;
        public float RemainingMoveDistance => Mathf.Max(0f, CurrentAP * MoveSpeedMetersPerAp - m_unpaidMoveDistance);

        private NavMeshAgent m_agent;
        private MeshRenderer m_meshRenderer;
        private Material m_materialInstance;
        private Vector3 m_lastMovementPosition;
        private float m_unpaidMoveDistance;

        public NavMeshAgent Agent => m_agent;

        private void Awake()
        {
            m_agent = GetComponent<NavMeshAgent>();
            m_agent.speed = m_agentSpeed;
            m_agent.stoppingDistance = 0.05f;
            m_agent.autoBraking = true;
            CurrentAP = m_maxAP;

            m_meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (m_meshRenderer != null)
            {
                m_materialInstance = m_meshRenderer.material;
                ApplyColor(m_defaultColor);
            }
        }

        private void Update()
        {
            if (m_allowDebugRefill && Input.GetKeyDown(m_refillKey))
            {
                BeginRound();
                Debug.Log($"[{DisplayName}] AP refilled to {CurrentAP}");
            }

            if (!IsMoving) return;

            AccountMovementDistance();

            if (!m_agent.pathPending && m_agent.remainingDistance <= m_agent.stoppingDistance)
            {
                IsMoving = false;
                StateChanged?.Invoke();
            }
        }

        public void BeginRound()
        {
            CurrentAP = m_maxAP;
            HasEndedRound = false;
            m_unpaidMoveDistance = 0f;
            GetComponent<SkillExecutor>()?.AdvanceCooldowns();
            StateChanged?.Invoke();
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;

            HasEndedRound = true;
            CurrentAP = 0;
            m_unpaidMoveDistance = 0f;
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

            m_agent.SetPath(path);
            IsMoving = true;
            m_lastMovementPosition = transform.position;
            MovementStarted?.Invoke();
            StateChanged?.Invoke();
            return true;
        }

        public bool TryStartMove(NavMeshPath path) => TryMove(path, 0);

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
            m_currentHP = Mathf.Max(0, m_currentHP - damage);
            StateChanged?.Invoke();
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            m_currentHP = Mathf.Min(m_maxHP, m_currentHP + amount);
            StateChanged?.Invoke();
        }

        public void SetVisualState(EPlayerVisualState state)
        {
            switch (state)
            {
                case EPlayerVisualState.Default:
                    ApplyColor(m_defaultColor);
                    break;
                case EPlayerVisualState.Hovered:
                    ApplyColor(m_hoveredColor);
                    break;
                case EPlayerVisualState.Selected:
                    ApplyColor(m_selectedColor);
                    break;
            }
        }

        private void ApplyColor(Color color)
        {
            if (m_materialInstance != null) m_materialInstance.color = color;
        }

        public int PreviewMovementApCost(float pathLength)
        {
            if (pathLength <= 0f || MoveSpeedMetersPerAp <= 0f) return 0;

            float projectedDistance = m_unpaidMoveDistance + pathLength;
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
            float delta = Vector3.Distance(m_lastMovementPosition, currentPosition);
            m_lastMovementPosition = currentPosition;

            if (delta <= 0.0001f || MoveSpeedMetersPerAp <= 0f) return;

            m_unpaidMoveDistance += delta;

            bool spentAp = false;
            while (m_unpaidMoveDistance + 0.0001f >= MoveSpeedMetersPerAp && CurrentAP > 0)
            {
                m_unpaidMoveDistance -= MoveSpeedMetersPerAp;
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



