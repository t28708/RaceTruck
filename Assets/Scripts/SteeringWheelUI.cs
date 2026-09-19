using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum SteeringControlType
{
    Buttons = 0, // Стрелки (Кнопки)
    Slider = 1,  // Слайдер (Линия)
    Wheel = 2    // Круглый руль
}

public class SteeringWheelUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static SteeringWheelUI Instance { get; private set; }

    public enum PedalSide
    {
        Left = 0,    // Педали слева, руль/кнопки справа
        Right = 1    // Педали справа, руль/кнопки слева
    }

    public const string PrefKey_ControlType = "SteeringControlType";
    public const string PrefKey_PedalSide = "PedalSide";
    public const string PrefKey_AutoCenter = "SteeringAutoCenter";

    [Header("Wheel Settings")]
    [Tooltip("Maximum rotation angle of the steering wheel in degrees (e.g., 450 = 1.25 turns each side)")]
    [SerializeField] private float maxWheelAngle = 450f;

    [Tooltip("Maximum front wheel turn angle in degrees")]
    [SerializeField] private float maxSteerAngle = 40f;

    [Tooltip("Rotation speed in degrees per second when turning via A and D keys or touch buttons")]
    [SerializeField] private float keyTurnSpeed = 1215f;

    [Tooltip("Speed to return to center when released (spring return to 0)")]
    [SerializeField] private float returnToCenterSpeed = 2278.125f;

    [Tooltip("Center deadzone in degrees for round wheel (3-5 deg) where steer is strictly straight")]
    [SerializeField] private float wheelDeadzoneAngle = 4.0f;

    [Header("Control Mode")]
    [SerializeField] private SteeringControlType controlType = SteeringControlType.Buttons;

    [Header("UI References")]
    [SerializeField] private RectTransform wheelRectTransform;
    [SerializeField] private GameObject buttonsContainerGo;
    [SerializeField] private GameObject sliderContainerGo;
    [SerializeField] private SteerTouchSlider touchSliderScript;

    private float currentWheelAngle = 0f;
    private float previousPointerAngle = 0f;
    private bool isDragging = false;
    private float touchButtonInput = 0f;
    private bool leftBtnPressed = false;
    private bool rightBtnPressed = false;
    private bool isSliderDragging = false;

    private const float SliderTrackHalfWidth = 280f;

    public static SteeringControlType CurrentControlType
    {
        get
        {
            if (Instance != null) return Instance.controlType;
            return (SteeringControlType)PlayerPrefs.GetInt(PrefKey_ControlType, (int)SteeringControlType.Buttons);
        }
    }

    public static PedalSide CurrentPedalSide
    {
        get
        {
            return (PedalSide)PlayerPrefs.GetInt(PrefKey_PedalSide, (int)PedalSide.Left);
        }
    }

    public static PedalSide TogglePedalSide()
    {
        PedalSide next = (CurrentPedalSide == PedalSide.Left) ? PedalSide.Right : PedalSide.Left;
        SetPedalSide(next);
        return next;
    }

    public static void SetPedalSide(PedalSide side)
    {
        PlayerPrefs.SetInt(PrefKey_PedalSide, (int)side);
        PlayerPrefs.Save();
        ApplyPedalSideLayout(side);
    }

    public static bool AutoCenterEnabled
    {
        get
        {
            return PlayerPrefs.GetInt(PrefKey_AutoCenter, 1) == 1;
        }
        set
        {
            PlayerPrefs.SetInt(PrefKey_AutoCenter, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ToggleAutoCenter()
    {
        bool newVal = !AutoCenterEnabled;
        AutoCenterEnabled = newVal;
        return newVal;
    }

    public static void ApplyPedalSideLayout(PedalSide side)
    {
        bool isLeft = (side == PedalSide.Left);

        // 1. Steering Wheel (Opposite of pedals)
        if (Instance != null && Instance.wheelRectTransform != null)
        {
            Instance.wheelRectTransform.anchorMin = isLeft ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            Instance.wheelRectTransform.anchorMax = isLeft ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            Instance.wheelRectTransform.anchoredPosition = isLeft ? new Vector2(-270f, 270f) : new Vector2(270f, 270f);
        }

        // 2. Steering Buttons Container (Opposite of pedals)
        if (Instance != null && Instance.buttonsContainerGo != null)
        {
            RectTransform btnRt = Instance.buttonsContainerGo.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchorMin = isLeft ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
                btnRt.anchorMax = isLeft ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
                btnRt.anchoredPosition = isLeft ? new Vector2(-280f, 240f) : new Vector2(280f, 240f);
            }
        }

        // 3. Steering Slider Container (Always stays centered at top)
        if (Instance != null && Instance.sliderContainerGo != null)
        {
            RectTransform sliderRt = Instance.sliderContainerGo.GetComponent<RectTransform>();
            if (sliderRt != null)
            {
                sliderRt.anchorMin = new Vector2(0.5f, 1f);
                sliderRt.anchorMax = new Vector2(0.5f, 1f);
                sliderRt.anchoredPosition = new Vector2(0f, -125f);
            }
        }

        // 4. Gas & Brake Pedals
        PedalUI[] pedals = Object.FindObjectsByType<PedalUI>(FindObjectsSortMode.None);
        foreach (var p in pedals)
        {
            if (p == null) continue;
            RectTransform rt = p.GetComponent<RectTransform>();
            if (rt == null) continue;

            if (p.Type == PedalUI.PedalType.Gas)
            {
                rt.anchorMin = isLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                rt.anchorMax = isLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                rt.anchoredPosition = isLeft ? new Vector2(170f, 490f) : new Vector2(-170f, 490f);
            }
            else if (p.Type == PedalUI.PedalType.BrakeReverse)
            {
                rt.anchorMin = isLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                rt.anchorMax = isLeft ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
                rt.anchoredPosition = isLeft ? new Vector2(190f, 170f) : new Vector2(-190f, 170f);
            }
        }
    }

    public float KeyTurnSpeed
    {
        get => keyTurnSpeed;
        set => keyTurnSpeed = value;
    }

    public float ReturnToCenterSpeed
    {
        get => returnToCenterSpeed;
        set => returnToCenterSpeed = value;
    }

    public void SetSpeeds(float turnSpeed, float returnSpeed)
    {
        keyTurnSpeed = turnSpeed;
        returnToCenterSpeed = returnSpeed;
    }

    public RectTransform WheelRect
    {
        get
        {
            if (wheelRectTransform == null)
            {
                wheelRectTransform = GetComponent<RectTransform>();
                if (wheelRectTransform == null) wheelRectTransform = gameObject.AddComponent<RectTransform>();
            }
            return wheelRectTransform;
        }
    }

    /// <summary>
    /// Current front wheel steering angle in degrees (-maxSteerAngle to +maxSteerAngle).
    /// Positive = Left, Negative = Right.
    /// </summary>
    public float CurrentSteerAngle
    {
        get
        {
            if (controlType == SteeringControlType.Wheel)
            {
                // 3-5 degrees deadzone in center for round wheel to prevent minor finger jitters
                if (Mathf.Abs(currentWheelAngle) <= wheelDeadzoneAngle)
                {
                    return 0f;
                }
                float sign = Mathf.Sign(currentWheelAngle);
                float activeRange = maxWheelAngle - wheelDeadzoneAngle;
                float angleBeyondDeadzone = Mathf.Abs(currentWheelAngle) - wheelDeadzoneAngle;
                return sign * (angleBeyondDeadzone / activeRange) * maxSteerAngle;
            }
            else
            {
                return (currentWheelAngle / maxWheelAngle) * maxSteerAngle;
            }
        }
    }

    /// <summary>
    /// Normalized steer value from -1 (full right) to +1 (full left).
    /// </summary>
    public float NormalizedSteer
    {
        get
        {
            if (maxSteerAngle <= 0.001f) return 0f;
            return CurrentSteerAngle / maxSteerAngle;
        }
    }

    public float CurrentWheelAngle => currentWheelAngle;
    public bool IsDragging => isDragging;
    public SteeringControlType ControlType => controlType;

    private Text btnLeftText;
    private Text btnRightText;
    private Text sliderLeftText;
    private Text sliderRightText;

    private void Awake()
    {
        Instance = this;
        controlType = (SteeringControlType)PlayerPrefs.GetInt(PrefKey_ControlType, (int)SteeringControlType.Buttons);
        FindComponents();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += UpdateLocalizedLabels;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdateLocalizedLabels;
    }

    public void UpdateLocalizedLabels()
    {
        if (btnLeftText != null) btnLeftText.text = LocalizationManager.Get("STEER_LEFT_MULTILINE");
        if (btnRightText != null) btnRightText.text = LocalizationManager.Get("STEER_RIGHT_MULTILINE");
        if (sliderLeftText != null) sliderLeftText.text = LocalizationManager.Get("STEER_LEFT_LABEL");
        if (sliderRightText != null) sliderRightText.text = LocalizationManager.Get("STEER_RIGHT_LABEL");
    }

    private void Start()
    {
        FindComponents();
        EnsureAllControls();
        ApplyPedalSideLayout(CurrentPedalSide);
        UpdateControlsVisibility();
        UpdateWheelVisual();
        UpdateLocalizedLabels();
    }

    public void FindComponents()
    {
        if (wheelRectTransform == null)
        {
            wheelRectTransform = GetComponent<RectTransform>();
        }
        if (wheelRectTransform != null && wheelRectTransform.localScale.sqrMagnitude < 0.001f)
        {
            wheelRectTransform.localScale = Vector3.one;
        }
    }

    public static string GetControlTypeName(SteeringControlType type)
    {
        switch (type)
        {
            case SteeringControlType.Buttons:
                return LocalizationManager.Get("CONTROL_BUTTONS");
            case SteeringControlType.Slider:
                return LocalizationManager.Get("CONTROL_SLIDER");
            case SteeringControlType.Wheel:
                return LocalizationManager.Get("CONTROL_WHEEL");
            default:
                return LocalizationManager.Get("CONTROL_BUTTONS");
        }
    }

    public static SteeringControlType CycleControlType()
    {
        SteeringControlType cur = CurrentControlType;
        SteeringControlType next = (SteeringControlType)(((int)cur + 1) % 3);
        if (Instance != null)
        {
            Instance.SetControlType(next);
        }
        else
        {
            PlayerPrefs.SetInt(PrefKey_ControlType, (int)next);
            PlayerPrefs.Save();
        }
        return next;
    }

    public void SetControlType(SteeringControlType newType)
    {
        controlType = newType;
        PlayerPrefs.SetInt(PrefKey_ControlType, (int)newType);
        PlayerPrefs.Save();
        EnsureAllControls();
        UpdateControlsVisibility();
        ResetWheel();
    }

    public void EnsureAllControls()
    {
        Transform canvasTransform = transform.parent;
        if (canvasTransform == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Steering Buttons Container (Left / Right Arrow Buttons on bottom right)
        Transform existingButtons = canvasTransform.Find("SteeringButtonsContainer");
        if (existingButtons == null)
        {
            buttonsContainerGo = new GameObject("SteeringButtonsContainer");
            buttonsContainerGo.transform.SetParent(canvasTransform, false);

            RectTransform containerRt = buttonsContainerGo.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(1f, 0f);
            containerRt.anchorMax = new Vector2(1f, 0f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.anchoredPosition = new Vector2(-280f, 240f);
            containerRt.sizeDelta = new Vector2(500f, 260f);

            // Left Button (◀ ВЛЕВО)
            btnLeftText = CreateSteerButton(buttonsContainerGo.transform, "Btn_SteerLeft", SteerTouchButton.Direction.Left,
                new Vector2(-130f, 0f), "◀\nВЛЕВО", font);

            // Right Button (▶ ВПРАВО)
            btnRightText = CreateSteerButton(buttonsContainerGo.transform, "Btn_SteerRight", SteerTouchButton.Direction.Right,
                new Vector2(130f, 0f), "▶\nВПРАВО", font);
        }
        else
        {
            buttonsContainerGo = existingButtons.gameObject;
            Transform btnL = buttonsContainerGo.transform.Find("Btn_SteerLeft");
            if (btnL != null) btnLeftText = btnL.GetComponentInChildren<Text>();
            Transform btnR = buttonsContainerGo.transform.Find("Btn_SteerRight");
            if (btnR != null) btnRightText = btnR.GetComponentInChildren<Text>();
        }

        // 2. Steering Slider Container (Horizontal Line bar near top)
        Transform existingSlider = canvasTransform.Find("SteeringSliderContainer");
        if (existingSlider == null)
        {
            sliderContainerGo = new GameObject("SteeringSliderContainer");
            sliderContainerGo.transform.SetParent(canvasTransform, false);

            RectTransform sliderRt = sliderContainerGo.AddComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.5f, 1f);
            sliderRt.anchorMax = new Vector2(0.5f, 1f);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.anchoredPosition = new Vector2(0f, -125f);
            sliderRt.sizeDelta = new Vector2(760f, 90f);

            // Track Background
            GameObject trackGo = new GameObject("Slider_Track");
            trackGo.transform.SetParent(sliderContainerGo.transform, false);
            RectTransform trackRt = trackGo.AddComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.5f, 0.5f);
            trackRt.anchorMax = new Vector2(0.5f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.sizeDelta = new Vector2(660f, 38f);
            trackRt.anchoredPosition = Vector2.zero;

            Image trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0.10f, 0.14f, 0.20f, 0.88f);
            trackImg.raycastTarget = true;

            Outline trackOutline = trackGo.AddComponent<Outline>();
            trackOutline.effectColor = new Color(0.30f, 0.50f, 0.80f, 0.85f);
            trackOutline.effectDistance = new Vector2(2f, -2f);

            // Center Notch (0 Position)
            GameObject notchGo = new GameObject("CenterNotch");
            notchGo.transform.SetParent(trackGo.transform, false);
            RectTransform notchRt = notchGo.AddComponent<RectTransform>();
            notchRt.anchorMin = new Vector2(0.5f, 0.5f);
            notchRt.anchorMax = new Vector2(0.5f, 0.5f);
            notchRt.pivot = new Vector2(0.5f, 0.5f);
            notchRt.sizeDelta = new Vector2(4f, 48f);
            notchRt.anchoredPosition = Vector2.zero;
            Image notchImg = notchGo.AddComponent<Image>();
            notchImg.color = new Color(1.0f, 0.85f, 0.20f, 0.95f);
            notchImg.raycastTarget = false;

            // Left Label
            sliderLeftText = CreateSliderLabel(trackGo.transform, "Label_Left", "◀ ВЛЕВО", new Vector2(-370f, 0f), font);
            // Right Label
            sliderRightText = CreateSliderLabel(trackGo.transform, "Label_Right", "ВПРАВО ▶", new Vector2(370f, 0f), font);

            // Handle
            GameObject handleGo = new GameObject("Slider_Handle");
            handleGo.transform.SetParent(trackGo.transform, false);
            RectTransform handleRt = handleGo.AddComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0.5f, 0.5f);
            handleRt.anchorMax = new Vector2(0.5f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(74f, 74f);
            handleRt.anchoredPosition = Vector2.zero;

            Image handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(0.20f, 0.65f, 1.00f, 0.95f);
            handleImg.raycastTarget = false;

            Outline handleOutline = handleGo.AddComponent<Outline>();
            handleOutline.effectColor = new Color(1f, 1f, 1f, 0.95f);
            handleOutline.effectDistance = new Vector2(2f, -2f);

            GameObject handleIconGo = new GameObject("Icon");
            handleIconGo.transform.SetParent(handleGo.transform, false);
            RectTransform iconRt = handleIconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.sizeDelta = Vector2.zero;
            Text iconText = handleIconGo.AddComponent<Text>();
            if (font != null) iconText.font = font;
            iconText.fontSize = 32;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = Color.white;
            iconText.text = "↔";

            touchSliderScript = trackGo.AddComponent<SteerTouchSlider>();
            touchSliderScript.Init(trackRt, handleRt, handleImg, SliderTrackHalfWidth);
        }
        else
        {
            sliderContainerGo = existingSlider.gameObject;
            RectTransform existingSliderRt = sliderContainerGo.GetComponent<RectTransform>();
            if (existingSliderRt != null)
            {
                existingSliderRt.anchoredPosition = new Vector2(0f, -125f);
            }
            touchSliderScript = sliderContainerGo.GetComponentInChildren<SteerTouchSlider>();
            Transform lblL = sliderContainerGo.transform.Find("Slider_Track/Label_Left");
            if (lblL != null) sliderLeftText = lblL.GetComponent<Text>();
            Transform lblR = sliderContainerGo.transform.Find("Slider_Track/Label_Right");
            if (lblR != null) sliderRightText = lblR.GetComponent<Text>();
        }

        ApplyPedalSideLayout(CurrentPedalSide);
    }

    private Text CreateSteerButton(Transform parent, string name, SteerTouchButton.Direction dir, Vector2 pos, string text, Font font)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(230f, 240f);

        Image img = btnGo.AddComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.24f, 0.70f);
        img.raycastTarget = true;

        Outline outline = btnGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.50f, 0.80f, 0.80f);
        outline.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = btnGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(4f, -4f);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        Text t = textGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = 36;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;

        SteerTouchButton btnScript = btnGo.AddComponent<SteerTouchButton>();
        btnScript.Init(dir, img, outline);
        return t;
    }

    private Text CreateSliderLabel(Transform parent, string name, string text, Vector2 pos, Font font)
    {
        GameObject labelGo = new GameObject(name);
        labelGo.transform.SetParent(parent, false);
        RectTransform rt = labelGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(140f, 40f);

        Text t = labelGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = 20;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.85f, 0.90f, 1.0f, 0.95f);
        t.text = text;
        return t;
    }

    public void UpdateControlsVisibility()
    {
        // 1. Wheel visibility (this gameObject)
        bool showWheel = (controlType == SteeringControlType.Wheel);
        Image img = GetComponent<Image>();
        if (img != null) img.enabled = showWheel;

        // 2. Buttons container visibility
        if (buttonsContainerGo != null)
        {
            buttonsContainerGo.SetActive(controlType == SteeringControlType.Buttons);
        }

        // 3. Slider container visibility
        if (sliderContainerGo != null)
        {
            sliderContainerGo.SetActive(controlType == SteeringControlType.Slider);
        }
    }

    public void SetTouchButtonState(SteerTouchButton.Direction dir, bool pressed)
    {
        if (dir == SteerTouchButton.Direction.Left) leftBtnPressed = pressed;
        else rightBtnPressed = pressed;

        float input = 0f;
        if (leftBtnPressed) input += 1f;
        if (rightBtnPressed) input -= 1f;

        touchButtonInput = input;
    }

    public void SetSliderInput(float steerNorm, float handleX)
    {
        isSliderDragging = true;
        // Apply 3.5% center deadzone on slider
        if (Mathf.Abs(steerNorm) < 0.035f)
        {
            steerNorm = 0f;
        }

        currentWheelAngle = steerNorm * maxWheelAngle;
        if (touchSliderScript != null)
        {
            touchSliderScript.SetHandleLocalX(handleX);
        }
        UpdateWheelVisual();
    }

    public void OnSliderReleased()
    {
        isSliderDragging = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (controlType != SteeringControlType.Wheel) return;
        isDragging = true;
        previousPointerAngle = CalculatePointerAngle(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (controlType != SteeringControlType.Wheel || !isDragging) return;

        float newPointerAngle = CalculatePointerAngle(eventData.position, eventData.pressEventCamera);
        float angleDelta = Mathf.DeltaAngle(previousPointerAngle, newPointerAngle);

        currentWheelAngle = Mathf.Clamp(currentWheelAngle + angleDelta, -maxWheelAngle, maxWheelAngle);
        previousPointerAngle = newPointerAngle;

        UpdateWheelVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (controlType == SteeringControlType.Wheel)
        {
            isDragging = false;
        }
    }

    private void Update()
    {
        // Cancel dragging if mouse/touch is released
#if ENABLE_INPUT_SYSTEM
        if (isDragging)
        {
            bool mouseHeld = (Mouse.current != null && Mouse.current.leftButton.isPressed);
            bool touchHeld = (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);
            if (!mouseHeld && !touchHeld)
            {
                isDragging = false;
            }
        }
#else
        if (isDragging && !Input.GetMouseButton(0))
        {
            isDragging = false;
        }
#endif

        bool hasKeyInput = HandleKeyboardSteering();

        if (controlType == SteeringControlType.Buttons)
        {
            // Button steering: rotates with keyTurnSpeed while button is held, springs back to 0 on release
            if (Mathf.Abs(touchButtonInput) > 0.01f && !hasKeyInput)
            {
                currentWheelAngle = Mathf.Clamp(
                    currentWheelAngle + touchButtonInput * keyTurnSpeed * Time.deltaTime,
                    -maxWheelAngle,
                    maxWheelAngle
                );
                UpdateWheelVisual();
            }
            else if (!hasKeyInput && AutoCenterEnabled && returnToCenterSpeed > 0f && Mathf.Abs(currentWheelAngle) > 0.01f)
            {
                currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, returnToCenterSpeed * Time.deltaTime);
                UpdateWheelVisual();
            }
            else if (!hasKeyInput && AutoCenterEnabled && Mathf.Abs(currentWheelAngle) <= 0.01f && currentWheelAngle != 0f)
            {
                currentWheelAngle = 0f;
                UpdateWheelVisual();
            }
        }
        else if (controlType == SteeringControlType.Slider)
        {
            // Slider spring return to center (0) when finger is released (only if auto-center is enabled)
            if (!isSliderDragging && !hasKeyInput && AutoCenterEnabled && Mathf.Abs(currentWheelAngle) > 0.01f)
            {
                currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, returnToCenterSpeed * 2.5f * Time.deltaTime);
                if (touchSliderScript != null)
                {
                    float currentNorm = currentWheelAngle / maxWheelAngle;
                    touchSliderScript.SetHandleLocalX(-currentNorm * SliderTrackHalfWidth);
                }
                UpdateWheelVisual();
            }
            else if (!isSliderDragging && !hasKeyInput && AutoCenterEnabled && Mathf.Abs(currentWheelAngle) <= 0.01f && currentWheelAngle != 0f)
            {
                currentWheelAngle = 0f;
                if (touchSliderScript != null)
                {
                    touchSliderScript.SetHandleLocalX(0f);
                }
                UpdateWheelVisual();
            }
        }
        else // SteeringControlType.Wheel
        {
            // Wheel spring return to center (0) when released (only if auto-center is enabled)
            if (!isDragging && !hasKeyInput && AutoCenterEnabled && returnToCenterSpeed > 0f && Mathf.Abs(currentWheelAngle) > 0.01f)
            {
                currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, returnToCenterSpeed * Time.deltaTime);
                UpdateWheelVisual();
            }
            else if (!isDragging && !hasKeyInput && AutoCenterEnabled && Mathf.Abs(currentWheelAngle) <= 0.01f && currentWheelAngle != 0f)
            {
                currentWheelAngle = 0f;
                UpdateWheelVisual();
            }
        }
    }

    private bool HandleKeyboardSteering()
    {
        float keyInput = 0f;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // A key or Left Arrow turns the steering wheel LEFT (+angle)
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keyInput += 1f;

            // D key or Right Arrow turns the steering wheel RIGHT (-angle)
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keyInput -= 1f;
        }
