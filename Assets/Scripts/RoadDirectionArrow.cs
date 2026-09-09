using UnityEngine;

/// <summary>
/// Visual road direction and navigation arrow marking.
/// Can be freely placed anywhere on the map at any angle to indicate traffic directions and trajectories.
/// Does NOT block the truck physically - truck drives smoothly over it.
/// </summary>
[ExecuteAlways]
public class RoadDirectionArrow : MonoBehaviour
{
    [Header("Placement")]
    public Vector2 position;
    public float rotationAngle = 0f; // 0 deg = forward (+Y)
    public float scale = 1.0f;       // Scale multiplier (standard size ~ 1.5m x 3.0m)

    [Header("Appearance")]
    public Color color = new Color(0.95f, 0.95f, 0.95f, 1.0f); // Bright white road marking (or yellow/orange)

    public void Setup(Vector2 pos, float rot, float arrowScale = 1.0f, Color? arrowColor = null, Sprite sprite = null)
    {
        position = pos;
        rotationAngle = rot;
        scale = Mathf.Max(0.2f, arrowScale);
        if (arrowColor.HasValue) color = arrowColor.Value;
        UpdateTransformAndVisual(sprite);
    }

    public void UpdateTransformAndVisual(Sprite customSprite = null)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);
        transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        if (customSprite != null)
        {
            sr.sprite = customSprite;
        }

        sr.color = color;
        sr.sortingOrder = 2; // On asphalt (-10), under truck wheels/body (7-10)
    }

    private void OnValidate()
    {
        UpdateTransformAndVisual();
    }
}
