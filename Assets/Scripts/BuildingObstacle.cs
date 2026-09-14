using UnityEngine;

/// <summary>
/// Solid Warehouse / Building Obstacle Component.
/// Forms a solid rectangular building with a realistic top-down industrial roof:
/// - Dark slate parapet rim / border flashing
/// - Tiled corrugated metal roof surface
/// - Elevated drop shadow
/// - Authentic commercial rooftop HVAC condensers, skylights, and vents
/// - Bold warehouse rooftop signage
/// - Solid BoxCollider2D covering the full building area
/// </summary>
[ExecuteAlways]
public class BuildingObstacle : MonoBehaviour
{
    [Header("Building Rectangle Dimensions")]
    public Vector2 centerPosition;
    public Vector2 size = new Vector2(13.5f, 13.5f); // Width (X) and Length (Y) in meters
    public float rotationAngle = 0f;

    [Header("Legacy Corner References")]
    public Vector2 startPoint;
    public Vector2 endPoint;

    [Header("Appearance & Customization")]
    public Color buildingColor = new Color(0.84f, 0.84f, 0.81f, 1.0f); // #d6d6ce light warm grey metal roof
    public Color outlineColor = new Color(0.28f, 0.33f, 0.41f, 1.0f);  // #475569 slate parapet border
    public string buildingLabel = "LOGISTICS DC";
    public bool showRoofDetails = true;
    public bool showShadow = true;

    // Sprite Cache
    private static Sprite s_RoofSprite;
    private static Sprite s_HvacSprite;
    private static Sprite s_SkylightSprite;
    private static Sprite s_VentSprite;
    private static Sprite s_BorderSprite;

