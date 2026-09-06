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
    [SerializeField] private float hitchLocalOffset = -2.8f;

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
    [Tooltip("Speed at which the front wheels turn in degrees per second")]
    [SerializeField] private float powerSteeringSpeed = 48f;

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
    private bool wIsBraking = false;
    private bool sIsBraking = false;
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

    // Tracked historical positions for smooth tractrix integration
    private Vector2 prevHitchPos;
    private Vector2 prevTrailerRearAxlePos;
    private bool isInitialized = false;

    public float CurrentSpeed => currentSpeed;
    public float SteerAngle => actualSteerAngle;
    public float TargetSteerAngle => steeringWheel != null ? steeringWheel.CurrentSteerAngle : 0f;
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
    }

    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }
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
            wheel = wheelGo.GetComponent<SteeringWheelUI>();
            if (wheel == null) wheel = wheelGo.AddComponent<SteeringWheelUI>();
        }
        else
        {
            wheelGo = wheel.gameObject;
            wheelGo.transform.SetParent(canvasGo.transform, false);
        }

        wheelGo.SetActive(true);
        RectTransform wheelRect = wheelGo.GetComponent<RectTransform>();
        if (wheelRect == null) wheelRect = wheelGo.AddComponent<RectTransform>();
        wheelRect.anchorMin = new Vector2(1f, 0f);
        wheelRect.anchorMax = new Vector2(1f, 0f);
        wheelRect.pivot = new Vector2(0.5f, 0.5f);
        wheelRect.anchoredPosition = new Vector2(-270f, 270f);
        wheelRect.sizeDelta = new Vector2(480f, 480f);
        wheelRect.localScale = Vector3.one;

        Image wheelImg = wheelGo.GetComponent<Image>();
        if (wheelImg == null) wheelImg = wheelGo.AddComponent<Image>();
        wheelImg.raycastTarget = true;
        wheelImg.preserveAspect = true;
        wheelImg.color = Color.white;
        // Always enforce the exact same realistic steering wheel sprite across all maps!
        wheelImg.sprite = GetOrCreateSprite("SteeringWheelRealistic", GenerateSteeringWheelTexture);

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

        // 3. HUD Text
        if (hudText == null)
        {
            Transform hudTrans = canvasGo.transform.Find("TruckHUDText");
            GameObject hudGo = (hudTrans != null) ? hudTrans.gameObject : null;
            if (hudGo == null)
            {
                hudGo = new GameObject("TruckHUDText");
                hudGo.transform.SetParent(canvasGo.transform, false);
                RectTransform hudRect = hudGo.AddComponent<RectTransform>();
                hudRect.anchorMin = new Vector2(0.5f, 1f);
                hudRect.anchorMax = new Vector2(0.5f, 1f);
                hudRect.pivot = new Vector2(0.5f, 1f);
                hudRect.anchoredPosition = new Vector2(0f, -25f);
                hudRect.sizeDelta = new Vector2(650f, 160f);
                Text t = hudGo.AddComponent<Text>();
                t.alignment = TextAnchor.UpperCenter;
                t.fontSize = 24;
                t.fontStyle = FontStyle.Bold;
                t.color = Color.white;
                hudText = t;
            }
            else
            {
                hudText = hudGo.GetComponent<Text>();
            }
        }

        // 4. Camera Zoom Multiplier Widget (Top-Right: "< 1x >")
        Transform zoomTrans = canvasGo.transform.Find("CameraZoomWidget");
        if (zoomTrans == null)
        {
            CameraZoomUI.CreateZoomWidget(canvasGo);
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
        wIsBraking = false;
        sIsBraking = false;

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
        wIsBraking = false;
        sIsBraking = false;

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
            GameObject trailer = GameObject.Find("Trailer");
            if (trailer != null)
            {
                trailerRb = trailer.GetComponent<Rigidbody2D>();
                if (trailerRb != null)
                {
                    trailerRb.bodyType = RigidbodyType2D.Kinematic;
                    trailerRb.useFullKinematicContacts = true;
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

                // If this is the obstacle we collided with, and we are moving in the ESCAPE direction, ignore it!
                if (col == lastCrashedObstacle)
                {
                    if (isMovingForward && lastCrashDirection == -1) continue;
                    if (!isMovingForward && lastCrashDirection == +1) continue;
                }

                // Relative position to tractor: local Y > 0 is front, local Y < 0 is rear
                Vector2 localObstaclePos = transform.InverseTransformPoint(col.bounds.center);

                // If moving forward and we previously crashed in reverse (lastCrashDirection == -1):
                // We are actively pulling forward to escape!
                // Only obstacles directly in front of the tractor cabin (> 2.0m) can block forward movement.
                if (isMovingForward && lastCrashDirection == -1 && localObstaclePos.y <= 2.0f)
                {
                    continue;
                }

                // If reversing and we previously crashed forward (lastCrashDirection == +1):
                // We are backing away/escaping from a front collision!
                // Tractor is pulling backward away from the front obstacle, so ignore tractor hits.
                if (!isMovingForward && lastCrashDirection == +1)
                {
                    continue;
                }

                // Standard moving away checks:
                // If reversing (speed < 0) and obstacle is in front of tractor, the tractor is moving AWAY!
                if (!isMovingForward && localObstaclePos.y > -0.5f)
                {
                    continue;
                }

                // If moving forward and obstacle is behind tractor (hitch area), ignore
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

                // If this is the obstacle we collided with, and we are moving in the ESCAPE direction, ignore it!
                if (col == lastCrashedObstacle)
                {
                    if (isMovingForward && lastCrashDirection == -1) continue;
                    if (!isMovingForward && lastCrashDirection == +1) continue;
                }

                // Relative position to trailer
                Vector2 localObstaclePos = trailerRb.transform.InverseTransformPoint(col.bounds.center);

                // If moving forward and we previously crashed in reverse (lastCrashDirection == -1):
                // We are pulling forward to escape! The trailer follows the tractor forward.
                // Any obstacle hit in reverse along trailer sides or rear MUST NOT block pulling forward!
                if (isMovingForward && lastCrashDirection == -1)
                {
                    continue;
                }

                // If reversing and we previously crashed forward (lastCrashDirection == +1):
                // We are backing away from front collision!
                // Only obstacles behind trailer rear tandem (< -5.0m) can block reverse.
                if (!isMovingForward && lastCrashDirection == +1 && localObstaclePos.y >= -5.0f)
                {
                    continue;
                }

                // Standard moving away checks:
                // If moving forward and obstacle is behind trailer rear, the trailer is pulling AWAY!
                if (isMovingForward && localObstaclePos.y < 0f)
                {
                    continue;
                }

                // If reversing and obstacle is near front/hitch, ignore
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

        // Detect leading edge of forward press
        if (forwardInput && !prevForwardInput)
        {
            // If rolling backward, pressing forward acts as INSTANT STOP
            if (currentSpeed < -0.01f)
            {
                currentSpeed = 0f;
                wIsBraking = true;
            }
            else
            {
                wIsBraking = false;
            }
        }
        else if (!forwardInput)
        {
            wIsBraking = false;
        }

        // Detect leading edge of reverse press
        if (reverseInput && !prevReverseInput)
        {
            // If rolling forward, pressing reverse acts as INSTANT STOP
            if (currentSpeed > 0.01f)
            {
                currentSpeed = 0f;
                sIsBraking = true;
            }
            else
            {
                sIsBraking = false;
            }
        }
        else if (!reverseInput)
        {
            sIsBraking = false;
        }

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
        if (hudText == null) return;

        float kmh = Mathf.Abs(currentSpeed) * 3.6f;
        string gear = "N";
        string mode = "СТОП";

        bool forwardInput = wPressed || gasPedalPressed;
        bool reverseInput = sPressed || brakePedalPressed;

        if (currentSpeed > 0.05f)
        {
            gear = "D";
            if (reverseInput) mode = "МГНОВЕННЫЙ ТОРМОЗ";
            else if (forwardInput) mode = "ГАЗ (10 км/ч)";
            else mode = "ПЛАВНЫЙ НАКАТ";
        }
        else if (currentSpeed < -0.05f)
        {
            gear = "R";
            if (forwardInput) mode = "МГНОВЕННЫЙ ТОРМОЗ";
            else if (reverseInput) mode = "НАЗАД (10 км/ч)";
            else mode = "ПЛАВНЫЙ НАКАТ";
        }
        else
        {
            if (spacePressed) mode = "РУЧНИК";
            else if (wIsBraking || sIsBraking) mode = "ТОРМОЗ [СТОП]";
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
            warning = " | <color=#FF1111>💥 СКЛАДЫВАНИЕ! НАЖМИТЕ [ГАЗ / W] ДЛЯ ВЫРАВНИВАНИЯ 💥</color>";
        }
        else if (isBlockedReverse)
        {
            warning = " | <color=#FF3333>⚠️ УДАР СЗАДИ! НАЖМИТЕ [ГАЗ / W], ЧТОБЫ ОТЪЕХАТЬ ⚠️</color>";
        }
        else if (isBlockedForward)
        {
            warning = " | <color=#FF3333>⚠️ УДАР СПЕРЕДИ! НАЖМИТЕ [ТОРМОЗ / S], ЧТОБЫ СДАТЬ НАЗАД ⚠️</color>";
        }
        else if (Mathf.Abs(articulation) > maxArticulationAngle * 0.8f)
        {
            warning = $" | <color=#FFAA00>⚠️ ОПАСНОСТЬ СКЛАДЫВАНИЯ ({Mathf.Abs(articulation):0}°/{maxArticulationAngle:0}°)</color>";
        }

        TruckGuideLines gl = GetComponent<TruckGuideLines>();
        string linesStatus = (gl != null && gl.ShowGuideLines) ? "ВКЛ" : "ВЫКЛ";

        hudText.text = $"SPEED: {kmh:0.0} km/h [{gear}:{mode}] | РУЛЬ: {steerStr} | СЦЕПКА: {Mathf.Abs(articulation):0.0}° | [L] ЛИНИИ: {linesStatus} | МАСШТАБ: {camZoom}{warning}";
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
                    string obstacleName = TruckCollisionDetector.FormatObstacleNameStatic(hitObstacle.name, hitObstacle.transform);
                    OnCrash(obstacleName, forwardImpact, hitObstacle);

                    if (TruckCrashEffect.Instance != null)
                    {
                        TruckCrashEffect.Instance.TriggerCrash(obstacleName, hitObstacle.transform.position, forwardImpact);
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
                    string obstacleName = TruckCollisionDetector.FormatObstacleNameStatic(hitObstacle.name, hitObstacle.transform);
                    OnCrash(obstacleName, forwardImpact, hitObstacle);

                    if (TruckCrashEffect.Instance != null)
                    {
                        TruckCrashEffect.Instance.TriggerCrash(obstacleName, hitObstacle.transform.position, forwardImpact);
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

        // 2. Both forward and reverse pressed simultaneously -> instant stop
        if (forwardInput && reverseInput)
        {
            currentSpeed = 0f;
            return;
        }

        // 3. Opposite pedal pressed while in motion -> INSTANT STOP
        if (forwardInput && currentSpeed < -0.01f)
        {
            currentSpeed = 0f;
            wIsBraking = true;
            return;
        }

        if (reverseInput && currentSpeed > 0.01f)
        {
            currentSpeed = 0f;
            sIsBraking = true;
            return;
        }

        // 4. Forward Gas Pedal (Accelerates and holds constant 10.0 km/h)
        if (forwardInput)
        {
            if (wIsBraking)
            {
                currentSpeed = 0f;
            }
            else
            {
                if (isBlockedForward)
                {
                    currentSpeed = 0f;
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, MaxForwardSpeed, acceleration * dt);
                }
            }
            return;
        }

        // 5. Reverse / Brake Pedal (Accelerates and holds constant -10.0 km/h)
        if (reverseInput)
        {
            if (sIsBraking)
            {
                currentSpeed = 0f;
            }
            else
            {
                if (isBlockedReverse)
                {
                    currentSpeed = 0f;
                }
                else
                {
                    currentSpeed = Mathf.MoveTowards(currentSpeed, -MaxReverseSpeed, reverseAcceleration * dt);
                }
            }
            return;
        }

        // 6. Neither pedal pressed: smoothly decelerate to complete stop (smooth coasting)
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
