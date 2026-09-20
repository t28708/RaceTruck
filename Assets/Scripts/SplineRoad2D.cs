using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SplineRoad2D: High-performance procedural 2D curved road generator for Unity 6 (Mobile-optimized).
/// Generates smooth asphalt roads with concrete/dark curbs and physical EdgeCollider2Ds along Catmull-Rom splines.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DisallowMultipleComponent]
public class SplineRoad2D : MonoBehaviour
{
    public enum SplineInterpolation
    {
        CatmullRom = 0,
        Bezier = 1
    }

    [Header("Spline Control Points (Local Space)")]
    [Tooltip("List of control points defining the road spline path in local coordinates.")]
    public List<Vector2> points = new List<Vector2>();

    [Tooltip("Whether the spline connects back to the start point forming a closed circuit.")]
    [SerializeField] private bool isClosedLoop = false;

    [Tooltip("Interpolation algorithm for smooth curves.")]
    public SplineInterpolation interpolation = SplineInterpolation.CatmullRom;

    [Header("Road Dimensions")]
    [Tooltip("Width of the drivable asphalt surface in meters.")]
    [Range(2.0f, 30.0f)]
    public float roadWidth = 8.0f;

    [Tooltip("Width of the curb border on each side in meters.")]
    [Range(0.0f, 2.0f)]
    public float borderWidth = 0.8f;

    [Header("Curve Resolution & Tiling")]
    [Tooltip("Number of subdivision segments between adjacent control points.")]
    [Range(2, 40)]
    public int segmentsPerCurve = 12;

    [Tooltip("Texture tile size in meters along the road for asphalt.")]
    [Range(0.5f, 20.0f)]
    public float asphaltTileSize = 4.0f;

    [Tooltip("Texture tile size in meters along the curb border.")]
    [Range(0.2f, 10.0f)]
    public float curbTileSize = 2.0f;

    [Header("Visual Appearance")]
    [Tooltip("Tint color for asphalt road surface.")]
    public Color asphaltColor = Color.white;

    [Tooltip("Tint color for the curb borders (stone concrete white/grey).")]
    public Color curbColor = Color.white;

    [Tooltip("Sorting order for 2D rendering (Default: -8, above ground -10, below markings 2).")]
    public int sortingOrder = -8;

    [Header("Materials (Optional custom overrides)")]
    public Material customAsphaltMaterial;
    public Material customCurbMaterial;

    [Header("Physics Colliders")]
    [Tooltip("Whether to generate EdgeCollider2D obstacles on Left and Right borders.")]
    public bool createColliders = true;

    [Tooltip("Whether the border colliders act as triggers (compatible with TruckCollisionDetector).")]
    public bool collidersAreTrigger = true;

    [Tooltip("Collider edge placement: 0 = road edge, 1 = outer curb edge.")]
    [Range(0.0f, 1.0f)]
    public float colliderPositionOffset = 1.0f;

    [Header("Truck Turn Safety Highlighting")]
    [Tooltip("Highlight turns where the truck won't pass by changing curb borders from red to orange.")]
    public bool highlightImpassableTurns = true;

    [Tooltip("Minimum curve radius in meters required for truck to pass without getting stuck/jackknifing.")]
    public float minPassableRadius = 13.5f;

    // Component references
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh roadMesh;

    // Child colliders
    private EdgeCollider2D leftBorderCollider;
    private EdgeCollider2D rightBorderCollider;

    // Reusable buffers for zero-allocation mesh rebuilds (Mobile optimization)
    private readonly List<Vector3> m_Vertices = new List<Vector3>(256);
    private readonly List<Vector2> m_UVs = new List<Vector2>(256);
    private readonly List<Color32> m_Colors = new List<Color32>(256);
    private readonly List<int> m_RoadTriangles = new List<int>(512);
    private readonly List<int> m_CurbTriangles = new List<int>(512);
    private readonly List<int> m_OrangeCurbTriangles = new List<int>(512);
    private readonly List<Vector2> m_LeftColliderPoints = new List<Vector2>(128);
    private readonly List<Vector2> m_RightColliderPoints = new List<Vector2>(128);

