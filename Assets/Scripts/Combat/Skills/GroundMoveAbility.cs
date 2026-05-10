using MiniChess.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "GroundMoveAbility", menuName = "MiniChess/Skill Abilities/Ground Move", order = 20)]
    public class GroundMoveAbility : SkillAbility
    {
        private static GroundMoveAbility s_defaultInstance;

        public static GroundMoveAbility DefaultInstance
        {
            get
            {
                if (s_defaultInstance == null)
                {
                    s_defaultInstance = CreateInstance<GroundMoveAbility>();
                    s_defaultInstance.hideFlags = HideFlags.HideAndDontSave;
                }

                return s_defaultInstance;
            }
        }

        public override SkillCastResult CanApply(SkillExecutionContext context)
        {
            var skill = context.Skill;
            if (skill == null)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "Skill is null.");

            if (skill.TargetType != ESkillTargetType.GroundPoint)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    $"Expected GroundPoint target type, got {skill.TargetType}.");

            var attr = context.CasterAttributes;
            if (attr == null || !attr.IsAlive)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster is dead or missing AttributeSet.");

            var path = context.Path;
            if (path == null || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 2)
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid,
                    "NavMesh path is invalid or incomplete.");

            var movement = context.CasterMovement;
            if (movement == null)
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed, "Caster has no MovementController.");
            float remainingDistance = movement.RemainingMoveDistance;

            float pathLength = PathCostCalculator.PathLength(path.corners);
            if (pathLength > remainingDistance + 0.001f)
                return SkillCastResult.Fail(ESkillCastFailure.InsufficientAp,
                    $"Move path ({pathLength:F2}m) exceeds remaining move distance ({remainingDistance:F2}m).");

            return SkillCastResult.Success();
        }

        public override SkillCastResult Apply(SkillExecutionContext context)
        {
            var movement = context.CasterMovement;
            if (movement == null)
                return SkillCastResult.Fail(ESkillCastFailure.CasterDead, "Caster has no MovementController.");

            if (!movement.TryStartMove(context.Path))
                return SkillCastResult.Fail(ESkillCastFailure.EffectApplicationFailed,
                    "Failed to start movement via MovementController.");

            return SkillCastResult.Success();
        }

        public override SkillCastResult HandleInput(SkillExecutionContext context, SkillInputRequest inputRequest)
        {
            if (!inputRequest.IsTarget(SkillInputTag.k_TargetGround) || !inputRequest.HasWorldPosition)
            {
                PathPreview.Instance?.Clear();
                return SkillCastResult.Success();
            }

            if (inputRequest.IsSignal(SkillInputTag.k_PointerHover))
            {
                ShowMovePreview(context, inputRequest.WorldPosition);
                return SkillCastResult.Success();
            }

            if (!inputRequest.IsSignal(SkillInputTag.k_PrimaryPressed))
                return SkillCastResult.Success();

            if (!TryBuildMovePath(context, inputRequest.WorldPosition, out Vector3 destination, out NavMeshPath path))
                return SkillCastResult.Fail(ESkillCastFailure.TargetInvalid, "No valid move path.");

            var executeContext = context;
            executeContext.TargetPosition = destination;
            executeContext.Path = path;

            var result = context.CasterExecutor.Execute(executeContext);
            if (result.IsSuccess)
            {
                PathPreview.Instance?.Clear();
                PathPreview.Instance?.ShowActivePath(path.corners);
            }

            return result;
        }

        private static void ShowMovePreview(SkillExecutionContext context, Vector3 worldPosition)
        {
            var preview = PathPreview.Instance;
            if (preview == null) return;

            var movement = context.CasterMovement;
            if (movement == null || !TryGetOrigin(context, out Vector3 origin))
            {
                preview.Clear();
                return;
            }

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit nav, NavMeshManager.Instance.MouseSnapRadius, NavMesh.AllAreas))
            {
                preview.Show(System.Array.Empty<Vector3>(), new[] { origin, worldPosition });
                return;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length < 2)
            {
                Vector3[] invalidPath = path.corners != null && path.corners.Length >= 2
                    ? path.corners
                    : new[] { origin, nav.position };
                preview.Show(System.Array.Empty<Vector3>(), invalidPath);
                return;
            }

            float length = PathCostCalculator.PathLength(path.corners);
            if (length <= movement.RemainingMoveDistance)
            {
                preview.Show(path.corners, System.Array.Empty<Vector3>());
                return;
            }

            PathCostCalculator.Clip(path.corners, movement.RemainingMoveDistance,
                out Vector3[] head, out Vector3[] tail);
            preview.Show(head, tail);
        }

        private static bool TryBuildMovePath(SkillExecutionContext context, Vector3 worldPosition, out Vector3 destination, out NavMeshPath path)
        {
            destination = default;
            path = null;

            var move = context.CasterMovement;
            if (move == null || move.RemainingMoveDistance <= 0f) return false;
            float remainingDist = move.RemainingMoveDistance;
            if (!TryGetOrigin(context, out Vector3 origin)) return false;

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit nav, NavMeshManager.Instance.MouseSnapRadius, NavMesh.AllAreas))
                return false;

            var fullPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, fullPath)
                || fullPath.status != NavMeshPathStatus.PathComplete
                || fullPath.corners == null
                || fullPath.corners.Length < 2)
            {
                return false;
            }

            float length = PathCostCalculator.PathLength(fullPath.corners);
            destination = nav.position;

            if (length > remainingDist + 0.001f)
            {
                PathCostCalculator.Clip(fullPath.corners, remainingDist,
                    out Vector3[] head, out _);
                if (head == null || head.Length < 2) return false;
                destination = head[head.Length - 1];
            }

            path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length < 2)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetOrigin(SkillExecutionContext context, out Vector3 origin)
        {
            var casterObject = context.Caster;
            if (casterObject != null && NavMesh.SamplePosition(casterObject.transform.position, out NavMeshHit hit,
                    NavMeshManager.Instance.OriginSnapRadius, NavMesh.AllAreas))
            {
                origin = hit.position;
                return true;
            }

            origin = casterObject != null ? casterObject.transform.position : Vector3.zero;
            return casterObject != null;
        }
    }
}
