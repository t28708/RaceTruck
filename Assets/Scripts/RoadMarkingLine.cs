using UnityEngine;

/// <summary>
/// Visual road and parking slot marking line.
/// Can be placed between any start point A and end point B at any angle.
/// Does NOT block the truck physically - truck drives smoothly across/over it.
/// </summary>
[ExecuteAlways]
public class RoadMarkingLine : MonoBehaviour
{
    [Header("Line Coordinates")]
    public Vector2 startPoint;
    public Vector2 endPoint;

    [Header("Appearance")]
    public float thickness = 0.20f;
    public Color color = new Color(0.95f, 0.95f, 0.95f, 1.0f); // Bright white marking

    public void Setup(Vector2 start, Vector2 end, float lineThickness = 0.20f, Color? lineColor = null, Sprite sprite = null)
    {
        startPoint = start;
        endPoint = end;
        thickness = Mathf.Max(0.05f, lineThickness);
        if (lineColor.HasValue) color = lineColor.Value;
        UpdateTransformAndVisual(sprite);
    }

    public void UpdateTransformAndVisual(Sprite customSprite = null)
    {
        Vector2 dir = endPoint - startPoint;
        float length = dir.magnitude;
        if (length < 0.01f) length = 0.01f;

        // Angle in degrees: 0 deg = along +Y axis
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        Vector2 center = (startPoint + endPoint) * 0.5f;

        transform.position = new Vector3(center.x, center.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        if (customSprite != null)
        {
            sr.sprite = customSprite;
        }

        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(thickness, length);
        sr.color = color;
        sr.sortingOrder = 2; // on asphalt (-10), under truck (8-10)
    }

    private void OnValidate()
    {
        UpdateTransformAndVisual();
    }
}
