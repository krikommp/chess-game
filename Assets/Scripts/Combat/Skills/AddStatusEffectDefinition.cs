using MiniChess.Combat;
using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "AddStatusEffect", menuName = "MiniChess/Effects/Add Status", order = 3)]
    public class AddStatusEffectDefinition : EffectDefinition
    {
        [Tooltip("Status to apply. Pending StatusDefinition type.")]
        [SerializeField] private string m_statusId;

        [SerializeField] private int m_durationTurns = 1;

        public string StatusId => m_statusId ?? string.Empty;
        public int DurationTurns => m_durationTurns;

        public override ETargetCapability RequiredCapability => ETargetCapability.Statusable;

        public override void Apply(EffectContext context)
        {
            if (context.Target == null) return;
            // Status application pending StatusDefinition / StatusComponent implementation.
            Debug.Log($"[Effect] AddStatus '{m_statusId}' on {context.Target.name} for {m_durationTurns} turns (pending StatusComponent).");
        }
    }
}
