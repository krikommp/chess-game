using System.Collections;
using UnityEngine;

namespace MiniChess.Combat
{
    public class EnemyController : MonoBehaviour, ICombatUnit
    {
        [Header("Identity")]
        public string displayName = "Enemy";

        [Header("HP")]
        public int maxHP = 100;
        public int currentHP = 100;

        [Header("Initiative")]
        public int initiative = 5;

        [Header("Visuals")]
        public Color defaultColor = new Color(0.7f, 0.2f, 0.2f);
        public Color actingColor = new Color(1f, 0.5f, 0f);

        // ICombatUnit implementation
        public Faction Faction => Faction.Enemy;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public int Initiative => initiative;
        public int MaxAP => 0;
        public int CurrentAP => 0;
        public int MaxHP => maxHP;
        public int CurrentHP { get => currentHP; set => currentHP = Mathf.Clamp(value, 0, maxHP); }
        public bool IsAlive => currentHP > 0;
        public bool IsMoving => false;
        public bool HasEndedRound { get; private set; }
        public float MoveSpeedMetersPerAp => 0f;

        private MeshRenderer _meshRenderer;
        private Material _materialInstance;

        private void Awake()
        {
            currentHP = maxHP;
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void Start()
        {
            if (_meshRenderer != null)
            {
                _materialInstance = _meshRenderer.material;
                ApplyColor(defaultColor);
            }
        }

        public void BeginRound()
        {
            HasEndedRound = false;
        }

        public bool TryEndRound()
        {
            HasEndedRound = true;
            return true;
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive) return;
            currentHP = Mathf.Max(0, currentHP - damage);

            if (!IsAlive)
            {
                Destroy(gameObject);
            }
        }

        public void SetVisualState(PlayerVisualState state) { }

        public void FlashTurn()
        {
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            ApplyColor(actingColor);
            yield return new WaitForSeconds(0.5f);
            ApplyColor(defaultColor);
        }

        private void ApplyColor(Color color)
        {
            if (_materialInstance != null) _materialInstance.color = color;
        }
    }
}
