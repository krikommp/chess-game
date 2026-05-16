using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// 主动移动 Ability。BlockedTags 控制移动流程权限；Costs 控制 AP 消耗。
    /// 直接调用 NavMeshService + MovementController，不通过 MoveEffect。
    /// HandleInput 已移出到 UnitTurnHandler。
    /// </summary>
    [CreateAssetMenu(fileName = "GroundMoveAbility", menuName = "MiniChess/Skill Abilities/Ground Move", order = 20)]
    public class GroundMoveAbility : SkillAbility
    {
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

            // 1. Compute costs (e.g. SpendAP): failure blocks.
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
            var costApplyResult = ApplyCosts(context, costResults);
            if (!costApplyResult.IsSuccess)
            {
                movement.StopMovement();
                return SkillCastResult.Fail(costApplyResult.Failure, costApplyResult.FailureMessage);
            }

            return SkillCastResult.Success();
        }
    }
}
