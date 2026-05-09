using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Renders the move preview as two LineRenderers:
    ///   - reachable (green, dashed via texture tile)
    ///   - unreachable (red)
    /// Caller pushes the corner arrays each frame; this component only renders.
    /// </summary>
    public class PathPreview : MonoBehaviour
    {
        [Header("Lines (assign in inspector)")]
        public LineRenderer reachableLine;
        public LineRenderer unreachableLine;

        [Header("Style")]
        [SerializeField] private Color reachableColor = new Color(0.3f, 1f, 0.4f, 1f);
        [SerializeField] private Color unreachableColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField, Min(0.01f)] private float width = 0.12f;
        [Tooltip("Lift line off the ground to avoid z-fighting.")]
        [SerializeField] private float yLift = 0.05f;

        private void Awake()
        {
            ConfigureLine(reachableLine, reachableColor);
            ConfigureLine(unreachableLine, unreachableColor);
            Clear();
        }

        private void ConfigureLine(LineRenderer lr, Color c)
        {
            if (lr == null) return;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Tile;
            lr.alignment = LineAlignment.View;
            if (lr.sharedMaterial == null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                lr.material = mat;
            }
            lr.startColor = c;
            lr.endColor = c;
        }

        public void Show(Vector3[] reachable, Vector3[] unreachable)
        {
            SetPoints(reachableLine, reachable);
            SetPoints(unreachableLine, unreachable);
        }

        public void Clear()
        {
            if (reachableLine != null) reachableLine.positionCount = 0;
            if (unreachableLine != null) unreachableLine.positionCount = 0;
        }

        private void SetPoints(LineRenderer lr, Vector3[] pts)
        {
            if (lr == null) return;
            if (pts == null || pts.Length < 2) { lr.positionCount = 0; return; }

            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = pts[i];
                p.y += yLift;
                lr.SetPosition(i, p);
            }
        }
    }
}
