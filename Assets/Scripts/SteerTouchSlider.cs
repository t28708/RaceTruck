using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Touch Slider for Horizontal Line/Slider steering mode.
/// Finger moves strictly left and right on the bar.
/// On release, handle smoothly/immediately springs back to the center (0).
/// </summary>
public class SteerTouchSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform trackRt;
    [SerializeField] private RectTransform handleRt;
    [SerializeField] private Image handleImg;
    [SerializeField] private float trackHalfWidth = 280f;

    private bool isDragging = false;
    private Color normalHandleColor = new Color(0.20f, 0.65f, 1.00f, 0.95f);
    private Color activeHandleColor = new Color(1.00f, 0.85f, 0.20f, 1.00f);

    public void Init(RectTransform track, RectTransform handle, Image handleImage, float halfWidth)
    {
        trackRt = track;
        handleRt = handle;
        handleImg = handleImage;
        trackHalfWidth = halfWidth;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        if (handleImg != null) handleImg.color = activeHandleColor;
        ProcessPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        ProcessPointer(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        if (handleImg != null) handleImg.color = normalHandleColor;
        if (SteeringWheelUI.Instance != null)
        {
            SteeringWheelUI.Instance.OnSliderReleased();
        }
    }

    private void ProcessPointer(PointerEventData eventData)
    {
        if (trackRt == null || SteeringWheelUI.Instance == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRt, eventData.position, eventData.pressEventCamera, out localPoint);

        float clampedX = Mathf.Clamp(localPoint.x, -trackHalfWidth, trackHalfWidth);
        float norm = clampedX / trackHalfWidth; // -1 (left) to +1 (right)

        // Sign conversion: Left drag (-x) -> positive steering (+angle = turn left)
        // Right drag (+x) -> negative steering (-angle = turn right)
        float steerNorm = -norm;

        SteeringWheelUI.Instance.SetSliderInput(steerNorm, clampedX);
    }

    public void SetHandleLocalX(float handleX)
    {
        if (handleRt != null)
        {
            handleRt.anchoredPosition = new Vector2(handleX, 0f);
        }
    }

    private void OnDisable()
    {
        isDragging = false;
        if (handleImg != null) handleImg.color = normalHandleColor;
        if (SteeringWheelUI.Instance != null)
        {
            SteeringWheelUI.Instance.OnSliderReleased();
        }
    }
}
