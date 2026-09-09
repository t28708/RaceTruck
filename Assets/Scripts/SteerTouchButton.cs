using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Touch Button for Arrow/Button steering mode (Left/Right).
/// Supports multi-touch, pointer down/up/exit events, and smooth visual feedback.
/// </summary>
public class SteerTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum Direction { Left, Right }

    [SerializeField] private Direction direction;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Outline outline;
    [SerializeField] private Color normalBgColor = new Color(0.12f, 0.16f, 0.24f, 0.70f);
    [SerializeField] private Color pressedBgColor = new Color(0.16f, 0.52f, 0.96f, 0.95f);
    [SerializeField] private Color normalOutlineColor = new Color(0.30f, 0.45f, 0.70f, 0.75f);
    [SerializeField] private Color pressedOutlineColor = new Color(0.50f, 0.85f, 1.00f, 1.00f);

    private int activePointerCount = 0;

    public void Init(Direction dir, Image img, Outline outl)
    {
        direction = dir;
        buttonImage = img;
        outline = outl;
        UpdateVisual(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        activePointerCount++;
        UpdateVisual(true);
        if (SteeringWheelUI.Instance != null)
        {
            SteeringWheelUI.Instance.SetTouchButtonState(direction, true);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        activePointerCount = Mathf.Max(0, activePointerCount - 1);
        if (activePointerCount == 0)
        {
            UpdateVisual(false);
            if (SteeringWheelUI.Instance != null)
            {
                SteeringWheelUI.Instance.SetTouchButtonState(direction, false);
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (activePointerCount > 0)
        {
            activePointerCount = 0;
            UpdateVisual(false);
            if (SteeringWheelUI.Instance != null)
            {
                SteeringWheelUI.Instance.SetTouchButtonState(direction, false);
            }
        }
    }

    private void UpdateVisual(bool pressed)
    {
        if (buttonImage != null)
        {
            buttonImage.color = pressed ? pressedBgColor : normalBgColor;
        }
        if (outline != null)
        {
            outline.effectColor = pressed ? pressedOutlineColor : normalOutlineColor;
        }
        transform.localScale = pressed ? new Vector3(0.96f, 0.96f, 1f) : Vector3.one;
    }

    private void OnDisable()
    {
        activePointerCount = 0;
        UpdateVisual(false);
        if (SteeringWheelUI.Instance != null)
        {
            SteeringWheelUI.Instance.SetTouchButtonState(direction, false);
        }
    }
}
