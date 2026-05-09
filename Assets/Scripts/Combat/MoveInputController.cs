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

        // Hover tracking for player visual feedback.
        private Player1Controller _hoveredPlayer;

        // Last computed preview state (for click handling).
        private bool _hasValidTarget;
        private bool _reachable;
        private int _previewApCost;
        private bool _canMoveOnClick;
        private int _clickMoveApCost;

        private void Awake()
        {
            _path = new NavMeshPath();
            _clickMovePath = new NavMeshPath();
            if (cam == null) cam = Camera.main;
            if (combatManager == null) combatManager = FindObjectOfType<CombatRoundManager>();
        }

        private void Update()
        {
            Player1Controller activePlayer = GetActivePlayer();
            if (activePlayer == null || preview == null || cam == null) return;

            // While moving: hide preview, ignore input.
            if (activePlayer.IsMoving || activePlayer.HasEndedRound)
            {
                preview.Clear();
                _hasValidTarget = false;
                return;
            }

            UpdatePreview();

            if (Input.GetMouseButtonDown(0))
            {
                if (TrySelectPlayerUnderCursor())
                {
                    preview.Clear();
                    _hasValidTarget = false;
                    _canMoveOnClick = false;
                    return;
                }

                if (_hasValidTarget && _canMoveOnClick && activePlayer.TryMove(_clickMovePath, _clickMoveApCost))
                {
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

            // 3. Validate hit is on a ground layer
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
            float maxRange = activePlayer.CurrentAP * activePlayer.MoveSpeedMetersPerAp;
            int cost = PathCostCalculator.ApCost(length, activePlayer.MoveSpeedMetersPerAp);

            if (length <= maxRange && cost <= activePlayer.CurrentAP)
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

                if (activePlayer.CurrentAP > 0 && head != null && head.Length >= 2)
                {
                    Vector3 furthestReachable = head[head.Length - 1];
                    _canMoveOnClick = CacheClickMovePath(furthestReachable, activePlayer.CurrentAP);
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
            if (apCost <= 0 || apCost > activePlayer.CurrentAP) return false;
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

        // Expose for HUD
        public bool HasPreview => _hasValidTarget;
        public bool PreviewReachable => _reachable;
        public int PreviewApCost => _previewApCost;
    }
}
