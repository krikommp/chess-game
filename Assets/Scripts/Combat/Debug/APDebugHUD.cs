using UnityEngine;

namespace MiniChess.Combat.DebugUI
{
    /// <summary>OnGUI overlay showing round, AP, HP, turn order, and attack preview.</summary>
    public class APDebugHUD : MonoBehaviour
    {
        [SerializeField] private Player1Controller m_player;
        [SerializeField] private MoveInputController m_input;
        [SerializeField] private CombatRoundManager m_combatManager;

        private GUIStyle m_style;
        private GUIStyle m_smallStyle;
        private GUIStyle m_enemyStyle;

        private void Awake()
        {
            if (m_combatManager == null) m_combatManager = FindObjectOfType<CombatRoundManager>();
        }

        private void OnGUI()
        {
            Player1Controller activePlayer = m_combatManager != null && m_combatManager.SelectedPlayer != null
                ? m_combatManager.SelectedPlayer
                : m_player;
            if (activePlayer == null) return;

            if (m_style == null)
            {
                m_style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    normal = { textColor = Color.white }
                };
                m_smallStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                };
                m_enemyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(1f, 0.6f, 0.4f) }
                };
            }

            string waitingLine = "";
            if (m_combatManager != null && m_combatManager.IsWaiting && m_combatManager.SelectedUnit != null)
            {
                waitingLine = $" >>> {m_combatManager.SelectedUnit.DisplayName} acting... <<<";
            }

            string costLine = "Cost: -";
            if (m_input != null && m_input.IsAttackMode)
            {
                costLine = m_input.PreviewReachable
                    ? $"Atk {m_input.AttackTargetName}: 20 dmg ({m_input.PreviewApCost} AP)"
                    : $"Atk {m_input.AttackTargetName}: 20 dmg ({m_input.PreviewApCost} AP, UNREACHABLE)";
            }
            else if (m_input != null && m_input.HasPreview)
            {
                costLine = m_input.PreviewReachable
                    ? $"Cost: {m_input.PreviewApCost} AP"
                    : $"Cost: {m_input.PreviewApCost} AP (UNREACHABLE)";
            }

            int turnCount = m_combatManager != null ? m_combatManager.TurnOrder.Count : 0;
            float boxHeight = 112 + turnCount * 20 + 10;
            GUI.Box(new Rect(10, 10, 400, boxHeight), GUIContent.none);

            GUI.Label(new Rect(20, 14, 300, 24),
                $"Round: {(m_combatManager != null ? m_combatManager.RoundCount : 1)}{waitingLine}", m_style);
            GUI.Label(new Rect(20, 38, 380, 24), costLine, m_style);
            GUI.Label(new Rect(20, 62, 380, 24),
                activePlayer.IsMoving ? "Moving..." : "Space = end selected m_player", m_smallStyle);

            GUI.Label(new Rect(20, 88, 380, 22),
                $"Selected: {activePlayer.DisplayName} | HP {activePlayer.CurrentHP}/{activePlayer.MaxHP} | AP {activePlayer.CurrentAP}/{activePlayer.MaxAP}",
                m_smallStyle);

            if (m_combatManager == null) return;

            // Full turn order
            GUI.Label(new Rect(20, 112, 380, 20), "-- Turn Order (by Initiative) --", m_smallStyle);

            for (int i = 0; i < turnCount && i < 8; i++)
            {
                ICombatUnit unit = m_combatManager.TurnOrder[i];
                if (unit == null || !unit.IsAlive) continue;

                bool isActive = (unit == m_combatManager.SelectedUnit);
                string marker = isActive ? ">" : " ";
                string typeTag = unit.Faction == EFaction.Enemy ? "[E]" : "[P]";
                string state = unit.HasEndedRound ? "DONE" : $"{unit.CurrentAP}/{unit.MaxAP} AP";
                string hpStr = $"HP {unit.CurrentHP}/{unit.MaxHP}";

                GUIStyle rowStyle = unit.Faction == EFaction.Enemy ? m_enemyStyle : m_smallStyle;
                GUI.Label(new Rect(20, 132 + i * 20, 380, 20),
                    $"{marker} {i + 1}. {typeTag} {unit.DisplayName}  Init:{unit.Initiative}  {hpStr}  {state}",
                    rowStyle);
            }
        }
    }
}




