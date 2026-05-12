using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// Stub: SetCooldown — Compute 检查 Cooldown.{skillId} Tag；Apply 添加冷却 Status。
    /// TODO: 当 Status 系统实现时完成。
    /// </summary>
    public static class SetCooldownFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
        {
            if (string.IsNullOrEmpty(effect.CooldownSkillId))
                return EffectResult.Success(); // No cooldown configured

            var cooldownTag = new GameplayTags.GameplayTag($"Cooldown.{effect.CooldownSkillId}");
            var casterTags = context.Caster?.GetComponent<GameplayTags.GameplayTagComponent>()?.TagSet;
            if (casterTags != null && casterTags.Has(cooldownTag, ETagMatchMode.Exact))
                return EffectResult.Fail(ESkillCastFailure.OnCooldown,
                    $"'{effect.CooldownSkillId}' is on cooldown.");

            return EffectResult.Success();
        }

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            if (string.IsNullOrEmpty(effect.CooldownSkillId)) return;

            var cooldownTag = new GameplayTags.GameplayTag($"Cooldown.{effect.CooldownSkillId}");
            var executor = context.Caster?.GetComponent<SkillExecutor>();
            if (executor != null)
            {
                // Register cooldown on SkillExecutor (simple integer-based for now)
                executor.SetCooldown(effect.CooldownSkillId, effect.CooldownRounds);
            }

            var tagComp = context.Caster?.GetComponent<GameplayTags.GameplayTagComponent>();
            tagComp?.AddTag(cooldownTag, $"Cooldown.{effect.CooldownSkillId}");
        }
    }

    /// <summary>Stub: AddStatus — 给目标添加 Status。</summary>
    public static class AddStatusFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            Debug.Log($"[AddStatus] Stub: would add status to {context.Target?.name}");
        }
    }

    /// <summary>Stub: RemoveStatus — 从目标移除 Status。</summary>
    public static class RemoveStatusFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            Debug.Log($"[RemoveStatus] Stub: would remove status from {context.Target?.name}");
        }
    }
}
