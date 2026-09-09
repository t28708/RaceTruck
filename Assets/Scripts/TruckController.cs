using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class TruckController : MonoBehaviour
{
    public static TruckController Instance { get; private set; }

    [Header("Articulated Trailer Link")]
    [SerializeField] private Rigidbody2D trailerRb;

    [Header("UI Steering Wheel")]
    [SerializeField] private SteeringWheelUI steeringWheel;

    [Header("Visual Front Wheels")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;

    [Header("HUD")]
    [SerializeField] private Text hudText;

    [Header("Tractor Dimensions (Real Class 8 American Semi)")]
    [Tooltip("Wheelbase L1: Distance from front steer axle to rear drive tandem center (5.8 meters)")]
    [SerializeField] private float wheelbase = 5.8f;
    [Tooltip("Local Y offset of tractor rear drive tandem from center")]
    [SerializeField] private float rearAxleLocalOffset = -3.0f;
    [Tooltip("Local Y offset of 5th wheel hitch plate from center (over drive tandem)")]
    [SerializeField] private float hitchLocalOffset = -2.0f;

    [Header("Trailer Dimensions (Real 53-ft Semi-Trailer)")]
    [Tooltip("Local Y offset of kingpin from trailer center")]
    [SerializeField] private float kingpinLocalOffset = 6.9f;
    [Tooltip("Local Y offset of trailer rear tandem from center")]
    [SerializeField] private float trailerAxleLocalOffset = -5.6f;
    [Tooltip("Trailer Wheelbase L2: Distance from kingpin to trailer tandem (12.5 meters)")]
    [SerializeField] private float trailerWheelbase = 12.5f;
    [Tooltip("Maximum articulation / jackknife angle in degrees")]
    [SerializeField] private float maxArticulationAngle = 107.3f;

    [Header("Steering Dynamics & Hydraulic Inertia")]
    [Tooltip("Maximum front wheel steer angle in degrees")]
    [SerializeField] private float maxSteerAngle = 38f;
    [Tooltip("Power steering hydraulic response speed (deg/s)")]
    [SerializeField] private float powerSteeringSpeed = 120f;

    [Header("Diesel Engine & Driving Dynamics (km/h)")]
    [Tooltip("Maximum forward maneuvering speed in km/h (fixed at 10.0 km/h)")]
    [SerializeField] private float maxForwardSpeedKmh = 10.0f;
    [Tooltip("Maximum reverse backing speed in km/h (fixed at 10.0 km/h)")]
    [SerializeField] private float maxReverseSpeedKmh = 10.0f;
    [Tooltip("Engine acceleration (smooth ramp to 10 km/h)")]
    [SerializeField] private float acceleration = 2.5f;
    [Tooltip("Progressive pneumatic air brake deceleration (smooth stop)")]
    [SerializeField] private float brakePower = 12.0f;
    [Tooltip("Smooth coasting deceleration when pedals are released")]
    [SerializeField] private float coastDeceleration = 1.2f;
    [Tooltip("Reverse acceleration (smooth ramp to 10 km/h)")]
    [SerializeField] private float reverseAcceleration = 2.2f;
    [Tooltip("Rolling resistance of 18 wheels on asphalt")]
    [SerializeField] private float rollingResistance = 1.6f;

    public float MaxForwardSpeed => maxForwardSpeedKmh / 3.6f;
    public float MaxReverseSpeed => maxReverseSpeedKmh / 3.6f;

    private Rigidbody2D tractorRb;
    private float currentSpeed = 0f;
    private float actualSteerAngle = 0f; // Smooth hydraulic front wheel angle

    // Mobile touch pedal & keyboard state
    private bool gasPedalPressed = false;
    private bool brakePedalPressed = false;
    private bool prevGasPedalPressed = false;
    private bool prevBrakePedalPressed = false;

    private bool wPressed = false;
    private bool sPressed = false;
    private bool spacePressed = false;
    private bool prevWPressed = false;
    private bool prevSPressed = false;

    public void SetGasPedal(bool isPressed)
    {
        gasPedalPressed = isPressed;
    }

    public void SetBrakePedal(bool isPressed)
    {
        brakePedalPressed = isPressed;
    }

    public bool IsGasPedalPressed => gasPedalPressed || wPressed;
    public bool IsBrakePedalPressed => brakePedalPressed || sPressed;
    public bool IsWPressed => wPressed;
    public bool IsSPressed => sPressed;

    // Tracked historical positions for smooth tractrix integration
    private Vector2 prevHitchPos;
    private Vector2 prevTrailerRearAxlePos;
    private bool isInitialized = false;

    // Strict Map Perimeter Bounds
    private float mapWidth = 54f;
    private float mapHeight = 60f;
    private bool hasMapBounds = false;

    public float CurrentSpeed => currentSpeed;
    public float SteerAngle => actualSteerAngle;
    public float TargetSteerAngle
    {
        get
        {
            if (steeringWheel == null)
            {
                steeringWheel = Object.FindFirstObjectByType<SteeringWheelUI>();
            }
            return steeringWheel != null ? steeringWheel.CurrentSteerAngle : 0f;
        }
    }
    public Rigidbody2D TrailerRb => trailerRb;
    public Transform FrontLeftWheel => frontLeftWheel;
    public Transform FrontRightWheel => frontRightWheel;

    private void Awake()
    {
        Instance = this;
        tractorRb = GetComponent<Rigidbody2D>();
        tractorRb.bodyType = RigidbodyType2D.Kinematic;
        tractorRb.useFullKinematicContacts = true;
        tractorRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (trailerRb != null)
        {
            trailerRb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        maxForwardSpeedKmh = 10.0f;
        maxReverseSpeedKmh = 10.0f;
        maxArticulationAngle = 107.3f;
    }

    private void OnValidate()
    {
        maxForwardSpeedKmh = 10.0f;
        maxReverseSpeedKmh = 10.0f;
        maxArticulationAngle = 107.3f;
    }

    private void Start()
    {
        maxArticulationAngle = 107.3f;
        EnsureEventSystem();
        EnsureControlsCanvas();
        FindReferences();
        InitializePositions();
        EnsureLevelSwitcher();
        EnsureMapBoundaries();
    }

    private void EnsureEventSystem()
    {
        UnityEngine.EventSystems.EventSystem es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (standalone != null) Destroy(standalone);

        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
#else
        if (es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
        {
            es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
#endif
    }

    private void EnsureLevelSwitcher()
    {
        if (Object.FindFirstObjectByType<LevelSwitcher>() == null)
        {
            gameObject.AddComponent<LevelSwitcher>();
        }
    }

    private void EnsureControlsCanvas()
    {
        Canvas existingCanvas = Object.FindFirstObjectByType<Canvas>();
        GameObject canvasGo = null;

        if (existingCanvas != null)
        {
            canvasGo = existingCanvas.gameObject;
            if (!canvasGo.activeSelf) canvasGo.SetActive(true);
            existingCanvas.enabled = true;
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            existingCanvas.sortingOrder = 100;
        }
        else
        {
            canvasGo = GameObject.Find("TruckControlsCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("TruckControlsCanvas");
                Canvas c = canvasGo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 100;
            }
            else
            {
                canvasGo.SetActive(true);
                Canvas c = canvasGo.GetComponent<Canvas>();
                if (c == null) c = canvasGo.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 100;
                c.enabled = true;
            }
        }

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bool isPortrait = Screen.width < Screen.height;
        scaler.referenceResolution = isPortrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
        {
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        // 1. Steering Wheel (Strictly on bottom-right, identical across all maps)
        SteeringWheelUI[] existingWheels = Object.FindObjectsByType<SteeringWheelUI>(FindObjectsSortMode.None);
        SteeringWheelUI wheel = existingWheels.Length > 0 ? existingWheels[0] : null;

        // Destroy any duplicate extra wheels if they exist
        for (int i = 1; i < existingWheels.Length; i++)
        {
            if (existingWheels[i] != null) Destroy(existingWheels[i].gameObject);
        }

        GameObject wheelGo = null;
        if (wheel == null)
        {
            Transform wheelTrans = canvasGo.transform.Find("SteeringWheel");
            wheelGo = (wheelTrans != null) ? wheelTrans.gameObject : new GameObject("SteeringWheel");
            wheelGo.transform.SetParent(canvasGo.transform, false);

            // Add RectTransform FIRST before adding SteeringWheelUI component
            RectTransform wheelRect = wheelGo.GetComponent<RectTransform>();
            if (wheelRect == null) wheelRect = wheelGo.AddComponent<RectTransform>();

            wheel = wheelGo.GetComponent<SteeringWheelUI>();
            if (wheel == null) wheel = wheelGo.AddComponent<SteeringWheelUI>();
        }
        else
        {
            wheelGo = wheel.gameObject;
            wheelGo.transform.SetParent(canvasGo.transform, false);
        }

        wheelGo.SetActive(true);
        RectTransform wheelRt = wheelGo.GetComponent<RectTransform>();
        if (wheelRt == null) wheelRt = wheelGo.AddComponent<RectTransform>();
        wheelRt.anchorMin = new Vector2(1f, 0f);
        wheelRt.anchorMax = new Vector2(1f, 0f);
        wheelRt.pivot = new Vector2(0.5f, 0.5f);
        wheelRt.anchoredPosition = new Vector2(-270f, 270f);
        wheelRt.sizeDelta = new Vector2(480f, 480f);
        wheelRt.localScale = Vector3.one;

        Image wheelImg = wheelGo.GetComponent<Image>();
        if (wheelImg == null) wheelImg = wheelGo.AddComponent<Image>();
        wheelImg.raycastTarget = true;
        wheelImg.preserveAspect = true;
        wheelImg.sprite = GetOrCreateSprite("SteeringWheelRealistic", GenerateSteeringWheelTexture);

        wheel.FindComponents();
        wheel.SetSpeeds(810f, 1518.75f);
        steeringWheel = wheel;

        // 2. Gas & Brake Pedals (Large, comfortable, strictly on bottom-left, matching MTS reference)
        PedalUI[] allPedals = Object.FindObjectsByType<PedalUI>(FindObjectsSortMode.None);
        PedalUI gasPedal = null;
        PedalUI brakePedal = null;

        foreach (var p in allPedals)
        {
            if (p.Type == PedalUI.PedalType.Gas && gasPedal == null) gasPedal = p;
            else if (p.Type == PedalUI.PedalType.BrakeReverse && brakePedal == null) brakePedal = p;
            else if (p != gasPedal && p != brakePedal)
            {
                Destroy(p.gameObject);
            }
        }

        // Configure Gas Pedal (Upper-Left: 180 x 340)
        if (gasPedal == null)
        {
            Transform gasTrans = canvasGo.transform.Find("Pedal_Gas");
            GameObject gasGo = (gasTrans != null) ? gasTrans.gameObject : new GameObject("Pedal_Gas");
            gasGo.transform.SetParent(canvasGo.transform, false);
            gasPedal = gasGo.GetComponent<PedalUI>();
            if (gasPedal == null) gasPedal = gasGo.AddComponent<PedalUI>();
            gasPedal.SetPedalType(PedalUI.PedalType.Gas);
        }
        else
        {
            gasPedal.transform.SetParent(canvasGo.transform, false);
        }

        gasPedal.gameObject.SetActive(true);
        RectTransform gasRect = gasPedal.GetComponent<RectTransform>();
        if (gasRect == null) gasRect = gasPedal.gameObject.AddComponent<RectTransform>();
        gasRect.anchorMin = new Vector2(0f, 0f);
        gasRect.anchorMax = new Vector2(0f, 0f);
        gasRect.pivot = new Vector2(0.5f, 0.5f);
        gasRect.anchoredPosition = new Vector2(170f, 490f);
        gasRect.sizeDelta = new Vector2(180f, 340f);
        gasRect.localScale = Vector3.one;

        Image gasImg = gasPedal.GetComponent<Image>();
        if (gasImg == null) gasImg = gasPedal.gameObject.AddComponent<Image>();
        gasImg.raycastTarget = true;
        gasImg.preserveAspect = true;
        gasImg.color = new Color(1f, 1f, 1f, 0.90f);
        gasImg.sprite = GetOrCreateSprite("PedalGas", () => GeneratePedalTexture(new Color(0.15f, 0.55f, 0.25f, 0.95f), "GAS"));

        // Configure Brake Pedal (Lower-Left: 340 x 240, wide horizontal)
        if (brakePedal == null)
        {
            Transform brakeTrans = canvasGo.transform.Find("Pedal_Brake");
            GameObject brakeGo = (brakeTrans != null) ? brakeTrans.gameObject : new GameObject("Pedal_Brake");
            brakeGo.transform.SetParent(canvasGo.transform, false);
            brakePedal = brakeGo.GetComponent<PedalUI>();
            if (brakePedal == null) brakePedal = brakeGo.AddComponent<PedalUI>();
            brakePedal.SetPedalType(PedalUI.PedalType.BrakeReverse);
        }
        else
        {
            brakePedal.transform.SetParent(canvasGo.transform, false);
        }

        brakePedal.gameObject.SetActive(true);
        RectTransform brakeRect = brakePedal.GetComponent<RectTransform>();
        if (brakeRect == null) brakeRect = brakePedal.gameObject.AddComponent<RectTransform>();
        brakeRect.anchorMin = new Vector2(0f, 0f);
        brakeRect.anchorMax = new Vector2(0f, 0f);
        brakeRect.pivot = new Vector2(0.5f, 0.5f);
        brakeRect.anchoredPosition = new Vector2(190f, 170f);
        brakeRect.sizeDelta = new Vector2(340f, 240f);
        brakeRect.localScale = Vector3.one;

        Image brakeImg = brakePedal.GetComponent<Image>();
        if (brakeImg == null) brakeImg = brakePedal.gameObject.AddComponent<Image>();
        brakeImg.raycastTarget = true;
        brakeImg.preserveAspect = true;
        brakeImg.color = new Color(1f, 1f, 1f, 0.90f);
        brakeImg.sprite = GetOrCreateSprite("PedalBrake", () => GeneratePedalTexture(new Color(0.65f, 0.15f, 0.15f, 0.95f), "BRAKE"));

        // 3. HUD Plashka (Telemetry Panel with Speed, Steer, Articulation Angle)
        Transform hudPanelTrans = canvasGo.transform.Find("HUD_Panel");
        GameObject hudPanelGo = (hudPanelTrans != null) ? hudPanelTrans.gameObject : null;
        if (hudPanelGo == null)
        {
            Transform oldHud = canvasGo.transform.Find("TruckHUDText");
            if (oldHud != null) Destroy(oldHud.gameObject);

            hudPanelGo = new GameObject("HUD_Panel");
            hudPanelGo.transform.SetParent(canvasGo.transform, false);
            
            RectTransform panelRt = hudPanelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 1f);
            panelRt.anchorMax = new Vector2(0.5f, 1f);
            panelRt.pivot = new Vector2(0.5f, 1f);
            panelRt.anchoredPosition = new Vector2(0f, -20f);
            panelRt.sizeDelta = new Vector2(1050f, 54f);

            Image panelImg = hudPanelGo.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.13f, 0.85f);
            panelImg.raycastTarget = false;

            GameObject hudTextGo = new GameObject("TruckHUDText");
            hudTextGo.transform.SetParent(hudPanelGo.transform, false);
            RectTransform textRt = hudTextGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(15f, 4f);
            textRt.offsetMax = new Vector2(-15f, -4f);

            Text t = hudTextGo.AddComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) t.font = font;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 20;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(1f, 0.95f, 0.8f, 1f);
            t.supportRichText = true;
            t.raycastTarget = false;
            hudText = t;
        }
        else
        {
            hudText = hudPanelGo.GetComponentInChildren<Text>();
            if (hudText != null && hudText.font == null)
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (font != null) hudText.font = font;
            }
        }

        // 4. Camera Zoom Multiplier Widget (Top-Right: "< 1x >")
        Transform zoomTrans = canvasGo.transform.Find("CameraZoomWidget");
        if (zoomTrans == null)
        {
            CameraZoomUI.CreateZoomWidget(canvasGo);
        }

        // 5. In-Game Menu (Top-Left: "☰ МЕНЮ" -> Restart map, Go to main menu)
        if (FindFirstObjectByType<MapSelectMenu>() == null)
        {
            GameObject msmGo = new GameObject("MapSelectMenuController");
            msmGo.AddComponent<MapSelectMenu>();
        }
        if (FindFirstObjectByType<InGameMenu>() == null)
        {
            GameObject igmGo = new GameObject("InGameMenuController");
            igmGo.AddComponent<InGameMenu>();
        }

        // 6. Ensure ParkingTargetZone win detector exists
        EnsureParkingTargetZone();
    }

    private void EnsureParkingTargetZone()
    {
        if (FindFirstObjectByType<ParkingTargetZone>() != null) return;

        GameObject targetSlot = GameObject.Find("TargetParkingSlot");
        if (targetSlot != null)
        {
            targetSlot.AddComponent<ParkingTargetZone>();
            return;
        }

        GameObject arrow = GameObject.Find("TargetParking_YellowArrow");
        if (arrow == null) arrow = GameObject.Find("TargetStall_Arrow");
        if (arrow != null && arrow.transform.parent != null)
        {
            arrow.transform.parent.gameObject.AddComponent<ParkingTargetZone>();
            return;
        }

        GameObject emptyStall = GameObject.Find("Stall_Standard_Empty");
        if (emptyStall != null)
        {
            emptyStall.AddComponent<ParkingTargetZone>();
        }
    }

    private static Sprite GetOrCreateSprite(string resourceName, System.Func<Texture2D> generator)
    {
        Sprite s = Resources.Load<Sprite>(resourceName);
        if (s != null) return s;
        Texture2D tex = generator();
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Texture2D GenerateSteeringWheelTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.46f;
        float thickness = size * 0.08f;
        float hubRadius = size * 0.16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                Color col = Color.clear;

                if (dist <= radius && dist >= radius - thickness)
                {
                    col = new Color(0.18f, 0.18f, 0.22f, 0.95f);
                    if (y > center.y + radius * 0.65f && Mathf.Abs(x - center.x) < size * 0.04f)
                    {
                        col = new Color(0.95f, 0.15f, 0.15f, 1f);
                    }
                }
                else if (dist <= hubRadius)
                {
                    col = new Color(0.25f, 0.25f, 0.3f, 0.95f);
                }
                else if (dist < radius - thickness && dist > hubRadius)
                {
                    float dy = y - center.y;
                    float dx = x - center.x;
                    if (Mathf.Abs(dy) < size * 0.035f) col = new Color(0.3f, 0.3f, 0.35f, 0.9f);
                    if (Mathf.Abs(dx) < size * 0.035f && dy < 0) col = new Color(0.3f, 0.3f, 0.35f, 0.9f);
                }

                colors[y * size + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    private static Texture2D GeneratePedalTexture(Color baseColor, string label)
    {
        int w = 120, h = 200;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color border = new Color(0.85f, 0.88f, 0.92f, 1f);
        Color dark = new Color(0.12f, 0.12f, 0.15f, 0.95f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color col = dark;
                if (x < 4 || x >= w - 4 || y < 4 || y >= h - 4)
                {
                    col = border;
                }
                else if (y % 16 < 4)
                {
                    col = baseColor;
                }
                colors[y * w + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    private bool isBlockedForward = false;
    private bool isBlockedReverse = false;
    private int lastCrashDirection = 0; // +1 = crashed going forward, -1 = crashed going in reverse, 0 = none
    private Collider2D lastCrashedObstacle = null;
    private bool isJackknifed = false;
    private float lastJackknifeCrashTime = -1f;
    private const float JackknifeCrashCooldown = 0.5f;

    private BoxCollider2D tractorCollider;
    private BoxCollider2D trailerCollider;
    private ContactFilter2D obstacleFilter;
    private readonly Collider2D[] candidateHits = new Collider2D[16];

    public bool IsJackknifed => isJackknifed;
    public bool IsBlockedForward => isBlockedForward;
    public bool IsBlockedReverse => isBlockedReverse;
    public int LastCrashDirection => lastCrashDirection;
    public Collider2D LastCrashedObstacle => lastCrashedObstacle;
    public bool IsForwardInputActive => wPressed || gasPedalPressed;
    public bool IsReverseInputActive => sPressed || brakePedalPressed;

    public void OnCrash(string obstacleName, bool forwardImpact, Collider2D hitObstacle = null)
    {
        // Stop the truck dead in its tracks without bouncing or displacing position
        currentSpeed = 0f;

        if (forwardImpact)
        {
            isBlockedForward = true;
            isBlockedReverse = false; // Always allow reversing away from forward collision!
            lastCrashDirection = +1;
        }
        else
        {
            isBlockedReverse = true;
            isBlockedForward = false; // Always allow pulling forward to escape reverse collision!
            lastCrashDirection = -1;
        }

        if (hitObstacle != null)
        {
            lastCrashedObstacle = hitObstacle;
        }
    }

    public void ClearBlock()
    {
        // Don't accidentally clear reverse block if the vehicle is currently folded/jackknifed
        if (!isJackknifed)
        {
            isBlockedForward = false;
            isBlockedReverse = false;
            lastCrashDirection = 0;
            lastCrashedObstacle = null;
        }
    }

    private void HandleJackknifeImpact(bool hitRightSide, Vector2 tractorCenter, Vector2 tractorForward)
    {
        // Stop dead: truck cannot push further into the trailer
        currentSpeed = 0f;
        isBlockedReverse = true;
        isJackknifed = true;

        if (Time.time - lastJackknifeCrashTime > JackknifeCrashCooldown)
        {
            lastJackknifeCrashTime = Time.time;
            Vector2 tractorRight = new Vector2(tractorForward.y, -tractorForward.x);
            Vector2 contactPos = prevHitchPos + (hitRightSide ? tractorRight : -tractorRight) * 1.3f;

            if (TruckCrashEffect.Instance != null)
            {
                TruckCrashEffect.Instance.TriggerJackknifeCrash(contactPos);
            }
        }
    }

    private void FindReferences()
    {
        if (steeringWheel == null)
        {
            steeringWheel = Object.FindFirstObjectByType<SteeringWheelUI>();
        }

        if (trailerRb == null)
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == "Trailer")
                {
                    trailerRb = go.GetComponent<Rigidbody2D>();
                    if (trailerRb != null)
                    {
                        trailerRb.bodyType = RigidbodyType2D.Kinematic;
                        trailerRb.useFullKinematicContacts = true;
                    }
                    break;
                }
            }
        }

        if (frontLeftWheel == null)
        {
            Transform fl = transform.Find("FrontLeftWheel");
            if (fl != null) frontLeftWheel = fl;
        }

        if (frontRightWheel == null)
        {
            Transform fr = transform.Find("FrontRightWheel");
            if (fr != null) frontRightWheel = fr;
        }

        if (hudText == null)
        {
            GameObject hudGo = GameObject.Find("TruckHUDText");
            if (hudGo != null) hudText = hudGo.GetComponent<Text>();
        }

        if (tractorCollider == null)
        {
            tractorCollider = GetComponent<BoxCollider2D>();
        }

        if (trailerCollider == null && trailerRb != null)
        {
            trailerCollider = trailerRb.GetComponent<BoxCollider2D>();
        }

        obstacleFilter = new ContactFilter2D();
        obstacleFilter.useTriggers = true;
        obstacleFilter.useLayerMask = false;
    }

    private bool CheckCandidateCollision(Vector2 candTractorPos, float candTractorAngle, Vector2 candTrailerPos, float candTrailerAngle, out Collider2D hitObstacle)
    {
        hitObstacle = null;
        bool isMovingForward = (currentSpeed > 0f);

        // 1. Check Tractor box at candidate destination
        if (tractorCollider != null)
        {
            Vector2 tractorSize = tractorCollider.size - new Vector2(0.04f, 0.04f);
            int count = Physics2D.OverlapBox(candTractorPos, tractorSize, candTractorAngle, obstacleFilter, candidateHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D col = candidateHits[i];
                if (!IsObstacle(col)) continue;

                if (col == lastCrashedObstacle)
                {
                    if (isMovingForward && lastCrashDirection == -1) continue;
                    if (!isMovingForward && lastCrashDirection == +1) continue;
                }

                // Use the closest point on the collider rather than the center of long walls
                Vector2 contactPoint = col.ClosestPoint(candTractorPos);
                Vector2 localObstaclePos = transform.InverseTransformPoint(contactPoint);

                if (isMovingForward && lastCrashDirection == -1 && localObstaclePos.y <= 2.0f)
                {
                    continue;
                }

                if (!isMovingForward && lastCrashDirection == +1)
                {
                    continue;
                }

                if (!isMovingForward && localObstaclePos.y > -0.5f)
                {
                    continue;
                }

                if (isMovingForward && localObstaclePos.y < -3.5f)
                {
                    continue;
                }

                hitObstacle = col;
                return true;
            }
        }

        // 2. Check Trailer box at candidate destination
        if (trailerCollider != null && trailerRb != null)
        {
            Vector2 trailerSize = trailerCollider.size - new Vector2(0.04f, 0.04f);
            int count = Physics2D.OverlapBox(candTrailerPos, trailerSize, candTrailerAngle, obstacleFilter, candidateHits);
            for (int i = 0; i < count; i++)
            {
                Collider2D col = candidateHits[i];
                if (!IsObstacle(col)) continue;

                if (col == lastCrashedObstacle)
                {
                    if (isMovingForward && lastCrashDirection == -1) continue;
                    if (!isMovingForward && lastCrashDirection == +1) continue;
                }

                Vector2 contactPoint = col.ClosestPoint(candTrailerPos);
                Vector2 localObstaclePos = trailerRb.transform.InverseTransformPoint(contactPoint);

                if (isMovingForward && lastCrashDirection == -1)
                {
                    continue;
                }

                if (!isMovingForward && lastCrashDirection == +1 && localObstaclePos.y >= -5.0f)
                {
                    continue;
                }

                if (isMovingForward && localObstaclePos.y < 0f)
                {
                    continue;
                }

                if (!isMovingForward && localObstaclePos.y > 6.0f)
                {
                    continue;
                }

                hitObstacle = col;
                return true;
            }
        }

        return false;
    }

    private bool IsObstacle(Collider2D col)
    {
        if (col == null || !col.enabled) return false;
        if (TruckCollisionDetector.IsIgnoredObstacle(col)) return false;

        GameObject go = col.gameObject;

        // Ignore player tractor and all its children (visual wheels, guide lines)
        if (go == gameObject || go.transform.IsChildOf(transform)) return false;

        // Ignore player trailer and all its children (wheels)
        if (trailerRb != null)
        {
            if (go == trailerRb.gameObject || go.transform.IsChildOf(trailerRb.transform)) return false;
        }

        return true;
    }

    public void EnsureMapBoundaries()
    {
        float mapW = 54f;
        float mapH = 60f;

        if (MapData.Instance != null)
        {
            mapW = MapData.Instance.mapWidth;
            mapH = MapData.Instance.mapHeight;
        }
        else
        {
            MapData mapData = FindFirstObjectByType<MapData>();
            if (mapData != null)
            {
                mapData.DetectDimensions();
                mapW = mapData.mapWidth;
                mapH = mapData.mapHeight;
            }
            else
            {
                GameObject workspace = GameObject.Find("MapBuilder_Workspace");
                Transform groundTr = workspace != null ? workspace.transform.Find("AsphaltGround") : null;
                if (groundTr == null)
                {
                    GameObject groundGo = GameObject.Find("AsphaltGround");
                    if (groundGo != null) groundTr = groundGo.transform;
                }

                if (groundTr != null)
                {
                    SpriteRenderer sr = groundTr.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.size.x > 5f)
                    {
                        mapW = sr.size.x;
                        mapH = sr.size.y;
                    }
                }
                else
                {
                    GameObject borderRight = GameObject.Find("Border_Right");
                    GameObject borderTop = GameObject.Find("Border_Top");
                    if (borderRight != null && borderRight.transform.position.x > 5f)
                    {
                        mapW = borderRight.transform.position.x;
                    }
                    if (borderTop != null && borderTop.transform.position.y > 5f)
                    {
                        mapH = borderTop.transform.position.y;
                    }
                }
            }
        }

        mapWidth = mapW;
        mapHeight = mapH;

        Debug.Log($"<color=#55ff55>[TruckController] Активные границы сцены: {mapWidth:F1}м x {mapHeight:F1}м</color>");

        GameObject ws = GameObject.Find("MapBuilder_Workspace");
        Transform borderParent = (ws != null) ? ws.transform.Find("YardBorders") : null;
        if (borderParent == null)
        {
            GameObject borderGo = GameObject.Find("YardBorders");
            if (borderGo != null)
            {
                borderParent = borderGo.transform;
            }
            else
            {
                GameObject newBorderParent = new GameObject("YardBorders");
                if (ws != null) newBorderParent.transform.SetParent(ws.transform, false);
                borderParent = newBorderParent.transform;
            }
        }

        // 4 Physical Boundary GameObjects along the perimeter of the map (with bold yellow border lines and 4m thick impenetrable colliders)
        EnsureBorderCollider(borderParent, "Border_Bottom", new Vector3(mapW * 0.5f, 0f, 0f), new Vector2(mapW + 2f, 1.0f), new Vector2(mapW + 10f, 4.0f), new Vector2(0f, -1.5f));
        EnsureBorderCollider(borderParent, "Border_Top", new Vector3(mapW * 0.5f, mapH, 0f), new Vector2(mapW + 2f, 1.0f), new Vector2(mapW + 10f, 4.0f), new Vector2(0f, 1.5f));
        EnsureBorderCollider(borderParent, "Border_Left", new Vector3(0f, mapH * 0.5f, 0f), new Vector2(1.0f, mapH + 2f), new Vector2(4.0f, mapH + 10f), new Vector2(-1.5f, 0f));
        EnsureBorderCollider(borderParent, "Border_Right", new Vector3(mapW, mapH * 0.5f, 0f), new Vector2(1.0f, mapH + 2f), new Vector2(4.0f, mapH + 10f), new Vector2(1.5f, 0f));
    }

    private static Sprite GetWhiteStripeSprite()
    {
        Sprite s = Resources.Load<Sprite>("ParkingStripe");
        if (s != null) return s;
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void EnsureBorderCollider(Transform parent, string name, Vector3 pos, Vector2 visualSize, Vector2 colSize, Vector2 colOffset)
    {
        Transform lineTr = parent.Find(name);
        GameObject borderObj;
        if (lineTr == null)
        {
            borderObj = new GameObject(name);
            borderObj.transform.SetParent(parent, false);
        }
        else
        {
            borderObj = lineTr.gameObject;
        }

        borderObj.transform.position = pos;

        // Visual bold yellow boundary line
        SpriteRenderer sr = borderObj.GetComponent<SpriteRenderer>();
        if (sr == null) sr = borderObj.AddComponent<SpriteRenderer>();
        if (sr.sprite == null) sr.sprite = GetWhiteStripeSprite();
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = visualSize;
        sr.color = new Color(0.98f, 0.82f, 0.12f, 1.0f); // Bold safety yellow
        sr.sortingOrder = -5;

        // Physical collision barrier
        BoxCollider2D col = borderObj.GetComponent<BoxCollider2D>();
        if (col == null) col = borderObj.AddComponent<BoxCollider2D>();
        col.size = colSize;
        col.offset = colOffset;
        col.isTrigger = true;
    }

    private void InitializePositions()
    {
        Vector2 tractorForward = transform.up;
        prevHitchPos = (Vector2)transform.position + tractorForward * hitchLocalOffset;

        if (trailerRb != null)
        {
            Vector2 trailerForward = trailerRb.transform.up;
            prevTrailerRearAxlePos = (Vector2)trailerRb.position + trailerForward * trailerAxleLocalOffset;
        }

        isInitialized = true;
    }

    private void Update()
    {
        // 1. Read W, S, Space input
        wPressed = false;
        sPressed = false;
        spacePressed = false;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            wPressed = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            sPressed = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
            spacePressed = keyboard.spaceKey.isPressed;
        }
#else
        wPressed = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        sPressed = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        spacePressed = Input.GetKey(KeyCode.Space);
#endif

        bool forwardInput = wPressed || gasPedalPressed;
        bool reverseInput = sPressed || brakePedalPressed;
        bool prevForwardInput = prevWPressed || prevGasPedalPressed;
        bool prevReverseInput = prevSPressed || prevBrakePedalPressed;

        prevWPressed = wPressed;
        prevSPressed = sPressed;
        prevGasPedalPressed = gasPedalPressed;
        prevBrakePedalPressed = brakePedalPressed;

        // Hydraulic power steering smoothing
        float targetSteer = TargetSteerAngle;
        actualSteerAngle = Mathf.MoveTowards(actualSteerAngle, targetSteer, powerSteeringSpeed * Time.deltaTime);

        UpdateWheelVisuals();
        UpdateHUD();
    }

    private void UpdateWheelVisuals()
    {
        if (frontLeftWheel != null)
        {
            frontLeftWheel.localEulerAngles = new Vector3(0f, 0f, actualSteerAngle);
        }
        if (frontRightWheel != null)
        {
            frontRightWheel.localEulerAngles = new Vector3(0f, 0f, actualSteerAngle);
        }
    }

    private void UpdateHUD()
    {
        if (hudText == null)
        {
            GameObject hudGo = GameObject.Find("TruckHUDText");
            if (hudGo != null) hudText = hudGo.GetComponent<Text>();
        }

        if (hudText == null) return;

        if (hudText.font == null)
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f != null) hudText.font = f;
        }

        float kmh = Mathf.Abs(currentSpeed) * 3.6f;
        string gear = "N";
        string mode = "СТОП";

        bool forwardInput = wPressed || gasPedalPressed;
        bool reverseInput = sPressed || brakePedalPressed;

        if (currentSpeed > 0.05f)
        {
            gear = "D";
            if (reverseInput) mode = "ТОРМОЖЕНИЕ";
            else if (forwardInput) mode = "ГАЗ (10 км/ч)";
            else mode = "НАКАТ";
        }
        else if (currentSpeed < -0.05f)
        {
            gear = "R";
            if (forwardInput) mode = "ТОРМОЖЕНИЕ";
            else if (reverseInput) mode = "НАЗАД (10 км/ч)";
            else mode = "НАКАТ";
        }
        else
        {
            if (spacePressed) mode = "РУЧНИК";
            else if (forwardInput) mode = "ВПЕРЕД";
            else if (reverseInput) mode = "НАЗАД";
            else mode = "СТОП";
        }

        float steer = actualSteerAngle;
        string steerStr = Mathf.Abs(steer) < 0.5f ? "0°" : $"{Mathf.Abs(steer):0}° {(steer > 0 ? "L" : "R")}";

        float articulation = 0f;
        if (trailerRb != null)
        {
            articulation = Mathf.DeltaAngle(tractorRb.rotation, trailerRb.rotation);
        }

        string camZoom = "1x";
        CameraFollow cf = CameraFollow.Instance;
        if (cf == null && Camera.main != null) cf = Camera.main.GetComponent<CameraFollow>();
        if (cf != null) camZoom = $"{cf.ZoomMultiplier}x";

        string warning = "";
        if (isJackknifed)
        {
            warning = " | <color=#FF2222>💥 СКЛАДЫВАНИЕ! НАЖМИТЕ [ГАЗ / W] 💥</color>";
        }
        else if (isBlockedReverse)
        {
            warning = " | <color=#FF4444>⚠️ УДАР СЗАДИ! НАЖМИТЕ [ГАЗ / W] ⚠️</color>";
        }
        else if (isBlockedForward)
        {
            warning = " | <color=#FF4444>⚠️ УДАР СПЕРЕДИ! НАЖМИТЕ [ТОРМОЗ / S] ⚠️</color>";
        }
        else if (Mathf.Abs(articulation) > maxArticulationAngle * 0.8f)
        {
            warning = $" | <color=#FFAA00>⚠️ ОПАСНОСТЬ СКЛАДЫВАНИЯ ({Mathf.Abs(articulation):0}°/{maxArticulationAngle:0}°)</color>";
        }

        hudText.text = $"СКОРОСТЬ: <color=#55FFFF>{kmh:0.0} км/ч</color> [{gear}:{mode}] | РУЛЬ: <color=#FFFF55>{steerStr}</color> | СЦЕПКА: <color=#55FF55>{Mathf.Abs(articulation):0.0}°</color> | ЗУМ: <color=#FFAA55>{camZoom}</color>{warning}";
    }

    private void FixedUpdate()
    {
        if (!isInitialized) InitializePositions();

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        // 1. Throttle / Diesel Speed Dynamics
        UpdateSpeed(dt);

        // 2. Tractor Ackermann Steering Kinematics around rear tandem
        float steerRad = actualSteerAngle * Mathf.Deg2Rad;
        float distance = currentSpeed * dt;

        // Angular velocity of tractor: omega = (v / L1) * tan(steer)
        float tractorOmegaRad = 0f;
        if (Mathf.Abs(wheelbase) > 0.001f)
        {
            tractorOmegaRad = (currentSpeed / wheelbase) * Mathf.Tan(steerRad);
        }

        float deltaTractorAngleDeg = tractorOmegaRad * Mathf.Rad2Deg * dt;
        float newTractorAngleDeg = tractorRb.rotation + deltaTractorAngleDeg;

        // Use mid-angle integration for superior curvature precision
        float midAngleRad = (tractorRb.rotation + deltaTractorAngleDeg * 0.5f) * Mathf.Deg2Rad;
        Vector2 midForward = new Vector2(-Mathf.Sin(midAngleRad), Mathf.Cos(midAngleRad));

        // Rear tandem moves along midForward
        Vector2 currentTractorForward = transform.up;
        Vector2 currentRearAxlePos = (Vector2)tractorRb.position + currentTractorForward * rearAxleLocalOffset;
        Vector2 newRearAxlePos = currentRearAxlePos + midForward * distance;

        // New tractor center position
        float newTractorAngleRad = newTractorAngleDeg * Mathf.Deg2Rad;
        Vector2 newTractorForward = new Vector2(-Mathf.Sin(newTractorAngleRad), Mathf.Cos(newTractorAngleRad));
        Vector2 newTractorCenter = newRearAxlePos - newTractorForward * rearAxleLocalOffset;

        // Hitch position on tractor
        Vector2 newHitchPos = newTractorCenter + newTractorForward * hitchLocalOffset;
        Vector2 hitchDelta = newHitchPos - prevHitchPos;

        // 3. Trailer Tractrix Kinematics (Real 53ft Trailer Delay & Off-Tracking)
        if (trailerRb != null)
        {
            Vector2 currentTrailerForward = trailerRb.transform.up;

            // Trailer rear tandem rolls only along longitudinal axis
            float deltaRearAxle = Vector2.Dot(hitchDelta, currentTrailerForward);
            Vector2 newTrailerRearAxle = prevTrailerRearAxlePos + currentTrailerForward * deltaRearAxle;

            // Heading vector points from trailer rear tandem to the hitch
            Vector2 toHitch = newHitchPos - newTrailerRearAxle;
            float newTrailerAngleDeg = trailerRb.rotation;

            if (toHitch.sqrMagnitude > 0.0001f)
            {
                Vector2 newTrailerDir = toHitch.normalized;
                float rawAngle = Mathf.Atan2(-newTrailerDir.x, newTrailerDir.y) * Mathf.Rad2Deg;

                // Continuity protection: prevent discontinuous 180-degree jumping
                float stepDelta = Mathf.DeltaAngle(trailerRb.rotation, rawAngle);
                if (Mathf.Abs(stepDelta) < 30f)
                {
                    newTrailerAngleDeg = rawAngle;
                }
                else
                {
                    // Fallback to differential lateral step
                    Vector2 currentTrailerRight = trailerRb.transform.right;
                    float deltaLateral = Vector2.Dot(hitchDelta, currentTrailerRight);
                    float safeDelta = -(deltaLateral / trailerWheelbase) * Mathf.Rad2Deg;
                    newTrailerAngleDeg = trailerRb.rotation + Mathf.Clamp(safeDelta, -10f, 10f);
                }
            }

            // Real-world physical jackknife limit matching visual contact at 107.3 degrees
            float deltaAngle = Mathf.DeltaAngle(newTractorAngleDeg, newTrailerAngleDeg);
            if (deltaAngle > maxArticulationAngle)
            {
                newTrailerAngleDeg = newTractorAngleDeg + maxArticulationAngle;
                HandleJackknifeImpact(true, newTractorCenter, newTractorForward);
            }
            else if (deltaAngle < -maxArticulationAngle)
            {
                newTrailerAngleDeg = newTractorAngleDeg - maxArticulationAngle;
                HandleJackknifeImpact(false, newTractorCenter, newTractorForward);
            }
            else
            {
                // Unjackknife when vehicle pulls forward and angle straightens by >= 4 degrees
                if (isJackknifed && Mathf.Abs(deltaAngle) < maxArticulationAngle - 4f)
                {
                    isJackknifed = false;
                    isBlockedReverse = false;
                }
            }

            float finalTrailerAngleRad = newTrailerAngleDeg * Mathf.Deg2Rad;
            Vector2 finalTrailerForward = new Vector2(-Mathf.Sin(finalTrailerAngleRad), Mathf.Cos(finalTrailerAngleRad));

            // Geometric lock: Trailer kingpin is locked exactly to 5th wheel hitch
            Vector2 newTrailerCenter = newHitchPos - finalTrailerForward * kingpinLocalOffset;

            // Predictive collision check before applying movement
            if (Mathf.Abs(currentSpeed) > 0.01f)
            {
                if (CheckCandidateCollision(newTractorCenter, newTractorAngleDeg, newTrailerCenter, newTrailerAngleDeg, out Collider2D hitObstacle))
                {
                    bool forwardImpact = (currentSpeed > 0f);
                    string obstacleName = (hitObstacle != null) ? TruckCollisionDetector.FormatObstacleNameStatic(hitObstacle.name, hitObstacle.transform) : "границу площадки";
                    OnCrash(obstacleName, forwardImpact, hitObstacle);

                    if (TruckCrashEffect.Instance != null)
                    {
                        Vector3 impactPos = (hitObstacle != null) ? hitObstacle.transform.position : (forwardImpact ? (Vector3)newTractorCenter : (Vector3)newTrailerCenter);
                        TruckCrashEffect.Instance.TriggerCrash(obstacleName, impactPos, forwardImpact);
                    }

                    currentSpeed = 0f;
                    return; // Refuse penetration! Keep current position.
                }
            }

            // Store for next step
            prevTrailerRearAxlePos = newTrailerCenter + finalTrailerForward * trailerAxleLocalOffset;

            // Apply movement to trailer
            trailerRb.MoveRotation(newTrailerAngleDeg);
            trailerRb.MovePosition(newTrailerCenter);
        }
        else
        {
            // Solo tractor candidate check
            if (Mathf.Abs(currentSpeed) > 0.01f)
            {
                if (CheckCandidateCollision(newTractorCenter, newTractorAngleDeg, Vector2.zero, 0f, out Collider2D hitObstacle))
                {
                    bool forwardImpact = (currentSpeed > 0f);
                    string obstacleName = (hitObstacle != null) ? TruckCollisionDetector.FormatObstacleNameStatic(hitObstacle.name, hitObstacle.transform) : "границу площадки";
                    OnCrash(obstacleName, forwardImpact, hitObstacle);

                    if (TruckCrashEffect.Instance != null)
                    {
                        Vector3 impactPos = (hitObstacle != null) ? hitObstacle.transform.position : (forwardImpact ? (Vector3)newTractorCenter : (Vector3)prevTrailerRearAxlePos);
                        TruckCrashEffect.Instance.TriggerCrash(obstacleName, impactPos, forwardImpact);
                    }

                    currentSpeed = 0f;
                    return;
                }
            }
        }

        // Apply movement to tractor
        tractorRb.MoveRotation(newTractorAngleDeg);
        tractorRb.MovePosition(newTractorCenter);

        // Store hitch position for next step
        prevHitchPos = newHitchPos;

        // Automatically clear collision blocks once the entire rig has completely moved away from all obstacles
        if ((isBlockedForward || isBlockedReverse) && !isJackknifed)
        {
            bool isTouching = false;
            if (tractorCollider != null)
            {
                int tCount = tractorCollider.Overlap(obstacleFilter, candidateHits);
                for (int i = 0; i < tCount; i++)
                {
                    if (IsObstacle(candidateHits[i])) { isTouching = true; break; }
                }
            }
            if (!isTouching && trailerCollider != null)
            {
                int trCount = trailerCollider.Overlap(obstacleFilter, candidateHits);
                for (int i = 0; i < trCount; i++)
                {
                    if (IsObstacle(candidateHits[i])) { isTouching = true; break; }
                }
            }

            if (!isTouching)
            {
                ClearBlock();
            }
        }
    }

    private void UpdateSpeed(float dt)
    {
        bool forwardInput = wPressed || gasPedalPressed;
        bool reverseInput = sPressed || brakePedalPressed;

        // 1. Emergency handbrake (Space) -> instant stop
        if (spacePressed)
        {
            currentSpeed = 0f;
            return;
        }

        // 2. Both forward and reverse pressed simultaneously -> brake firmly to 0
        if (forwardInput && reverseInput)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * dt);
            if (Mathf.Abs(currentSpeed) < 0.01f) currentSpeed = 0f;
            return;
        }

        // 3. Forward Gas Pedal (Accelerates forward; if rolling backward, brakes to 0 and then moves forward)
        if (forwardInput)
        {
            if (isBlockedForward)
            {
                currentSpeed = 0f;
            }
            else
            {
                // If moving backward, apply strong braking force towards 0, then smoothly accelerate forward
                float rate = (currentSpeed < 0f) ? brakePower : acceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, MaxForwardSpeed, rate * dt);
            }
            return;
        }

        // 4. Reverse / Brake Pedal (Accelerates backward; if rolling forward, brakes to 0 and then moves backward)
        if (reverseInput)
        {
            if (isBlockedReverse)
            {
                currentSpeed = 0f;
            }
            else
            {
                // If moving forward, apply strong braking force towards 0, then smoothly accelerate in reverse
                float rate = (currentSpeed > 0f) ? brakePower : reverseAcceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, -MaxReverseSpeed, rate * dt);
            }
            return;
        }

        // 5. Neither pedal pressed: smoothly decelerate to complete stop (smooth coasting)
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, coastDeceleration * dt);
        if (Mathf.Abs(currentSpeed) < 0.01f) currentSpeed = 0f;
    }

    public void SetupWheelReferences(Transform fl, Transform fr)
    {
        frontLeftWheel = fl;
        frontRightWheel = fr;
    }

    public void SetupTrailer(Rigidbody2D trailer)
    {
        trailerRb = trailer;
        if (trailerRb != null)
        {
            trailerRb.bodyType = RigidbodyType2D.Kinematic;
            trailerRb.useFullKinematicContacts = true;
            trailerCollider = trailerRb.GetComponent<BoxCollider2D>();
        }
        InitializePositions();
    }
}
