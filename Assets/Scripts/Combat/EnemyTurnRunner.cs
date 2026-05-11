using System.Collections;
using System.Collections.Generic;
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
            IReadOnlyList<EnemyController> enemies)
        {
            var enemyAttr = enemy.GetComponent<AttributeSet>();
            var enemyMove = enemy.GetComponent<MovementController>();

            if (enemy == null || !IsAlive(enemy, enemyAttr)) yield break;

            yield return new WaitForSeconds(m_turnStartDelay);

            Player1Controller target = FindNearestLivingPlayer(enemy, players);
            if (target == null)
            {
                Debug.Log($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} found no living player target.");
                yield break;
            }

            // Move toward nearest player using available AP
            TryMoveTowardTarget(enemy, target.gameObject, players, enemies, enemyAttr, enemyMove);

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
            AttributeSet enemyAttr,
            MovementController enemyMove)
        {
            float currentAP = enemyAttr != null ? enemyAttr.Get(WellKnownAttributeTags.AP) : enemy.CurrentAP;
            if (currentAP <= 0f) return;

            float moveSpeed = enemyAttr != null ? enemyAttr.Get(WellKnownAttributeTags.MoveSpeed) : enemy.MoveSpeedMetersPerAp;
            float maxMoveDist = currentAP * moveSpeed;

            if (!CombatMovementResolver.TryGetNavMeshPosition(enemy.transform.position, m_navMeshSnapRadius, out Vector3 origin))
                return;

            if (!CombatMovementResolver.TryBuildCompletePath(origin, target.transform.position, out NavMeshPath fullPath))
                return;

            if (!CombatMovementResolver.TryFindFarthestReachablePoint(fullPath.corners, maxMoveDist, out Vector3 farthestPoint))
                return;

            if (!TryFindOpenMoveDestination(enemy, players, enemies, origin, farthestPoint, enemyAttr, enemyMove, out Vector3 destination))
                return;

            if (!CombatMovementResolver.TryBuildCompletePath(origin, destination, out NavMeshPath movePath))
                return;

            float moveLength = PathCostCalculator.PathLength(movePath.corners);
            int moveCost = PreviewMovementCost(enemy, enemyMove, moveLength);

            if (TryMove(enemy, enemyMove, movePath))
            {
                Debug.Log($"[EnemyAI] {GetDisplayName(enemy, enemyAttr)} moves toward {target.name} ({moveCost} AP).");
            }
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
