using MiniChess.Combat;
using UnityEngine;

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

            float amount = effect.Amount > 0f ? effect.Amount : 1f;
            float currentAP = casterAttr.Get(WellKnownAttributeTags.AP);

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
            if (amount <= 0f) amount = 1f;

            casterAttr.TrySpend(WellKnownAttributeTags.AP, amount);
        }
    }
}
