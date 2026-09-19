using UnityEngine;

/// <summary>
/// Standalone Trailer Obstacle (53' semi-trailer without tractor) placed anywhere on the map.
/// Can be freely positioned and rotated to any angle (with 15° step hotkeys).
/// Physical obstacle: causes truck crash upon collision.
/// Includes full visual details (trailer body, rear wheels, soft shadows, landing gear).
/// </summary>
[ExecuteAlways]
public class StandaloneTrailerObstacle : MonoBehaviour
{
    [Header("Placement")]
    public Vector2 position;
    public float rotationAngle = 0f; // Any angle in degrees

    [Header("Appearance")]
    public Color trailerColor = Color.white;

    private void Awake()
    {
        UpdateTransformAndVisual();
    }

    private void Start()
    {
        UpdateTransformAndVisual();
    }

    public void Setup(Vector2 pos, float rot, Color? color = null, Sprite trailerSp = null, Sprite wheelSp = null)
    {
        position = pos;
        rotationAngle = Mathf.Repeat(rot, 360f);
        if (color.HasValue) trailerColor = color.Value;
        UpdateTransformAndVisual(trailerSp, wheelSp);
    }

    public static float SnapAngle45(float angle)
    {
        float norm = Mathf.Repeat(angle, 360f);
        return Mathf.Round(norm / 45f) * 45f % 360f;
    }

    public void UpdateTransformAndVisual(Sprite customTrailerSprite = null, Sprite customWheelSprite = null)
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
        trailerGo.transform.localPosition = Vector3.zero;
        trailerGo.transform.localRotation = Quaternion.identity;

        SpriteRenderer srTrailer = trailerGo.GetComponent<SpriteRenderer>();
        if (srTrailer == null) srTrailer = trailerGo.AddComponent<SpriteRenderer>();
        if (customTrailerSprite != null)
        {
            srTrailer.sprite = customTrailerSprite;
        }
        else if (srTrailer.sprite == null)
        {
            srTrailer.sprite = ParkedTruckVisuals.GetTrailerSprite();
        }
        srTrailer.color = trailerColor;
        srTrailer.sortingOrder = 10;

        BoxCollider2D colTrailer = trailerGo.GetComponent<BoxCollider2D>();
        if (colTrailer == null) colTrailer = trailerGo.AddComponent<BoxCollider2D>();
        colTrailer.size = new Vector2(2.58f, 15.9f);
        colTrailer.offset = Vector2.zero;
        colTrailer.isTrigger = true;

        // 2. Wheels and soft ground shadows
        ParkedTruckVisuals.SetupTrailerVisuals(trailerGo.transform);

        // If trailerColor is custom, reapply to srTrailer (SetupTrailerVisuals defaults to white)
        srTrailer.color = trailerColor;

        // 3. Landing gear (опоры полуприцепа спереди)
        EnsureLandingGear(trailerGo.transform);
    }

    private void EnsureLandingGear(Transform trailerTr)
    {
        Transform gearTr = trailerTr.Find("LandingGear");
        if (gearTr == null)
        {
            GameObject gearGo = new GameObject("LandingGear");
            gearGo.transform.SetParent(trailerTr, false);
            gearGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            gearGo.transform.localRotation = Quaternion.identity;
            gearTr = gearGo.transform;
        }

        Sprite sq = GetSquareSprite();
        Material mat = GetDefaultSpriteMaterial();

        // Left landing pad
        EnsureLandingPad(gearTr, "LegLeft", new Vector3(-0.95f, 0f, 0f), sq, mat);
        // Right landing pad
        EnsureLandingPad(gearTr, "LegRight", new Vector3(0.95f, 0f, 0f), sq, mat);
        // Cross brace bar
        EnsureCrossBar(gearTr, "CrossBar", Vector3.zero, sq, mat);
    }

    private void EnsureLandingPad(Transform parent, string name, Vector3 localPos, Sprite sq, Material mat)
    {
        Transform padTr = parent.Find(name);
        GameObject padGo;
        if (padTr == null)
        {
            padGo = new GameObject(name);
            padGo.transform.SetParent(parent, false);
            padGo.transform.localPosition = localPos;
            padGo.transform.localRotation = Quaternion.identity;
            padGo.transform.localScale = Vector3.one;
        }
        else
        {
            padGo = padTr.gameObject;
            padTr.localPosition = localPos;
        }

        SpriteRenderer sr = padGo.GetComponent<SpriteRenderer>();
        if (sr == null) sr = padGo.AddComponent<SpriteRenderer>();
        if (sq != null)
        {
            sr.sprite = sq;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.32f, 0.45f);
        }
        if (mat != null && (sr.sharedMaterial == null || sr.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
        {
            sr.sharedMaterial = mat;
        }
        sr.color = new Color(0.22f, 0.23f, 0.25f, 1f); // Metallic landing gear pad
        sr.sortingOrder = 8;
    }

    private void EnsureCrossBar(Transform parent, string name, Vector3 localPos, Sprite sq, Material mat)
    {
        Transform barTr = parent.Find(name);
        GameObject barGo;
        if (barTr == null)
        {
            barGo = new GameObject(name);
            barGo.transform.SetParent(parent, false);
            barGo.transform.localPosition = localPos;
            barGo.transform.localRotation = Quaternion.identity;
            barGo.transform.localScale = Vector3.one;
        }
        else
        {
            barGo = barTr.gameObject;
            barTr.localPosition = localPos;
        }

        SpriteRenderer sr = barGo.GetComponent<SpriteRenderer>();
        if (sr == null) sr = barGo.AddComponent<SpriteRenderer>();
        if (sq != null)
        {
            sr.sprite = sq;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1.9f, 0.14f);
        }
        if (mat != null && (sr.sharedMaterial == null || sr.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
        {
            sr.sharedMaterial = mat;
        }
        sr.color = new Color(0.18f, 0.19f, 0.20f, 1f);
        sr.sortingOrder = 7;
    }

    private static Sprite cachedSquare;
    private static Sprite GetSquareSprite()
    {
        if (cachedSquare != null) return cachedSquare;
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "Square")
            {
                cachedSquare = s;
                return s;
            }
        }
        return null;
    }

    private static Material cachedSpriteMaterial;
    private static Material GetDefaultSpriteMaterial()
    {
        if (cachedSpriteMaterial != null) return cachedSpriteMaterial;
        Shader s = Shader.Find("Sprites/Default");
        if (s != null) cachedSpriteMaterial = new Material(s);
        return cachedSpriteMaterial;
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
