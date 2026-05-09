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
        public LineRenderer activeMoveLine;

        [Header("Style")]
        [SerializeField] private Color reachableColor = new Color(0.3f, 1f, 0.4f, 1f);
        [SerializeField] private Color unreachableColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color activeMoveColor = new Color(0.25f, 0.75f, 1f, 0.95f);
        [SerializeField, Min(0.01f)] private float width = 0.12f;
        [Tooltip("Lift line off the ground to avoid z-fighting.")]
        [SerializeField] private float yLift = 0.05f;

        [Header("Distance Label")]
        [SerializeField] private bool showDistanceLabel = true;
        [SerializeField] private Vector2 labelPixelOffset = new Vector2(10f, -28f);

        private GUIStyle _distanceLabelStyle;
        private bool _hasDistanceLabel;
        private Vector3 _distanceLabelWorldPosition;
        private float _reachableDistance;
        private float _totalDistance;

        private void Awake()
        {
            EnsureActiveMoveLine();
            ConfigureLine(reachableLine, reachableColor);
            ConfigureLine(unreachableLine, unreachableColor);
            ConfigureLine(activeMoveLine, activeMoveColor);
            Clear();
            ClearActivePath();
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
            UpdateDistanceLabel(reachable, unreachable);
        }

        public void Clear()
        {
            if (reachableLine != null) reachableLine.positionCount = 0;
            if (unreachableLine != null) unreachableLine.positionCount = 0;
            _hasDistanceLabel = false;
        }

        public void ShowActivePath(Vector3[] path)
        {
            SetPoints(activeMoveLine, path);
        }

        public void ClearActivePath()
        {
            if (activeMoveLine != null) activeMoveLine.positionCount = 0;
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

        private void OnGUI()
        {
            if (!showDistanceLabel || !_hasDistanceLabel) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPoint = cam.WorldToScreenPoint(_distanceLabelWorldPosition);
            if (screenPoint.z <= 0f) return;

            EnsureDistanceLabelStyle();

            string text = BuildDistanceText();
            Vector2 size = _distanceLabelStyle.CalcSize(new GUIContent(text));
            Rect rect = new Rect(
                screenPoint.x + labelPixelOffset.x,
                Screen.height - screenPoint.y + labelPixelOffset.y,
                size.x + 14f,
                size.y + 8f);

            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 7f, rect.y + 4f, size.x, size.y), text, _distanceLabelStyle);
        }

        private void UpdateDistanceLabel(Vector3[] reachable, Vector3[] unreachable)
        {
            _reachableDistance = PathCostCalculator.PathLength(reachable);
            float unreachableDistance = PathCostCalculator.PathLength(unreachable);
            _totalDistance = _reachableDistance + unreachableDistance;

            if (_totalDistance <= 0.001f)
            {
                _hasDistanceLabel = false;
                return;
            }

            if (reachable != null && reachable.Length > 0)
            {
                _distanceLabelWorldPosition = reachable[reachable.Length - 1];
            }
            else if (unreachable != null && unreachable.Length > 0)
            {
                _distanceLabelWorldPosition = unreachable[unreachable.Length - 1];
            }
            else
            {
                _hasDistanceLabel = false;
                return;
            }

            _distanceLabelWorldPosition.y += yLift + 0.35f;
            _hasDistanceLabel = true;
        }

        private string BuildDistanceText()
        {
            bool hasUnreachablePart = _totalDistance - _reachableDistance > 0.05f;
            if (hasUnreachablePart && _reachableDistance > 0.05f)
            {
                return $"{_reachableDistance:0.0}m / {_totalDistance:0.0}m";
            }

            return $"{_totalDistance:0.0}m";
        }

        private void EnsureDistanceLabelStyle()
        {
            if (_distanceLabelStyle != null) return;

            _distanceLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private void EnsureActiveMoveLine()
        {
            if (activeMoveLine != null) return;

            GameObject lineObject = new GameObject("ActiveMoveLine");
            lineObject.transform.SetParent(transform, false);
            activeMoveLine = lineObject.AddComponent<LineRenderer>();
        }
    }
}