    private static void LoadSpritesIfNull()
    {
#if UNITY_EDITOR
        if (s_RoofSprite == null)
            s_RoofSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/WarehouseRoof_Base.png")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/BuildingRoof.png");
        if (s_HvacSprite == null)
            s_HvacSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/HVAC_Unit.png");
        if (s_SkylightSprite == null)
            s_SkylightSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/RooftopSkylight.png");
        if (s_VentSprite == null)
            s_VentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/RoofVent.png");
        if (s_BorderSprite == null)
            s_BorderSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/ParkingStripe.png")
                          ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GeneratedSprites/Square.png");
#endif
        if (s_RoofSprite == null) s_RoofSprite = Resources.Load<Sprite>("GeneratedSprites/WarehouseRoof_Base");
        if (s_HvacSprite == null) s_HvacSprite = Resources.Load<Sprite>("GeneratedSprites/HVAC_Unit");
        if (s_SkylightSprite == null) s_SkylightSprite = Resources.Load<Sprite>("GeneratedSprites/RooftopSkylight");
        if (s_VentSprite == null) s_VentSprite = Resources.Load<Sprite>("GeneratedSprites/RoofVent");
        if (s_BorderSprite == null) s_BorderSprite = Resources.Load<Sprite>("GeneratedSprites/ParkingStripe");
    }

    /// <summary>
    /// Setup building by two opposite diagonal corners (Start -> End rectangle)
    /// </summary>
    public void SetupByCorners(Vector2 cornerA, Vector2 cornerB, Color? color = null, string label = "KROGER DC")
    {
        startPoint = cornerA;
        endPoint = cornerB;

        float minX = Mathf.Min(cornerA.x, cornerB.x);
        float maxX = Mathf.Max(cornerA.x, cornerB.x);
        float minY = Mathf.Min(cornerA.y, cornerB.y);
        float maxY = Mathf.Max(cornerA.y, cornerB.y);

        float w = Mathf.Max(4.0f, maxX - minX);
        float h = Mathf.Max(4.0f, maxY - minY);

        size = new Vector2(w, h);
        centerPosition = new Vector2(minX + w * 0.5f, minY + h * 0.5f);
        rotationAngle = 0f;

        if (color.HasValue) buildingColor = color.Value;
        if (!string.IsNullOrEmpty(label)) buildingLabel = label;

        UpdateTransformAndVisual();
    }

    /// <summary>
    /// Setup building by center and size
    /// </summary>
    public void Setup(Vector2 center, Vector2 buildingSize, float rot = 0f, Color? color = null, string label = "KROGER DC")
    {
        centerPosition = center;
        size = new Vector2(Mathf.Max(3f, buildingSize.x), Mathf.Max(3f, buildingSize.y));
        rotationAngle = rot;
        startPoint = center - size * 0.5f;
        endPoint = center + size * 0.5f;

        if (color.HasValue) buildingColor = color.Value;
        if (!string.IsNullOrEmpty(label)) buildingLabel = label;

        UpdateTransformAndVisual();
    }

    /// <summary>
    /// Legacy compatibility Setup
    /// </summary>
    public void Setup(Vector2 start, Vector2 end, float buildingThickness = 10.0f, Color? color = null, string label = "", Sprite sprite = null)
    {
        if (color.HasValue) buildingColor = color.Value;
        if (!string.IsNullOrEmpty(label)) buildingLabel = label;

        Vector2 diff = end - start;
        if (Mathf.Abs(diff.x) > 0.5f && Mathf.Abs(diff.y) > 0.5f)
        {
            SetupByCorners(start, end, buildingColor, buildingLabel);
        }
        else
        {
            // Linear segment with thickness
            float len = diff.magnitude;
            if (len < 0.1f) len = 0.1f;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90f;
            Vector2 center = (start + end) * 0.5f;
            size = new Vector2(Mathf.Max(3f, buildingThickness), len);
            centerPosition = center;
            rotationAngle = angle;
            startPoint = start;
            endPoint = end;
            UpdateTransformAndVisual(sprite);
        }
    }

    public void UpdateTransformAndVisual(Sprite customRoofSprite = null)
    {
        LoadSpritesIfNull();

        Sprite roofSp = customRoofSprite != null ? customRoofSprite : s_RoofSprite;
        Sprite hvacSp = s_HvacSprite;
        Sprite skylightSp = s_SkylightSprite;
        Sprite ventSp = s_VentSprite;
        Sprite borderSp = s_BorderSprite ?? roofSp;

        if (size.x < 3.0f) size.x = 3.0f;
        if (size.y < 3.0f) size.y = 3.0f;

        transform.position = new Vector3(centerPosition.x, centerPosition.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationAngle);

        // Remove old renderer directly on root if present
        SpriteRenderer rootSr = GetComponent<SpriteRenderer>();
        if (rootSr != null)
        {
            if (Application.isPlaying) Destroy(rootSr);
            else DestroyImmediate(rootSr);
        }

        // 1. Root Solid Box Collider for Physical Collision
        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider2D>();
        boxCol.size = size;
        boxCol.offset = Vector2.zero;
        boxCol.isTrigger = true; // Triggers collision detector in TruckCollisionDetector

        // 2. Elevated Drop Shadow underneath South-East
        Transform shadowTr = transform.Find("Shadow");
        GameObject shadowGo;
        if (shadowTr == null)
        {
            shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(transform, false);
        }
        else
        {
            shadowGo = shadowTr.gameObject;
        }

        if (showShadow)
        {
            shadowGo.SetActive(true);
            shadowGo.transform.localPosition = new Vector3(1.2f, -1.8f, 0f);
            shadowGo.transform.localRotation = Quaternion.identity;
            SpriteRenderer srShadow = shadowGo.GetComponent<SpriteRenderer>();
            if (srShadow == null) srShadow = shadowGo.AddComponent<SpriteRenderer>();
            srShadow.sprite = borderSp;
            srShadow.drawMode = SpriteDrawMode.Tiled;
            srShadow.size = size + new Vector2(0.4f, 0.4f);
            srShadow.color = new Color(0f, 0f, 0f, 0.48f);
            srShadow.sortingOrder = 4;
        }
        else
        {
            shadowGo.SetActive(false);
        }

        // 3. Parapet Border / Outer Wall Rim
        Transform parapetTr = transform.Find("ParapetBorder");
        GameObject parapetGo;
        if (parapetTr == null)
        {
            parapetGo = new GameObject("ParapetBorder");
            parapetGo.transform.SetParent(transform, false);
        }
        else
        {
            parapetGo = parapetTr.gameObject;
        }
        parapetGo.transform.localPosition = Vector3.zero;
        parapetGo.transform.localRotation = Quaternion.identity;
        SpriteRenderer srParapet = parapetGo.GetComponent<SpriteRenderer>();
        if (srParapet == null) srParapet = parapetGo.AddComponent<SpriteRenderer>();
        srParapet.sprite = borderSp;
        srParapet.drawMode = SpriteDrawMode.Tiled;
        srParapet.size = size;
        srParapet.color = outlineColor;
        srParapet.sortingOrder = 6;

        // 4. Main Solid Inset Roof Surface
        Transform roofTr = transform.Find("RoofSurface");
        GameObject roofGo;
        if (roofTr == null)
        {
            roofGo = new GameObject("RoofSurface");
            roofGo.transform.SetParent(transform, false);
        }
        else
        {
            roofGo = roofTr.gameObject;
        }
        roofGo.transform.localPosition = Vector3.zero;
        roofGo.transform.localRotation = Quaternion.identity;
        SpriteRenderer srRoof = roofGo.GetComponent<SpriteRenderer>();
        if (srRoof == null) srRoof = roofGo.AddComponent<SpriteRenderer>();
        srRoof.sprite = roofSp ?? borderSp;
        srRoof.drawMode = SpriteDrawMode.Tiled;
        srRoof.size = new Vector2(Mathf.Max(1.0f, size.x - 0.8f), Mathf.Max(1.0f, size.y - 0.8f));
        srRoof.color = buildingColor;
        srRoof.sortingOrder = 7;

        // 5. Rooftop Equipment (HVAC Units, Skylights, Vents)
        Transform equipTr = transform.Find("RoofEquipment");
        if (equipTr != null)
        {
            if (Application.isPlaying) Destroy(equipTr.gameObject);
            else DestroyImmediate(equipTr.gameObject);
        }

        if (showRoofDetails && size.x >= 6f && size.y >= 6f)
        {
            GameObject equipGo = new GameObject("RoofEquipment");
            equipGo.transform.SetParent(transform, false);
            equipGo.transform.localPosition = Vector3.zero;

            float marginX = size.x * 0.22f;
            float marginY = size.y * 0.22f;
            float usableW = size.x - marginX * 2f;
            float usableH = size.y - marginY * 2f;

            // Place HVAC condenser units along top/bottom bays
            if (hvacSp != null && usableW >= 3f && usableH >= 3f)
            {
                int hvacCols = Mathf.Clamp(Mathf.FloorToInt(usableW / 12f), 1, 6);
                float stepX = (hvacCols > 1) ? (usableW / (hvacCols - 1)) : 0f;
                float startX = -usableW * 0.5f;

                for (int i = 0; i < hvacCols; i++)
                {
                    float x = (hvacCols == 1) ? 0f : (startX + i * stepX);
                    // Top row HVAC
                    CreateEquipmentChild(equipGo.transform, "HVAC_Top_" + i, new Vector3(x, usableH * 0.35f, 0f), hvacSp, new Vector3(1f, 1f, 1f), 8);
                    // Bottom row HVAC (if building is tall enough)
                    if (usableH >= 18f)
                    {
                        CreateEquipmentChild(equipGo.transform, "HVAC_Bot_" + i, new Vector3(x, -usableH * 0.35f, 0f), hvacSp, new Vector3(1f, 1f, 1f), 8);
                    }
                }
            }

            // Place Skylight rows in the middle
            if (skylightSp != null && usableW >= 8f && usableH >= 10f)
            {
                int skyRows = Mathf.Clamp(Mathf.FloorToInt(usableH / 14f), 1, 4);
                float stepY = (skyRows > 1) ? (usableH * 0.5f / (skyRows - 1)) : 0f;
                float startY = -usableH * 0.25f;

                for (int j = 0; j < skyRows; j++)
                {
                    float y = (skyRows == 1) ? 0f : (startY + j * stepY);
                    CreateEquipmentChild(equipGo.transform, "Skylight_Left_" + j, new Vector3(-usableW * 0.25f, y, 0f), skylightSp, new Vector3(1.1f, 1.1f, 1f), 8);
                    CreateEquipmentChild(equipGo.transform, "Skylight_Right_" + j, new Vector3(usableW * 0.25f, y, 0f), skylightSp, new Vector3(1.1f, 1.1f, 1f), 8);
                }
            }

            // Place Circular turbine vents in between
            if (ventSp != null && usableW >= 3f)
            {
                int ventCount = Mathf.Clamp(Mathf.FloorToInt(usableW / 8f), 1, 5);
                float stepV = (ventCount > 1) ? (usableW / (ventCount - 1)) : 0f;
                float startV = -usableW * 0.5f;

                for (int k = 0; k < ventCount; k++)
                {
                    float vx = (ventCount == 1) ? 0f : (startV + k * stepV);
                    CreateEquipmentChild(equipGo.transform, "Vent_" + k, new Vector3(vx, 0f, 0f), ventSp, new Vector3(1.2f, 1.2f, 1f), 8);
                }
            }
        }

        // 6. Rooftop Text Sign
        Transform labelTr = transform.Find("RoofLabel");
        if (labelTr != null)
        {
            if (Application.isPlaying) Destroy(labelTr.gameObject);
            else DestroyImmediate(labelTr.gameObject);
        }

        if (!string.IsNullOrEmpty(buildingLabel) && size.x >= 8f && size.y >= 6f)
        {
            GameObject labelGo = new GameObject("RoofLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            labelGo.transform.localRotation = Quaternion.identity;

            TextMesh tm = labelGo.AddComponent<TextMesh>();
            tm.text = buildingLabel;
            tm.fontSize = 48;
            tm.characterSize = 0.22f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.18f, 0.22f, 0.30f, 0.95f); // Deep slate industrial text
            tm.fontStyle = FontStyle.Bold;

            MeshRenderer mr = labelGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 9;
            }
        }
    }

    private static void CreateEquipmentChild(Transform parent, string name, Vector3 localPos, Sprite sprite, Vector3 localScale, int sortOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Color.white;
        sr.sortingOrder = sortOrder;
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
 
