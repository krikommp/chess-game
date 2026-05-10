using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Main entry: every frame raycast mouse → ground, sample NavMesh, calc path,
    /// drive PathPreview, and on left-click execute either the full path or the
    /// furthest reachable point on that path.
    /// </summary>
    public class MoveInputController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera m_cam;
        [SerializeField] private Player1Controller m_player;
        [SerializeField] private CombatRoundManager m_combatManager;
        [SerializeField] private PathPreview m_preview;

        [Header("Raycast")]
        [Tooltip("Layers considered as 'ground' for mouse raycast (e.g. Ground + Obstacle).")]
        [SerializeField] private LayerMask m_groundMask = ~0;

        [Tooltip("Max distance from mouse hit point to a NavMesh point. Smaller = stricter.")]
        [SerializeField, Min(0.05f)] private float m_navMeshSnapRadius = 0.5f;

        [Tooltip("Max distance from m_player transform to the nearest NavMesh point. Handles capsule pivot height.")]
        [SerializeField, Min(0.05f)] private float m_originSnapRadius = 2f;

        // cached, reused per frame to avoid GC
        private NavMeshPath m_path;
        private NavMeshPath m_clickMovePath;
        private NavMeshPath m_activeMovePath;

        // Hover tracking for m_player visual feedback.
        private Player1Controller m_hoveredPlayer;

        // Attack mode state (hovering over an enemy).
        private bool m_isAttackMode;
        private EnemyController m_targetEnemy;
        private bool m_attackRequiresMove;
        private int m_attackMoveApCost;
        private Vector3 m_attackDestination;

        // Last computed m_preview state (for click handling).
        private bool m_hasValidTarget;
        private bool m_reachable;
        private int m_previewApCost;
        private bool m_canMoveOnClick;
        private int m_clickMoveApCost;
        private bool m_hasActiveMoveTarget;
        private Vector3 m_activeMoveTarget;

        private void Awake()
        {
            m_path = new NavMeshPath();
            m_clickMovePath = new NavMeshPath();
            m_activeMovePath = new NavMeshPath();
            if (m_cam == null) m_cam = Camera.main;
            if (m_combatManager == null) m_combatManager = FindObjectOfType<CombatRoundManager>();
        }

        private void Update()
        {
            // Block input during enemy turns
            if (m_combatManager != null && m_combatManager.IsWaiting)
            {
                m_preview?.Clear();
                m_hasValidTarget = false;
                m_isAttackMode = false;
                m_attackRequiresMove = false;
                ClearActiveMovementPath();
                return;
            }

            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer == null || m_preview == null || m_cam == null) return;

            if (activePlayer.HasEndedRound)
            {
                m_preview.Clear();
                m_hasValidTarget = false;
                m_isAttackMode = false;
                ClearActiveMovementPath();
                return;
            }

            UpdateActiveMovementPath(activePlayer);
            UpdatePreview();

            if (Input.GetMouseButtonDown(0))
            {
                if (TrySelectPlayerUnderCursor())
                {
                    m_preview.Clear();
                    m_hasValidTarget = false;
                    m_canMoveOnClick = false;
                    m_isAttackMode = false;
                    m_attackRequiresMove = false;
                    return;
                }

                if (m_isAttackMode && m_targetEnemy != null && m_targetEnemy.IsAlive && activePlayer.CurrentAP >= m_attackMoveApCost + 1)
                {
                    ExecuteAttack(activePlayer, m_targetEnemy);
                    return;
                }

                if (!m_isAttackMode && m_hasValidTarget && m_canMoveOnClick && activePlayer.TryMove(m_clickMovePath, m_clickMoveApCost))
                {
                    StartActiveMovementPath(m_clickMovePath);
                    m_preview.Clear();
                    m_hasValidTarget = false;
                    m_canMoveOnClick = false;
                }
            }
        }

        private void UpdatePreview()
        {
            m_hasValidTarget = false;
            m_reachable = false;
            m_previewApCost = 0;
            m_canMoveOnClick = false;
            m_clickMoveApCost = 0;
            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer == null) return;

            // 1. Mouse → world hit (all layers to detect players)
            Ray ray = m_cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                m_preview.Clear();
                ClearHoveredPlayer();
                return;
            }

            // 2. If hovering a m_player, show hover color and skip path drawing
            Player1Controller hitPlayer = hit.collider.GetComponentInParent<Player1Controller>();
            if (hitPlayer != null && m_combatManager != null && m_combatManager.TurnOrder.Contains(hitPlayer))
            {
                m_preview.Clear();
                if (m_hoveredPlayer != null && m_hoveredPlayer != hitPlayer)
                {
                    if (m_hoveredPlayer != m_combatManager?.SelectedPlayer)
                        m_hoveredPlayer.SetVisualState(EPlayerVisualState.Default);
                }
                m_hoveredPlayer = hitPlayer;
                if (hitPlayer != activePlayer)
                {
                    hitPlayer.SetVisualState(EPlayerVisualState.Hovered);
                }
                return;
            }

            ClearHoveredPlayer();

            // 3. If hovering an enemy, show attack path m_preview
            EnemyController hitEnemy = hit.collider.GetComponentInParent<EnemyController>();
            if (hitEnemy != null && hitEnemy.IsAlive && m_combatManager != null)
            {
                m_preview.Clear();
                ShowAttackPreview(activePlayer, hitEnemy);
                return;
            }

            m_isAttackMode = false;
            m_targetEnemy = null;
            m_attackRequiresMove = false;

            // 4. Validate hit is on a ground layer
            if (((1 << hit.collider.gameObject.layer) & m_groundMask) == 0)
            {
                m_preview.Clear();
                return;
            }

            Vector3 hitPoint = hit.point;
            if (!TryGetNavMeshOrigin(out Vector3 origin))
            {
                m_preview.Clear();
                return;
            }

            // 2. Snap to NavMesh. Failure = invalid target (wall / NotWalkable area).
            if (!NavMesh.SamplePosition(hitPoint, out NavMeshHit nav, m_navMeshSnapRadius, NavMesh.AllAreas))
            {
                ShowInvalidLine(origin, hitPoint);
                return;
            }

            Vector3 target = nav.position;

            // 3. Compute path. Partial / Invalid = unreachable.
            if (!NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, m_path)
                || m_path.status != NavMeshPathStatus.PathComplete
                || m_path.corners == null || m_path.corners.Length < 2)
            {
                // Use whatever partial corners we have (or a straight stub) to show red.
                Vector3[] redPath = (m_path != null && m_path.corners != null && m_path.corners.Length >= 2)
                    ? m_path.corners
                    : new[] { origin, target };
                m_preview.Show(System.Array.Empty<Vector3>(), redPath);
                m_hasValidTarget = true; // we have a target, just unreachable
                m_reachable = false;
                return;
            }

            // 4. Length + AP
            float length = PathCostCalculator.PathLength(m_path.corners);
            float maxRange = activePlayer.RemainingMoveDistance;
            int cost = activePlayer.PreviewMovementApCost(length);

            if (length <= maxRange)
            {
                m_preview.Show(m_path.corners, System.Array.Empty<Vector3>());
                m_hasValidTarget = true;
                m_reachable = true;
                m_previewApCost = cost;
                m_canMoveOnClick = CacheClickMovePath(target, cost);
            }
            else
            {
                // Split at AP budget. Clicking a too-far target still moves to the
                // furthest reachable point on the same NavMesh path.
                PathCostCalculator.Clip(m_path.corners, maxRange,
                    out Vector3[] head, out Vector3[] tail);
                m_preview.Show(head, tail);
                m_hasValidTarget = true;
                m_reachable = false;
                m_previewApCost = cost;

                if (maxRange > 0f && head != null && head.Length >= 2)
                {
                    Vector3 furthestReachable = head[head.Length - 1];
                    m_canMoveOnClick = CacheClickMovePath(furthestReachable, activePlayer.PreviewMovementApCost(maxRange));
                }
            }
        }

        private void ShowInvalidLine(Vector3 origin, Vector3 target)
        {
            m_preview.Show(System.Array.Empty<Vector3>(), new[] { origin, target });
            m_hasValidTarget = false;
            m_reachable = false;
        }

        private bool CacheClickMovePath(Vector3 destination, int apCost)
        {
            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer == null) return false;
            if (!TryGetNavMeshOrigin(out Vector3 origin)) return false;

            bool foundPath = NavMesh.CalculatePath(
                origin,
                destination,
                NavMesh.AllAreas,
                m_clickMovePath);

            if (!foundPath
                || m_clickMovePath.status != NavMeshPathStatus.PathComplete
                || m_clickMovePath.corners == null
                || m_clickMovePath.corners.Length < 2)
            {
                return false;
            }

            float length = PathCostCalculator.PathLength(m_clickMovePath.corners);
            if (length <= 0.001f || length > activePlayer.RemainingMoveDistance + 0.001f)
            {
                return false;
            }

            m_clickMoveApCost = apCost;
            return true;
        }

        private bool TryGetNavMeshOrigin(out Vector3 origin)
        {
            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer != null && NavMesh.SamplePosition(activePlayer.transform.position, out NavMeshHit hit, m_originSnapRadius, NavMesh.AllAreas))
            {
                origin = hit.position;
                return true;
            }

            origin = activePlayer != null ? activePlayer.transform.position : Vector3.zero;
            return false;
        }

        public void SetPlayer(Player1Controller nextPlayer)
        {
            m_player = nextPlayer;
            m_preview?.Clear();
            ClearActiveMovementPath();
            m_hasValidTarget = false;
            m_canMoveOnClick = false;
        }

        private Player1Controller GetActivePlayer()
        {
            return m_combatManager != null && m_combatManager.SelectedPlayer != null
                ? m_combatManager.SelectedPlayer
                : m_player;
        }

        private bool TrySelectPlayerUnderCursor()
        {
            if (m_combatManager == null) return false;

            Ray ray = m_cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Player1Controller hitPlayer = hit.collider.GetComponentInParent<Player1Controller>();
            return hitPlayer != null && m_combatManager.TrySelectPlayer(hitPlayer);
        }

        private void ClearHoveredPlayer()
        {
            if (m_hoveredPlayer != null)
            {
                if (m_hoveredPlayer != m_combatManager?.SelectedPlayer)
                    m_hoveredPlayer.SetVisualState(EPlayerVisualState.Default);
                m_hoveredPlayer = null;
            }
        }

        private void ShowAttackPreview(Player1Controller player, EnemyController enemy)
        {
            float attackRange = m_combatManager != null ? m_combatManager.AttackRange : 1.5f;

            var result = CombatMovementResolver.Resolve(
                casterPosition: player.transform.position,
                targetPosition: enemy.transform.position,
                skillRange: attackRange,
                skillApCost: 1,
                currentAp: player.CurrentAP,
                remainingMoveDistance: player.RemainingMoveDistance,
                moveSpeedMetersPerAp: player.MoveSpeedMetersPerAp,
                navMeshSnapRadius: m_originSnapRadius);

            if (result.IsAlreadyInRange)
            {
                m_attackDestination = player.transform.position;
                m_attackRequiresMove = false;
                m_attackMoveApCost = 0;
                m_isAttackMode = true;
                m_targetEnemy = enemy;
                m_hasValidTarget = true;
                m_reachable = true;
                m_previewApCost = 1;
                m_canMoveOnClick = false;
                m_preview.Clear();
                return;
            }

            if (result.CanReachRange)
            {
                m_preview.Show(result.MovePath.corners, System.Array.Empty<Vector3>());
                m_hasValidTarget = true;
                m_reachable = true;
                m_previewApCost = result.TotalApCost;
                m_isAttackMode = true;
                m_targetEnemy = enemy;
                m_attackRequiresMove = true;
                m_attackMoveApCost = result.MoveApCost;
                m_attackDestination = result.Destination;
                return;
            }

            if (result.HasFallback && result.FallbackPath != null)
            {
                // Show partial path: reachable head + unreachable tail
                PathCostCalculator.Clip(result.FallbackPath.corners, player.RemainingMoveDistance,
                    out Vector3[] head, out Vector3[] tail);

                // tail is the portion from fallback point toward target
                m_preview.Show(head,
                    tail != null && tail.Length >= 2 ? tail : System.Array.Empty<Vector3>());
                m_hasValidTarget = true;
                m_reachable = false;
                m_previewApCost = result.TotalApCost;
                m_isAttackMode = true;
                m_targetEnemy = enemy;
                m_attackRequiresMove = false;
                return;
            }

            // Unreachable
            m_hasValidTarget = false;
            m_isAttackMode = true;
            m_targetEnemy = enemy;
            m_attackRequiresMove = false;
        }

        private void ExecuteAttack(Player1Controller player, EnemyController enemy)
        {
            if (m_attackRequiresMove)
            {
                if (!CombatMovementResolver.TryGetNavMeshPosition(player.transform.position, m_originSnapRadius, out Vector3 origin))
                {
                    ClearAttackState();
                    return;
                }

                if (!CombatMovementResolver.TryBuildCompletePath(origin, m_attackDestination, out NavMeshPath movePath))
                {
                    ClearAttackState();
                    return;
                }

                if (!player.TryMove(movePath, m_attackMoveApCost + 1))
                {
                    ClearAttackState();
                    return;
                }

                StartActiveMovementPath(movePath);

                if (!player.TrySpendAP(1))
                {
                    return;
                }
            }
            else
            {
                if (!player.TrySpendAP(1))
                {
                    return;
                }
            }

            enemy.TakeDamage(20);
            Debug.Log($"[Combat] {player.DisplayName} attacks {enemy.DisplayName} for 20 damage ({enemy.CurrentHP}/{enemy.MaxHP} HP)");

            ClearAttackState();
        }

        private void ClearAttackState()
        {
            m_preview.Clear();
            m_hasValidTarget = false;
            m_isAttackMode = false;
            m_attackRequiresMove = false;
            m_targetEnemy = null;
        }

        // Expose for HUD
        public bool HasPreview => m_hasValidTarget;
        public bool PreviewReachable => m_reachable;
        public int PreviewApCost => m_previewApCost;
        public bool IsAttackMode => m_isAttackMode;
        public string AttackTargetName => m_targetEnemy != null ? m_targetEnemy.DisplayName : "";

        private void StartActiveMovementPath(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                ClearActiveMovementPath();
                return;
            }

            m_activeMoveTarget = path.corners[path.corners.Length - 1];
            m_hasActiveMoveTarget = true;
            m_preview?.ShowActivePath(path.corners);
        }

        private void UpdateActiveMovementPath(Player1Controller activePlayer)
        {
            if (!m_hasActiveMoveTarget) return;

            if (activePlayer == null || !activePlayer.IsMoving)
            {
                ClearActiveMovementPath();
                return;
            }

            if (!TryGetNavMeshOrigin(out Vector3 origin))
            {
                ClearActiveMovementPath();
                return;
            }

            bool foundPath = NavMesh.CalculatePath(
                origin,
                m_activeMoveTarget,
                NavMesh.AllAreas,
                m_activeMovePath);

            if (!foundPath
                || m_activeMovePath.status != NavMeshPathStatus.PathComplete
                || m_activeMovePath.corners == null
                || m_activeMovePath.corners.Length < 2)
            {
                m_preview?.ClearActivePath();
                return;
            }

            m_preview?.ShowActivePath(m_activeMovePath.corners);
        }

        private void ClearActiveMovementPath()
        {
            m_hasActiveMoveTarget = false;
            m_preview?.ClearActivePath();
        }
    }
}





