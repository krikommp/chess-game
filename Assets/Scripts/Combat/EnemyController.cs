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

        private void Awake()
        {
            currentHP = maxHP;
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
    }
}
