using System;
using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Handles player-side turn logic. Subscribes to CombatRoundManager.UnitTurnStarted,
    /// filters for Faction.Player, and manages unit selection, camera, and input.
    /// </summary>
    public class UnitTurnHandler : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CombatRoundManager m_roundManager;
        [SerializeField] private InputController m_inputController;
        [SerializeField] private CameraController m_cameraController;

        [Header("Controls")]
        [SerializeField] private KeyCode m_endTurnKey = KeyCode.Space;

        private GameObject m_selectedUnit;

        private void Awake()
        {
            if (m_inputController == null) m_inputController = FindObjectOfType<InputController>();
            if (m_cameraController == null) m_cameraController = FindObjectOfType<CameraController>();
        }

        private void OnEnable()
        {
            if (m_roundManager != null)
                m_roundManager.UnitTurnStarted += OnUnitTurnStarted;
            if (m_inputController != null)
                m_inputController.InputReceived += OnInputReceived;
        }

        private void OnDisable()
        {
            if (m_roundManager != null)
                m_roundManager.UnitTurnStarted -= OnUnitTurnStarted;
            if (m_inputController != null)
                m_inputController.InputReceived -= OnInputReceived;
        }

        private void Update()
        {
            if (m_roundManager == null || m_selectedUnit == null || m_roundManager.IsWaiting) return;
            if (m_roundManager.HasEndedRound(m_selectedUnit)) return;

            if (Input.GetKeyDown(m_endTurnKey))
                m_roundManager.EndTurn(m_selectedUnit);

            var controllable = m_roundManager.ControllableUnits;
            for (int i = 0; i < controllable.Count && i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    SelectUnit(controllable[i]);
            }
        }

        private void OnUnitTurnStarted(GameObject unit)
        {
            if (unit == null) return;

            var attr = unit.GetComponent<AttributeSet>();
            if (attr == null || attr.Faction != EFaction.Player) return;

            SelectUnit(unit);
        }

        private void SelectUnit(GameObject unit)
        {
            // Deselect previous
            if (m_selectedUnit != null && m_selectedUnit != unit)
            {
                var prevPlayer = m_selectedUnit.GetComponent<Player1Controller>();
                if (prevPlayer != null)
                    prevPlayer.SetVisualState(EPlayerVisualState.Default);
            }

            m_selectedUnit = unit;

            // Camera
            if (m_cameraController != null)
                m_cameraController.FocusOn(unit.transform);

            // Visual
            var player = unit.GetComponent<Player1Controller>();
            if (player != null)
                player.SetVisualState(EPlayerVisualState.Selected);

            // Activate basic_move
            var executor = unit.GetComponent<SkillExecutor>();
            var moveSkill = executor != null ? executor.FindSkill("basic_move") : null;
            if (moveSkill != null)
                executor.ActivateSkill(moveSkill);
        }

        private void OnInputReceived(SkillInputRequest request)
        {
            if (m_roundManager == null || m_roundManager.IsWaiting) return;
            if (m_selectedUnit == null || m_roundManager.HasEndedRound(m_selectedUnit)) return;

            // Number key to switch unit
            if (request.IsSignal(SkillInputTag.k_PrimaryPressed)
                && request.IsTarget(SkillInputTag.k_TargetPlayer)
                && request.TargetObject != null)
            {
                var player = request.TargetObject.GetComponent<Player1Controller>();
                if (player != null)
                {
                    SelectUnit(player.gameObject);
                    return;
                }
            }

            // Route to active skill
            var executor = m_selectedUnit.GetComponent<SkillExecutor>();
            executor?.HandleInput(request);
        }
    }
}
