using MiniChess.Combat;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// RestoreAttributeFunction — 恢复属性（AP 回满、按值恢复等）。
    /// ModifyAttributeFunction — 修改属性（伤害、治疗等）。Stub: 暂不实现完整行为。
    /// </summary>

    public static class RestoreAttributeFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null)
                return EffectResult.Fail(ESkillCastFailure.TargetInvalid, "Target has no AttributeSet.");

            var tag = effect.AttributeTag;
            if (string.IsNullOrEmpty(tag.Value))
                return EffectResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "RestoreAttribute: no attribute tag configured.");

            return EffectResult.Success();
        }

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null) return;

            var tag = effect.AttributeTag;
            if (string.IsNullOrEmpty(tag.Value)) return;

            switch (effect.RestoreMode)
            {
                case ERestoreMode.ToMax:
                    targetAttr.SetToMax(tag);
                    break;
                case ERestoreMode.ByValue:
                    targetAttr.Modify(tag, effect.Amount);
                    break;
                case ERestoreMode.PercentOfMax:
                    float max = targetAttr.GetMax(tag);
                    if (max > 0f)
                        targetAttr.Modify(tag, max * effect.Amount);
                    break;
            }
        }
    }

    /// <summary>
    /// Stub: ModifyAttribute — 修改属性值（伤害/治疗）。
    /// TODO: 当 basic_attack / minor_heal 实现时完成。
    /// </summary>
    public static class ModifyAttributeFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null)
                return EffectResult.Fail(ESkillCastFailure.TargetInvalid, "Target has no AttributeSet.");

            if (string.IsNullOrEmpty(effect.AttributeTag.Value))
                return EffectResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "ModifyAttribute: no attribute tag configured.");

            // Check target capability
            if (effect.RequiredCapability != ETargetCapability.None)
            {
                var targetExec = context.TargetExecutor;
                if (targetExec != null && (targetExec.Capabilities & effect.RequiredCapability) == 0)
                    return EffectResult.Fail(ESkillCastFailure.TargetCapabilityBlocked,
                        $"Target lacks capability '{effect.RequiredCapability}'.");
            }

            return EffectResult.Success(effect.Amount);
        }

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null || !targetAttr.IsAlive) return;

            var tag = effect.AttributeTag;
            if (string.IsNullOrEmpty(tag.Value)) return;

            float delta = computed.ComputedValue;
            targetAttr.Modify(tag, delta);
        }
    }
}
