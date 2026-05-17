using MiniChess.Combat;

namespace MiniChess.Combat.Skills
{
    public interface ISkillInputHandler
    {
        SkillCastResult HandleInput(SkillExecutionContext context);
    }
}
