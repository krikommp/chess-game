using System;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// MVP player combat unit. Owns initiative, AP, per-round done state, and NavMeshAgent movement.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class Player1Controller : MonoBehaviour
    {
        public event Action MovementStarted;
        public event Action StateChanged;

        [Header("Identity")]
        public string displayName = "Player";
        public int partySlot = 1;

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

        public int CurrentAP { get; private set; }
        public int Initiative { get => initiative; set => initiative = value; }
        public int MaxAP { get => maxAP; set => maxAP = Mathf.Max(0, value); }
        public int PartySlot { get => partySlot; set => partySlot = value; }
        public string DisplayName { get => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName; set => displayName = value; }
        public float MoveSpeedMetersPerAp { get => moveSpeedMetersPerAp; set => moveSpeedMetersPerAp = Mathf.Max(0.01f, value); }
        public bool IsMoving { get; private set; }
        public bool HasEndedRound { get; private set; }

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
            if (allowDebugRefill && Input.GetKeyDown(refillKey))
            {
                BeginRound();
                Debug.Log($"[{DisplayName}] AP refilled to {CurrentAP}");
            }

            if (IsMoving && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
            {
                IsMoving = false;
                StateChanged?.Invoke();
            }
        }

        public void BeginRound()
        {
            CurrentAP = maxAP;
            HasEndedRound = false;
            StateChanged?.Invoke();
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;

            HasEndedRound = true;
            CurrentAP = 0;
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Try to consume AP and start moving along the given complete path.</summary>
        /// <returns>true if move started.</returns>
        public bool TryMove(NavMeshPath path, int apCost)
        {
            if (IsMoving) return false;
            if (HasEndedRound) return false;
            if (path == null || path.status != NavMeshPathStatus.PathComplete) return false;
            if (apCost <= 0 || apCost > CurrentAP) return false;

            _agent.SetPath(path);
            CurrentAP -= apCost;
            IsMoving = true;
            MovementStarted?.Invoke();
            StateChanged?.Invoke();
            return true;
        }
    }
}
