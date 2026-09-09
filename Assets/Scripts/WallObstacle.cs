using UnityEngine;

/// <summary>
/// Solid wall obstacle component.
/// Placed as straight segments with strictly orthogonal / 90° angles.
/// Blocks the truck physically (causes collision/crash if driven into).
/// </summary>
[ExecuteAlways]
public class WallObstacle : MonoBehaviour
{
    [Header("Wall Coordinates")]
    public Vector2 startPoint;
    public Vector2 endPoint;

    [Header("Appearance & Size")]
    public float thickness = 0.45f;
    public Color color = new Color(0.48f, 0.28f, 0.15f, 1.0f); // Solid rich brown wall

    public void Setup(Vector2 start, Vector2 end, float wallThickness = 0.45f, Color? wallColor = null, Sprite sprite = null)
    {
        startPoint = start;
        endPoint = end;
        thickness = Mathf.Max(0.1f, wallThickness);
        if (wallColor.HasValue) color = wallColor.Value;
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

        // Visual SpriteRenderer
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        if (customSprite != null)
        {
            sr.sprite = customSprite;
        }

        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(thickness, length);
        sr.color = color;
        sr.sortingOrder = 6; // Above ground/asphalt and markings, visible obstacle

        // Physics Collider (Obstacle)
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider2D>();

        boxCol.size = new Vector2(thickness, length);
        boxCol.offset = Vector2.zero;
        boxCol.isTrigger = true; // Detected by TruckCollisionDetector
    }

    private void OnValidate()
    {
        UpdateTransformAndVisual();
    }
}
