using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Singleton NavMesh utility service. Pure NavMesh API wrapper — no game logic.
    /// Provides path calculation, sampling, distance, occupancy, and AP estimation.
    ///
    /// Merges the former CombatMovementResolver, PathCostCalculator, and NavMeshManager.
    /// Skills use this for pathfinding queries; MovementController handles per-unit execution.
    /// </summary>
    public class NavMeshService : MonoBehaviour
    {
        public static NavMeshService Instance { get; private set; }

        [Header("Snap Radii")]
        [Tooltip("Max distance from mouse hit point to a NavMesh point.")]
        [SerializeField, Min(0.05f)] private float m_mouseSnapRadius = 0.5f;

        [Tooltip("Max distance from unit transform to the nearest NavMesh point.")]
        [SerializeField, Min(0.05f)] private float m_originSnapRadius = 2f;

        public float MouseSnapRadius => m_mouseSnapRadius;
        public float OriginSnapRadius => m_originSnapRadius;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ── NavMesh queries ────────────────────────────────────────

        public bool SamplePosition(Vector3 position, float snapRadius, out Vector3 navMeshPosition)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
            {
                navMeshPosition = hit.position;
                return true;
            }
            navMeshPosition = position;
            return false;
        }

        public bool CalculatePath(Vector3 origin, Vector3 destination, out NavMeshPath path)
        {
            path = new NavMeshPath();
            return NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete
                && path.corners != null
                && path.corners.Length >= 2;
        }

        // ── Path geometry ──────────────────────────────────────────

        public static float PathLength(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2) return 0f;
            float total = 0f;
            for (int i = 1; i < corners.Length; i++)
                total += Vector3.Distance(corners[i - 1], corners[i]);
            return total;
        }

        /// <summary>Walk along corners and split at maxDistance.</summary>
        public static void ClipPath(Vector3[] corners, float maxDistance,
            out Vector3[] reachable, out Vector3[] unreachable)
        {
            if (corners == null || corners.Length == 0)
            {
                reachable = System.Array.Empty<Vector3>();
                unreachable = System.Array.Empty<Vector3>();
                return;
            }

            if (corners.Length == 1 || maxDistance <= 0f)
            {
                reachable = new[] { corners[0] };
                unreachable = corners;
                return;
            }

            float traveled = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                float seg = Vector3.Distance(corners[i - 1], corners[i]);
                if (traveled + seg >= maxDistance)
                {
                    float remain = maxDistance - traveled;
                    float t = seg <= 0.0001f ? 0f : remain / seg;
                    Vector3 cut = Vector3.Lerp(corners[i - 1], corners[i], t);

                    var head = new Vector3[i + 1];
                    System.Array.Copy(corners, 0, head, 0, i);
                    head[i] = cut;

                    int tailLen = corners.Length - i + 1;
                    var tail = new Vector3[tailLen];
                    tail[0] = cut;
                    System.Array.Copy(corners, i, tail, 1, corners.Length - i);

                    reachable = head;
                    unreachable = tail;
                    return;
                }
                traveled += seg;
            }

            reachable = corners;
            unreachable = System.Array.Empty<Vector3>();
        }

        /// <summary>
        /// Walk along corners and find the farthest point within maxDistance.
        /// </summary>
        public bool FindFarthestReachablePoint(Vector3[] corners, float maxDistance, out Vector3 destination)
        {
            if (maxDistance <= 0.001f)
            {
                destination = default;
                return false;
            }

            ClipPath(corners, maxDistance, out Vector3[] reachable, out _);
            if (reachable == null || reachable.Length < 2)
            {
                destination = default;
                return false;
            }

            destination = reachable[reachable.Length - 1];
            return true;
        }

        /// <summary>
        /// Walk backward along path corners from the target to find the point
        /// where accumulated distance to target equals range.
        /// </summary>
        public static bool FindAttackDestination(
            Vector3[] corners, Vector3 targetPosition, float range,
            out Vector3 destination)
        {
            if (corners == null || corners.Length < 2)
            {
                destination = default;
                return false;
            }

            float distanceFromTarget = 0f;
            for (int i = corners.Length - 1; i > 0; i--)
            {
                Vector3 current = corners[i];
                Vector3 previous = corners[i - 1];
                float segmentLength = Vector3.Distance(current, previous);

                if (distanceFromTarget + segmentLength >= range)
                {
                    float remaining = range - distanceFromTarget;
                    float t = segmentLength <= 0.0001f ? 0f : remaining / segmentLength;
                    destination = Vector3.Lerp(current, previous, t);
                    return true;
                }
                distanceFromTarget += segmentLength;
            }

            destination = default;
            return false;
        }

        // ── Geometry ───────────────────────────────────────────────

        public static bool IsInRange(Vector3 a, Vector3 b, float range)
        {
            return Vector3.Distance(a, b) <= range + 0.05f;
        }

        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        // ── Occupancy ──────────────────────────────────────────────

        /// <summary>Check if a position is open against a set of occupied positions.</summary>
        public static bool IsDestinationOpen(
            Vector3 destination, float occupiedRadius,
            IReadOnlyList<Vector3> occupiedPositions,
            Vector3? excludePosition = null)
        {
            float minSqr = occupiedRadius * occupiedRadius;
            if (occupiedPositions != null)
            {
                foreach (Vector3 pos in occupiedPositions)
                {
                    if (excludePosition.HasValue && FlatSqrDistance(pos, excludePosition.Value) < 0.0001f)
                        continue;
                    if (FlatSqrDistance(pos, destination) < minSqr)
                        return false;
                }
            }
            return true;
        }

        /// <summary>Check if a position is open against all combat units.</summary>
        public bool IsDestinationOpenAgainstUnits(
            Vector3 destination, float occupiedRadius,
            GameObject excludeUnit = null,
            bool excludeDead = true)
        {
            float minSqr = occupiedRadius * occupiedRadius;
            foreach (var cu in FindObjectsOfType<CombatUnit>())
            {
                if (!cu.gameObject.activeInHierarchy) continue;
                if (excludeUnit != null && cu.gameObject == excludeUnit) continue;
                if (excludeDead)
                {
                    var attr = cu.GetComponent<AttributeSet>();
                    if (attr != null && !attr.IsAlive) continue;
                }
                if (FlatSqrDistance(cu.transform.position, destination) < minSqr)
                    return false;
            }
            return true;
        }

        // ── AP estimation ──────────────────────────────────────────

        public static int EstimateMoveApCost(float pathLength, float moveSpeedMetersPerAp,
            int currentAp, float unpaidMoveDistance = 0f)
        {
            if (pathLength <= 0f || moveSpeedMetersPerAp <= 0f) return 0;
            float projectedDistance = unpaidMoveDistance + pathLength;
            int cost = Mathf.FloorToInt((projectedDistance + 0.0001f) / moveSpeedMetersPerAp);
            return Mathf.Clamp(cost, 0, currentAp);
        }
    }
}