#else
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keyInput += 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keyInput -= 1f;
#endif

        if (Mathf.Abs(keyInput) > 0.01f)
        {
            currentWheelAngle = Mathf.Clamp(
                currentWheelAngle + keyInput * keyTurnSpeed * Time.deltaTime,
                -maxWheelAngle,
                maxWheelAngle
            );
            if (controlType == SteeringControlType.Slider && touchSliderScript != null)
            {
                float currentNorm = currentWheelAngle / maxWheelAngle;
                touchSliderScript.SetHandleLocalX(-currentNorm * SliderTrackHalfWidth);
            }
            UpdateWheelVisual();
            return true;
        }

        return false;
    }

    private float CalculatePointerAngle(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform rt = WheelRect;
        if (rt == null) return 0f;

        Vector2 wheelScreenPos = RectTransformUtility.WorldToScreenPoint(eventCamera, rt.position);
        Vector2 dir = screenPosition - wheelScreenPos;
        if (dir.sqrMagnitude < 4f) return previousPointerAngle;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private void UpdateWheelVisual()
    {
        RectTransform rt = WheelRect;
        if (rt != null)
        {
            rt.localEulerAngles = new Vector3(0f, 0f, currentWheelAngle);
        }
    }

    public void ResetWheel()
    {
        currentWheelAngle = 0f;
        touchButtonInput = 0f;
        leftBtnPressed = false;
        rightBtnPressed = false;
        isDragging = false;
        isSliderDragging = false;
        if (touchSliderScript != null)
        {
            touchSliderScript.SetHandleLocalX(0f);
        }
        UpdateWheelVisual();
    }
}

