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
        }

        private void Update()
        {
            if (player == null || preview == null || cam == null) return;

            // While moving: hide preview, ignore input.
            if (player.IsMoving)
            {
                preview.Clear();
                _hasValidTarget = false;
                return;
            }

            UpdatePreview();

            if (Input.GetMouseButtonDown(0) && _hasValidTarget && _canMoveOnClick)
            {
                if (player.TryMove(_clickMovePath, _clickMoveApCost))
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

            // 1. Mouse → world hit
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
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
            float maxRange = player.CurrentAP * player.MoveSpeedMetersPerAp;
            int cost = PathCostCalculator.ApCost(length, player.MoveSpeedMetersPerAp);

            if (length <= maxRange && cost <= player.CurrentAP)
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

                if (player.CurrentAP > 0 && head != null && head.Length >= 2)
                {
                    Vector3 furthestReachable = head[head.Length - 1];
                    _canMoveOnClick = CacheClickMovePath(furthestReachable, player.CurrentAP);
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
            if (apCost <= 0 || apCost > player.CurrentAP) return false;
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
            if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit, originSnapRadius, NavMesh.AllAreas))
            {
                origin = hit.position;
                return true;
            }

            origin = player.transform.position;
            return false;
        }

        // Expose for HUD
        public bool HasPreview => _hasValidTarget;
        public bool PreviewReachable => _reachable;
        public int PreviewApCost => _previewApCost;
    }
}
