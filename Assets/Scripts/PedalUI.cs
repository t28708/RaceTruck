using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PedalUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum PedalType
    {
        Gas,
        BrakeReverse
    }

    [Header("Pedal Configuration")]
    [SerializeField] private PedalType pedalType = PedalType.Gas;

    [Header("Visual Feedback")]
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color pressedColor = new Color(0.42f, 0.42f, 0.42f, 1.0f); // Darker when pressed
    [SerializeField] private float pressedScale = 0.93f;

    private bool isPointerPressed = false;
    private bool isVisuallyPressed = false;
    private RectTransform rectTransform;
    private Vector3 originalScale = Vector3.one;

    public bool IsPressed => isPointerPressed;
    public PedalType Type { get => pedalType; set => pedalType = value; }

    public void SetPedalType(PedalType type)
    {
        pedalType = type;
    }

    private void Awake()
    {
        FindComponents();
    }

    private void Start()
    {
        FindComponents();
        UpdateVisuals();
    }

    public void FindComponents()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
            if (originalScale.sqrMagnitude < 0.001f)
            {
                originalScale = Vector3.one;
                rectTransform.localScale = Vector3.one;
            }
        }
        else
        {
            originalScale = Vector3.one;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetPointerPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetPointerPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPointerPressed)
        {
            SetPointerPressed(false);
        }
    }

    private void OnDisable()
    {
        if (isPointerPressed)
        {
            SetPointerPressed(false);
        }
    }

    private void SetPointerPressed(bool pressed)
    {
        isPointerPressed = pressed;

        if (TruckController.Instance != null)
        {
            if (pedalType == PedalType.Gas)
            {
                TruckController.Instance.SetGasPedal(pressed);
            }
            else
            {
                TruckController.Instance.SetBrakePedal(pressed);
            }
        }

        CheckVisualState();
    }

    private void Update()
    {
        CheckVisualState();
    }

    private void CheckVisualState()
    {
        bool kbActive = false;
        if (TruckController.Instance != null)
        {
            if (pedalType == PedalType.Gas)
            {
                kbActive = TruckController.Instance.IsWPressed;
            }
            else
            {
                kbActive = TruckController.Instance.IsSPressed;
            }
        }
        else
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (pedalType == PedalType.Gas) kbActive = kb.wKey.isPressed || kb.upArrowKey.isPressed;
                else kbActive = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            }
#else
            if (pedalType == PedalType.Gas) kbActive = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            else kbActive = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
#endif
        }

        bool active = isPointerPressed || kbActive;
        if (active != isVisuallyPressed)
        {
            isVisuallyPressed = active;
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        if (targetGraphic == null)
        {
            targetGraphic = GetComponent<Graphic>();
        }

        if (targetGraphic != null)
        {
            targetGraphic.color = isVisuallyPressed ? pressedColor : normalColor;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = isVisuallyPressed ? originalScale * pressedScale : originalScale;
        }
    }
}
