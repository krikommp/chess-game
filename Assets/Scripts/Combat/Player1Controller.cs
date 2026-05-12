using System;
using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
    public enum EPlayerVisualState { Default, Hovered, Selected }
    public enum EFaction { Player, Enemy }

    [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
    public class Player1Controller : MonoBehaviour
    {
        public event Action MovementStarted;
        public event Action StateChanged;

        [Header("Identity")]
        [SerializeField] private int m_partySlot = 1;

        [Header("Debug")]
        [SerializeField] private bool m_allowDebugRefill = true;
        [SerializeField] private KeyCode m_refillKey = KeyCode.R;

        [Header("Visuals")]
        [SerializeField] private Color m_defaultColor = new Color(0.4f, 0.4f, 0.5f);
        [SerializeField] private Color m_hoveredColor = new Color(0.9f, 0.8f, 0.3f);
        [SerializeField] private Color m_selectedColor = new Color(0.3f, 0.8f, 0.4f);

        private AttributeSet m_attributes;
        private MovementController m_movement;
        private MeshRenderer m_meshRenderer;
        private Material m_materialInstance;

        public int PartySlot => m_partySlot;

        // ── Convenience pass-throughs ───────────────────────────────

        public string DisplayName => m_attributes != null ? m_attributes.DisplayName : gameObject.name;
        public bool IsAlive => m_attributes != null && m_attributes.IsAlive;
        public bool IsMoving => m_movement != null && m_movement.IsMoving;
        public int CurrentAP => m_attributes != null ? (int)m_attributes.Get(WellKnownAttributeTags.AP) : 0;
        public int MaxAP => m_attributes != null ? (int)m_attributes.GetMax(WellKnownAttributeTags.AP) : 0;
        public int CurrentHP => m_attributes != null ? (int)m_attributes.Get(WellKnownAttributeTags.HP) : 0;
        public int MaxHP => m_attributes != null ? (int)m_attributes.GetMax(WellKnownAttributeTags.HP) : 0;
        public int Initiative => m_attributes != null ? (int)m_attributes.Get(WellKnownAttributeTags.Initiative) : 0;
        public float MoveSpeedMetersPerAp => m_attributes != null ? m_attributes.Get(WellKnownAttributeTags.MoveSpeed) : 2f;
        public float RemainingMoveDistance => m_movement != null ? m_movement.RemainingMoveDistance : 0f;

        // ── Unity lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            m_attributes = GetComponent<AttributeSet>();
            m_movement = GetComponent<MovementController>();

            WireEvents();

            m_meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (m_meshRenderer != null)
            {
                m_materialInstance = m_meshRenderer.material;
                ApplyColor(m_defaultColor);
            }
        }

        private void Update()
        {
            if (m_allowDebugRefill && Input.GetKeyDown(m_refillKey))
            {
                m_attributes?.SetToMax(WellKnownAttributeTags.AP);
                StateChanged?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (m_attributes != null)
                m_attributes.AttributeDepleted -= OnAttributeDepleted;
        }

        // ── Public methods ──────────────────────────────────────────

        public void BeginRound()
        {
            m_attributes?.SetToMax(WellKnownAttributeTags.AP);
            GetComponent<SkillExecutor>()?.AdvanceCooldowns();
            StateChanged?.Invoke();
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;
            m_attributes?.Set(WellKnownAttributeTags.AP, 0f);
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySpendAP(int amount)
        {
            return m_attributes != null && m_attributes.TrySpend(WellKnownAttributeTags.AP, amount);
        }

        public bool TryStartMove(UnityEngine.AI.NavMeshPath path)
        {
            return m_movement != null && m_movement.TryStartMove(path);
        }

        public bool TryMove(UnityEngine.AI.NavMeshPath path, int apCost)
        {
            return m_movement != null && m_movement.TryMove(path);
        }

        public void StopMovement()
        {
            m_movement?.StopMovement();
        }

        public int PreviewMovementApCost(float pathLength)
        {
            return NavMeshService.EstimateMoveApCost(pathLength,
                m_attributes?.Get(WellKnownAttributeTags.MoveSpeed) ?? 1f,
                Mathf.FloorToInt(m_attributes?.Get(WellKnownAttributeTags.AP) ?? 0f));
        }

        public void TakeDamage(int damage)
        {
            m_attributes?.Modify(WellKnownAttributeTags.HP, -damage);
            StateChanged?.Invoke();
        }

        public void Heal(int amount)
        {
            m_attributes?.Modify(WellKnownAttributeTags.HP, amount);
            StateChanged?.Invoke();
        }

        public void SetVisualState(EPlayerVisualState state)
        {
            switch (state)
            {
                case EPlayerVisualState.Default: ApplyColor(m_defaultColor); break;
                case EPlayerVisualState.Hovered: ApplyColor(m_hoveredColor); break;
                case EPlayerVisualState.Selected: ApplyColor(m_selectedColor); break;
            }
        }

        // ── Private helpers ─────────────────────────────────────────

        private void WireEvents()
        {
            if (m_movement != null)
            {
                m_movement.MovementStarted += () => { MovementStarted?.Invoke(); StateChanged?.Invoke(); };
                m_movement.MovementStopped += () => StateChanged?.Invoke();
            }

            if (m_attributes != null)
            {
                m_attributes.AttributeChanged += (tag, prev, cur) => StateChanged?.Invoke();
                m_attributes.AttributeDepleted += OnAttributeDepleted;
            }
        }

        private void OnAttributeDepleted(GameplayTags.GameplayTag tag) { }

        private void ApplyColor(Color color)
        {
            if (m_materialInstance != null) m_materialInstance.color = color;
        }
    }
}
