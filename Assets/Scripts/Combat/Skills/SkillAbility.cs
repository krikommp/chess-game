using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// 开放给用户编写的流程类。基类提供 Tag / Cost / Cooldown / Effect 的通用 helper；
    /// 具体 Ability 在 Execute(context) 中显式调用这些 helper，自行编排流程顺序。
    ///
    /// Costs / Cooldowns / Effects 是标准槽位，能力可以选择性使用。
    /// </summary>
    public abstract class SkillAbility : ScriptableObject
    {
        [Header("Ability Tags")]
        [Tooltip("Caster has any of these → ability cannot execute.")]
        [SerializeField] protected GameplayTag[] m_blockedTags;

        [Header("Costs")]
        [Tooltip("Resource costs (e.g. SpendAP). Compute failure blocks the entire skill.")]
        [SerializeField] protected EffectDefinition[] m_costs;

        [Header("Cooldowns")]
        [Tooltip("Cooldown management (e.g. SetCooldown). Compute failure blocks the entire skill.")]
        [SerializeField] protected EffectDefinition[] m_cooldowns;

        [Header("Effects")]
        [Tooltip("Actual game effects (damage, heal, add status, etc.). Individual Compute failure does NOT block the skill.")]
        [SerializeField] protected EffectDefinition[] m_effects;

        // ── Public properties ──────────────────────────────────────

        public GameplayTag[] BlockedTags => m_blockedTags ?? System.Array.Empty<GameplayTag>();
        public EffectDefinition[] Costs => m_costs ?? System.Array.Empty<EffectDefinition>();
        public EffectDefinition[] Cooldowns => m_cooldowns ?? System.Array.Empty<EffectDefinition>();
        public EffectDefinition[] Effects => m_effects ?? System.Array.Empty<EffectDefinition>();

        // ── Abstract ───────────────────────────────────────────────

        /// <summary>
        /// Execute the ability's full flow. Concrete abilities call helpers in the desired order.
        /// </summary>
        public abstract SkillCastResult Execute(SkillExecutionContext context);

        // ── Helpers (显式调用，不自动执行) ──────────────────────────

        /// <summary>Check if the caster has any blocked tag. Returns fail if blocked.</summary>
        protected SkillCastResult CheckAbilityTags(SkillExecutionContext context)
        {
            if (m_blockedTags == null || m_blockedTags.Length == 0)
                return SkillCastResult.Success();

            var casterTags = context.CasterExecutor?.GetComponent<GameplayTags.GameplayTagComponent>()?.TagSet;
            if (casterTags == null)
                return SkillCastResult.Success();

            for (int i = 0; i < m_blockedTags.Length; i++)
            {
                if (string.IsNullOrEmpty(m_blockedTags[i].Value)) continue;
                if (casterTags.Has(m_blockedTags[i], ETagMatchMode.Exact))
                    return SkillCastResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Ability blocked by caster tag '{m_blockedTags[i].Value}'.");
            }

            return SkillCastResult.Success();
        }

        /// <summary>Compute all cost effects. Returns array of results. Any failure blocks the skill.</summary>
        protected EffectResult[] ComputeCosts(SkillExecutionContext context)
        {
            if (m_costs == null || m_costs.Length == 0)
                return System.Array.Empty<EffectResult>();

            var results = new EffectResult[m_costs.Length];
            for (int i = 0; i < m_costs.Length; i++)
            {
                if (m_costs[i] == null) continue;
                var ctx = BuildEffectContext(context, context.Target);
                results[i] = m_costs[i].Compute(ctx);
                if (!results[i].IsSuccess)
                    return results; // 调用方检查 IsSuccess
            }
            return results;
        }

        /// <summary>Apply all cost effects with their computed results.</summary>
        protected void ApplyCosts(SkillExecutionContext context, EffectResult[] results)
        {
            if (m_costs == null || results == null) return;
            int count = System.Math.Min(m_costs.Length, results.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_costs[i] == null) continue;
                if (!results[i].IsSuccess) continue;
                var ctx = BuildEffectContext(context, context.Target);
                m_costs[i].Apply(ctx, results[i]);
            }
        }

        /// <summary>Compute all cooldown effects. Returns array of results. Any failure blocks the skill.</summary>
        protected EffectResult[] ComputeCooldowns(SkillExecutionContext context)
        {
            if (m_cooldowns == null || m_cooldowns.Length == 0)
                return System.Array.Empty<EffectResult>();

            var results = new EffectResult[m_cooldowns.Length];
            for (int i = 0; i < m_cooldowns.Length; i++)
            {
                if (m_cooldowns[i] == null) continue;
                var ctx = BuildEffectContext(context, context.Target);
                results[i] = m_cooldowns[i].Compute(ctx);
                if (!results[i].IsSuccess)
                    return results;
            }
            return results;
        }

        /// <summary>Apply all cooldown effects with their computed results.</summary>
        protected void ApplyCooldowns(SkillExecutionContext context, EffectResult[] results)
        {
            if (m_cooldowns == null || results == null) return;
            int count = System.Math.Min(m_cooldowns.Length, results.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_cooldowns[i] == null) continue;
                if (!results[i].IsSuccess) continue;
                var ctx = BuildEffectContext(context, context.Target);
                m_cooldowns[i].Apply(ctx, results[i]);
            }
        }

        /// <summary>Compute a single effect. Does NOT block the skill on failure (caller decides).</summary>
        protected EffectResult ComputeEffect(SkillExecutionContext context, EffectDefinition effect)
        {
            if (effect == null)
                return EffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect is null.");
            var ctx = BuildEffectContext(context, context.Target);
            return effect.Compute(ctx);
        }

        /// <summary>Apply a single effect with its computed result.</summary>
        protected void ApplyEffect(SkillExecutionContext context, EffectDefinition effect, EffectResult result)
        {
            if (effect == null || !result.IsSuccess) return;
            var ctx = BuildEffectContext(context, context.Target);
            effect.Apply(ctx, result);
        }

        /// <summary>Compute + Apply a batch of effects. Skips Compute-failed effects individually.</summary>
        protected void ApplyEffects(SkillExecutionContext context, EffectDefinition[] effects)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] == null) continue;
                var ctx = BuildEffectContext(context, context.Target);
                var result = effects[i].Compute(ctx);
                if (result.IsSuccess)
                    effects[i].Apply(ctx, result);
            }
        }

        // ── Internal ────────────────────────────────────────────────

        private static EffectContext BuildEffectContext(SkillExecutionContext context, GameObject target)
        {
            return new EffectContext
            {
                Caster = context.Caster,
                Target = target ?? context.Target,
                CasterExecutor = context.CasterExecutor,
                TargetExecutor = target != null ? target.GetComponent<SkillExecutor>() : null,
                TargetPosition = context.TargetPosition,
            };
        }
    }
}
