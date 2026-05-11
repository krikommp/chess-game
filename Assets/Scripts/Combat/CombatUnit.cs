using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Marker component. Attach to any GameObject that participates in combat.
    /// CombatRoundManager finds units via FindObjectsOfType&lt;CombatUnit&gt; instead
    /// of depending on Player1Controller / EnemyController types.
    /// </summary>
    public class CombatUnit : MonoBehaviour { }
}
