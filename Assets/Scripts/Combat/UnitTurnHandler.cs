using System.Linq;
using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Handles player-side turn logic. Subscribes to CombatRoundManager.UnitTurnStarted,
    /// filters for Control.Human units, and manages unit selection, camera, input, and
    /// movement input handling (previously in GroundMoveAbility.HandleInput).
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
                    if (!tagComp.HasTag(new GameplayTag("Control.Human"), ETagMatchMode.Exact)) continue;
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
            if (tagComp == null || !tagComp.HasTag(new GameplayTag("Control.Human"), ETagMatchMode.Exact))
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
                if (tagComp == null || !tagComp.HasTag(new GameplayTag("Control.Human"), ETagMatchMode.Exact))
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

        // ── Input handling (moved from GroundMoveAbility.HandleInput) ──

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
                if (tagComp != null && tagComp.HasTag(new GameplayTag("Control.Human"), ETagMatchMode.Exact))
                {
                    TrySelectUnit(request.TargetObject);
                    return;
                }
            }

            // Route ground-point input to movement handling
            var executor = m_selectedUnit.GetComponent<AbilitySystemComponent>();
            var activeSkill = executor?.ActiveSkill;

            if (activeSkill != null && activeSkill is GroundMoveAbility)
            {
                HandleMoveInput(executor, activeSkill, request);
                return;
            }

            // Fallback: route to executor for other skill types
            executor?.HandleInputLegacy(request, activeSkill);
        }

        private void HandleMoveInput(AbilitySystemComponent executor, SkillAbility moveSkill, SkillInputRequest request)
        {
            if (!request.IsTarget(SkillInputTag.k_TargetGround) || !request.HasWorldPosition)
            {
                PathPreview.Instance?.Clear();
                return;
            }

            if (request.IsSignal(SkillInputTag.k_PointerHover))
            {
                ShowMovePreview(executor, request.WorldPosition);
                return;
            }

            if (!request.IsSignal(SkillInputTag.k_PrimaryPressed))
                return;

            if (!TryBuildMovePath(executor, request.WorldPosition, out Vector3 destination, out NavMeshPath path))
                return;

            var context = SkillExecutionContext.ForGroundPoint(executor, moveSkill, destination, path);
            var result = executor.Execute(context);
            if (result.IsSuccess)
            {
                PathPreview.Instance?.Clear();
                PathPreview.Instance?.ShowActivePath(path.corners);
            }
        }

        private static void ShowMovePreview(AbilitySystemComponent executor, Vector3 worldPosition)
        {
            var preview = PathPreview.Instance;
            if (preview == null) return;

            var movement = executor.Movement;
            if (movement == null || !TryGetOrigin(executor, out Vector3 origin))
            {
                preview.Clear();
                return;
            }

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit nav,
                    NavMeshService.Instance.MouseSnapRadius, NavMesh.AllAreas))
            {
                preview.Show(System.Array.Empty<Vector3>(), new[] { origin, worldPosition });
                return;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length < 2)
            {
                Vector3[] invalidPath = path.corners != null && path.corners.Length >= 2
                    ? path.corners
                    : new[] { origin, nav.position };
                preview.Show(System.Array.Empty<Vector3>(), invalidPath);
                return;
            }

            float length = NavMeshService.PathLength(path.corners);
            if (length <= movement.RemainingMoveDistance)
            {
                preview.Show(path.corners, System.Array.Empty<Vector3>());
                return;
            }

            NavMeshService.ClipPath(path.corners, movement.RemainingMoveDistance,
                out Vector3[] head, out Vector3[] tail);
            preview.Show(head, tail);
        }

        private static bool TryBuildMovePath(AbilitySystemComponent executor, Vector3 worldPosition,
            out Vector3 destination, out NavMeshPath path)
        {
            destination = default;
            path = null;

            var move = executor.Movement;
            if (move == null || move.RemainingMoveDistance <= 0f) return false;
            float remainingDist = move.RemainingMoveDistance;
            if (!TryGetOrigin(executor, out Vector3 origin)) return false;

            if (!NavMesh.SamplePosition(worldPosition, out NavMeshHit nav,
                    NavMeshService.Instance.MouseSnapRadius, NavMesh.AllAreas))
                return false;

            var fullPath = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, nav.position, NavMesh.AllAreas, fullPath)
                || fullPath.status != NavMeshPathStatus.PathComplete
                || fullPath.corners == null
                || fullPath.corners.Length < 2)
                return false;

            float length = NavMeshService.PathLength(fullPath.corners);
            destination = nav.position;

            if (length > remainingDist + 0.001f)
            {
                NavMeshService.ClipPath(fullPath.corners, remainingDist,
                    out Vector3[] head, out _);
                if (head == null || head.Length < 2) return false;
                destination = head[head.Length - 1];
            }

            path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, destination, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete
                || path.corners == null
                || path.corners.Length < 2)
                return false;

            return true;
        }

        private static bool TryGetOrigin(AbilitySystemComponent executor, out Vector3 origin)
        {
            var casterObject = executor.gameObject;
            if (casterObject != null && NavMesh.SamplePosition(casterObject.transform.position,
                    out NavMeshHit hit, NavMeshService.Instance.OriginSnapRadius, NavMesh.AllAreas))
            {
                origin = hit.position;
                return true;
            }

            origin = casterObject != null ? casterObject.transform.position : Vector3.zero;
            return casterObject != null;
        }
    }
}
