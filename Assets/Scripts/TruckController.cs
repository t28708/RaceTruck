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
    [Tooltip("Maximum forward maneuvering speed in km/h (limited to 10 km/h)")]
    [SerializeField] private float maxForwardSpeedKmh = 10.0f;
    [Tooltip("Maximum reverse backing speed in km/h (8 km/h)")]
    [SerializeField] private float maxReverseSpeedKmh = 8.0f;
    [Tooltip("Engine acceleration (heavy 36-ton gross weight momentum)")]
    [SerializeField] private float acceleration = 1.6f;
    [Tooltip("Progressive pneumatic air brake deceleration")]
    [SerializeField] private float brakePower = 8.5f;
    [Tooltip("Reverse acceleration (smooth, low-speed backing torque)")]
    [SerializeField] private float reverseAcceleration = 1.2f;
    [Tooltip("Rolling resistance of 18 wheels on asphalt")]
    [SerializeField] private float rollingResistance = 1.6f;

    public float MaxForwardSpeed => maxForwardSpeedKmh / 3.6f;
    public float MaxReverseSpeed => maxReverseSpeedKmh / 3.6f;

    private Rigidbody2D tractorRb;
    private float currentSpeed = 0f;
    private float actualSteerAngle = 0f; // Smooth hydraulic front wheel angle

    // My Trucking Skills Cruise Throttle & Symmetric Braking state
    private bool wPressed = false;
    private bool sPressed = false;
    private bool spacePressed = false;
    private bool wIsBraking = false;
    private bool sIsBraking = false;
    private bool prevWPressed = false;
    private bool prevSPressed = false;

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

        if (maxArticulationAngle <= 0f) maxArticulationAngle = 70f;
    }

    private void OnValidate()
    {
        if (maxArticulationAngle <= 0f) maxArticulationAngle = 70f;
    }

    private void Start()
    {
        FindReferences();
        InitializePositions();
        EnsureLevelSwitcher();
    }

    private void EnsureLevelSwitcher()
    {
        if (FindObjectOfType<LevelSwitcher>() == null)
        {
            gameObject.AddComponent<LevelSwitcher>();
        }
    }

    private bool isBlockedForward = false;
    private bool isBlockedReverse = false;
    private bool isJackknifed = false;
    private float lastJackknifeCrashTime = -1f;
    private const float JackknifeCrashCooldown = 0.5f;

    public bool IsJackknifed => isJackknifed;

    public void OnCrash(string obstacleName, bool forwardImpact)
    {
        // Stop the truck dead in its tracks without bouncing or displacing position
        currentSpeed = 0f;
        wIsBraking = false;
        sIsBraking = false;

        if (forwardImpact)
        {
            isBlockedForward = true;
        }
        else
        {
            isBlockedReverse = true;
        }
    }

    public void ClearBlock()
    {
        // Don't accidentally clear reverse block if the vehicle is currently folded/jackknifed
        if (!isJackknifed)
        {
            isBlockedForward = false;
            isBlockedReverse = false;
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

        // Detect leading edge of W press
        if (wPressed && !prevWPressed)
        {
            // If rolling backward, W acts as a BRAKE to stop
            wIsBraking = (currentSpeed < -0.03f);
        }
        else if (!wPressed)
        {
            wIsBraking = false;
        }

        // Detect leading edge of S press
        if (sPressed && !prevSPressed)
        {
            // If rolling forward, S acts as a BRAKE to stop
            sIsBraking = (currentSpeed > 0.03f);
        }
        else if (!sPressed)
        {
            sIsBraking = false;
        }

        prevWPressed = wPressed;
        prevSPressed = sPressed;

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

        if (currentSpeed > 0.05f)
        {
            gear = "D";
            if (sPressed && sIsBraking) mode = "ТОРМОЗ";
            else if (wPressed) mode = "ГАЗ";
            else mode = "КРУИЗ";
        }
        else if (currentSpeed < -0.05f)
        {
            gear = "R";
            if (wPressed && wIsBraking) mode = "ТОРМОЗ";
            else if (sPressed) mode = "ГАЗ";
            else mode = "КРУИЗ";
        }
        else
        {
            if (spacePressed) mode = "РУЧНИК";
            else mode = "СТОП";
        }

        float steer = actualSteerAngle;
        string steerStr = Mathf.Abs(steer) < 0.5f ? "0°" : $"{Mathf.Abs(steer):0}° {(steer > 0 ? "L" : "R")}";

        float articulation = 0f;
        if (trailerRb != null)
        {
            articulation = Mathf.DeltaAngle(tractorRb.rotation, trailerRb.rotation);
        }

        string camMode = "СВЕРХУ";
        CameraFollow cf = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cf != null && cf.RotateWithTruck) camMode = "ЗА ТРАКОМ";

        string warning = "";
        if (isJackknifed)
        {
            warning = " | <color=#FF1111>💥 СКЛАДЫВАНИЕ! НАЖМИТЕ [W] ДЛЯ ВЫРАВНИВАНИЯ 💥</color>";
        }
        else if (Mathf.Abs(articulation) > maxArticulationAngle * 0.8f)
        {
            warning = $" | <color=#FFAA00>⚠️ ОПАСНОСТЬ СКЛАДЫВАНИЯ ({Mathf.Abs(articulation):0}°/{maxArticulationAngle:0}°)</color>";
        }

        TruckGuideLines gl = GetComponent<TruckGuideLines>();
        string linesStatus = (gl != null && gl.ShowGuideLines) ? "ВКЛ" : "ВЫКЛ";

        hudText.text = $"SPEED: {kmh:0.0} km/h [{gear}:{mode}] | РУЛЬ: {steerStr} | СЦЕПКА: {Mathf.Abs(articulation):0.0}° | [L] ЛИНИИ: {linesStatus} | [C] КАМЕРА: {camMode}{warning}";
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

            // Store for next step
            prevTrailerRearAxlePos = newTrailerCenter + finalTrailerForward * trailerAxleLocalOffset;

            // Apply movement to trailer
            trailerRb.MoveRotation(newTrailerAngleDeg);
            trailerRb.MovePosition(newTrailerCenter);
        }

        // Apply movement to tractor
        tractorRb.MoveRotation(newTractorAngleDeg);
        tractorRb.MovePosition(newTractorCenter);

        // Store hitch position for next step
        prevHitchPos = newHitchPos;
    }

    private void UpdateSpeed(float dt)
    {
        // 1. Emergency handbrake (Space)
        if (spacePressed)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * 1.5f * dt);
            if (Mathf.Abs(currentSpeed) < 0.01f) currentSpeed = 0f;
            return;
        }

        // 2. Both W and S pressed simultaneously -> brake to stop
        if (wPressed && sPressed)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * dt);
            if (Mathf.Abs(currentSpeed) < 0.01f) currentSpeed = 0f;
            return;
        }

        // 3. W (Gas forward / Brake reverse)
        if (wPressed)
        {
            if (wIsBraking)
            {
                // Air brakes while moving backward -> slows to 0
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * dt);
                if (currentSpeed >= 0f)
                {
                    currentSpeed = 0f; // Clamps at 0 without switching to forward
                }
            }
            else
            {
                // Accelerate forward up to MaxForwardSpeed (10 km/h)
                if (isBlockedForward)
                {
                    currentSpeed = 0f;
                }
                else
                {
                    if (!isJackknifed)
                    {
                        isBlockedReverse = false;
                    }
                    currentSpeed = Mathf.MoveTowards(currentSpeed, MaxForwardSpeed, acceleration * dt);
                }
            }
            return;
        }

        // 4. S (Gas reverse / Brake forward)
        if (sPressed)
        {
            if (sIsBraking)
            {
                // Air brakes while moving forward -> slows to 0
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakePower * dt);
                if (currentSpeed <= 0f)
                {
                    currentSpeed = 0f; // Clamps at 0 without switching to reverse
                }
            }
            else
            {
                // Accelerate backward up to MaxReverseSpeed (8 km/h)
                if (isBlockedReverse)
                {
                    currentSpeed = 0f;
                }
                else
                {
                    isBlockedForward = false;
                    currentSpeed = Mathf.MoveTowards(currentSpeed, -MaxReverseSpeed, reverseAcceleration * dt);
                }
            }
            return;
        }

        // 5. Neither key pressed: MAINTAIN CURRENT CRUISE SPEED (My Trucking Skills throttle!)
        // Speed is held constant; no deceleration or rolling drag occurs.
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
        }
        InitializePositions();
    }
}
