using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Combat.Skills;
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
        [SerializeField] private InputController m_inputController;
        [SerializeField] private CameraController m_cameraController;
        [SerializeField] private EnemyTurnRunner m_enemyTurnRunner;
        [SerializeField] private List<Player1Controller> m_playerUnits = new List<Player1Controller>();

        [Header("Controls")]
        [SerializeField] private KeyCode m_endTurnKey = KeyCode.Space;

        [Header("Combat")]
        [SerializeField, Range(1, 4)] private int m_maxPartySize = 4;
        [Header("Skills")]
        [Tooltip("Optional: assign basic_attack skill. If null, auto-loaded from Assets/Data/Skills/.")]
        public SkillDefinition basicAttackSkillOverride;
        public SkillDefinition basicMoveSkillOverride;

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
        public float AttackRange => BasicAttackSkill != null ? BasicAttackSkill.Range : 1.5f;
        public SkillDefinition BasicAttackSkill { get; private set; }
        public SkillDefinition BasicMoveSkill { get; private set; }
        public int RoundCount { get; private set; }
        public bool IsWaiting { get; private set; }

        private void Awake()
        {
            if (m_inputController == null) m_inputController = FindObjectOfType<InputController>();
            if (m_cameraController == null) m_cameraController = FindObjectOfType<CameraController>();
            if (m_enemyTurnRunner == null) m_enemyTurnRunner = GetComponent<EnemyTurnRunner>();
            if (m_enemyTurnRunner == null) m_enemyTurnRunner = gameObject.AddComponent<EnemyTurnRunner>();
            if (m_inputController != null) m_inputController.InputReceived += HandleInputReceived;
            CacheUnits();
            ResolveBasicAttackSkill();
            ResolveBasicMoveSkill();
        }

        private void OnDestroy()
        {
            if (m_inputController != null) m_inputController.InputReceived -= HandleInputReceived;
        }

        private void ResolveBasicAttackSkill()
        {
            // 1. Inspector override
            if (basicAttackSkillOverride != null)
            {
                BasicAttackSkill = basicAttackSkillOverride;
                return;
            }

            // 2. Try load from asset path (Editor only)
#if UNITY_EDITOR
            BasicAttackSkill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                "Assets/Data/Skills/basic_attack.asset");
            if (BasicAttackSkill != null)
            {
                Debug.Log("[CombatRoundManager] Loaded basic_attack.asset from project.");
                return;
            }
#endif
            // 3. Try Resources
            BasicAttackSkill = Resources.Load<SkillDefinition>("Skills/basic_attack");
            if (BasicAttackSkill == null)
                BasicAttackSkill = Resources.Load<SkillDefinition>("basic_attack");

            if (BasicAttackSkill == null)
                Debug.LogWarning("[CombatRoundManager] basic_attack skill not found. Attacks will not work.");
        }

        private void ResolveBasicMoveSkill()
        {
            if (basicMoveSkillOverride != null)
            {
                BasicMoveSkill = basicMoveSkillOverride;
                return;
            }

#if UNITY_EDITOR
            BasicMoveSkill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>(
                "Assets/Data/Skills/basic_move.asset");
            if (BasicMoveSkill != null)
            {
                Debug.Log("[CombatRoundManager] Loaded basic_move.asset from project.");
                return;
            }
#endif
            BasicMoveSkill = Resources.Load<SkillDefinition>("Skills/basic_move");
            if (BasicMoveSkill == null)
                BasicMoveSkill = Resources.Load<SkillDefinition>("basic_move");

            if (BasicMoveSkill == null)
                Debug.LogWarning("[CombatRoundManager] basic_move skill not found. Units must configure their own movement skill assets.");
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
            ValidateUnitSkillComponents();
            BuildTurnOrder();
            RoundCount = 0;
            StartNextRound();
        }

        private void ValidateUnitSkillComponents()
        {
            foreach (var unit in m_playerUnits)
            {
                if (unit == null) continue;
                var executor = unit.GetComponent<SkillExecutor>();
                if (executor == null)
                {
                    Debug.LogWarning($"[CombatRoundManager] {unit.DisplayName} has no SkillExecutor and cannot use skills.");
                    continue;
                }

                if (executor.FindSkill("basic_move") == null)
                {
                    Debug.LogWarning($"[CombatRoundManager] {unit.DisplayName} has no configured basic_move skill.");
                }
            }

            foreach (var enemy in m_enemyUnits)
            {
                if (enemy == null) continue;
                var executor = enemy.GetComponent<SkillExecutor>();
                if (executor == null)
                {
                    Debug.LogWarning($"[CombatRoundManager] {enemy.DisplayName} has no SkillExecutor and cannot use skills.");
                }
            }
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

            if (m_inputController != null)
            {
                var executor = player.GetComponent<SkillExecutor>();
                var moveSkill = executor != null ? executor.FindSkill("basic_move") : null;
                if (moveSkill == null)
                {
                    Debug.LogWarning($"[CombatRoundManager] {player.DisplayName} has no basic_move skill; selected unit cannot move.");
                    executor?.ClearActiveSkill();
                }
                else
                {
                    executor.ActivateSkill(moveSkill, m_inputController.CreateSkillInputServices());
                }
            }

            FocusCameraOnUnit(player);

            SelectedPlayerChanged?.Invoke();
            return true;
        }

        private void HandleInputReceived(SkillInputRequest request)
        {
            if (IsWaiting) return;

            if (request.IsSignal(SkillInputTag.k_PrimaryPressed)
                && request.IsTarget(SkillInputTag.k_TargetPlayer)
                && request.TargetObject != null)
            {
                var player = request.TargetObject.GetComponent<Player1Controller>();
                if (player != null && TrySelectPlayer(player))
                    return;
            }

            var selectedPlayer = SelectedPlayer;
            if (selectedPlayer == null || selectedPlayer.HasEndedRound) return;

            var executor = selectedPlayer.GetComponent<SkillExecutor>();
            executor?.HandleInput(request);
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

            yield return m_enemyTurnRunner.RunTurn(enemy, m_playerUnits, m_enemyUnits, BasicAttackSkill);

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




