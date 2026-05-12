using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public struct SkillEffectContext
    {
        public GameObject Caster;
        public GameObject Target;
        public SkillExecutor CasterExecutor;
        public SkillExecutor TargetExecutor;
        public Vector3? TargetPosition;
    }
}
