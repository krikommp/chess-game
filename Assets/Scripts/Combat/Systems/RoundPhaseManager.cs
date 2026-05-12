using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Subscribes to CombatRoundManager events and executes system skills on units.
    ///
    /// Responsibilities:
    /// - RoundStarted → sys_round_start on each alive unit (RestoreAP + ResetMovement + AdvanceCooldowns + StatusTick)
    /// - UnitTurnEnded → sys_turn_end on the unit (StatusTick + DecrementDuration)
    /// - ExecuteOnDeath → sys_on_death on the unit (Deregister + DeathVisual + Destroy)
    ///
    /// All system skill references are configured via Inspector [SerializeField].
    /// </summary>
    public class RoundPhaseManager : MonoBehaviour
    {
        [Header("System Skills")]
        [Tooltip("Executed on each alive unit when a new round starts.")]
        [SerializeField] private SkillDefinition m_sysRoundStart;

        [Tooltip("Executed on a unit when its turn ends.")]
        [SerializeField] private SkillDefinition m_sysTurnEnd;

        [Tooltip("Executed on a unit when it dies (HP <= 0).")]
        [SerializeField] private SkillDefinition m_sysOnDeath;

        [Header("Refs")]
        [SerializeField] private CombatRoundManager m_roundManager;

        private void OnEnable()
        {
            if (m_roundManager != null)
            {
                m_roundManager.RoundStarted += OnRoundStarted;
                m_roundManager.UnitTurnEnded += OnUnitTurnEnded;
            }
        }

        private void OnDisable()
        {
            if (m_roundManager != null)
            {
                m_roundManager.RoundStarted -= OnRoundStarted;
                m_roundManager.UnitTurnEnded -= OnUnitTurnEnded;
            }
        }

        private void OnRoundStarted(int roundCount)
        {
            if (m_sysRoundStart == null)
            {
                Debug.LogWarning("[RoundPhase] No sys_round_start skill assigned. AP restore, movement reset, and cooldown advance will not run.");
                return;
            }

            foreach (var unit in m_roundManager.TurnOrder)
            {
                ExecuteSystemSkill(unit, m_sysRoundStart, "sys_round_start");
            }
        }

        private void OnUnitTurnEnded(GameObject unit)
        {
            if (m_sysTurnEnd == null) return;
            ExecuteSystemSkill(unit, m_sysTurnEnd, "sys_turn_end");
        }

        public void ExecuteOnDeath(GameObject unit)
        {
            if (m_sysOnDeath == null)
            {
                Debug.LogWarning($"[RoundPhase] No sys_on_death skill assigned. {unit?.name} will not be cleaned up.");
                return;
            }
            ExecuteSystemSkill(unit, m_sysOnDeath, "sys_on_death");
        }

        private void ExecuteSystemSkill(GameObject unit, SkillDefinition skill, string logLabel)
        {
            if (unit == null || skill == null) return;

            var attr = unit.GetComponent<AttributeSet>();
            if (attr == null || !attr.IsAlive) return;

            var executor = unit.GetComponent<SkillExecutor>();
            if (executor == null) return;

            // System skills are self-targeted
            var result = executor.Execute(skill, unit);
            if (!result.IsSuccess)
            {
                Debug.Log($"[RoundPhase] {unit.name}: {logLabel} partially blocked — {result.FailureMessage}");
            }
        }
    }
}
