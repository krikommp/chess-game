using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Round loop with initiative-based turn order.
    /// Consecutive player units form a "controllable block" where the player
    /// can freely switch. Enemy units block the front and auto-skip their turn.
    /// </summary>
    public class CombatRoundManager : MonoBehaviour
    {
        public event Action SelectedPlayerChanged;
        public event Action RoundChanged;

        [Header("Refs")]
        [SerializeField] private MoveInputController m_moveInput;
        [SerializeField] private CameraController m_cameraController;
        [SerializeField] private EnemyTurnRunner m_enemyTurnRunner;
        [SerializeField] private List<Player1Controller> m_playerUnits = new List<Player1Controller>();

        [Header("Controls")]
        [SerializeField] private KeyCode m_endTurnKey = KeyCode.Space;

        [Header("Combat")]
        [SerializeField, Range(1, 4)] private int m_maxPartySize = 4;
        [SerializeField] private float m_attackRange = 1.5f;
        [SerializeField] private int m_basicAttackCost = 1;
        [SerializeField] private int m_basicAttackDamage = 20;

        [Header("Debug")]
        [Tooltip("When enabled, all enemy units act before any player unit regardless of Initiative. Intended only for AI testing — does not represent formal first-strike rules.")]
        [SerializeField] private bool m_enemyFirstForDebug = false;

        private readonly List<ICombatUnit> m_turnOrder = new List<ICombatUnit>();
        private readonly List<EnemyController> m_enemyUnits = new List<EnemyController>();
        private readonly List<Player1Controller> m_controllableBlock = new List<Player1Controller>();

        public IReadOnlyList<ICombatUnit> TurnOrder => m_turnOrder;
        public IReadOnlyList<Player1Controller> ControllableBlock => m_controllableBlock;
        public ICombatUnit SelectedUnit { get; private set; }
        public Player1Controller SelectedPlayer => SelectedUnit as Player1Controller;
        public float AttackRange => m_attackRange;
        public int RoundCount { get; private set; }
        public bool IsWaiting { get; private set; }

        private void Awake()
        {
            if (m_moveInput == null) m_moveInput = FindObjectOfType<MoveInputController>();
            if (m_cameraController == null) m_cameraController = FindObjectOfType<CameraController>();
            if (m_enemyTurnRunner == null) m_enemyTurnRunner = GetComponent<EnemyTurnRunner>();
            if (m_enemyTurnRunner == null) m_enemyTurnRunner = gameObject.AddComponent<EnemyTurnRunner>();
            CacheUnits();
        }

        private void Start()
        {
            StartCombat();
        }

        private void Update()
        {
            if (IsWaiting) return;

            if (Input.GetKeyDown(m_endTurnKey))
            {
                TryEndSelectedPlayerRound();
            }

            for (int i = 0; i < m_controllableBlock.Count && i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    TrySelectPlayer(m_controllableBlock[i]);
                }
            }
        }

        public void StartCombat()
        {
            CacheUnits();
            BuildTurnOrder();
            RoundCount = 0;
            StartNextRound();
        }

        public bool TrySelectPlayer(Player1Controller player)
        {
            if (player == null || !m_controllableBlock.Contains(player) || player.HasEndedRound)
            {
                return false;
            }

            if (SelectedPlayer != null && SelectedPlayer != player)
            {
                SelectedPlayer.SetVisualState(EPlayerVisualState.Default);
            }

            SelectedUnit = player;
            player.SetVisualState(EPlayerVisualState.Selected);

            if (m_moveInput != null)
            {
                m_moveInput.SetPlayer(player);
            }

            FocusCameraOnUnit(player);

            SelectedPlayerChanged?.Invoke();
            return true;
        }

        public bool TryEndSelectedPlayerRound()
        {
            if (SelectedPlayer == null || !SelectedPlayer.TryEndRound())
            {
                return false;
            }

            AdvanceTurn();
            return true;
        }

        public EnemyController GetEnemyUnderAttack(Player1Controller attacker)
        {
            // Find the nearest alive enemy
            EnemyController best = null;
            float bestDist = float.MaxValue;
            foreach (var enemy in m_enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                float dist = Vector3.Distance(attacker.transform.position, enemy.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = enemy;
                }
            }
            return best;
        }

        private void CacheUnits()
        {
            m_playerUnits.RemoveAll(p => p == null);
            if (m_playerUnits.Count == 0)
            {
                m_playerUnits.AddRange(FindObjectsOfType<Player1Controller>());
            }

            // Always re-scan enemies because EnemySpawners may have run their Awake
            // after our first CacheUnits call (Awake order is non-deterministic).
            m_enemyUnits.Clear();
            m_enemyUnits.AddRange(FindObjectsOfType<EnemyController>());
        }

        private void BuildTurnOrder()
        {
            m_turnOrder.Clear();

            var allUnits = new List<ICombatUnit>();
            allUnits.AddRange(m_playerUnits.Where(p => p != null && p.gameObject.activeInHierarchy).Take(m_maxPartySize));
            allUnits.AddRange(m_enemyUnits.Where(e => e != null && e.gameObject.activeInHierarchy));

            m_turnOrder.AddRange(allUnits
                .OrderBy(u => m_enemyFirstForDebug && u is EnemyController ? 0 : 1)
                .ThenByDescending(u => u.Initiative)
                .ThenBy(u => u is Player1Controller p ? p.PartySlot : 99));
        }

        private void StartNextRound()
        {
            RoundCount++;

            // Rebuild turn order for the new round (old one was consumed)
            CacheUnits();
            BuildTurnOrder();

            foreach (var unit in m_turnOrder)
            {
                if (unit.IsAlive)
                {
                    unit.BeginRound();
                }
            }

            RoundChanged?.Invoke();
            RefreshControllableBlock();
            AdvanceTurn();
        }

        private IEnumerator EnemyTurnCoroutine(EnemyController enemy)
        {
            IsWaiting = true;
            SelectedUnit = enemy;
            FocusCameraOnUnit(enemy);
            enemy.FlashTurn();
            Debug.Log($"[Combat] Enemy turn starts: {enemy.DisplayName}");

            yield return m_enemyTurnRunner.RunTurn(enemy, m_playerUnits, m_enemyUnits, m_attackRange, m_basicAttackCost, m_basicAttackDamage);

            enemy.TryEndRound();
            m_turnOrder.RemoveAt(0);
            SelectedUnit = null;
            IsWaiting = false;

            AdvanceTurn();
        }

        private void RefreshControllableBlock()
        {
            m_controllableBlock.Clear();
            foreach (var unit in m_turnOrder)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (unit.HasEndedRound) continue;

                if (unit is Player1Controller player)
                {
                    m_controllableBlock.Add(player);
                }
                else
                {
                    break; // enemy blocks the front
                }
            }
        }

        private void AdvanceTurn()
        {
            // Skip finished/dead units at the front
            while (m_turnOrder.Count > 0)
            {
                var front = m_turnOrder[0];
                if (front == null || !front.IsAlive)
                {
                    m_turnOrder.RemoveAt(0);
                    continue;
                }
                if (front.HasEndedRound)
                {
                    m_turnOrder.RemoveAt(0);
                    continue;
                }
                break;
            }

            if (m_turnOrder.Count == 0)
            {
                StartNextRound();
                return;
            }

            var next = m_turnOrder[0];

            if (next is Player1Controller player)
            {
                // Player unit → refresh block and select
                RefreshControllableBlock();
                if (!TrySelectPlayer(m_controllableBlock.FirstOrDefault(p => !p.HasEndedRound)))
                {
                    // No selectable player in block → all ended, advance past them
                    foreach (var p in m_controllableBlock)
                    {
                        if (!p.HasEndedRound) p.TryEndRound();
                    }
                    m_turnOrder.RemoveAll(u => u is Player1Controller pc && m_controllableBlock.Contains(pc));
                    AdvanceTurn();
                }
            }
            else if (next is EnemyController enemy)
            {
                // Enemy unit → flash highlight then auto-end
                StartCoroutine(EnemyTurnCoroutine(enemy));
            }
        }

        private void FocusCameraOnUnit(ICombatUnit unit)
        {
            if (m_cameraController == null) return;
            if (!(unit is Component component)) return;

            m_cameraController.FocusOn(component.transform);
        }
    }
}




