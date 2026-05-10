using MiniChess.Combat;

namespace MiniChess.Combat.Skills
{
    public readonly struct SkillInputServices
    {
        public readonly PathPreview Preview;
        public readonly float NavMeshSnapRadius;
        public readonly float OriginSnapRadius;

        public SkillInputServices(PathPreview preview, float navMeshSnapRadius, float originSnapRadius)
        {
            Preview = preview;
            NavMeshSnapRadius = navMeshSnapRadius;
            OriginSnapRadius = originSnapRadius;
        }
    }
}
