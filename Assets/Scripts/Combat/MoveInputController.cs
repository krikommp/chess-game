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
        public Camera cam;
        public Player1Controller player;
        public CombatRoundManager combatManager;
        public PathPreview preview;

        [Header("Raycast")]
        [Tooltip("Layers considered as 'ground' for mouse raycast (e.g. Ground + Obstacle).")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Max distance from mouse hit point to a NavMesh point. Smaller = stricter.")]
        [SerializeField, Min(0.05f)] private float navMeshSnapRadius = 0.5f;

        [Tooltip("Max distance from player transform to the nearest NavMesh point. Handles capsule pivot height.")]
        [SerializeField, Min(0.05f)] private float originSnapRadius = 2f;

        // cached, reused per frame to avoid GC
        private NavMeshPath _path;
        private NavMeshPath _clickMovePath;
        private NavMeshPath _activeMovePath;

        // Hover tracking for player visual feedback.
        private Player1Controller _hoveredPlayer;

        // Attack mode state (hovering over an enemy).
        private bool _isAttackMode;
        private EnemyController _targetEnemy;
        private bool _attackRequiresMove;
        private int _attackMoveApCost;
        private Vector3 _attackDestination;

        // Last computed preview state (for click handling).
        private bool _hasValidTarget;
        private bool _reachable;
        private int _previewApCost;
        private bool _canMoveOnClick;
        private int _clickMoveApCost;
        private bool _hasActiveMoveTarget;
        private Vector3 _activeMoveTarget;

        private void Awake()
        {
            _path = new NavMeshPath();
            _clickMovePath = new NavMeshPath();
            _activeMovePath = new NavMeshPath();
            if (cam == null) cam = Camera.main;
            if (combatManager == null) combatManager = FindObjectOfType<CombatRoundManager>();
        }

        private void Update()
        {
            // Block input during enemy turns
            if (combatManager != null && combatManager.IsWaiting)
            {
                preview?.Clear();
                _hasValidTarget = false;
                _isAttackMode = false;
                _attackRequiresMove = false;
                ClearActiveMovementPath();
                return;
            }

            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer == null || preview == null || cam == null) return;

            if (activePlayer.HasEndedRound)
            {
                preview.Clear();
                _hasValidTarget = false;
                _isAttackMode = false;
                ClearActiveMovementPath();
                return;
            }

            UpdateActiveMovementPath(activePlayer);
            UpdatePreview();

            if (Input.GetMouseButtonDown(0))
            {
                if (TrySelectPlayerUnderCursor())
                {
                    preview.Clear();
                    _hasValidTarget = false;
                    _canMoveOnClick = false;
                    _isAttackMode = false;
                    _attackRequiresMove = false;
                    return;
                }

                if (_isAttackMode && _targetEnemy != null && _targetEnemy.IsAlive && activePlayer.CurrentAP >= _attackMoveApCost + 1)
                {
                    ExecuteAttack(activePlayer, _targetEnemy);
                    return;
                }

                if (!_isAttackMode && _hasValidTarget && _canMoveOnClick && activePlayer.TryMove(_clickMovePath, _clickMoveApCost))
                {
                    StartActiveMovementPath(_clickMovePath);
                    preview.Clear();
                    _hasValidTarget = false;
                    _canMoveOnClick = false;
                }
            }
        }

        private void UpdatePreview()
        {
            _hasValidTarget = false;
            _reachable = false;
            _previewApCost = 0;
            _canMoveOnClick = false;
            _clickMoveApCost = 0;
            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer == null) return;

            // 1. Mouse → world hit (all layers to detect players)
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                preview.Clear();
                ClearHoveredPlayer();
                return;
            }

            // 2. If hovering a player, show hover color and skip path drawing
            Player1Controller hitPlayer = hit.collider.GetComponentInParent<Player1Controller>();
            if (hitPlayer != null && combatManager != null && combatManager.TurnOrder.Contains(hitPlayer))
            {
                preview.Clear();
                if (_hoveredPlayer != null && _hoveredPlayer != hitPlayer)
                {
                    if (_hoveredPlayer != combatManager?.SelectedPlayer)
                        _hoveredPlayer.SetVisualState(PlayerVisualState.Default);
                }
                _hoveredPlayer = hitPlayer;
                if (hitPlayer != activePlayer)
                {
                    hitPlayer.SetVisualState(PlayerVisualState.Hovered);
                }
                return;
            }

            ClearHoveredPlayer();

            // 3. If hovering an enemy, show attack path preview
            EnemyController hitEnemy = hit.collider.GetComponentInParent<EnemyController>();
            if (hitEnemy != null && hitEnemy.IsAlive && combatManager != null)
            {
                preview.Clear();
                ShowAttackPreview(activePlayer, hitEnemy);
                return;
            }

            _isAttackMode = false;
            _targetEnemy = null;
            _attackRequiresMove = false;

            // 4. Validate hit is on a ground layer
            if (((1 << hit.collider.gameObject.layer) & groundMask) == 0)
            {
                preview.Clear();
                return;
            }

            Vector3 hitPoint = hit.point;
            if (!TryGetNavMeshOrigin(out Vector3 origin))
            {
                preview.Clear();
                return;
            }

            // 2. Snap to NavMesh. Failure = invalid target (wall / NotWalkable area).
            if (!NavMesh.SamplePosition(hitPoint, out NavMeshHit nav, navMeshSnapRadius, NavMesh.AllAreas))
            {
                ShowInvalidLine(origin, hitPoint);
                return;
            }

            Vector3 target = nav.position;

            // 3. Compute path. Partial / Invalid = unreachable.
            if (!NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, _path)
                || _path.status != NavMeshPathStatus.PathComplete
                || _path.corners == null || _path.corners.Length < 2)
            {
                // Use whatever partial corners we have (or a straight stub) to show red.
                Vector3[] redPath = (_path != null && _path.corners != null && _path.corners.Length >= 2)
                    ? _path.corners
                    : new[] { origin, target };
                preview.Show(System.Array.Empty<Vector3>(), redPath);
                _hasValidTarget = true; // we have a target, just unreachable
                _reachable = false;
                return;
            }

            // 4. Length + AP
            float length = PathCostCalculator.PathLength(_path.corners);
            float maxRange = activePlayer.RemainingMoveDistance;
            int cost = activePlayer.PreviewMovementApCost(length);

            if (length <= maxRange)
            {
                preview.Show(_path.corners, System.Array.Empty<Vector3>());
                _hasValidTarget = true;
                _reachable = true;
                _previewApCost = cost;
                _canMoveOnClick = CacheClickMovePath(target, cost);
            }
            else
            {
                // Split at AP budget. Clicking a too-far target still moves to the
                // furthest reachable point on the same NavMesh path.
                PathCostCalculator.Clip(_path.corners, maxRange,
                    out Vector3[] head, out Vector3[] tail);
                preview.Show(head, tail);
                _hasValidTarget = true;
                _reachable = false;
                _previewApCost = cost;

                if (maxRange > 0f && head != null && head.Length >= 2)
                {
                    Vector3 furthestReachable = head[head.Length - 1];
                    _canMoveOnClick = CacheClickMovePath(furthestReachable, activePlayer.PreviewMovementApCost(maxRange));
                }
            }
        }

        private void ShowInvalidLine(Vector3 origin, Vector3 target)
        {
            preview.Show(System.Array.Empty<Vector3>(), new[] { origin, target });
            _hasValidTarget = false;
            _reachable = false;
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
                _clickMovePath);

            if (!foundPath
                || _clickMovePath.status != NavMeshPathStatus.PathComplete
                || _clickMovePath.corners == null
                || _clickMovePath.corners.Length < 2)
            {
                return false;
            }

            float length = PathCostCalculator.PathLength(_clickMovePath.corners);
            if (length <= 0.001f || length > activePlayer.RemainingMoveDistance + 0.001f)
            {
                return false;
            }

            _clickMoveApCost = apCost;
            return true;
        }

        private bool TryGetNavMeshOrigin(out Vector3 origin)
        {
            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer != null && NavMesh.SamplePosition(activePlayer.transform.position, out NavMeshHit hit, originSnapRadius, NavMesh.AllAreas))
            {
                origin = hit.position;
                return true;
            }

            origin = activePlayer != null ? activePlayer.transform.position : Vector3.zero;
            return false;
        }

        public void SetPlayer(Player1Controller nextPlayer)
        {
            player = nextPlayer;
            preview?.Clear();
            ClearActiveMovementPath();
            _hasValidTarget = false;
            _canMoveOnClick = false;
        }

        private Player1Controller GetActivePlayer()
        {
            return combatManager != null && combatManager.SelectedPlayer != null
                ? combatManager.SelectedPlayer
                : player;
        }

        private bool TrySelectPlayerUnderCursor()
        {
            if (combatManager == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            Player1Controller hitPlayer = hit.collider.GetComponentInParent<Player1Controller>();
            return hitPlayer != null && combatManager.TrySelectPlayer(hitPlayer);
        }

        private void ClearHoveredPlayer()
        {
            if (_hoveredPlayer != null)
            {
                if (_hoveredPlayer != combatManager?.SelectedPlayer)
                    _hoveredPlayer.SetVisualState(PlayerVisualState.Default);
                _hoveredPlayer = null;
            }
        }

        private void ShowAttackPreview(Player1Controller player, EnemyController enemy)
        {
            float attackRange = combatManager != null ? combatManager.AttackRange : 1.5f;

            var result = CombatMovementResolver.Resolve(
                casterPosition: player.transform.position,
                targetPosition: enemy.transform.position,
                skillRange: attackRange,
                skillApCost: 1,
                currentAp: player.CurrentAP,
                remainingMoveDistance: player.RemainingMoveDistance,
                moveSpeedMetersPerAp: player.MoveSpeedMetersPerAp,
                navMeshSnapRadius: originSnapRadius);

            if (result.IsAlreadyInRange)
            {
                _attackDestination = player.transform.position;
                _attackRequiresMove = false;
                _attackMoveApCost = 0;
                _isAttackMode = true;
                _targetEnemy = enemy;
                _hasValidTarget = true;
                _reachable = true;
                _previewApCost = 1;
                _canMoveOnClick = false;
                preview.Clear();
                return;
            }

            if (result.CanReachRange)
            {
                preview.Show(result.MovePath.corners, System.Array.Empty<Vector3>());
                _hasValidTarget = true;
                _reachable = true;
                _previewApCost = result.TotalApCost;
                _isAttackMode = true;
                _targetEnemy = enemy;
                _attackRequiresMove = true;
                _attackMoveApCost = result.MoveApCost;
                _attackDestination = result.Destination;
                return;
            }

            if (result.HasFallback && result.FallbackPath != null)
            {
                // Show partial path: reachable head + unreachable tail
                PathCostCalculator.Clip(result.FallbackPath.corners, player.RemainingMoveDistance,
                    out Vector3[] head, out Vector3[] tail);

                // tail is the portion from fallback point toward target
                preview.Show(head,
                    tail != null && tail.Length >= 2 ? tail : System.Array.Empty<Vector3>());
                _hasValidTarget = true;
                _reachable = false;
                _previewApCost = result.TotalApCost;
                _isAttackMode = true;
                _targetEnemy = enemy;
                _attackRequiresMove = false;
                return;
            }

            // Unreachable
            _hasValidTarget = false;
            _isAttackMode = true;
            _targetEnemy = enemy;
            _attackRequiresMove = false;
        }

        private void ExecuteAttack(Player1Controller player, EnemyController enemy)
        {
            if (_attackRequiresMove)
            {
                if (!CombatMovementResolver.TryGetNavMeshPosition(player.transform.position, originSnapRadius, out Vector3 origin))
                {
                    ClearAttackState();
                    return;
                }

                if (!CombatMovementResolver.TryBuildCompletePath(origin, _attackDestination, out NavMeshPath movePath))
                {
                    ClearAttackState();
                    return;
                }

                if (!player.TryMove(movePath, _attackMoveApCost + 1))
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
            preview.Clear();
            _hasValidTarget = false;
            _isAttackMode = false;
            _attackRequiresMove = false;
            _targetEnemy = null;
        }

        // Expose for HUD
        public bool HasPreview => _hasValidTarget;
        public bool PreviewReachable => _reachable;
        public int PreviewApCost => _previewApCost;
        public bool IsAttackMode => _isAttackMode;
        public string AttackTargetName => _targetEnemy != null ? _targetEnemy.DisplayName : "";

        private void StartActiveMovementPath(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                ClearActiveMovementPath();
                return;
            }

            _activeMoveTarget = path.corners[path.corners.Length - 1];
            _hasActiveMoveTarget = true;
            preview?.ShowActivePath(path.corners);
        }

        private void UpdateActiveMovementPath(Player1Controller activePlayer)
        {
            if (!_hasActiveMoveTarget) return;

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
                _activeMoveTarget,
                NavMesh.AllAreas,
                _activeMovePath);

            if (!foundPath
                || _activeMovePath.status != NavMeshPathStatus.PathComplete
                || _activeMovePath.corners == null
                || _activeMovePath.corners.Length < 2)
            {
                preview?.ClearActivePath();
                return;
            }

            preview?.ShowActivePath(_activeMovePath.corners);
        }

        private void ClearActiveMovementPath()
        {
            _hasActiveMoveTarget = false;
            preview?.ClearActivePath();
        }
    }
}
