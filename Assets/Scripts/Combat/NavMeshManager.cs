using UnityEngine;

namespace MiniChess.Combat
{
    public class NavMeshManager : MonoBehaviour
    {
        public static NavMeshManager Instance { get; private set; }

        [Tooltip("Max distance from mouse hit point to a NavMesh point. Smaller = stricter.")]
        [SerializeField, Min(0.05f)] private float m_mouseSnapRadius = 0.5f;

        [Tooltip("Max distance from unit transform to the nearest NavMesh point.")]
        [SerializeField, Min(0.05f)] private float m_originSnapRadius = 2f;

        public float MouseSnapRadius => m_mouseSnapRadius;
        public float OriginSnapRadius => m_originSnapRadius;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
