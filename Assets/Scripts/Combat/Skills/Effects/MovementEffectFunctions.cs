using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// Stub: 强制位移 EffectFunction（ForcedMove, PullTarget, TeleportTarget）。
    /// TODO: 后续实现。
    /// </summary>
    public static class ForcedMoveFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();
        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
            => Debug.Log($"[ForcedMove] Stub for {context.Target?.name}");
    }

    public static class PullTargetFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();
        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
            => Debug.Log($"[PullTarget] Stub for {context.Target?.name}");
    }

    public static class TeleportTargetFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();
        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
            => Debug.Log($"[TeleportTarget] Stub for {context.Target?.name}");
    }
}
