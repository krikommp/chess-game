using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    public interface ICombatUnit
    {
        EFaction Faction { get; }
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
        float RemainingMoveDistance { get; }

        void BeginRound();
        bool TryEndRound();
        bool TrySpendAP(int amount);
        bool TryStartMove(NavMeshPath path);
        void TakeDamage(int damage);
        void Heal(int amount);
        void SetVisualState(EPlayerVisualState state);
    }
}


