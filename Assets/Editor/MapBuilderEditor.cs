using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class MapBuilderEditor : EditorWindow
{
    public enum ObjectType
    {
        StandardEmpty = 0,   // 1. Стандартное без трака (4.5м)
        StandardParked = 1,  // 2. Стандартное с траком (4.5м)
        TargetParking = 2,   // 3. Целевое место парковки (4.5м, Единственное)
        TruckStartPoint = 3  // 4. Место старта трака (Единственный)
    }

    public const string CustomMapsFolder = "Assets/Scenes/CustomMaps";
    private const string SpritesDir = "Assets/GeneratedSprites";
    private const string WorkspaceRootName = "MapBuilder_Workspace";
    private const string SlotsContainerName = "MapBuilder_Slots";
    private const string GhostPreviewName = "__MapBuilder_GhostPreview__";

    public const float SlotWidth = 4.5f;   // Фиксированная ширина стандартного места (4.5м)
    public const float SlotLength = 26.0f; // Фиксированная длина места (26.0м)

    [SerializeField] private ObjectType currentObjectType = ObjectType.StandardEmpty;
    [SerializeField] private Vector2 cursorPosition = new Vector2(2.25f, 11.0f);
    [SerializeField] private float currentRotation = 0f; // 0, 90, 180, 270 degrees
    [SerializeField] private bool builderActive = true;
    [SerializeField] private bool showVisualGrid = false;
    [SerializeField] private bool autoAdvanceAfterPlacement = true;
    [SerializeField] private bool snapMouseClick = true;
    [SerializeField] private bool showControlsOverlay = true;
    [SerializeField] private string mapName = "MyParkingMap_1";
    [SerializeField] private float mapWidth = 54.0f;   // Ширина карты в метрах
    [SerializeField] private float mapHeight = 60.0f;  // Высота карты в метрах

    private GameObject ghostPreviewObj;
    private ObjectType lastBuiltPreviewType = (ObjectType)(-1);
    private Sprite asphaltSprite;
    private Sprite stripeSprite;
    private Sprite tractorSprite;
    private Sprite tractorHDSprite;
    private Sprite trailerSprite;
    private Sprite arrowSprite;
    private Sprite yellowArrowSprite;
    private Sprite wheelSprite;
    private Sprite pedalGasSprite;
    private Sprite pedalBrakeSprite;
    private Sprite steeringWheelSprite;

    private static readonly string[] ObjectTypeNames = new string[]
    {
        "1. Стандартное без трака (4.5м)",
        "2. Стандартное с траком (4.5м)",
        "3. Целевое место парковки (4.5м, Единственное)",
        "4. Место старта трака (Единственный)"
    };

    [MenuItem("Tools/Map Builder/Open Editor", false, 1)]
    public static void OpenEditor()
    {
        MapBuilderEditor window = GetWindow<MapBuilderEditor>("Map Builder");
        window.minSize = new Vector2(360, 560);
        window.Show();
        window.builderActive = true;
        window.EnsureCleanWorkPlane(clearExistingScene: false);
        window.SnapCursorToGrid();
        window.UpdateGhostPreview();
        window.FocusSceneView();
    }

    [MenuItem("Tools/Map Builder/New Clean Map Canvas", false, 2)]
    public static void NewCleanMap()
    {
        MapBuilderEditor window = GetWindow<MapBuilderEditor>("Map Builder");
        window.minSize = new Vector2(360, 560);
        window.Show();
        window.builderActive = true;
        window.currentObjectType = ObjectType.StandardEmpty;
        window.cursorPosition = new Vector2(SlotWidth * 0.5f, SlotLength * 0.5f);
        window.currentRotation = 0f;
        window.mapName = GetNextAvailableMapName();
        window.EnsureCleanWorkPlane(clearExistingScene: true);
        window.SnapCursorToGrid();
        window.UpdateGhostPreview();
        window.FocusSceneView();
    }

    [MenuItem("Tools/Map Builder/Править сохранённую карту", false, 3)]
    public static void OpenSavedMapsManager()
    {
        SavedMapsWindow.Open();
    }

    private void OnEnable()
    {
        LoadSprites();
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        SnapCursorToGrid();
        UpdateGhostPreview();
        DetectCurrentSceneMapName();
        HideCanvasInEditMode();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        DestroyGhostPreview();
        SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            DestroyGhostPreview();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            UpdateGhostPreview();
        }
    }

    public void FocusSceneView()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null && SceneView.sceneViews.Count > 0)
        {
            sv = SceneView.sceneViews[0] as SceneView;
        }

        if (sv != null)
        {
            Selection.activeObject = null;
            sv.in2DMode = true;
            sv.LookAt(new Vector3(27f, 30f, 0f), Quaternion.identity, 38f);
            sv.Focus();
            sv.Repaint();
        }
    }

    private void DetectCurrentSceneMapName()
    {
        string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(scenePath))
        {
            string fileName = Path.GetFileNameWithoutExtension(scenePath);
            if (!string.IsNullOrEmpty(fileName) && fileName != "SampleScene")
            {
                mapName = fileName;
            }
        }

        // Detect current ground size from scene if present
        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace != null)
        {
            MapData mapData = workspace.GetComponent<MapData>();
            if (mapData != null)
            {
                mapData.DetectDimensions();
                mapWidth = mapData.mapWidth;
                mapHeight = mapData.mapHeight;
            }
            else
            {
                Transform groundTr = workspace.transform.Find("AsphaltGround");
                if (groundTr != null)
                {
                    SpriteRenderer sr = groundTr.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        mapWidth = sr.size.x;
                        mapHeight = sr.size.y;
                    }
                }
            }
        }
    }

    public static string GetNextAvailableMapName()
    {
        EnsureFolder(CustomMapsFolder);
        int index = 1;
        while (File.Exists($"{CustomMapsFolder}/Map_{index}.unity"))
        {
            index++;
        }
        return $"Map_{index}";
    }

    public static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }
    }

    #region Play Mode Safe Helpers

    private static void SafeMarkSceneDirty()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }

    private static void SafeRegisterCreatedObjectUndo(UnityEngine.Object obj, string name)
    {
        if (!EditorApplication.isPlaying && obj != null)
        {
            Undo.RegisterCreatedObjectUndo(obj, name);
        }
    }

    private static void SafeDestroyObject(GameObject go)
    {
        if (go == null) return;
        if (!EditorApplication.isPlaying)
        {
            Undo.DestroyObjectImmediate(go);
        }
        else
        {
            Destroy(go);
        }
    }

    #endregion

    #region Magnetic Snapping Math

    private Vector2 GetMagneticSnappedPosition(Vector2 rawPos, float rot)
    {
        // Only apply magnetic snap for parking stalls (StandardEmpty, StandardParked, TargetParking)
        if (currentObjectType == ObjectType.TruckStartPoint) return rawPos;

        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace == null) return rawPos;
        Transform container = workspace.transform.Find(SlotsContainerName);
        if (container == null) return rawPos;

        // Subtle, gentle magnetic snap threshold: 0.75 meters (gives 3.0m+ of smooth free movement between slots)
        float bestDist = 0.75f;
        Vector2 bestSnapPos = rawPos;
        bool snapped = false;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            bool isCompatibleSlot = child.name.StartsWith("Stall_") || child.name.StartsWith("TargetParking");
            if (!isCompatibleSlot) continue;

            // Check rotation similarity (parallel slots with same angle)
            float childAngle = child.eulerAngles.z;
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(childAngle, rot));
            bool isParallel = deltaAngle < 5.0f || Mathf.Abs(deltaAngle - 180f) < 5.0f;
            if (!isParallel) continue;

            Vector2 childPos = new Vector2(child.position.x, child.position.y);
            Vector2 childRight = new Vector2(child.right.x, child.right.y);
            Vector2 childUp = new Vector2(child.up.x, child.up.y);

            // Candidate snap points: only direct adjacent neighbor slots (+/- 4.5m side or +/- 26m end)
            Vector2[] candidateSnapPoints = new Vector2[]
            {
                childPos + childRight * SlotWidth, // +1 slot right (4.5m)
                childPos - childRight * SlotWidth, // -1 slot left (4.5m)
                childPos + childUp * SlotLength,   // end-to-end forward (26.0m)
                childPos - childUp * SlotLength    // end-to-end backward (26.0m)
            };

            foreach (var cand in candidateSnapPoints)
            {
                float dist = Vector2.Distance(rawPos, cand);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestSnapPos = cand;
                    snapped = true;
                }
            }
        }

        return snapped ? bestSnapPos : rawPos;
    }

    #endregion

    #region Grid Snapping Math

    private void SnapCursorToGrid()
    {
        if (currentObjectType == ObjectType.TruckStartPoint) return;

        float rem = Mathf.Abs(currentRotation) % 90f;
        if (rem > 1.0f && rem < 89.0f)
        {
            return; // Diagonal placement (45 deg, etc.): free position preserved without axis snapping
        }

        float width = SlotWidth;
        float length = SlotLength;

        bool isRotatedHorizontal = Mathf.Approximately(Mathf.Abs(currentRotation), 90f) || Mathf.Approximately(Mathf.Abs(currentRotation), 270f);

        if (isRotatedHorizontal)
        {
            int colX = Mathf.RoundToInt((cursorPosition.x - length * 0.5f) / length);
            int rowY = Mathf.RoundToInt((cursorPosition.y - width * 0.5f) / width);

            cursorPosition.x = (colX + 0.5f) * length;
            cursorPosition.y = (rowY + 0.5f) * width;
        }
        else
        {
            int colX = Mathf.RoundToInt((cursorPosition.x - width * 0.5f) / width);
            int rowY = Mathf.RoundToInt((cursorPosition.y - length * 0.5f) / length);

            cursorPosition.x = (colX + 0.5f) * width;
            cursorPosition.y = (rowY + 0.5f) * length;
        }
    }

    #endregion

    #region Sprite Loading

    private void LoadSprites()
    {
        asphaltSprite = LoadOrCreateSprite(SpritesDir + "/AsphaltGround.png", () => CreateSolidTexture(64, 64, new Color(0.20f, 0.20f, 0.22f)));
        stripeSprite = LoadOrCreateSprite(SpritesDir + "/ParkingStripe.png", () => CreateStripeTexture(16, 128, Color.white));
        tractorSprite = LoadOrCreateSprite(SpritesDir + "/Tractor.png", () => CreateSolidTexture(32, 64, new Color(0.15f, 0.45f, 0.85f)));
        tractorHDSprite = LoadOrCreateSprite(SpritesDir + "/Tractor_HD.png", null);
        trailerSprite = LoadOrCreateSprite(SpritesDir + "/Trailer.png", () => CreateSolidTexture(32, 128, new Color(0.88f, 0.88f, 0.90f)));
        arrowSprite = LoadOrCreateSprite(SpritesDir + "/DockArrow.png", null);
        yellowArrowSprite = LoadOrCreateSprite(SpritesDir + "/YellowParkingArrow.png", CreateYellowArrowTexture);
        wheelSprite = LoadOrCreateSprite(SpritesDir + "/Tire.png", () => CreateSolidTexture(16, 32, new Color(0.15f, 0.15f, 0.15f)));
        pedalGasSprite = LoadOrCreateSprite(SpritesDir + "/PedalGas.png", null);
        pedalBrakeSprite = LoadOrCreateSprite(SpritesDir + "/PedalBrake.png", null);
        steeringWheelSprite = LoadOrCreateSprite(SpritesDir + "/SteeringWheelRealistic.png", null);
        if (steeringWheelSprite == null) steeringWheelSprite = LoadOrCreateSprite(SpritesDir + "/SteeringWheel.png", null);
    }

    private Sprite LoadOrCreateSprite(string path, Func<Texture2D> proceduralFallback)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        if (proceduralFallback != null)
        {
            Texture2D tex = proceduralFallback();
            if (tex != null)
            {
                if (!Directory.Exists(SpritesDir)) Directory.CreateDirectory(SpritesDir);
                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spritePixelsPerUnit = 100f;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return null;
    }

    private Texture2D CreateSolidTexture(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateYellowArrowTexture()
    {
        int w = 128;
        int h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);
        Color yellow = new Color(1.0f, 0.84f, 0.05f, 1.0f);
        Color[] colors = new Color[w * h];
        for (int i = 0; i < colors.Length; i++) colors[i] = transparent;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Vertical shaft: y from 20 to 140, x from 44 to 84 (40px wide)
                bool inStem = (y >= 20 && y <= 140 && x >= 44 && x <= 84);

                // Arrow head: triangle from y=125 to y=242
                bool inHead = false;
                if (y >= 125 && y <= 242)
                {
                    float progress = (y - 125f) / 117f; // 0 at base, 1 at tip
                    float halfWidthAtY = (1f - progress) * 54f;
                    float midX = 64f;
                    if (Mathf.Abs(x - midX) <= halfWidthAtY)
                    {
                        inHead = true;
                    }
                }

                if (inStem || inHead)
                {
                    colors[y * w + x] = yellow;
                }
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateStripeTexture(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    #endregion

    #region Map Sizing & Dimension Controls

    public void SetMapDimensions(float newWidth, float newHeight)
    {
        mapWidth = Mathf.Clamp(newWidth, 18.0f, 150.0f);
        mapHeight = Mathf.Clamp(newHeight, 25.0f, 150.0f);

        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace == null)
        {
            EnsureCleanWorkPlane(clearExistingScene: false);
            workspace = GameObject.Find(WorkspaceRootName);
        }

        if (workspace != null)
        {
            MapData mapData = workspace.GetComponent<MapData>();
            if (mapData == null) mapData = workspace.AddComponent<MapData>();
            mapData.SetDimensions(mapWidth, mapHeight);

            Vector3 center = new Vector3(mapWidth * 0.5f, mapHeight * 0.5f, 0f);

            // 1. Update Asphalt
            Transform groundTr = workspace.transform.Find("AsphaltGround");
            if (groundTr != null)
            {
                groundTr.position = center;
                SpriteRenderer sr = groundTr.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.size = new Vector2(mapWidth, mapHeight);
                }
            }

            // 2. Update Yard Borders & Physical Colliders
            Transform borderTr = workspace.transform.Find("YardBorders");
            if (borderTr != null)
            {
                UpdateBorderLine(borderTr, "Border_Bottom", new Vector3(mapWidth * 0.5f, 0f, 0f), new Vector2(mapWidth + 2f, 1.0f), new Vector2(mapWidth + 10f, 4.0f), new Vector2(0f, -1.5f));
                UpdateBorderLine(borderTr, "Border_Top", new Vector3(mapWidth * 0.5f, mapHeight, 0f), new Vector2(mapWidth + 2f, 1.0f), new Vector2(mapWidth + 10f, 4.0f), new Vector2(0f, 1.5f));
                UpdateBorderLine(borderTr, "Border_Left", new Vector3(0f, mapHeight * 0.5f, 0f), new Vector2(1.0f, mapHeight + 2f), new Vector2(4.0f, mapHeight + 10f), new Vector2(-1.5f, 0f));
                UpdateBorderLine(borderTr, "Border_Right", new Vector3(mapWidth, mapHeight * 0.5f, 0f), new Vector2(1.0f, mapHeight + 2f), new Vector2(4.0f, mapHeight + 10f), new Vector2(1.5f, 0f));
            }

            SafeMarkSceneDirty();
            SceneView.RepaintAll();
        }
    }

    private void UpdateBorderLine(Transform parent, string name, Vector3 pos, Vector2 spriteSize, Vector2 colSize, Vector2 colOffset)
    {
        Transform lineTr = parent.Find(name);
        if (lineTr != null)
        {
            lineTr.position = pos;
            SpriteRenderer sr = lineTr.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.size = spriteSize;
                sr.color = new Color(0.98f, 0.82f, 0.12f, 1.0f);
            }
            BoxCollider2D col = lineTr.GetComponent<BoxCollider2D>();
            if (col == null) col = lineTr.gameObject.AddComponent<BoxCollider2D>();
            col.size = colSize;
            col.offset = colOffset;
            col.isTrigger = true;
        }
        else
        {
            CreateBorderLine(name, parent, pos, spriteSize, colSize, colOffset);
        }
    }

    public void AdjustMapHeight(float delta)
    {
        SetMapDimensions(mapWidth, mapHeight + delta);
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent($"Высота карты: {mapHeight:F0}м"));
        }
    }

    public void AdjustMapWidth(float delta)
    {
        SetMapDimensions(mapWidth + delta, mapHeight);
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.ShowNotification(new GUIContent($"Ширина карты: {mapWidth:F0}м"));
        }
    }

    public void ShiftAllMapObjects(Vector2 delta)
    {
        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace != null)
        {
            Transform slotsContainer = workspace.transform.Find(SlotsContainerName);
            if (slotsContainer != null)
            {
                if (!EditorApplication.isPlaying)
                {
                    Undo.RegisterFullObjectHierarchyUndo(slotsContainer.gameObject, "Shift Placed Slots");
                }
                for (int i = 0; i < slotsContainer.childCount; i++)
                {
                    Transform child = slotsContainer.GetChild(i);
                    child.position += (Vector3)delta;
                }
            }

            GameObject tractor = FindPlayerTractor();
            GameObject trailer = FindPlayerTrailer();
            if (tractor != null)
            {
                if (!EditorApplication.isPlaying) Undo.RecordObject(tractor.transform, "Shift Truck");
                tractor.transform.position += (Vector3)delta;
                
                Rigidbody2D trRb = tractor.GetComponent<Rigidbody2D>();
                if (trRb != null) { trRb.linearVelocity = Vector2.zero; trRb.angularVelocity = 0f; }
            }
            if (trailer != null)
            {
                if (!EditorApplication.isPlaying) Undo.RecordObject(trailer.transform, "Shift Truck");
                trailer.transform.position += (Vector3)delta;
                
                Rigidbody2D tlRb = trailer.GetComponent<Rigidbody2D>();
                if (tlRb != null) { tlRb.linearVelocity = Vector2.zero; tlRb.angularVelocity = 0f; }
            }

            if (tractor != null && trailer != null)
            {
                TruckController tc = tractor.GetComponent<TruckController>();
                if (tc != null)
                {
                    tc.SetupTrailer(trailer.GetComponent<Rigidbody2D>());
                }
            }

            if (Camera.main != null && tractor != null)
            {
                Camera.main.transform.position = new Vector3(tractor.transform.position.x, tractor.transform.position.y, -10f);
            }

            cursorPosition += delta;
            UpdateGhostPreview();
            SafeMarkSceneDirty();
            Repaint();
            SceneView.RepaintAll();

            if (SceneView.lastActiveSceneView != null)
            {
                string dirStr = delta.x != 0 ? $"X: {delta.x:+0.0;-0.0}м" : $"Y: {delta.y:+0.0;-0.0}м";
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent($"✥ Все объекты сдвинуты ({dirStr})"));
            }
        }
    }

    #endregion

    #region Workspace & Compact Rest Area Ground Plane

    public void EnsureCleanWorkPlane(bool clearExistingScene)
    {
        Physics2D.gravity = Vector2.zero;

        if (clearExistingScene)
        {
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject go in rootObjects)
            {
                if (go.name == "Main Camera" || go.name == "Global Light 2D" || go.name == "Directional Light" || go.name == "EventSystem")
                {
                    continue;
                }
                SafeDestroyObject(go);
            }
        }

        // 1. Ensure 2D Camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 25f;
            camGo.transform.position = new Vector3(27f, 25f, -10f);
            camGo.transform.rotation = Quaternion.identity;
            SafeRegisterCreatedObjectUndo(camGo, "Create Camera");
        }
        else
        {
            cam.orthographic = true;
            cam.orthographicSize = 25f;
            cam.transform.position = new Vector3(27f, 25f, -10f);
        }

        // 2. Ensure 2D Global Light
        Light2D globalLight = FindFirstObjectByType<Light2D>();
        if (globalLight == null)
        {
            GameObject lightGo = new GameObject("Global Light 2D");
            globalLight = lightGo.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1.0f;
            globalLight.color = Color.white;
            SafeRegisterCreatedObjectUndo(lightGo, "Create Global Light");
        }

        // 3. Compact Rest Area Yard Setup
        float yardWidth = mapWidth;
        float yardHeight = mapHeight;
        Vector3 yardCenter = new Vector3(yardWidth * 0.5f, yardHeight * 0.5f, 0f);

        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace == null)
        {
            workspace = new GameObject(WorkspaceRootName);
            SafeRegisterCreatedObjectUndo(workspace, "Create Map Workspace");
        }

        MapData wsMapData = workspace.GetComponent<MapData>();
        if (wsMapData == null) wsMapData = workspace.AddComponent<MapData>();
        wsMapData.SetDimensions(mapWidth, mapHeight);

        Transform groundTr = workspace.transform.Find("AsphaltGround");
        if (groundTr == null)
        {
            GameObject ground = new GameObject("AsphaltGround");
            ground.transform.SetParent(workspace.transform, false);
            ground.transform.position = yardCenter;
            ground.transform.localScale = Vector3.one;

            SpriteRenderer sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = asphaltSprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(yardWidth, yardHeight);
            sr.sortingOrder = -10;

            SafeRegisterCreatedObjectUndo(ground, "Create Asphalt Ground");
        }

        // Boundary lines and physical collision barriers around the map perimeter
        Transform borderTr = workspace.transform.Find("YardBorders");
        if (borderTr == null)
        {
            GameObject borderParent = new GameObject("YardBorders");
            borderParent.transform.SetParent(workspace.transform, false);

            float w = yardWidth;
            float h = yardHeight;

            CreateBorderLine("Border_Bottom", borderParent.transform, new Vector3(w * 0.5f, 0f, 0f), new Vector2(w + 2f, 1.0f), new Vector2(w + 10f, 4.0f), new Vector2(0f, -1.5f));
            CreateBorderLine("Border_Top", borderParent.transform, new Vector3(w * 0.5f, h, 0f), new Vector2(w + 2f, 1.0f), new Vector2(w + 10f, 4.0f), new Vector2(0f, 1.5f));
            CreateBorderLine("Border_Left", borderParent.transform, new Vector3(0f, h * 0.5f, 0f), new Vector2(1.0f, h + 2f), new Vector2(4.0f, h + 10f), new Vector2(-1.5f, 0f));
            CreateBorderLine("Border_Right", borderParent.transform, new Vector3(w, h * 0.5f, 0f), new Vector2(1.0f, h + 2f), new Vector2(4.0f, h + 10f), new Vector2(1.5f, 0f));

            SafeRegisterCreatedObjectUndo(borderParent, "Create Yard Borders");
        }

        Transform slotsContainer = workspace.transform.Find(SlotsContainerName);
        if (slotsContainer == null)
        {
            GameObject slots = new GameObject(SlotsContainerName);
            slots.transform.SetParent(workspace.transform, false);
            SafeRegisterCreatedObjectUndo(slots, "Create Slots Container");
        }

        // 4. Ensure Playable Truck & Mobile Canvas exist
        EnsurePlayableTruckAndCanvas();

        SafeMarkSceneDirty();
    }

    public static void HideCanvasInEditMode()
    {
        if (!EditorApplication.isPlaying)
        {
            GameObject canvas = GameObject.Find("TruckControlsCanvas");
            if (canvas != null && canvas.activeSelf)
            {
                canvas.SetActive(false);
            }
        }
    }


    public static GameObject FindPlayerTractor()
    {
        TruckController tc = UnityEngine.Object.FindFirstObjectByType<TruckController>();
        if (tc != null) return tc.gameObject;

        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == "Tractor" && go.GetComponent<TruckController>() != null) return go;
            if (go.name == "Tractor" && go.transform.parent == null) return go;
        }
        return null;
    }

    public static GameObject FindPlayerTrailer()
    {
        GameObject tractor = FindPlayerTractor();
        if (tractor != null)
        {
            TruckController tc = tractor.GetComponent<TruckController>();
            if (tc != null && tc.TrailerRb != null) return tc.TrailerRb.gameObject;
        }

        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == "Trailer" && go.transform.parent == null) return go;
        }
        return null;
    }

    public void EnsurePlayableTruckAndCanvas()
    {
        LoadSprites();
        GameObject tractor = FindPlayerTractor();
        GameObject trailer = FindPlayerTrailer();
        if (tractor == null || trailer == null)
        {
            Vector3 defaultTractorPos = new Vector3(27f, 40f, 0f);
            Vector3 defaultTrailerPos = new Vector3(27f, 31.1f, 0f);
            BuildCompletePlayableTruck(defaultTractorPos, defaultTrailerPos, Quaternion.identity);
        }
        else
        {
            TruckSimulatorSetup.RebuildControlsCanvasInActiveScene();
        }

        // Hide UI Canvas in Edit Mode so huge 1920x1080 UI elements never obstruct Scene View
        HideCanvasInEditMode();
    }

    private void CreateBorderLine(string name, Transform parent, Vector3 pos, Vector2 spriteSize, Vector2 colSize, Vector2 colOffset)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent, false);
        line.transform.position = pos;

        SpriteRenderer sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = stripeSprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = spriteSize;
        sr.color = new Color(0.98f, 0.82f, 0.12f, 1.0f); // Bold safety yellow
        sr.sortingOrder = -5;

        // Physical collision barrier extending outside the asphalt to block driving into the void
        BoxCollider2D col = line.AddComponent<BoxCollider2D>();
        col.size = colSize;
        col.offset = colOffset;
        col.isTrigger = true;
    }

    public void ClearAllPlacedSlots()
    {
        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace != null)
        {
            Transform slotsContainer = workspace.transform.Find(SlotsContainerName);
            if (slotsContainer != null)
            {
                if (!EditorApplication.isPlaying)
                {
                    Undo.RegisterFullObjectHierarchyUndo(slotsContainer.gameObject, "Clear All Placed Slots");
                }
                for (int i = slotsContainer.childCount - 1; i >= 0; i--)
                {
                    if (EditorApplication.isPlaying)
                    {
                        Destroy(slotsContainer.GetChild(i).gameObject);
                    }
                    else
                    {
                        DestroyImmediate(slotsContainer.GetChild(i).gameObject);
                    }
                }
                SafeMarkSceneDirty();
                SceneView.RepaintAll();
            }
        }
    }

    #endregion

    #region Map Save & Load Management

    public void SaveCurrentMap(string customName = "")
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Режим игры активен",
                "Сохранение сцены в Unity невозможно во время активного режима Play Mode.\n\nПожалуйста, остановите режим игры (кнопка Play вверху Unity) и нажмите 'Сохранить карту'.",
                "Понятно");
            return;
        }

        if (!string.IsNullOrEmpty(customName))
        {
            mapName = customName;
        }

        if (string.IsNullOrWhiteSpace(mapName))
        {
            mapName = "MyParkingMap_1";
        }

        char[] invalids = Path.GetInvalidFileNameChars();
        string cleanName = string.Join("_", mapName.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrEmpty(cleanName)) cleanName = "MyParkingMap_1";

        EnsurePlayableTruckAndCanvas();
        DestroyGhostPreview();

        EnsureFolder(CustomMapsFolder);
        string scenePath = $"{CustomMapsFolder}/{cleanName}.unity";

        UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        
        bool success = EditorSceneManager.SaveScene(activeScene, scenePath);
        if (success)
        {
            AssetDatabase.Refresh();
            MyMapMenu.RegenerateMenu();
            Debug.Log($"<color=#55ff55>[MapBuilder] Успешно сохранена карта: {scenePath}</color>");
            ShowNotification(new GUIContent($"Карта '{cleanName}' сохранена!"));
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent($"Карта '{cleanName}' сохранена!"));
            }
        }
        else
        {
            EditorUtility.DisplayDialog("Ошибка сохранения", $"Не удалось сохранить карту по пути {scenePath}", "OK");
        }
    }

    public static void LoadAndEditMap(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
        {
            EditorUtility.DisplayDialog("Ошибка", "Файл карты не найден: " + scenePath, "OK");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            MapBuilderEditor window = GetWindow<MapBuilderEditor>("Map Builder");
            window.minSize = new Vector2(360, 560);
            window.Show();
            window.builderActive = true;
            window.mapName = Path.GetFileNameWithoutExtension(scenePath);
            window.EnsureCleanWorkPlane(clearExistingScene: false);
            window.SnapCursorToGrid();
            window.UpdateGhostPreview();
            window.FocusSceneView();
        }
    }

    #endregion

    #region GUI Window

    private Vector2 scrollPos;

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Map Builder (Широкие парковки 4.5м)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Интерактивный редактор парковок с жесткой сеткой 4.5м:\n" +
            "• [W / A / S / D] или [Стрелки] — перемещение ровно на 1 ячейку\n" +
            "• [C] — переключение объекта (Без трака / С траком / Старт трака)\n" +
            "• [R] — поворот на 90°\n" +
            "• [Enter / Space] — поставить объект\n" +
            "• [Delete / Backspace] — удалить слот под курсором\n" +
            "• [Клик мыши в Scene View] — мгновенно привязать курсор к ячейке",
            MessageType.Info);

        EditorGUILayout.Space(6);

        // Save Map Section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("💾 Сохранение карты:", EditorStyles.boldLabel);
        mapName = EditorGUILayout.TextField("Название карты", mapName);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("💾 Сохранить карту", GUILayout.Height(28)))
        {
            SaveCurrentMap();
        }
        if (GUILayout.Button("📂 Мои сохранённые карты", GUILayout.Height(28)))
        {
            SavedMapsWindow.Open();
        }
        EditorGUILayout.EndHorizontal();

        string currentScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (!string.IsNullOrEmpty(currentScenePath))
        {
            EditorGUILayout.LabelField("Файл: " + currentScenePath, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Map Dimensions Section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📐 Размер площадки (Карты):", EditorStyles.boldLabel);
        
        EditorGUI.BeginChangeCheck();
        float newH = EditorGUILayout.Slider("Высота карты (м) [ / ]", mapHeight, 25f, 120f);
        float newW = EditorGUILayout.Slider("Ширина карты (м)", mapWidth, 18f, 120f);
        if (EditorGUI.EndChangeCheck())
        {
            SetMapDimensions(newW, newH);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("[-] Высота ( [ )", GUILayout.Height(24)))
        {
            AdjustMapHeight(-5f);
        }
        if (GUILayout.Button("[+] Высота ( ] )", GUILayout.Height(24)))
        {
            AdjustMapHeight(+5f);
        }
        if (GUILayout.Button("[-] Ширина", GUILayout.Height(24)))
        {
            AdjustMapWidth(-4.5f);
        }
        if (GUILayout.Button("[+] Ширина", GUILayout.Height(24)))
        {
            AdjustMapWidth(+4.5f);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Shift All Map Objects Section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("✥ Сдвиг всех объектов карты (Шаг 4.5м):", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⬅ Влево (-4.5м)", GUILayout.Height(25)))
        {
            ShiftAllMapObjects(new Vector2(-SlotWidth, 0f));
        }
        if (GUILayout.Button("➡ Вправо (+4.5м)", GUILayout.Height(25)))
        {
            ShiftAllMapObjects(new Vector2(SlotWidth, 0f));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("⬇ Вниз (-4.5м)", GUILayout.Height(25)))
        {
            ShiftAllMapObjects(new Vector2(0f, -SlotWidth));
        }
        if (GUILayout.Button("⬆ Вверх (+4.5м)", GUILayout.Height(25)))
        {
            ShiftAllMapObjects(new Vector2(0f, SlotWidth));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Fixed Grid Info
        EditorGUILayout.LabelField("Сетка (Grid):", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Широкая сетка (4.5м) — Фиксированная", EditorStyles.miniBoldLabel);
        showVisualGrid = EditorGUILayout.Toggle("Показывать направляющие сетки", showVisualGrid);
        showControlsOverlay = EditorGUILayout.Toggle("Показывать педали и руль в Scene View", showControlsOverlay);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Текущий объект (Клавиша C):", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        currentObjectType = (ObjectType)EditorGUILayout.Popup((int)currentObjectType, ObjectTypeNames);
        if (EditorGUI.EndChangeCheck())
        {
            if (currentObjectType != ObjectType.TruckStartPoint)
            {
                SnapCursorToGrid();
            }
            UpdateGhostPreview();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Параметры положения и поворота:", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        cursorPosition = EditorGUILayout.Vector2Field("Координаты (м)", cursorPosition);
        currentRotation = EditorGUILayout.Slider("Поворот (0-360°)", currentRotation, 0f, 360f);
        if (EditorGUI.EndChangeCheck())
        {
            UpdateGhostPreview();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(4);
        autoAdvanceAfterPlacement = EditorGUILayout.Toggle("Автосдвиг к следующему краю", autoAdvanceAfterPlacement);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Действия:", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Установить (Enter)", GUILayout.Height(30)))
        {
            PlaceCurrentObject();
        }
        if (GUILayout.Button("Повернуть 45° (R)", GUILayout.Height(30)))
        {
            RotateCursor(45f);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Фокус на курсор (F)", GUILayout.Height(25)))
        {
            FocusSceneView();
        }
        if (GUILayout.Button("Создать чистую карту", GUILayout.Height(25)))
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog("Новая карта", "Очистить сцену и создать новую компактную карту парковки?", "Да, создать", "Отмена"))
                {
                    EnsureCleanWorkPlane(clearExistingScene: true);
                    cursorPosition = new Vector2(SlotWidth * 0.5f, SlotLength * 0.5f);
                    mapName = GetNextAvailableMapName();
                    SnapCursorToGrid();
                    FocusSceneView();
                }
            };
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Очистить все установленные слоты", GUILayout.Height(25)))
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorUtility.DisplayDialog("Очистка карты", "Вы уверены, что хотите удалить все установленные слоты?", "Да, удалить", "Отмена"))
                {
                    ClearAllPlacedSlots();
                }
            };
        }

        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region Scene View GUI & Key Interception

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!builderActive || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (ghostPreviewObj != null && ghostPreviewObj.activeSelf)
            {
                ghostPreviewObj.SetActive(false);
            }
            return;
        }

        Event e = Event.current;

        int defaultControlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(defaultControlID);

        float width = SlotWidth;
        float length = SlotLength;
        bool isRotatedHorizontal = Mathf.Approximately(Mathf.Abs(currentRotation), 90f) || Mathf.Approximately(Mathf.Abs(currentRotation), 270f);

        // Draw Visual World Grid in Scene View
        if (showVisualGrid)
        {
            DrawWorldGrid(width, length);
        }

        // Interactive 2D Position Handle for Scene View (Free Mouse Movement with Magnetic Snapping)
        EditorGUI.BeginChangeCheck();
        Vector3 curPos3 = new Vector3(cursorPosition.x, cursorPosition.y, 0f);
        Quaternion curRotQ = Quaternion.Euler(0f, 0f, currentRotation);

        Vector3 newPos = Handles.PositionHandle(curPos3, curRotQ);

        if (EditorGUI.EndChangeCheck())
        {
            Vector2 rawPos = new Vector2(newPos.x, newPos.y);
            cursorPosition = GetMagneticSnappedPosition(rawPos, currentRotation);
            UpdateGhostPreview();
            Repaint();
        }

        // Handle Mouse Drag & Click anywhere in Scene View without triggering Unity's selection box
        if (e.button == 0 && !e.alt && !e.control)
        {
            if (e.type == EventType.MouseDown && (GUIUtility.hotControl == 0 || GUIUtility.hotControl == defaultControlID))
            {
                GUIUtility.hotControl = defaultControlID;
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector2 rawPos = new Vector2(ray.origin.x, ray.origin.y);
                cursorPosition = GetMagneticSnappedPosition(rawPos, currentRotation);

                UpdateGhostPreview();
                Repaint();
                sceneView.Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == defaultControlID)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector2 rawPos = new Vector2(ray.origin.x, ray.origin.y);
                cursorPosition = GetMagneticSnappedPosition(rawPos, currentRotation);

                UpdateGhostPreview();
                Repaint();
                sceneView.Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == defaultControlID)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        // Handle Keyboard Shortcuts
        if (e.type == EventType.KeyDown)
        {
            bool handled = false;
            
            if (currentObjectType == ObjectType.TruckStartPoint)
            {
                // Fine-grained keyboard control for Truck Start point (0.5m / 2m / 0.1m)
                float step = e.shift ? 2.0f : (e.alt || e.control ? 0.1f : 0.5f);
                
                switch (e.keyCode)
                {
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                        MoveCursor(new Vector2(0f, step));
                        handled = true;
                        break;
                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        MoveCursor(new Vector2(0f, -step));
                        handled = true;
                        break;
                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        MoveCursor(new Vector2(-step, 0f));
                        handled = true;
                        break;
                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        MoveCursor(new Vector2(step, 0f));
                        handled = true;
                        break;

                    case KeyCode.C:
                        CycleObjectType();
                        handled = true;
                        break;

                    case KeyCode.R:
                        RotateCursor(e.shift ? 45f : 15f);
                        handled = true;
                        break;

                    case KeyCode.G:
                        SnapCursorToGrid();
                        UpdateGhostPreview();
                        handled = true;
                        break;

                    case KeyCode.F:
                        FocusSceneView();
                        handled = true;
                        break;

                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Space:
                        PlaceCurrentObject();
                        handled = true;
                        break;

                    case KeyCode.Delete:
                    case KeyCode.Backspace:
                        DeleteSlotUnderCursor();
                        handled = true;
                        break;

                    case KeyCode.LeftBracket:
                        AdjustMapHeight(e.shift ? -10f : -5f);
                        handled = true;
                        break;

                    case KeyCode.RightBracket:
                        AdjustMapHeight(e.shift ? +10f : +5f);
                        handled = true;
                        break;
                }
            }
            if (e.control && (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow || e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.RightArrow))
            {
                float shiftStep = SlotWidth; // 4.5m
                if (e.shift) shiftStep *= 2f;
                switch (e.keyCode)
                {
                    case KeyCode.UpArrow:
                        ShiftAllMapObjects(new Vector2(0f, shiftStep));
                        handled = true;
                        break;
                    case KeyCode.DownArrow:
                        ShiftAllMapObjects(new Vector2(0f, -shiftStep));
                        handled = true;
                        break;
                    case KeyCode.LeftArrow:
                        ShiftAllMapObjects(new Vector2(-shiftStep, 0f));
                        handled = true;
                        break;
                    case KeyCode.RightArrow:
                        ShiftAllMapObjects(new Vector2(shiftStep, 0f));
                        handled = true;
                        break;
                }
            }
            else
            {
                Vector3 rightDir = Quaternion.Euler(0f, 0f, currentRotation) * Vector3.right;
                Vector3 forwardDir = Quaternion.Euler(0f, 0f, currentRotation) * Vector3.up;

                float step = SlotWidth; // 4.5m step for W, S, A, D

                if (e.shift)
                {
                    step *= 2f; // 9.0m
                }
                else if (e.alt || e.control)
                {
                    step = 0.5f; // 0.5m fine step
                }

                switch (e.keyCode)
                {
                    case KeyCode.W:
                    case KeyCode.UpArrow:
                        cursorPosition += new Vector2(forwardDir.x, forwardDir.y) * step;
                        UpdateGhostPreview();
                        handled = true;
                        break;
                    case KeyCode.S:
                    case KeyCode.DownArrow:
                        cursorPosition -= new Vector2(forwardDir.x, forwardDir.y) * step;
                        UpdateGhostPreview();
                        handled = true;
                        break;
                    case KeyCode.A:
                    case KeyCode.LeftArrow:
                        cursorPosition -= new Vector2(rightDir.x, rightDir.y) * step;
                        UpdateGhostPreview();
                        handled = true;
                        break;
                    case KeyCode.D:
                    case KeyCode.RightArrow:
                        cursorPosition += new Vector2(rightDir.x, rightDir.y) * step;
                        UpdateGhostPreview();
                        handled = true;
                        break;

                    case KeyCode.C:
                        CycleObjectType();
                        handled = true;
                        break;

                    case KeyCode.R:
                        RotateCursor(e.shift ? 15f : (e.alt || e.control ? 5f : 45f));
                        handled = true;
                        break;

                    case KeyCode.G:
                        SnapCursorToGrid();
                        UpdateGhostPreview();
                        handled = true;
                        break;

                    case KeyCode.F:
                        FocusSceneView();
                        handled = true;
                        break;

                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Space:
                        PlaceCurrentObject();
                        handled = true;
                        break;

                    case KeyCode.Delete:
                    case KeyCode.Backspace:
                        DeleteSlotUnderCursor();
                        handled = true;
                        break;

                    case KeyCode.LeftBracket:
                        AdjustMapHeight(e.shift ? -10f : -5f);
                        handled = true;
                        break;

                    case KeyCode.RightBracket:
                        AdjustMapHeight(e.shift ? +10f : +5f);
                        handled = true;
                        break;
                }
            }

            if (handled)
            {
                e.Use();
                Repaint();
                sceneView.Repaint();
            }
        }

        // Ensure ghost preview is in correct position & rotation
        if (ghostPreviewObj != null)
        {
            if (!ghostPreviewObj.activeSelf) ghostPreviewObj.SetActive(true);
            ghostPreviewObj.transform.position = new Vector3(cursorPosition.x, cursorPosition.y, 0f);
            ghostPreviewObj.transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);
        }

        // Draw HUD Overlay in Scene View
        DrawSceneHUD(sceneView);
    }

    private void DrawWorldGrid(float cellWidth, float cellLength)
    {
        float maxCellsX = 12;
        float maxX = maxCellsX * cellWidth; // 54m

        Handles.color = new Color(0.35f, 0.65f, 0.95f, 0.18f);

        // Subtle column guides only for the parking row (0 to 22m)
        for (float x = 0; x <= maxX + 0.01f; x += cellWidth)
        {
            Handles.DrawLine(new Vector3(x, 0f, 0f), new Vector3(x, cellLength, 0f));
        }

        Handles.DrawLine(new Vector3(0f, cellLength, 0f), new Vector3(maxX, cellLength, 0f));
    }

    private void MoveCursor(Vector2 delta)
    {
        cursorPosition += delta;
        if (currentObjectType != ObjectType.TruckStartPoint)
        {
            SnapCursorToGrid();
        }
        UpdateGhostPreview();
    }

    private void RotateCursor(float angleDelta)
    {
        currentRotation = (currentRotation + angleDelta) % 360f;
        if (currentRotation < 0f) currentRotation += 360f;
        UpdateGhostPreview();
    }

    private void CycleObjectType()
    {
        currentObjectType = (ObjectType)(((int)currentObjectType + 1) % 4);
        if (currentObjectType != ObjectType.TruckStartPoint)
        {
            SnapCursorToGrid();
        }
        UpdateGhostPreview();
    }

    private void DrawSceneHUD(SceneView sceneView)
    {
        Handles.BeginGUI();
        
        Rect boxRect = new Rect(14, 14, 350, 195);
        Color oldColor = GUI.color;
        GUI.color = new Color(0.08f, 0.08f, 0.10f, 0.94f);
        GUI.Box(boxRect, GUIContent.none);
        GUI.color = Color.white;

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);
        headerStyle.fontSize = 13;
        GUI.Label(new Rect(24, 18, 330, 22), "🛠 MAP BUILDER (" + mapName + ")", headerStyle);

        GUIStyle textStyle = new GUIStyle(EditorStyles.label);
        textStyle.normal.textColor = Color.white;
        textStyle.fontSize = 12;

        if (currentObjectType == ObjectType.TruckStartPoint)
        {
            GUI.Label(new Rect(24, 40, 330, 20), "Сетка: <color=#55ff55>Свободное размещение (Без сетки)</color>", textStyle);
            GUI.Label(new Rect(24, 60, 330, 20), "Объект: " + ObjectTypeNames[(int)currentObjectType], textStyle);
            GUI.Label(new Rect(24, 80, 330, 20), $"Старт: X: {cursorPosition.x:F2}м | Y: {cursorPosition.y:F2}м | {currentRotation:F1}°", textStyle);

            GUIStyle helpStyle = new GUIStyle(EditorStyles.miniLabel);
            helpStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(24, 104, 330, 18), "[WASD/Стрелки] Шаг 0.5м (Shift: 2м, Alt: 0.1м) | [R] 15°", helpStyle);
            GUI.Label(new Rect(24, 122, 330, 18), "[Мышь] Клик в точку / тащите за стрелки гизмо", helpStyle);
            GUI.Label(new Rect(24, 140, 330, 18), "[Enter / Space] Применить старт трака | [C] Сменить", helpStyle);
        }
        else
        {
            GUI.Label(new Rect(24, 40, 330, 20), "Мышь: <color=#55ff55>Свободное перемещение</color> | Клавиши: <color=#55ffff>4.5м</color>", textStyle);
            GUI.Label(new Rect(24, 60, 330, 20), "Объект: " + ObjectTypeNames[(int)currentObjectType], textStyle);
            GUI.Label(new Rect(24, 80, 330, 20), "Позиция: X: " + cursorPosition.x.ToString("F2") + "м | Y: " + cursorPosition.y.ToString("F2") + "м | " + currentRotation.ToString("F0") + "°", textStyle);

            GUIStyle helpStyle = new GUIStyle(EditorStyles.miniLabel);
            helpStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(24, 104, 330, 18), "[Мышь] Свободное таскание + авто-прилипание к слотам", helpStyle);
            GUI.Label(new Rect(24, 122, 330, 18), "[R] Поворот 45° (Shift: 15°) | [WASD] Шаг 4.5м | [Enter] Ставить", helpStyle);
            GUI.Label(new Rect(24, 140, 330, 18), "[Ctrl+Стрелки] Сдвиг всей карты 4.5м | [Del] Удалить", helpStyle);
        }

        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f, 0.95f);
        if (GUI.Button(new Rect(24, 162, 145, 24), "💾 Сохранить карту"))
        {
            SaveCurrentMap();
        }
        GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 0.95f);
        if (GUI.Button(new Rect(175, 162, 175, 24), "📂 Список карт"))
        {
            SavedMapsWindow.Open();
        }

        float startX = 375;
        float btnY = 14;
        float btnH = 28;

        // Height quick adjuster in HUD
        GUI.backgroundColor = new Color(0.95f, 0.75f, 0.2f, 0.95f);
        if (GUI.Button(new Rect(startX + 580, btnY, 42, btnH), "[-H]"))
        {
            AdjustMapHeight(-5f);
        }
        if (GUI.Button(new Rect(startX + 626, btnY, 42, btnH), "[+H]"))
        {
            AdjustMapHeight(+5f);
        }

        // Shift All Map Objects quick buttons in HUD
        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.95f, 0.95f);
        if (GUI.Button(new Rect(startX + 672, btnY, 34, btnH), "⬅"))
        {
            ShiftAllMapObjects(new Vector2(-SlotWidth, 0f));
        }
        if (GUI.Button(new Rect(startX + 708, btnY, 34, btnH), "➡"))
        {
            ShiftAllMapObjects(new Vector2(SlotWidth, 0f));
        }
        if (GUI.Button(new Rect(startX + 744, btnY, 34, btnH), "⬇"))
        {
            ShiftAllMapObjects(new Vector2(0f, -SlotWidth));
        }
        if (GUI.Button(new Rect(startX + 780, btnY, 34, btnH), "⬆"))
        {
            ShiftAllMapObjects(new Vector2(0f, SlotWidth));
        }

        bool isEmpty = currentObjectType == ObjectType.StandardEmpty;
        GUI.backgroundColor = isEmpty ? new Color(0.3f, 0.85f, 0.3f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.85f);
        if (GUI.Button(new Rect(startX, btnY, 95, btnH), "🅿 1. Пусто"))
        {
            currentObjectType = ObjectType.StandardEmpty;
            SnapCursorToGrid();
            UpdateGhostPreview();
        }

        bool isParked = currentObjectType == ObjectType.StandardParked;
        GUI.backgroundColor = isParked ? new Color(1f, 0.45f, 0.45f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.85f);
        if (GUI.Button(new Rect(startX + 98, btnY, 95, btnH), "🚛 2. Трак"))
        {
            currentObjectType = ObjectType.StandardParked;
            SnapCursorToGrid();
            UpdateGhostPreview();
        }

        bool isTarget = currentObjectType == ObjectType.TargetParking;
        GUI.backgroundColor = isTarget ? new Color(1f, 0.85f, 0.05f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.85f);
        if (GUI.Button(new Rect(startX + 196, btnY, 85, btnH), "🎯 3. Цель"))
        {
            currentObjectType = ObjectType.TargetParking;
            SnapCursorToGrid();
            UpdateGhostPreview();
        }

        bool isStart = currentObjectType == ObjectType.TruckStartPoint;
        GUI.backgroundColor = isStart ? new Color(0.2f, 0.9f, 1f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.85f);
        if (GUI.Button(new Rect(startX + 284, btnY, 115, btnH), "🏁 4. Старт трака"))
        {
            currentObjectType = ObjectType.TruckStartPoint;
            UpdateGhostPreview();
        }

        GUI.backgroundColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        if (GUI.Button(new Rect(startX + 403, btnY, 78, btnH), "⟳ 45°"))
        {
            RotateCursor(45f);
        }

        GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f, 0.95f);
        if (GUI.Button(new Rect(startX + 485, btnY, 90, btnH), "✓ Применить"))
        {
            PlaceCurrentObject();
        }

        GUI.backgroundColor = Color.white;

        Handles.EndGUI();

        float slotWidth = SlotWidth;
        float slotLength = (currentObjectType == ObjectType.TargetParking) ? 26.0f : SlotLength;
        Vector3 size = new Vector3(slotWidth, slotLength, 0f);
        
        Matrix4x4 origMatrix = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(new Vector3(cursorPosition.x, cursorPosition.y, 0f), Quaternion.Euler(0f, 0f, currentRotation), Vector3.one);
        
        Color boxOutlineColor;
        Color boxFillColor;

        if (currentObjectType == ObjectType.TruckStartPoint)
        {
            boxOutlineColor = new Color(0.2f, 0.95f, 0.4f, 0.95f);
            boxFillColor = new Color(0.2f, 0.95f, 0.4f, 0.15f);
        }
        else if (currentObjectType == ObjectType.TargetParking)
        {
            boxOutlineColor = new Color(1f, 0.85f, 0.05f, 0.98f);
            boxFillColor = new Color(1f, 0.85f, 0.05f, 0.22f);
        }
        else
        {
            boxOutlineColor = new Color(0.2f, 0.9f, 1f, 0.95f);
            boxFillColor = new Color(0.2f, 0.85f, 1f, 0.12f);
        }

        Handles.DrawSolidRectangleWithOutline(new Vector3[]
        {
            new Vector3(-slotWidth * 0.5f, -slotLength * 0.5f, 0f),
            new Vector3(-slotWidth * 0.5f,  slotLength * 0.5f, 0f),
            new Vector3( slotWidth * 0.5f,  slotLength * 0.5f, 0f),
            new Vector3( slotWidth * 0.5f, -slotLength * 0.5f, 0f)
        }, boxFillColor, boxOutlineColor);

        Handles.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        Handles.DrawLine(new Vector3(0f, -slotLength * 0.5f - 2.0f, 0f), new Vector3(0f, -slotLength * 0.5f + 1.0f, 0f));
        Handles.DrawLine(new Vector3(0f, -slotLength * 0.5f + 1.0f, 0f), new Vector3(-0.6f, -slotLength * 0.5f - 0.2f, 0f));
        Handles.DrawLine(new Vector3(0f, -slotLength * 0.5f + 1.0f, 0f), new Vector3( 0.6f, -slotLength * 0.5f - 0.2f, 0f));

        Handles.matrix = origMatrix;
    }

    #endregion

    #region Ghost Preview Management

    private void UpdateGhostPreview(bool forceRebuild = false)
    {
        if (!builderActive || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            DestroyGhostPreview();
            return;
        }

        if (ghostPreviewObj == null)
        {
            ghostPreviewObj = GameObject.Find(GhostPreviewName);
            if (ghostPreviewObj == null)
            {
                ghostPreviewObj = new GameObject(GhostPreviewName);
                ghostPreviewObj.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
            }
        }

        // Only rebuild child GameObjects when the object type changes or forced
        if (forceRebuild || lastBuiltPreviewType != currentObjectType || ghostPreviewObj.transform.childCount == 0)
        {
            for (int i = ghostPreviewObj.transform.childCount - 1; i >= 0; i--)
            {
                if (EditorApplication.isPlaying)
                {
                    Destroy(ghostPreviewObj.transform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(ghostPreviewObj.transform.GetChild(i).gameObject);
                }
            }

            BuildObjectHierarchy(ghostPreviewObj.transform, currentObjectType, isPreview: true);
            lastBuiltPreviewType = currentObjectType;
        }

        // Blazing fast position and rotation update: zero GC allocation, 120+ FPS
        ghostPreviewObj.transform.position = new Vector3(cursorPosition.x, cursorPosition.y, 0f);
        ghostPreviewObj.transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);
    }

    private void DestroyGhostPreview()
    {
        if (ghostPreviewObj != null)
        {
            if (EditorApplication.isPlaying)
            {
                Destroy(ghostPreviewObj);
            }
            else
            {
                DestroyImmediate(ghostPreviewObj);
            }
            ghostPreviewObj = null;
        lastBuiltPreviewType = (ObjectType)(-1);
        }
        GameObject stray = GameObject.Find(GhostPreviewName);
        if (stray != null)
        {
            if (EditorApplication.isPlaying)
            {
                Destroy(stray);
            }
            else
            {
                DestroyImmediate(stray);
            }
        }
    }

    #endregion

    #region Object Placement & Truck Spawn Point Handling

    public void PlaceCurrentObject()
    {
        EnsureCleanWorkPlane(clearExistingScene: false);

        if (currentObjectType == ObjectType.TruckStartPoint)
        {
            PlaceOrRelocateTruckStart();
            return;
        }

        if (currentObjectType == ObjectType.TargetParking)
        {
            PlaceOrRelocateTargetParking();
            return;
        }

        GameObject workspace = GameObject.Find(WorkspaceRootName);
        Transform container = workspace != null ? workspace.transform.Find(SlotsContainerName) : null;

        // Ensure only ONE object exists at this grid cell: remove existing slot at cursor position
        if (container != null)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                float dist = Vector2.Distance(new Vector2(child.position.x, child.position.y), cursorPosition);
                if (dist < 2.2f)
                {
                    SafeDestroyObject(child.gameObject);
                }
            }
        }

        string slotName = currentObjectType == ObjectType.StandardEmpty ? "Stall_Standard_Empty" : "Stall_Standard_Parked";
        GameObject slotObj = new GameObject(slotName);
        
        if (container != null)
        {
            slotObj.transform.SetParent(container, false);
        }
        slotObj.transform.position = new Vector3(cursorPosition.x, cursorPosition.y, 0f);
        slotObj.transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);

        BuildObjectHierarchy(slotObj.transform, currentObjectType, isPreview: false);

        SafeRegisterCreatedObjectUndo(slotObj, "Place " + slotName);
        SafeMarkSceneDirty();

        if (autoAdvanceAfterPlacement)
        {
            Vector3 rightDir = Quaternion.Euler(0f, 0f, currentRotation) * Vector3.right;
            cursorPosition += new Vector2(rightDir.x, rightDir.y) * SlotWidth;
            UpdateGhostPreview();
        }

        SceneView.RepaintAll();
    }

    private void PlaceOrRelocateTargetParking()
    {
        GameObject workspace = GameObject.Find(WorkspaceRootName);
        Transform container = workspace != null ? workspace.transform.Find(SlotsContainerName) : null;

        // Ensure strictly ONLY ONE TargetParkingSlot exists on the map
        if (container != null)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (child.name == "TargetParkingSlot" || child.GetComponent<ParkingTargetZone>() != null)
                {
                    SafeDestroyObject(child.gameObject);
                }
            }
        }
        GameObject strayTarget = GameObject.Find("TargetParkingSlot");
        if (strayTarget != null) SafeDestroyObject(strayTarget);

        // Remove any slot existing at the target cursor cell
        if (container != null)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                float dist = Vector2.Distance(new Vector2(child.position.x, child.position.y), cursorPosition);
                if (dist < 2.2f)
                {
                    SafeDestroyObject(child.gameObject);
                }
            }
        }

        GameObject slotObj = new GameObject("TargetParkingSlot");
        if (container != null)
        {
            slotObj.transform.SetParent(container, false);
        }
        slotObj.transform.position = new Vector3(cursorPosition.x, cursorPosition.y, 0f);
        slotObj.transform.rotation = Quaternion.Euler(0f, 0f, currentRotation);

        BuildObjectHierarchy(slotObj.transform, ObjectType.TargetParking, isPreview: false);

        SafeRegisterCreatedObjectUndo(slotObj, "Place Target Parking Slot");
        SafeMarkSceneDirty();

        if (autoAdvanceAfterPlacement)
        {
            Vector3 rightDir = Quaternion.Euler(0f, 0f, currentRotation) * Vector3.right;
            cursorPosition += new Vector2(rightDir.x, rightDir.y) * SlotWidth;
            UpdateGhostPreview();
        }

        SceneView.RepaintAll();
        Debug.Log($"<color=#ffd700>[MapBuilder] Установлено единственное Целевое место парковки (4.5м) в ({cursorPosition.x:F1}, {cursorPosition.y:F1})</color>");
    }

    private void PlaceOrRelocateTruckStart()
    {
        Quaternion rot = Quaternion.Euler(0f, 0f, currentRotation);
        Vector3 tractorPos = new Vector3(cursorPosition.x, cursorPosition.y, 0f);
        
        // Distance between tractor and trailer centers: hitch (-2.0m) + kingpin (6.9m) = 8.9m
        Vector3 trailerOffset = rot * new Vector3(0f, -8.9f, 0f);
        Vector3 trailerPos = tractorPos + trailerOffset;

        GameObject tractor = FindPlayerTractor();
        GameObject trailer = FindPlayerTrailer();

        if (tractor != null && trailer != null)
        {
            if (!EditorApplication.isPlaying)
            {
                Undo.RecordObject(tractor.transform, "Move Truck Start");
                Undo.RecordObject(trailer.transform, "Move Truck Start");
            }

            tractor.transform.position = tractorPos;
            tractor.transform.rotation = rot;

            trailer.transform.position = trailerPos;
            trailer.transform.rotation = rot;

            Rigidbody2D trRb = tractor.GetComponent<Rigidbody2D>();
            if (trRb != null) { trRb.linearVelocity = Vector2.zero; trRb.angularVelocity = 0f; }

            Rigidbody2D tlRb = trailer.GetComponent<Rigidbody2D>();
            if (tlRb != null) { tlRb.linearVelocity = Vector2.zero; tlRb.angularVelocity = 0f; }

            TruckController tc = tractor.GetComponent<TruckController>();
            if (tc != null)
            {
                tc.SetupTrailer(tlRb);
            }

            if (Camera.main != null)
            {
                Camera.main.transform.position = new Vector3(tractorPos.x, tractorPos.y, -10f);
            }

            SafeMarkSceneDirty();
            Debug.Log($"<color=#55ff55>[MapBuilder] Перемещен старт игрового грузовика в ({tractorPos.x:F1}, {tractorPos.y:F1}) с углом {currentRotation:F0}°</color>");
        }
        else
        {
            BuildCompletePlayableTruck(tractorPos, trailerPos, rot);
        }

        SceneView.RepaintAll();
    }

    public void BuildCompletePlayableTruck(Vector3 tractorPos, Vector3 trailerPos, Quaternion rot)
    {
        LoadSprites();

        // Clean existing player partials only
        GameObject oldTractor = FindPlayerTractor();
        if (oldTractor != null) SafeDestroyObject(oldTractor);
        GameObject oldTrailer = FindPlayerTrailer();
        if (oldTrailer != null) SafeDestroyObject(oldTrailer);

        // Ensure EventSystem & Controls Canvas
        TruckSimulatorSetup.RebuildControlsCanvasInActiveScene();

        Sprite playerSprite = tractorHDSprite != null ? tractorHDSprite : tractorSprite;

        // 1. Tractor
        GameObject tractor = new GameObject("Tractor");
        tractor.transform.position = tractorPos;
        tractor.transform.rotation = rot;
        tractor.transform.localScale = Vector3.one;

        SpriteRenderer tractorSr = tractor.AddComponent<SpriteRenderer>();
        tractorSr.sprite = playerSprite;
        tractorSr.sortingOrder = 8;

        BoxCollider2D tractorCollider = tractor.AddComponent<BoxCollider2D>();
        tractorCollider.size = new Vector2(2.55f, 8.2f);

        Rigidbody2D tractorRb = tractor.AddComponent<Rigidbody2D>();
        tractorRb.bodyType = RigidbodyType2D.Kinematic;
        tractorRb.useFullKinematicContacts = true;

        GameObject frontLeftWheel = CreateWheelChild("FrontLeftWheel", tractor.transform, new Vector3(-1.08f, 2.8f, 0f), wheelSprite);
        GameObject frontRightWheel = CreateWheelChild("FrontRightWheel", tractor.transform, new Vector3(1.08f, 2.8f, 0f), wheelSprite);
        CreateWheelChild("RearLeftWheel1", tractor.transform, new Vector3(-1.08f, -2.25f, 0f), wheelSprite);
        CreateWheelChild("RearRightWheel1", tractor.transform, new Vector3(1.08f, -2.25f, 0f), wheelSprite);
        CreateWheelChild("RearLeftWheel2", tractor.transform, new Vector3(-1.08f, -3.35f, 0f), wheelSprite);
        CreateWheelChild("RearRightWheel2", tractor.transform, new Vector3(1.08f, -3.35f, 0f), wheelSprite);

        tractor.AddComponent<TruckCollisionDetector>();
        TruckController truckController = tractor.AddComponent<TruckController>();
        truckController.SetupWheelReferences(frontLeftWheel.transform, frontRightWheel.transform);

        // 2. Trailer
        GameObject trailer = new GameObject("Trailer");
        trailer.transform.position = trailerPos;
        trailer.transform.rotation = rot;
        trailer.transform.localScale = Vector3.one;

        SpriteRenderer trailerSr = trailer.AddComponent<SpriteRenderer>();
        trailerSr.sprite = trailerSprite;
        trailerSr.sortingOrder = 10;

        BoxCollider2D trailerCollider = trailer.AddComponent<BoxCollider2D>();
        trailerCollider.size = new Vector2(2.58f, 15.9f);

        Rigidbody2D trailerRb = trailer.AddComponent<Rigidbody2D>();
        trailerRb.bodyType = RigidbodyType2D.Kinematic;
        trailerRb.useFullKinematicContacts = true;

        CreateWheelChild("TrailerRearLeft1", trailer.transform, new Vector3(-1.08f, -5.05f, 0f), wheelSprite);
        CreateWheelChild("TrailerRearRight1", trailer.transform, new Vector3(1.08f, -5.05f, 0f), wheelSprite);
        CreateWheelChild("TrailerRearLeft2", trailer.transform, new Vector3(-1.08f, -6.15f, 0f), wheelSprite);
        CreateWheelChild("TrailerRearRight2", trailer.transform, new Vector3(1.08f, -6.15f, 0f), wheelSprite);

        trailer.AddComponent<TruckCollisionDetector>();
        truckController.SetupTrailer(trailerRb);
        tractor.AddComponent<TruckGuideLines>();

        HingeJoint2D hinge = tractor.AddComponent<HingeJoint2D>();
        hinge.connectedBody = trailerRb;
        hinge.anchor = new Vector2(0f, -2.8f);
        hinge.autoConfigureConnectedAnchor = false;
        hinge.connectedAnchor = new Vector2(0f, 6.9f);
        hinge.enableCollision = true;
        hinge.useLimits = false;

        // 3. Camera Follow
        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 16.0f;
            Camera.main.transform.position = new Vector3(tractorPos.x, tractorPos.y, -10f);
            Camera.main.transform.rotation = Quaternion.identity;

            CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow == null)
            {
                camFollow = Camera.main.gameObject.AddComponent<CameraFollow>();
            }
            camFollow.SetupTargets(tractor.transform, trailer.transform);
        }

        SafeRegisterCreatedObjectUndo(tractor, "Create Tractor");
        SafeRegisterCreatedObjectUndo(trailer, "Create Trailer");
        SafeMarkSceneDirty();

        Debug.Log($"[MapBuilder] Built complete playable Truck at ({tractorPos.x:F1}, {tractorPos.y:F1}) at {currentRotation:F0}°");
    }

    private static GameObject CreateWheelChild(string name, Transform parent, Vector3 localPos, Sprite sprite)
    {
        GameObject wheel = new GameObject(name);
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPos;
        wheel.transform.localRotation = Quaternion.identity;
        wheel.transform.localScale = Vector3.one;

        SpriteRenderer sr = wheel.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 7;
        return wheel;
    }

    private void DeleteSlotUnderCursor()
    {
        GameObject workspace = GameObject.Find(WorkspaceRootName);
        if (workspace == null) return;
        Transform container = workspace.transform.Find(SlotsContainerName);
        if (container == null) return;

        bool destroyedAny = false;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            float dist = Vector2.Distance(new Vector2(child.position.x, child.position.y), cursorPosition);
            if (dist < 2.5f)
            {
                SafeDestroyObject(child.gameObject);
                destroyedAny = true;
            }
        }

        if (destroyedAny)
        {
            SafeMarkSceneDirty();
            SceneView.RepaintAll();
        }
    }

    private void BuildObjectHierarchy(Transform parent, ObjectType type, bool isPreview)
    {
        bool isTargetParking = (type == ObjectType.TargetParking);
        float width = SlotWidth;
        float length = isTargetParking ? 26.0f : SlotLength;
        float halfWidth = width * 0.5f;
        float stripeThickness = 0.18f;

        int baseSortOrder = isPreview ? 40 : 2;
        int truckSortOrder = isPreview ? 50 : 10;

        Color stripeColor = isPreview ? new Color(0.4f, 0.9f, 1f, 0.75f) : Color.white;
        Color truckColor = isPreview ? new Color(0.7f, 0.9f, 1f, 0.65f) : Color.white;

        if (type == ObjectType.TruckStartPoint)
        {
            // Preview of Player Truck Spawn
            GameObject trailer = new GameObject("Preview_Trailer");
            trailer.transform.SetParent(parent, false);
            trailer.transform.localPosition = new Vector3(0f, -8.9f, 0f);
            trailer.transform.localRotation = Quaternion.identity;
            SpriteRenderer srTrailer = trailer.AddComponent<SpriteRenderer>();
            srTrailer.sprite = trailerSprite;
            srTrailer.color = new Color(0.3f, 1f, 0.5f, 0.7f);
            srTrailer.sortingOrder = truckSortOrder;

            GameObject tractor = new GameObject("Preview_Tractor");
            tractor.transform.SetParent(parent, false);
            tractor.transform.localPosition = Vector3.zero;
            tractor.transform.localRotation = Quaternion.identity;
            SpriteRenderer srTractor = tractor.AddComponent<SpriteRenderer>();
            srTractor.sprite = tractorHDSprite != null ? tractorHDSprite : tractorSprite;
            srTractor.color = new Color(0.3f, 1f, 0.5f, 0.75f);
            srTractor.sortingOrder = truckSortOrder + 1;
            return;
        }

        if (isTargetParking)
        {
            stripeThickness = 0.36f; // Bold yellow stripe
            stripeColor = isPreview ? new Color(1.0f, 0.85f, 0.05f, 0.85f) : new Color(1.0f, 0.85f, 0.05f, 1.0f);
        }

        // 1. Left Stripe
        GameObject leftStripe = new GameObject("StallLine_Left");
        leftStripe.transform.SetParent(parent, false);
        leftStripe.transform.localPosition = new Vector3(-halfWidth, 0f, 0f);
        leftStripe.transform.localRotation = Quaternion.identity;
        SpriteRenderer srLeft = leftStripe.AddComponent<SpriteRenderer>();
        srLeft.sprite = stripeSprite;
        srLeft.drawMode = SpriteDrawMode.Tiled;
        srLeft.size = new Vector2(stripeThickness, length);
        srLeft.color = stripeColor;
        srLeft.sortingOrder = isTargetParking ? (baseSortOrder + 1) : baseSortOrder;

        // 2. Right Stripe
        GameObject rightStripe = new GameObject("StallLine_Right");
        rightStripe.transform.SetParent(parent, false);
        rightStripe.transform.localPosition = new Vector3(halfWidth, 0f, 0f);
        rightStripe.transform.localRotation = Quaternion.identity;
        SpriteRenderer srRight = rightStripe.AddComponent<SpriteRenderer>();
        srRight.sprite = stripeSprite;
        srRight.drawMode = SpriteDrawMode.Tiled;
        srRight.size = new Vector2(stripeThickness, length);
        srRight.color = stripeColor;
        srRight.sortingOrder = isTargetParking ? (baseSortOrder + 1) : baseSortOrder;

        // 3. Back Cap Stripe
        GameObject backStripe = new GameObject("StallLine_Back");
        backStripe.transform.SetParent(parent, false);
        backStripe.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
        backStripe.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        SpriteRenderer srBack = backStripe.AddComponent<SpriteRenderer>();
        srBack.sprite = stripeSprite;
        srBack.drawMode = SpriteDrawMode.Tiled;
        srBack.size = new Vector2(stripeThickness, width);
        srBack.color = stripeColor;
        srBack.sortingOrder = isTargetParking ? (baseSortOrder + 1) : baseSortOrder;

        // 4. Target Parking Big Bold Yellow Arrow & Win Trigger
        if (isTargetParking)
        {
            if (yellowArrowSprite != null)
            {
                GameObject arrow = new GameObject("TargetParking_YellowArrow");
                arrow.transform.SetParent(parent, false);
                arrow.transform.localPosition = new Vector3(0f, -2.0f, 0f);
                arrow.transform.localRotation = Quaternion.identity;
                arrow.transform.localScale = new Vector3(2.8f, 2.8f, 1f);

                SpriteRenderer srArrow = arrow.AddComponent<SpriteRenderer>();
                srArrow.sprite = yellowArrowSprite;
                srArrow.color = isPreview ? new Color(1f, 0.85f, 0.05f, 0.85f) : new Color(1f, 0.85f, 0.05f, 1.0f);
                srArrow.sortingOrder = baseSortOrder;
            }

            if (!isPreview)
            {
                ParkingTargetZone zone = parent.gameObject.GetComponent<ParkingTargetZone>();
                if (zone == null) parent.gameObject.AddComponent<ParkingTargetZone>();
            }
        }

        // 5. Parked Truck (Centered in 26m slot)
        if (type == ObjectType.StandardParked)
        {
            GameObject trailer = new GameObject("Trailer");
            trailer.transform.SetParent(parent, false);
            trailer.transform.localPosition = new Vector3(0f, -2.9f, 0f);
            trailer.transform.localRotation = Quaternion.identity;
            SpriteRenderer srTrailer = trailer.AddComponent<SpriteRenderer>();
            srTrailer.sprite = trailerSprite;
            srTrailer.color = truckColor;
            srTrailer.sortingOrder = truckSortOrder;

            if (!isPreview)
            {
                BoxCollider2D colTrailer = trailer.AddComponent<BoxCollider2D>();
                colTrailer.size = new Vector2(2.58f, 15.9f);
                colTrailer.isTrigger = true;
            }

            GameObject tractor = new GameObject("Tractor");
            tractor.transform.SetParent(parent, false);
            tractor.transform.localPosition = new Vector3(0f, 6.8f, 0f);
            tractor.transform.localRotation = Quaternion.identity;
            SpriteRenderer srTractor = tractor.AddComponent<SpriteRenderer>();
            srTractor.sprite = tractorSprite;
            srTractor.color = truckColor;
            srTractor.sortingOrder = truckSortOrder;

            if (!isPreview)
            {
                BoxCollider2D colTractor = tractor.AddComponent<BoxCollider2D>();
                colTractor.size = new Vector2(2.55f, 8.2f);
                colTractor.isTrigger = true;
            }
        }
    }

    #endregion
}

