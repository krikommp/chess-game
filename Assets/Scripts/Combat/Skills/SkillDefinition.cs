using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "NewSkill", menuName = "MiniChess/Skill Definition", order = 10)]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea(1, 4)] private string _description;

        [Header("Cost & Limits")]
        [SerializeField, Min(0)] private int _apCost = 1;
        [SerializeField, Min(0)] private int _cooldown;
        [SerializeField, Min(0f)] private float _range = 1.5f;

        [Header("Targeting")]
        [SerializeField] private SkillTargetType _targetType = SkillTargetType.SingleEnemy;

        [Header("Effects")]
        [SerializeField] private EffectDefinition[] _effects;

        [Header("Tags")]
        [Tooltip("Gameplay semantics: e.g. Damage.Physical, Element.Fire")]
        [SerializeField] private GameplayTagRef[] _skillTags;
        [Tooltip("AI categorization: e.g. AI.Skill.Damage, AI.Skill.Heal")]
        [SerializeField] private GameplayTagRef[] _aiTags;

        [Header("AI")]
        [Tooltip("Base weight for AI candidate scoring (higher = more likely to pick)")]
        [SerializeField] private float _aiBaseWeight = 10f;

        public string Id => _id ?? string.Empty;
        public string DisplayName => _displayName ?? string.Empty;
        public string Description => _description ?? string.Empty;
        public int ApCost => _apCost;
        public int Cooldown => _cooldown;
        public float Range => _range;
        public SkillTargetType TargetType => _targetType;
        public EffectDefinition[] Effects => _effects ?? System.Array.Empty<EffectDefinition>();
        public GameplayTagRef[] SkillTags => _skillTags ?? System.Array.Empty<GameplayTagRef>();
        public GameplayTagRef[] AiTags => _aiTags ?? System.Array.Empty<GameplayTagRef>();
        public float AiBaseWeight => _aiBaseWeight;

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
