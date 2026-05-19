using UnityEngine;

namespace MiniChess.Combat.Skills
{
    public sealed class SkillExecutionState
    {
        public SkillExecutionContext Context { get; }
        public SkillEffectResult[] CostResults { get; }
        public Vector3 LastCasterPosition { get; private set; }
        public float DeltaDistance { get; private set; }
        public float TotalDistance { get; private set; }
        public bool IsComplete { get; private set; }

        public SkillExecutionState(
            SkillExecutionContext context,
            SkillEffectResult[] costResults,
            Vector3 casterPosition)
        {
            Context = context;
            CostResults = costResults ?? System.Array.Empty<SkillEffectResult>();
            LastCasterPosition = casterPosition;
        }

        public void RecordCasterPosition(Vector3 currentPosition)
        {
            DeltaDistance = Vector3.Distance(LastCasterPosition, currentPosition);
            TotalDistance += DeltaDistance;
            LastCasterPosition = currentPosition;
        }

        public void Complete()
        {
            IsComplete = true;
            DeltaDistance = 0f;
        }
    }
}