public class SavedMapsWindow : EditorWindow
{
    private Vector2 scrollPos;
    private List<string> mapScenePaths = new List<string>();

    public static void Open()
    {
        SavedMapsWindow window = GetWindow<SavedMapsWindow>("Сохранённые карты");
        window.minSize = new Vector2(340, 480);
        Resolution res = Screen.currentResolution;
        window.position = new Rect(Mathf.Max(200, res.width - 400), 100, 360, 600);
        window.Show();
        window.RefreshMapList();
    }

    private void OnEnable()
    {
        RefreshMapList();
    }

    private void OnFocus()
    {
        RefreshMapList();
    }

    private void RefreshMapList()
    {
        mapScenePaths.Clear();
        MapBuilderEditor.EnsureFolder(MapBuilderEditor.CustomMapsFolder);

        if (Directory.Exists(MapBuilderEditor.CustomMapsFolder))
        {
            string[] files = Directory.GetFiles(MapBuilderEditor.CustomMapsFolder, "*.unity");
            foreach (string file in files)
            {
                mapScenePaths.Add(file.Replace("\\", "/"));
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("📁 Сохранённые карты (Custom Maps)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Выберите карту для открытия в Map Builder, редактирования или тестирования.", MessageType.Info);
        
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔄 Обновить список", GUILayout.Height(26)))
        {
            RefreshMapList();
        }
        if (GUILayout.Button("➕ Создать новую карту", GUILayout.Height(26)))
        {
            MapBuilderEditor.NewCleanMap();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        if (mapScenePaths.Count == 0)
        {
            EditorGUILayout.HelpBox("Пока нет сохранённых карт в папке Assets/Scenes/CustomMaps/.\nСоздайте карту в Map Builder и нажмите 'Сохранить карту'.", MessageType.Warning);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < mapScenePaths.Count; i++)
        {
            string path = mapScenePaths[i];
            string mapName = Path.GetFileNameWithoutExtension(path);
            DateTime lastWrite = File.GetLastWriteTime(path);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🗺 <b>{mapName}</b>", new GUIStyle(EditorStyles.label) { richText = true, fontSize = 13 });
            EditorGUILayout.LabelField(lastWrite.ToString("dd.MM HH:mm"), EditorStyles.miniLabel, GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);

            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.85f, 1f, 1f);
            if (GUILayout.Button("✏️ Править в Map Builder", GUILayout.Height(26)))
            {
                MapBuilderEditor.LoadAndEditMap(path);
            }

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f, 1f);
            if (GUILayout.Button("▶ Играть", GUILayout.Width(65), GUILayout.Height(26)))
            {
                MyMapMenu.PlayMap(mapName);
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button("🗑", GUILayout.Width(28), GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Удаление карты", $"Удалить карту '{mapName}' навсегда?", "Да, удалить", "Отмена"))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.Refresh();
                    MyMapMenu.RegenerateMenu();
                    RefreshMapList();
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
    }
}
