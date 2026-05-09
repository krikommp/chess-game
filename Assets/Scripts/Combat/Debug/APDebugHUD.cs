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
        private GUIStyle _enemyStyle;

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
                _enemyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(1f, 0.6f, 0.4f) }
                };
            }

            string waitingLine = "";
            if (combatManager != null && combatManager.IsWaiting && combatManager.SelectedUnit != null)
            {
                waitingLine = $" >>> {combatManager.SelectedUnit.DisplayName} acting... <<<";
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

            int turnCount = combatManager != null ? combatManager.TurnOrder.Count : 0;
            float boxHeight = 112 + turnCount * 20 + 10;
            GUI.Box(new Rect(10, 10, 400, boxHeight), GUIContent.none);

            GUI.Label(new Rect(20, 14, 300, 24),
                $"Round: {(combatManager != null ? combatManager.RoundCount : 1)}{waitingLine}", _style);
            GUI.Label(new Rect(20, 38, 380, 24), costLine, _style);
            GUI.Label(new Rect(20, 62, 380, 24),
                activePlayer.IsMoving ? "Moving..." : "Space = end selected player", _smallStyle);

            GUI.Label(new Rect(20, 88, 380, 22),
                $"Selected: {activePlayer.DisplayName} | HP {activePlayer.CurrentHP}/{activePlayer.MaxHP} | AP {activePlayer.CurrentAP}/{activePlayer.MaxAP}",
                _smallStyle);

            if (combatManager == null) return;

            // Full turn order
            GUI.Label(new Rect(20, 112, 380, 20), "-- Turn Order (by Initiative) --", _smallStyle);

            for (int i = 0; i < turnCount && i < 8; i++)
            {
                ICombatUnit unit = combatManager.TurnOrder[i];
                if (unit == null || !unit.IsAlive) continue;

                bool isActive = (unit == combatManager.SelectedUnit);
                string marker = isActive ? ">" : " ";
                string typeTag = unit.Faction == Faction.Enemy ? "[E]" : "[P]";
                string state = unit.HasEndedRound ? "DONE" : $"{unit.CurrentAP}/{unit.MaxAP} AP";
                string hpStr = $"HP {unit.CurrentHP}/{unit.MaxHP}";

                GUIStyle rowStyle = unit.Faction == Faction.Enemy ? _enemyStyle : _smallStyle;
                GUI.Label(new Rect(20, 132 + i * 20, 380, 20),
                    $"{marker} {i + 1}. {typeTag} {unit.DisplayName}  Init:{unit.Initiative}  {hpStr}  {state}",
                    rowStyle);
            }
        }
    }
}
