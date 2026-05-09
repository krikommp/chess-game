using UnityEngine;

namespace MiniChess.Combat
{
    public interface ICombatUnit
    {
        Faction Faction { get; }
        string DisplayName { get; }
        int Initiative { get; }
        int MaxAP { get; }
        int CurrentAP { get; }
        int MaxHP { get; }
        int CurrentHP { get; }
        bool IsAlive { get; }
        bool IsMoving { get; }
        bool HasEndedRound { get; }
        float MoveSpeedMetersPerAp { get; }

        void BeginRound();
        bool TryEndRound();
        void TakeDamage(int damage);
        void SetVisualState(PlayerVisualState state);
    }
}
