using UnityEngine;

namespace MiniChess.Combat.DebugUI
{
    /// <summary>OnGUI overlay showing round, AP, HP, turn order, and attack preview.</summary>
    public class APDebugHUD : MonoBehaviour
    {
        public Player1Controller player;
        public MoveInputController input;
        public CombatRoundManager combatManager;

        private GUIStyle _style;
        private GUIStyle _smallStyle;

        private void Awake()
        {
            if (combatManager == null) combatManager = FindObjectOfType<CombatRoundManager>();
        }

        private void OnGUI()
        {
            Player1Controller activePlayer = combatManager != null && combatManager.SelectedPlayer != null
                ? combatManager.SelectedPlayer
                : player;
            if (activePlayer == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    normal = { textColor = Color.white }
                };
                _smallStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
            }

            string costLine = "Cost: -";
            if (input != null && input.IsAttackMode)
            {
                costLine = input.PreviewReachable
                    ? $"Atk {input.AttackTargetName}: 20 dmg ({input.PreviewApCost} AP)"
                    : $"Atk {input.AttackTargetName}: 20 dmg ({input.PreviewApCost} AP, UNREACHABLE)";
            }
            else if (input != null && input.HasPreview)
            {
                costLine = input.PreviewReachable
                    ? $"Cost: {input.PreviewApCost} AP"
                    : $"Cost: {input.PreviewApCost} AP (UNREACHABLE)";
            }

            GUI.Box(new Rect(10, 10, 380, 200), GUIContent.none);
            GUI.Label(new Rect(20, 14, 240, 24),
                $"Round: {(combatManager != null ? combatManager.RoundCount : 1)}", _style);
            GUI.Label(new Rect(20, 38, 360, 24), costLine, _style);
            GUI.Label(new Rect(20, 62, 360, 24),
                activePlayer.IsMoving ? "Moving..." : "Space = end selected player", _smallStyle);

            GUI.Label(new Rect(20, 88, 360, 22),
                $"Selected: {activePlayer.DisplayName} | HP {activePlayer.CurrentHP}/{activePlayer.MaxHP} | AP {activePlayer.CurrentAP}/{activePlayer.MaxAP}",
                _smallStyle);

            if (combatManager == null) return;

            for (int i = 0; i < combatManager.TurnOrder.Count && i < 4; i++)
            {
                ICombatUnit unit = combatManager.TurnOrder[i];
                if (unit == null || !unit.IsAlive) continue;

                string marker = unit == activePlayer ? ">" : " ";
                string typeTag = unit.Faction == Faction.Enemy ? "[E]" : "[P]";
                string state = unit.HasEndedRound ? "DONE" : $"{unit.CurrentAP}/{unit.MaxAP} AP";
                string hpStr = $"HP {unit.CurrentHP}/{unit.MaxHP}";
                GUI.Label(new Rect(20, 112 + i * 18, 360, 18),
                    $"{marker} {i + 1}. {typeTag} {unit.DisplayName} {hpStr} {state}",
                    _smallStyle);
            }
        }
    }
}
