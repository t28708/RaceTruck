using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum RoundaboutArmType
{
    Entry = 0,   // Заезд на кольцо
    Exit = 1,    // Выезд с кольца
    TwoWay = 2   // Двусторонний (заезд + выезд)
}

[System.Serializable]
public class RoundaboutArm
{
    public string name = "Примыкание";
    [Range(0f, 360f)]
    public float angleDeg = 270f;
    public RoundaboutArmType armType = RoundaboutArmType.TwoWay;
    [Range(3.5f, 15.0f)]
    public float width = 6.0f;
    [Range(1.0f, 15.0f)]
    public float extensionLength = 5.0f;
    public SplineRoad2D connectedRoad;

    public RoundaboutArm() {}

    public RoundaboutArm(string name, float angleDeg, RoundaboutArmType armType, float width = 6.0f, float extensionLength = 5.0f)
    {
        this.name = name;
        this.angleDeg = angleDeg;
        this.armType = armType;
        this.width = width;
        this.extensionLength = extensionLength;
    }

    public Vector2 GetSocketLocalPos(float outerRadius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float totalDist = outerRadius + extensionLength;
        return new Vector2(Mathf.Cos(rad) * totalDist, Mathf.Sin(rad) * totalDist);
    }

    public Vector2 GetMouthLocalPos(float outerRadius)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * outerRadius, Mathf.Sin(rad) * outerRadius);
    }

    public Vector2 GetOutwardDirection()
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}

