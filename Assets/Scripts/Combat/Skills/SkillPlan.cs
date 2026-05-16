using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat.Skills
{
    /// <summary>
    /// Describes a movement + skill compound plan used by both player preview and AI candidate evaluation.
    /// </summary>
    public struct SkillPlan
    {
        /// <summary>Movement skill to approach the target (e.g. basic_move). Null if already in range.</summary>
        public AbilitySpec MovementSkill;

        /// <summary>The primary skill to execute after movement (e.g. basic_attack).</summary>
        public AbilitySpec PrimarySkill;

        /// <summary>Target of the primary skill.</summary>
        public GameObject PrimaryTarget;

        /// <summary>Where to move before casting the primary skill.</summary>
        public Vector3 MoveDestination;

        /// <summary>Pre-computed NavMesh path from caster to MoveDestination.</summary>
        public NavMeshPath MovePath;

        /// <summary>Estimated AP cost of the movement portion.</summary>
        public int MoveApCost;

        /// <summary>AP cost of the primary skill (read from Ability.Costs).</summary>
        public int PrimaryApCost;

        /// <summary>MoveApCost + PrimaryApCost.</summary>
        public int TotalApCost;

        /// <summary>True if the primary skill can be executed this turn (AP + path valid).</summary>
        public bool CanExecutePrimaryThisTurn;

        /// <summary>True if the caster is already in skill range and no movement is needed.</summary>
        public bool IsAlreadyInRange;

        /// <summary>True if only movement is performed (primary skill not actionable).</summary>
        public bool IsMovementOnly;

        public static SkillPlan Invalid => default;
        public bool IsValid => PrimarySkill != null;

        public override string ToString()
        {
            if (!IsValid) return "[Invalid Plan]";
            var moveDesc = IsAlreadyInRange ? "in range" :
                IsMovementOnly ? $"move only ({MoveApCost} AP)" :
                $"move {MoveApCost}+{PrimaryApCost}={TotalApCost} AP";
            return $"{PrimarySkill.Id} -> {PrimaryTarget?.name} ({moveDesc})";
        }
    }
}
