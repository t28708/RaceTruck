using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TruckGuideLines : MonoBehaviour
{
    [Header("Visibility and Controls")]
    [Tooltip("Toggle visibility of trajectory guide lines")]
    [SerializeField] private bool showGuideLines = true;
    [Tooltip("Toggle key (Press L during gameplay to show or hide lines)")]
    [SerializeField] private KeyCode toggleKey = KeyCode.L;

    [Header("Line Lengths in Meters")]
    [Tooltip("Length of red clearance lines extending backwards from trailer rear corners")]
    [SerializeField] private float trailerLineLength = 26f;
    [Tooltip("Length of green clearance lines extending backwards from tractor drive wheels")]
    [SerializeField] private float tractorLineLength = 22f;
    [Tooltip("Length of cyan steering lines extending forwards from front steer wheels")]
    [SerializeField] private float steerLineLength = 14f;

    [Header("Visual Appearance")]
    [SerializeField] private float lineWidth = 0.09f;
    [SerializeField] private Color trailerLineColor = new Color(1f, 0.12f, 0.12f, 0.95f);
    [SerializeField] private Color tractorLineColor = new Color(0.08f, 0.95f, 0.22f, 0.9f);
    [SerializeField] private Color steerLineColor = new Color(0.2f, 0.75f, 1f, 0.75f);
    [SerializeField] private int sortingOrder = 15;

    [Header("References")]
    [SerializeField] private Transform tractorTransform;
    [SerializeField] private Transform trailerTransform;
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;

    private LineRenderer trailerLeftLine;
    private LineRenderer trailerRightLine;
    private LineRenderer tractorLeftLine;
    private LineRenderer tractorRightLine;
    private LineRenderer steerLeftLine;
    private LineRenderer steerRightLine;

    private Material lineMaterial;

    public bool ShowGuideLines
    {
        get => showGuideLines;
        set
        {
            showGuideLines = value;
            UpdateLinesVisibility();
        }
    }

    private void Awake()
    {
        ResolveReferences();
        CreateLineMaterial();
        CreateLineRenderers();
    }

    private void Start()
    {
        ResolveReferences();
        UpdateLinesVisibility();
    }

    private void ResolveReferences()
    {
        if (tractorTransform == null)
        {
            tractorTransform = transform;
        }

        TruckController controller = tractorTransform.GetComponent<TruckController>();
        if (controller == null)
        {
            controller = TruckController.Instance;
        }

        if (controller != null)
        {
            if (trailerTransform == null && controller.TrailerRb != null)
            {
                trailerTransform = controller.TrailerRb.transform;
            }
            if (frontLeftWheel == null)
            {
                frontLeftWheel = controller.FrontLeftWheel;
            }
            if (frontRightWheel == null)
            {
                frontRightWheel = controller.FrontRightWheel;
            }
        }

        if (trailerTransform == null)
        {
            GameObject trGo = GameObject.Find("Trailer");
            if (trGo != null) trailerTransform = trGo.transform;
        }
    }

    private void CreateLineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("UI/Default");

        lineMaterial = new Material(shader)
        {
            name = "TruckGuideLinesMaterial",
            hideFlags = HideFlags.DontSave
        };
    }

    private void CreateLineRenderers()
    {
        GameObject container = new GameObject("GuideLinesContainer");
        container.transform.SetParent(transform, false);

        trailerLeftLine = CreateSingleLine("TrailerRearLeftLine", container.transform, trailerLineColor);
        trailerRightLine = CreateSingleLine("TrailerRearRightLine", container.transform, trailerLineColor);
        tractorLeftLine = CreateSingleLine("TractorDriveLeftLine", container.transform, tractorLineColor);
        tractorRightLine = CreateSingleLine("TractorDriveRightLine", container.transform, tractorLineColor);
        steerLeftLine = CreateSingleLine("TractorSteerLeftLine", container.transform, steerLineColor);
        steerRightLine = CreateSingleLine("TractorSteerRightLine", container.transform, steerLineColor);
    }

    private LineRenderer CreateSingleLine(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.sortingOrder = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.alignment = LineAlignment.TransformZ;

        return lr;
    }

    private void Update()
    {
        bool toggleTriggered = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            toggleTriggered = true;
        }