/// <summary>
/// Procedural 2D Roundabout Generator with Swept Path / Off-tracking verification
/// according to AASHTO WB-67 American Class 8 semi-truck engineering standards.
/// Generates concentric multi-submesh rings for asphalt lanes, dashed markings,
/// mountable truck apron, curbs, and central lawn island with EdgeCollider2D borders.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RoundaboutGenerator2D : MonoBehaviour
{
    [Header("Roundabout Geometry")]
    [Tooltip("Total outer radius in meters to the outer curb edge.")]
    [Range(12.0f, 65.0f)]
    public float outerRadius = 25.0f;

    [Tooltip("Number of travel lanes on the roundabout (1 to 3).")]
    [Range(1, 3)]
    public int laneCount = 2;

    [Tooltip("Width of each travel lane in meters (typically 4.5 - 5.5 m).")]
    [Range(3.5f, 7.0f)]
    public float laneWidth = 5.0f;

    [Tooltip("Width of outer and inner curbs in meters.")]
    [Range(0.2f, 0.8f)]
    public float curbWidth = 0.5f;

    [Tooltip("Circle radial segment resolution (higher = smoother curve).")]
    [Range(24, 128)]
    public int segments = 64;

    [Header("Truck Apron (Mountable Inner Ring)")]
    [Tooltip("Mountable paved cobblestone apron for semi-trailer tandem off-tracking.")]
    public bool hasTruckApron = true;

    [Tooltip("Width of the paved truck apron in meters towards the center (typically 1.5 - 25+ m).")]
    [Range(0.5f, 35.0f)]
    public float truckApronWidth = 2.5f;

    [Header("Central Island")]
    [Tooltip("Whether to generate a green central island/lawn inside the inner curb.")]
    public bool hasCenterIsland = true;

    [Header("Connection Arms (Entry / Exit / Two-Way Roads)")]
    [Tooltip("Default width of entry and exit connecting roads in meters.")]
    [Range(4.0f, 16.0f)]
    public float armWidth = 8.5f;

    [Tooltip("List of entry and exit connection arms connecting roads to the roundabout.")]
    public List<RoundaboutArm> arms = new List<RoundaboutArm>();

    /// <summary>
    /// Preset: 1 Entry + 2 Exits (T-junction roundabout: South In, East Out, North Out).
    /// </summary>
    public void SetPreset_1Entry_2Exits(float customWidth = -1f)
    {
        float w = customWidth > 0f ? customWidth : armWidth;
        arms.Clear();
        arms.Add(new RoundaboutArm("Заезд (Юг)", 270f, RoundaboutArmType.Entry, w, 5.0f));
        arms.Add(new RoundaboutArm("Выезд 1 (Восток)", 0f, RoundaboutArmType.Exit, w, 5.0f));
        arms.Add(new RoundaboutArm("Выезд 2 (Север)", 90f, RoundaboutArmType.Exit, w, 5.0f));
        RebuildRoundabout();
    }

    /// <summary>
    /// Preset: 1 Entry + 3 Exits (Crossroad roundabout: South In, East Out, North Out, West Out).
    /// </summary>
    public void SetPreset_1Entry_3Exits(float customWidth = -1f)
    {
        float w = customWidth > 0f ? customWidth : armWidth;
        arms.Clear();
        arms.Add(new RoundaboutArm("Заезд (Юг)", 270f, RoundaboutArmType.Entry, w, 5.0f));
        arms.Add(new RoundaboutArm("Выезд 1 (Восток)", 0f, RoundaboutArmType.Exit, w, 5.0f));
        arms.Add(new RoundaboutArm("Выезд 2 (Север)", 90f, RoundaboutArmType.Exit, w, 5.0f));
        arms.Add(new RoundaboutArm("Выезд 3 (Запад)", 180f, RoundaboutArmType.Exit, w, 5.0f));
        RebuildRoundabout();
    }

    /// <summary>
    /// Preset: 4 Two-Way connection arms (South, East, North, West).
    /// </summary>
    public void SetPreset_4Way(float customWidth = -1f)
    {
        float w = customWidth > 0f ? customWidth : armWidth;
        arms.Clear();
        arms.Add(new RoundaboutArm("Примыкание (Юг)", 270f, RoundaboutArmType.TwoWay, w, 5.0f));
        arms.Add(new RoundaboutArm("Примыкание (Восток)", 0f, RoundaboutArmType.TwoWay, w, 5.0f));
        arms.Add(new RoundaboutArm("Примыкание (Север)", 90f, RoundaboutArmType.TwoWay, w, 5.0f));
        arms.Add(new RoundaboutArm("Примыкание (Запад)", 180f, RoundaboutArmType.TwoWay, w, 5.0f));
        RebuildRoundabout();
    }

    public void SetAllArmsWidth(float newWidth)
    {
        armWidth = newWidth;
        if (arms != null)
        {
            for (int i = 0; i < arms.Count; i++)
            {
                arms[i].width = newWidth;
            }
        }
        RebuildRoundabout();
    }

    public Vector2 GetArmSocketWorldPos(int armIndex)
    {
        if (arms == null || armIndex < 0 || armIndex >= arms.Count) return transform.position;
        Vector2 loc = arms[armIndex].GetSocketLocalPos(outerRadius);
        return transform.TransformPoint(loc);
    }

    public Vector2 GetArmSocketDirection(int armIndex)
    {
        if (arms == null || armIndex < 0 || armIndex >= arms.Count) return Vector2.up;
        Vector2 dir = arms[armIndex].GetOutwardDirection();
        return transform.TransformDirection(dir);
    }

    public int FindClosestArmSocket(Vector2 worldPos, float maxDist, out Vector2 socketWorldPos)
    {
        socketWorldPos = Vector2.zero;
        if (arms == null || arms.Count == 0) return -1;

        int bestIndex = -1;
        float bestDist = maxDist;

        for (int i = 0; i < arms.Count; i++)
        {
            Vector2 sPos = GetArmSocketWorldPos(i);
            float d = Vector2.Distance(worldPos, sPos);
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
                socketWorldPos = sPos;
            }
        }

        return bestIndex;
    }

    [Header("Visual Styling & Sorting")]
    public int sortingOrder = -8;
    public Color asphaltColor = Color.white;
    public Color curbColor = Color.white;
    public Color apronColor = Color.white;
    public Color grassColor = Color.white;
    public Color markingColor = new Color(1f, 1f, 1f, 0.95f);

    [Header("Truck Specifications for Swept Path Verification (WB-67)")]
    [Tooltip("Wheelbase of tractor: steer axle to rear tandem center (m).")]
    public float tractorWheelbase = 5.8f;

    [Tooltip("Distance from 5th wheel kingpin to trailer rear tandem center (m) for 53' trailer.")]
    public float kingpinToRearAxle = 12.5f;

    [Tooltip("Overall body width of tractor and trailer (m).")]
    public float vehicleWidth = 2.6f;

    [Tooltip("Maximum steering angle of front wheels in degrees.")]
    public float maxSteerAngle = 38.0f;

    [Tooltip("Lateral safety margin clearance between tires and curb (m).")]
    public float safetyMargin = 0.3f;

    [Tooltip("Colorize inner curb orange when roundabout is impassable.")]
    public bool highlightImpassable = true;

    [Header("Physics Colliders")]
    public bool createColliders = true;
    public bool collidersAreTrigger = true;

    [System.Serializable]
    public struct PassabilityResult
    {
        public bool isPassable;
        public bool isPhysicallyImpossible;
        public string statusMessage;
        public float rFrontOuter;
        public float rSteer;
        public float rKingpin;
        public float rTrailerAxle;
        public float rInnerWheel;
        public float sweptPathWidth;
        public float availableLaneWidth;
        public float margin;
    }

    [SerializeField, HideInInspector]
    private PassabilityResult lastVerification;

    public PassabilityResult LastVerification => lastVerification;

    // Component references
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh roundaboutMesh;

    // Reusable lists for zero-allocation mesh rebuild
    private readonly List<Vector3> m_Vertices = new List<Vector3>(512);
    private readonly List<Vector2> m_UVs = new List<Vector2>(512);
    private readonly List<Color32> m_Colors = new List<Color32>(512);

    private readonly List<int> m_TriAsphalt = new List<int>(512);
    private readonly List<int> m_TriCurbs = new List<int>(512);
    private readonly List<int> m_TriMarkings = new List<int>(256);
    private readonly List<int> m_TriApron = new List<int>(256);
    private readonly List<int> m_TriIsland = new List<int>(256);

    // Default shared resources
    private static Texture2D s_AsphaltTex;
    private static Texture2D s_CurbTex;
    private static Texture2D s_OrangeCurbTex;
    private static Texture2D s_ApronTex;
    private static Texture2D s_GrassTex;
    private static Texture2D s_DashesTex;

    private static Material s_AsphaltMat;
    private static Material s_CurbMat;
    private static Material s_OrangeCurbMat;
    private static Material s_MarkingMat;
    private static Material s_ApronMat;
    private static Material s_GrassMat;

    private const string OuterColliderName = "OuterCurbCollider";
    private const string InnerColliderName = "InnerCurbCollider";

    private void Awake()
    {
        // Auto-cleanup: If another RoundaboutGenerator2D exists at the exact same location, keep only the larger/active one!
        if (transform.parent != null)
        {
            var siblings = transform.parent.GetComponentsInChildren<RoundaboutGenerator2D>(true);
            for (int i = 0; i < siblings.Length; i++)
            {
                var other = siblings[i];
                if (other != null && other != this && Vector2.Distance(other.transform.position, transform.position) < 5.0f)
                {
                    if (other.outerRadius > this.outerRadius)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    else if (other.outerRadius < this.outerRadius)
                    {
                        Destroy(other.gameObject);
                    }
                }
            }
        }

        EnsureComponents();
        RebuildRoundabout();
    }

    private void Start()
    {
        // Auto-cleanup duplicate roundabouts at runtime
        if (transform.parent != null)
        {
            var siblings = transform.parent.GetComponentsInChildren<RoundaboutGenerator2D>(true);
            for (int i = 0; i < siblings.Length; i++)
            {
                var other = siblings[i];
                if (other != null && other != this && Vector2.Distance(other.transform.position, transform.position) < 5.0f)
                {
                    if (other.outerRadius > this.outerRadius)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    else if (other.outerRadius < this.outerRadius)
                    {
                        Destroy(other.gameObject);
                    }
                }
            }
        }

        if (roundaboutMesh == null || roundaboutMesh.vertexCount == 0)
        {
            RebuildRoundabout();
        }
    }

    private void OnValidate()
    {
        outerRadius = Mathf.Max(12.0f, outerRadius);
        laneCount = Mathf.Clamp(laneCount, 1, 3);
        laneWidth = Mathf.Clamp(laneWidth, 3.0f, 8.0f);
        curbWidth = Mathf.Clamp(curbWidth, 0.2f, 1.0f);
        segments = Mathf.Clamp(segments, 24, 128);

        // Ensure minimum outerRadius fits the asphalt travel lanes and outer curb
        float minRoad = (laneCount * laneWidth) + (curbWidth * 2f) + 1.0f;
        if (outerRadius < minRoad)
        {
            outerRadius = minRoad;
        }

        // Clamp apron width so it expands cleanly towards center without crossing zero
        float rRoadOuter = outerRadius - curbWidth;
        float rRoadInner = rRoadOuter - (laneCount * laneWidth);
        float maxApron = Mathf.Max(0.5f, rRoadInner - 0.2f);
        truckApronWidth = Mathf.Clamp(truckApronWidth, 0.0f, maxApron);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    RebuildRoundabout();
                }
            };
            return;
        }
