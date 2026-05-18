using System.Linq;
using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using MiniChess.GameplayTags.Generated;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Handles player-side turn logic. Subscribes to CombatRoundManager.UnitTurnStarted,
    /// filters for Control.Human units, and routes input to the selected unit's ASC.
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

        public GameObject SelectedUnit => m_selectedUnit;

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

            TrySelectDefaultPlayerUnit();
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
            if (!IsSelectableHumanUnit(unit))
                return;

            TrySelectUnit(unit);
        }

        public bool TrySelectDefaultPlayerUnit()
        {
            if (m_selectedUnit != null)
                return true;

            if (m_roundManager != null && m_roundManager.ControllableUnits.Count > 0)
                return TrySelectUnit(m_roundManager.ControllableUnits[0]);

            foreach (var cu in FindObjectsOfType<CombatUnit>())
            {
                if (IsSelectableHumanUnit(cu != null ? cu.gameObject : null))
                {
                    SelectUnit(cu.gameObject);
                    return true;
                }
            }

            return false;
        }

        public void HandleInputRequest(SkillInputRequest request)
        {
            OnInputReceived(request);
        }

        private bool TrySelectUnit(GameObject unit)
        {
            if (unit == null) return false;

            if (m_roundManager != null)
            {
                if (!m_roundManager.ControllableUnits.Contains(unit)) return false;
                if (m_roundManager.HasEndedRound(unit)) return false;
            }
            else if (!IsSelectableHumanUnit(unit))
            {
                return false;
            }

            SelectUnit(unit);
            return true;
        }

        private void SelectUnit(GameObject unit)
        {
            m_selectedUnit = unit;

            if (m_cameraController != null)
                m_cameraController.FocusOn(unit.transform);

            var executor = unit.GetComponent<AbilitySystemComponent>();
            var moveSkill = executor != null ? executor.FindAbility("basic_move") : null;
            if (moveSkill != null)
                executor.ActivateAbility(moveSkill);
        }

        private void OnInputReceived(SkillInputRequest request)
        {
            if (m_selectedUnit == null) return;
            if (m_roundManager != null)
            {
                if (m_roundManager.IsWaiting) return;
                if (m_roundManager.HasEndedRound(m_selectedUnit)) return;
            }

            if (request.IsSignal(SkillInputTag.k_PrimaryPressed)
                && request.IsTarget(SkillInputTag.k_TargetPlayer)
                && request.TargetObject != null)
            {
                if (IsSelectableHumanUnit(request.TargetObject))
                {
                    TrySelectUnit(request.TargetObject);
                    return;
                }
            }

            var executor = m_selectedUnit.GetComponent<AbilitySystemComponent>();
            executor?.HandleInput(request);
        }

        private static bool IsSelectableHumanUnit(GameObject unit)
        {
            if (unit == null || !unit.activeInHierarchy) return false;

            var tagComp = unit.GetComponent<GameplayTagComponent>();
            if (tagComp == null || !tagComp.HasTag(GameplayTagConstants.Control.Human, ETagMatchMode.Exact))
                return false;

            var attr = unit.GetComponent<AttributeSet>();
            return attr != null && attr.IsAlive;
        }
    }
}
