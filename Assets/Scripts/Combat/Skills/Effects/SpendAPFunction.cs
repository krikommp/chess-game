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

            if (context.TargetPosition.HasValue)
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

        public override void Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            var casterAttr = context.Caster?.GetComponent<AttributeSet>();
            if (casterAttr == null) return;

            float amount = computed.ComputedValue > 0f ? computed.ComputedValue : m_fallbackAmount;
            if (amount <= 0f) return;

            casterAttr.TrySpend(WellKnownAttributeTags.AP, amount);
        }
    }
}
