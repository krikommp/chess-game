using MiniChess.Combat;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform m_target;

    [Header("Follow")]
    [SerializeField] private Vector3 m_offset = new Vector3(0f, 10f, -8f);
    [SerializeField, Min(0.01f)] private float m_followSmoothTime = 0.18f;
    [Tooltip("Fixed camera rotation. Position may smooth, rotation must not.")]
    [SerializeField] private Vector3 m_fixedEulerAngles = new Vector3(51f, 0f, 0f);
    [SerializeField, Min(0.01f)] private float m_focusReturnSmoothTime = 0.18f;

    [Header("Pan")]
    [SerializeField, Min(0.1f)] private float m_keyboardPanSpeed = 12f;
    [SerializeField, Min(0.001f)] private float m_dragPanSensitivity = 0.03f;

    [Header("Zoom")]
    [SerializeField, Min(1f)] private float m_minDistance = 7f;
    [SerializeField, Min(1f)] private float m_maxDistance = 18f;
    [SerializeField, Min(0.1f)] private float m_zoomSpeed = 6f;
    [SerializeField, Min(0.01f)] private float m_zoomSmoothTime = 0.12f;

    private float m_targetDistance = 12f;
    private float m_currentDistance = 12f;
    private float m_zoomVelocity;
    private Vector3 m_followVelocity;
    private Vector3 m_offsetDirection;
    private Vector3 m_manualPanOffset;
    private Vector3 m_manualPanVelocity;
    private Vector3 m_lastMousePosition;
    private Player1Controller m_trackedPlayer;
    private bool m_autoFocusActive = true;

    private void Awake()
    {
        CacheOffset();
        m_targetDistance = Mathf.Clamp(m_offset.magnitude, m_minDistance, m_maxDistance);
        m_currentDistance = m_targetDistance;
        ApplyFixedRotation();
        BindDefaultTargetIfNeeded();
    }

    private void OnEnable()
    {
        BindDefaultTargetIfNeeded();
    }

    private void LateUpdate()
    {
        BindDefaultTargetIfNeeded();
        if (m_target == null) return;

        if (HandlePanInput())
        {
            m_autoFocusActive = false;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            m_targetDistance = Mathf.Clamp(m_targetDistance - scroll * m_zoomSpeed, m_minDistance, m_maxDistance);
        }

        m_currentDistance = Mathf.SmoothDamp(
            m_currentDistance,
            m_targetDistance,
            ref m_zoomVelocity,
            m_zoomSmoothTime);

        if (m_autoFocusActive)
        {
            m_manualPanOffset = Vector3.SmoothDamp(
                m_manualPanOffset,
                Vector3.zero,
                ref m_manualPanVelocity,
                m_focusReturnSmoothTime);
        }

        Vector3 desiredPosition = m_target.position + m_manualPanOffset + m_offsetDirection * m_currentDistance;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref m_followVelocity,
            m_followSmoothTime);

        ApplyFixedRotation();
    }

    private void OnDisable()
    {
        UnbindTrackedPlayer();
    }

    private void OnDestroy()
    {
        UnbindTrackedPlayer();
    }

    private void OnValidate()
    {
        if (m_maxDistance < m_minDistance)
        {
            m_maxDistance = m_minDistance;
        }

        CacheOffset();
    }

    public void FocusOn(Transform focusTarget)
    {
        if (focusTarget == null) return;

        m_target = focusTarget;
        ActivateAutoFocus();
        BindDefaultTargetIfNeeded();
    }

    private void BindDefaultTargetIfNeeded()
    {
        if (m_target == null)
        {
            var player = FindObjectOfType<Player1Controller>();
            if (player != null)
            {
                m_target = player.transform;
            }
        }

        var currentPlayer = m_target != null ? m_target.GetComponent<Player1Controller>() : null;
        if (m_trackedPlayer == currentPlayer) return;

        UnbindTrackedPlayer();
        m_trackedPlayer = currentPlayer;
        if (m_trackedPlayer != null)
        {
            m_trackedPlayer.MovementStarted += HandleTrackedPlayerMovementStarted;
        }
    }

    private void UnbindTrackedPlayer()
    {
        if (m_trackedPlayer == null) return;
        m_trackedPlayer.MovementStarted -= HandleTrackedPlayerMovementStarted;
        m_trackedPlayer = null;
    }

    private bool HandlePanInput()
    {
        Vector3 panDelta = Vector3.zero;
        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;

        float horizontal = 0f;
        float vertical = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;

        if (Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f)
        {
            panDelta += (right * horizontal + forward * vertical) * (m_keyboardPanSpeed * Time.deltaTime);
        }

        if (Input.GetMouseButtonDown(2))
        {
            m_lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(2))
        {
            Vector3 mouseDelta = Input.mousePosition - m_lastMousePosition;
            m_lastMousePosition = Input.mousePosition;
            panDelta += (-right * mouseDelta.x - forward * mouseDelta.y) * m_dragPanSensitivity;
        }

        if (panDelta.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        m_manualPanOffset += panDelta;
        return true;
    }

    private void HandleTrackedPlayerMovementStarted()
    {
        ActivateAutoFocus();
    }

    private void ActivateAutoFocus()
    {
        m_autoFocusActive = true;
        m_manualPanVelocity = Vector3.zero;
    }

    private void CacheOffset()
    {
        m_offsetDirection = m_offset.sqrMagnitude > 0.0001f
            ? m_offset.normalized
            : new Vector3(0f, 10f, -8f).normalized;
    }

    private void ApplyFixedRotation()
    {
        transform.rotation = Quaternion.Euler(m_fixedEulerAngles);
    }
}



