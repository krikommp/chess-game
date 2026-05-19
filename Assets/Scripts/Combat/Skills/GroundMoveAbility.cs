using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// 主动移动 Ability。BlockedTags 控制移动流程权限；Costs 可通过 Function 持续扣除移动消耗。
    /// 直接调用 NavMeshService + MovementController，不通过 MoveEffect。
    /// </summary>
    [CreateAssetMenu(fileName = "GroundMoveAbility", menuName = "MiniChess/Skill Abilities/Ground Move", order = 20)]
    public class GroundMoveAbility : SkillAbility, ISkillInputHandler, ISkillUpdate
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
                ShowMovePreview(context, request.WorldPosition);
                return SkillCastResult.Success();
            }

            if (!request.IsSignal(SkillInputTag.k_PrimaryPressed))
                return SkillCastResult.Success();

            if (!TryBuildMovePath(context, request.WorldPosition,
                    out Vector3 destination, out NavMeshPath path))
            {
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    "Ground move target is not reachable.");
            }

            var moveContext = SkillExecutionContext.ForGroundPoint(context.CasterExecutor, context.Spec, destination, path);
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
            var tagResult = CheckAbilityTags(context);
            if (!tagResult.IsSuccess)
                return tagResult;

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

            var costResults = ComputeCosts(context);
            for (int i = 0; i < costResults.Length; i++)
            {
                if (!costResults[i].IsSuccess)
                    return SkillCastResult.Fail(costResults[i].Failure, costResults[i].FailureMessage);
            }

            var costApplyResult = ApplyCosts(context, costResults);
            if (!costApplyResult.IsSuccess)
                return SkillCastResult.Fail(costApplyResult.Failure, costApplyResult.FailureMessage);

            if (!movement.TryStartMove(path))
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Failed to start movement via MovementController.");

            context.CasterExecutor.BeginSkillUpdate(new SkillExecutionState(context, costResults, caster.transform.position));
            return SkillCastResult.Success();
        }

        public SkillCastResult SkillUpdate(SkillExecutionState state, float deltaTime)
        {
            var movement = state.Context.CasterMovement;
            var caster = state.Context.Caster;
            if (movement == null || caster == null || !movement.IsMoving)
            {
                state.Complete();
                return SkillCastResult.Success();
            }

            state.RecordCasterPosition(caster.transform.position);

            var costResult = UpdateCosts(state, deltaTime);
            if (!costResult.IsSuccess)
            {
                state.Complete();
                return SkillCastResult.Fail(costResult.Failure, costResult.FailureMessage);
            }

            if (!movement.IsMoving)
                state.Complete();

            return SkillCastResult.Success();
        }

        private void ShowMovePreview(SkillExecutionContext context, Vector3 worldPosition)
        {
            var preview = PathPreview.Instance;
            if (preview == null) return;

            var executor = context.CasterExecutor;
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
            var moveContext = SkillExecutionContext.ForGroundPoint(executor, context.Spec, nav.position, path);
            var costPreview = PreviewCosts(moveContext, length);
            if (!costPreview.IsSuccess || costPreview.MaxPathLength <= 0.001f)
            {
                preview.Show(System.Array.Empty<Vector3>(), path.corners);
                return;
            }

            if (length <= costPreview.MaxPathLength)
            {
                preview.Show(path.corners, System.Array.Empty<Vector3>());
                return;
            }

            NavMeshService.ClipPath(path.corners, costPreview.MaxPathLength,
                out Vector3[] head, out Vector3[] tail);
            preview.Show(head, tail);
        }

        private bool TryBuildMovePath(SkillExecutionContext context, Vector3 worldPosition,
            out Vector3 destination, out NavMeshPath path)
        {
            destination = default;
            path = null;

            var executor = context.CasterExecutor;
            var move = executor.Movement;
            if (move == null) return false;
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
            var moveContext = SkillExecutionContext.ForGroundPoint(executor, context.Spec, nav.position, fullPath);
            var costPreview = PreviewCosts(moveContext, length);
            if (!costPreview.IsSuccess || costPreview.MaxPathLength <= 0.001f)
                return false;

            if (length > costPreview.MaxPathLength + 0.001f)
            {
                NavMeshService.ClipPath(fullPath.corners, costPreview.MaxPathLength,
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
