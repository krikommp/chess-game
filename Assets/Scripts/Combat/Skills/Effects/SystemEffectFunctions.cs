using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// 系统 EffectFunction 实现（回合维护、状态 tick、死亡流程等）。
    /// 按需实现，其余为 stub。
    /// </summary>

    // ── Real implementations ────────────────────────────────────

    public static class ResetMovementFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
        {
            return EffectResult.Success();
        }

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            // Movement budget reset now handled by sys_round_start's ResetMovement effect.
            // MovementController is a pure executor — no unpaid distance to reset.
        }
    }

    public static class AdvanceCooldownsFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
        {
            return EffectResult.Success();
        }

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            context.Target?.GetComponent<SkillExecutor>()?.AdvanceCooldowns();
        }
    }

    // ── Stub implementations ───────────────────────────────────

    /// <summary>Stub: TriggerStatusTick — 遍历 active effects 执行 tick。</summary>
    public static class TriggerStatusTickFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            // TODO: 当 Status 系统实现时完成
            var executor = context.Target?.GetComponent<SkillExecutor>();
            if (executor == null) return;

            var activeEffects = executor.ActiveEffects;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var active = activeEffects[i];
                if (active.Definition.TickPerRound)
                {
                    var tickCtx = new EffectContext
                    {
                        Caster = active.Source,
                        Target = context.Target,
                        CasterExecutor = active.Source?.GetComponent<SkillExecutor>(),
                        TargetExecutor = context.TargetExecutor,
                    };
                    active.Definition.Apply(tickCtx, EffectResult.Success());
                }
            }
        }
    }

    /// <summary>Stub: DecrementStatusDuration — 所有 Status remainingRounds--。</summary>
    public static class DecrementStatusDurationFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            // TODO: 当 StatusComponent 实现时改为遍历 StatusComponent
            var executor = context.Target?.GetComponent<SkillExecutor>();
            if (executor == null) return;

            var activeEffects = executor.ActiveEffects;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var active = activeEffects[i];
                if (active.Definition.DurationRounds > 0)
                {
                    active.RemainingRounds--;
                    if (active.RemainingRounds <= 0)
                    {
                        executor.RemoveActiveEffect(active);
                    }
                }
            }
        }
    }

    /// <summary>Stub: DeregisterFromCombat — 从 CombatRoundManager.turnOrder 移除单位。</summary>
    public static class DeregisterFromCombatFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            // TODO: 需要引用 CombatRoundManager 实例来移除单位
            Debug.Log($"[DeregisterFromCombat] {context.Target?.name} deregistered (stub).");
        }
    }

    /// <summary>Stub: DeathVisual — 死亡视觉表现。</summary>
    public static class DeathVisualFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            Debug.Log($"[DeathVisual] {context.Target?.name} death visual (stub).");
        }
    }

    /// <summary>Stub: DestroyGameObject — 延迟 Destroy。</summary>
    public static class DestroyGameObjectFunction
    {
        public static EffectResult Compute(EffectContext context, EffectDefinition effect)
            => EffectResult.Success();

        public static void Apply(EffectContext context, EffectDefinition effect, EffectResult computed)
        {
            if (context.Target != null)
                Object.Destroy(context.Target, effect.DestroyDelaySeconds);
        }
    }
}
