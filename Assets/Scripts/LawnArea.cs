using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lawn / Grass Closed Polygon Area Component.
/// Defined by a sequence of points strictly at 45° and 90° angles.
/// Renders a procedural 2D grass mesh with a photorealistic concrete curb border.
/// Can act as an obstacle (PolygonCollider2D) or decorative off-road ground.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class LawnArea : MonoBehaviour
{
    [Header("Polygon Vertices (World Coordinates)")]
    public List<Vector2> points = new List<Vector2>();

    [Header("Corner Rounding (Fillet)")]
    [Tooltip("Smooth fillet radius for corners in meters (0 for sharp corners)")]
    [Range(0.0f, 2.0f)]
    public float cornerRadius = 0.5f;

    [Header("Grass Visuals")]
    [Tooltip("Grass tint color (default White to preserve photorealistic texture colors)")]
    public Color grassColor = Color.white;

    [Tooltip("Whether to use repeating grass texture")]
    public bool useTexture = true;

    [Tooltip("Texture tile size in meters")]
    [Range(1.0f, 15.0f)]
    public float textureTileSize = 3.5f;

    [Tooltip("Sorting order for 2D rendering (Above asphalt -10, below markings 2)")]
    public int sortingOrder = -5;

    [Header("Curb Border")]
    [Tooltip("Whether to draw a concrete curb around the lawn perimeter")]
    public bool showCurb = true;

    [Tooltip("Curb border thickness in meters")]
    [Range(0.10f, 0.80f)]
    public float curbWidth = 0.32f;

    [Tooltip("Length in meters for one curb stone texture repeat (each block is 1/4 of this)")]
    [Range(1.0f, 8.0f)]
    public float curbBlockLength = 3.0f;

    [Tooltip("Curb placement: 0.5 = centered on boundary, 1.0 = strictly inside boundary")]
    [Range(0.0f, 1.0f)]
    public float curbOffset = 0.5f;

    [Tooltip("Curb stone / concrete tint color")]
    public Color curbColor = Color.white;

    [Header("Corner Mulch Detailing")]
    [Tooltip("Fill sharp acute corners (< 65°) with dark wood bark mulch as seen in reference")]
    public bool showCornerMulch = true;

    [Tooltip("Size of mulch in acute corners")]
    [Range(0.5f, 3.0f)]
    public float mulchSize = 1.3f;

    [Header("Flower Shrubs")]
    [Tooltip("Place decorative flowering shrubs on the lawn")]
    public bool showFlowers = true;

    [Tooltip("Number of flower shrubs on the lawn")]
    [Range(0, 10)]
    public int flowerCount = 3;

    [Tooltip("Size scale of flower shrubs")]
    [Range(0.5f, 2.5f)]
    public float flowerScale = 1.0f;

    [Header("Physics and Collision")]
    [Tooltip("If enabled, truck crashes/collides when driving onto the lawn/curb")]
    public bool isObstacle = true;

    // Internal components
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private PolygonCollider2D polyCollider;

    private static Material s_GrassMaterial;
    private static Material s_CurbMaterial;
    private static Material s_MulchMaterial;
    private static Texture2D s_GrassTexture;
    private static Texture2D s_CurbTexture;
    private static Texture2D s_MulchTexture;
    private static Sprite s_FlowerSprite;

    private void Awake()
    {
        UpdateVisuals();
    }

    private void OnValidate()
    {
        textureTileSize = Mathf.Max(0.5f, textureTileSize);
        curbWidth = Mathf.Max(0.05f, curbWidth);
        curbBlockLength = Mathf.Max(0.5f, curbBlockLength);
        cornerRadius = Mathf.Max(0.0f, cornerRadius);
        UpdateVisuals();
    }

    private static void LoadResources()
    {
        if (s_GrassTexture == null)
        {
#if UNITY_EDITOR
            s_GrassTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/GrassGround.png");
#endif
            if (s_GrassTexture == null) s_GrassTexture = Resources.Load<Texture2D>("GeneratedSprites/GrassGround");
        }

        if (s_CurbTexture == null)
        {
#if UNITY_EDITOR
            s_CurbTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/LawnCurb.png");
            if (s_CurbTexture == null)
            {
                s_CurbTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/Curb.png");
            }
#endif
            if (s_CurbTexture == null) s_CurbTexture = Resources.Load<Texture2D>("GeneratedSprites/LawnCurb");
        }

        if (s_MulchTexture == null)
        {
#if UNITY_EDITOR
            s_MulchTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/LawnMulch.png");
#endif
            if (s_MulchTexture == null) s_MulchTexture = Resources.Load<Texture2D>("GeneratedSprites/LawnMulch");
        }

        if (s_FlowerSprite == null)
        {
#if UNITY_EDITOR
            s_FlowerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/LawnFlower.png");
#endif
            if (s_FlowerSprite == null) s_FlowerSprite = Resources.Load<Sprite>("GeneratedSprites/LawnFlower");
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
        if (unlitShader == null) unlitShader = Shader.Find("UI/Default");

        if (s_GrassMaterial == null)
        {
            s_GrassMaterial = new Material(unlitShader)
            {
                name = "LawnGrass_Material",
                hideFlags = HideFlags.DontSave
            };
        }

        if (s_CurbMaterial == null)
        {
            s_CurbMaterial = new Material(unlitShader)
            {
                name = "LawnCurb_Material",
                hideFlags = HideFlags.DontSave
            };
        }

        if (s_MulchMaterial == null)
        {
            s_MulchMaterial = new Material(unlitShader)
            {
                name = "LawnMulch_Material",
                hideFlags = HideFlags.DontSave
            };
        }
    }

    public void UpdateVisuals()
    {
        LoadResources();

        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // Configure Grass Material
        Material grassMat = new Material(s_GrassMaterial);
        Texture2D grassTex = (useTexture && s_GrassTexture != null) ? s_GrassTexture : Texture2D.whiteTexture;
        if (grassMat.HasProperty("_MainTex")) grassMat.SetTexture("_MainTex", grassTex);
        if (grassMat.HasProperty("_BaseMap")) grassMat.SetTexture("_BaseMap", grassTex);
        if (grassMat.HasProperty("_Color")) grassMat.SetColor("_Color", grassColor);
        if (grassMat.HasProperty("_BaseColor")) grassMat.SetColor("_BaseColor", grassColor);
        meshRenderer.material = grassMat;

        if (points == null || points.Count < 3)
        {
            if (meshFilter.sharedMesh != null) meshFilter.sharedMesh.Clear();
            UpdateCurbBorderMesh(null, null, false);
            UpdateCornerMulch(null, false);
            UpdateFlowerShrubs(null, false);
            UpdateCollider(null, false);
            return;
        }

        // 1. Normalize orientation to Counter-Clockwise (CCW)
        List<Vector2> ccwPoints = NormalizeCCW(points);

        // 2. Corner Fillet (Smoothing)
        List<Vector2> contour = (cornerRadius > 0.01f) ? FilletPolygon(ccwPoints, cornerRadius, 5) : ccwPoints;

        // 3. Generate Curb Strip Geometry (Inner and Outer contours)
        List<Vector2> vOuter, vInner;
        ComputeCurbContours(contour, curbWidth, curbOffset, out vOuter, out vInner);

        // 4. Build Procedural Grass Mesh
        // The grass fills the inner boundary of the curb (or contour if no curb)
        List<Vector2> grassPoints = showCurb ? vInner : contour;
        BuildGrassMesh(grassPoints);

        // 5. Build Procedural Curb Mesh
        UpdateCurbBorderMesh(vOuter, vInner, showCurb);

        // 6. Corner Mulch Detailing for acute corners (< 65°)
        UpdateCornerMulch(ccwPoints, showCornerMulch);

        // 7. Decorative Flower Shrubs
        UpdateFlowerShrubs(grassPoints, showFlowers);

        // 8. Update Physics Collider
        UpdateCollider(showCurb ? vOuter : contour, isObstacle);
    }

    private void BuildGrassMesh(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 3)
        {
            if (meshFilter.sharedMesh != null) meshFilter.sharedMesh.Clear();
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "Lawn_Mesh" };
            meshFilter.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        int n = pts.Count;
        Vector3[] vertices = new Vector3[n];
        Vector2[] uvs = new Vector2[n];
        Color[] colors = new Color[n];
        Vector3[] normals = new Vector3[n];

        float tileSize = Mathf.Max(0.5f, textureTileSize);
        for (int i = 0; i < n; i++)
        {
            Vector2 p = pts[i];
            vertices[i] = new Vector3(p.x, p.y, 0f);
            uvs[i] = new Vector2(p.x / tileSize, p.y / tileSize);
            colors[i] = grassColor;
            normals[i] = Vector3.back;
        }

        int[] triangles = Triangulate(pts);

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void UpdateCurbBorderMesh(List<Vector2> vOuter, List<Vector2> vInner, bool visible)
    {
        Transform curbTr = transform.Find("CurbBorder");
        if (!visible || vOuter == null || vInner == null || vOuter.Count < 3)
        {
            if (curbTr != null) curbTr.gameObject.SetActive(false);
            return;
        }

        if (curbTr == null)
        {
            GameObject curbGo = new GameObject("CurbBorder");
            curbGo.transform.SetParent(transform, false);
            curbTr = curbGo.transform;
        }

        curbTr.gameObject.SetActive(true);
        curbTr.localPosition = Vector3.zero;
        curbTr.localRotation = Quaternion.identity;
        curbTr.localScale = Vector3.one;

        // Clean up legacy LineRenderer if present
        LineRenderer lr = curbTr.GetComponent<LineRenderer>();
        if (lr != null) DestroyImmediate(lr);

        MeshFilter mf = curbTr.GetComponent<MeshFilter>();
        if (mf == null) mf = curbTr.gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = curbTr.GetComponent<MeshRenderer>();
        if (mr == null) mr = curbTr.gameObject.AddComponent<MeshRenderer>();

        mr.sortingOrder = sortingOrder + 1; // On top of grass
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Material curbMat = new Material(s_CurbMaterial);
        Texture2D tex = (s_CurbTexture != null) ? s_CurbTexture : Texture2D.whiteTexture;
        if (curbMat.HasProperty("_MainTex")) curbMat.SetTexture("_MainTex", tex);
        if (curbMat.HasProperty("_BaseMap")) curbMat.SetTexture("_BaseMap", tex);
        if (curbMat.HasProperty("_Color")) curbMat.SetColor("_Color", curbColor);
        if (curbMat.HasProperty("_BaseColor")) curbMat.SetColor("_BaseColor", curbColor);
        mr.material = curbMat;

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "Curb_Mesh" };
            mf.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        int n = vOuter.Count;
        float[] distances = new float[n + 1];
        float totalDist = 0f;
        for (int i = 0; i < n; i++)
        {
            distances[i] = totalDist;
            int next = (i + 1) % n;
            totalDist += Vector2.Distance(vOuter[i], vOuter[next]);
        }
        distances[n] = totalDist;

        float blockLen = Mathf.Max(0.5f, curbBlockLength);
        int totalBlocks = Mathf.Max(1, Mathf.RoundToInt(totalDist / blockLen));

        Vector3[] vertices = new Vector3[n * 2];
        Vector2[] uvs = new Vector2[n * 2];
        Color[] colors = new Color[n * 2];
        Vector3[] normals = new Vector3[n * 2];

        for (int i = 0; i < n; i++)
        {
            float u = (distances[i] / totalDist) * totalBlocks;

            // Inner vertex (grass side)
            vertices[i * 2] = new Vector3(vInner[i].x, vInner[i].y, 0f);
            uvs[i * 2] = new Vector2(u, 0.0f);
            colors[i * 2] = curbColor;
            normals[i * 2] = Vector3.back;

            // Outer vertex (road/asphalt side with sunlit bevel)
            vertices[i * 2 + 1] = new Vector3(vOuter[i].x, vOuter[i].y, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1.0f);
            colors[i * 2 + 1] = curbColor;
            normals[i * 2 + 1] = Vector3.back;
        }

        int[] triangles = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int inA = i * 2;
            int outA = i * 2 + 1;
            int inB = next * 2;
            int outB = next * 2 + 1;

            int triIdx = i * 6;
            triangles[triIdx] = inA;
            triangles[triIdx + 1] = outA;
            triangles[triIdx + 2] = outB;

            triangles[triIdx + 3] = inA;
            triangles[triIdx + 4] = outB;
            triangles[triIdx + 5] = inB;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private void UpdateCornerMulch(List<Vector2> rawPts, bool visible)
    {
        Transform mulchRoot = transform.Find("CornerMulch");
        if (!visible || rawPts == null || rawPts.Count < 3 || s_MulchTexture == null)
        {
            if (mulchRoot != null) mulchRoot.gameObject.SetActive(false);
            return;
        }

        if (mulchRoot == null)
        {
            GameObject go = new GameObject("CornerMulch");
            go.transform.SetParent(transform, false);
            mulchRoot = go.transform;
        }

        mulchRoot.gameObject.SetActive(true);
        mulchRoot.localPosition = Vector3.zero;
        mulchRoot.localRotation = Quaternion.identity;
        mulchRoot.localScale = Vector3.one;

        MeshFilter mf = mulchRoot.GetComponent<MeshFilter>();
        if (mf == null) mf = mulchRoot.gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = mulchRoot.GetComponent<MeshRenderer>();
        if (mr == null) mr = mulchRoot.gameObject.AddComponent<MeshRenderer>();

        mr.sortingOrder = sortingOrder + 1; // Above grass, under curb
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        Material mulchMat = new Material(s_MulchMaterial);
        if (mulchMat.HasProperty("_MainTex")) mulchMat.SetTexture("_MainTex", s_MulchTexture);
        if (mulchMat.HasProperty("_BaseMap")) mulchMat.SetTexture("_BaseMap", s_MulchTexture);
        mr.material = mulchMat;

        Mesh mesh = mf.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "Mulch_Mesh" };
            mf.sharedMesh = mesh;
        }
        else
        {
            mesh.Clear();
        }

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        int n = rawPts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = rawPts[(i - 1 + n) % n];
            Vector2 curr = rawPts[i];
            Vector2 next = rawPts[(i + 1) % n];

            Vector2 v1 = (prev - curr).normalized;
            Vector2 v2 = (next - curr).normalized;

            float angle = Vector2.Angle(v1, v2);
            // Detect acute corners (< 65°, such as 45° corners)
            if (angle < 65f && angle > 15f)
            {
                float len1 = Vector2.Distance(prev, curr);
                float len2 = Vector2.Distance(next, curr);
                float mDist = Mathf.Min(mulchSize, Mathf.Min(len1, len2) * 0.40f);

                Vector2 p1 = curr + v1 * mDist;
                Vector2 p2 = curr + v2 * mDist;

                int baseIdx = verts.Count;
                verts.Add(new Vector3(curr.x, curr.y, 0f));
                verts.Add(new Vector3(p1.x, p1.y, 0f));
                verts.Add(new Vector3(p2.x, p2.y, 0f));

                uvs.Add(curr / 2.0f);
                uvs.Add(p1 / 2.0f);
                uvs.Add(p2 / 2.0f);

                tris.Add(baseIdx);
                tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 2);
            }
        }

        if (verts.Count > 0)
        {
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateBounds();
        }
    }

    private void UpdateFlowerShrubs(List<Vector2> grassPts, bool visible)
    {
        Transform flowerRoot = transform.Find("FlowerDecorations");
        if (!visible || flowerCount <= 0 || grassPts == null || grassPts.Count < 3 || s_FlowerSprite == null)
        {
            if (flowerRoot != null) flowerRoot.gameObject.SetActive(false);
            return;
        }

        if (flowerRoot == null)
        {
            GameObject go = new GameObject("FlowerDecorations");
            go.transform.SetParent(transform, false);
            flowerRoot = go.transform;
        }

        flowerRoot.gameObject.SetActive(true);
        flowerRoot.localPosition = Vector3.zero;
        flowerRoot.localRotation = Quaternion.identity;
        flowerRoot.localScale = Vector3.one;

        Vector2 centroid = ComputeCentroid();
        UnityEngine.Random.InitState(Mathf.RoundToInt(centroid.x * 100f) ^ Mathf.RoundToInt(centroid.y * 100f));

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 p in grassPts)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        List<Vector2> validPositions = new List<Vector2>();
        int attempts = 0;
        float margin = Mathf.Min(1.2f, curbWidth * 2.5f);
        while (validPositions.Count < flowerCount && attempts < 100)
        {
            attempts++;
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(minX + margin, maxX - margin),
                UnityEngine.Random.Range(minY + margin, maxY - margin)
            );

            if (IsPointInside(candidate) && DistanceToBorder(candidate) > margin)
            {
                bool tooClose = false;
                foreach (Vector2 pos in validPositions)
                {
                    if (Vector2.Distance(candidate, pos) < 2.0f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose) validPositions.Add(candidate);
            }
        }

        while (flowerRoot.childCount < validPositions.Count)
        {
            GameObject fg = new GameObject("Flower_" + flowerRoot.childCount);
            fg.transform.SetParent(flowerRoot, false);
            fg.AddComponent<SpriteRenderer>();
        }

        for (int i = 0; i < flowerRoot.childCount; i++)
        {
            Transform child = flowerRoot.GetChild(i);
            if (i < validPositions.Count)
            {
                child.gameObject.SetActive(true);
                child.position = new Vector3(validPositions[i].x, validPositions[i].y, 0f);
                float sc = flowerScale * UnityEngine.Random.Range(0.85f, 1.25f);
                child.localScale = new Vector3(sc, sc, 1f);
                child.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = s_FlowerSprite;
                    sr.sortingOrder = sortingOrder + 2;
                }
            }
            else
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateCollider(List<Vector2> colliderPoints, bool active)
    {
        if (polyCollider == null) polyCollider = GetComponent<PolygonCollider2D>();
        if (!active || colliderPoints == null || colliderPoints.Count < 3)
        {
            if (polyCollider != null) polyCollider.enabled = false;
            return;
        }

        if (polyCollider == null) polyCollider = gameObject.AddComponent<PolygonCollider2D>();

        polyCollider.enabled = true;
        polyCollider.isTrigger = true;
        polyCollider.pathCount = 1;
        polyCollider.SetPath(0, colliderPoints.ToArray());
    }

    #region Geometry Algorithms (Fillet, CCW, Curb Offset)

    public static List<Vector2> NormalizeCCW(IList<Vector2> pts)
    {
        if (pts == null || pts.Count < 3) return new List<Vector2>(pts ?? new Vector2[0]);
        List<Vector2> res = new List<Vector2>(pts);
        if (ComputeArea(res) < 0f)
        {
            res.Reverse();
        }
        return res;
    }

    public static List<Vector2> FilletPolygon(IList<Vector2> pts, float radius, int subdivs = 5)
    {
        if (pts == null || pts.Count < 3) return new List<Vector2>(pts ?? new Vector2[0]);
        int n = pts.Count;
        List<Vector2> outPts = new List<Vector2>();

        for (int i = 0; i < n; i++)
        {
            Vector2 pPrev = pts[(i - 1 + n) % n];
            Vector2 pCurr = pts[i];
            Vector2 pNext = pts[(i + 1) % n];

            Vector2 v1 = pPrev - pCurr;
            Vector2 v2 = pNext - pCurr;
            float d1 = v1.magnitude;
            float d2 = v2.magnitude;

            if (d1 < 1e-4f || d2 < 1e-4f)
            {
                outPts.Add(pCurr);
                continue;
            }

            Vector2 u1 = v1 / d1;
            Vector2 u2 = v2 / d2;
            float cosAng = Mathf.Clamp(Vector2.Dot(u1, u2), -1.0f, 1.0f);
            float ang = Mathf.Acos(cosAng);

            if (ang < 0.05f || ang > Mathf.PI - 0.05f)
            {
                outPts.Add(pCurr);
                continue;
            }

            float halfAng = ang * 0.5f;
            float tDist = radius / Mathf.Tan(halfAng);
            float maxT = Mathf.Min(d1 * 0.42f, d2 * 0.42f);
            tDist = Mathf.Min(tDist, maxT);
            float actualR = tDist * Mathf.Tan(halfAng);

            Vector2 t1 = pCurr + u1 * tDist;
            Vector2 t2 = pCurr + u2 * tDist;

            Vector2 bisector = u1 + u2;
            float bisLen = bisector.magnitude;
            if (bisLen < 1e-4f)
            {
                outPts.Add(pCurr);
                continue;
            }
            Vector2 bisDir = bisector / bisLen;
            float centerDist = tDist / Mathf.Cos(halfAng);
            Vector2 center = pCurr + bisDir * centerDist;

            float a1 = Mathf.Atan2(t1.y - center.y, t1.x - center.x);
            float a2 = Mathf.Atan2(t2.y - center.y, t2.x - center.x);
            float da = a2 - a1;
            while (da > Mathf.PI) da -= 2f * Mathf.PI;
            while (da < -Mathf.PI) da += 2f * Mathf.PI;

            for (int s = 0; s <= subdivs; s++)
            {
                float f = s / (float)subdivs;
                float a = a1 + da * f;
                Vector2 pt = center + actualR * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                outPts.Add(pt);
            }
        }

        return outPts;
    }

    public static void ComputeCurbContours(IList<Vector2> pts, float width, float offset, out List<Vector2> vOuter, out List<Vector2> vInner)
    {
        int n = pts.Count;
        vOuter = new List<Vector2>(n);
        vInner = new List<Vector2>(n);

        for (int i = 0; i < n; i++)
        {
            Vector2 prevP = pts[(i - 1 + n) % n];
            Vector2 currP = pts[i];
            Vector2 nextP = pts[(i + 1) % n];

            Vector2 dIn = (currP - prevP).normalized;
            Vector2 dOut = (nextP - currP).normalized;

            Vector2 nIn = new Vector2(dIn.y, -dIn.x);
            Vector2 nOut = new Vector2(dOut.y, -dOut.x);

            Vector2 nAvg = (nIn + nOut).normalized;
            if (nAvg.sqrMagnitude < 1e-4f) nAvg = nIn;

            float dot = Mathf.Max(0.5f, Vector2.Dot(nAvg, nIn));
            Vector2 miter = nAvg / dot;
            if (miter.magnitude > 1.6f) miter = miter.normalized * 1.6f;

            Vector2 outerPt = currP + miter * (width * (1.0f - offset));
            Vector2 innerPt = currP - miter * (width * offset);

            vOuter.Add(outerPt);
            vInner.Add(innerPt);
        }
    }

    #endregion

    #region Ear Clipping Triangulation

    public static int[] Triangulate(IList<Vector2> pts)
    {
        if (pts == null || pts.Count < 3) return new int[0];
        int n = pts.Count;
        List<int> indices = new List<int>(n);
        for (int i = 0; i < n; i++) indices.Add(i);

        if (ComputeArea(pts) < 0f)
        {
            indices.Reverse();
        }

        List<int> triangles = new List<int>((n - 2) * 3);
        int count = indices.Count;
        int maxIterations = n * 5;
        int iterations = 0;

        while (count > 2 && iterations < maxIterations)
        {
            iterations++;
            bool earFound = false;

            for (int i = 0; i < count; i++)
            {
                int prevIdx = indices[(i - 1 + count) % count];
                int currIdx = indices[i];
                int nextIdx = indices[(i + 1) % count];

                Vector2 a = pts[prevIdx];
                Vector2 b = pts[currIdx];
                Vector2 c = pts[nextIdx];

                float cp = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                if (cp <= 1e-6f) continue;

                bool hasInside = false;
                for (int j = 0; j < count; j++)
                {
                    if (j == (i - 1 + count) % count || j == i || j == (i + 1) % count) continue;
                    Vector2 p = pts[indices[j]];
                    if (IsPointInTriangle(p, a, b, c))
                    {
                        hasInside = true;
                        break;
                    }
                }

                if (!hasInside)
                {
                    triangles.Add(prevIdx);
                    triangles.Add(currIdx);
                    triangles.Add(nextIdx);
                    indices.RemoveAt(i);
                    count--;
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                if (count >= 3)
                {
                    triangles.Add(indices[0]);
                    triangles.Add(indices[1]);
                    triangles.Add(indices[2]);
                    indices.RemoveAt(1);
                    count--;
                }
                else
                {
                    break;
                }
            }
        }

        return triangles.ToArray();
    }

    public static float ComputeArea(IList<Vector2> pts)
    {
        if (pts == null || pts.Count < 3) return 0f;
        float area = 0f;
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += pts[i].x * pts[j].y - pts[j].x * pts[i].y;
        }
        return area * 0.5f;
    }

    private static bool IsPointInTriangle(Vector2 pt, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        float d1 = (pt.x - v3.x) * (v2.y - v3.y) - (v2.x - v3.x) * (pt.y - v3.y);
        float d2 = (pt.x - v1.x) * (v3.y - v1.y) - (v3.x - v1.x) * (pt.y - v1.y);
        float d3 = (pt.x - v2.x) * (v1.y - v2.y) - (v1.x - v2.x) * (pt.y - v2.y);

        bool hasNeg = (d1 < -1e-5f) || (d2 < -1e-5f) || (d3 < -1e-5f);
        bool hasPos = (d1 > 1e-5f) || (d2 > 1e-5f) || (d3 > 1e-5f);

        return !(hasNeg && hasPos);
    }

    #endregion

    #region Geometry Queries and Editing

    public Vector2 ComputeCentroid()
    {
        if (points == null || points.Count == 0) return (Vector2)transform.position;
        Vector2 sum = Vector2.zero;
        for (int i = 0; i < points.Count; i++) sum += points[i];
        return sum / points.Count;
    }

    public bool IsPointInside(Vector2 p)
    {
        if (points == null || points.Count < 3) return false;
        bool inside = false;
        int j = points.Count - 1;
        for (int i = 0; i < points.Count; i++)
        {
            if (((points[i].y > p.y) != (points[j].y > p.y)) &&
                (p.x < (points[j].x - points[i].x) * (p.y - points[i].y) / (points[j].y - points[i].y) + points[i].x))
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    public float DistanceToBorder(Vector2 p)
    {
        if (points == null || points.Count == 0) return float.MaxValue;
        if (points.Count == 1) return Vector2.Distance(p, points[0]);

        float minDist = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            float d = DistanceToSegment(p, a, b);
            if (d < minDist) minDist = d;
        }
        return minDist;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 projection = a + t * ab;
        return Vector2.Distance(p, projection);
    }

    public void AddPoint(Vector2 pt)
    {
        points.Add(pt);
        UpdateVisuals();
    }

    public void InsertPoint(int index, Vector2 pt)
    {
        if (index >= 0 && index <= points.Count)
        {
            points.Insert(index, pt);
            UpdateVisuals();
        }
    }

    public void RemovePoint(int index)
    {
        if (index >= 0 && index < points.Count && points.Count > 3)
        {
            points.RemoveAt(index);
            UpdateVisuals();
        }
    }

    public void SetPoint(int index, Vector2 pt)
    {
        if (index >= 0 && index < points.Count)
        {
            points[index] = pt;
            UpdateVisuals();
        }
    }

    #endregion
}