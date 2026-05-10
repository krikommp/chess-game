using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
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
        [SerializeField] private SkillDefinition m_basicAttackSkillOverride;
        [SerializeField] private SkillDefinition m_basicMoveSkillOverride;

        [Header("Debug")]
        [Tooltip("When enabled, all enemy units act before any player unit regardless of Initiative.")]
        [SerializeField] private bool m_enemyFirstForDebug = false;

        private readonly List<GameObject> m_turnOrder = new List<GameObject>();
        private readonly List<EnemyController> m_enemyUnits = new List<EnemyController>();
        private readonly List<Player1Controller> m_controllableBlock = new List<Player1Controller>();
        private readonly HashSet<GameObject> m_hasEndedRound = new HashSet<GameObject>();

        public IReadOnlyList<GameObject> TurnOrder => m_turnOrder;
        public IReadOnlyList<Player1Controller> ControllableBlock => m_controllableBlock;
        public GameObject SelectedUnit { get; private set; }
        public Player1Controller SelectedPlayer => SelectedUnit != null ? SelectedUnit.GetComponent<Player1Controller>() : null;
        public float AttackRange => BasicAttackSkill != null ? BasicAttackSkill.Range : 1.5f;
        public SkillDefinition BasicAttackSkill { get; private set; }
        public SkillDefinition BasicMoveSkill { get; private set; }
        public int RoundCount { get; private set; }
        public bool IsWaiting { get; private set; }

        public bool HasEndedRound(GameObject unitGO) => unitGO != null && m_hasEndedRound.Contains(unitGO);

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
            if (m_basicAttackSkillOverride != null) { BasicAttackSkill = m_basicAttackSkillOverride; return; }
#if UNITY_EDITOR
            BasicAttackSkill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Skills/basic_attack.asset");
            if (BasicAttackSkill != null) return;
