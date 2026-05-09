using MiniChess.Combat;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.18f;
    [Tooltip("Fixed camera rotation. Position may smooth, rotation must not.")]
    [SerializeField] private Vector3 fixedEulerAngles = new Vector3(51f, 0f, 0f);
    [SerializeField, Min(0.01f)] private float focusReturnSmoothTime = 0.18f;

    [Header("Pan")]
    [SerializeField, Min(0.1f)] private float keyboardPanSpeed = 12f;
    [SerializeField, Min(0.001f)] private float dragPanSensitivity = 0.03f;

    [Header("Zoom")]
    [SerializeField, Min(1f)] private float minDistance = 7f;
    [SerializeField, Min(1f)] private float maxDistance = 18f;
    [SerializeField, Min(0.1f)] private float zoomSpeed = 6f;
    [SerializeField, Min(0.01f)] private float zoomSmoothTime = 0.12f;

    private float _targetDistance = 12f;
    private float _currentDistance = 12f;
    private float _zoomVelocity;
    private Vector3 _followVelocity;
    private Vector3 _offsetDirection;
    private Vector3 _manualPanOffset;
    private Vector3 _manualPanVelocity;
    private Vector3 _lastMousePosition;
    private Player1Controller _trackedPlayer;
    private bool _autoFocusActive = true;

    private void Awake()
    {
        CacheOffset();
        _targetDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
        _currentDistance = _targetDistance;
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
        if (target == null) return;

        if (HandlePanInput())
        {
            _autoFocusActive = false;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetDistance = Mathf.Clamp(_targetDistance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        _currentDistance = Mathf.SmoothDamp(
            _currentDistance,
            _targetDistance,
            ref _zoomVelocity,
            zoomSmoothTime);

        if (_autoFocusActive)
        {
            _manualPanOffset = Vector3.SmoothDamp(
                _manualPanOffset,
                Vector3.zero,
                ref _manualPanVelocity,
                focusReturnSmoothTime);
        }

        Vector3 desiredPosition = target.position + _manualPanOffset + _offsetDirection * _currentDistance;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _followVelocity,
            followSmoothTime);

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
        if (maxDistance < minDistance)
        {
            maxDistance = minDistance;
        }

        CacheOffset();
    }

    private void BindDefaultTargetIfNeeded()
    {
        if (target == null)
        {
            var player = FindObjectOfType<Player1Controller>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        var currentPlayer = target != null ? target.GetComponent<Player1Controller>() : null;
        if (_trackedPlayer == currentPlayer) return;

        UnbindTrackedPlayer();
        _trackedPlayer = currentPlayer;
        if (_trackedPlayer != null)
        {
            _trackedPlayer.MovementStarted += HandleTrackedPlayerMovementStarted;
        }
    }

    private void UnbindTrackedPlayer()
    {
        if (_trackedPlayer == null) return;
        _trackedPlayer.MovementStarted -= HandleTrackedPlayerMovementStarted;
        _trackedPlayer = null;
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
            panDelta += (right * horizontal + forward * vertical) * (keyboardPanSpeed * Time.deltaTime);
        }

        if (Input.GetMouseButtonDown(2))
        {
            _lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(2))
        {
            Vector3 mouseDelta = Input.mousePosition - _lastMousePosition;
            _lastMousePosition = Input.mousePosition;
            panDelta += (-right * mouseDelta.x - forward * mouseDelta.y) * dragPanSensitivity;
        }

        if (panDelta.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        _manualPanOffset += panDelta;
        return true;
    }

    private void HandleTrackedPlayerMovementStarted()
    {
        _autoFocusActive = true;
        _manualPanVelocity = Vector3.zero;
    }

    private void CacheOffset()
    {
        _offsetDirection = offset.sqrMagnitude > 0.0001f
            ? offset.normalized
            : new Vector3(0f, 10f, -8f).normalized;
    }

    private void ApplyFixedRotation()
    {
        transform.rotation = Quaternion.Euler(fixedEulerAngles);
    }
}
