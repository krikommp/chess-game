using System;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Pure round state machine. Responsibilities:
    /// 1. Collect combat units, sort by Initiative
    /// 2. Maintain turn order queue + controllable block
    /// 3. Broadcast events: RoundStarted, UnitTurnStarted, UnitTurnEnded, CombatEnded
    /// 4. Receive EndTurn and advance the queue
    ///
    /// No game logic (AP, skills, cooldowns, movement, AI, input, camera) lives here.
    /// All game logic is executed through SkillExecutor.OnRoundStart() and handlers
    /// that subscribe to UnitTurnStarted.
    /// </summary>
    public class CombatRoundManager : MonoBehaviour
    {
        // ── Events ──────────────────────────────────────────────────

        public event Action<int> RoundStarted;
        public event Action<GameObject> UnitTurnStarted;
        public event Action<GameObject> UnitTurnEnded;
        public event Action<EBattleResult> CombatEnded;

        // ── Inspector ───────────────────────────────────────────────

        [SerializeField] private bool m_enemyFirstForDebug = false;

        // ── State ───────────────────────────────────────────────────

        private readonly List<GameObject> m_turnOrder = new List<GameObject>();
        private readonly List<GameObject> m_controllableUnits = new List<GameObject>();
        private readonly HashSet<GameObject> m_hasEndedRound = new HashSet<GameObject>();

        public IReadOnlyList<GameObject> TurnOrder => m_turnOrder;
        public IReadOnlyList<GameObject> ControllableUnits => m_controllableUnits;
        public GameObject ActiveUnit { get; private set; }
        public int RoundCount { get; private set; }
        public bool IsWaiting { get; set; }

        private void Start()
        {
            StartCombat();
        }

        // ── Public API ──────────────────────────────────────────────

        public void StartCombat()
        {
            var units = CollectUnits();
            BuildTurnOrder(units);
            RoundCount = 0;
            m_hasEndedRound.Clear();
            StartNextRound();
        }

        public bool EndTurn(GameObject unit)
        {
            if (unit == null || m_hasEndedRound.Contains(unit)) return false;

            var movement = unit.GetComponent<MovementController>();
            if (movement != null && movement.IsMoving) return false;

            m_hasEndedRound.Add(unit);
            UnitTurnEnded?.Invoke(unit);
            AdvanceToNextUnit();
            return true;
        }

        public bool HasEndedRound(GameObject unit) =>
            unit != null && m_hasEndedRound.Contains(unit);

        // ── Internal ────────────────────────────────────────────────

        private List<GameObject> CollectUnits()
        {
            var result = new List<GameObject>();
            foreach (var cu in FindObjectsOfType<CombatUnit>())
            {
                if (cu.gameObject.activeInHierarchy)
                    result.Add(cu.gameObject);
            }
            return result;
        }

        private void BuildTurnOrder(List<GameObject> units)
        {
            m_turnOrder.Clear();
            m_turnOrder.AddRange(units
                .OrderBy(go => m_enemyFirstForDebug && GetFaction(go) == EFaction.Enemy ? 0 : 1)
                .ThenByDescending(go => GetInitiative(go))
                .ThenBy(go => go.name));
        }

        private void StartNextRound()
        {
            RoundCount++;
            var units = CollectUnits();
            BuildTurnOrder(units);
            m_hasEndedRound.Clear();

            // RoundStarted event → RoundPhaseManager executes sys_round_start on each unit
            RoundStarted?.Invoke(RoundCount);

            RefreshControllableBlock();
            AdvanceToNextUnit();
        }

        private void RefreshControllableBlock()
        {
            m_controllableUnits.Clear();
            if (m_turnOrder.Count == 0) return;

            var firstFaction = GetFaction(m_turnOrder[0]);
            if (firstFaction == null) return;

            foreach (var go in m_turnOrder)
            {
                if (go == null || !IsAlive(go) || m_hasEndedRound.Contains(go)) continue;
                if (GetFaction(go) != firstFaction) break;
                m_controllableUnits.Add(go);
            }
        }

        private void AdvanceToNextUnit()
        {
            while (m_turnOrder.Count > 0)
            {
                var front = m_turnOrder[0];
                if (front == null || !IsAlive(front) || m_hasEndedRound.Contains(front))
                    m_turnOrder.RemoveAt(0);
                else
                    break;
            }

            if (m_turnOrder.Count == 0)
            {
                CheckVictory();
                return;
            }

            RefreshControllableBlock();

            if (m_controllableUnits.Count == 0)
            {
                foreach (var go in m_turnOrder.ToList())
                    m_turnOrder.Remove(go);
                AdvanceToNextUnit();
                return;
            }

            var nextUnit = m_controllableUnits[0];
            ActiveUnit = nextUnit;
            UnitTurnStarted?.Invoke(nextUnit);
        }

        private void CheckVictory()
        {
            bool anyPlayerAlive = false;
            bool anyEnemyAlive = false;

            foreach (var unit in CollectUnits())
            {
                var attr = unit.GetComponent<AttributeSet>();
                if (attr == null || !attr.IsAlive) continue;
                if (attr.Faction == EFaction.Player) anyPlayerAlive = true;
                if (attr.Faction == EFaction.Enemy) anyEnemyAlive = true;
            }

            if (!anyPlayerAlive) CombatEnded?.Invoke(EBattleResult.Defeat);
            else if (!anyEnemyAlive) CombatEnded?.Invoke(EBattleResult.Victory);
            else StartNextRound();
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static bool IsAlive(GameObject go) =>
            go.GetComponent<AttributeSet>()?.IsAlive ?? false;

        private static float GetInitiative(GameObject go) =>
            go.GetComponent<AttributeSet>()?.Get(WellKnownAttributeTags.Initiative) ?? 0f;

        private static EFaction? GetFaction(GameObject go) =>
            go.GetComponent<AttributeSet>()?.Faction;
    }

    public enum EBattleResult { Victory, Defeat }
}