#else
        if (Input.GetKeyDown(toggleKey))
        {
            toggleTriggered = true;
        }
#endif

        if (toggleTriggered)
        {
            showGuideLines = !showGuideLines;
            UpdateLinesVisibility();
        }
    }

    private void UpdateLinesVisibility()
    {
        SetLineActive(trailerLeftLine, showGuideLines);
        SetLineActive(trailerRightLine, showGuideLines);
        SetLineActive(tractorLeftLine, showGuideLines);
        SetLineActive(tractorRightLine, showGuideLines);
        SetLineActive(steerLeftLine, showGuideLines);
        SetLineActive(steerRightLine, showGuideLines);
    }

    private void SetLineActive(LineRenderer lr, bool active)
    {
        if (lr != null && lr.gameObject != null)
        {
            lr.enabled = active;
        }
    }

    private void LateUpdate()
    {
        if (!showGuideLines) return;

        if (tractorTransform == null || trailerTransform == null)
        {
            ResolveReferences();
        }

        // 1. Trailer Rear Clearance Trajectory Lines (RED)
        if (trailerTransform != null)
        {
            Vector3 trLeftStart = trailerTransform.TransformPoint(new Vector3(-1.26f, -8.05f, 0f));
            Vector3 trRightStart = trailerTransform.TransformPoint(new Vector3(1.26f, -8.05f, 0f));
            Vector3 trBackDir = -trailerTransform.up;

            if (trailerLeftLine != null && trailerLeftLine.enabled)
            {
                trailerLeftLine.SetPosition(0, trLeftStart);
                trailerLeftLine.SetPosition(1, trLeftStart + trBackDir * trailerLineLength);
            }

            if (trailerRightLine != null && trailerRightLine.enabled)
            {
                trailerRightLine.SetPosition(0, trRightStart);
                trailerRightLine.SetPosition(1, trRightStart + trBackDir * trailerLineLength);
            }
        }

        // 2. Tractor Drive Corridor Trajectory Lines (GREEN)
        if (tractorTransform != null)
        {
            Vector3 tcLeftStart = tractorTransform.TransformPoint(new Vector3(-1.12f, -2.9f, 0f));
            Vector3 tcRightStart = tractorTransform.TransformPoint(new Vector3(1.12f, -2.9f, 0f));
            Vector3 tcBackDir = -tractorTransform.up;

            if (tractorLeftLine != null && tractorLeftLine.enabled)
            {
                tractorLeftLine.SetPosition(0, tcLeftStart);
                tractorLeftLine.SetPosition(1, tcLeftStart + tcBackDir * tractorLineLength);
            }

            if (tractorRightLine != null && tractorRightLine.enabled)
            {
                tractorRightLine.SetPosition(0, tcRightStart);
                tractorRightLine.SetPosition(1, tcRightStart + tcBackDir * tractorLineLength);
            }
        }

        // 3. Tractor Front Steer Trajectory Lines (CYAN)
        if (frontLeftWheel != null && steerLeftLine != null && steerLeftLine.enabled)
        {
            Vector3 steerLeftStart = frontLeftWheel.position;
            steerLeftLine.SetPosition(0, steerLeftStart);
            steerLeftLine.SetPosition(1, steerLeftStart + frontLeftWheel.up * steerLineLength);
        }

        if (frontRightWheel != null && steerRightLine != null && steerRightLine.enabled)
        {
            Vector3 steerRightStart = frontRightWheel.position;
            steerRightLine.SetPosition(0, steerRightStart);
            steerRightLine.SetPosition(1, steerRightStart + frontRightWheel.up * steerLineLength);
        }
    }
}
