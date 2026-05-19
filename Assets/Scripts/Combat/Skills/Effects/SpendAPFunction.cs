using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "SpendAP", menuName = "MiniChess/Effect Functions/Spend AP", order = 1)]
    public class SpendAPFunction : SkillEffectFunction
    {
        [Tooltip("Fallback amount when no NavMesh path is available.")]
        [SerializeField] private float m_fallbackAmount = 1f;

        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            var casterAttr = context.Caster?.GetComponent<AttributeSet>();
            if (casterAttr == null)
                return SkillEffectResult.Fail(ESkillCastFailure.CasterDead, "Caster has no AttributeSet.");

            float currentAP = casterAttr.Get(WellKnownAttributeTags.AP);
            float amount;

            if (context.PathLength.HasValue)
            {
                if (currentAP <= 0f && context.PathLength.Value > 0.001f)
                    return SkillEffectResult.Fail(ESkillCastFailure.InsufficientAp, "No AP available for movement.");

                return SkillEffectResult.Success(0f);
            }
            else if (context.TargetPosition.HasValue)
            {
                var casterPos = context.Caster.transform.position;
                var nav = NavMeshService.Instance;
                if (nav != null && nav.CalculatePath(casterPos, context.TargetPosition.Value, out var path))
                {
                    float pathLength = NavMeshService.PathLength(path.corners);
                    float speed = casterAttr.Get(WellKnownAttributeTags.MoveSpeed);
                    amount = NavMeshService.EstimateMoveApCost(pathLength, speed, Mathf.FloorToInt(currentAP));
                }
                else
                {
                    amount = m_fallbackAmount;
                }
            }
            else
            {
                amount = m_fallbackAmount;
            }

            if (amount <= 0f)
                return SkillEffectResult.Success(0f);

            if (currentAP < amount)
                return SkillEffectResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Need {amount} AP, have {currentAP}.");

            return SkillEffectResult.Success(amount);
        }

        public override SkillEffectResult Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            if (context.PathLength.HasValue)
                return SkillEffectResult.Success(0f);

            var casterAttr = context.Caster?.GetComponent<AttributeSet>();
            if (casterAttr == null)
                return SkillEffectResult.Fail(ESkillCastFailure.CasterDead, "Caster has no AttributeSet.");

            float amount = computed.ComputedValue > 0f ? computed.ComputedValue : m_fallbackAmount;
            if (amount <= 0f)
                return SkillEffectResult.Success(0f);

            if (!casterAttr.TrySpend(WellKnownAttributeTags.AP, amount))
                return SkillEffectResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Failed to spend {amount} AP.");

            return SkillEffectResult.Success(amount);
        }

        public override SkillEffectResult Update(
            SkillEffectContext context,
            SkillEffect effect,
            SkillEffectResult result,
            float deltaTime)
        {
            if (context.DeltaDistance <= 0.0001f)
                return SkillEffectResult.Success();

            var movement = context.Caster?.GetComponent<MovementController>();
            if (movement == null)
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Caster has no MovementController for movement AP cost.");

            if (!movement.TrySpendMoveDistance(context.DeltaDistance, out int spentAmount))
                return SkillEffectResult.Fail(ESkillCastFailure.InsufficientAp,
                    "Movement AP budget depleted.");

            return SkillEffectResult.Success(spentAmount);
        }

        public override SkillCostPreviewResult PreviewMaxPathLength(
            SkillEffectContext context,
            SkillEffect effect,
            float currentMaxPathLength)
        {
            if (!context.PathLength.HasValue)
                return SkillCostPreviewResult.Success(currentMaxPathLength);

            var casterAttr = context.Caster?.GetComponent<AttributeSet>();
            if (casterAttr == null)
            {
                return SkillCostPreviewResult.Fail(ESkillCastFailure.CasterDead,
                    "Caster has no AttributeSet.");
            }

            float currentAP = casterAttr.Get(WellKnownAttributeTags.AP);
            float moveSpeed = casterAttr.Get(WellKnownAttributeTags.MoveSpeed);
            var movement = context.Caster.GetComponent<MovementController>();
            float unpaidDistance = movement != null ? movement.UnpaidMoveDistance : 0f;
            float maxDistance = Mathf.Max(0f, currentAP * moveSpeed - unpaidDistance);

            return SkillCostPreviewResult.Success(Mathf.Min(currentMaxPathLength, maxDistance));
        }
    }
}
