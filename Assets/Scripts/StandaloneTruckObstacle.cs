using UnityEngine;

/// <summary>
/// Standalone Truck Obstacle (Tractor + 53' Trailer) placed anywhere on the map.
/// Can be freely positioned and rotated to any angle (with 45° step hotkeys).
/// Physical obstacle: causes truck crash upon collision.
/// Includes full visual details (tractor, trailer, wheels, shadows).
/// </summary>
[ExecuteAlways]
public class StandaloneTruckObstacle : MonoBehaviour
{
    [Header("Placement")]
    public Vector2 position;
    public float rotationAngle = 0f; // Any angle in degrees

    [Header("Appearance")]
    public Color tractorColor = Color.white;
    public Color truckColor { get => tractorColor; set => tractorColor = value; }

    private void Awake()
    {
        UpdateTransformAndVisual();
    }

    private void Start()
    {
        UpdateTransformAndVisual();
    }

    public void Setup(Vector2 pos, float rot, Color? color = null, Sprite tractorSp = null, Sprite trailerSp = null, Sprite wheelSp = null)
    {
        position = pos;
        rotationAngle = Mathf.Repeat(rot, 360f);
        if (color.HasValue) tractorColor = color.Value;
        UpdateTransformAndVisual(tractorSp, trailerSp, wheelSp);
    }

    public static float SnapAngle45(float angle)
    {
        float norm = Mathf.Repeat(angle, 360f);
        return Mathf.Round(norm / 45f) * 45f % 360f;
    }

    public void UpdateTransformAndVisual(Sprite customTractorSprite = null, Sprite customTrailerSprite = null, Sprite customWheelSprite = null)
    {
        transform.position = new Vector3(position.x, position.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);

        // 1. Trailer Child
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
        if (customTrailerSprite != null) srTrailer.sprite = customTrailerSprite;
        srTrailer.color = Color.white; // Always pure white for trailer!
        srTrailer.sortingOrder = 5;

        BoxCollider2D colTrailer = trailerGo.GetComponent<BoxCollider2D>();
        if (colTrailer == null) colTrailer = trailerGo.AddComponent<BoxCollider2D>();
        colTrailer.size = new Vector2(2.58f, 15.9f);
        colTrailer.offset = Vector2.zero;
        colTrailer.isTrigger = true;

        // 2. Tractor Child
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
        if (customTractorSprite != null) srTractor.sprite = customTractorSprite;
        srTractor.color = tractorColor;
        srTractor.sortingOrder = 6;

        BoxCollider2D colTractor = tractorGo.GetComponent<BoxCollider2D>();
        if (colTractor == null) colTractor = tractorGo.AddComponent<BoxCollider2D>();
        colTractor.size = new Vector2(2.55f, 8.2f);
        colTractor.offset = Vector2.zero;
        colTractor.isTrigger = true;

        // 3. Enrich with wheels and soft shadows
        ParkedTruckVisuals.SetupTractorVisuals(tractorGo.transform);
        ParkedTruckVisuals.SetupTrailerVisuals(trailerGo.transform);
    }

    private void OnValidate()
    {
        rotationAngle = Mathf.Repeat(rotationAngle, 360f);
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
