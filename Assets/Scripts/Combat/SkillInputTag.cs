using MiniChess.GameplayTags;
using MiniChess.GameplayTags.Generated;

namespace MiniChess.Combat
{
    public static class SkillInputTag
    {
        public static readonly GameplayTag k_PointerHover = GameplayTagConstants.Input.Pointer.Hover;
        public static readonly GameplayTag k_PrimaryPressed = GameplayTagConstants.Input.Pointer.PrimaryPressed;

        public static readonly GameplayTag k_TargetGround = GameplayTagConstants.Input.Target.Ground;
        public static readonly GameplayTag k_TargetPlayer = GameplayTagConstants.Input.Target.Unit.Player;
        public static readonly GameplayTag k_TargetEnemy = GameplayTagConstants.Input.Target.Unit.Enemy;
        public static readonly GameplayTag k_TargetUnknown = GameplayTagConstants.Input.Target.Unknown;
    }
}
