using System.Collections;
using MiniChess.Combat.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Handles enemy AI turns. Subscribes to CombatRoundManager.UnitTurnStarted,
    /// filters for Control.AI units, and runs move + attack decision loop.
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

            // Check Control.AI tag instead of EnemyController component
            var tagComp = unit.GetComponent<GameplayTags.GameplayTagComponent>();
            if (tagComp == null || !tagComp.HasTag(new GameplayTags.GameplayTag("Control.AI"),
                    GameplayTags.ETagMatchMode.Exact))
                return;

            StartCoroutine(RunAITurn(unit));
        }

        private IEnumerator RunAITurn(GameObject unit)
        {
            m_roundManager.IsWaiting = true;

            if (m_cameraController != null)
                m_cameraController.FocusOn(unit.transform);

            yield return new WaitForSeconds(m_turnStartDelay);

            var executor = unit.GetComponent<AbilitySystemComponent>();

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

            var nav = NavMeshService.Instance;
            if (!nav.SamplePosition(unit.transform.position, m_navMeshSnapRadius, out var origin))
                yield break;

            if (!nav.CalculatePath(origin, target.transform.position, out var fullPath))
                yield break;

            if (!nav.FindFarthestReachablePoint(fullPath.corners, maxMoveDist, out var farthestPoint))
                yield break;

            if (!NavMesh.SamplePosition(farthestPoint, out var destHit, m_navMeshSnapRadius, NavMesh.AllAreas))
                yield break;

            if (!nav.CalculatePath(origin, destHit.position, out var movePath))
                yield break;

            float pathLength = NavMeshService.PathLength(movePath.corners);
            if (pathLength > maxMoveDist) yield break;

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