#endif
            BasicAttackSkill = Resources.Load<SkillDefinition>("Skills/basic_attack");
            if (BasicAttackSkill == null) BasicAttackSkill = Resources.Load<SkillDefinition>("basic_attack");
            if (BasicAttackSkill == null)
                Debug.LogWarning("[CombatRoundManager] basic_attack skill not found.");
        }

        private void ResolveBasicMoveSkill()
        {
            if (m_basicMoveSkillOverride != null) { BasicMoveSkill = m_basicMoveSkillOverride; return; }
#if UNITY_EDITOR
            BasicMoveSkill = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Skills/basic_move.asset");
            if (BasicMoveSkill != null) return;
#endif
            BasicMoveSkill = Resources.Load<SkillDefinition>("Skills/basic_move");
            if (BasicMoveSkill == null) BasicMoveSkill = Resources.Load<SkillDefinition>("basic_move");
            if (BasicMoveSkill == null)
                Debug.LogWarning("[CombatRoundManager] basic_move skill not found.");
        }

        private void Start() { StartCombat(); }

        private void Update()
        {
            if (IsWaiting) return;

            if (Input.GetKeyDown(m_endTurnKey))
                TryEndSelectedPlayerRound();

            for (int i = 0; i < m_controllableBlock.Count && i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    TrySelectPlayer(m_controllableBlock[i]);
            }
        }

        public void StartCombat()
        {
            CacheUnits();
            ValidateUnitSkillComponents();
            BuildTurnOrder();
            RoundCount = 0;
            m_hasEndedRound.Clear();
            StartNextRound();
        }

        private void ValidateUnitSkillComponents()
        {
            foreach (var unit in m_playerUnits)
            {
                if (unit == null) continue;
                var executor = unit.GetComponent<SkillExecutor>();
                if (executor == null)
                    Debug.LogWarning($"[CombatRoundManager] {GetDisplayName(unit.gameObject)} has no SkillExecutor.");
                else if (executor.FindSkill("basic_move") == null)
                    Debug.LogWarning($"[CombatRoundManager] {GetDisplayName(unit.gameObject)} has no basic_move skill.");
            }

            foreach (var enemy in m_enemyUnits)
            {
                if (enemy == null) continue;
                var executor = enemy.GetComponent<SkillExecutor>();
                if (executor == null)
                    Debug.LogWarning($"[CombatRoundManager] {GetDisplayName(enemy.gameObject)} has no SkillExecutor.");
            }
        }

        public bool TrySelectPlayer(Player1Controller player)
        {
            if (player == null || !m_controllableBlock.Contains(player) || HasEndedRound(player.gameObject))
                return false;

            if (SelectedPlayer != null && SelectedPlayer != player)
                SelectedPlayer.SetVisualState(EPlayerVisualState.Default);

            SelectedUnit = player.gameObject;
            player.SetVisualState(EPlayerVisualState.Selected);

            if (m_inputController != null)
            {
                var executor = player.GetComponent<SkillExecutor>();
                var moveSkill = executor != null ? executor.FindSkill("basic_move") : null;
                if (moveSkill == null)
                {
                    Debug.LogWarning($"[CombatRoundManager] {GetDisplayName(player.gameObject)} has no basic_move skill.");
                    executor?.ClearActiveSkill();
                }
                else
                {
                    executor.ActivateSkill(moveSkill);
                }
            }

            FocusCameraOnUnit(player.gameObject);
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
                if (player != null && TrySelectPlayer(player)) return;
            }

            var selectedPlayer = SelectedPlayer;
            if (selectedPlayer == null || HasEndedRound(selectedPlayer.gameObject)) return;

            var executor = selectedPlayer.GetComponent<SkillExecutor>();
            executor?.HandleInput(request);
        }

        public bool TryEndSelectedPlayerRound()
        {
            if (SelectedPlayer == null || !TryEndUnitRound(SelectedPlayer.gameObject))
                return false;
            AdvanceTurn();
            return true;
        }

        public bool TryEndUnitRound(GameObject unitGO)
        {
            if (unitGO == null) return false;

            var movement = unitGO.GetComponent<MovementController>();
            if (movement != null && movement.IsMoving) return false;

            m_hasEndedRound.Add(unitGO);

            var attr = unitGO.GetComponent<AttributeSet>();
            attr?.Set(WellKnownAttributeTags.AP, 0f);

            movement?.ResetUnpaidDistance();
            return true;
        }

        public EnemyController GetEnemyUnderAttack(Player1Controller attacker)
        {
            EnemyController best = null;
            float bestDist = float.MaxValue;
            foreach (var enemy in m_enemyUnits)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                float dist = Vector3.Distance(attacker.transform.position, enemy.transform.position);
                if (dist < bestDist) { bestDist = dist; best = enemy; }
            }
            return best;
        }

        private void CacheUnits()
        {
            m_playerUnits.RemoveAll(p => p == null);
            if (m_playerUnits.Count == 0)
                m_playerUnits.AddRange(FindObjectsOfType<Player1Controller>());

            m_enemyUnits.Clear();
            m_enemyUnits.AddRange(FindObjectsOfType<EnemyController>());
        }

        private void BuildTurnOrder()
        {
            m_turnOrder.Clear();

            var allGOs = new List<GameObject>();
            allGOs.AddRange(m_playerUnits.Where(p => p != null && p.gameObject.activeInHierarchy).Take(m_maxPartySize).Select(p => p.gameObject));
            allGOs.AddRange(m_enemyUnits.Where(e => e != null && e.gameObject.activeInHierarchy).Select(e => e.gameObject));

            m_turnOrder.AddRange(allGOs
                .OrderBy(go => m_enemyFirstForDebug && go.GetComponent<EnemyController>() != null ? 0 : 1)
                .ThenByDescending(go => GetInitiative(go))
                .ThenBy(go => go.GetComponent<Player1Controller>()?.PartySlot ?? 99));
        }

        private void StartNextRound()
        {
            RoundCount++;
            CacheUnits();
            BuildTurnOrder();

            foreach (var go in m_turnOrder)
            {
                var attr = go.GetComponent<AttributeSet>();
                if (attr != null && attr.IsAlive)
                {
                    attr.SetToMax(WellKnownAttributeTags.AP);
                    go.GetComponent<MovementController>()?.ResetUnpaidDistance();
                    go.GetComponent<SkillExecutor>()?.AdvanceCooldowns();
                }
            }

            m_hasEndedRound.Clear();
            RoundChanged?.Invoke();
            RefreshControllableBlock();
            AdvanceTurn();
        }

        private IEnumerator EnemyTurnCoroutine(EnemyController enemy)
        {
            IsWaiting = true;
            SelectedUnit = enemy.gameObject;
            FocusCameraOnUnit(enemy.gameObject);
            enemy.FlashTurn();
            Debug.Log($"[Combat] Enemy turn starts: {GetDisplayName(enemy.gameObject)}");

            yield return m_enemyTurnRunner.RunTurn(enemy, m_playerUnits, m_enemyUnits, BasicAttackSkill);

            TryEndUnitRound(enemy.gameObject);
            m_turnOrder.Remove(enemy.gameObject);
            SelectedUnit = null;
            IsWaiting = false;

            AdvanceTurn();
        }

        private void RefreshControllableBlock()
        {
            m_controllableBlock.Clear();
            foreach (var go in m_turnOrder)
            {
                if (go == null || !IsAlive(go)) continue;
                if (HasEndedRound(go)) continue;

                var player = go.GetComponent<Player1Controller>();
                if (player != null)
                    m_controllableBlock.Add(player);
                else
                    break;
            }
        }

        private void AdvanceTurn()
        {
            while (m_turnOrder.Count > 0)
            {
                var front = m_turnOrder[0];
                if (front == null || !IsAlive(front))
                {
                    m_turnOrder.RemoveAt(0);
                    continue;
                }
                if (HasEndedRound(front))
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
            var player = next.GetComponent<Player1Controller>();

            if (player != null)
            {
                RefreshControllableBlock();
                var nextPlayer = m_controllableBlock.FirstOrDefault(p => !HasEndedRound(p.gameObject));
                if (!TrySelectPlayer(nextPlayer))
                {
                    foreach (var p in m_controllableBlock)
                    {
                        if (!HasEndedRound(p.gameObject))
                            TryEndUnitRound(p.gameObject);
                    }
                    m_turnOrder.RemoveAll(go => go.GetComponent<Player1Controller>() != null && m_controllableBlock.Any(p => p.gameObject == go));
                    AdvanceTurn();
                }
            }
            else if (next.GetComponent<EnemyController>() is EnemyController enemy)
            {
                StartCoroutine(EnemyTurnCoroutine(enemy));
            }
        }

        private void FocusCameraOnUnit(GameObject unitGO)
        {
            if (m_cameraController == null || unitGO == null) return;
            m_cameraController.FocusOn(unitGO.transform);
        }

        // ── Helpers ─────────────────────────────────────────────────

        private static bool IsAlive(GameObject go)
        {
            var attr = go.GetComponent<AttributeSet>();
            return attr != null && attr.IsAlive;
        }

        private static float GetInitiative(GameObject go)
        {
            var attr = go.GetComponent<AttributeSet>();
            return attr != null ? attr.Get(WellKnownAttributeTags.Initiative) : 0f;
        }

        private static string GetDisplayName(GameObject go)
        {
            var attr = go.GetComponent<AttributeSet>();
            return attr != null ? attr.DisplayName : go.name;
        }
    }
}
