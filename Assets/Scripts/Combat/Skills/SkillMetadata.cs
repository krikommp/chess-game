using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public readonly struct SkillMetadata
    {
        public SkillMetadata(
            string id,
            string displayName,
            string description,
            ESkillTargetType targetType,
            float range,
            ESkillAreaShape areaShape,
            float areaRadius,
            float areaAngle,
            float areaWidth,
            float areaLength,
            bool requiresLineOfSight,
            bool endsTurnAfterCast,
            GameplayTag[] skillTags,
            GameplayTag[] aiTags,
            float aiBaseWeight,
            AnimationClip animation,
            GameObject vfx)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            TargetType = targetType;
            Range = range;
            AreaShape = areaShape;
            AreaRadius = areaRadius;
            AreaAngle = areaAngle;
            AreaWidth = areaWidth;
            AreaLength = areaLength;
            RequiresLineOfSight = requiresLineOfSight;
            EndsTurnAfterCast = endsTurnAfterCast;
            SkillTags = skillTags ?? System.Array.Empty<GameplayTag>();
            AITags = aiTags ?? System.Array.Empty<GameplayTag>();
            AIBaseWeight = aiBaseWeight;
            Animation = animation;
            Vfx = vfx;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public ESkillTargetType TargetType { get; }
        public float Range { get; }
        public ESkillAreaShape AreaShape { get; }
        public float AreaRadius { get; }
        public float AreaAngle { get; }
        public float AreaWidth { get; }
        public float AreaLength { get; }
        public bool RequiresLineOfSight { get; }
        public bool EndsTurnAfterCast { get; }
        public GameplayTag[] SkillTags { get; }
        public GameplayTag[] AITags { get; }
        public float AIBaseWeight { get; }
        public AnimationClip Animation { get; }
        public GameObject Vfx { get; }
    }
}
