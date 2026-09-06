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

    public static CameraFollow Instance { get; private set; }

    public int ZoomMultiplier => zoomMultiplier;
    public event System.Action<int> OnZoomMultiplierChanged;

    private Camera cam;
    private float targetZoom;
    private Vector3 shakeOffset = Vector3.zero;
    private Coroutine shakeRoutine;

    public bool RotateWithTruck => rotateWithTruck;
    public float CurrentZoom => cam != null ? cam.orthographicSize : targetZoom;

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

        // Unconditionally ensure camera is strictly locked to tractor at dead center
        rotateWithTruck = true;
        offset = new Vector3(0f, 0f, -10f);

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

        // Rigid lock: tractor is ALWAYS strictly in the center of the frame and looks strictly UP (90 degrees to bottom line of screen)
        Vector3 targetPos = new Vector3(tractorTarget.position.x, tractorTarget.position.y, -10f);
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
                SyncMultiplierFromTarget();
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
                SyncMultiplierFromTarget();
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.C))
        {
            SetZoomMultiplier(zoomMultiplier == 1 ? 2 : 1);
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
            SyncMultiplierFromTarget();
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.05f)
        {
            float notches = Mathf.Sign(scroll) * Mathf.Max(1f, Mathf.Abs(scroll));
            float step = Mathf.Max(1.5f, targetZoom * 0.22f);
            targetZoom = Mathf.Clamp(targetZoom - notches * step, minZoom, maxZoom);
            SyncMultiplierFromTarget();
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

    private void LateUpdate()
    {
        if (tractorTarget == null)
        {
            FindTargetsIfNull();
            if (tractorTarget == null) return;
        }

        // Rigid lock: tractor is ALWAYS strictly in the center of the frame and looks strictly UP (90 degrees to bottom line of screen)
        Vector3 targetPos = new Vector3(tractorTarget.position.x, tractorTarget.position.y, -10f);
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
