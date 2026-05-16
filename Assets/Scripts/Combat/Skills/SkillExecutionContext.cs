using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    public struct SkillExecutionContext
    {
        public AbilitySystemComponent CasterExecutor;
        public AbilitySpec Spec;
        public GameObject Target;
        public Vector3? TargetPosition;
        public NavMeshPath Path;
        public SkillInputRequest InputRequest;

        public SkillDefinition Definition => Spec != null ? Spec.Definition : null;
        public SkillAbility Skill => Spec != null ? Spec.Ability : null;
        public GameObject Caster => CasterExecutor != null ? CasterExecutor.gameObject : null;
        public AttributeSet CasterAttributes => CasterExecutor != null ? CasterExecutor.Attributes : null;
        public MovementController CasterMovement => CasterExecutor != null ? CasterExecutor.Movement : null;

        public static SkillExecutionContext ForTarget(
            AbilitySystemComponent casterExecutor,
            AbilitySpec spec,
            GameObject target)
        {
            return new SkillExecutionContext
            {
                CasterExecutor = casterExecutor,
                Spec = spec,
                Target = target,
            };
        }

        public static SkillExecutionContext ForGroundPoint(
            AbilitySystemComponent casterExecutor,
            AbilitySpec spec,
            Vector3 groundPosition,
            NavMeshPath path)
        {
            return new SkillExecutionContext
            {
                CasterExecutor = casterExecutor,
                Spec = spec,
                TargetPosition = groundPosition,
                Path = path,
            };
        }

        public static SkillExecutionContext ForInput(
            AbilitySystemComponent casterExecutor,
            AbilitySpec spec,
            SkillInputRequest inputRequest)
        {
            return new SkillExecutionContext
            {
                CasterExecutor = casterExecutor,
                Spec = spec,
                Target = inputRequest.TargetObject,
                TargetPosition = inputRequest.HasWorldPosition ? inputRequest.WorldPosition : null,
                InputRequest = inputRequest,
            };
        }
    }
}
