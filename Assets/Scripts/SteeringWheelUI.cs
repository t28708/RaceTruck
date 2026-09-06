using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SteeringWheelUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Wheel Settings")]
    [Tooltip("Maximum rotation angle of the steering wheel in degrees (e.g., 450 = 1.25 turns each side)")]
    [SerializeField] private float maxWheelAngle = 450f;

    [Tooltip("Maximum front wheel turn angle in degrees")]
    [SerializeField] private float maxSteerAngle = 40f;

    [Tooltip("Rotation speed in degrees per second when turning via A and D keys")]
    [SerializeField] private float keyTurnSpeed = 360f;

    [Tooltip("Speed to return to center when released (spring return to 0)")]
    [SerializeField] private float returnToCenterSpeed = 450f;

    [Header("UI References")]
    [SerializeField] private RectTransform wheelRectTransform;

    private float currentWheelAngle = 0f;
    private float previousPointerAngle = 0f;
    private bool isDragging = false;

    /// <summary>
    /// Current front wheel steering angle in degrees (-maxSteerAngle to +maxSteerAngle).
    /// Positive = Left, Negative = Right.
    /// </summary>
    public float CurrentSteerAngle => (currentWheelAngle / maxWheelAngle) * maxSteerAngle;

    /// <summary>
    /// Normalized steer value from -1 (full right) to +1 (full left).
    /// </summary>
    public float NormalizedSteer => currentWheelAngle / maxWheelAngle;

    public float CurrentWheelAngle => currentWheelAngle;
    public bool IsDragging => isDragging;

    private void Awake()
    {
        if (wheelRectTransform == null)
        {
            wheelRectTransform = GetComponent<RectTransform>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        previousPointerAngle = CalculatePointerAngle(eventData.position, eventData.pressEventCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        float newPointerAngle = CalculatePointerAngle(eventData.position, eventData.pressEventCamera);
        float angleDelta = Mathf.DeltaAngle(previousPointerAngle, newPointerAngle);

        currentWheelAngle = Mathf.Clamp(currentWheelAngle + angleDelta, -maxWheelAngle, maxWheelAngle);
        previousPointerAngle = newPointerAngle;

        UpdateWheelVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void Update()
    {
        bool hasKeyInput = HandleKeyboardSteering();

        // Spring return to center when not dragging and no keyboard steering active
        if (!isDragging && !hasKeyInput && returnToCenterSpeed > 0f && Mathf.Abs(currentWheelAngle) > 0.01f)
        {
            currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, returnToCenterSpeed * Time.deltaTime);
            UpdateWheelVisual();
        }
        else if (!isDragging && !hasKeyInput && Mathf.Abs(currentWheelAngle) <= 0.01f && currentWheelAngle != 0f)
        {
            currentWheelAngle = 0f;
            UpdateWheelVisual();
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
            UpdateWheelVisual();
            return true;
        }

        return false;
    }

    private float CalculatePointerAngle(Vector2 screenPosition, Camera eventCamera)
    {
        if (wheelRectTransform == null) return 0f;

        // Calculate angle relative to the center of the steering wheel on screen
        Vector2 wheelScreenPos = RectTransformUtility.WorldToScreenPoint(eventCamera, wheelRectTransform.position);
        Vector2 dir = screenPosition - wheelScreenPos;
        if (dir.sqrMagnitude < 4f) return previousPointerAngle;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private void UpdateWheelVisual()
    {
        if (wheelRectTransform != null)
        {
            wheelRectTransform.localEulerAngles = new Vector3(0f, 0f, currentWheelAngle);
        }
    }

    public void ResetWheel()
    {
        currentWheelAngle = 0f;
        UpdateWheelVisual();
    }
}
