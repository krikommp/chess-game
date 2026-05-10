using System;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Unified NavMeshAgent movement wrapper. Extracted from the duplicated movement logic
    /// that was in EnemyController and Player1Controller.
    /// Requires NavMeshAgent (for pathfinding) and AttributeSet (for AP / MoveSpeed).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(AttributeSet))]
    public class MovementController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Animation/agent speed in meters per second. Independent from AP-based movement budget.")]
        [SerializeField, Min(0.1f)] private float m_agentSpeed = 3.5f;

        public event Action MovementStarted;
        public event Action MovementStopped;
        public event Action ApDeducted; // fired each time AP is deducted during movement

        private NavMeshAgent m_agent;
        private AttributeSet m_attributes;
        private Vector3 m_lastMovementPosition;
        private float m_unpaidMoveDistance;
        private bool m_isMoving;

        public NavMeshAgent Agent => m_agent;
        public bool IsMoving => m_isMoving;

        public float RemainingMoveDistance
        {
            get
            {
                float ap = m_attributes.Get(WellKnownAttributeTags.AP);
                float speed = m_attributes.Get(WellKnownAttributeTags.MoveSpeed);
                return Mathf.Max(0f, ap * speed - m_unpaidMoveDistance);
            }
        }

        public void ResetUnpaidDistance()
        {
            m_unpaidMoveDistance = 0f;
        }

        // ── Unity lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            m_agent = GetComponent<NavMeshAgent>();
            m_attributes = GetComponent<AttributeSet>();
            m_agent.speed = m_agentSpeed;
            m_agent.stoppingDistance = 0.05f;
            m_agent.autoBraking = true;
        }

        private void Update()
        {
            if (!m_isMoving) return;

            AccountMovementDistance();

            if (!m_agent.pathPending
                && (!m_agent.hasPath || m_agent.remainingDistance <= m_agent.stoppingDistance + 0.1f))
            {
                StopMovement();
            }
        }

        // ── Public API ──────────────────────────────────────────────

        public bool TryStartMove(NavMeshPath path) => TryMove(path);

        public bool TryMove(NavMeshPath path)
        {
            if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (!CanMoveAlong(path)) return false;

            m_agent.SetPath(path);
            m_isMoving = true;
            m_lastMovementPosition = transform.position;
            MovementStarted?.Invoke();
            return true;
        }

        public void StopMovement()
        {
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
                m_agent.ResetPath();

            bool wasMoving = m_isMoving;
            m_isMoving = false;
            if (wasMoving)
                MovementStopped?.Invoke();
        }

        public int PreviewMovementApCost(float pathLength)
        {
            if (pathLength <= 0f) return 0;

            float speed = m_attributes.Get(WellKnownAttributeTags.MoveSpeed);
            if (speed <= 0f) return 0;

            float projectedDistance = m_unpaidMoveDistance + pathLength;
            int cost = Mathf.FloorToInt((projectedDistance + 0.0001f) / speed);
            return Mathf.Clamp(cost, 0, (int)m_attributes.Get(WellKnownAttributeTags.AP));
        }

        // ── Internals ───────────────────────────────────────────────

        private bool CanMoveAlong(NavMeshPath path)
        {
            if (m_attributes.Get(WellKnownAttributeTags.AP) <= 0f) return false;
            if (path.corners == null || path.corners.Length < 2) return false;

            float pathLength = PathCostCalculator.PathLength(path.corners);
            return pathLength > 0.001f && pathLength <= RemainingMoveDistance + 0.001f;
        }

        private void AccountMovementDistance()
        {
            Vector3 currentPosition = transform.position;
            float delta = Vector3.Distance(m_lastMovementPosition, currentPosition);
            m_lastMovementPosition = currentPosition;

            if (delta <= 0.0001f) return;

            float speed = m_attributes.Get(WellKnownAttributeTags.MoveSpeed);
            if (speed <= 0f) return;

            m_unpaidMoveDistance += delta;

            while (m_unpaidMoveDistance + 0.0001f >= speed)
            {
                if (!m_attributes.TrySpend(WellKnownAttributeTags.AP, 1f))
                    break;

                m_unpaidMoveDistance -= speed;
                ApDeducted?.Invoke();
            }
        }
    }
}
