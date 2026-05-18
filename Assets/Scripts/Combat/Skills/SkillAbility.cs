using MiniChess.GameplayTags;
using UnityEngine;
using MiniChess.Combat;

namespace MiniChess.Combat.Skills
{
    public abstract class SkillAbility : ScriptableObject
    {
        [Header("Costs")]
        [Tooltip("Resource costs (e.g. SpendAP). Compute failure blocks the entire skill.")]
        [SerializeField] private SkillEffect[] m_costs;

        [Header("Cooldowns")]
        [Tooltip("Cooldown effects (persistent effects with Cooldown tags). Compute failure blocks the entire skill.")]
        [SerializeField] private SkillEffect[] m_cooldowns;

        [Header("Effects")]
        [Tooltip("Actual game effects (damage, heal, add status, etc.). Individual Compute failure does NOT block the skill.")]
        [SerializeField] private SkillEffect[] m_effects;

        [Header("Tag Conditions")]
        [Tooltip("Tags the caster must have to use this skill.")]
        [SerializeField] private GameplayTag[] m_requiredCasterTags;
        [Tooltip("Tags that block the caster from using this skill.")]
        [SerializeField] private GameplayTag[] m_blockedCasterTags;
        [Tooltip("Tags the target must have to be affected by this skill.")]
        [SerializeField] private GameplayTag[] m_requiredTargetTags;
        [Tooltip("Tags that block the target from being affected by this skill.")]
        [SerializeField] private GameplayTag[] m_blockedTargetTags;

        // Public properties: Execution slots

        public SkillEffect[] Costs => m_costs ?? System.Array.Empty<SkillEffect>();
        public SkillEffect[] Cooldowns => m_cooldowns ?? System.Array.Empty<SkillEffect>();
        public SkillEffect[] Effects => m_effects ?? System.Array.Empty<SkillEffect>();

        // Public properties: Tag conditions

        public GameplayTag[] RequiredCasterTags => m_requiredCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedCasterTags => m_blockedCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTargetTags => m_requiredTargetTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTargetTags => m_blockedTargetTags ?? System.Array.Empty<GameplayTag>();

        // Abstract

        public abstract SkillCastResult Execute(SkillExecutionContext context);

        // Helpers (子类显式调用)

        protected SkillEffectResult[] ComputeCosts(SkillExecutionContext context)
        {
            if (m_costs == null || m_costs.Length == 0)
                return System.Array.Empty<SkillEffectResult>();

            var results = new SkillEffectResult[m_costs.Length];
            for (int i = 0; i < m_costs.Length; i++)
            {
                if (m_costs[i] == null) continue;
                var ctx = BuildEffectContext(context, m_costs[i], ESkillEffectTarget.Caster);
                results[i] = m_costs[i].Compute(ctx);
                if (!results[i].IsSuccess)
                    return results;
            }
            return results;
        }

        protected SkillEffectResult ApplyCosts(SkillExecutionContext context, SkillEffectResult[] results)
        {
            if (m_costs == null || results == null)
                return SkillEffectResult.Success();

            int count = System.Math.Min(m_costs.Length, results.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_costs[i] == null) continue;
                if (!results[i].IsSuccess) continue;
                var ctx = BuildEffectContext(context, m_costs[i], ESkillEffectTarget.Caster);
                var applyResult = m_costs[i].Apply(ctx, results[i]);
                if (!applyResult.IsSuccess)
                    return applyResult;
            }

            return SkillEffectResult.Success();
        }

