using UnityEngine;

namespace MiniChess.Combat.DebugUI
{
    /// <summary>OnGUI overlay showing AP and current move preview cost.</summary>
    public class APDebugHUD : MonoBehaviour
    {
        public Player1Controller player;
        public MoveInputController input;

        private GUIStyle _style;

        private void OnGUI()
        {
            if (player == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
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

            GUI.Box(new Rect(10, 10, 240, 90), GUIContent.none);
            GUI.Label(new Rect(20, 14, 220, 24),
                $"AP: {player.CurrentAP} / {player.MaxAP}", _style);
            GUI.Label(new Rect(20, 38, 220, 24), costLine, _style);
            GUI.Label(new Rect(20, 62, 220, 24),
                player.IsMoving ? "Moving..." : "Idle (R = refill AP)", _style);
        }
    }
}
