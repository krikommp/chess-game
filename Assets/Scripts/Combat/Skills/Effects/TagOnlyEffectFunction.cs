using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "TagOnly", menuName = "MiniChess/Effect Functions/Tag Only", order = 2)]
    public class TagOnlyEffectFunction : SkillEffectFunction
    {
        public override SkillEffectResult Compute(SkillEffectContext context, SkillEffect effect)
        {
            return SkillEffectResult.Success();
        }

        public override SkillEffectResult Apply(SkillEffectContext context, SkillEffect effect, SkillEffectResult result)
        {
            return SkillEffectResult.Success();
        }
    }
}
