using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Route / Polyline Guide Tool Component.
/// Draws a smooth or sharp trajectory guide line with directional arrowheads on the asphalt.
/// Has NO physical collider (truck drives smoothly over it).
/// Configurable color, width, smoothing (Catmull-Rom / Bezier / None), and display mode.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class RouteGuideLine : MonoBehaviour
{
    public enum SmoothingMode
    {
        None = 0,
        CatmullRom = 1,
        Bezier = 2
    }

    public enum DisplayMode
    {
        AlwaysVisible = 0,
        FirstFiveSeconds = 1,
        TrainingOnly = 2
    }

    [Header("Route Waypoints (World Coordinates)")]
    public List<Vector2> waypoints = new List<Vector2>();

    [Header("Line Visuals")]
    [Tooltip("Line width in meters")]
    [Range(0.08f, 1.50f)]
    public float lineWidth = 0.30f;

    [Tooltip("Line color with transparency (Default: Warm Yellow #FFD500 with Alpha 0.55)")]
    public Color lineColor = new Color(1.0f, 0.835f, 0.0f, 0.55f);

    [Tooltip("Smoothing algorithm between waypoints")]
    public SmoothingMode smoothing = SmoothingMode.CatmullRom;

    [Tooltip("Curve subdivision density per segment")]
    [Range(2, 24)]
    public int curveResolution = 10;

    [Tooltip("Sorting order for 2D rendering (Above asphalt -10 and markings 2, below truck 8-10)")]
    public int sortingOrder = 3;

    [Header("Directional Arrowhead")]
    [Tooltip("Whether to display a directional cone arrowhead at the final waypoint")]
    public bool showEndArrow = true;

    [Tooltip("Scale multiplier for the end arrowhead")]
    [Range(0.4f, 3.0f)]
    public float arrowSize = 1.0f;

    [Header("Display Rules")]
    public DisplayMode displayMode = DisplayMode.AlwaysVisible;

    private LineRenderer lineRenderer;
    private static Material cachedLineMaterial;
    private static Sprite cachedArrowheadSprite;

    private void Awake()
    {
        EnsureComponents();
        UpdateVisuals();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            if (displayMode == DisplayMode.FirstFiveSeconds)
            {
                StartCoroutine(FadeOutAfterDelay(5.0f, 1.5f));
            }
        }
    }

    private void OnValidate()
    {
        lineWidth = Mathf.Max(0.05f, lineWidth);
        arrowSize = Mathf.Max(0.1f, arrowSize);
        curveResolution = Mathf.Clamp(curveResolution, 2, 32);
        UpdateVisuals();
    }

    public void EnsureComponents()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        if (cachedLineMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("UI/Default");

            cachedLineMaterial = new Material(shader)
            {
                name = "RouteGuideLine_Material",
                hideFlags = HideFlags.DontSave
            };
        }

        lineRenderer.material = cachedLineMaterial;
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.sortingOrder = sortingOrder;
    }

    public void UpdateVisuals()
    {
        EnsureComponents();

        if (lineRenderer == null) return;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.sortingOrder = sortingOrder;

        if (waypoints == null || waypoints.Count < 2)
        {
            lineRenderer.positionCount = 0;
            UpdateArrowhead(Vector2.zero, Vector2.up, false);
            return;
        }

        List<Vector3> renderPoints = GenerateCurvePoints();
        lineRenderer.positionCount = renderPoints.Count;
        lineRenderer.SetPositions(renderPoints.ToArray());

        // Update arrowhead at end
        if (showEndArrow && renderPoints.Count >= 2)
        {
            Vector3 lastPt = renderPoints[renderPoints.Count - 1];
            Vector3 prevPt = renderPoints[Mathf.Max(0, renderPoints.Count - 2)];
            Vector2 dir = new Vector2(lastPt.x - prevPt.x, lastPt.y - prevPt.y);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            UpdateArrowhead(lastPt, dir.normalized, true);
        }
        else
        {
            UpdateArrowhead(Vector2.zero, Vector2.up, false);
        }
    }

    private List<Vector3> GenerateCurvePoints()
    {
        List<Vector3> result = new List<Vector3>();
        if (waypoints.Count < 2) return result;

        if (smoothing == SmoothingMode.None || waypoints.Count == 2)
        {
            for (int i = 0; i < waypoints.Count; i++)
            {
                result.Add(new Vector3(waypoints[i].x, waypoints[i].y, 0f));
            }
            return result;
        }

        if (smoothing == SmoothingMode.CatmullRom)
        {
            // Catmull-Rom spline interpolation through all waypoints
            int n = waypoints.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = (i > 0) ? waypoints[i - 1] : (waypoints[0] - (waypoints[1] - waypoints[0]));
                Vector2 p1 = waypoints[i];
                Vector2 p2 = waypoints[i + 1];
                Vector2 p3 = (i + 2 < n) ? waypoints[i + 2] : (p2 + (p2 - p1));

                int steps = Mathf.Max(2, curveResolution);
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    Vector2 pt = EvaluateCatmullRom(p0, p1, p2, p3, t);
                    result.Add(new Vector3(pt.x, pt.y, 0f));
                }
            }
            // Add final point
            result.Add(new Vector3(waypoints[n - 1].x, waypoints[n - 1].y, 0f));
            return result;
        }

        if (smoothing == SmoothingMode.Bezier)
        {
            // Smooth bezier spline using auto-tangents
            int n = waypoints.Count;
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p1 = waypoints[i];
                Vector2 p2 = waypoints[i + 1];

                Vector2 tangent1 = (i > 0) ? (p2 - waypoints[i - 1]).normalized : (p2 - p1).normalized;
                Vector2 tangent2 = (i + 2 < n) ? (waypoints[i + 2] - p1).normalized : (p2 - p1).normalized;

                float segLen = Vector2.Distance(p1, p2) * 0.35f;
                Vector2 cp1 = p1 + tangent1 * segLen;
                Vector2 cp2 = p2 - tangent2 * segLen;

                int steps = Mathf.Max(2, curveResolution);
                for (int step = 0; step < steps; step++)
                {
                    float t = step / (float)steps;
                    Vector2 pt = EvaluateCubicBezier(p1, cp1, cp2, p2, t);
                    result.Add(new Vector3(pt.x, pt.y, 0f));
                }
            }
            result.Add(new Vector3(waypoints[n - 1].x, waypoints[n - 1].y, 0f));
            return result;
        }

        return result;
    }

    private static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2.0f * p1) +
            (-p0 + p2) * t +
            (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
            (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
        );
    }

    private static Vector2 EvaluateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return (uuu * p0) + (3.0f * uu * t * p1) + (3.0f * u * tt * p2) + (ttt * p3);
    }

    private void UpdateArrowhead(Vector2 pos, Vector2 forwardDir, bool visible)
    {
        Transform arrowChild = transform.Find("Arrowhead");
        if (!visible)
        {
            if (arrowChild != null) arrowChild.gameObject.SetActive(false);
            return;
        }

        if (arrowChild == null)
        {
            GameObject go = new GameObject("Arrowhead");
            go.transform.SetParent(transform, false);
            arrowChild = go.transform;
        }

        arrowChild.gameObject.SetActive(true);
        arrowChild.position = new Vector3(pos.x, pos.y, 0f);

        float angle = Mathf.Atan2(forwardDir.y, forwardDir.x) * Mathf.Rad2Deg - 90f;
        arrowChild.rotation = Quaternion.Euler(0f, 0f, angle);

        float baseWidth = lineWidth * 3.8f * arrowSize;
        float baseLength = lineWidth * 4.6f * arrowSize;
        arrowChild.localScale = new Vector3(baseWidth, baseLength, 1f);

        SpriteRenderer sr = arrowChild.GetComponent<SpriteRenderer>();
        if (sr == null) sr = arrowChild.gameObject.AddComponent<SpriteRenderer>();

        if (cachedArrowheadSprite == null)
        {
            cachedArrowheadSprite = GenerateArrowheadSprite();
        }

        sr.sprite = cachedArrowheadSprite;
        if (cachedLineMaterial != null) sr.material = cachedLineMaterial;
        sr.color = lineColor;
        sr.sortingOrder = sortingOrder + 1; // render slightly above the line
    }

    private static Sprite GenerateArrowheadSprite()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(1f, 1f, 1f, 0f);
        Color solidWhite = Color.white;

        // Draw crisp tapered aerodynamic cone arrowhead pointing towards (size/2, size - 4)
        Vector2 tip = new Vector2(size * 0.5f, size - 6f);
        Vector2 baseLeft = new Vector2(size * 0.12f, 8f);
        Vector2 baseRight = new Vector2(size * 0.88f, 8f);
        Vector2 baseCenterIndent = new Vector2(size * 0.5f, 22f); // slight chevron cutout at base

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pt = new Vector2(x, y);
                bool insideLeft = IsPointInTriangle(pt, baseLeft, tip, baseCenterIndent);
                bool insideRight = IsPointInTriangle(pt, baseRight, tip, baseCenterIndent);

                if (insideLeft || insideRight)
                {
                    tex.SetPixel(x, y, solidWhite);
                }
                else
                {
                    tex.SetPixel(x, y, transparent);
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.4f), 128f);
    }

    private static bool IsPointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        float d1 = Sign(pt, v1, v2);
        float d2 = Sign(pt, v2, v3);
        float d3 = Sign(pt, v3, v1);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private IEnumerator FadeOutAfterDelay(float delay, float fadeDuration)
    {
        yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        Color initialColor = lineColor;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            Color c = initialColor;
            c.a = Mathf.Lerp(initialColor.a, 0f, t);
            SetRuntimeColor(c);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private void SetRuntimeColor(Color c)
    {
        if (lineRenderer != null)
        {
            lineRenderer.startColor = c;
            lineRenderer.endColor = c;
        }

        Transform arrowChild = transform.Find("Arrowhead");
        if (arrowChild != null)
        {
            SpriteRenderer sr = arrowChild.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = c;
        }
    }

    public void AddWaypoint(Vector2 pt)
    {
        waypoints.Add(pt);
        UpdateVisuals();
    }

    public void InsertWaypoint(int index, Vector2 pt)
    {
        if (index >= 0 && index <= waypoints.Count)
        {
            waypoints.Insert(index, pt);
            UpdateVisuals();
        }
    }

    public void RemoveWaypoint(int index)
    {
        if (index >= 0 && index < waypoints.Count)
        {
            waypoints.RemoveAt(index);
            UpdateVisuals();
        }
    }

    public void SetWaypoint(int index, Vector2 pt)
    {
        if (index >= 0 && index < waypoints.Count)
        {
            waypoints[index] = pt;
            UpdateVisuals();
        }
    }
}
