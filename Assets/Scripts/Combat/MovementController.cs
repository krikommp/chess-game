using System;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Per-unit movement execution component. Attach to any GameObject that can move.
    /// Pure movement — no game logic (AP, cost, validation) lives here.
    ///
    /// Extension point: future abilities (flight, teleport, etc.) override movement behavior
    /// by extending this component or adding sibling components that interact with it.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(AttributeSet))]
    public class MovementController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Animation/agent speed in meters per second.")]
        [SerializeField, Min(0.1f)] private float m_agentSpeed = 3.5f;

        public event Action MovementStarted;
        public event Action MovementStopped;

        private NavMeshAgent m_agent;
        private AttributeSet m_attributes;
        private bool m_isMoving;

        public NavMeshAgent Agent => m_agent;
        public bool IsMoving => m_isMoving;

        /// <summary>How many meters this unit can still move this turn.</summary>
        public float RemainingMoveDistance
        {
            get
            {
                float ap = m_attributes.Get(WellKnownAttributeTags.AP);
                float speed = m_attributes.Get(WellKnownAttributeTags.MoveSpeed);
                return ap * speed;
            }
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

            if (!m_agent.pathPending
                && (!m_agent.hasPath || m_agent.remainingDistance <= m_agent.stoppingDistance + 0.1f))
            {
                StopMovement();
            }
        }

        // ── Public API ──────────────────────────────────────────────

        public bool TryMove(NavMeshPath path)
        {
            if (m_agent == null || !m_agent.enabled || !m_agent.isOnNavMesh) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (path.corners == null || path.corners.Length < 2) return false;

            m_agent.SetPath(path);
            m_isMoving = true;
            MovementStarted?.Invoke();
            return true;
        }

        public bool TryStartMove(NavMeshPath path) => TryMove(path);

        public void StopMovement()
        {
            if (m_agent != null && m_agent.enabled && m_agent.isOnNavMesh)
                m_agent.ResetPath();

            bool wasMoving = m_isMoving;
            m_isMoving = false;
            if (wasMoving)
                MovementStopped?.Invoke();
        }
    }
}
