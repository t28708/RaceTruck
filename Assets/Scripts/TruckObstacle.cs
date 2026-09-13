using UnityEngine;

/// <summary>
/// Standalone Truck Obstacle (Tractor + 53' Semi-Trailer) placed anywhere on the map.
/// Can be positioned at any angle, has precise box colliders, realistic wheels, and soft shadows.
/// Causes truck crash upon collision with TruckCollisionDetector.
/// </summary>
[ExecuteAlways]
public class TruckObstacle : MonoBehaviour
{
    [Header("Placement")]
    public Vector2 position;
    public float rotationAngle = 0f;

    [Header("Appearance")]
    public Color color = Color.white; // Tractor body tint color
    public int sortingOrder = 10;     // Truck sorting layer

    private static Sprite cachedTractorSprite;
    private static Sprite cachedTrailerSprite;

    private void Awake()
    {
        UpdateTransformAndVisual();
    }

    private void Start()
    {
        UpdateTransformAndVisual();
    }

    public void Setup(Vector2 pos, float rot, Color? truckColor = null, Sprite tractorSpr = null, Sprite trailerSpr = null)
    {
        position = pos;
        rotationAngle = Mathf.Repeat(rot, 360f);
        if (truckColor.HasValue) color = truckColor.Value;
        UpdateTransformAndVisual(tractorSpr, trailerSpr);
    }

    public void UpdateTransformAndVisual(Sprite tractorSpr = null, Sprite trailerSpr = null)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);

        if (tractorSpr != null) cachedTractorSprite = tractorSpr;
        if (trailerSpr != null) cachedTrailerSprite = trailerSpr;

        Sprite activeTractorSprite = tractorSpr ?? cachedTractorSprite ?? GetTractorSprite();
        Sprite activeTrailerSprite = trailerSpr ?? cachedTrailerSprite ?? GetTrailerSprite();

        // 1. Trailer Child (Always realistic white dry van / refrigerated trailer)
        Transform trailerTr = transform.Find("Trailer") ?? transform.Find("Preview_Trailer");
        GameObject trailerGo;
        if (trailerTr == null)
        {
            trailerGo = new GameObject("Trailer");
            trailerGo.transform.SetParent(transform, false);
        }
        else
        {
            trailerGo = trailerTr.gameObject;
            trailerGo.name = "Trailer";
        }

        trailerGo.transform.localPosition = new Vector3(0f, -2.9f, 0f);
        trailerGo.transform.localRotation = Quaternion.identity;

        SpriteRenderer srTrailer = trailerGo.GetComponent<SpriteRenderer>();
        if (srTrailer == null) srTrailer = trailerGo.AddComponent<SpriteRenderer>();
        if (activeTrailerSprite != null) srTrailer.sprite = activeTrailerSprite;
        srTrailer.color = Color.white; // Always pure crisp white for trailer (never tinted blue/transparent)
        srTrailer.sortingOrder = sortingOrder;

        BoxCollider2D colTrailer = trailerGo.GetComponent<BoxCollider2D>();
        if (colTrailer == null) colTrailer = trailerGo.AddComponent<BoxCollider2D>();
        colTrailer.size = new Vector2(2.58f, 15.9f);
        colTrailer.offset = Vector2.zero;
        colTrailer.isTrigger = true;

        // 2. Tractor Child (Cab with customizable tint color)
        Transform tractorTr = transform.Find("Tractor") ?? transform.Find("Preview_Tractor");
        GameObject tractorGo;
        if (tractorTr == null)
        {
            tractorGo = new GameObject("Tractor");
            tractorGo.transform.SetParent(transform, false);
        }
        else
        {
            tractorGo = tractorTr.gameObject;
            tractorGo.name = "Tractor";
        }

        tractorGo.transform.localPosition = new Vector3(0f, 6.8f, 0f);
        tractorGo.transform.localRotation = Quaternion.identity;

        SpriteRenderer srTractor = tractorGo.GetComponent<SpriteRenderer>();
        if (srTractor == null) srTractor = tractorGo.AddComponent<SpriteRenderer>();
        if (activeTractorSprite != null) srTractor.sprite = activeTractorSprite;
        srTractor.color = Color.white;
        srTractor.sortingOrder = sortingOrder;

        BoxCollider2D colTractor = tractorGo.GetComponent<BoxCollider2D>();
        if (colTractor == null) colTractor = tractorGo.AddComponent<BoxCollider2D>();
        colTractor.size = new Vector2(2.55f, 8.2f);
        colTractor.offset = Vector2.zero;
        colTractor.isTrigger = true;

        // 3. Attach Wheels and Soft Contact Shadows
        ParkedTruckVisuals.SetupTractorVisuals(tractorGo.transform);
        ParkedTruckVisuals.SetupTrailerVisuals(trailerGo.transform);
    }

    private static Sprite GetTractorSprite()
    {
        if (cachedTractorSprite != null) return cachedTractorSprite;
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "Tractor")
            {
                cachedTractorSprite = s;
                return s;
            }
        }
        return null;
    }

    private static Sprite GetTrailerSprite()
    {
        if (cachedTrailerSprite != null) return cachedTrailerSprite;
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "Trailer")
            {
                cachedTrailerSprite = s;
                return s;
            }
        }
        return null;
    }

    private void OnValidate()
    {
        position = new Vector2(transform.position.x, transform.position.y);
        rotationAngle = Mathf.Repeat(transform.eulerAngles.z, 360f);
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
