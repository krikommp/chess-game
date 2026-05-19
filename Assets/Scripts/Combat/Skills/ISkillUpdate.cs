namespace MiniChess.Combat.Skills
{
    public interface ISkillUpdate
    {
        SkillCastResult SkillUpdate(SkillExecutionState state, float deltaTime);
    }
}
