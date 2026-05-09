using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -8f);
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.18f;
    [Tooltip("Fixed camera rotation. Position may smooth, rotation must not.")]
    [SerializeField] private Vector3 fixedEulerAngles = new Vector3(51f, 0f, 0f);

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

    private void Awake()
    {
        CacheOffset();
        _targetDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
        _currentDistance = _targetDistance;
        ApplyFixedRotation();
        BindDefaultTargetIfNeeded();
    }

    private void OnValidate()
    {
        if (maxDistance < minDistance)
        {
            maxDistance = minDistance;
        }

        CacheOffset();
    }

    private void LateUpdate()
    {
        BindDefaultTargetIfNeeded();
        if (target == null) return;

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

        Vector3 desiredPosition = target.position + _offsetDirection * _currentDistance;
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _followVelocity,
            followSmoothTime);

        ApplyFixedRotation();
    }

    private void BindDefaultTargetIfNeeded()
    {
        if (target != null) return;

        var player = FindObjectOfType<MiniChess.Combat.Player1Controller>();
        if (player != null)
        {
            target = player.transform;
        }
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
