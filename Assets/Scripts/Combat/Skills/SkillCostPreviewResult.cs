namespace MiniChess.Combat.Skills
{
    public struct SkillCostPreviewResult
    {
        public bool IsSuccess;
        public ESkillCastFailure Failure;
        public string FailureMessage;
        public float MaxPathLength;

        public static SkillCostPreviewResult Success(float maxPathLength)
        {
            return new SkillCostPreviewResult
            {
                IsSuccess = true,
                MaxPathLength = maxPathLength,
            };
        }

        public static SkillCostPreviewResult Fail(ESkillCastFailure reason, string message)
        {
            return new SkillCostPreviewResult
            {
                IsSuccess = false,
                Failure = reason,
                FailureMessage = message ?? string.Empty,
                MaxPathLength = 0f,
            };
        }
    }
}
