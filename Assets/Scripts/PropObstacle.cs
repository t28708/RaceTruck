using UnityEngine;

public enum PropType
{
    Cone = 0,            // 1. Конус (Traffic Cone)
    TrafficCone = 0,
    Hydrant = 1,         // 2. Пожарный гидрант (Fire Hydrant)
    FireHydrant = 1,
    CheckinBooth = 2,    // 3. Будка чекина / КПП (Check-in / Guard Booth)
    ConcreteBarrier = 3, // 4. Бетонный блок (Jersey Barrier)
    Barrel = 4,          // 5. Бочка (Hazard Barrel)
    HazardBarrel = 4,
    LightPole = 5,       // 6. Фонарный столб (Light Pole)
    TireStack = 6        // 7. Стопка шин (Tire Stack)
}

/// <summary>
/// Obstacle prop placed on the map (Cone, Hydrant, Guard/Check-in Booth, Barrier, Barrel, Light Pole, Tire Stack).
/// Physical obstacle - detects collisions and triggers crashes with TruckCollisionDetector.
/// </summary>
[ExecuteAlways]
public class PropObstacle : MonoBehaviour
{
    public static Vector2 GetPropSize(PropType type) => GetDefaultDimensions(type);

    [Header("Type and Placement")]
    public PropType propType = PropType.Cone;
    public Vector2 position;
    public float rotationAngle = 0f; // degrees

    [Header("Appearance and Customization")]
    public Color color = Color.white;
    public Vector2 customSize = Vector2.zero; // If zero, uses default dimensions for propType

    private void Awake()
    {
        UpdateTransformAndVisual();
    }

    private void Start()
    {
        UpdateTransformAndVisual();
    }

    public static Vector2 GetDefaultDimensions(PropType type)
    {
        switch (type)
        {
            case PropType.Cone:
                return new Vector2(1.40f, 1.40f);
            case PropType.Hydrant:
                return new Vector2(0.70f, 0.70f);
            case PropType.CheckinBooth:
                return new Vector2(4.20f, 3.40f);
            case PropType.ConcreteBarrier:
                return new Vector2(2.50f, 0.70f);
            case PropType.Barrel:
                return new Vector2(0.80f, 0.80f);
            case PropType.LightPole:
                return new Vector2(0.80f, 0.80f);
            case PropType.TireStack:
                return new Vector2(1.10f, 1.10f);
            default:
                return new Vector2(1.0f, 1.0f);
        }
    }

    public static string GetSpriteFileName(PropType type)
    {
        switch (type)
        {
            case PropType.Cone:
                return "Prop_TrafficCone.png";
            case PropType.Hydrant:
                return "Prop_FireHydrant.png";
            case PropType.CheckinBooth:
                return "Prop_CheckinBooth.png";
            case PropType.ConcreteBarrier:
                return "Prop_ConcreteBarrier.png";
            case PropType.Barrel:
                return "Prop_Barrel.png";
            case PropType.LightPole:
                return "Prop_LightPole.png";
            case PropType.TireStack:
                return "Prop_TireStack.png";
            default:
                return "Prop_TrafficCone.png";
        }
    }

    public static string GetDisplayName(PropType type)
    {
        switch (type)
        {
            case PropType.Cone:
                return "Конус";
            case PropType.Hydrant:
                return "Гидрант";
            case PropType.CheckinBooth:
                return "Будка чекина / КПП";
            case PropType.ConcreteBarrier:
                return "Бетонный блок";
            case PropType.Barrel:
                return "Бочка";
            case PropType.LightPole:
                return "Фонарный столб";
            case PropType.TireStack:
                return "Стопка шин";
            default:
                return "Препятствие";
        }
    }

    public Vector2 CurrentDimensions
    {
        get
        {
            if (customSize.x > 0.05f && customSize.y > 0.05f)
            {
                return customSize;
            }
            return GetDefaultDimensions(propType);
        }
    }

    public void Setup(PropType type, Vector2 pos, float rot, Color? tint = null, Sprite sprite = null, Vector2? size = null)
    {
        propType = type;
        position = pos;
        rotationAngle = rot;
        if (tint.HasValue) color = tint.Value;
        if (size.HasValue) customSize = size.Value;

        UpdateTransformAndVisual(sprite);
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
        sr.sortingOrder = 6; // Obstacle layer

        // Scale sprite appropriately to match target physical dimensions
        Vector2 dims = CurrentDimensions;
        if (sr.sprite != null)
        {
            Vector2 spriteSize = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;
            if (spriteSize.x > 0.001f && spriteSize.y > 0.001f)
            {
                transform.localScale = new Vector3(dims.x / spriteSize.x, dims.y / spriteSize.y, 1f);
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }
        else
        {
            transform.localScale = Vector3.one;
        }

        // Physics Collider
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider2D>();

        // Box size in local coordinates (account for localScale)
        Vector2 scale = transform.localScale;
        float localW = Mathf.Abs(scale.x) > 0.001f ? dims.x / Mathf.Abs(scale.x) : dims.x;
        float localH = Mathf.Abs(scale.y) > 0.001f ? dims.y / Mathf.Abs(scale.y) : dims.y;

        boxCol.size = new Vector2(localW, localH);
        boxCol.offset = Vector2.zero;
        boxCol.isTrigger = true; // Registered by TruckCollisionDetector
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    UpdateTransformAndVisual();
                }
            };
            return;
        }
#endif
        UpdateTransformAndVisual();
    }
}
