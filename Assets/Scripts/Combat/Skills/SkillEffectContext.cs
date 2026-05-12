using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public struct SkillEffectContext
    {
        public GameObject Caster;
        public GameObject Target;
        public AbilitySystemComponent CasterExecutor;
        public AbilitySystemComponent TargetExecutor;
        public Vector3? TargetPosition;
    }
}
