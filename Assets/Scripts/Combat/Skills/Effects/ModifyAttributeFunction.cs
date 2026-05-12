using MiniChess.Combat;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "ModifyAttribute", menuName = "MiniChess/Effect Functions/Modify Attribute", order = 10)]
    public class ModifyAttributeFunction : SkillEffectFunction
    {
        [SerializeField] private float m_amount;
        [SerializeField] private GameplayTag m_attributeTag;

        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null)
                return SkillEffectResult.Fail(ESkillCastFailure.TargetInvalid, "Target has no AttributeSet.");

            if (string.IsNullOrEmpty(m_attributeTag.Value))
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "ModifyAttribute: no attribute tag configured.");

            return SkillEffectResult.Success(m_amount);
        }

        public override void Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null || !targetAttr.IsAlive) return;

            if (string.IsNullOrEmpty(m_attributeTag.Value)) return;

            float delta = computed.ComputedValue;
            targetAttr.Modify(m_attributeTag, delta);
        }
    }
}
