using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.78f);
    [SerializeField] private Color pressedColor = new Color(1f, 1f, 1f, 1.0f);
    [SerializeField] private float pressedScale = 0.93f;

    private bool isPressed = false;
    private RectTransform rectTransform;
    private Vector3 originalScale;

    public bool IsPressed => isPressed;
    public PedalType Type { get => pedalType; set => pedalType = value; }

    public void SetPedalType(PedalType type)
    {
        pedalType = type;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
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
        UpdateVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        SetPressed(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        SetPressed(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPressed)
        {
            SetPressed(false);
        }
    }

    private void OnDisable()
    {
        if (isPressed)
        {
            SetPressed(false);
        }
    }

    private void SetPressed(bool pressed)
    {
        if (isPressed == pressed) return;
        isPressed = pressed;

        UpdateVisuals();

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
    }

    private void UpdateVisuals()
    {
        if (targetGraphic != null)
        {
            targetGraphic.color = isPressed ? pressedColor : normalColor;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = isPressed ? originalScale * pressedScale : originalScale;
        }
    }
}
