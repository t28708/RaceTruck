using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFollow : MonoBehaviour
{
    [Header("Targets (Tractor & Trailer)")]
    [SerializeField] private Transform tractorTarget;
    [SerializeField] private Transform trailerTarget;

    [Header("Tracking Dynamics")]
    [Tooltip("Base tracking speed. Dynamically increases if vehicle moves fast")]
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Camera Modes")]
    [Tooltip("If true, camera rotates to match truck heading. Press 'C' to toggle")]
    [SerializeField] private bool rotateWithTruck = false;
    [SerializeField] private float rotationSmoothSpeed = 6f;

    [Header("Zoom Settings (Extended Range)")]
    [Tooltip("Closest zoom level (close inspection of wheels, hitch, dock clearance)")]
    [SerializeField] private float minZoom = 3.5f;
    [Tooltip("Furthest zoom level (high birds-eye view of the entire yard and surroundings)")]
    [SerializeField] private float maxZoom = 120f;
    [Tooltip("Default initial camera zoom")]
    [SerializeField] private float defaultZoom = 16f;

    [Header("Dynamic Look-Ahead / Look-Behind (Reversing)")]
    [Tooltip("Delay in seconds of continuous reverse before camera glides backward")]
    [SerializeField] private float reversePanDelay = 0.85f;
    [Tooltip("Speed of transition between forward and reverse camera positions")]
    [SerializeField] private float panTransitionSpeed = 2.0f;
    [Tooltip("Forward camera bias in meters (shows space ahead of cab)")]
    [SerializeField] private float forwardShift = 2.2f;
    [Tooltip("Backward camera bias in meters (shows space behind trailer while keeping tractor wheels in view)")]
    [SerializeField] private float reverseShift = 3.5f;

    private Camera cam;
    private float targetZoom;
    private Vector3 shakeOffset = Vector3.zero;
    private Coroutine shakeRoutine;

    // Dynamic reverse backing tracking state
    private float reverseTimer = 0f;
    private bool isBackingView = false;
    private float backingWeight = 0f; // 0 = forward look-ahead, 1 = reverse trailer look-behind

    public bool RotateWithTruck => rotateWithTruck;
    public float CurrentZoom => cam != null ? cam.orthographicSize : targetZoom;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (maxZoom < 120f) maxZoom = 120f;
        if (minZoom > 3.5f) minZoom = 3.5f;

        targetZoom = defaultZoom;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = defaultZoom;
        }
    }

    private void OnValidate()
    {
        if (maxZoom < 120f) maxZoom = 120f;
        if (minZoom > 3.5f) minZoom = 3.5f;
    }

    private void Start()
    {
        FindTargetsIfNull();
        SnapToTarget();
    }

    public void SetupTargets(Transform tractor, Transform trailer)
    {
        tractorTarget = tractor;
        trailerTarget = trailer;
        SnapToTarget();
    }

    private void FindTargetsIfNull()
    {
        if (tractorTarget == null)
        {
            GameObject tractor = GameObject.Find("Tractor");
            if (tractor != null) tractorTarget = tractor.transform;
        }

        if (trailerTarget == null)
        {
            GameObject trailer = GameObject.Find("Trailer");
            if (trailer != null) trailerTarget = trailer.transform;
        }
    }

    public void SnapToTarget()
    {
        Vector3 center = CalculateRigCenter();
        Vector3 targetPos = center + offset;
        targetPos.z = -10f;
        transform.position = targetPos;

        if (rotateWithTruck && tractorTarget != null)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, tractorTarget.eulerAngles.z);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }

    private void Update()
    {
        HandleInput();
        HandleZoom();
        UpdateBackingState();
    }

    private void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.cKey.wasPressedThisFrame)
            {
                rotateWithTruck = !rotateWithTruck;
            }

            // Keyboard zoom controls (+ / - / PageUp / PageDown)
            float kbZoom = 0f;
            if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed || kb.pageUpKey.isPressed)
            {
                kbZoom -= 1f; // Zoom in
            }
            if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed || kb.pageDownKey.isPressed)
            {
                kbZoom += 1f; // Zoom out
            }

            if (Mathf.Abs(kbZoom) > 0.01f)
            {
                float step = Mathf.Max(4f, targetZoom * 0.5f) * Time.deltaTime * 2.5f;
                targetZoom = Mathf.Clamp(targetZoom + kbZoom * step, minZoom, maxZoom);
            }
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.05f)
            {
                // Multi-notch acceleration: scale step by scroll ticks (120 units per notch in Input System)
                float notches = Mathf.Sign(scroll) * Mathf.Max(1f, Mathf.Abs(scroll) / 120f);
                // Proportional zoom step: fine when close, fast when high up
                float step = Mathf.Max(1.5f, targetZoom * 0.22f);
                targetZoom = Mathf.Clamp(targetZoom - notches * step, minZoom, maxZoom);
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.C))
        {
            rotateWithTruck = !rotateWithTruck;
        }

        float kbZoom = 0f;
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.PageUp))
        {
            kbZoom -= 1f;
        }
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.PageDown))
        {
            kbZoom += 1f;
        }

        if (Mathf.Abs(kbZoom) > 0.01f)
        {
            float step = Mathf.Max(4f, targetZoom * 0.5f) * Time.deltaTime * 2.5f;
            targetZoom = Mathf.Clamp(targetZoom + kbZoom * step, minZoom, maxZoom);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.05f)
        {
            float notches = Mathf.Sign(scroll) * Mathf.Max(1f, Mathf.Abs(scroll));
            float step = Mathf.Max(1.5f, targetZoom * 0.22f);
            targetZoom = Mathf.Clamp(targetZoom - notches * step, minZoom, maxZoom);
        }
