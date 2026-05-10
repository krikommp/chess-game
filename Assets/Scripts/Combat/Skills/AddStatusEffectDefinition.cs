using UnityEngine;

namespace MiniChess.Combat.Skills
{
    [CreateAssetMenu(fileName = "AddStatusEffect", menuName = "MiniChess/Effects/Add Status", order = 3)]
    public class AddStatusEffectDefinition : EffectDefinition
    {
        [Tooltip("Status to apply. Pending StatusDefinition type.")]
        [SerializeField] private string _statusId;

        [SerializeField] private int _durationTurns = 1;

        public string StatusId => _statusId ?? string.Empty;
        public int DurationTurns => _durationTurns;
    }
}