        protected SkillEffectResult[] ComputeCooldowns(SkillExecutionContext context)
        {
            if (m_cooldowns == null || m_cooldowns.Length == 0)
                return System.Array.Empty<SkillEffectResult>();

            var results = new SkillEffectResult[m_cooldowns.Length];
            for (int i = 0; i < m_cooldowns.Length; i++)
            {
                var cd = m_cooldowns[i];
                if (cd == null) continue;

                var effectContext = BuildEffectContext(context, cd, ESkillEffectTarget.Caster);

                // Check if the mapped cooldown owner already has any GrantedTag from this cooldown effect.
                var targetTagComp = effectContext.Target?.GetComponent<MiniChess.GameplayTags.GameplayTagComponent>();
                if (targetTagComp != null)
                {
                    var grantedTags = cd.GrantedTags;
                    for (int j = 0; j < grantedTags.Length; j++)
                    {
                        if (string.IsNullOrEmpty(grantedTags[j].Value)) continue;
                        if (targetTagComp.HasTag(grantedTags[j], MiniChess.GameplayTags.ETagMatchMode.Exact))
                        {
                            results[i] = SkillEffectResult.Fail(ESkillCastFailure.OnCooldown,
                                $"Skill is on cooldown (tag: {grantedTags[j].Value})");
                            return results;
                        }
                    }
                }

                results[i] = cd.Compute(effectContext);
                if (!results[i].IsSuccess)
                    return results;
            }
            return results;
        }

        protected SkillEffectResult ApplyCooldowns(SkillExecutionContext context, SkillEffectResult[] results)
        {
            if (m_cooldowns == null || results == null)
                return SkillEffectResult.Success();

            int count = System.Math.Min(m_cooldowns.Length, results.Length);
            for (int i = 0; i < count; i++)
            {
                if (m_cooldowns[i] == null) continue;
                if (!results[i].IsSuccess) continue;
                var effectContext = BuildEffectContext(context, m_cooldowns[i], ESkillEffectTarget.Caster);
                var targetExecutor = effectContext.TargetExecutor;
                if (targetExecutor == null)
                    return SkillEffectResult.Fail(ESkillCastFailure.CasterDead, "Cooldown target has no AbilitySystemComponent.");

                var applyResult = targetExecutor.ApplyEffect(m_cooldowns[i], effectContext, results[i]);
                if (!applyResult.IsSuccess)
                    return applyResult;
            }

            return SkillEffectResult.Success();
        }

        protected void ApplyEffects(SkillExecutionContext context, SkillEffect[] effects)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] == null) continue;
                var ctx = BuildEffectContext(context, effects[i], ESkillEffectTarget.Target);
                var result = effects[i].Compute(ctx);
                if (result.IsSuccess)
                {
                    if (ctx.TargetExecutor != null)
                    {
                        ctx.TargetExecutor.ApplyEffect(effects[i], ctx, result);
                    }
                    else if (!effects[i].IsPersistent)
                    {
                        effects[i].Apply(ctx, result);
                    }
                }
            }
        }

        protected static SkillEffectContext BuildEffectContext(
            SkillExecutionContext context,
            SkillEffect effect,
            ESkillEffectTarget defaultTarget)
        {
            var resolvedTarget = ResolveEffectTarget(context, effect, defaultTarget);

            return new SkillEffectContext
            {
                Caster = context.Caster,
                Target = resolvedTarget,
                Source = context.Caster,
                CasterExecutor = context.CasterExecutor,
                TargetExecutor = resolvedTarget != null ? resolvedTarget.GetComponent<AbilitySystemComponent>() : null,
                TargetPosition = context.TargetPosition,
                PathLength = context.Path != null ? NavMeshService.PathLength(context.Path.corners) : (float?)null,
            };
        }

        private static GameObject ResolveEffectTarget(
            SkillExecutionContext context,
            SkillEffect effect,
            ESkillEffectTarget defaultTarget)
        {
            var mapping = effect != null && effect.TargetMapping != ESkillEffectTarget.Default
                ? effect.TargetMapping
                : defaultTarget;

            switch (mapping)
            {
                case ESkillEffectTarget.Caster:
                case ESkillEffectTarget.Self:
                    return context.Caster;
                case ESkillEffectTarget.Target:
                    return context.Target;
                case ESkillEffectTarget.Source:
                    return context.Caster;
                case ESkillEffectTarget.GroundPoint:
                    return null;
                default:
                    return context.Target;
            }
        }
    }
}
