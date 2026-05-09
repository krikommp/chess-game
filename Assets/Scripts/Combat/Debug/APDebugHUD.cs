using UnityEngine;

namespace MiniChess.Combat.DebugUI
{
    /// <summary>OnGUI overlay showing AP and current move preview cost.</summary>
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
            if (input != null && input.HasPreview)
            {
                costLine = input.PreviewReachable
                    ? $"Cost: {input.PreviewApCost} AP"
                    : $"Cost: {input.PreviewApCost} AP (UNREACHABLE)";
            }

            GUI.Box(new Rect(10, 10, 330, 180), GUIContent.none);
            GUI.Label(new Rect(20, 14, 220, 24),
                $"Round: {(combatManager != null ? combatManager.RoundCount : 1)}", _style);
            GUI.Label(new Rect(20, 38, 220, 24), costLine, _style);
            GUI.Label(new Rect(20, 62, 220, 24),
                activePlayer.IsMoving ? "Moving..." : "Space = end selected player", _smallStyle);

            GUI.Label(new Rect(20, 88, 300, 22),
                $"Selected: {activePlayer.DisplayName} | Init {activePlayer.Initiative} | AP {activePlayer.CurrentAP}/{activePlayer.MaxAP}",
                _smallStyle);

            if (combatManager == null) return;

            for (int i = 0; i < combatManager.TurnOrder.Count && i < 4; i++)
            {
                Player1Controller unit = combatManager.TurnOrder[i];
                string marker = unit == activePlayer ? ">" : " ";
                string done = unit.HasEndedRound ? "DONE" : $"{unit.CurrentAP}/{unit.MaxAP} AP";
                GUI.Label(new Rect(20, 112 + i * 18, 300, 18),
                    $"{marker} {i + 1}. {unit.DisplayName} Init {unit.Initiative} {done}",
                    _smallStyle);
            }
        }
    }
}
