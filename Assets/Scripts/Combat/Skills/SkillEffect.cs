using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public enum EModifierType { Additive, Percent }

    [System.Serializable]
    public struct StatModifier
    {
        public GameplayTag Attribute;
        public float Value;
        public EModifierType Type;
    }

    [CreateAssetMenu(fileName = "NewSkillEffect", menuName = "MiniChess/Skill Effect", order = 5)]
    public sealed class SkillEffect : ScriptableObject
    {
        [Header("Function")]
        [SerializeField] private SkillEffectFunction m_function;

        [Header("Targeting")]
        [SerializeField] private ESkillEffectTarget m_targetMapping = ESkillEffectTarget.Default;

        [Header("Duration")]
        [SerializeField] private ESkillEffectDuration m_duration = ESkillEffectDuration.Instant;
        [SerializeField] private int m_durationRounds;
        [SerializeField] private bool m_tickPerRound;

        [Header("Identity Tags")]
        [SerializeField] private GameplayTag[] m_tags;

        [Header("Tag Interactions")]
        [SerializeField] private GameplayTag[] m_requiredTags;
        [SerializeField] private GameplayTag[] m_blockedTags;
        [SerializeField] private GameplayTag[] m_grantedTags;
        [SerializeField] private GameplayTag[] m_removeTags;

        [Header("Granted Abilities")]
        [SerializeField] private SkillDefinition[] m_grantedAbilities;

        [Header("Stat Modifiers")]
        [SerializeField] private StatModifier[] m_statModifiers;

        [Header("Description")]
        [SerializeField, TextArea(1, 3)] private string m_description;

        // ── Public properties ──────────────────────────────────────

        public SkillEffectFunction Function => m_function;
        public ESkillEffectTarget TargetMapping => m_targetMapping;
        public ESkillEffectDuration Duration => m_duration;
        public int DurationRounds => m_durationRounds;
        public bool TickPerRound => m_tickPerRound;
        public string Description => m_description ?? string.Empty;

        public GameplayTag[] Tags => m_tags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTags => m_requiredTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTags => m_blockedTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] GrantedTags => m_grantedTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RemoveTags => m_removeTags ?? System.Array.Empty<GameplayTag>();
        public SkillDefinition[] GrantedAbilities => m_grantedAbilities ?? System.Array.Empty<SkillDefinition>();
        public StatModifier[] StatModifiers => m_statModifiers ?? System.Array.Empty<StatModifier>();

        // ── Compute / Apply ────────────────────────────────────────

        public SkillEffectResult Compute(SkillEffectContext context)
        {
            if (m_function == null)
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect has no Function assigned.");

            var tagResult = EvaluateEffectTags(context);
            if (!tagResult.IsSuccess)
                return tagResult;

            return m_function.Compute(context, this);
        }

        public SkillEffectResult Apply(SkillEffectContext context, SkillEffectResult computed)
        {
            if (m_function == null)
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect has no Function assigned.");

            if (!computed.IsSuccess)
                return computed;

            return m_function.Apply(context, this, computed);
        }

        public SkillEffectResult Update(SkillEffectContext context, SkillEffectResult computed, float deltaTime)
        {
            if (m_function == null)
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect has no Function assigned.");

            if (!computed.IsSuccess)
                return computed;

            return m_function.Update(context, this, computed, deltaTime);
        }

        public SkillCostPreviewResult PreviewMaxPathLength(
            SkillEffectContext context,
            float currentMaxPathLength)
        {
            if (m_function == null)
            {
                return SkillCostPreviewResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Effect has no Function assigned.");
            }

            var tagResult = EvaluateEffectTags(context);
            if (!tagResult.IsSuccess)
                return SkillCostPreviewResult.Fail(tagResult.Failure, tagResult.FailureMessage);

            return m_function.PreviewMaxPathLength(context, this, currentMaxPathLength);
        }

        // ── Helpers ────────────────────────────────────────────────

        public bool IsPersistent =>
            m_duration == ESkillEffectDuration.Persistent
            || m_durationRounds > 0
            || GrantedAbilities.Length > 0
            || GrantedTags.Length > 0;

        public bool HasAnyTag()
        {
            var all = Tags;
            for (int i = 0; i < all.Length; i++)
            {
                if (!string.IsNullOrEmpty(all[i].Value))
                    return true;
            }
            return false;
        }

        private SkillEffectResult EvaluateEffectTags(SkillEffectContext context)
        {
            var target = context.Target;
            if (target == null) return SkillEffectResult.Success();

            var tagComp = target.GetComponent<GameplayTagComponent>();
            var tagSet = tagComp?.TagSet;
            if (tagSet == null) return SkillEffectResult.Success();

            var required = RequiredTags;
            for (int i = 0; i < required.Length; i++)
            {
                if (string.IsNullOrEmpty(required[i].Value)) continue;
                if (!tagSet.Has(required[i], ETagMatchMode.Exact))
                    return SkillEffectResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target lacks required tag '{required[i].Value}' for effect.");
            }

            var blocked = BlockedTags;
            for (int i = 0; i < blocked.Length; i++)
            {
                if (string.IsNullOrEmpty(blocked[i].Value)) continue;
                if (tagSet.Has(blocked[i], ETagMatchMode.Exact))
                    return SkillEffectResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target is blocked by tag '{blocked[i].Value}' for effect.");
            }

            return SkillEffectResult.Success();
        }
    }
}
