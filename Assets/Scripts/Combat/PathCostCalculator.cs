using UnityEngine;
using UnityEngine.AI;

namespace MiniChess.Combat
{
    /// <summary>
    /// Pure helpers: path length, AP cost, and clipping a corner-list at a given walk distance.
    /// No MonoBehaviour state. Safe to call every frame.
    /// </summary>
    public static class PathCostCalculator
    {
        /// <summary>Sum euclidean distance between consecutive corners.</summary>
        public static float PathLength(Vector3[] corners)
        {
            if (corners == null || corners.Length < 2) return 0f;
            float total = 0f;
            for (int i = 1; i < corners.Length; i++)
                total += Vector3.Distance(corners[i - 1], corners[i]);
            return total;
        }

        /// <summary>AP cost = ceil(length / moveSpeed). Clamped to >=0.</summary>
        public static int ApCost(float length, float moveSpeedMetersPerAp)
        {
            if (moveSpeedMetersPerAp <= 0f) return int.MaxValue;
            return Mathf.CeilToInt(length / moveSpeedMetersPerAp);
        }

        /// <summary>
        /// Walk along corners and split at maxDistance.
        /// Returns the reachable prefix (includes the cut point) and the remaining suffix
        /// (starts at the cut point, ends at original last corner).
        /// If totalLength &lt;= maxDistance, suffix is empty.
        /// </summary>
        public static void Clip(Vector3[] corners, float maxDistance,
            out Vector3[] reachable, out Vector3[] unreachable)
        {
            if (corners == null || corners.Length == 0)
            {
                reachable = System.Array.Empty<Vector3>();
                unreachable = System.Array.Empty<Vector3>();
                return;
            }

            if (corners.Length == 1 || maxDistance <= 0f)
            {
                reachable = new[] { corners[0] };
                unreachable = corners;
                return;
            }

            float traveled = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                float seg = Vector3.Distance(corners[i - 1], corners[i]);
                if (traveled + seg >= maxDistance)
                {
                    float remain = maxDistance - traveled;
                    float t = seg <= 0.0001f ? 0f : remain / seg;
                    Vector3 cut = Vector3.Lerp(corners[i - 1], corners[i], t);

                    var head = new Vector3[i + 1];
                    System.Array.Copy(corners, 0, head, 0, i);
                    head[i] = cut;

                    int tailLen = corners.Length - i + 1; // cut + remaining corners
                    var tail = new Vector3[tailLen];
                    tail[0] = cut;
                    System.Array.Copy(corners, i, tail, 1, corners.Length - i);

                    reachable = head;
                    unreachable = tail;
                    return;
                }
                traveled += seg;
            }

            // entire path within budget
            reachable = corners;
            unreachable = System.Array.Empty<Vector3>();
        }
    }
}
