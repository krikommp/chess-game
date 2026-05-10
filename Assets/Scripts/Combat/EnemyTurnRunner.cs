using System.Collections;
using System.Collections.Generic;
using MiniChess.Combat.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
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
            SkillDefinition skill)
        {
            var enemyAttr = enemy.GetComponent<AttributeSet>();
            var enemyMove = enemy.GetComponent<MovementController>();

            if (enemy == null || !IsAlive(enemy, enemyAttr)) yield break;

            yield return new WaitForSeconds(m_turnStartDelay);

            var skillExecutor = enemy.GetComponent<SkillExecutor>();
            if (skillExecutor == null)
            {
                Debug.LogWarning($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} has no SkillExecutor.");
                yield break;
            }

            if (skill == null)
            {
                Debug.LogWarning($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} has no skill assigned.");
                yield break;
            }

            Player1Controller target = FindNearestLivingPlayer(enemy, players);
            if (target == null)
            {
                Debug.Log($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} found no living player target.");
                yield break;
            }

            // 1. Move into range if needed
            if (!CombatMovementResolver.IsInRange(enemy.transform.position, target.transform.position, skill.Range))
            {
                TryMoveTowardTarget(enemy, target.gameObject, players, enemies, skill, enemyAttr, enemyMove);

                float waitStart = Time.time;
                while (IsMovingEnemy(enemy, enemyMove))
                {
                    if (Time.time - waitStart > m_movementTimeoutSeconds)
                    {
                        enemyMove?.StopMovement();
                        enemy.StopMovement();
                        Debug.LogWarning($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} movement timed out.");
                        break;
                    }
                    yield return null;
                }
            }

            // 2. Execute skill via shared post-move entry
            var result = skillExecutor.ExecuteAfterMove(skill, target.gameObject);
            if (result.IsSuccess)
            {
                Debug.Log($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} casts '{skill.DisplayName}' on {GetDisplayName(target)}.");
            }
            else
            {
                Debug.Log($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} failed to cast '{skill.DisplayName}': {result.FailureMessage}");
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
                if (player == null) continue;
                var playerAttr = player.GetComponent<AttributeSet>();
                if (playerAttr != null ? !playerAttr.IsAlive : !player.IsAlive) continue;

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
            GameObject target,
            IReadOnlyList<Player1Controller> players,
            IReadOnlyList<EnemyController> enemies,
            SkillDefinition skill,
            AttributeSet enemyAttr,
            MovementController enemyMove)
        {
            float currentAP = enemyAttr != null ? enemyAttr.Get(WellKnownAttributeTags.AP) : enemy.CurrentAP;
            if (currentAP <= 0f) return;

            float remainingMove = enemyMove != null ? enemyMove.RemainingMoveDistance : enemy.RemainingMoveDistance;
            float moveSpeed = enemyAttr != null ? enemyAttr.Get(WellKnownAttributeTags.MoveSpeed) : enemy.MoveSpeedMetersPerAp;

            var positioning = CombatMovementResolver.Resolve(
                casterPosition: enemy.transform.position,
                targetPosition: target.transform.position,
                skillRange: skill.Range,
                skillApCost: skill.ApCost,
                currentAp: (int)currentAP,
                remainingMoveDistance: remainingMove,
                moveSpeedMetersPerAp: moveSpeed,
                navMeshSnapRadius: m_navMeshSnapRadius);

            if (!CombatMovementResolver.TryGetNavMeshPosition(enemy.transform.position, m_navMeshSnapRadius, out Vector3 origin))
                return;

            Vector3 destination = default;

            if (positioning.CanReachRange)
            {
                if (TryFindOpenAttackDestination(enemy, players, enemies, origin,
                    positioning.Destination, target.transform.position, skill.Range, skill.ApCost, enemyAttr, enemyMove, out destination))
                {
                }
                else
                {
                    if (!TryFindFarthestReachablePoint(enemy, enemyMove, positioning.MovePath.corners, out Vector3 farthest)
                        || !TryFindOpenMoveDestination(enemy, players, enemies, origin, farthest, enemyAttr, enemyMove, out destination))
                    {
                        return;
                    }
                }
            }
            else if (positioning.HasFallback)
            {
                if (!TryFindOpenMoveDestination(enemy, players, enemies, origin,
                    positioning.FallbackDestination, enemyAttr, enemyMove, out destination))
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
            int moveCost = PreviewMovementCost(enemy, enemyMove, moveLength);

            if (TryMove(enemy, enemyMove, movePath))
            {
                Debug.Log($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} moves toward {target.name} ({moveCost} AP movement preview).");
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
            AttributeSet enemyAttr,
            MovementController enemyMove,
            out Vector3 destination)
        {
            if (TryUseDestination(enemy, players, enemies, origin, preferred, attackCost, enemyAttr, enemyMove, out destination))
                return true;

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

                if (TryUseDestination(enemy, players, enemies, origin, candidate, attackCost, enemyAttr, enemyMove, out destination))
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
            AttributeSet enemyAttr,
            MovementController enemyMove,
            out Vector3 destination)
        {
            if (TryUseDestination(enemy, players, enemies, origin, preferred, 0, enemyAttr, enemyMove, out destination))
                return true;

            for (int i = 0; i < m_occupancyProbeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / m_occupancyProbeCount;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * m_occupiedRadius;
                if (TryUseDestination(enemy, players, enemies, origin, preferred + offset, 0, enemyAttr, enemyMove, out destination))
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
            AttributeSet enemyAttr,
            MovementController enemyMove,
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
            int moveCost = PreviewMovementCost(enemy, enemyMove, moveLength);
            float currentAP = enemyAttr != null ? enemyAttr.Get(WellKnownAttributeTags.AP) : enemy.CurrentAP;
            if (moveCost + reserveAp > currentAP)
            {
                destination = default;
                return false;
            }

            destination = navDestination;
            return true;
        }

        private bool TryFindFarthestReachablePoint(EnemyController enemy, MovementController enemyMove, Vector3[] fullPathCorners, out Vector3 destination)
        {
            float remainingDist = enemyMove != null ? enemyMove.RemainingMoveDistance : enemy.RemainingMoveDistance;
            return CombatMovementResolver.TryFindFarthestReachablePoint(fullPathCorners, remainingDist, out destination);
        }

        // ── Bridge helpers ──────────────────────────────────────────

        private static bool IsAlive(EnemyController enemy, AttributeSet attr)
        {
            return attr != null ? attr.IsAlive : enemy.IsAlive;
        }

        private static bool IsMovingEnemy(EnemyController enemy, MovementController move)
        {
            return move != null ? move.IsMoving : enemy.IsMoving;
        }

        private static string GetDisplayName(EnemyController enemy, AttributeSet attr)
        {
            return attr != null ? attr.DisplayName : enemy.DisplayName;
        }

        private static string GetDisplayName(Player1Controller player)
        {
            var attr = player.GetComponent<AttributeSet>();
            return attr != null ? attr.DisplayName : player.DisplayName;
        }

        private static bool TryMove(EnemyController enemy, MovementController move, NavMeshPath path)
        {
            if (move != null) return move.TryMove(path);
            return enemy.TryMove(path);
        }

        private static int PreviewMovementCost(EnemyController enemy, MovementController move, float pathLength)
        {
            if (move != null) return move.PreviewMovementApCost(pathLength);
            return enemy.PreviewMovementApCost(pathLength);
        }
    }
}
