using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Pure-logic shared resolver for NavMesh movement, skill-range entry,
    /// AP estimation, and fallback approach. Used by both player input and AI.
    /// </summary>
    public static class CombatMovementResolver
    {
        public const float k_DefaultNavMeshSnapRadius = 2f;
        public const float k_DefaultOccupiedRadius = 1.0f;

        // ── Result types ────────────────────────────────────────

        public enum EPositioningFailure
        {
            None = 0,
            NavMeshOriginFailed,
            NavMeshTargetUnreachable,
            InsufficientAp,
            DestinationOccupied,
            NoFallbackPossible,
        }

        public struct SkillPositioningResult
        {
            /// <summary>Whether the caster is already within skill range (no movement needed).</summary>
            public bool IsAlreadyInRange { get; set; }

            /// <summary>Whether the caster can reach skill range this turn.</summary>
            public bool CanReachRange { get; set; }

            /// <summary>Whether a fallback approach point is available.</summary>
            public bool HasFallback { get; set; }

            /// <summary>Destination to move to before releasing the skill.</summary>
            public Vector3 Destination { get; set; }

            /// <summary>NavMesh path from origin to Destination.</summary>
            public NavMeshPath MovePath { get; set; }

            /// <summary>AP cost of the movement portion.</summary>
            public int MoveApCost { get; set; }

            /// <summary>moveApCost + skillApCost.</summary>
            public int TotalApCost { get; set; }

            /// <summary>NavMesh path length of the movement portion.</summary>
            public float MoveLength { get; set; }

            /// <summary>Failure reason.</summary>
            public EPositioningFailure Failure { get; set; }

            /// <summary>Farthest reachable approach point when range can't be reached.</summary>
            public Vector3 FallbackDestination { get; set; }

            /// <summary>NavMesh path to FallbackDestination.</summary>
            public NavMeshPath FallbackPath { get; set; }

            /// <summary>AP cost for the fallback movement.</summary>
            public int FallbackMoveApCost { get; set; }

            /// <summary>Whether the primary plan is actionable (in range or can reach).</summary>
            public bool IsActionable => IsAlreadyInRange || CanReachRange;

            /// <summary>Best available path (move path or fallback).</summary>
            public NavMeshPath BestPath => IsActionable ? MovePath : (HasFallback ? FallbackPath : null);

            public static SkillPositioningResult AlreadyInRange()
            {
                return new SkillPositioningResult
                {
                    IsAlreadyInRange = true,
                    CanReachRange = true,
                    Failure = EPositioningFailure.None,
                };
            }

            public static SkillPositioningResult Fail(EPositioningFailure reason)
            {
                return new SkillPositioningResult { Failure = reason };
            }
        }

        // ── NavMesh utilities ───────────────────────────────────

        public static bool TryGetNavMeshPosition(Vector3 position, float snapRadius, out Vector3 navMeshPosition)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, snapRadius, NavMesh.AllAreas))
            {
                navMeshPosition = hit.position;
                return true;
            }

            navMeshPosition = position;
            return false;
        }

        public static bool TryBuildCompletePath(Vector3 origin, Vector3 destination, out NavMeshPath path)
        {
            path = new NavMeshPath();
            return NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete
                && path.corners != null
                && path.corners.Length >= 2;
        }

        // ── Geometry helpers ────────────────────────────────────

        public static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        public static bool IsInRange(Vector3 caster, Vector3 target, float range)
        {
            return Vector3.Distance(caster, target) <= range + 0.05f;
        }

        // ── Occupancy ───────────────────────────────────────────

        /// <summary>Check destination openness against a list of occupied positions.</summary>
        public static bool IsDestinationOpen(
            Vector3 destination,
            float occupiedRadius,
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

        /// <summary>Check destination openness against player and enemy unit transforms.</summary>
        public static bool IsDestinationOpen(
            Vector3 destination,
            float occupiedRadius,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            EnemyController excludeEnemy = null)
        {
            float minSqr = occupiedRadius * occupiedRadius;

            if (players != null)
            {
                foreach (Player1Controller p in players)
                {
                    if (p == null || !p.IsAlive) continue;
                    if (FlatSqrDistance(p.transform.position, destination) < minSqr) return false;
                }
            }

            if (enemies != null)
            {
                foreach (EnemyController e in enemies)
                {
                    if (e == null || e == excludeEnemy || !e.IsAlive) continue;
                    if (FlatSqrDistance(e.transform.position, destination) < minSqr) return false;
                }
            }

            return true;
        }

        // ── Attack destination ──────────────────────────────────

        /// <summary>
        /// Walk backward along path corners from the target to find the point
        /// where accumulated distance to target equals <paramref name="range"/>.
        /// </summary>
        public static bool TryFindAttackDestination(
            Vector3[] corners,
            Vector3 targetPosition,
            float range,
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

        // ── Fallback ────────────────────────────────────────────

        /// <summary>
        /// Walk along corners and find the farthest point within maxDistance.
        /// </summary>
        public static bool TryFindFarthestReachablePoint(
            Vector3[] corners,
            float maxDistance,
            out Vector3 destination)
        {
            if (maxDistance <= 0.001f)
            {
                destination = default;
                return false;
            }

            PathCostCalculator.Clip(corners, maxDistance, out Vector3[] reachable, out _);
            if (reachable == null || reachable.Length < 2)
            {
                destination = default;
                return false;
            }

            destination = reachable[reachable.Length - 1];
            return true;
        }

        // ── AP estimation ───────────────────────────────────────

        /// <summary>
        /// Estimate movement AP cost. Uses the same formula as unit controllers
        /// (FloorToInt of projected distance / speed) but without unpaid-distance carryover.
        /// </summary>
        public static int EstimateMoveApCost(float pathLength, float moveSpeedMetersPerAp, int currentAp, float unpaidMoveDistance = 0f)
        {
            if (pathLength <= 0f || moveSpeedMetersPerAp <= 0f) return 0;
            float projectedDistance = unpaidMoveDistance + pathLength;
            int cost = Mathf.FloorToInt((projectedDistance + 0.0001f) / moveSpeedMetersPerAp);
            return Mathf.Clamp(cost, 0, currentAp);
        }

        // ── Main resolution ─────────────────────────────────────

        /// <summary>
        /// Resolve positioning for a skill cast on a target.
        /// Determines whether the caster is already in range, can reach range,
        /// or can only approach via fallback.
        /// </summary>
        /// <param name="casterPosition">World position of the caster.</param>
        /// <param name="targetPosition">World position of the target.</param>
        /// <param name="skillRange">Range of the skill in meters.</param>
        /// <param name="skillApCost">AP cost of the skill itself.</param>
        /// <param name="currentAp">Caster's current AP.</param>
        /// <param name="remainingMoveDistance">Caster's remaining move distance (CurrentAP * MoveSpeedMetersPerAp - unpaidDistance).</param>
        /// <param name="moveSpeedMetersPerAp">Caster's move speed in meters per AP.</param>
        /// <param name="navMeshSnapRadius">Radius for NavMesh origin sampling.</param>
        public static SkillPositioningResult Resolve(
            Vector3 casterPosition,
            Vector3 targetPosition,
            float skillRange,
            int skillApCost,
            int currentAp,
            float remainingMoveDistance,
            float moveSpeedMetersPerAp,
            float navMeshSnapRadius = k_DefaultNavMeshSnapRadius)
        {
            // 1. Already in range
            if (IsInRange(casterPosition, targetPosition, skillRange))
            {
                return SkillPositioningResult.AlreadyInRange();
            }

            // 2. Sample NavMesh origin
            if (!TryGetNavMeshPosition(casterPosition, navMeshSnapRadius, out Vector3 origin))
            {
                return SkillPositioningResult.Fail(EPositioningFailure.NavMeshOriginFailed);
            }

            // Compute unpaid distance for accurate AP estimation
            float maxPossibleDistance = currentAp * moveSpeedMetersPerAp;
            float unpaidDistance = Mathf.Max(0f, maxPossibleDistance - remainingMoveDistance);

            // 3. Full path to target
            if (!TryBuildCompletePath(origin, targetPosition, out NavMeshPath fullPath))
            {
                return SkillPositioningResult.Fail(EPositioningFailure.NavMeshTargetUnreachable);
            }

            // 4. Find attack destination on path at skillRange distance from target
            if (!TryFindAttackDestination(fullPath.corners, targetPosition, skillRange, out Vector3 attackDest))
            {
                return BuildFallbackResult(origin, fullPath.corners, currentAp,
                    remainingMoveDistance, moveSpeedMetersPerAp, skillApCost);
            }

            // 5. NavMesh path to attack destination
            if (!TryBuildCompletePath(origin, attackDest, out NavMeshPath movePath))
            {
                return BuildFallbackResult(origin, fullPath.corners, currentAp,
                    remainingMoveDistance, moveSpeedMetersPerAp, skillApCost);
            }

            // 6. Calculate costs
            float moveLength = PathCostCalculator.PathLength(movePath.corners);
            int moveApCost = EstimateMoveApCost(moveLength, moveSpeedMetersPerAp, currentAp, unpaidDistance);
            int totalCost = moveApCost + skillApCost;

            // 7. Affordability check
            if (moveLength <= remainingMoveDistance + 0.001f && totalCost <= currentAp)
            {
                return new SkillPositioningResult
                {
                    IsAlreadyInRange = false,
                    CanReachRange = true,
                    Destination = attackDest,
                    MovePath = movePath,
                    MoveApCost = moveApCost,
                    TotalApCost = totalCost,
                    MoveLength = moveLength,
                    Failure = EPositioningFailure.None,
                };
            }

            // 8. Can't afford primary plan — try fallback
            return BuildFallbackResult(origin, fullPath.corners, currentAp,
                remainingMoveDistance, moveSpeedMetersPerAp, skillApCost);
        }

        private static SkillPositioningResult BuildFallbackResult(
            Vector3 origin,
            Vector3[] fullCorners,
            int currentAp,
            float remainingMoveDistance,
            float moveSpeedMetersPerAp,
            int skillApCost)
        {
            if (!TryFindFarthestReachablePoint(fullCorners, remainingMoveDistance, out Vector3 fallbackDest))
            {
                return SkillPositioningResult.Fail(EPositioningFailure.NoFallbackPossible);
            }

            if (!TryBuildCompletePath(origin, fallbackDest, out NavMeshPath fallbackPath))
            {
                return SkillPositioningResult.Fail(EPositioningFailure.NoFallbackPossible);
            }

            float maxPossibleDist = currentAp * moveSpeedMetersPerAp;
            float unpaidDist = Mathf.Max(0f, maxPossibleDist - remainingMoveDistance);
            float fallbackLength = PathCostCalculator.PathLength(fallbackPath.corners);
            int fallbackApCost = EstimateMoveApCost(fallbackLength, moveSpeedMetersPerAp, currentAp, unpaidDist);

            return new SkillPositioningResult
            {
                IsAlreadyInRange = false,
                CanReachRange = false,
                HasFallback = true,
                FallbackDestination = fallbackDest,
                FallbackPath = fallbackPath,
                FallbackMoveApCost = fallbackApCost,
                MoveLength = fallbackLength,
                Failure = EPositioningFailure.InsufficientAp,
            };
        }
    }
}



