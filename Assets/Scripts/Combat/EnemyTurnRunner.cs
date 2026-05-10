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
        [SerializeField, Min(0f)] private float m_turnStartDelay = 0.25f;
        [SerializeField, Min(0f)] private float m_afterActionDelay = 0.25f;
        [SerializeField, Min(0.05f)] private float m_navMeshSnapRadius = 2f;
        [SerializeField, Min(0.1f)] private float m_movementTimeoutSeconds = 8f;
        [SerializeField, Min(0.2f)] private float m_occupiedRadius = 1.0f;
        [SerializeField, Range(4, 24)] private int m_occupancyProbeCount = 12;

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

            yield return new WaitForSeconds(m_turnStartDelay);

            Player1Controller target = FindNearestLivingPlayer(enemy, players);
            if (target == null)
            {
                Debug.Log($"[EnemyAI] {enemy.DisplayName} found no living player target.");
                yield break;
            }

            if (!CombatMovementResolver.IsInRange(enemy.transform.position, target.transform.position, attackRange))
            {
                TryMoveTowardTarget(enemy, target, players, enemies, attackRange, attackCost);

                float waitStart = Time.time;
                while (enemy.IsMoving)
                {
                    if (Time.time - waitStart > m_movementTimeoutSeconds)
                    {
                        enemy.StopMovement();
                        Debug.LogWarning($"[EnemyAI] {enemy.DisplayName} movement timed out.");
                        break;
                    }

                    yield return null;
                }
            }

            if (target.IsAlive
                && CombatMovementResolver.IsInRange(enemy.transform.position, target.transform.position, attackRange)
                && enemy.TrySpendAP(attackCost))
            {
                target.TakeDamage(attackDamage);
                Debug.Log($"[EnemyAI] {enemy.DisplayName} attacks {target.DisplayName} for {attackDamage} damage ({target.CurrentHP}/{target.MaxHP} HP).");
            }
            else
            {
                Debug.Log($"[EnemyAI] {enemy.DisplayName} cannot attack {target.DisplayName}; AP {enemy.CurrentAP}/{enemy.MaxAP}.");
            }

            yield return new WaitForSeconds(m_afterActionDelay);
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

            var positioning = CombatMovementResolver.Resolve(
                casterPosition: enemy.transform.position,
                targetPosition: target.transform.position,
                skillRange: attackRange,
                skillApCost: attackCost,
                currentAp: enemy.CurrentAP,
                remainingMoveDistance: enemy.RemainingMoveDistance,
                moveSpeedMetersPerAp: enemy.MoveSpeedMetersPerAp,
                navMeshSnapRadius: m_navMeshSnapRadius);

            if (!CombatMovementResolver.TryGetNavMeshPosition(enemy.transform.position, m_navMeshSnapRadius, out Vector3 origin))
                return;

            Vector3 destination = default;

            if (positioning.CanReachRange)
            {
                // Try the preferred attack destination first, then probe alternatives
                if (TryFindOpenAttackDestination(enemy, players, enemies, origin,
                    positioning.Destination, target.transform.position, attackRange, attackCost, out destination))
                {
                    // Found an open attack destination
                }
                else
                {
                    // Can't find open attack point — fall back to approach
                    if (!TryFindFarthestReachablePoint(enemy, positioning.MovePath.corners, out Vector3 farthest)
                        || !TryFindOpenMoveDestination(enemy, players, enemies, origin, farthest, out destination))
                    {
                        return;
                    }
                }
            }
            else if (positioning.HasFallback)
            {
                // Try fallback approach point
                if (!TryFindOpenMoveDestination(enemy, players, enemies, origin,
                    positioning.FallbackDestination, out destination))
                {
                    return;
                }
            }
            else
            {
                return;
            }

            if (!CombatMovementResolver.TryBuildCompletePath(origin, destination, out NavMeshPath movePath))
                return;

            float moveLength = PathCostCalculator.PathLength(movePath.corners);
            int moveCost = enemy.PreviewMovementApCost(moveLength);

            if (enemy.TryMove(movePath))
            {
                Debug.Log($"[EnemyAI] {enemy.DisplayName} moves toward {target.DisplayName} ({moveCost} AP movement preview).");
            }
        }

        private bool TryFindOpenAttackDestination(
            EnemyController enemy,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            Vector3 origin,
            Vector3 preferred,
            Vector3 targetPosition,
            float attackRange,
            int attackCost,
            out Vector3 destination)
        {
            if (TryUseDestination(enemy, players, enemies, origin, preferred, attackCost, out destination))
                return true;

            // Probe a ring around the target at attackRange distance
            Vector3 baseDirection = preferred - targetPosition;
            baseDirection.y = 0f;
            if (baseDirection.sqrMagnitude <= 0.001f)
            {
                baseDirection = enemy.transform.position - targetPosition;
                baseDirection.y = 0f;
            }
            if (baseDirection.sqrMagnitude <= 0.001f) baseDirection = Vector3.forward;
            baseDirection.Normalize();

            float baseAngle = Mathf.Atan2(baseDirection.z, baseDirection.x) * Mathf.Rad2Deg;
            for (int i = 0; i < m_occupancyProbeCount; i++)
            {
                int step = (i + 1) / 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                float angle = baseAngle + sign * step * (360f / m_occupancyProbeCount);
                Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                Vector3 candidate = targetPosition + direction * attackRange;

                if (TryUseDestination(enemy, players, enemies, origin, candidate, attackCost, out destination))
                    return true;
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
                return true;

            for (int i = 0; i < m_occupancyProbeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / m_occupancyProbeCount;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * m_occupiedRadius;
                if (TryUseDestination(enemy, players, enemies, origin, preferred + offset, reserveAp: 0, out destination))
                    return true;
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
            if (!CombatMovementResolver.TryGetNavMeshPosition(candidate, m_navMeshSnapRadius, out Vector3 navDestination)
                || !CombatMovementResolver.IsDestinationOpen(navDestination, m_occupiedRadius, players, enemies, enemy)
                || !CombatMovementResolver.TryBuildCompletePath(origin, navDestination, out NavMeshPath path))
            {
                destination = default;
                return false;
            }

            float moveLength = PathCostCalculator.PathLength(path.corners);
            int moveCost = enemy.PreviewMovementApCost(moveLength);
            if (moveCost + reserveAp > enemy.CurrentAP)
            {
                destination = default;
                return false;
            }

            destination = navDestination;
            return true;
        }

        private bool TryFindFarthestReachablePoint(EnemyController enemy, Vector3[] fullPathCorners, out Vector3 destination)
        {
            return CombatMovementResolver.TryFindFarthestReachablePoint(
                fullPathCorners, enemy.RemainingMoveDistance, out destination);
        }
    }
}


