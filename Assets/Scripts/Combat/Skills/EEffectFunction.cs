namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// Identifies which static EffectFunction class an EffectDefinition delegates to.
    /// Used as a serialized enum on the data-only EffectDefinition asset.
    /// </summary>
    public enum EEffectFunction
    {
        // ── Costs ──────────────────────────────────────────
        SpendAP,

        // ── Cooldowns ──────────────────────────────────────
        SetCooldown,

        // ── Attribute modification ─────────────────────────
        ModifyAttribute,
        RestoreAttribute,

        // ── Status ─────────────────────────────────────────
        AddStatus,
        RemoveStatus,

        // ── Forced movement ────────────────────────────────
        ForcedMove,
        PullTarget,
        TeleportTarget,

        // ── System ─────────────────────────────────────────
        ResetMovement,
        AdvanceCooldowns,
        TriggerStatusTick,
        DecrementStatusDuration,
        DeregisterFromCombat,
        DeathVisual,
        DestroyGameObject,
    }
}
