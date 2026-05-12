using MiniChess.Combat;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public enum ERestoreMode { ToMax, ByValue, PercentOfMax }

    [CreateAssetMenu(fileName = "RestoreAttribute", menuName = "MiniChess/Effect Functions/Restore Attribute", order = 11)]
    public class RestoreAttributeFunction : SkillEffectFunction
    {
        [SerializeField] private float m_amount;
        [SerializeField] private GameplayTag m_attributeTag;
        [SerializeField] private ERestoreMode m_mode = ERestoreMode.ToMax;

        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null)
                return SkillEffectResult.Fail(ESkillCastFailure.TargetInvalid, "Target has no AttributeSet.");

            if (string.IsNullOrEmpty(m_attributeTag.Value))
                return SkillEffectResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "RestoreAttribute: no attribute tag configured.");

            return SkillEffectResult.Success(m_amount);
        }

        public override void Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult computed)
        {
            var targetAttr = context.Target?.GetComponent<AttributeSet>();
            if (targetAttr == null) return;

            if (string.IsNullOrEmpty(m_attributeTag.Value)) return;

            switch (m_mode)
            {
                case ERestoreMode.ToMax:
                    targetAttr.SetToMax(m_attributeTag);
                    break;
                case ERestoreMode.ByValue:
                    targetAttr.Modify(m_attributeTag, m_amount);
                    break;
                case ERestoreMode.PercentOfMax:
                    float max = targetAttr.GetMax(m_attributeTag);
                    if (max > 0f)
                        targetAttr.Modify(m_attributeTag, max * m_amount);
                    break;
            }
        }
    }
}
