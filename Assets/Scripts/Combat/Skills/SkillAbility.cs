using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public abstract class SkillAbility : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_id;
        [SerializeField] private string m_displayName;
        [SerializeField, TextArea(1, 4)] private string m_description;

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

        // ── Public properties — Identity ──────────────────────────────

        public string Id => m_id ?? string.Empty;
        public string DisplayName => m_displayName ?? string.Empty;
        public string Description => m_description ?? string.Empty;

        // ── Public properties — Execution slots ───────────────────────

        public SkillEffect[] Costs => m_costs ?? System.Array.Empty<SkillEffect>();
        public SkillEffect[] Cooldowns => m_cooldowns ?? System.Array.Empty<SkillEffect>();
        public SkillEffect[] Effects => m_effects ?? System.Array.Empty<SkillEffect>();

        // ── Public properties — Tag conditions ────────────────────────

        public GameplayTag[] RequiredCasterTags => m_requiredCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedCasterTags => m_blockedCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTargetTags => m_requiredTargetTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTargetTags => m_blockedTargetTags ?? System.Array.Empty<GameplayTag>();

        // ── Abstract ──────────────────────────────────────────────────

        public abstract SkillCastResult Execute(SkillExecutionContext context);

        // ── Helpers (子类显式调用) ────────────────────────────────────

        protected SkillEffectResult[] ComputeCosts(SkillExecutionContext context)
        {
            if (m_costs == null || m_costs.Length == 0)
                return System.Array.Empty<SkillEffectResult>();

            var results = new SkillEffectResult[m_costs.Length];
            for (int i = 0; i < m_costs.Length; i++)
            {
                if (m_costs[i] == null) continue;
                var ctx = BuildEffectContext(context, context.Target);
                results[i] = m_costs[i].Compute(ctx);
                if (!results[i].IsSuccess)
                    return results;
            }
            return results;
        }

        protected void ApplyCosts(SkillExecutionContext context, SkillEffectResult[] results)
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

        protected SkillEffectResult[] ComputeCooldowns(SkillExecutionContext context)
        {
            if (m_cooldowns == null || m_cooldowns.Length == 0)
                return System.Array.Empty<SkillEffectResult>();

            var results = new SkillEffectResult[m_cooldowns.Length];
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

        protected void ApplyCooldowns(SkillExecutionContext context, SkillEffectResult[] results)
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

        protected void ApplyEffects(SkillExecutionContext context, SkillEffect[] effects)
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

        private static SkillEffectContext BuildEffectContext(SkillExecutionContext context, GameObject target)
        {
            return new SkillEffectContext
            {
                Caster = context.Caster,
                Target = target ?? context.Target,
                CasterExecutor = context.CasterExecutor,
                TargetExecutor = target != null ? target.GetComponent<AbilitySystemComponent>() : null,
                TargetPosition = context.TargetPosition,
            };
        }
    }
}
