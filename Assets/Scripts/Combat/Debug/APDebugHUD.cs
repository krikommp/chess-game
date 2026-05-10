using UnityEngine;

namespace MiniChess.Combat.DebugUI
{
    /// <summary>OnGUI overlay showing round, AP, HP, turn order, and attack preview.</summary>
    public class APDebugHUD : MonoBehaviour
    {
        [SerializeField] private Player1Controller m_player;
        [SerializeField] private InputController m_input;
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
                var selAttr = m_combatManager.SelectedUnit.GetComponent<AttributeSet>();
                string selName = selAttr != null ? selAttr.DisplayName : "?";
                waitingLine = $" >>> {selName} acting... <<<";
            }

            string costLine = "Input: routed through active skill";

            int turnCount = m_combatManager != null ? m_combatManager.TurnOrder.Count : 0;
            float boxHeight = 112 + turnCount * 20 + 10;
            GUI.Box(new Rect(10, 10, 400, boxHeight), GUIContent.none);

            GUI.Label(new Rect(20, 14, 300, 24),
                $"Round: {(m_combatManager != null ? m_combatManager.RoundCount : 1)}{waitingLine}", m_style);
            GUI.Label(new Rect(20, 38, 380, 24), costLine, m_style);
            GUI.Label(new Rect(20, 62, 380, 24),
                activePlayer.IsMoving ? "Moving..." : "Space = end selected m_player", m_smallStyle);

            var aAttr = activePlayer.GetComponent<AttributeSet>();
            string aName = aAttr != null ? aAttr.DisplayName : activePlayer.DisplayName;
            int aHP = (int)(aAttr != null ? aAttr.Get(WellKnownAttributeTags.HP) : activePlayer.CurrentHP);
            int aMaxHP = (int)(aAttr != null ? aAttr.GetMax(WellKnownAttributeTags.HP) : activePlayer.MaxHP);
            int aAP = (int)(aAttr != null ? aAttr.Get(WellKnownAttributeTags.AP) : activePlayer.CurrentAP);
            int aMaxAP = (int)(aAttr != null ? aAttr.GetMax(WellKnownAttributeTags.AP) : activePlayer.MaxAP);

            GUI.Label(new Rect(20, 88, 380, 22),
                $"Selected: {aName} | HP {aHP}/{aMaxHP} | AP {aAP}/{aMaxAP}",
                m_smallStyle);

            if (m_combatManager == null) return;

            // Full turn order
            GUI.Label(new Rect(20, 112, 380, 20), "-- Turn Order (by Initiative) --", m_smallStyle);

            for (int i = 0; i < turnCount && i < 8; i++)
            {
                GameObject go = m_combatManager.TurnOrder[i];
                if (go == null) continue;
                var attr = go.GetComponent<AttributeSet>();
                if (attr == null || !attr.IsAlive) continue;

                bool isActive = (go == m_combatManager.SelectedUnit);
                string marker = isActive ? ">" : " ";
                string typeTag = attr.Faction == EFaction.Enemy ? "[E]" : "[P]";
                bool ended = m_combatManager.HasEndedRound(go);
                string state = ended ? "DONE" : $"{(int)attr.Get(WellKnownAttributeTags.AP)}/{(int)attr.GetMax(WellKnownAttributeTags.AP)} AP";
                string hpStr = $"HP {(int)attr.Get(WellKnownAttributeTags.HP)}/{(int)attr.GetMax(WellKnownAttributeTags.HP)}";
                float init = attr.Get(WellKnownAttributeTags.Initiative);
                string dispName = attr.DisplayName;

                GUIStyle rowStyle = attr.Faction == EFaction.Enemy ? m_enemyStyle : m_smallStyle;
                GUI.Label(new Rect(20, 132 + i * 20, 380, 20),
                    $"{marker} {i + 1}. {typeTag} {dispName}  Init:{init}  {hpStr}  {state}",
                    rowStyle);
            }
        }
    }
}


