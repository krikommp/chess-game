using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Combat.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Handles enemy AI turns. Subscribes to CombatRoundManager.UnitTurnStarted,
    /// filters for Faction.Enemy, and runs move + attack decision loop.
    /// </summary>
    public class EnemyTurnRunner : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CombatRoundManager m_roundManager;
        [SerializeField] private CameraController m_cameraController;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float m_turnStartDelay = 0.25f;
        [SerializeField, Min(0f)] private float m_moveDelay = 0.3f;
        [SerializeField, Min(0f)] private float m_skillDelay = 1.0f;

        [Header("Movement")]
        [SerializeField, Min(0.05f)] private float m_navMeshSnapRadius = 2f;
        [SerializeField, Min(0.1f)] private float m_approachRange = 1.5f;
        [SerializeField, Min(0.1f)] private float m_occupiedRadius = 1.0f;
        [SerializeField, Range(4, 24)] private int m_occupancyProbeCount = 12;
        [SerializeField, Min(0.1f)] private float m_movementTimeoutSeconds = 8f;

        private void OnEnable()
        {
            if (m_roundManager != null)
                m_roundManager.UnitTurnStarted += OnUnitTurnStarted;
        }

        private void OnDisable()
        {
            if (m_roundManager != null)
                m_roundManager.UnitTurnStarted -= OnUnitTurnStarted;
        }

        private void OnUnitTurnStarted(GameObject unit)
        {
            if (unit == null) return;

            var attr = unit.GetComponent<AttributeSet>();
            if (attr == null || attr.Faction != EFaction.Enemy) return;

            StartCoroutine(RunAITurn(unit));
        }

        private IEnumerator RunAITurn(GameObject unit)
        {
            m_roundManager.IsWaiting = true;

            // Camera focus
            if (m_cameraController != null)
                m_cameraController.FocusOn(unit.transform);

            // Flash enemy turn indicator
            var controller = unit.GetComponent<EnemyController>();
            controller?.FlashTurn();

            yield return new WaitForSeconds(m_turnStartDelay);

            var executor = unit.GetComponent<SkillExecutor>();
            var attr = unit.GetComponent<AttributeSet>();

            // Find target: nearest alive player
            var target = FindNearestEnemy(unit);
            if (target != null && executor != null)
            {
                // Try move then attack
                yield return StartCoroutine(MoveTowardTarget(unit, target));

                yield return new WaitForSeconds(m_moveDelay);

                // Try basic_attack
                var attackSkill = executor.FindSkill("basic_attack");
                if (attackSkill != null)
                {
                    var canCast = executor.CanCast(attackSkill, target);
                    if (canCast.IsSuccess)
                    {
                        executor.Execute(attackSkill, target);
                        yield return new WaitForSeconds(m_skillDelay);
                    }
                }
            }

            m_roundManager.EndTurn(unit);
            m_roundManager.IsWaiting = false;
        }

        private GameObject FindNearestEnemy(GameObject self)
        {
            var selfFaction = self.GetComponent<AttributeSet>()?.Faction;
            GameObject best = null;
            float bestDist = float.MaxValue;

            foreach (var go in m_roundManager.TurnOrder)
            {
                if (go == null || go == self) continue;
                var attr = go.GetComponent<AttributeSet>();
                if (attr == null || !attr.IsAlive) continue;
                if (attr.Faction == selfFaction) continue;

                float dist = Vector3.Distance(self.transform.position, go.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = go;
                }
            }
            return best;
        }

        private IEnumerator MoveTowardTarget(GameObject unit, GameObject target)
        {
            var moveExec = unit.GetComponent<MovementController>();
            if (moveExec == null || moveExec.IsMoving) yield break;

            var attr = unit.GetComponent<AttributeSet>();
            float currentAP = attr?.Get(WellKnownAttributeTags.AP) ?? 0f;
            if (currentAP <= 0f) yield break;

            float moveSpeed = attr?.Get(WellKnownAttributeTags.MoveSpeed) ?? 1f;
            float maxMoveDist = currentAP * moveSpeed;

            if (!CombatMovementResolver.TryGetNavMeshPosition(unit.transform.position, m_navMeshSnapRadius, out var origin))
                yield break;

            if (!CombatMovementResolver.TryBuildCompletePath(origin, target.transform.position, out var fullPath))
                yield break;

            if (!CombatMovementResolver.TryFindFarthestReachablePoint(fullPath.corners, maxMoveDist, out var farthestPoint))
                yield break;

            if (!NavMesh.SamplePosition(farthestPoint, out var destHit, m_navMeshSnapRadius, NavMesh.AllAreas))
                yield break;

            if (!CombatMovementResolver.TryBuildCompletePath(origin, destHit.position, out var movePath))
                yield break;

            float pathLength = PathCostCalculator.PathLength(movePath.corners);
            int moveCost = moveExec.PreviewMovementApCost(pathLength);
            if (moveCost > currentAP) yield break;

            moveExec.TryMove(movePath);

            float waitStart = Time.time;
            while (moveExec.IsMoving)
            {
                if (Time.time - waitStart > m_movementTimeoutSeconds)
                {
                    moveExec.StopMovement();
                    break;
                }
                yield return null;
            }
        }
    }
}
