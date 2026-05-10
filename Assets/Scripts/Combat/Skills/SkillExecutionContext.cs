using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    public struct SkillExecutionContext
    {
        public SkillExecutor CasterExecutor;
        public SkillDefinition Skill;
        public GameObject Target;
        public Vector3? TargetPosition;
        public NavMeshPath Path;
        public SkillInputRequest InputRequest;

        public GameObject Caster => CasterExecutor != null ? CasterExecutor.gameObject : null;
        public AttributeSet CasterAttributes => CasterExecutor != null ? CasterExecutor.Attributes : null;
        public MovementController CasterMovement => CasterExecutor != null ? CasterExecutor.Movement : null;

        public static SkillExecutionContext ForTarget(
            SkillExecutor casterExecutor,
            SkillDefinition skill,
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
            SkillExecutor casterExecutor,
            SkillDefinition skill,
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
            SkillExecutor casterExecutor,
            SkillDefinition skill,
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
