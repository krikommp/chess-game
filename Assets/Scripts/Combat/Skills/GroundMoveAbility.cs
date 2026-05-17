using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// 主动移动 Ability。BlockedTags 控制移动流程权限；Costs 控制 AP 消耗。
    /// 直接调用 NavMeshService + MovementController，不通过 MoveEffect。
    /// </summary>
    [CreateAssetMenu(fileName = "GroundMoveAbility", menuName = "MiniChess/Skill Abilities/Ground Move", order = 20)]
    public class GroundMoveAbility : SkillAbility, ISkillInputHandler
    {
        public SkillCastResult HandleInput(SkillExecutionContext context)
        {
            if (context.CasterExecutor == null)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster executor is null.");

            var request = context.InputRequest;
            if (!request.IsTarget(SkillInputTag.k_TargetGround) || !request.HasWorldPosition)
            {
                PathPreview.Instance?.Clear();
                return SkillCastResult.Success();
            }

            if (request.IsSignal(SkillInputTag.k_PointerHover))
            {
                ShowMovePreview(context.CasterExecutor, request.WorldPosition);
                return SkillCastResult.Success();
            }

            if (!request.IsSignal(SkillInputTag.k_PrimaryPressed))
                return SkillCastResult.Success();

            if (!TryBuildMovePath(context.CasterExecutor, request.WorldPosition,
                    out Vector3 destination, out NavMeshPath path))
            {
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    "Ground move target is not reachable.");
            }

            var moveContext = SkillExecutionContext.ForGroundPoint(context.CasterExecutor, this, destination, path);
            var result = context.CasterExecutor.Execute(moveContext);
            if (result.IsSuccess)
            {
                PathPreview.Instance?.Clear();
                PathPreview.Instance?.ShowActivePath(path.corners);
            }

            return result;
        }

        public override SkillCastResult Execute(SkillExecutionContext context)
        {
            var caster = context.Caster;
            if (caster == null)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is null.");

            var movement = context.CasterMovement;
            if (movement == null)
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Caster has no MovementController.");

            var path = context.Path;
            if (path == null || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    "NavMesh path is invalid or incomplete.");

            // 1. Compute costs (e.g. SpendAP) — failure blocks
            var costResults = ComputeCosts(context);
            for (int i = 0; i < costResults.Length; i++)
            {
                if (!costResults[i].IsSuccess)
                    return SkillCastResult.Fail(costResults[i].Failure, costResults[i].FailureMessage);
            }

            // 2. Execute movement
            if (!movement.TryStartMove(path))
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Failed to start movement via MovementController.");

            // 3. Apply costs after movement starts
            ApplyCosts(context, costResults);

            return SkillCastResult.Success();
        }

        private static void ShowMovePreview(AbilitySystemComponent executor, Vector3 worldPosition)
        {
            var preview = PathPreview.Instance;
            if (preview == null) return;

            var movement = executor.Movement;
            if (movement == null || !TryGetOrigin(executor, out Vector3 origin))
            {
                preview.Clear();
                return;
            }

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit nav,
                    NavMeshService.Instance.MouseSnapRadius, NavMesh.AllAreas))
            {
                preview.Show(System.Array.Empty<Vector3>(), new[] { origin, worldPosition });
                return;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length < 2)
            {
                Vector3[] invalidPath = path.corners != null && path.corners.Length >= 2
                    ? path.corners
                    : new[] { origin, nav.position };
                preview.Show(System.Array.Empty<Vector3>(), invalidPath);
                return;
            }

            float length = NavMeshService.PathLength(path.corners);
            if (length <= movement.RemainingMoveDistance)
            {
                preview.Show(path.corners, System.Array.Empty<Vector3>());
                return;
            }

            NavMeshService.ClipPath(path.corners, movement.RemainingMoveDistance,
                out Vector3[] head, out Vector3[] tail);
            preview.Show(head, tail);
        }

        private static bool TryBuildMovePath(AbilitySystemComponent executor, Vector3 worldPosition,
            out Vector3 destination, out NavMeshPath path)
        {
            destination = default;
            path = null;

            var move = executor.Movement;
            if (move == null || move.RemainingMoveDistance <= 0f) return false;
            float remainingDist = move.RemainingMoveDistance;
            if (!TryGetOrigin(executor, out Vector3 origin)) return false;

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit nav,
                    NavMeshService.Instance.MouseSnapRadius, NavMesh.AllAreas))
                return false;

            var fullPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, fullPath)
                || fullPath.status != NavMeshPathStatus.PathComplete
                || fullPath.corners == null
                || fullPath.corners.Length < 2)
                return false;

            float length = NavMeshService.PathLength(fullPath.corners);
            destination = nav.position;

            if (length > remainingDist + 0.001f)
            {
                NavMeshService.ClipPath(fullPath.corners, remainingDist,
                    out Vector3[] head, out _);
                if (head == null || head.Length < 2) return false;
                destination = head[head.Length - 1];
            }

            path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length < 2)
                return false;

            return true;
        }

        private static bool TryGetOrigin(AbilitySystemComponent executor, out Vector3 origin)
        {
            var casterObject = executor.gameObject;
            if (casterObject != null && NavMesh.SamplePosition(casterObject.transform.position,
                    out NavMeshHit hit, NavMeshService.Instance.OriginSnapRadius, NavMesh.AllAreas))
            {
                origin = hit.position;
                return true;
            }

            origin = casterObject != null ? casterObject.transform.position : Vector3.zero;
            return casterObject != null;
        }
    }
}
