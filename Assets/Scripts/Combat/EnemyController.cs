using System.Collections;
using MiniChess.Combat.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour, ICombatUnit
    {
        [Header("Identity")]
        [SerializeField] private string m_displayName = "Enemy";

        [Header("HP")]
        [SerializeField] private int m_maxHP = 100;
        [SerializeField] private int m_currentHP = 100;

        [Header("Initiative")]
        [SerializeField] private int m_initiative = 5;

        [Header("AP")]
        [Tooltip("Action Points granted at the start of each round.")]
        [SerializeField] private int m_maxAP = 6;

        [Header("Move")]
        [Tooltip("Meters one AP can buy (path length, not straight line).")]
        [SerializeField, Min(0.01f)] private float m_moveSpeedMetersPerAp = 2f;

        [Tooltip("Animation/agent speed (meters/second). Independent from AP.")]
        [SerializeField, Min(0.1f)] private float m_agentSpeed = 3.5f;

        [Header("Visuals")]
        [SerializeField] private Color m_defaultColor = new Color(0.7f, 0.2f, 0.2f);
        [SerializeField] private Color m_actingColor = new Color(1f, 0.5f, 0f);

        // ICombatUnit implementation
        public EFaction Faction => EFaction.Enemy;
        public string DisplayName { get => string.IsNullOrWhiteSpace(m_displayName) ? gameObject.name : m_displayName; set => m_displayName = value; }
        public int Initiative { get => m_initiative; set => m_initiative = value; }
        public int MaxAP { get => m_maxAP; set => m_maxAP = Mathf.Max(0, value); }
        public int CurrentAP { get; private set; }
        public int MaxHP { get => m_maxHP; set => m_maxHP = Mathf.Max(0, value); }
        public int CurrentHP { get => m_currentHP; set => m_currentHP = Mathf.Clamp(value, 0, m_maxHP); }
        public bool IsAlive => m_currentHP > 0;
        public bool IsMoving { get; private set; }
        public bool HasEndedRound { get; private set; }
        public float MoveSpeedMetersPerAp { get => m_moveSpeedMetersPerAp; set => m_moveSpeedMetersPerAp = Mathf.Max(0.01f, value); }
        public Color DefaultColor { get => m_defaultColor; set => m_defaultColor = value; }
        public float RemainingMoveDistance => Mathf.Max(0f, CurrentAP * MoveSpeedMetersPerAp - m_unpaidMoveDistance);
        public bool HasPath => m_agent != null && m_agent.enabled && m_agent.isOnNavMesh && m_agent.hasPath;
        public float RemainingPathDistance => m_agent != null && m_agent.enabled && m_agent.isOnNavMesh ? m_agent.remainingDistance : 0f;

        private NavMeshAgent m_agent;
        private MeshRenderer m_meshRenderer;
        private Material m_materialInstance;
        private Vector3 m_lastMovementPosition;
        private float m_unpaidMoveDistance;

        private void Awake()
        {
            m_currentHP = m_maxHP;
            CurrentAP = m_maxAP;
            m_agent = GetComponent<NavMeshAgent>();
            if (GetComponent<SkillExecutor>() == null)
                gameObject.AddComponent<SkillExecutor>();
            m_agent.speed = m_agentSpeed;
            m_agent.stoppingDistance = 0.05f;
            m_agent.autoBraking = true;

            NavMeshObstacle obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                // TODO(Docs/06_MAP_SPEC.md §2): Revisit dynamic unit blocking when the shared MovementController exists.
                obstacle.enabled = false;
            }

            m_meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void Update()
        {
            if (!IsMoving) return;

            AccountMovementDistance();

            if (!m_agent.pathPending
                && (!m_agent.hasPath || m_agent.remainingDistance <= m_agent.stoppingDistance + 0.1f))
            {
                StopMovement();
            }
        }

        private void Start()
        {
            if (m_meshRenderer != null)
            {
                m_materialInstance = m_meshRenderer.material;
                ApplyColor(m_defaultColor);
            }
        }

        public void BeginRound()
        {
            CurrentAP = m_maxAP;
            HasEndedRound = false;
            m_unpaidMoveDistance = 0f;
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;

            HasEndedRound = true;
            CurrentAP = 0;
            m_unpaidMoveDistance = 0f;
            return true;
        }

        public bool TryMove(NavMeshPath path)
        {
            if (HasEndedRound) return false;
            if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (!CanMoveAlong(path)) return false;

            m_agent.SetPath(path);
            IsMoving = true;
            m_lastMovementPosition = transform.position;
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
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
            {
                m_agent.ResetPath();
            }

            IsMoving = false;
        }

        public int PreviewMovementApCost(float pathLength)
        {
            if (pathLength <= 0f || MoveSpeedMetersPerAp <= 0f) return 0;

            float projectedDistance = m_unpaidMoveDistance + pathLength;
            int cost = Mathf.FloorToInt((projectedDistance + 0.0001f) / MoveSpeedMetersPerAp);
            return Mathf.Clamp(cost, 0, CurrentAP);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            m_currentHP = Mathf.Max(0, m_currentHP - damage);

            if (!IsAlive)
            {
                Destroy(gameObject);
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0) return;
            m_currentHP = Mathf.Min(m_maxHP, m_currentHP + amount);
        }

        public void SetVisualState(EPlayerVisualState state) { }

        public void FlashTurn()
        {
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            ApplyColor(m_actingColor);
            yield return new WaitForSeconds(0.5f);
            ApplyColor(m_defaultColor);
        }

        private void ApplyColor(Color color)
        {
            if (m_materialInstance != null) m_materialInstance.color = color;
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

            while (m_unpaidMoveDistance + 0.0001f >= MoveSpeedMetersPerAp && CurrentAP > 0)
            {
                m_unpaidMoveDistance -= MoveSpeedMetersPerAp;
                CurrentAP--;
            }
        }
    }
}