    private readonly List<Vector2> m_SampledCenterline = new List<Vector2>(256);
    private readonly List<float> m_SampledDistances = new List<float>(256);

    // Dirty state tracking
    private bool isDirty = false;

    // Static default textures and materials
    private static Material s_DefaultAsphaltMat;
    private static Material s_DefaultCurbMat;
    private static Material s_DefaultOrangeCurbMat;
    private static Texture2D s_AsphaltTex;
    private static Texture2D s_CurbTex;
    private static Texture2D s_OrangeCurbTex;

    public bool IsClosedLoop
    {
        get => isClosedLoop;
        set => SetClosedLoop(value);
    }
    public int PointCount => points != null ? points.Count : 0;

    private void Awake()
    {
        EnsureComponents();
        RebuildMesh();
    }

    private void Start()
    {
        if (roadMesh == null || roadMesh.vertexCount == 0)
        {
            RebuildMesh();
        }
    }

    private void OnValidate()
    {
        roadWidth = Mathf.Max(1.0f, roadWidth);
        borderWidth = Mathf.Max(0.0f, borderWidth);
        segmentsPerCurve = Mathf.Clamp(segmentsPerCurve, 2, 64);
        asphaltTileSize = Mathf.Max(0.5f, asphaltTileSize);
        curbTileSize = Mathf.Max(0.2f, curbTileSize);

        isDirty = true;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isDirty)
                {
                    RebuildMesh();
                }
            };
            return;
        }
