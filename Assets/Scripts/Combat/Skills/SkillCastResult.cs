namespace MiniChess.Combat.Skills
{
    public struct SkillCastResult
    {
        public bool IsSuccess;
        public ESkillCastFailure Failure;
        public string FailureMessage;

        public static SkillCastResult Success()
        {
            return new SkillCastResult { IsSuccess = true, Failure = ESkillCastFailure.None };
        }

        public static SkillCastResult Fail(ESkillCastFailure reason, string message)
        {
            return new SkillCastResult { IsSuccess = false, Failure = reason, FailureMessage = message ?? string.Empty };
        }
    }
}
