using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public struct SkillEffectContext
    {
        public GameObject Caster;
        public GameObject Target;
        public GameObject Source;
        public AbilitySystemComponent CasterExecutor;
        public AbilitySystemComponent TargetExecutor;
        public Vector3? TargetPosition;
        public float? PathLength;
        public float DeltaDistance;
        public float TotalDistance;
    }
}
