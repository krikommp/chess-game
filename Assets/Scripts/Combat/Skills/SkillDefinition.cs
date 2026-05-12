using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "MiniChess/Skill Definition", order = 10)]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_id;
        [SerializeField] private string m_displayName;
        [SerializeField, TextArea(1, 4)] private string m_description;

        [Header("Targeting")]
        [SerializeField] private ESkillTargetType m_targetType = ESkillTargetType.SingleEnemy;
        [SerializeField, Min(0f)] private float m_range = 1.5f;
        [Tooltip("Does this skill require line of sight? (MVP: reserved, not enforced)")]
        [SerializeField] private bool m_requiresLineOfSight;

        [Header("Turn")]
        [Tooltip("If true, using this skill ends the unit's turn immediately.")]
        [SerializeField] private bool m_endsTurnAfterCast;

        [Header("Ability")]
        [Tooltip("Skill-specific behavior. Must be explicitly assigned. No default fallback.")]
        [SerializeField] private SkillAbility m_ability;

        [Header("Effects")]
        [Tooltip("Legacy effect references. Preferred path: Ability.Effects. Retained for SkillDefinition-level tag checks.")]
        [SerializeField] private EffectDefinition[] m_effects;

        [Header("Tags")]
        [Tooltip("Gameplay semantics: e.g. Damage.Physical, Element.Fire")]
        [SerializeField] private GameplayTag[] m_skillTags;
        [Tooltip("AI categorization: e.g. AI.Skill.Damage, AI.Skill.Heal")]
        [SerializeField] private GameplayTag[] m_aiTags;

        [Header("Tag Conditions")]
        [Tooltip("Tags the caster must have to use this skill.")]
        [SerializeField] private GameplayTag[] m_requiredCasterTags;
        [Tooltip("Tags that block the caster from using this skill.")]
        [SerializeField] private GameplayTag[] m_blockedCasterTags;
        [Tooltip("Tags the target must have to be affected by this skill.")]
        [SerializeField] private GameplayTag[] m_requiredTargetTags;
        [Tooltip("Tags that block the target from being affected by this skill.")]
        [SerializeField] private GameplayTag[] m_blockedTargetTags;

        [Header("AI")]
        [Tooltip("Base weight for AI candidate scoring (higher = more likely to pick)")]
        [SerializeField] private float m_aiBaseWeight = 10f;

        // ── Public properties ──────────────────────────────────────

        public string Id => m_id ?? string.Empty;
        public string DisplayName => m_displayName ?? string.Empty;
        public string Description => m_description ?? string.Empty;
        public ESkillTargetType TargetType => m_targetType;
        public float Range => m_range;
        public bool RequiresLineOfSight => m_requiresLineOfSight;
        public bool EndsTurnAfterCast => m_endsTurnAfterCast;

        /// <summary>
        /// The ability configured on this skill. Must be explicitly assigned.
        /// No fallback — returns null if not configured.
        /// </summary>
        public SkillAbility Ability => m_ability;

        public EffectDefinition[] Effects => m_effects ?? System.Array.Empty<EffectDefinition>();
        public GameplayTag[] SkillTags => m_skillTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] AiTags => m_aiTags ?? System.Array.Empty<GameplayTag>();
        public float AiBaseWeight => m_aiBaseWeight;
        public GameplayTag[] RequiredCasterTags => m_requiredCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedCasterTags => m_blockedCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTargetTags => m_requiredTargetTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTargetTags => m_blockedTargetTags ?? System.Array.Empty<GameplayTag>();

        public bool HasEffectTag(GameplayTag tag)
        {
            var effects = Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                var tags = effects[i].Tags;
                for (int j = 0; j < tags.Length; j++)
                {
                    if (tags[j] == tag)
                        return true;
                }
            }
            return false;
        }
    }
}
