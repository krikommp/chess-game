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

        [Header("Cost & Limits")]
        [SerializeField, Min(0)] private int m_apCost = 1;
        [SerializeField, Min(0)] private int m_cooldown;
        [SerializeField, Min(0f)] private float m_range = 1.5f;

        [Header("Targeting")]
        [SerializeField] private ESkillTargetType m_targetType = ESkillTargetType.SingleEnemy;

        [Header("Effects")]
        [SerializeField] private EffectDefinition[] m_effects;

        [Header("Tags")]
        [Tooltip("Gameplay semantics: e.g. Damage.Physical, Element.Fire")]
        [SerializeField] private GameplayTagRef[] m_skillTags;
        [Tooltip("AI categorization: e.g. AI.Skill.Damage, AI.Skill.Heal")]
        [SerializeField] private GameplayTagRef[] m_aiTags;

        [Header("AI")]
        [Tooltip("Base weight for AI candidate scoring (higher = more likely to pick)")]
        [SerializeField] private float m_aiBaseWeight = 10f;

        public string Id => m_id ?? string.Empty;
        public string DisplayName => m_displayName ?? string.Empty;
        public string Description => m_description ?? string.Empty;
        public int ApCost => m_apCost;
        public int Cooldown => m_cooldown;
        public float Range => m_range;
        public ESkillTargetType TargetType => m_targetType;
        public EffectDefinition[] Effects => m_effects ?? System.Array.Empty<EffectDefinition>();
        public GameplayTagRef[] SkillTags => m_skillTags ?? System.Array.Empty<GameplayTagRef>();
        public GameplayTagRef[] AiTags => m_aiTags ?? System.Array.Empty<GameplayTagRef>();
        public float AiBaseWeight => m_aiBaseWeight;

        public bool HasEffectTag(GameplayTag tag)
        {
            var effects = Effects;
            for (int i = 0; i < effects.Length; i++)
            {
                var tags = effects[i].Tags;
                for (int j = 0; j < tags.Length; j++)
                {
                    if (tags[j].TryGetTag(out var t) && t == tag)
                        return true;
                }
            }
            return false;
        }
    }
}


