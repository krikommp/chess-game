using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// 标准"对单体目标释放技能"的 Ability 流程。
    /// 编排顺序: ComputeCosts → ComputeCooldowns → Compute/Apply Effects → ApplyCosts → ApplyCooldowns
    /// </summary>
    [CreateAssetMenu(fileName = "SimpleTargetAbility", menuName = "MiniChess/Skill Abilities/Simple Target", order = 20)]
    public class SimpleTargetAbility : SkillAbility
    {
        public override SkillCastResult Execute(SkillExecutionContext context)
        {
            // 1. Compute costs — any failure blocks the entire skill
            var costResults = ComputeCosts(context);
            for (int i = 0; i < costResults.Length; i++)
            {
                if (!costResults[i].IsSuccess)
                    return SkillCastResult.Fail(costResults[i].Failure, costResults[i].FailureMessage);
            }

            // 2. Compute cooldowns — any failure blocks the entire skill
            var cooldownResults = ComputeCooldowns(context);
            for (int i = 0; i < cooldownResults.Length; i++)
            {
                if (!cooldownResults[i].IsSuccess)
                    return SkillCastResult.Fail(cooldownResults[i].Failure, cooldownResults[i].FailureMessage);
            }

            // 3. Compute + Apply effects — individual failures do NOT block
            ApplyEffects(context, Effects);

            // 4. Apply costs
            ApplyCosts(context, costResults);

            // 5. Apply cooldowns
            ApplyCooldowns(context, cooldownResults);

            return SkillCastResult.Success();
        }
    }
}
