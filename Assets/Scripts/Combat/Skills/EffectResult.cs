namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// Result of an EffectFunction.Compute call.
    /// Carries the computed value (e.g. AP cost, damage amount) and success/failure info.
    /// </summary>
    public struct EffectResult
    {
        public bool IsSuccess;
        public ESkillCastFailure Failure;
        public string FailureMessage;
        public float ComputedValue;

        public static EffectResult Success(float value = 0f)
        {
            return new EffectResult
            {
                IsSuccess = true,
                Failure = ESkillCastFailure.None,
                ComputedValue = value,
            };
        }

        public static EffectResult Fail(ESkillCastFailure reason, string message)
        {
            return new EffectResult
            {
                IsSuccess = false,
                Failure = reason,
                FailureMessage = message ?? string.Empty,
            };
        }
    }
}
