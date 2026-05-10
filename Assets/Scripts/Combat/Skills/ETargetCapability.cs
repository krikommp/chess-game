using System;

namespace MiniChess.Combat.Skills
{
    [Flags]
    public enum ETargetCapability
    {
        None = 0,
        Damageable = 1 << 0,
        Healable = 1 << 1,
        Statusable = 1 << 2,
        Interactable = 1 << 3,
        Destructible = 1 << 4,
    }
}
