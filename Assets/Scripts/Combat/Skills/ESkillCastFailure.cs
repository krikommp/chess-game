namespace MiniChess.Combat.Skills
{
    public enum ESkillCastFailure
    {
        None = 0,
        CasterDead,
        TargetDead,
        TargetInvalid,
        InsufficientAp,
        OnCooldown,
        OutOfRange,
        TargetCapabilityBlocked,
        TagConditionFailed,
        EffectApplicationFailed,
    }
}
