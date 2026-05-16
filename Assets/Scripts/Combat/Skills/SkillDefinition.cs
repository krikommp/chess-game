using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "NewSkillDefinition", menuName = "MiniChess/Skill Definition", order = 4)]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string m_id;
        [SerializeField] private string m_displayName;
        [SerializeField, TextArea(1, 4)] private string m_description;

        [Header("Targeting")]
        [SerializeField] private ESkillTargetType m_targetType;
        [SerializeField, Min(0f)] private float m_range;
        [SerializeField] private ESkillAreaShape m_areaShape = ESkillAreaShape.Single;
        [SerializeField, Min(0f)] private float m_areaRadius;
        [SerializeField, Range(0f, 360f)] private float m_areaAngle;
        [SerializeField, Min(0f)] private float m_areaWidth;
        [SerializeField, Min(0f)] private float m_areaLength;
        [SerializeField] private bool m_requiresLineOfSight;

        [Header("Turn Flow")]
        [SerializeField] private bool m_endsTurnAfterCast;

        [Header("Tags")]
        [SerializeField] private GameplayTag[] m_skillTags;
        [SerializeField] private GameplayTag[] m_aiTags;
        [SerializeField] private GameplayTag[] m_requiredCasterTags;
        [SerializeField] private GameplayTag[] m_blockedCasterTags;
        [SerializeField] private GameplayTag[] m_requiredTargetTags;
        [SerializeField] private GameplayTag[] m_blockedTargetTags;

        [Header("AI")]
        [SerializeField] private float m_aiBaseWeight = 1f;

        [Header("Presentation")]
        [SerializeField] private AnimationClip m_animation;
        [SerializeField] private GameObject m_vfx;

        [Header("Execution")]
        [Tooltip("Explicit flow asset. SkillDefinition never creates a hidden default ability.")]
        [SerializeField] private SkillAbility m_ability;

        public string Id => m_id ?? string.Empty;
        public string DisplayName => m_displayName ?? string.Empty;
        public string Description => m_description ?? string.Empty;
        public ESkillTargetType TargetType => m_targetType;
        public float Range => m_range;
        public ESkillAreaShape AreaShape => m_areaShape;
        public float AreaRadius => m_areaRadius;
        public float AreaAngle => m_areaAngle;
        public float AreaWidth => m_areaWidth;
        public float AreaLength => m_areaLength;
        public bool RequiresLineOfSight => m_requiresLineOfSight;
        public bool EndsTurnAfterCast => m_endsTurnAfterCast;
        public GameplayTag[] SkillTags => m_skillTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] AITags => m_aiTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredCasterTags => m_requiredCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedCasterTags => m_blockedCasterTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] RequiredTargetTags => m_requiredTargetTags ?? System.Array.Empty<GameplayTag>();
        public GameplayTag[] BlockedTargetTags => m_blockedTargetTags ?? System.Array.Empty<GameplayTag>();
        public float AIBaseWeight => m_aiBaseWeight;
        public AnimationClip Animation => m_animation;
        public GameObject Vfx => m_vfx;
        public SkillAbility Ability => m_ability;

        public SkillMetadata Metadata => new SkillMetadata(
            Id,
            DisplayName,
            Description,
            m_targetType,
            m_range,
            m_areaShape,
            m_areaRadius,
            m_areaAngle,
            m_areaWidth,
            m_areaLength,
            m_requiresLineOfSight,
            m_endsTurnAfterCast,
            SkillTags,
            AITags,
            m_aiBaseWeight,
            m_animation,
            m_vfx);
    }
}
