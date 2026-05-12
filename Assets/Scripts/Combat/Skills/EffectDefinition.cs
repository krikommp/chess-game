using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public enum EStackRule { RefreshDuration, Independent, ExtendDuration }
    public enum EModifierType { Additive, Percent }

    [System.Serializable]
    public struct StatModifier
    {
        public GameplayTag Attribute;
        public float Value;
        public EModifierType Type;
    }

    /// <summary>
    /// 纯数据 ScriptableObject — 不允许用户派生 Effect 子类。
    /// 行为由配置的 EEffectFunction + 参数决定，通过 Compute / Apply 调用静态 EffectFunction。
    /// </summary>
    [CreateAssetMenu(fileName = "NewEffect", menuName = "MiniChess/Effect Definition", order = 5)]
    public sealed class EffectDefinition : ScriptableObject
    {
        [Header("Function")]
        [Tooltip("Which static EffectFunction class handles Compute / Apply.")]
        [SerializeField] private EEffectFunction m_function;

        [Header("Parameters")]
        [Tooltip("Generic numeric value (damage, heal amount, AP cost, etc.).")]
        [SerializeField] private float m_amount;
        [Tooltip("Target attribute tag (e.g. Attribute.HP, Attribute.AP).")]
        [SerializeField] private GameplayTag m_attributeTag;
        [Tooltip("Restore mode for RestoreAttribute function.")]
        [SerializeField] private ERestoreMode m_restoreMode = ERestoreMode.ToMax;
        [Tooltip("Skill id for SetCooldown function.")]
        [SerializeField] private string m_cooldownSkillId;
        [Tooltip("Cooldown duration in rounds.")]
        [SerializeField] private int m_cooldownRounds = 1;
        [Tooltip("Delay before DestroyGameObject (seconds).")]
        [SerializeField] private float m_destroyDelaySeconds = 0.5f;
        [Tooltip("Tick phase for TriggerStatusTick.")]
        [SerializeField] private EStatusTickPhase m_tickPhase = EStatusTickPhase.TurnStart;

        [Header("Duration")]
        [Tooltip("Instant = fire and forget. Persistent = tracked on target.")]
        [SerializeField] private EEffectDuration m_duration = EEffectDuration.Instant;
        [Tooltip("Rounds this effect persists. 0 for persistent-without-expiry (e.g. granted abilities).")]
        [SerializeField] private int m_durationRounds;
        [SerializeField] private EStackRule m_stackRule = EStackRule.Independent;

        [Header("Target Capability")]
        [SerializeField] private ETargetCapability m_requiredCapability = ETargetCapability.None;

        [Header("Identity Tags")]
        [Tooltip("Semantic identity (e.g. Effect.Damage.Physical).")]
        [SerializeField] private GameplayTag[] m_tags;

        [Header("Tag Interactions")]
        [Tooltip("Tags added to the target on Apply. Persistent effects auto-remove on expiry.")]
        [SerializeField] private GameplayTag[] m_grantedTags;
        [Tooltip("Tags removed from the target on Apply.")]
        [SerializeField] private GameplayTag[] m_removeTags;
        [Tooltip("Target must have these tags for Compute to succeed.")]
        [SerializeField] private GameplayTag[] m_requiredTags;
        [Tooltip("Target must NOT have these tags for Compute to succeed.")]
        [SerializeField] private GameplayTag[] m_blockedTags;

        [Header("Granted Abilities")]
        [Tooltip("Skills added to the target while this effect is active.")]
        [SerializeField] private SkillAbility[] m_grantedAbilities;

        [Header("Stat Modifiers")]
        [Tooltip("Attribute modifiers applied while this effect is active.")]
        [SerializeField] private StatModifier[] m_statModifiers;

        [Header("Tick")]
        [Tooltip("If true, Apply is called again each round.")]
        [SerializeField] private bool m_tickPerRound;

        [Header("Description")]
        [SerializeField, TextArea(1, 3)] private string m_description;

        // ── Public properties ──────────────────────────────────────

        public EEffectFunction Function => m_function;
        public float Amount => m_amount;
        public GameplayTag AttributeTag => m_attributeTag;
        public ERestoreMode RestoreMode => m_restoreMode;
        public string CooldownSkillId => m_cooldownSkillId;
        public int CooldownRounds => m_cooldownRounds;
        public float DestroyDelaySeconds => m_destroyDelaySeconds;
        public EStatusTickPhase TickPhase => m_tickPhase;

        public EEffectDuration Duration => m_duration;
        public int DurationRounds => m_durationRounds;
        public EStackRule StackRule => m_stackRule;
        public ETargetCapability RequiredCapability => m_requiredCapability;
        public string Description => m_description ?? string.Empty;

        public GameplayTag[] Tags => m_tags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] GrantedTags => m_grantedTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RemoveTags => m_removeTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTags => m_requiredTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTags => m_blockedTags ?? System.Array.Empty<GameplayTag>();
        public SkillAbility[] GrantedAbilities => m_grantedAbilities ?? System.Array.Empty<SkillAbility>();
        public StatModifier[] StatModifiers => m_statModifiers ?? System.Array.Empty<StatModifier>();
        public bool TickPerRound => m_tickPerRound;

        // ── Compute / Apply ────────────────────────────────────────

        public EffectResult Compute(EffectContext context)
        {
            return EffectFunctionDispatcher.Compute(this, context);
        }

        public void Apply(EffectContext context, EffectResult computed)
        {
            EffectFunctionDispatcher.Apply(this, context, computed);
        }

        // ── Helpers ────────────────────────────────────────────────

        public bool IsPersistent =>
            m_duration == EEffectDuration.Persistent
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
    }
}
