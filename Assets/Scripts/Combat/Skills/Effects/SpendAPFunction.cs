using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// AP 消耗 EffectFunction。Compute 检查 AP 是否足够；Apply 实际扣 AP。
    /// </summary>
    public static class SpendAPFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
        {
            var casterAttr = context.Caster?.GetComponent<AttributeSet>();
            if (casterAttr == null)
                return EffectResult.Fail(ESkillCastFailure.CasterDead, "Caster has no AttributeSet.");

            float currentAP = casterAttr.Get(WellKnownAttributeTags.AP);
            float amount;

            if (context.TargetPosition.HasValue)
            {
                // GroundPoint: compute AP cost from NavMesh path
                var casterPos = context.Caster.transform.position;
                var nav = NavMeshService.Instance;
                if (nav != null && nav.CalculatePath(casterPos, context.TargetPosition.Value, out var path))
                {
                    float pathLength = NavMeshService.PathLength(path.corners);
                    float speed = casterAttr.Get(WellKnownAttributeTags.MoveSpeed);
                    amount = NavMeshService.EstimateMoveApCost(pathLength, speed, Mathf.FloorToInt(currentAP));
                }
                else
                {
                    amount = effect.Amount > 0f ? effect.Amount : 1f;
                }
            }
            else
            {
                amount = effect.Amount > 0f ? effect.Amount : 1f;
            }

            if (amount <= 0f)
                return EffectResult.Success(0f);

            if (currentAP < amount)
                return EffectResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Need {amount} AP, have {currentAP}.");

            return EffectResult.Success(amount);
        }

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            var casterAttr = context.Caster?.GetComponent<AttributeSet>();
            if (casterAttr == null) return;

            float amount = computed.ComputedValue > 0f ? computed.ComputedValue : effect.Amount;
            if (amount <= 0f) return;

            casterAttr.TrySpend(WellKnownAttributeTags.AP, amount);
        }
    }
}
