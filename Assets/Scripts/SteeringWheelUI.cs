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
    [SerializeField] private float keyTurnSpeed = 320f;

    [Tooltip("Speed to return to center when released (0 = stays where you leave it, like My Trucking Skills)")]
    [SerializeField] private float returnToCenterSpeed = 0f;

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
        previousPointerAngle = CalculatePointerAngle(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        float newPointerAngle = CalculatePointerAngle(eventData.position);
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
        HandleKeyboardSteering();

        if (!isDragging && returnToCenterSpeed > 0f && currentWheelAngle != 0f)
        {
            currentWheelAngle = Mathf.MoveTowards(currentWheelAngle, 0f, returnToCenterSpeed * Time.deltaTime);
            UpdateWheelVisual();
        }
    }

    private void HandleKeyboardSteering()
    {
        float keyInput = 0f;

#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // A key turns the steering wheel LEFT (+angle)
            if (keyboard.aKey.isPressed) keyInput += 1f;

            // D key turns the steering wheel RIGHT (-angle)
            if (keyboard.dKey.isPressed) keyInput -= 1f;
        }
#endif

        if (Mathf.Abs(keyInput) > 0.01f)
        {
            currentWheelAngle = Mathf.Clamp(
                currentWheelAngle + keyInput * keyTurnSpeed * Time.deltaTime,
                -maxWheelAngle,
                maxWheelAngle
            );
            UpdateWheelVisual();
        }
    }

    private float CalculatePointerAngle(Vector2 screenPosition)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            wheelRectTransform, screenPosition, null, out Vector2 localPoint))
        {
            return Mathf.Atan2(localPoint.y, localPoint.x) * Mathf.Rad2Deg;
        }
        return 0f;
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
