using UnityEngine;

/// <summary>
/// Displays a blinking hazard warning sign (yellow exclamation badge with pulsing aura)
/// at the exact physical pinch point where the semi-trailer contacts the tractor
/// when approaching the jackknife folding limit (within 10 degrees of max articulation).
/// </summary>
public class JackknifeWarningIndicator : MonoBehaviour
{
    public static JackknifeWarningIndicator Instance { get; private set; }

    [Header("Warning Margin")]
    [Tooltip("Degrees before maximum articulation angle to start warning")]
    [SerializeField] private float warningMarginDeg = 10.0f;

    [Header("Blink Animation")]
    [Tooltip("Pulsing frequency in cycles per second (calm, subtle)")]
    [SerializeField] private float blinkFrequency = 1.0f;
    [SerializeField] private float iconMinScale = 0.52f;
    [SerializeField] private float iconMaxScale = 0.58f;
    [SerializeField] private float glowMinScale = 0.65f;
    [SerializeField] private float glowMaxScale = 0.85f;
    [SerializeField] private float iconMinAlpha = 0.30f;
    [SerializeField] private float iconMaxAlpha = 0.55f;

    private TruckController truckController;
    private Rigidbody2D tractorRb;
    private Rigidbody2D trailerRb;

    private GameObject warningRoot;
    private Transform iconTransform;
    private Transform glowTransform;
    private SpriteRenderer iconRenderer;
    private SpriteRenderer glowRenderer;

    private static Sprite s_warningSprite;
    private static Sprite s_glowSprite;

    public bool IsWarningActive { get; private set; }

    private void Awake()
    {
        Instance = this;
        tractorRb = GetComponent<Rigidbody2D>();
        truckController = GetComponent<TruckController>();
        if (truckController == null)
        {
            truckController = Object.FindFirstObjectByType<TruckController>();
        }
    }

    private void Start()
    {
        if (truckController == null)
        {
            truckController = Object.FindFirstObjectByType<TruckController>();
        }
        if (tractorRb == null)
        {
            tractorRb = GetComponent<Rigidbody2D>();
        }

        FindTrailer();
        BuildVisualObjects();
    }

    private void FindTrailer()
    {
        if (truckController != null && truckController.TrailerRb != null)
        {
            trailerRb = truckController.TrailerRb;
            return;
        }

        GameObject trailerGo = GameObject.Find("Trailer");
        if (trailerGo != null)
        {
            trailerRb = trailerGo.GetComponent<Rigidbody2D>();
        }
    }

    private void BuildVisualObjects()
    {
        if (warningRoot != null) return;

        warningRoot = new GameObject("JackknifeWarning_Root");
        // Detach from tractor so rotation can strictly face camera
        warningRoot.transform.SetParent(null, false);

        // 1. Soft pulsing glow aura (under the icon)
        GameObject glowGo = new GameObject("WarningGlow");
        glowGo.transform.SetParent(warningRoot.transform, false);
        glowTransform = glowGo.transform;
        glowRenderer = glowGo.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GetGlowSprite();
        glowRenderer.sortingOrder = 148;
        glowRenderer.color = new Color(1.0f, 0.82f, 0.05f, 0.5f);

        // 2. Yellow hazard triangle badge with bold exclamation mark
        GameObject iconGo = new GameObject("WarningIcon");
        iconGo.transform.SetParent(warningRoot.transform, false);
        iconTransform = iconGo.transform;
        iconRenderer = iconGo.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = GetWarningSprite();
        iconRenderer.sortingOrder = 150; // Well above tractor (8) & trailer (10)
        iconRenderer.color = Color.white;

        warningRoot.SetActive(false);
    }

