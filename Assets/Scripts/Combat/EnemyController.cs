using System.Collections;
using MiniChess.Combat.AI;
using MiniChess.Combat.Skills;
using UnityEngine;

namespace MiniChess.Combat
{
    [RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
    public class EnemyController : MonoBehaviour
    {
        [Header("AI")]
        [Tooltip("AI behavior profile. If null, defaults to Aggressive.")]
        [SerializeField] private AIProfile m_aiProfile;

        [Header("Visuals")]
        [SerializeField] private Color m_defaultColor = new Color(0.7f, 0.2f, 0.2f);
        [SerializeField] private Color m_actingColor = new Color(1f, 0.5f, 0f);

        private AttributeSet m_attributes;
        private MovementController m_movement;
        private MeshRenderer m_meshRenderer;
        private Material m_materialInstance;

        public AIProfile AIProfile => m_aiProfile;
        public Color DefaultColor { get => m_defaultColor; set => m_defaultColor = value; }

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

            if (m_attributes != null)
                m_attributes.AttributeDepleted += OnAttributeDepleted;

            m_meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void Start()
        {
            if (m_meshRenderer != null)
            {
                m_materialInstance = m_meshRenderer.material;
                ApplyColor(m_defaultColor);
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
            m_movement?.ResetUnpaidDistance();
            GetComponent<SkillExecutor>()?.AdvanceCooldowns();
        }

        public bool TryEndRound()
        {
            if (IsMoving) return false;
            m_attributes?.Set(WellKnownAttributeTags.AP, 0f);
            m_movement?.ResetUnpaidDistance();
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

        public bool TryMove(UnityEngine.AI.NavMeshPath path)
        {
            return m_movement != null && m_movement.TryMove(path);
        }

        public void StopMovement()
        {
            m_movement?.StopMovement();
        }

        public int PreviewMovementApCost(float pathLength)
        {
            return m_movement != null ? m_movement.PreviewMovementApCost(pathLength) : 0;
        }

        public void TakeDamage(int damage)
        {
            m_attributes?.Modify(WellKnownAttributeTags.HP, -damage);
        }

        public void Heal(int amount)
        {
            m_attributes?.Modify(WellKnownAttributeTags.HP, amount);
        }

        public void FlashTurn()
        {
            StartCoroutine(FlashCoroutine());
        }

        public void SetVisualState(EPlayerVisualState state) { }

        // ── Private helpers ─────────────────────────────────────────

        private void OnAttributeDepleted(GameplayTags.GameplayTag tag)
        {
            if (tag == WellKnownAttributeTags.HP)
                Destroy(gameObject);
        }

        private IEnumerator FlashCoroutine()
        {
            ApplyColor(m_actingColor);
            yield return new WaitForSeconds(0.5f);
            ApplyColor(m_defaultColor);
        }

        private void ApplyColor(Color color)
        {
            if (m_materialInstance != null) m_materialInstance.color = color;
        }
    }
}
