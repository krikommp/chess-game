using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    public struct SkillExecutionContext
    {
        public AbilitySystemComponent CasterExecutor;
        public SkillAbility Skill;
        public GameObject Target;
        public Vector3? TargetPosition;
        public NavMeshPath Path;
        public SkillInputRequest InputRequest;

        public GameObject Caster => CasterExecutor != null ? CasterExecutor.gameObject : null;
        public AttributeSet CasterAttributes => CasterExecutor != null ? CasterExecutor.Attributes : null;
        public MovementController CasterMovement => CasterExecutor != null ? CasterExecutor.Movement : null;

        public static SkillExecutionContext ForTarget(
            AbilitySystemComponent casterExecutor,
            SkillAbility skill,
            GameObject target)
        {
            return new SkillExecutionContext
            {
                CasterExecutor = casterExecutor,
                Skill = skill,
                Target = target,
            };
        }

        public static SkillExecutionContext ForGroundPoint(
            AbilitySystemComponent casterExecutor,
            SkillAbility skill,
            Vector3 groundPosition,
            NavMeshPath path)
        {
            return new SkillExecutionContext
            {
                CasterExecutor = casterExecutor,
                Skill = skill,
                TargetPosition = groundPosition,
                Path = path,
            };
        }

        public static SkillExecutionContext ForInput(
            AbilitySystemComponent casterExecutor,
            SkillAbility skill,
            SkillInputRequest inputRequest)
        {
            return new SkillExecutionContext
            {
                CasterExecutor = casterExecutor,
                Skill = skill,
                Target = inputRequest.TargetObject,
                TargetPosition = inputRequest.HasWorldPosition ? inputRequest.WorldPosition : null,
                InputRequest = inputRequest,
            };
        }
    }
}