    private void LateUpdate()
    {
        if (truckController == null)
        {
            truckController = Object.FindFirstObjectByType<TruckController>();
            if (truckController == null) return;
        }

        if (trailerRb == null)
        {
            FindTrailer();
            if (trailerRb == null) return;
        }

        if (warningRoot == null)
        {
            BuildVisualObjects();
        }

        float maxAngle = truckController.MaxArticulationAngle;
        float warningThreshold = Mathf.Max(10f, maxAngle - warningMarginDeg);

        float tractorRot = (tractorRb != null) ? tractorRb.rotation : transform.eulerAngles.z;
        float trailerRot = (trailerRb != null) ? trailerRb.rotation : 0f;
        float deltaAngle = Mathf.DeltaAngle(tractorRot, trailerRot);
        float absDelta = Mathf.Abs(deltaAngle);

        bool shouldWarn = (absDelta >= warningThreshold) || truckController.IsJackknifed;

        if (shouldWarn)
        {
            IsWarningActive = true;
            if (!warningRoot.activeSelf) warningRoot.SetActive(true);

            // 1. Position centered at the 5th wheel hitch coupling
            Vector2 pinchPos = truckController.GetJackknifePinchPoint();
            warningRoot.transform.position = new Vector3(pinchPos.x, pinchPos.y, -1.5f);

            // 2. Always face camera upright (so exclamation mark is upright to the player)
            Camera cam = Camera.main;
            if (cam != null)
            {
                warningRoot.transform.rotation = cam.transform.rotation;
            }
            else
            {
                warningRoot.transform.rotation = Quaternion.identity;
            }

            // 3. Smooth, calm sine breathing pulsation (non-intrusive warning)
            float wave = Mathf.Sin(Time.time * blinkFrequency * Mathf.PI * 2f) * 0.5f + 0.5f;
            float smoothPulse = Mathf.SmoothStep(0f, 1f, wave);

            if (iconTransform != null)
            {
                float iconScale = Mathf.Lerp(iconMinScale, iconMaxScale, smoothPulse);
                iconTransform.localScale = Vector3.one * iconScale;
            }

            if (iconRenderer != null)
            {
                float alpha = Mathf.Lerp(iconMinAlpha, iconMaxAlpha, smoothPulse);
                iconRenderer.color = new Color(1.0f, 1.0f, 1.0f, alpha);
            }

            if (glowTransform != null)
            {
                float glowScale = Mathf.Lerp(glowMinScale, glowMaxScale, smoothPulse);
                glowTransform.localScale = Vector3.one * glowScale;
            }

            if (glowRenderer != null)
            {
                float glowAlpha = Mathf.Lerp(0.02f, 0.12f, smoothPulse);
                glowRenderer.color = new Color(1.0f, 0.82f, 0.05f, glowAlpha);
            }
        }
        else
        {
            IsWarningActive = false;
            if (warningRoot.activeSelf)
            {
                warningRoot.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (warningRoot != null)
        {
            Destroy(warningRoot);
        }
    }

    // ── Sprite Resources with Robust Procedural Fallback ─────────────────────

    private static Sprite GetWarningSprite()
    {
        if (s_warningSprite != null) return s_warningSprite;

        s_warningSprite = Resources.Load<Sprite>("WarningJackknife");
        if (s_warningSprite != null) return s_warningSprite;

        // Procedural fallback
        s_warningSprite = GenerateFallbackWarningSprite();
        return s_warningSprite;
    }

    private static Sprite GetGlowSprite()
    {
        if (s_glowSprite != null) return s_glowSprite;

        s_glowSprite = Resources.Load<Sprite>("WarningJackknifeGlow");
        if (s_glowSprite != null) return s_glowSprite;

        s_glowSprite = GenerateFallbackGlowSprite();
        return s_glowSprite;
    }

    private static Sprite GenerateFallbackWarningSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float r = size * 0.46f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                Color col = Color.clear;

                if (dist <= r)
                {
                    if (dist > r - 8f)
                    {
                        col = new Color(0.12f, 0.12f, 0.15f, 1.0f); // Dark outer ring
                    }
                    else
                    {
                        col = new Color(1.0f, 0.82f, 0.05f, 1.0f); // Hazard yellow fill

                        // Exclamation mark upper bar
                        if (x >= center.x - 4f && x <= center.x + 4f && y >= center.y - 8f && y <= center.y + 24f)
                        {
                            col = new Color(0.12f, 0.12f, 0.15f, 1.0f);
                        }
                        // Exclamation mark dot
                        else if (Vector2.Distance(new Vector2(x, y), new Vector2(center.x, center.y - 18f)) <= 5f)
                        {
                            col = new Color(0.12f, 0.12f, 0.15f, 1.0f);
                        }
                    }
                }

                cols[y * size + x] = col;
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite GenerateFallbackGlowSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float t = Mathf.Clamp01(dist / r);
                float alpha = (1f - t) * (1f - t);
                cols[y * size + x] = new Color(1.0f, 0.82f, 0.05f, alpha * 0.7f);
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
