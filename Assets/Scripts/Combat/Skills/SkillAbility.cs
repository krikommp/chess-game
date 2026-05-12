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
        [SerializeField] private EffectDefinition[] m_costs;

        [Header("Cooldowns")]
        [Tooltip("Cooldown management (e.g. SetCooldown). Compute failure blocks the entire skill.")]
        [SerializeField] private EffectDefinition[] m_cooldowns;

        [Header("Effects")]
        [Tooltip("Actual game effects (damage, heal, add status, etc.). Individual Compute failure does NOT block the skill.")]
        [SerializeField] private EffectDefinition[] m_effects;

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

        public EffectDefinition[] Costs => m_costs ?? System.Array.Empty<EffectDefinition>();
        public EffectDefinition[] Cooldowns => m_cooldowns ?? System.Array.Empty<EffectDefinition>();
        public EffectDefinition[] Effects => m_effects ?? System.Array.Empty<EffectDefinition>();

        // ── Public properties — Tag conditions ────────────────────────

        public GameplayTag[] RequiredCasterTags => m_requiredCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedCasterTags => m_blockedCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTargetTags => m_requiredTargetTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTargetTags => m_blockedTargetTags ?? System.Array.Empty<GameplayTag>();

        // ── Abstract ──────────────────────────────────────────────────

        public abstract SkillCastResult Execute(SkillExecutionContext context);

        // ── Helpers (子类显式调用) ────────────────────────────────────

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
                    return results;
            }
            return results;
        }

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
