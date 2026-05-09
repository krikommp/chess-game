using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// MVP single-player unit. Owns AP + NavMeshAgent.
    /// No turn system yet; AP is spent on every move and refilled with R (debug).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Player1Controller : MonoBehaviour
    {
        [Header("AP")]
        [Tooltip("Action Points granted per refill (and starting value).")]
        [SerializeField] private int maxAP = 5;

        [Header("Move")]
        [Tooltip("Meters one AP can buy (path length, not straight line).")]
        [SerializeField, Min(0.01f)] private float moveSpeedMetersPerAp = 2f;

        [Tooltip("Animation/agent speed (meters/second). Independent from AP.")]
        [SerializeField, Min(0.1f)] private float agentSpeed = 4f;

        [Header("Debug")]
        [SerializeField] private KeyCode refillKey = KeyCode.R;

        public int CurrentAP { get; private set; }
        public int MaxAP => maxAP;
        public float MoveSpeedMetersPerAp => moveSpeedMetersPerAp;
        public bool IsMoving { get; private set; }

        private NavMeshAgent _agent;

        public NavMeshAgent Agent => _agent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.speed = agentSpeed;
            _agent.stoppingDistance = 0.05f;
            _agent.autoBraking = true;
            CurrentAP = maxAP;
        }

        private void Update()
        {
            if (Input.GetKeyDown(refillKey))
            {
                CurrentAP = maxAP;
                Debug.Log($"[Player1] AP refilled to {CurrentAP}");
            }

            if (IsMoving && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                IsMoving = false;
            }
        }

        /// <summary>Try to consume AP and start moving along the given complete path.</summary>
        /// <returns>true if move started.</returns>
        public bool TryMove(NavMeshPath path, int apCost)
        {
            if (IsMoving) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (apCost <= 0 || apCost > CurrentAP) return false;

            _agent.SetPath(path);
            CurrentAP -= apCost;
            IsMoving = true;
            return true;
        }
    }
}
