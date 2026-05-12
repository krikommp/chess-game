namespace MiniChess.Combat.Skills
{
    public struct SkillEffectResult
    {
        public bool IsSuccess;
        public ESkillCastFailure Failure;
        public string FailureMessage;
        public float ComputedValue;

        public static SkillEffectResult Success(float value = 0f)
        {
            return new SkillEffectResult
            {
                IsSuccess = true,
                ComputedValue = value,
            };
        }

        public static SkillEffectResult Fail(ESkillCastFailure reason, string message)
        {
            return new SkillEffectResult
            {
                IsSuccess = false,
                Failure = reason,
                FailureMessage = message ?? string.Empty,
            };
        }
    }
}
