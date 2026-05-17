using System.Linq;
using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using MiniChess.GameplayTags.Generated;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Handles player-side turn logic. Subscribes to CombatRoundManager.UnitTurnStarted,
    /// filters for Control.Human units, and manages unit selection, camera, input, and
    /// input routing to the selected unit's AbilitySystemComponent.
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

        private void Start()
        {
            if (NavMeshService.Instance == null)
            {
                var go = new GameObject("[NavMeshService]");
                go.AddComponent<NavMeshService>();
            }

            if (m_roundManager == null)
            {
                // Bypass round controller: auto-select first human-controlled unit
                foreach (var cu in FindObjectsOfType<CombatUnit>())
                {
                    if (!cu.gameObject.activeInHierarchy) continue;
                    var tagComp = cu.GetComponent<GameplayTagComponent>();
                    if (tagComp == null) continue;
                    if (!tagComp.HasTag(GameplayTagConstants.Control.Human, ETagMatchMode.Exact)) continue;
                    var attr = cu.GetComponent<AttributeSet>();
                    if (attr == null || !attr.IsAlive) continue;

                    SelectUnit(cu.gameObject);
                    break;
                }
            }
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
            if (m_selectedUnit == null) return;
            if (m_roundManager == null) return;

            if (m_roundManager.IsWaiting) return;
            if (m_roundManager.HasEndedRound(m_selectedUnit)) return;

            if (Input.GetKeyDown(m_endTurnKey))
                m_roundManager.EndTurn(m_selectedUnit);

            var controllable = m_roundManager.ControllableUnits;
            for (int i = 0; i < controllable.Count && i < 4; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                    TrySelectUnit(controllable[i]);
            }
        }

        private void OnUnitTurnStarted(GameObject unit)
        {
            if (unit == null) return;

            // Check Control.Human tag instead of component type
            var tagComp = unit.GetComponent<GameplayTagComponent>();
            if (tagComp == null || !tagComp.HasTag(GameplayTagConstants.Control.Human, ETagMatchMode.Exact))
                return;

            TrySelectUnit(unit);
        }

        private bool TrySelectUnit(GameObject unit)
        {
            if (unit == null) return false;

            if (m_roundManager != null)
            {
                if (!m_roundManager.ControllableUnits.Contains(unit)) return false;
                if (m_roundManager.HasEndedRound(unit)) return false;
            }
            else
            {
                var tagComp = unit.GetComponent<GameplayTagComponent>();
                if (tagComp == null || !tagComp.HasTag(GameplayTagConstants.Control.Human, ETagMatchMode.Exact))
                    return false;
                var attr = unit.GetComponent<AttributeSet>();
                if (attr == null || !attr.IsAlive) return false;
            }

            SelectUnit(unit);
            return true;
        }

        private void SelectUnit(GameObject unit)
        {
            m_selectedUnit = unit;

            // Camera
            if (m_cameraController != null)
                m_cameraController.FocusOn(unit.transform);

            // Activate basic_move
            var executor = unit.GetComponent<AbilitySystemComponent>();
            var moveSkill = executor != null ? executor.FindSkill("basic_move") : null;
            if (moveSkill != null)
                executor.ActivateSkill(moveSkill);
        }

        // ── Input routing ─────────────────────────────────────────────

        private void OnInputReceived(SkillInputRequest request)
        {
            if (m_selectedUnit == null) return;
            if (m_roundManager != null)
            {
                if (m_roundManager.IsWaiting) return;
                if (m_roundManager.HasEndedRound(m_selectedUnit)) return;
            }

            // Number key / click to switch unit
            if (request.IsSignal(SkillInputTag.k_PrimaryPressed)
                && request.IsTarget(SkillInputTag.k_TargetPlayer)
                && request.TargetObject != null)
            {
                // Check Control.Human tag
                var tagComp = request.TargetObject.GetComponent<GameplayTagComponent>();
                if (tagComp != null && tagComp.HasTag(GameplayTagConstants.Control.Human, ETagMatchMode.Exact))
                {
                    TrySelectUnit(request.TargetObject);
                    return;
                }
            }

            var executor = m_selectedUnit.GetComponent<AbilitySystemComponent>();
            executor?.HandleInput(request);
        }
    }
}