#endif
    }

    private void HandleZoom()
    {
        if (cam != null && Mathf.Abs(cam.orthographicSize - targetZoom) > 0.01f)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, 14f * Time.deltaTime);
        }
    }

    private void UpdateBackingState()
    {
        float speed = 0f;
        if (TruckController.Instance != null)
        {
            speed = TruckController.Instance.CurrentSpeed;
        }

        // Detect reverse vs forward driving
        if (speed < -0.15f) // Reversing
        {
            reverseTimer += Time.deltaTime;
            if (reverseTimer >= reversePanDelay)
            {
                isBackingView = true;
            }
        }
        else if (speed > 0.15f) // Driving forward
        {
            reverseTimer = 0f;
            isBackingView = false;
        }
        else // Neutral / stopped
        {
            // If stopped, keep current view for a moment then slowly reset
            if (reverseTimer > 0f)
            {
                reverseTimer = Mathf.Max(0f, reverseTimer - Time.deltaTime * 0.4f);
                if (reverseTimer <= 0.01f)
                {
                    isBackingView = false;
                }
            }
        }

        // Smoothly interpolate camera view weighting between forward (0) and reverse backing (1)
        float targetWeight = isBackingView ? 1f : 0f;
        backingWeight = Mathf.MoveTowards(backingWeight, targetWeight, panTransitionSpeed * Time.deltaTime);
    }

    private Vector3 CalculateRigCenter()
    {
        if (tractorTarget == null && trailerTarget == null) return transform.position;
        if (tractorTarget != null && trailerTarget == null) return tractorTarget.position;
        if (tractorTarget == null && trailerTarget != null) return trailerTarget.position;

        // Front steer axle point on tractor (where wheels turn)
        Vector3 frontAxle = tractorTarget.position + tractorTarget.up * 2.8f;
        // Trailer rear bumper point
        Vector3 trailerRear = trailerTarget.position - trailerTarget.up * 7.8f;

        // Geometric midpoint between front steer wheels and trailer rear bumper
        Vector3 rigMidpoint = (frontAxle + trailerRear) * 0.5f;

        // 1. Forward Mode: slight forward bias to show room ahead of the cab
        Vector3 forwardTargetPoint = rigMidpoint + tractorTarget.up * forwardShift;

        // 2. Reverse Mode: moderate shift towards trailer rear, keeping front wheels & tractor 100% in view
        Vector3 reverseTargetPoint = rigMidpoint - trailerTarget.up * reverseShift;

        // 3. Smooth blend between forward view and backing view
        return Vector3.Lerp(forwardTargetPoint, reverseTargetPoint, backingWeight);
    }

    private void LateUpdate()
    {
        Vector3 rigCenter = CalculateRigCenter();
        Vector3 desiredPosition = rigCenter + offset + shakeOffset;
        desiredPosition.z = -10f;

        // Dynamic catch-up speed: if camera falls behind, it accelerates smoothly
        float distanceToTarget = Vector2.Distance(transform.position, desiredPosition);
        float dynamicSpeed = Mathf.Max(smoothSpeed, distanceToTarget * 4.0f);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, dynamicSpeed * Time.deltaTime);

        // Rotation handling (toggled via 'C')
        if (rotateWithTruck && tractorTarget != null)
        {
            Quaternion targetRot = Quaternion.Euler(0f, 0f, tractorTarget.eulerAngles.z);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSmoothSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.identity, rotationSmoothSpeed * Time.deltaTime);
        }
    }

    public void Shake(float duration, float magnitude)
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(DoShake(duration, magnitude));
    }

    private IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = 1f - (elapsed / duration);
            shakeOffset = new Vector3(
                Random.Range(-magnitude, magnitude) * percent,
                Random.Range(-magnitude, magnitude) * percent,
                0f
            );
            yield return null;
        }
        shakeOffset = Vector3.zero;
        shakeRoutine = null;
    }
}
