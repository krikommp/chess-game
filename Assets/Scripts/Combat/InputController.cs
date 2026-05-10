using System;
using MiniChess.Combat.Skills;
using MiniChess.GameplayTags;
using UnityEngine;

namespace MiniChess.Combat
{
    /// <summary>
    /// Pure input receiver. It translates pointer input into tagged input requests
    /// and lets gameplay systems decide how to interpret them.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        public event Action<SkillInputRequest> InputReceived;

        [Header("Refs")]
        [SerializeField] private Camera m_cam;
        [SerializeField] private PathPreview m_preview;

        [Header("Raycast")]
        [Tooltip("Layers considered as 'ground' for mouse raycast (e.g. Ground + Obstacle).")]
        [SerializeField] private LayerMask m_groundMask = ~0;

        [Tooltip("Max distance from mouse hit point to a NavMesh point. Smaller = stricter.")]
        [SerializeField, Min(0.05f)] private float m_navMeshSnapRadius = 0.5f;

        [Tooltip("Max distance from unit transform to the nearest NavMesh point. Handles capsule pivot height.")]
        [SerializeField, Min(0.05f)] private float m_originSnapRadius = 2f;

        private void Awake()
        {
            if (m_cam == null) m_cam = Camera.main;
        }

        private void Update()
        {
            if (m_cam == null) return;

            Ray ray = m_cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore))
            {
                InputReceived?.Invoke(new SkillInputRequest(
                    SkillInputTag.k_PointerHover,
                    SkillInputTag.k_TargetUnknown,
                    null,
                    default,
                    hasWorldPosition: false));
                return;
            }

            InputReceived?.Invoke(BuildRequest(SkillInputTag.k_PointerHover, hit));

            if (Input.GetMouseButtonDown(0))
            {
                InputReceived?.Invoke(BuildRequest(SkillInputTag.k_PrimaryPressed, hit));
            }
        }

        public SkillInputServices CreateSkillInputServices()
        {
            return new SkillInputServices(m_preview, m_navMeshSnapRadius, m_originSnapRadius);
        }

        private SkillInputRequest BuildRequest(GameplayTag signalTag, RaycastHit hit)
        {
            GameObject targetObject = hit.collider != null ? hit.collider.gameObject : null;
            var targetTag = ResolveTargetTag(hit, out GameObject semanticTarget);

            return new SkillInputRequest(
                signalTag,
                targetTag,
                semanticTarget != null ? semanticTarget : targetObject,
                hit.point,
                hasWorldPosition: true);
        }

        private GameplayTag ResolveTargetTag(RaycastHit hit, out GameObject semanticTarget)
        {
            semanticTarget = null;

            var hitPlayer = hit.collider.GetComponentInParent<Player1Controller>();
            if (hitPlayer != null)
            {
                semanticTarget = hitPlayer.gameObject;
                return SkillInputTag.k_TargetPlayer;
            }

            var hitEnemy = hit.collider.GetComponentInParent<EnemyController>();
            if (hitEnemy != null)
            {
                semanticTarget = hitEnemy.gameObject;
                return SkillInputTag.k_TargetEnemy;
            }

            if (((1 << hit.collider.gameObject.layer) & m_groundMask) != 0)
            {
                semanticTarget = hit.collider.gameObject;
                return SkillInputTag.k_TargetGround;
            }

            semanticTarget = hit.collider.gameObject;
            return SkillInputTag.k_TargetUnknown;
        }
    }
}
