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
        [SerializeField] private MoveInputController moveInput;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private List<Player1Controller> playerUnits = new List<Player1Controller>();

        [Header("Controls")]
        [SerializeField] private KeyCode endTurnKey = KeyCode.Space;

        [Header("Combat")]
        [SerializeField, Range(1, 4)] private int maxPartySize = 4;
        [SerializeField] private float attackRange = 1.5f;

        private readonly List<ICombatUnit> _turnOrder = new List<ICombatUnit>();
        private readonly List<EnemyController> _enemyUnits = new List<EnemyController>();
        private readonly List<Player1Controller> _controllableBlock = new List<Player1Controller>();

        public IReadOnlyList<ICombatUnit> TurnOrder => _turnOrder;
        public IReadOnlyList<Player1Controller> ControllableBlock => _controllableBlock;
        public ICombatUnit SelectedUnit { get; private set; }
        public Player1Controller SelectedPlayer => SelectedUnit as Player1Controller;
        public float AttackRange => attackRange;
        public int RoundCount { get; private set; }
        public bool IsWaiting { get; private set; }

        private void Awake()
        {
            if (moveInput == null) moveInput = FindObjectOfType<MoveInputController>();
            if (cameraController == null) cameraController = FindObjectOfType<CameraController>();
            CacheUnits();
        }

        private void Start()
        {
            StartCombat();
        }

        private void Update()
        {
            if (IsWaiting) return;

            if (Input.GetKeyDown(endTurnKey))
            {
                TryEndSelectedPlayerRound();
            }

            for (int i = 0; i < _controllableBlock.Count && i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    TrySelectPlayer(_controllableBlock[i]);
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
            if (player == null || !_controllableBlock.Contains(player) || player.HasEndedRound)
            {
                return false;
            }

            if (SelectedPlayer != null && SelectedPlayer != player)
            {
                SelectedPlayer.SetVisualState(PlayerVisualState.Default);
            }

            SelectedUnit = player;
            player.SetVisualState(PlayerVisualState.Selected);

            if (moveInput != null)
            {
                moveInput.SetPlayer(player);
            }

            if (cameraController != null)
            {
                cameraController.target = player.transform;
            }

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
            foreach (var enemy in _enemyUnits)
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
            playerUnits.RemoveAll(p => p == null);
            if (playerUnits.Count == 0)
            {
                playerUnits.AddRange(FindObjectsOfType<Player1Controller>());
            }

            _enemyUnits.RemoveAll(e => e == null);
            if (_enemyUnits.Count == 0)
            {
                _enemyUnits.AddRange(FindObjectsOfType<EnemyController>());
            }
        }

        private void BuildTurnOrder()
        {
            _turnOrder.Clear();

            var allUnits = new List<ICombatUnit>();
            allUnits.AddRange(playerUnits.Where(p => p != null && p.gameObject.activeInHierarchy).Take(maxPartySize));
            allUnits.AddRange(_enemyUnits.Where(e => e != null && e.gameObject.activeInHierarchy));

            _turnOrder.AddRange(allUnits
                .OrderByDescending(u => u.Initiative)
                .ThenBy(u => u is Player1Controller p ? p.PartySlot : 99));
        }

        private void StartNextRound()
        {
            RoundCount++;

            // Rebuild turn order for the new round (old one was consumed)
            CacheUnits();
            BuildTurnOrder();

            foreach (var unit in _turnOrder)
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
            enemy.FlashTurn();

            yield return new WaitForSeconds(1f);

            enemy.TryEndRound();
            _turnOrder.RemoveAt(0);
            SelectedUnit = null;
            IsWaiting = false;

            AdvanceTurn();
        }

        private void RefreshControllableBlock()
        {
            _controllableBlock.Clear();
            foreach (var unit in _turnOrder)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (unit.HasEndedRound) continue;

                if (unit is Player1Controller player)
                {
                    _controllableBlock.Add(player);
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
            while (_turnOrder.Count > 0)
            {
                var front = _turnOrder[0];
                if (front == null || !front.IsAlive)
                {
                    _turnOrder.RemoveAt(0);
                    continue;
                }
                if (front.HasEndedRound)
                {
                    _turnOrder.RemoveAt(0);
                    continue;
                }
                break;
            }

            if (_turnOrder.Count == 0)
            {
                StartNextRound();
                return;
            }

            var next = _turnOrder[0];

            if (next is Player1Controller player)
            {
                // Player unit → refresh block and select
                RefreshControllableBlock();
                if (!TrySelectPlayer(_controllableBlock.FirstOrDefault(p => !p.HasEndedRound)))
                {
                    // No selectable player in block → all ended, advance past them
                    foreach (var p in _controllableBlock)
                    {
                        if (!p.HasEndedRound) p.TryEndRound();
                    }
                    _turnOrder.RemoveAll(u => u is Player1Controller pc && _controllableBlock.Contains(pc));
                    AdvanceTurn();
                }
            }
            else if (next is EnemyController enemy)
            {
                // Enemy unit → flash highlight then auto-end
                StartCoroutine(EnemyTurnCoroutine(enemy));
            }
        }
    }
}