#endif
        RebuildRoundabout();
    }

    private void EnsureComponents()
    {
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();

        if (roundaboutMesh == null)
        {
            roundaboutMesh = new Mesh
            {
                name = "Procedural_Roundabout2D_Mesh"
            };
            roundaboutMesh.MarkDynamic();
            meshFilter.sharedMesh = roundaboutMesh;
        }

        UpdateMaterials();
    }

    /// <summary>
    /// Performs AASHTO/WB-67 Swept Path & Off-tracking calculation for American Class 8 semi-truck.
    /// </summary>
    public PassabilityResult CheckPassability()
    {
        return CalculatePassability(
            outerRadius,
            laneCount,
            laneWidth,
            curbWidth,
            hasTruckApron,
            truckApronWidth,
            tractorWheelbase,
            kingpinToRearAxle,
            vehicleWidth,
            maxSteerAngle,
            safetyMargin
        );
    }

    /// <summary>
    /// Evaluates passability with arbitrary parameters without requiring an active instance.
    /// </summary>
    public static PassabilityResult CalculatePassability(
        float outerRadius,
        int laneCount,
        float laneWidth,
        float curbWidth,
        bool hasTruckApron,
        float truckApronWidth,
        float tractorWheelbase = 5.8f,
        float kingpinToRearAxle = 12.5f,
        float vehicleWidth = 2.6f,
        float maxSteerAngle = 38.0f,
        float safetyMargin = 0.3f)
    {
        PassabilityResult res = new PassabilityResult();

        float rFrontOuter = outerRadius - curbWidth;
        res.rFrontOuter = rFrontOuter;

        float halfW = vehicleWidth * 0.5f;
        float rSteer = rFrontOuter - halfW;
        res.rSteer = rSteer;

        // 1. Physical steering limit of tractor front wheels
        float sinSteer = Mathf.Sin(maxSteerAngle * Mathf.Deg2Rad);
        float minSteerRadius = tractorWheelbase / (sinSteer > 0.001f ? sinSteer : 0.001f);
        if (rSteer < minSteerRadius)
        {
            res.isPassable = false;
            res.isPhysicallyImpossible = true;
            res.statusMessage = $"✗ КРИТИЧЕСКАЯ ОШИБКА: Радиус кольца ({rSteer:F1} м) меньше предела поворота тягача (мин. {minSteerRadius:F1} м при угле {maxSteerAngle}°)!";
            return res;
        }

        // 2. Tractor drive axle (kingpin location) radius via circular motion:
        // R_drive^2 + Wheelbase_tractor^2 = R_steer^2
        float driveSq = (rSteer * rSteer) - (tractorWheelbase * tractorWheelbase);
        if (driveSq <= 0.01f)
        {
            res.isPassable = false;
            res.isPhysicallyImpossible = true;
            res.statusMessage = "✗ КРИТИЧЕСКАЯ ОШИБКА: База тягача превышает радиус поворота!";
            return res;
        }
        float rKingpin = Mathf.Sqrt(driveSq);
        res.rKingpin = rKingpin;

        // 3. Trailer rear tandem axle radius:
        // R_trailer^2 + L2^2 = R_kingpin^2
        float trailerSq = (rKingpin * rKingpin) - (kingpinToRearAxle * kingpinToRearAxle);
        if (trailerSq <= 0.01f)
        {
            res.isPassable = false;
            res.isPhysicallyImpossible = true;
            res.statusMessage = $"✗ КРИТИЧЕСКАЯ ОШИБКА: Радиус седла ({rKingpin:F1} м) меньше базы полуприцепа (L2={kingpinToRearAxle:F1} м)! Сцепка сложится в «ножницы».";
            return res;
        }
        float rTrailerAxle = Mathf.Sqrt(trailerSq);
        res.rTrailerAxle = rTrailerAxle;

        // 4. Inner wheel radius of the trailer tandem with margin
        float rInnerWheel = rTrailerAxle - halfW - safetyMargin;
        res.rInnerWheel = rInnerWheel;

        // 5. Total swept path width required by truck
        float sweptWidth = rFrontOuter - rInnerWheel;
        res.sweptPathWidth = sweptWidth;

        // 6. Available road width (lanes + mountable truck apron if present)
        float availableWidth = (laneCount * laneWidth) + (hasTruckApron ? truckApronWidth : 0f);
        res.availableLaneWidth = availableWidth;

        float margin = availableWidth - sweptWidth;
        res.margin = margin;

        if (margin >= 0f)
        {
            res.isPassable = true;
            string apronNote = hasTruckApron ? $" (с учётом Apron +{truckApronWidth:F1}м)" : "";
            res.statusMessage = $"✓ ПРОХОДИМО: Трак 53' свободно проходит кольцо. Запас: +{margin:F1} м (Требуется: {sweptWidth:F1} м, Доступно: {availableWidth:F1} м{apronNote}).";
        }
        else
        {
            res.isPassable = false;
            float deficit = Mathf.Abs(margin);
            res.statusMessage = $"✗ НЕПРОХОДИМО: ТЕЛЕЖКА СРЕЖЕТ ВНУТРЕННИЙ БОРДЮР на {deficit:F1} м! (Требуется: {sweptWidth:F1} м, Доступно: {availableWidth:F1} м). Увеличьте радиус или добавьте полосу / Truck Apron.";
        }

        return res;
    }

    /// <summary>
    /// Fully rebuilds the procedural mesh, materials and colliders.
    /// </summary>
    public void RebuildRoundabout()
    {
        EnsureComponents();
        lastVerification = CheckPassability();

        m_Vertices.Clear();
        m_UVs.Clear();
        m_Colors.Clear();
        m_TriAsphalt.Clear();
        m_TriCurbs.Clear();
        m_TriMarkings.Clear();
        m_TriApron.Clear();
        m_TriIsland.Clear();

        int seg = segments;

        // Calculate radial bands
        float rOuterCurbEdge = outerRadius;
        float rRoadOuter = outerRadius - curbWidth;
        float rRoadInner = rRoadOuter - (laneCount * laneWidth);
        float maxApron = Mathf.Max(0f, rRoadInner - 0.1f);
        float actualApronWidth = hasTruckApron ? Mathf.Clamp(truckApronWidth, 0f, maxApron) : 0f;
        float rApronInner = rRoadInner - actualApronWidth;
        float rInnerCurbOut = rApronInner;
        float rInnerCurbIn = Mathf.Max(0f, rInnerCurbOut - curbWidth);
        float rIsland = Mathf.Max(0f, rInnerCurbIn);

        // Precompute cos and sin table for the circle
        float[] cosTable = new float[seg + 1];
        float[] sinTable = new float[seg + 1];
        for (int i = 0; i <= seg; i++)
        {
            float angle = (i % seg) * (2f * Mathf.PI / seg);
            cosTable[i] = Mathf.Cos(angle);
            sinTable[i] = Mathf.Sin(angle);
        }

        // 1. Build Submesh 0: Asphalt Travel Lanes
        BuildRingMesh(rRoadInner, rRoadOuter, seg, cosTable, sinTable, m_TriAsphalt, asphaltColor, 4.0f, false);

        // 2. Build Submesh 1: Outer and Inner Curbs
        if (arms == null || arms.Count == 0)
        {
            // Full unbroken Outer Curb Ring
            BuildRingMesh(rRoadOuter, rOuterCurbEdge, seg, cosTable, sinTable, m_TriCurbs, curbColor, 2.0f, false);
        }
        else
        {
            // Sector circular curbs between arms and arm asphalt/curbs
            BuildSectorCurbsAndArms(rRoadOuter, rOuterCurbEdge);
        }

        // Inner Curb Ring: (rInnerCurbIn to rInnerCurbOut)
        if (rInnerCurbIn > 0.05f && rInnerCurbOut > curbWidth + 0.1f)
        {
            Color cCol = (highlightImpassable && !lastVerification.isPassable) ? new Color(1.0f, 0.55f, 0.0f, 1f) : curbColor;
            BuildRingMesh(rInnerCurbIn, rInnerCurbOut, seg, cosTable, sinTable, m_TriCurbs, cCol, 2.0f, true);
        }

        // 3. Build Submesh 2: Concentric Lane Divider Markings (Dashed white lines)
        if (laneCount > 1)
        {
            for (int k = 1; k < laneCount; k++)
            {
                float rDiv = rRoadOuter - (k * laneWidth);
                float rDivIn = rDiv - 0.10f;
                float rDivOut = rDiv + 0.10f;
                BuildRingMesh(rDivIn, rDivOut, seg, cosTable, sinTable, m_TriMarkings, markingColor, 3.0f, false);
            }
        }

        // 4. Build Submesh 3: Mountable Truck Apron (Paved Cobblestone Ring or Solid Disk)
        if (hasTruckApron && actualApronWidth > 0.05f)
        {
            if (rApronInner > 0.2f)
            {
                BuildRingMesh(rApronInner, rRoadInner, seg, cosTable, sinTable, m_TriApron, apronColor, 3.0f, false);
            }
            else
            {
                // Apron extends fully to the center: solid paved disk
                BuildCenterDiskMesh(rRoadInner, seg, cosTable, sinTable, m_TriApron, apronColor, 3.0f);
            }
        }

        // 5. Build Submesh 4: Central Island / Lawn (Green Disk)
        if (hasCenterIsland && rIsland > 0.1f && (!hasTruckApron || rApronInner > 0.2f))
        {
            BuildCenterDiskMesh(rIsland, seg, cosTable, sinTable, m_TriIsland, grassColor, 4.0f);
        }

        // Apply to Mesh
        roundaboutMesh.Clear();
        roundaboutMesh.SetVertices(m_Vertices);
        roundaboutMesh.SetUVs(0, m_UVs);
        roundaboutMesh.SetColors(m_Colors);

        roundaboutMesh.subMeshCount = 5;
        roundaboutMesh.SetTriangles(m_TriAsphalt, 0);
        roundaboutMesh.SetTriangles(m_TriCurbs, 1);
        roundaboutMesh.SetTriangles(m_TriMarkings, 2);
        roundaboutMesh.SetTriangles(m_TriApron, 3);
        roundaboutMesh.SetTriangles(m_TriIsland, 4);

        roundaboutMesh.RecalculateBounds();

        // Update EdgeCollider2D
        if (createColliders)
        {
            UpdateColliders(rOuterCurbEdge - (curbWidth * 0.5f), rInnerCurbOut - (curbWidth * 0.5f), seg, cosTable, sinTable);
        }
    }

    /// <summary>
    /// Builds sector curbs along the outer circle between arms, and generates the asphalt and curbs for each arm.
    /// </summary>
    private void BuildSectorCurbsAndArms(float rRoadOuter, float rOuterCurbEdge)
    {
        // 1. Build each arm's asphalt and side curbs
        for (int a = 0; a < arms.Count; a++)
        {
            RoundaboutArm arm = arms[a];
            float rad = Mathf.Repeat(arm.angleDeg, 360f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            Vector3 right = new Vector3(Mathf.Sin(rad), -Mathf.Cos(rad), 0f);

            float hRoad = Mathf.Max(1.5f, arm.width * 0.5f);
            float deltaAngle = Mathf.Asin(Mathf.Clamp01(hRoad / outerRadius));

            // Mouth points at circle boundary
            Vector3 mouthLeftCurb = new Vector3(Mathf.Cos(rad + deltaAngle) * outerRadius, Mathf.Sin(rad + deltaAngle) * outerRadius, 0f);
            Vector3 mouthRightCurb = new Vector3(Mathf.Cos(rad - deltaAngle) * outerRadius, Mathf.Sin(rad - deltaAngle) * outerRadius, 0f);
            Vector3 mouthLeftRoad = new Vector3(Mathf.Cos(rad + deltaAngle) * rRoadOuter, Mathf.Sin(rad + deltaAngle) * rRoadOuter, 0f);
            Vector3 mouthRightRoad = new Vector3(Mathf.Cos(rad - deltaAngle) * rRoadOuter, Mathf.Sin(rad - deltaAngle) * rRoadOuter, 0f);

            // Socket points at distance (outerRadius + extensionLength)
            float extDist = outerRadius + arm.extensionLength;
            Vector3 socketCenter = dir * extDist;
            Vector3 socketLeftRoad = socketCenter - (right * hRoad);
            Vector3 socketRightRoad = socketCenter + (right * hRoad);
            Vector3 socketLeftCurb = socketLeftRoad - (right * curbWidth);
            Vector3 socketRightCurb = socketRightRoad + (right * curbWidth);

            // Arm Asphalt (Submesh 0)
            BuildTexturedQuad(mouthLeftRoad, mouthRightRoad, socketLeftRoad, socketRightRoad, m_TriAsphalt, asphaltColor, arm.width / 4.0f, arm.extensionLength / 4.0f);

            // Mouth transition filler
            int mMid = m_Vertices.Count;
            m_Vertices.Add(dir * rRoadOuter);
            m_UVs.Add(new Vector2(0.5f, 0.5f));
            m_Colors.Add(asphaltColor);

            m_Vertices.Add(mouthLeftRoad);
            m_UVs.Add(new Vector2(0f, 0f));
            m_Colors.Add(asphaltColor);

            m_Vertices.Add(mouthRightRoad);
            m_UVs.Add(new Vector2(1f, 0f));
            m_Colors.Add(asphaltColor);

            m_TriAsphalt.Add(mMid);
            m_TriAsphalt.Add(mMid + 1);
            m_TriAsphalt.Add(mMid + 2);

            // Arm Curbs (Submesh 1)
            BuildTexturedQuad(mouthLeftRoad, mouthLeftCurb, socketLeftRoad, socketLeftCurb, m_TriCurbs, curbColor, 1.0f, arm.extensionLength / 2.0f);
            BuildTexturedQuad(mouthRightCurb, mouthRightRoad, socketRightCurb, socketRightRoad, m_TriCurbs, curbColor, 1.0f, arm.extensionLength / 2.0f);
        }

        // 2. Build circular curbs along the outer edge between arms
        var sortedArms = new List<RoundaboutArm>(arms);
        sortedArms.Sort((a, b) => Mathf.Repeat(a.angleDeg, 360f).CompareTo(Mathf.Repeat(b.angleDeg, 360f)));

        for (int i = 0; i < sortedArms.Count; i++)
        {
            RoundaboutArm cur = sortedArms[i];
            RoundaboutArm nxt = sortedArms[(i + 1) % sortedArms.Count];

            float aCurRad = Mathf.Repeat(cur.angleDeg, 360f) * Mathf.Deg2Rad;
            float aNxtRad = Mathf.Repeat(nxt.angleDeg, 360f) * Mathf.Deg2Rad;

            float hCur = Mathf.Max(1.5f, cur.width * 0.5f);
            float hNxt = Mathf.Max(1.5f, nxt.width * 0.5f);

            float dCur = Mathf.Asin(Mathf.Clamp01(hCur / outerRadius));
            float dNxt = Mathf.Asin(Mathf.Clamp01(hNxt / outerRadius));

            float startAngle = aCurRad + dCur;
            float endAngle = aNxtRad - dNxt;

            if (endAngle <= startAngle)
            {
                endAngle += 2f * Mathf.PI;
            }

            float arcSpan = endAngle - startAngle;
            if (arcSpan > 0.02f)
            {
                int steps = Mathf.Max(2, Mathf.RoundToInt(segments * (arcSpan / (2f * Mathf.PI))));
                BuildArcQuadStrip(rRoadOuter, rOuterCurbEdge, startAngle, endAngle, steps, m_TriCurbs, curbColor, 2.0f, false);
            }
        }
    }

    /// <summary>
    /// Builds a curved quad strip along an angular arc [startAngleRad, endAngleRad] between rIn and rOut.
    /// </summary>
    private void BuildArcQuadStrip(float rIn, float rOut, float startAngleRad, float endAngleRad, int steps,
                                   List<int> targetTriangles, Color32 col, float uTileRepeat, bool invertV)
    {
        if (steps < 1) steps = 1;
        int baseIndex = m_Vertices.Count;

        float rMid = (rIn + rOut) * 0.5f;
        float totalArcLength = Mathf.Abs(endAngleRad - startAngleRad) * rMid;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float angle = Mathf.Lerp(startAngleRad, endAngleRad, t);
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);

            Vector3 vIn = new Vector3(c * rIn, s * rIn, 0f);
            Vector3 vOut = new Vector3(c * rOut, s * rOut, 0f);

            m_Vertices.Add(vIn);
            m_Vertices.Add(vOut);

            float u = (t * totalArcLength) / uTileRepeat;
            float v0 = invertV ? 1.0f : 0.0f;
            float v1 = invertV ? 0.0f : 1.0f;

            m_UVs.Add(new Vector2(u, v0));
            m_UVs.Add(new Vector2(u, v1));

            m_Colors.Add(col);
            m_Colors.Add(col);
        }

        for (int i = 0; i < steps; i++)
        {
            int i0 = baseIndex + (i * 2);
            int i1 = i0 + 1;
            int i2 = baseIndex + ((i + 1) * 2);
            int i3 = i2 + 1;

            targetTriangles.Add(i0);
            targetTriangles.Add(i1);
            targetTriangles.Add(i2);

            targetTriangles.Add(i1);
            targetTriangles.Add(i3);
            targetTriangles.Add(i2);
        }
    }

    /// <summary>
    /// Builds a simple textured quad between 4 corner vertices.
    /// </summary>
    private void BuildTexturedQuad(Vector3 p0In, Vector3 p0Out, Vector3 p1In, Vector3 p1Out,
                                   List<int> targetTriangles, Color32 col, float uScale, float vScale)
    {
        int baseIndex = m_Vertices.Count;

        m_Vertices.Add(p0In);
        m_Vertices.Add(p0Out);
        m_Vertices.Add(p1In);
        m_Vertices.Add(p1Out);

        m_UVs.Add(new Vector2(0f, 0f));
        m_UVs.Add(new Vector2(uScale, 0f));
        m_UVs.Add(new Vector2(0f, vScale));
        m_UVs.Add(new Vector2(uScale, vScale));

        m_Colors.Add(col);
        m_Colors.Add(col);
        m_Colors.Add(col);
        m_Colors.Add(col);

        targetTriangles.Add(baseIndex);
        targetTriangles.Add(baseIndex + 1);
        targetTriangles.Add(baseIndex + 2);

        targetTriangles.Add(baseIndex + 1);
        targetTriangles.Add(baseIndex + 3);
        targetTriangles.Add(baseIndex + 2);
    }

    /// <summary>
    /// Helper to generate a ring quad strip between inner and outer radius.
    /// </summary>
    private void BuildRingMesh(float rIn, float rOut, int seg, float[] cosTable, float[] sinTable,
                               List<int> targetTriangles, Color32 col, float uTileRepeat, bool invertV)
    {
        int baseIndex = m_Vertices.Count;

        for (int i = 0; i <= seg; i++)
        {
            float c = cosTable[i];
            float s = sinTable[i];

            Vector3 vIn = new Vector3(c * rIn, s * rIn, 0f);
            Vector3 vOut = new Vector3(c * rOut, s * rOut, 0f);

            m_Vertices.Add(vIn);
            m_Vertices.Add(vOut);

            // Circumference distance for seamless texture tiling
            float u = (float)i / seg * (2f * Mathf.PI * ((rIn + rOut) * 0.5f)) / uTileRepeat;
            float v0 = invertV ? 1.0f : 0.0f;
            float v1 = invertV ? 0.0f : 1.0f;

            m_UVs.Add(new Vector2(u, v0));
            m_UVs.Add(new Vector2(u, v1));

            m_Colors.Add(col);
            m_Colors.Add(col);
        }

        for (int i = 0; i < seg; i++)
        {
            int i0 = baseIndex + (i * 2);
            int i1 = i0 + 1;
            int i2 = baseIndex + ((i + 1) * 2);
            int i3 = i2 + 1;

            // Two triangles for the quad
            targetTriangles.Add(i0);
            targetTriangles.Add(i1);
            targetTriangles.Add(i2);

            targetTriangles.Add(i1);
            targetTriangles.Add(i3);
            targetTriangles.Add(i2);
        }
    }

    /// <summary>
    /// Helper to generate a center circular disk.
    /// </summary>
    private void BuildCenterDiskMesh(float radius, int seg, float[] cosTable, float[] sinTable,
                                     List<int> targetTriangles, Color32 col, float tileSize)
    {
        int centerIndex = m_Vertices.Count;
        m_Vertices.Add(Vector3.zero);
        m_UVs.Add(new Vector2(0.5f, 0.5f));
        m_Colors.Add(col);

        int rimBase = m_Vertices.Count;
        for (int i = 0; i <= seg; i++)
        {
            float c = cosTable[i];
            float s = sinTable[i];
            Vector3 pt = new Vector3(c * radius, s * radius, 0f);

            m_Vertices.Add(pt);
            m_UVs.Add(new Vector2((pt.x / tileSize) + 0.5f, (pt.y / tileSize) + 0.5f));
            m_Colors.Add(col);
        }

        for (int i = 0; i < seg; i++)
        {
            int r0 = rimBase + i;
            int r1 = rimBase + i + 1;

            targetTriangles.Add(centerIndex);
            targetTriangles.Add(r0);
            targetTriangles.Add(r1);
        }
    }

    private void UpdateColliders(float rOuter, float rInner, int seg, float[] cosTable, float[] sinTable)
    {
        if (arms == null || arms.Count == 0)
        {
            // Destroy any leftover sector colliders
            for (int s = transform.childCount - 1; s >= 0; s--)
            {
                Transform ch = transform.GetChild(s);
                if (ch.name.StartsWith("SectorCurbCollider_"))
                {
                    if (Application.isPlaying) Destroy(ch.gameObject);
                    else DestroyImmediate(ch.gameObject);
                }
            }

            // 1. Single continuous Outer Curb Collider
            Transform outerTr = transform.Find(OuterColliderName);
            if (outerTr == null)
            {
                GameObject go = new GameObject(OuterColliderName);
                go.transform.SetParent(transform, false);
                outerTr = go.transform;
            }
            EdgeCollider2D outerCol = outerTr.GetComponent<EdgeCollider2D>();
            if (outerCol == null) outerCol = outerTr.gameObject.AddComponent<EdgeCollider2D>();
            outerCol.isTrigger = collidersAreTrigger;

            Vector2[] outerPoints = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                outerPoints[i] = new Vector2(cosTable[i] * rOuter, sinTable[i] * rOuter);
            }
            outerCol.points = outerPoints;
        }
        else
        {
            // Destroy continuous Outer Curb Collider if it exists
            Transform outerTr = transform.Find(OuterColliderName);
            if (outerTr != null)
            {
                if (Application.isPlaying) Destroy(outerTr.gameObject);
                else DestroyImmediate(outerTr.gameObject);
            }

            var sortedArms = new List<RoundaboutArm>(arms);
            sortedArms.Sort((a, b) => Mathf.Repeat(a.angleDeg, 360f).CompareTo(Mathf.Repeat(b.angleDeg, 360f)));

            float rColMid = outerRadius - (curbWidth * 0.5f);

            for (int i = 0; i < sortedArms.Count; i++)
            {
                RoundaboutArm cur = sortedArms[i];
                RoundaboutArm nxt = sortedArms[(i + 1) % sortedArms.Count];

                float aCurRad = Mathf.Repeat(cur.angleDeg, 360f) * Mathf.Deg2Rad;
                float aNxtRad = Mathf.Repeat(nxt.angleDeg, 360f) * Mathf.Deg2Rad;

                Vector3 dirCur = new Vector3(Mathf.Cos(aCurRad), Mathf.Sin(aCurRad), 0f);
                Vector3 rightCur = new Vector3(Mathf.Sin(aCurRad), -Mathf.Cos(aCurRad), 0f);

                Vector3 dirNxt = new Vector3(Mathf.Cos(aNxtRad), Mathf.Sin(aNxtRad), 0f);
                Vector3 rightNxt = new Vector3(Mathf.Sin(aNxtRad), -Mathf.Cos(aNxtRad), 0f);

                float hCur = Mathf.Max(1.5f, cur.width * 0.5f);
                float hNxt = Mathf.Max(1.5f, nxt.width * 0.5f);

                float dCur = Mathf.Asin(Mathf.Clamp01(hCur / outerRadius));
                float dNxt = Mathf.Asin(Mathf.Clamp01(hNxt / outerRadius));

                float startAngle = aCurRad + dCur;
                float endAngle = aNxtRad - dNxt;
                if (endAngle <= startAngle) endAngle += 2f * Mathf.PI;

                List<Vector2> colPoints = new List<Vector2>();

                // 1. Arm cur left socket curb
                Vector3 curSocketCenter = dirCur * (outerRadius + cur.extensionLength);
                Vector3 curSocketLeftCurb = curSocketCenter - (rightCur * (hCur + (curbWidth * 0.5f)));
                colPoints.Add(curSocketLeftCurb);

                // 2. Arm cur left mouth curb
                Vector3 curMouthLeftCurb = new Vector3(Mathf.Cos(startAngle) * rColMid, Mathf.Sin(startAngle) * rColMid, 0f);
                colPoints.Add(curMouthLeftCurb);

                // 3. Circular arc points between the arms
                float arcSpan = endAngle - startAngle;
                int arcSteps = Mathf.Max(2, Mathf.RoundToInt(segments * (arcSpan / (2f * Mathf.PI))));
                for (int s = 1; s < arcSteps; s++)
                {
                    float t = (float)s / arcSteps;
                    float a = Mathf.Lerp(startAngle, endAngle, t);
                    colPoints.Add(new Vector2(Mathf.Cos(a) * rColMid, Mathf.Sin(a) * rColMid));
                }

                // 4. Arm nxt right mouth curb
                Vector3 nxtMouthRightCurb = new Vector3(Mathf.Cos(endAngle) * rColMid, Mathf.Sin(endAngle) * rColMid, 0f);
                colPoints.Add(nxtMouthRightCurb);

                // 5. Arm nxt right socket curb
                Vector3 nxtSocketCenter = dirNxt * (outerRadius + nxt.extensionLength);
                Vector3 nxtSocketRightCurb = nxtSocketCenter + (rightNxt * (hNxt + (curbWidth * 0.5f)));
                colPoints.Add(nxtSocketRightCurb);

                string colName = $"SectorCurbCollider_{i}";
                Transform secTr = transform.Find(colName);
                if (secTr == null)
                {
                    GameObject go = new GameObject(colName);
                    go.transform.SetParent(transform, false);
                    secTr = go.transform;
                }
                EdgeCollider2D secCol = secTr.GetComponent<EdgeCollider2D>();
                if (secCol == null) secCol = secTr.gameObject.AddComponent<EdgeCollider2D>();
                secCol.isTrigger = collidersAreTrigger;
                secCol.points = colPoints.ToArray();
            }

            // Remove extra sector colliders if count decreased
            for (int s = transform.childCount - 1; s >= 0; s--)
            {
                Transform ch = transform.GetChild(s);
                if (ch.name.StartsWith("SectorCurbCollider_"))
                {
                    int idx;
                    if (int.TryParse(ch.name.Substring("SectorCurbCollider_".Length), out idx))
                    {
                        if (idx >= sortedArms.Count)
                        {
                            if (Application.isPlaying) Destroy(ch.gameObject);
                            else DestroyImmediate(ch.gameObject);
                        }
                    }
                }
            }
        }

        // 2. Inner Curb / Island Collider
        if (rInner > 0.5f)
        {
            Transform innerTr = transform.Find(InnerColliderName);
            if (innerTr == null)
            {
                GameObject go = new GameObject(InnerColliderName);
                go.transform.SetParent(transform, false);
                innerTr = go.transform;
            }
            EdgeCollider2D innerCol = innerTr.GetComponent<EdgeCollider2D>();
            if (innerCol == null) innerCol = innerTr.gameObject.AddComponent<EdgeCollider2D>();
            innerCol.isTrigger = collidersAreTrigger;

            Vector2[] innerPoints = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                innerPoints[i] = new Vector2(cosTable[i] * rInner, sinTable[i] * rInner);
            }
            innerCol.points = innerPoints;
        }
        else
        {
            Transform innerTr = transform.Find(InnerColliderName);
            if (innerTr != null)
            {
                if (Application.isPlaying) Destroy(innerTr.gameObject);
                else DestroyImmediate(innerTr.gameObject);
            }
        }
    }

    private void UpdateMaterials()
    {
        EnsureDefaultResources();
        if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;

        meshRenderer.sortingOrder = sortingOrder;

        Material asphaltMat = s_AsphaltMat;
        Material curbMat = (highlightImpassable && !lastVerification.isPassable && s_OrangeCurbMat != null) ? s_OrangeCurbMat : s_CurbMat;
        Material markingMat = s_MarkingMat;
        Material apronMat = s_ApronMat;
        Material grassMat = s_GrassMat;

        Material[] currentMats = meshRenderer.sharedMaterials;
        if (currentMats == null || currentMats.Length != 5 ||
            currentMats[0] != asphaltMat || currentMats[1] != curbMat || currentMats[2] != markingMat ||
            currentMats[3] != apronMat || currentMats[4] != grassMat)
        {
            meshRenderer.sharedMaterials = new Material[]
            {
                asphaltMat,
                curbMat,
                markingMat,
                apronMat,
                grassMat
            };
        }
    }

    private static void EnsureDefaultResources()
    {
        if (s_AsphaltTex == null)
        {
#if UNITY_EDITOR
            s_AsphaltTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/AsphaltGround.png");
#endif
            if (s_AsphaltTex == null) s_AsphaltTex = Resources.Load<Texture2D>("GeneratedSprites/AsphaltGround");
        }

        if (s_CurbTex == null)
        {
#if UNITY_EDITOR
            s_CurbTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/RoadCurbRedWhite.png");
#endif
            if (s_CurbTex == null) s_CurbTex = Resources.Load<Texture2D>("GeneratedSprites/RoadCurbRedWhite");
        }

        if (s_OrangeCurbTex == null)
        {
#if UNITY_EDITOR
            s_OrangeCurbTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/RoadCurbOrangeWhite.png");
#endif
            if (s_OrangeCurbTex == null) s_OrangeCurbTex = Resources.Load<Texture2D>("GeneratedSprites/RoadCurbOrangeWhite");
            if (s_OrangeCurbTex == null) s_OrangeCurbTex = s_CurbTex;
        }

        if (s_ApronTex == null)
        {
#if UNITY_EDITOR
            s_ApronTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/TruckApronPaving.png");
#endif
            if (s_ApronTex == null) s_ApronTex = Resources.Load<Texture2D>("GeneratedSprites/TruckApronPaving");
        }

        if (s_GrassTex == null)
        {
#if UNITY_EDITOR
            s_GrassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/GrassGround.png");
#endif
            if (s_GrassTex == null) s_GrassTex = Resources.Load<Texture2D>("GeneratedSprites/GrassGround");
        }

        if (s_DashesTex == null)
        {
#if UNITY_EDITOR
            s_DashesTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GeneratedSprites/DrivewayDashes.png");
#endif
            if (s_DashesTex == null) s_DashesTex = Resources.Load<Texture2D>("GeneratedSprites/DrivewayDashes");
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");

        if (s_AsphaltMat == null && unlitShader != null)
        {
            s_AsphaltMat = new Material(unlitShader) { name = "Roundabout_Asphalt_Mat", mainTexture = s_AsphaltTex };
        }
        if (s_CurbMat == null && unlitShader != null)
        {
            s_CurbMat = new Material(unlitShader) { name = "Roundabout_Curb_Mat", mainTexture = s_CurbTex };
        }
        if (s_OrangeCurbMat == null && unlitShader != null)
        {
            s_OrangeCurbMat = new Material(unlitShader) { name = "Roundabout_OrangeCurb_Mat", mainTexture = s_OrangeCurbTex };
        }
        if (s_MarkingMat == null && unlitShader != null)
        {
            s_MarkingMat = new Material(unlitShader) { name = "Roundabout_Marking_Mat", mainTexture = s_DashesTex };
        }
        if (s_ApronMat == null && unlitShader != null)
        {
            s_ApronMat = new Material(unlitShader) { name = "Roundabout_Apron_Mat", mainTexture = s_ApronTex };
        }
        if (s_GrassMat == null && unlitShader != null)
        {
            s_GrassMat = new Material(unlitShader) { name = "Roundabout_Grass_Mat", mainTexture = s_GrassTex };
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position;

        // Draw swept path verification arcs
        PassabilityResult p = CheckPassability();

        // 1. Tractor outer bumper trajectory (Cyan)
        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.7f);
        Handles.DrawWireDisc(center, Vector3.forward, p.rFrontOuter);

        // 2. Steer axle centerline
        Handles.color = new Color(0.2f, 0.9f, 1.0f, 0.35f);
        Handles.DrawWireDisc(center, Vector3.forward, p.rSteer);

        // 3. Trailer rear tandem inner wheel trajectory (Green if passable, Red if cutting curb)
        if (p.rInnerWheel > 0.1f)
        {
            Handles.color = p.isPassable ? new Color(0.2f, 1.0f, 0.3f, 0.9f) : new Color(1.0f, 0.2f, 0.2f, 0.95f);
            Handles.DrawWireDisc(center, Vector3.forward, p.rInnerWheel);
            Handles.DrawWireDisc(center, Vector3.forward, p.rInnerWheel + 0.05f);

            // Shaded swept path band
            Handles.color = p.isPassable ? new Color(0.2f, 1.0f, 0.3f, 0.06f) : new Color(1.0f, 0.2f, 0.2f, 0.12f);
            // Draw radial lines indicating swept corridor
            for (int a = 0; a < 360; a += 45)
            {
                float rad = a * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                Handles.DrawLine(center + (dir * p.rInnerWheel), center + (dir * p.rFrontOuter));
            }
        }

        // Draw status label above roundabout center
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = p.isPassable ? new Color(0.2f, 1.0f, 0.3f) : new Color(1.0f, 0.3f, 0.3f);
        labelStyle.fontSize = 12;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        string badge = p.isPassable ? $"✓ WB-67 ПРОХОДИМО (+{p.margin:F1}м)" : $"✗ WB-67 СРЕЗКА (дефицит {Mathf.Abs(p.margin):F1}м)";
        Handles.Label(center + new Vector3(0f, 1.5f, 0f), badge, labelStyle);

        // 4. Draw Connection Arms (Entry / Exit / Two-Way)
        if (arms != null)
        {
            for (int i = 0; i < arms.Count; i++)
            {
                RoundaboutArm arm = arms[i];
                Vector3 sPos = (Vector3)GetArmSocketWorldPos(i);
                Vector3 dir = (Vector3)GetArmSocketDirection(i);

                Color armCol = (arm.armType == RoundaboutArmType.Entry) ? new Color(0.2f, 1.0f, 0.4f, 0.95f) :
                               (arm.armType == RoundaboutArmType.Exit) ? new Color(0.2f, 0.7f, 1.0f, 0.95f) :
                               new Color(1.0f, 0.85f, 0.2f, 0.95f);

                Handles.color = armCol;
                Handles.DrawSolidDisc(sPos, Vector3.forward, 0.5f);
                Handles.DrawWireDisc(sPos, Vector3.forward, 0.7f);

                Vector3 arrowTarget = (arm.armType == RoundaboutArmType.Entry) ? sPos - (dir * 2.0f) : sPos + (dir * 2.0f);
                Vector3 arrowSource = (arm.armType == RoundaboutArmType.Entry) ? sPos + (dir * 2.0f) : sPos;
                Handles.DrawLine(arrowSource, arrowTarget);

                string typePrefix = (arm.armType == RoundaboutArmType.Entry) ? "🟢 [Заезд]" :
                                    (arm.armType == RoundaboutArmType.Exit) ? "🔵 [Выезд]" : "🔄 [Двусторонний]";

                GUIStyle lblStyle = new GUIStyle(EditorStyles.boldLabel);
                lblStyle.normal.textColor = armCol;
                Handles.Label(sPos + new Vector3(0.5f, 0.5f, 0f), $"{typePrefix} {arm.name}", lblStyle);
            }
        }
    }
#endif
}
