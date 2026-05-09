using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// MVP enemy turn executor: pick the nearest living player, move into basic
    /// attack range if possible, then attack once.
    /// </summary>
    public class EnemyTurnRunner : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float turnStartDelay = 0.25f;
        [SerializeField, Min(0f)] private float afterActionDelay = 0.25f;
        [SerializeField, Min(0.05f)] private float navMeshSnapRadius = 2f;
        [SerializeField, Min(0.1f)] private float movementTimeoutSeconds = 8f;
        [SerializeField, Min(0.2f)] private float occupiedRadius = 1.0f;
        [SerializeField, Range(4, 24)] private int occupancyProbeCount = 12;

        public IEnumerator RunTurn(
            EnemyController enemy,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            float attackRange,
            int attackCost,
            int attackDamage)
        {
            if (enemy == null || !enemy.IsAlive)
            {
                yield break;
            }

            yield return new WaitForSeconds(turnStartDelay);

            Player1Controller target = FindNearestLivingPlayer(enemy, players);
            if (target == null)
            {
                Debug.Log($"[EnemyAI] {enemy.DisplayName} found no living player target.");
                yield break;
            }

            if (!IsInAttackRange(enemy.transform.position, target.transform.position, attackRange))
            {
                TryMoveTowardTarget(enemy, target, players, enemies, attackRange, attackCost);

                float waitStart = Time.time;
                while (enemy.IsMoving)
                {
                    if (Time.time - waitStart > movementTimeoutSeconds)
                    {
                        enemy.StopMovement();
                        Debug.LogWarning($"[EnemyAI] {enemy.DisplayName} movement timed out.");
                        break;
                    }

                    yield return null;
                }
            }

            if (target.IsAlive
                && IsInAttackRange(enemy.transform.position, target.transform.position, attackRange)
                && enemy.TrySpendAP(attackCost))
            {
                target.TakeDamage(attackDamage);
                Debug.Log($"[EnemyAI] {enemy.DisplayName} attacks {target.DisplayName} for {attackDamage} damage ({target.CurrentHP}/{target.MaxHP} HP).");
            }
            else
            {
                Debug.Log($"[EnemyAI] {enemy.DisplayName} cannot attack {target.DisplayName}; AP {enemy.CurrentAP}/{enemy.MaxAP}.");
            }

            yield return new WaitForSeconds(afterActionDelay);
        }

        private Player1Controller FindNearestLivingPlayer(EnemyController enemy, IReadOnlyList<Player1Controller> players)
        {
            Player1Controller best = null;
            float bestDistance = float.MaxValue;

            if (players == null) return null;

            foreach (Player1Controller player in players)
            {
                if (player == null || !player.IsAlive) continue;

                float distance = Vector3.Distance(enemy.transform.position, player.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player;
                }
            }

            return best;
        }

        private void TryMoveTowardTarget(
            EnemyController enemy,
            Player1Controller target,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            float attackRange,
            int attackCost)
        {
            if (enemy.CurrentAP <= 0) return;
            if (!TryGetNavMeshPosition(enemy.transform.position, out Vector3 origin)) return;

            NavMeshPath fullPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, target.transform.position, NavMesh.AllAreas, fullPath)
                || fullPath.status != NavMeshPathStatus.PathComplete
                || fullPath.corners == null
                || fullPath.corners.Length < 2)
            {
                return;
            }

            Vector3 destination = default;
            bool canReachAttackPoint = false;

            if (TryFindAttackDestination(fullPath.corners, target.transform.position, attackRange, out Vector3 attackDestination))
            {
                canReachAttackPoint = TryFindOpenAttackDestination(
                    enemy,
                    target,
                    players,
                    enemies,
                    origin,
                    attackDestination,
                    attackRange,
                    attackCost,
                    out destination);
            }

            if (canReachAttackPoint)
            {
                // Destination was assigned by TryFindOpenAttackDestination.
            }
            else if (!TryFindFarthestReachablePoint(enemy, fullPath.corners, out Vector3 farthestReachable)
                || !TryFindOpenMoveDestination(enemy, players, enemies, origin, farthestReachable, out destination))
            {
                return;
            }

            if (!TryBuildCompletePath(origin, destination, out NavMeshPath movePath)) return;

            float moveLength = PathCostCalculator.PathLength(movePath.corners);
            int moveCost = enemy.PreviewMovementApCost(moveLength);

            if (enemy.TryMove(movePath))
            {
                Debug.Log($"[EnemyAI] {enemy.DisplayName} moves toward {target.DisplayName} ({moveCost} AP movement preview).");
            }
        }

        private bool TryFindOpenAttackDestination(
            EnemyController enemy,
            Player1Controller target,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            Vector3 origin,
            Vector3 preferred,
            float attackRange,
            int attackCost,
            out Vector3 destination)
        {
            if (TryUseDestination(enemy, players, enemies, origin, preferred, attackCost, out destination))
            {
                return true;
            }

            Vector3 targetPosition = target.transform.position;
            Vector3 baseDirection = preferred - targetPosition;
            if (baseDirection.sqrMagnitude <= 0.001f)
            {
                baseDirection = enemy.transform.position - targetPosition;
            }

            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude <= 0.001f) baseDirection = Vector3.forward;
            baseDirection.Normalize();

            float baseAngle = Mathf.Atan2(baseDirection.z, baseDirection.x) * Mathf.Rad2Deg;
            for (int i = 0; i < occupancyProbeCount; i++)
            {
                int step = (i + 1) / 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                float angle = baseAngle + sign * step * (360f / occupancyProbeCount);
                Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector3 candidate = targetPosition + direction * attackRange;

                if (TryUseDestination(enemy, players, enemies, origin, candidate, attackCost, out destination))
                {
                    return true;
                }
            }

            destination = default;
            return false;
        }

        private bool TryFindOpenMoveDestination(
            EnemyController enemy,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            Vector3 origin,
            Vector3 preferred,
            out Vector3 destination)
        {
            if (TryUseDestination(enemy, players, enemies, origin, preferred, reserveAp: 0, out destination))
            {
                return true;
            }

            for (int i = 0; i < occupancyProbeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / occupancyProbeCount;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * occupiedRadius;
                if (TryUseDestination(enemy, players, enemies, origin, preferred + offset, reserveAp: 0, out destination))
                {
                    return true;
                }
            }

            destination = default;
            return false;
        }

        private bool TryUseDestination(
            EnemyController enemy,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            Vector3 origin,
            Vector3 candidate,
            int reserveAp,
            out Vector3 destination)
        {
            if (!TryGetNavMeshPosition(candidate, out Vector3 navDestination)
                || !IsDestinationOpen(enemy, players, enemies, navDestination)
                || !TryBuildCompletePath(origin, navDestination, out NavMeshPath path)
                || !CanAffordMove(enemy, path, reserveAp))
            {
                destination = default;
                return false;
            }

            destination = navDestination;
            return true;
        }

        private bool IsDestinationOpen(
            EnemyController movingEnemy,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            Vector3 destination)
        {
            float minSqrDistance = occupiedRadius * occupiedRadius;

            if (players != null)
            {
                foreach (Player1Controller player in players)
                {
                    if (player == null || !player.IsAlive) continue;
                    if (FlatSqrDistance(player.transform.position, destination) < minSqrDistance) return false;
                }
            }

            if (enemies != null)
            {
                foreach (EnemyController enemy in enemies)
                {
                    if (enemy == null || enemy == movingEnemy || !enemy.IsAlive) continue;
                    if (FlatSqrDistance(enemy.transform.position, destination) < minSqrDistance) return false;
                }
            }

            return true;
        }

        private static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude;
        }

        private bool CanAffordMove(EnemyController enemy, NavMeshPath path, int reserveAp)
        {
            float moveLength = PathCostCalculator.PathLength(path.corners);
            int moveCost = enemy.PreviewMovementApCost(moveLength);
            return moveCost + reserveAp <= enemy.CurrentAP;
        }

        private bool TryBuildCompletePath(Vector3 origin, Vector3 destination, out NavMeshPath path)
        {
            path = new NavMeshPath();
            return NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete
                && path.corners != null
                && path.corners.Length >= 2;
        }

        private bool TryFindFarthestReachablePoint(EnemyController enemy, Vector3[] fullPathCorners, out Vector3 destination)
        {
            float maxMoveDistance = enemy.RemainingMoveDistance;
            if (maxMoveDistance <= 0.001f)
            {
                destination = default;
                return false;
            }

            PathCostCalculator.Clip(fullPathCorners, maxMoveDistance, out Vector3[] reachable, out _);
            if (reachable == null || reachable.Length < 2)
            {
                destination = default;
                return false;
            }

            destination = reachable[reachable.Length - 1];
            return true;
        }

        private bool TryFindAttackDestination(Vector3[] corners, Vector3 targetPosition, float attackRange, out Vector3 destination)
        {
            float distanceFromTarget = 0f;

            for (int i = corners.Length - 1; i > 0; i--)
            {
                Vector3 current = corners[i];
                Vector3 previous = corners[i - 1];
                float segmentLength = Vector3.Distance(current, previous);

                if (distanceFromTarget + segmentLength >= attackRange)
                {
                    float remaining = attackRange - distanceFromTarget;
                    float t = segmentLength <= 0.0001f ? 0f : remaining / segmentLength;
                    destination = Vector3.Lerp(current, previous, t);
                    return true;
                }

                distanceFromTarget += segmentLength;
            }

            destination = default;
            return false;
        }

        private bool TryGetNavMeshPosition(Vector3 position, out Vector3 navMeshPosition)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSnapRadius, NavMesh.AllAreas))
            {
                navMeshPosition = hit.position;
                return true;
            }

            navMeshPosition = position;
            return false;
        }

        private static bool IsInAttackRange(Vector3 attacker, Vector3 target, float attackRange)
        {
            return Vector3.Distance(attacker, target) <= attackRange + 0.05f;
        }
    }
}
