using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CameraFollow : MonoBehaviour
{
    [Header("Targets (Tractor and Trailer)")]
    [SerializeField] private Transform tractorTarget;
    [SerializeField] private Transform trailerTarget;

    [Header("Camera Tracking")]
    [Tooltip("If true, camera rigidly locks to tractor and rotates with it. Tractor is strictly centered and points UP.")]
    [SerializeField] private bool rotateWithTruck = true;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Zoom Settings")]
    [Tooltip("Closest zoom level")]
    [SerializeField] private float minZoom = 3.5f;
    [Tooltip("Furthest zoom level")]
    [SerializeField] private float maxZoom = 120f;
    [Tooltip("Default initial camera zoom")]
    [SerializeField] private float defaultZoom = 16f;

    [Header("Zoom Multiplier (1x - 5x)")]
    [SerializeField] private int zoomMultiplier = 1;

    [Header("Camera Framing Offset")]
    [Tooltip("Fixed camera offset along tractor forward axis (locked to reverse framing: -6.5f)")]
    [SerializeField] private float cameraShift = -6.5f;

    public static CameraFollow Instance { get; private set; }

    public int ZoomMultiplier => zoomMultiplier;
    public event System.Action<int> OnZoomMultiplierChanged;

    private Camera cam;
    private float targetZoom;
    private Vector3 shakeOffset = Vector3.zero;
    private Coroutine shakeRoutine;
    private float currentShift = -6.5f;
    private float mouseScrollAccumulator = 0f;
    private float lastScrollStepTime = 0f;
    private const float ScrollStepCooldown = 0.16f;

    public bool RotateWithTruck => rotateWithTruck;
    public float CurrentZoom => cam != null ? cam.orthographicSize : targetZoom;
    public float CurrentShift => currentShift;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (maxZoom < 120f) maxZoom = 120f;
        if (minZoom > 3.5f) minZoom = 3.5f;

        zoomMultiplier = Mathf.Clamp(zoomMultiplier, 1, 5);
        targetZoom = defaultZoom * zoomMultiplier;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = targetZoom;
        }

        // Unconditionally ensure camera is strictly locked to tractor with fixed reverse framing
        rotateWithTruck = true;
        offset = new Vector3(0f, 0f, -10f);
        float zoomScale = 1f + 0.25f * (zoomMultiplier - 1);
        currentShift = cameraShift * zoomScale;

        FindTargetsIfNull();
        SnapToTarget();
    }

    public void SetZoomMultiplier(int mult)
    {
        zoomMultiplier = Mathf.Clamp(mult, 1, 5);
        targetZoom = defaultZoom * zoomMultiplier;
        OnZoomMultiplierChanged?.Invoke(zoomMultiplier);
    }

    public void StepZoom(int direction)
    {
        SetZoomMultiplier(zoomMultiplier + direction);
    }

    private void SyncMultiplierFromTarget()
    {
        int mult = Mathf.Clamp(Mathf.RoundToInt(targetZoom / defaultZoom), 1, 5);
        if (mult != zoomMultiplier)
        {
            zoomMultiplier = mult;
            OnZoomMultiplierChanged?.Invoke(zoomMultiplier);
        }
    }

    private void OnValidate()
    {
        if (maxZoom < 120f) maxZoom = 120f;
        if (minZoom > 3.5f) minZoom = 3.5f;
        rotateWithTruck = true;
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
        TruckController tc = TruckController.Instance;
        if (tc == null)
        {
            tc = Object.FindFirstObjectByType<TruckController>();
        }

        if (tc != null)
        {
            if (tractorTarget == null) tractorTarget = tc.transform;
            if (trailerTarget == null && tc.TrailerRb != null) trailerTarget = tc.TrailerRb.transform;
        }

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
        if (tractorTarget == null) FindTargetsIfNull();
        if (tractorTarget == null) return;

        float zoomScale = 1f + 0.25f * (zoomMultiplier - 1);
        currentShift = cameraShift * zoomScale;
        Vector3 targetPos = new Vector3(tractorTarget.position.x, tractorTarget.position.y, -10f) + (Vector3)(tractorTarget.up * currentShift);
        transform.position = targetPos;
        transform.rotation = Quaternion.Euler(0f, 0f, tractorTarget.eulerAngles.z);
    }

    private void Update()
    {
        HandleInput();
        HandleZoom();
    }

    private void HandleInput()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            // Toggle quick zoom between normal (1x) and wide overview (2x)
            if (kb.cKey.wasPressedThisFrame)
            {
                SetZoomMultiplier(zoomMultiplier == 1 ? 2 : 1);
            }

            // Keyboard zoom controls (+ / - / PageUp / PageDown) - discrete steps like UI buttons
            if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame || kb.pageDownKey.wasPressedThisFrame)
            {
                StepZoom(-1); // Zoom in closer
            }
            if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame || kb.pageUpKey.wasPressedThisFrame)
            {
                StepZoom(+1); // Zoom out wider
            }
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            HandleMouseScroll(scroll);
        }
#else
        if (Input.GetKeyDown(KeyCode.C))
        {
            SetZoomMultiplier(zoomMultiplier == 1 ? 2 : 1);
        }

        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.PageDown))
        {
            StepZoom(-1); // Zoom in closer
        }
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.PageUp))
        {
            StepZoom(+1); // Zoom out wider
        }

        float scroll = Input.mouseScrollDelta.y * 120f;
        HandleMouseScroll(scroll);
#endif
    }

    private void HandleMouseScroll(float scroll)
    {
        if (Mathf.Abs(scroll) > 0.01f)
        {
            mouseScrollAccumulator += scroll;
        }
        else
        {
            mouseScrollAccumulator = Mathf.MoveTowards(mouseScrollAccumulator, 0f, 300f * Time.deltaTime);
        }

        if (Time.time - lastScrollStepTime >= ScrollStepCooldown)
        {
            // Scroll DOWN (negative delta): zoom OUT / wider overview (1x -> 2x -> 3x -> 4x -> 5x), max 5x
            if (mouseScrollAccumulator <= -50f)
            {
                StepZoom(+1);
                mouseScrollAccumulator = 0f;
                lastScrollStepTime = Time.time;
            }
            // Scroll UP (positive delta): zoom IN / closer view (5x -> 4x -> 3x -> 2x -> 1x), min 1x
            else if (mouseScrollAccumulator >= 50f)
            {
                StepZoom(-1);
                mouseScrollAccumulator = 0f;
                lastScrollStepTime = Time.time;
            }
        }
    }

    private void HandleZoom()
    {
        if (cam != null && Mathf.Abs(cam.orthographicSize - targetZoom) > 0.01f)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, 14f * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (tractorTarget == null)
        {
            FindTargetsIfNull();
            if (tractorTarget == null) return;
        }

        // Fixed reverse-style framing (-6.5f offset along tractor axis), no shifting while driving
        float zoomScale = 1f + 0.25f * (zoomMultiplier - 1);
        currentShift = cameraShift * zoomScale;

        Vector3 shiftVector = (Vector3)(tractorTarget.up * currentShift);
        Vector3 targetPos = new Vector3(tractorTarget.position.x, tractorTarget.position.y, -10f) + shiftVector;

        transform.position = targetPos + shakeOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, tractorTarget.eulerAngles.z);
    }

    public void Shake(float duration, float magnitude)
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeOffset = Vector3.zero;
        }
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
