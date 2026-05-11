using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public enum EStackRule { RefreshDuration, Independent, ExtendDuration }

    [System.Serializable]
    public struct StatModifier
    {
        public GameplayTags.GameplayTag Attribute;
        public float Value;
        public EModifierType Type;
    }

    public enum EModifierType { Additive, Percent }

    public abstract class EffectDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private GameplayTags.GameplayTag[] m_tags;
        [SerializeField, TextArea(1, 3)] private string m_description;

        [Header("Duration")]
        [Tooltip("Rounds this effect persists. 0 = instantaneous.")]
        [SerializeField] private int m_durationRounds;
        [SerializeField] private EStackRule m_stackRule = EStackRule.Independent;

        [Header("Granted Tags")]
        [Tooltip("Tags added to the target while this effect is active.")]
        [SerializeField] private GameplayTags.GameplayTag[] m_grantedTags;

        [Header("Granted Abilities")]
        [Tooltip("Skills added to the target's skill pool while this effect is active.")]
        [SerializeField] private SkillDefinition[] m_grantedAbilities;

        [Header("Stat Modifiers")]
        [Tooltip("Attribute modifiers applied while this effect is active.")]
        [SerializeField] private StatModifier[] m_statModifiers;

        [Header("Tick")]
        [Tooltip("If true, Apply() is called again each round.")]
        [SerializeField] private bool m_tickPerRound;

        // ── Public properties ──

        public GameplayTags.GameplayTag[] Tags => m_tags ?? System.Array.Empty<GameplayTags.GameplayTag>();
        public string Description => m_description ?? string.Empty;
        public int DurationRounds => m_durationRounds;
        public EStackRule StackRule => m_stackRule;
        public GameplayTags.GameplayTag[] GrantedTags => m_grantedTags ?? System.Array.Empty<GameplayTags.GameplayTag>();
        public SkillDefinition[] GrantedAbilities => m_grantedAbilities ?? System.Array.Empty<SkillDefinition>();
        public StatModifier[] StatModifiers => m_statModifiers ?? System.Array.Empty<StatModifier>();
        public bool TickPerRound => m_tickPerRound;

        // ── Abstract ──

        public abstract ETargetCapability RequiredCapability { get; }

        public abstract void Apply(EffectContext context);

        /// <summary>Called when the effect expires or is removed. Override to add cleanup behavior.</summary>
        public virtual void OnRemove(EffectContext context) { }

        // ── Helpers ──

        public bool IsPersistent => m_durationRounds > 0 || GrantedAbilities.Length > 0 || GrantedTags.Length > 0;

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

