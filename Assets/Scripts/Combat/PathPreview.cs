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
        public static PathPreview Instance { get; private set; }

        [Header("Lines (assign in inspector)")]
        [SerializeField] private LineRenderer m_reachableLine;
        [SerializeField] private LineRenderer m_unreachableLine;
        [SerializeField] private LineRenderer m_activeMoveLine;

        [Header("Style")]
        [SerializeField] private Color m_reachableColor = new Color(0.3f, 1f, 0.4f, 1f);
        [SerializeField] private Color m_unreachableColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color m_activeMoveColor = new Color(0.25f, 0.75f, 1f, 0.95f);
        [SerializeField, Min(0.01f)] private float m_width = 0.12f;
        [Tooltip("Lift line off the ground to avoid z-fighting.")]
        [SerializeField] private float m_yLift = 0.05f;

        [Header("Distance Label")]
        [SerializeField] private bool m_showDistanceLabel = true;
        [SerializeField] private Vector2 m_labelPixelOffset = new Vector2(10f, -28f);

        private GUIStyle m_distanceLabelStyle;
        private bool m_hasDistanceLabel;
        private Vector3 m_distanceLabelWorldPosition;
        private float m_reachableDistance;
        private float m_totalDistance;

        private void Awake()
        {
            Instance = this;
            if (m_reachableLine == null)
                m_reachableLine = FindLineRendererChild("ReachableLine");
            if (m_unreachableLine == null)
                m_unreachableLine = FindLineRendererChild("UnreachableLine");
            EnsureActiveMoveLine();
            ConfigureLine(m_reachableLine, m_reachableColor);
            ConfigureLine(m_unreachableLine, m_unreachableColor);
            ConfigureLine(m_activeMoveLine, m_activeMoveColor);
            Clear();
            ClearActivePath();
        }

        private LineRenderer FindLineRendererChild(string name)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == name)
                    return child.GetComponent<LineRenderer>();
            }
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void ConfigureLine(LineRenderer lr, Color c)
        {
            if (lr == null) return;
            lr.useWorldSpace = true;
            lr.startWidth = m_width;
            lr.endWidth = m_width;
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
            SetPoints(m_reachableLine, reachable);
            SetPoints(m_unreachableLine, unreachable);
            UpdateDistanceLabel(reachable, unreachable);
        }

        public void Clear()
        {
            if (m_reachableLine != null) m_reachableLine.positionCount = 0;
            if (m_unreachableLine != null) m_unreachableLine.positionCount = 0;
            m_hasDistanceLabel = false;
        }

        public void ShowActivePath(Vector3[] path)
        {
            SetPoints(m_activeMoveLine, path);
        }

        public void ClearActivePath()
        {
            if (m_activeMoveLine != null) m_activeMoveLine.positionCount = 0;
        }

        private void SetPoints(LineRenderer lr, Vector3[] pts)
        {
            if (lr == null) return;
            if (pts == null || pts.Length < 2) { lr.positionCount = 0; return; }

            lr.positionCount = pts.Length;
            for (int i = 0; i < pts.Length; i++)
            {
                var p = pts[i];
                p.y += m_yLift;
                lr.SetPosition(i, p);
            }
        }

        private void OnGUI()
        {
            if (!m_showDistanceLabel || !m_hasDistanceLabel) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPoint = cam.WorldToScreenPoint(m_distanceLabelWorldPosition);
            if (screenPoint.z <= 0f) return;

            EnsureDistanceLabelStyle();

            string text = BuildDistanceText();
            Vector2 size = m_distanceLabelStyle.CalcSize(new GUIContent(text));
            Rect rect = new Rect(
                screenPoint.x + m_labelPixelOffset.x,
                Screen.height - screenPoint.y + m_labelPixelOffset.y,
                size.x + 14f,
                size.y + 8f);

            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 7f, rect.y + 4f, size.x, size.y), text, m_distanceLabelStyle);
        }

        private void UpdateDistanceLabel(Vector3[] reachable, Vector3[] unreachable)
        {
            m_reachableDistance = PathCostCalculator.PathLength(reachable);
            float unreachableDistance = PathCostCalculator.PathLength(unreachable);
            m_totalDistance = m_reachableDistance + unreachableDistance;

            if (m_totalDistance <= 0.001f)
            {
                m_hasDistanceLabel = false;
                return;
            }

            if (reachable != null && reachable.Length > 0)
            {
                m_distanceLabelWorldPosition = reachable[reachable.Length - 1];
            }
            else if (unreachable != null && unreachable.Length > 0)
            {
                m_distanceLabelWorldPosition = unreachable[unreachable.Length - 1];
            }
            else
            {
                m_hasDistanceLabel = false;
                return;
            }

            m_distanceLabelWorldPosition.y += m_yLift + 0.35f;
            m_hasDistanceLabel = true;
        }

        private string BuildDistanceText()
        {
            bool hasUnreachablePart = m_totalDistance - m_reachableDistance > 0.05f;
            if (hasUnreachablePart && m_reachableDistance > 0.05f)
            {
                return $"{m_reachableDistance:0.0}m / {m_totalDistance:0.0}m";
            }

            return $"{m_totalDistance:0.0}m";
        }

        private void EnsureDistanceLabelStyle()
        {
            if (m_distanceLabelStyle != null) return;

            m_distanceLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private void EnsureActiveMoveLine()
        {
            if (m_activeMoveLine != null) return;

            GameObject lineObject = new GameObject("m_activeMoveLine");
            lineObject.transform.SetParent(transform, false);
            m_activeMoveLine = lineObject.AddComponent<LineRenderer>();
        }
    }
}


