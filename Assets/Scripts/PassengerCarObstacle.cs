using UnityEngine;

/// <summary>
/// Passenger car obstacle placed on the map.
/// Can be rotated to 0, 45, 90, 135, 180, etc.
/// Acts as a physical obstacle - truck crashes into it upon collision.
/// Supports customizable paint colors.
/// </summary>
[ExecuteAlways]
public class PassengerCarObstacle : MonoBehaviour
{
    [Header("Placement")]
    public Vector2 position;
    public float rotationAngle = 0f; // 0 deg = forward (+Y), snapped to 45 deg intervals (0, 45, 90, ...)

    [Header("Appearance and Size")]
    public Color color = Color.white; // Tinted body color (white, black, red, blue, etc.)
    public float carWidth = 2.05f;    // Physical width in meters
    public float carLength = 4.60f;   // Physical length in meters

    public void Setup(Vector2 pos, float rot, Color? carColor = null, Sprite sprite = null)
    {
        position = pos;
        rotationAngle = SnapAngle45(rot);
        if (carColor.HasValue) color = carColor.Value;
        UpdateTransformAndVisual(sprite);
    }

    public static float SnapAngle45(float angle)
    {
        float norm = Mathf.Repeat(angle, 360f);
        return Mathf.Round(norm / 45f) * 45f % 360f;
    }

    public void UpdateTransformAndVisual(Sprite customSprite = null)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);

        // Visual SpriteRenderer
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        if (customSprite != null)
        {
            sr.sprite = customSprite;
        }

        sr.color = color;
        sr.sortingOrder = 6; // Obstacle layer (above markings/asphalt, visible)

        // Physics Collider (Obstacle)
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider2D>();

        boxCol.size = new Vector2(carWidth, carLength);
        boxCol.offset = Vector2.zero;
        boxCol.isTrigger = true; // Detected by TruckCollisionDetector
    }

    private void OnValidate()
    {
        rotationAngle = SnapAngle45(rotationAngle);
        UpdateTransformAndVisual();
    }
}
