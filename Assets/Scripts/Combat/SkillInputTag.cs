using MiniChess.GameplayTags;

namespace MiniChess.Combat
{
    public static class SkillInputTag
    {
        public static readonly GameplayTag k_PointerHover = new GameplayTag("Input.Pointer.Hover");
        public static readonly GameplayTag k_PrimaryPressed = new GameplayTag("Input.Pointer.PrimaryPressed");

        public static readonly GameplayTag k_TargetGround = new GameplayTag("Input.Target.Ground");
        public static readonly GameplayTag k_TargetPlayer = new GameplayTag("Input.Target.Unit.Player");
        public static readonly GameplayTag k_TargetEnemy = new GameplayTag("Input.Target.Unit.Enemy");
        public static readonly GameplayTag k_TargetUnknown = new GameplayTag("Input.Target.Unknown");
    }
}