#endif
        RebuildMesh();
    }

    private void EnsureComponents()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        if (roadMesh == null)
        {
            roadMesh = new Mesh
            {
                name = $"SplineRoadMesh_{gameObject.name}"
            };
            meshFilter.sharedMesh = roadMesh;
        }

        UpdateMaterials();
    }

    public void UpdateMaterials()
    {
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = sortingOrder;

        EnsureDefaultResources();

        Material asphaltMat = customAsphaltMaterial != null ? customAsphaltMaterial : s_DefaultAsphaltMat;
        Material curbMat = customCurbMaterial != null ? customCurbMaterial : s_DefaultCurbMat;
        Material orangeCurbMat = s_DefaultOrangeCurbMat;

        if (curbMat != null && s_CurbTex != null && curbMat.mainTexture != s_CurbTex)
        {
            curbMat.mainTexture = s_CurbTex;
        }
        if (orangeCurbMat != null && s_OrangeCurbTex != null && orangeCurbMat.mainTexture != s_OrangeCurbTex)
        {
            orangeCurbMat.mainTexture = s_OrangeCurbTex;
        }

        Material[] currentMats = meshRenderer.sharedMaterials;
        if (currentMats == null || currentMats.Length != 3 || currentMats[0] != asphaltMat || currentMats[1] != curbMat || currentMats[2] != orangeCurbMat)
        {
            meshRenderer.sharedMaterials = new Material[] { asphaltMat, curbMat, orangeCurbMat };
        }
    }

    private static void EnsureDefaultResources()
    {
        if (s_AsphaltTex == null)
        {
#if UNITY_EDITOR
            s_AsphaltTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/AsphaltGround.png");
#endif
            if (s_AsphaltTex == null) s_AsphaltTex = Resources.Load<Texture2D>("GeneratedSprites/AsphaltGround");
        }

        if (s_CurbTex == null)
        {
#if UNITY_EDITOR
            s_CurbTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/RoadCurbRedWhite.png");
            if (s_CurbTex == null) s_CurbTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/LawnCurb.png");
            if (s_CurbTex == null) s_CurbTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/Curb.png");
#endif
            if (s_CurbTex == null) s_CurbTex = Resources.Load<Texture2D>("GeneratedSprites/RoadCurbRedWhite");
            if (s_CurbTex == null) s_CurbTex = Resources.Load<Texture2D>("GeneratedSprites/LawnCurb");
            if (s_CurbTex == null) s_CurbTex = Resources.Load<Texture2D>("GeneratedSprites/Curb");
        }

        if (s_OrangeCurbTex == null)
        {
#if UNITY_EDITOR
            s_OrangeCurbTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/RoadCurbOrangeWhite.png");
#endif
            if (s_OrangeCurbTex == null) s_OrangeCurbTex = Resources.Load<Texture2D>("GeneratedSprites/RoadCurbOrangeWhite");
            if (s_OrangeCurbTex == null) s_OrangeCurbTex = s_CurbTex;
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");

        if (s_DefaultAsphaltMat == null && unlitShader != null)
        {
            s_DefaultAsphaltMat = new Material(unlitShader)
            {
                name = "SplineRoad_Asphalt_Mat",
                mainTexture = s_AsphaltTex
            };
        }

        if (s_DefaultCurbMat == null && unlitShader != null)
        {
            s_DefaultCurbMat = new Material(unlitShader)
            {
                name = "SplineRoad_Curb_Mat",
                mainTexture = s_CurbTex
            };
        }

        if (s_DefaultOrangeCurbMat == null && unlitShader != null)
        {
            s_DefaultOrangeCurbMat = new Material(unlitShader)
            {
                name = "SplineRoad_OrangeCurb_Mat",
                mainTexture = s_OrangeCurbTex != null ? s_OrangeCurbTex : s_CurbTex
            };
        }
    }

    #region Public Builder API

    /// <summary>
    /// Adds a new control point at the end of the spline.
    /// </summary>
    public void AddPoint(Vector2 pos)
    {
        points.Add(pos);
        RebuildMesh();
    }

    /// <summary>
    /// Inserts a control point at the specified index.
    /// </summary>
    public void InsertPoint(int index, Vector2 pos)
    {
        if (index >= 0 && index <= points.Count)
        {
            points.Insert(index, pos);
            RebuildMesh();
        }
    }

    /// <summary>
    /// Removes the control point at the given index.
    /// </summary>
    public void RemovePoint(int index)
    {
        if (index >= 0 && index < points.Count)
        {
            points.RemoveAt(index);
            RebuildMesh();
        }
    }

    /// <summary>
    /// Sets whether the spline forms a closed loop.
    /// </summary>
    public void SetClosedLoop(bool isClosed)
    {
        if (isClosedLoop != isClosed)
        {
            isClosedLoop = isClosed;
            RebuildMesh();
        }
    }

    /// <summary>
    /// Clears all control points.
    /// </summary>
    public void ClearPoints()
    {
        points.Clear();
        RebuildMesh();
    }

    /// <summary>
    /// Sets the position of a specific control point.
    /// </summary>
    public void SetPoint(int index, Vector2 pos)
    {
        if (index >= 0 && index < points.Count)
        {
            points[index] = pos;
            RebuildMesh();
        }
    }

    #endregion

    #region Geometry & Spline Sampling

    /// <summary>
    /// Rebuilds road geometry, UVs, submeshes, and physics EdgeCollider2Ds.
    /// Zero garbage collection allocations during normal execution.
    /// </summary>
    public void RebuildMesh()
    {
        isDirty = false;
        EnsureComponents();

        if (points == null || points.Count < 2)
        {
            if (roadMesh != null) roadMesh.Clear();
            ClearColliders();
            return;
        }

        // 1. Sample spline centerline
        SampleSplineCenterline();

        int sliceCount = m_SampledCenterline.Count;
        if (sliceCount < 2)
        {
            if (roadMesh != null) roadMesh.Clear();
            ClearColliders();
            return;
        }

        // 2. Clear geometry buffers
        m_Vertices.Clear();
        m_UVs.Clear();
        m_Colors.Clear();
        m_RoadTriangles.Clear();
        m_CurbTriangles.Clear();
        m_OrangeCurbTriangles.Clear();
        m_LeftColliderPoints.Clear();
        m_RightColliderPoints.Clear();

        float halfRoad = roadWidth * 0.5f;
        float halfTotal = halfRoad + borderWidth;
        float colliderOffset = halfRoad + borderWidth * 0.5f;

        Color32 asphaltCol32 = asphaltColor;
        Color32 curbCol32 = curbColor;

        // 3. Generate vertices
        // We generate independent vertices for road and curbs so UVs are never shared or distorted.
        // First: 2 vertices per slice for road asphalt
        for (int i = 0; i < sliceCount; i++)
        {
            Vector2 center = m_SampledCenterline[i];
            float dist = m_SampledDistances[i];
            Vector2 normal = GetSliceNormal(i, sliceCount);

            Vector2 vRoadL = center - normal * halfRoad;
            Vector2 vRoadR = center + normal * halfRoad;

            m_Vertices.Add(new Vector3(vRoadL.x, vRoadL.y, 0f));
            m_Vertices.Add(new Vector3(vRoadR.x, vRoadR.y, 0f));

            float vAsphalt = dist / asphaltTileSize;
            float uRoadRepeat = roadWidth / asphaltTileSize;

            m_UVs.Add(new Vector2(0f, vAsphalt));
            m_UVs.Add(new Vector2(uRoadRepeat, vAsphalt));

            m_Colors.Add(asphaltCol32);
            m_Colors.Add(asphaltCol32);

            // Colliders: positioned along the centerline of each curb with edgeRadius = borderWidth * 0.5
            Vector2 leftCol = center - normal * colliderOffset;
            Vector2 rightCol = center + normal * colliderOffset;
            m_LeftColliderPoints.Add(leftCol);
            m_RightColliderPoints.Add(rightCol);
        }

        // Second: 4 vertices per slice for curbs (Left outer/inner, Right inner/outer)
        if (borderWidth > 0.001f)
        {
            for (int i = 0; i < sliceCount; i++)
            {
                Vector2 center = m_SampledCenterline[i];
                float dist = m_SampledDistances[i];
                Vector2 normal = GetSliceNormal(i, sliceCount);

                Vector2 vCurbL_Out = center - normal * halfTotal;
                Vector2 vCurbL_In = center - normal * halfRoad;
                Vector2 vCurbR_In = center + normal * halfRoad;
                Vector2 vCurbR_Out = center + normal * halfTotal;

                m_Vertices.Add(new Vector3(vCurbL_Out.x, vCurbL_Out.y, 0f));
                m_Vertices.Add(new Vector3(vCurbL_In.x, vCurbL_In.y, 0f));
                m_Vertices.Add(new Vector3(vCurbR_In.x, vCurbR_In.y, 0f));
                m_Vertices.Add(new Vector3(vCurbR_Out.x, vCurbR_Out.y, 0f));

                float uCurb = dist / curbTileSize;

                // Left curb: V=1 (outer bevel), V=0 (road edge)
                m_UVs.Add(new Vector2(uCurb, 1.0f));
                m_UVs.Add(new Vector2(uCurb, 0.0f));

                // Right curb: V=0 (road edge), V=1 (outer bevel)
                m_UVs.Add(new Vector2(uCurb, 0.0f));
                m_UVs.Add(new Vector2(uCurb, 1.0f));

                m_Colors.Add(curbCol32);
                m_Colors.Add(curbCol32);
                m_Colors.Add(curbCol32);
                m_Colors.Add(curbCol32);
            }
        }

        // 4. Generate Triangles for Road and Curbs
        int segments = isClosedLoop ? sliceCount : (sliceCount - 1);
        for (int s = 0; s < segments; s++)
        {
            int nextSlice = (s + 1) % sliceCount;

            // Submesh 0: Road Quad (r0, rNext0, r1 / r1, rNext0, rNext1)
            int r0 = s * 2;
            int r1 = r0 + 1;
            int rNext0 = nextSlice * 2;
            int rNext1 = rNext0 + 1;

            m_RoadTriangles.Add(r0);
            m_RoadTriangles.Add(rNext0);
            m_RoadTriangles.Add(r1);

            m_RoadTriangles.Add(r1);
            m_RoadTriangles.Add(rNext0);
            m_RoadTriangles.Add(rNext1);
        }

        if (borderWidth > 0.001f)
        {
            int curbBase = sliceCount * 2;
            for (int s = 0; s < segments; s++)
            {
                int nextSlice = (s + 1) % sliceCount;
                int cA = curbBase + s * 4;
                int cB = curbBase + nextSlice * 4;

                bool isImpassable = highlightImpassableTurns && (IsSliceImpassable(s) || IsSliceImpassable(nextSlice));
                List<int> targetCurbTriangles = isImpassable ? m_OrangeCurbTriangles : m_CurbTriangles;

                // Left Curb Quad (cA0, cB0, cA1 / cA1, cB0, cB1)
                targetCurbTriangles.Add(cA + 0);
                targetCurbTriangles.Add(cB + 0);
                targetCurbTriangles.Add(cA + 1);

                targetCurbTriangles.Add(cA + 1);
                targetCurbTriangles.Add(cB + 0);
                targetCurbTriangles.Add(cB + 1);

                // Right Curb Quad (cA2, cA3, cB2 / cB2, cA3, cB3)
                targetCurbTriangles.Add(cA + 2);
                targetCurbTriangles.Add(cA + 3);
                targetCurbTriangles.Add(cB + 2);

                targetCurbTriangles.Add(cB + 2);
                targetCurbTriangles.Add(cA + 3);
                targetCurbTriangles.Add(cB + 3);
            }
        }

        // 5. Update Mesh
        roadMesh.Clear();
        roadMesh.SetVertices(m_Vertices);
        roadMesh.SetUVs(0, m_UVs);
        roadMesh.SetColors(m_Colors);

        roadMesh.subMeshCount = 3;
        roadMesh.SetTriangles(m_RoadTriangles, 0);
        roadMesh.SetTriangles(m_CurbTriangles, 1);
        roadMesh.SetTriangles(m_OrangeCurbTriangles, 2);

        roadMesh.RecalculateBounds();

        // 6. Update Colliders
        UpdateEdgeColliders();
    }

    /// <summary>
    /// Checks if a sampled slice has a curve radius tighter than minPassableRadius.
    /// </summary>
    public bool IsSliceImpassable(int sliceIdx)
    {
        if (m_SampledCenterline == null || m_SampledCenterline.Count < 3) return false;
        int count = m_SampledCenterline.Count;
        int k = Mathf.Clamp(segmentsPerCurve / 3, 1, 4);

        int prevIdx = isClosedLoop ? (sliceIdx - k + count) % count : Mathf.Max(0, sliceIdx - k);
        int nextIdx = isClosedLoop ? (sliceIdx + k) % count : Mathf.Min(count - 1, sliceIdx + k);

        if (prevIdx == sliceIdx || nextIdx == sliceIdx || prevIdx == nextIdx) return false;

        Vector2 pA = m_SampledCenterline[prevIdx];
        Vector2 pB = m_SampledCenterline[sliceIdx];
        Vector2 pC = m_SampledCenterline[nextIdx];

        Vector2 v1 = pB - pA;
        Vector2 v2 = pC - pB;
        if (v1.sqrMagnitude < 0.001f || v2.sqrMagnitude < 0.001f) return false;

        float angle = Vector2.Angle(v1, v2);
        if (angle < 0.8f) return false;

        float chord = Vector2.Distance(pA, pC);
        float sinHalf = Mathf.Sin(angle * 0.5f * Mathf.Deg2Rad);
        if (sinHalf < 0.001f) return false;

        float r = chord / (2.0f * sinHalf);
        return r < minPassableRadius;
    }

    public struct ImpassableTurnInfo
    {
        public int pointIndex;
        public Vector2 position;
        public float radius;
        public float angle;
    }

    /// <summary>
    /// Analyzes all turns of the road and returns any turn where the curvature radius is below minPassableRadius.
    /// </summary>
    public List<ImpassableTurnInfo> GetImpassableTurns()
    {
        List<ImpassableTurnInfo> result = new List<ImpassableTurnInfo>();
        if (points == null || points.Count < 3) return result;

        int count = points.Count;
        int checkCount = isClosedLoop ? count : (count - 2);

        for (int i = 0; i < checkCount; i++)
        {
            int curr = isClosedLoop ? i : (i + 1);
            int prev = (curr - 1 + count) % count;
            int next = (curr + 1) % count;

            Vector2 pPrev = points[prev];
            Vector2 pCurr = points[curr];
            Vector2 pNext = points[next];

            Vector2 vIn = pCurr - pPrev;
            Vector2 vOut = pNext - pCurr;
            if (vIn.sqrMagnitude < 0.01f || vOut.sqrMagnitude < 0.01f) continue;

            float angle = Vector2.Angle(vIn, vOut);
            if (angle < 2.0f) continue;

            float sinHalf = Mathf.Sin(angle * 0.5f * Mathf.Deg2Rad);
            if (sinHalf < 0.001f) continue;

            float minArm = Mathf.Min(vIn.magnitude, vOut.magnitude);
            float radius = minArm / (2.0f * sinHalf);

            if (radius < minPassableRadius)
            {
                result.Add(new ImpassableTurnInfo
                {
                    pointIndex = curr,
                    position = (Vector2)transform.TransformPoint(pCurr),
                    radius = radius,
                    angle = angle
                });
            }
        }

        return result;
    }

    private void SampleSplineCenterline()
    {
        m_SampledCenterline.Clear();
        m_SampledDistances.Clear();

        int count = points.Count;
        if (count < 2) return;

        float cumulativeDist = 0f;

        if (interpolation == SplineInterpolation.CatmullRom)
        {
            int numCurves = isClosedLoop ? count : (count - 1);

            for (int i = 0; i < numCurves; i++)
            {
                Vector2 p0, p1, p2, p3;

                if (isClosedLoop)
                {
                    p0 = points[(i - 1 + count) % count];
                    p1 = points[i];
                    p2 = points[(i + 1) % count];
                    p3 = points[(i + 2) % count];
                }
                else
                {
                    p0 = (i > 0) ? points[i - 1] : (points[0] - (points[1] - points[0]));
                    p1 = points[i];
                    p2 = points[i + 1];
                    p3 = (i + 2 < count) ? points[i + 2] : (p2 + (p2 - p1));
                }

                int steps = Mathf.Max(2, segmentsPerCurve);
                // When open and at last curve, include t = 1.0f point
                int stepLimit = (isClosedLoop || i < numCurves - 1) ? steps : (steps + 1);

                for (int step = 0; step < stepLimit; step++)
                {
                    float t = step / (float)steps;
                    Vector2 pt = EvaluateCatmullRom(p0, p1, p2, p3, t);

                    if (m_SampledCenterline.Count > 0)
                    {
                        cumulativeDist += Vector2.Distance(m_SampledCenterline[m_SampledCenterline.Count - 1], pt);
                    }

                    m_SampledCenterline.Add(pt);
                    m_SampledDistances.Add(cumulativeDist);
                }
            }
        }
        else // Bezier
        {
            int numCurves = isClosedLoop ? count : (count - 1);

            for (int i = 0; i < numCurves; i++)
            {
                Vector2 p1 = points[i];
                Vector2 p2 = isClosedLoop ? points[(i + 1) % count] : points[i + 1];

                Vector2 prevPt = isClosedLoop ? points[(i - 1 + count) % count] : ((i > 0) ? points[i - 1] : p1 - (p2 - p1));
                Vector2 nextPt = isClosedLoop ? points[(i + 2) % count] : ((i + 2 < count) ? points[i + 2] : p2 + (p2 - p1));

                Vector2 tangent1 = (p2 - prevPt).normalized;
                Vector2 tangent2 = (nextPt - p1).normalized;

                float segLen = Vector2.Distance(p1, p2) * 0.35f;
                Vector2 cp1 = p1 + tangent1 * segLen;
                Vector2 cp2 = p2 - tangent2 * segLen;

                int steps = Mathf.Max(2, segmentsPerCurve);
                int stepLimit = (isClosedLoop || i < numCurves - 1) ? steps : (steps + 1);

                for (int step = 0; step < stepLimit; step++)
                {
                    float t = step / (float)steps;
                    Vector2 pt = EvaluateCubicBezier(p1, cp1, cp2, p2, t);

                    if (m_SampledCenterline.Count > 0)
                    {
                        cumulativeDist += Vector2.Distance(m_SampledCenterline[m_SampledCenterline.Count - 1], pt);
                    }

                    m_SampledCenterline.Add(pt);
                    m_SampledDistances.Add(cumulativeDist);
                }
            }
        }
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

    private Vector2 GetSliceNormal(int i, int sliceCount)
    {
        Vector2 tangent;
        if (isClosedLoop)
        {
            int prev = (i - 1 + sliceCount) % sliceCount;
            int next = (i + 1) % sliceCount;
            tangent = (m_SampledCenterline[next] - m_SampledCenterline[prev]).normalized;
        }
        else
        {
            if (i == 0)
            {
                tangent = (m_SampledCenterline[1] - m_SampledCenterline[0]).normalized;
            }
            else if (i == sliceCount - 1)
            {
                tangent = (m_SampledCenterline[sliceCount - 1] - m_SampledCenterline[sliceCount - 2]).normalized;
            }
            else
            {
                tangent = (m_SampledCenterline[i + 1] - m_SampledCenterline[i - 1]).normalized;
            }
        }

        if (tangent.sqrMagnitude < 0.0001f) tangent = Vector2.up;
        return new Vector2(-tangent.y, tangent.x);
    }

    #endregion

    #region Physics Colliders Management

    private void UpdateEdgeColliders()
    {
        if (!createColliders || m_LeftColliderPoints.Count < 2)
        {
            ClearColliders();
            return;
        }

        // Clean up legacy child names if any exist
        Transform oldLeft = transform.Find("LeftBorder");
        if (oldLeft != null) DestroyImmediate(oldLeft.gameObject);
        Transform oldRight = transform.Find("RightBorder");
        if (oldRight != null) DestroyImmediate(oldRight.gameObject);

        float radius = Mathf.Max(0.18f, borderWidth * 0.5f);

        // Left Curb Collider
        EnsureBorderCollider(ref leftBorderCollider, "LeftCurb");
        if (leftBorderCollider != null)
        {
            leftBorderCollider.enabled = true;
            leftBorderCollider.isTrigger = collidersAreTrigger;
            leftBorderCollider.edgeRadius = radius;

            List<Vector2> leftPts = new List<Vector2>(m_LeftColliderPoints);
            if (isClosedLoop && leftPts.Count > 2)
            {
                leftPts.Add(leftPts[0]); // Close edge collider loop
            }
            leftBorderCollider.SetPoints(leftPts);
        }

        // Right Curb Collider
        EnsureBorderCollider(ref rightBorderCollider, "RightCurb");
        if (rightBorderCollider != null)
        {
            rightBorderCollider.enabled = true;
            rightBorderCollider.isTrigger = collidersAreTrigger;
            rightBorderCollider.edgeRadius = radius;

            List<Vector2> rightPts = new List<Vector2>(m_RightColliderPoints);
            if (isClosedLoop && rightPts.Count > 2)
            {
                rightPts.Add(rightPts[0]); // Close edge collider loop
            }
            rightBorderCollider.SetPoints(rightPts);
        }
    }

    private void EnsureBorderCollider(ref EdgeCollider2D colliderRef, string childName)
    {
        if (colliderRef == null)
        {
            Transform child = transform.Find(childName);
            if (child == null)
            {
                GameObject go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                child = go.transform;
            }
            colliderRef = child.GetComponent<EdgeCollider2D>();
            if (colliderRef == null)
            {
                colliderRef = child.gameObject.AddComponent<EdgeCollider2D>();
            }
        }
    }

    private void ClearColliders()
    {
        if (leftBorderCollider != null) leftBorderCollider.enabled = false;
        if (rightBorderCollider != null) rightBorderCollider.enabled = false;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (points == null || points.Count == 0) return;

        Gizmos.matrix = transform.localToWorldMatrix;

        // Draw spline centerline
        if (m_SampledCenterline != null && m_SampledCenterline.Count > 1)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.75f);
            for (int i = 0; i < m_SampledCenterline.Count - 1; i++)
            {
                Gizmos.DrawLine(m_SampledCenterline[i], m_SampledCenterline[i + 1]);
            }
            if (isClosedLoop)
            {
                Gizmos.DrawLine(m_SampledCenterline[m_SampledCenterline.Count - 1], m_SampledCenterline[0]);
            }
        }

        // Draw control points
        Gizmos.color = new Color(1.0f, 0.85f, 0.1f, 0.9f);
        for (int i = 0; i < points.Count; i++)
        {
            Gizmos.DrawSphere(new Vector3(points[i].x, points[i].y, 0f), 0.35f);
        }
    }

    #endregion
}
