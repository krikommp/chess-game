using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// Routes Compute / Apply calls from EffectDefinition to the correct static EffectFunction class.
    /// </summary>
    public static class EffectFunctionDispatcher
    {
        public static EffectResult Compute(EffectDefinition effect, EffectContext context)
        {
            if (effect == null)
                return EffectResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Effect is null.");

            // Check effect-level tag conditions
            var tagResult = EvaluateEffectTags(effect, context);
            if (!tagResult.IsSuccess)
                return tagResult;

            switch (effect.Function)
            {
                case EEffectFunction.SpendAP:
                    return SpendAPFunction.Compute(context, effect);
                case EEffectFunction.SetCooldown:
                    return SetCooldownFunction.Compute(context, effect);
                case EEffectFunction.ModifyAttribute:
                    return ModifyAttributeFunction.Compute(context, effect);
                case EEffectFunction.RestoreAttribute:
                    return RestoreAttributeFunction.Compute(context, effect);
                case EEffectFunction.ResetMovement:
                    return ResetMovementFunction.Compute(context, effect);
                case EEffectFunction.AdvanceCooldowns:
                    return AdvanceCooldownsFunction.Compute(context, effect);
                case EEffectFunction.TriggerStatusTick:
                    return TriggerStatusTickFunction.Compute(context, effect);
                case EEffectFunction.DecrementStatusDuration:
                    return DecrementStatusDurationFunction.Compute(context, effect);
                case EEffectFunction.DeregisterFromCombat:
                    return DeregisterFromCombatFunction.Compute(context, effect);
                case EEffectFunction.DeathVisual:
                    return DeathVisualFunction.Compute(context, effect);
                case EEffectFunction.DestroyGameObject:
                    return DestroyGameObjectFunction.Compute(context, effect);
                case EEffectFunction.AddStatus:
                case EEffectFunction.RemoveStatus:
                case EEffectFunction.ForcedMove:
                case EEffectFunction.PullTarget:
                case EEffectFunction.TeleportTarget:
                default:
                    return EffectResult.Success(); // Stub: always succeed
            }
        }

        public static void Apply(EffectDefinition effect, EffectContext context, EffectResult computed)
        {
            if (effect == null) return;
            if (!computed.IsSuccess && effect.Function != EEffectFunction.SpendAP
                && effect.Function != EEffectFunction.SetCooldown
                && effect.Function != EEffectFunction.ModifyAttribute
                && effect.Function != EEffectFunction.RestoreAttribute
                && effect.Function != EEffectFunction.ResetMovement
                && effect.Function != EEffectFunction.AdvanceCooldowns
                && effect.Function != EEffectFunction.TriggerStatusTick
                && effect.Function != EEffectFunction.DecrementStatusDuration
                && effect.Function != EEffectFunction.DeregisterFromCombat
                && effect.Function != EEffectFunction.DeathVisual
                && effect.Function != EEffectFunction.DestroyGameObject)
                return;

            switch (effect.Function)
            {
                case EEffectFunction.SpendAP:
                    SpendAPFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.SetCooldown:
                    SetCooldownFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.ModifyAttribute:
                    ModifyAttributeFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.RestoreAttribute:
                    RestoreAttributeFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.ResetMovement:
                    ResetMovementFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.AdvanceCooldowns:
                    AdvanceCooldownsFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.TriggerStatusTick:
                    TriggerStatusTickFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.DecrementStatusDuration:
                    DecrementStatusDurationFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.DeregisterFromCombat:
                    DeregisterFromCombatFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.DeathVisual:
                    DeathVisualFunction.Apply(context, effect, computed);
                    break;
                case EEffectFunction.DestroyGameObject:
                    DestroyGameObjectFunction.Apply(context, effect, computed);
                    break;
                // Stubs: no-op
            }
        }

        private static EffectResult EvaluateEffectTags(EffectDefinition effect, EffectContext context)
        {
            var target = context.Target;
            if (target == null) return EffectResult.Success();

            var tagComp = target.GetComponent<GameplayTags.GameplayTagComponent>();
            var tagSet = tagComp?.TagSet;
            if (tagSet == null) return EffectResult.Success();

            // RequiredTags: target must have ALL
            var required = effect.RequiredTags;
            for (int i = 0; i < required.Length; i++)
            {
                if (string.IsNullOrEmpty(required[i].Value)) continue;
                if (!tagSet.Has(required[i], ETagMatchMode.Exact))
                    return EffectResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target lacks required tag '{required[i].Value}' for effect.");
            }

            // BlockedTags: target must have NONE
            var blocked = effect.BlockedTags;
            for (int i = 0; i < blocked.Length; i++)
            {
                if (string.IsNullOrEmpty(blocked[i].Value)) continue;
                if (tagSet.Has(blocked[i], ETagMatchMode.Exact))
                    return EffectResult.Fail(ESkillCastFailure.TagConditionFailed,
                        $"Target is blocked by tag '{blocked[i].Value}' for effect.");
            }

            return EffectResult.Success();
        }
    }
}
