using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class GenerateKrogerRingRoadMap
{
    public const string ScenePath = "Assets/Scenes/CustomMaps/Map_14_kroger_ring_road.unity";
    private const string SpritesDir = "Assets/GeneratedSprites";

    static GenerateKrogerRingRoadMap()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath))
            {
                GenerateScene();
            }
        };
    }

    [MenuItem("Tools/Map Builder/Generate Kroger Ring Road Map", false, 50)]
    public static void GenerateScene()
    {
        Debug.Log("<color=#33ccff>[GenerateKrogerRingRoadMap] Starting generation of Kroger DC Ring Road Map...</color>");

        // 1. Create a fresh new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Physics2D.gravity = Vector2.zero;

        // Load Sprites
        Sprite asphaltSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/AsphaltGround.png");
        Sprite stripeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/ParkingStripe.png");
        Sprite tractorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Tractor_HD.png") ?? AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Tractor.png");
        Sprite trailerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/Trailer.png");
        Sprite roadArrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/RoadArrow.png");
        Sprite yellowArrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/YellowParkingArrow.png");
        Sprite carSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/PassengerCar.png");
        Sprite buildingRoofSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "/BuildingRoof.png") ?? stripeSprite;

        // 2. Setup Camera
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 42f;
        camGo.transform.position = new Vector3(50f, 55f, -10f);
        camGo.transform.rotation = Quaternion.identity;

        // 3. Setup Global Light 2D
        GameObject lightGo = new GameObject("Global Light 2D");
        Light2D globalLight = lightGo.AddComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global;
        globalLight.intensity = 1.0f;
        globalLight.color = Color.white;

        // 4. Setup Workspace Root & Ground
        float mapWidth = 100f;
        float mapHeight = 110f;

        GameObject workspace = new GameObject("MapBuilder_Workspace");
        MapData mapData = workspace.AddComponent<MapData>();
        mapData.SetDimensions(mapWidth, mapHeight);

        // Asphalt Ground (100x110)
        GameObject ground = new GameObject("AsphaltGround");
        ground.transform.SetParent(workspace.transform, false);
        ground.transform.position = new Vector3(50f, 55f, 0f);
        SpriteRenderer srGround = ground.AddComponent<SpriteRenderer>();
        srGround.sprite = asphaltSprite;
        srGround.drawMode = SpriteDrawMode.Tiled;
        srGround.size = new Vector2(mapWidth, mapHeight);
        srGround.sortingOrder = -10;

        // Containers
        GameObject bordersContainer = new GameObject("YardBorders");
        bordersContainer.transform.SetParent(workspace.transform, false);

        GameObject buildingsContainer = new GameObject("MapBuilder_Buildings");
        buildingsContainer.transform.SetParent(workspace.transform, false);

        GameObject slotsContainer = new GameObject("MapBuilder_Slots");
        slotsContainer.transform.SetParent(workspace.transform, false);

        GameObject linesContainer = new GameObject("MapBuilder_MarkingLines");
        linesContainer.transform.SetParent(workspace.transform, false);

        GameObject arrowsContainer = new GameObject("MapBuilder_RoadArrows");
        arrowsContainer.transform.SetParent(workspace.transform, false);

        GameObject carsContainer = new GameObject("MapBuilder_PassengerCars");
        carsContainer.transform.SetParent(workspace.transform, false);

        // 5. Yard Borders (Physical boundaries with gate at top)
        CreateBorder("Border_Bottom", bordersContainer.transform, new Vector3(50f, 0f, 0f), new Vector2(102f, 1f), new Vector2(110f, 4f), new Vector2(0f, -1.5f), stripeSprite);
        CreateBorder("Border_Left", bordersContainer.transform, new Vector3(0f, 55f, 0f), new Vector2(1f, 112f), new Vector2(4f, 120f), new Vector2(-1.5f, 0f), stripeSprite);
        CreateBorder("Border_Right", bordersContainer.transform, new Vector3(100f, 55f, 0f), new Vector2(1f, 112f), new Vector2(4f, 120f), new Vector2(1.5f, 0f), stripeSprite);
        
        // Top border with Gate gap (Gate between x=42 and x=54)
        CreateBorder("Border_Top_Left", bordersContainer.transform, new Vector3(21f, 110f, 0f), new Vector2(42f, 1f), new Vector2(44f, 4f), new Vector2(0f, 1.5f), stripeSprite);
        CreateBorder("Border_Top_Right", bordersContainer.transform, new Vector3(77f, 110f, 0f), new Vector2(46f, 1f), new Vector2(48f, 4f), new Vector2(0f, 1.5f), stripeSprite);

        // 6. Guard Shack / Check-in booth at Top Gate
        GameObject shackGo = new GameObject("Building_GuardShack");
        shackGo.transform.SetParent(buildingsContainer.transform, false);
        BuildingObstacle shack = shackGo.AddComponent<BuildingObstacle>();
        shack.Setup(new Vector2(38f, 104.5f), new Vector2(43f, 104.5f), 4.5f, new Color(0.22f, 0.48f, 0.90f, 1.0f), "CHECK-IN / КПП", buildingRoofSprite);

        // Stop Line / Boom barrier markings at check-in
        CreateMarking(linesContainer.transform, new Vector2(43.5f, 104.5f), new Vector2(53.5f, 104.5f), 0.35f, new Color(1.0f, 0.85f, 0.05f, 1.0f), stripeSprite, "Marking_GateStopLine");

        // 7. Central Warehouse Building ("KROGER DC DISTRIBUTION CENTER")
        // Width: 56m (x: 20 to 76), Height: 50m (y: 40 to 90), Center: (48, 65)
        GameObject warehouseGo = new GameObject("Building_KrogerWarehouse");
        warehouseGo.transform.SetParent(buildingsContainer.transform, false);
        BuildingObstacle warehouse = warehouseGo.AddComponent<BuildingObstacle>();
        warehouse.Setup(new Vector2(48f, 40f), new Vector2(48f, 90f), 56f, new Color(0.84f, 0.84f, 0.81f, 1.0f), "KROGER DC DISTRIBUTION CENTER", buildingRoofSprite);

        // 8. Ring Road Direction Arrows (Clockwise perimeter road)
        // North Road: pointing East (0 deg)
        CreateArrow(arrowsContainer.transform, new Vector2(30f, 96.5f), 0f, 1.3f, Color.white, roadArrowSprite, "Arrow_North_1");
        CreateArrow(arrowsContainer.transform, new Vector2(48f, 96.5f), 0f, 1.3f, Color.white, roadArrowSprite, "Arrow_North_2");
        CreateArrow(arrowsContainer.transform, new Vector2(66f, 96.5f), 0f, 1.3f, Color.white, roadArrowSprite, "Arrow_North_3");
        CreateArrow(arrowsContainer.transform, new Vector2(83f, 94.0f), 315f, 1.3f, Color.white, roadArrowSprite, "Arrow_NorthEast_Curve");

        // East Road: pointing South (270 deg)
        CreateArrow(arrowsContainer.transform, new Vector2(84f, 78f), 270f, 1.3f, Color.white, roadArrowSprite, "Arrow_East_1");
        CreateArrow(arrowsContainer.transform, new Vector2(84f, 60f), 270f, 1.3f, Color.white, roadArrowSprite, "Arrow_East_2");
        CreateArrow(arrowsContainer.transform, new Vector2(84f, 42f), 270f, 1.3f, Color.white, roadArrowSprite, "Arrow_East_3");
        CreateArrow(arrowsContainer.transform, new Vector2(82f, 26f), 225f, 1.3f, Color.white, roadArrowSprite, "Arrow_SouthEast_Curve");

        // South Apron: pointing West (180 deg)
        CreateArrow(arrowsContainer.transform, new Vector2(70f, 24f), 180f, 1.3f, Color.white, roadArrowSprite, "Arrow_South_1");
        CreateArrow(arrowsContainer.transform, new Vector2(55f, 24f), 180f, 1.3f, Color.white, roadArrowSprite, "Arrow_South_2");

        // 9. Employee Parking Cars along East Wall (x = 95.5m)
        Color[] carColors = new Color[]
        {
            new Color(0.92f, 0.92f, 0.94f), // White
            new Color(0.85f, 0.15f, 0.15f), // Red
            new Color(0.20f, 0.45f, 0.85f), // Blue
            new Color(0.70f, 0.72f, 0.75f), // Silver
            new Color(0.95f, 0.75f, 0.10f), // Yellow
            new Color(0.95f, 0.45f, 0.10f), // Orange
            new Color(0.12f, 0.55f, 0.35f), // Emerald Green
            new Color(0.40f, 0.20f, 0.55f), // Purple
            new Color(0.80f, 0.80f, 0.80f), // Light Grey
            new Color(0.88f, 0.18f, 0.22f)  // Crimson
        };

        // Long parking stall line divider
        CreateMarking(linesContainer.transform, new Vector2(92.5f, 36f), new Vector2(92.5f, 88f), 0.20f, Color.white, stripeSprite, "Marking_EmployeeParkingLine");

        for (int i = 0; i < 10; i++)
        {
            float yPos = 40f + i * 4.8f;
            // Stall divider line
            CreateMarking(linesContainer.transform, new Vector2(92.5f, yPos - 2.4f), new Vector2(99.5f, yPos - 2.4f), 0.18f, Color.white, stripeSprite, $"Marking_CarStall_{i+1}");

            GameObject carGo = new GameObject($"PassengerCar_{i+1}");
            carGo.transform.SetParent(carsContainer.transform, false);
            PassengerCarObstacle car = carGo.AddComponent<PassengerCarObstacle>();
            car.Setup(new Vector2(96.0f, yPos), 270f, carColors[i % carColors.Length], carSprite);
        }
        // Top and bottom cap lines for employee parking
        CreateMarking(linesContainer.transform, new Vector2(92.5f, 88f), new Vector2(99.5f, 88f), 0.18f, Color.white, stripeSprite, "Marking_CarStall_Top");

        // 10. Staging Yard along West Wall (Stalls for 53' trailers at 90 deg, x: 2m to 18m)
        CreateMarking(linesContainer.transform, new Vector2(17.5f, 38f), new Vector2(17.5f, 88f), 0.20f, Color.white, stripeSprite, "Marking_StagingYardLine");

        for (int i = 0; i < 6; i++)
        {
            float yPos = 44f + i * 7.5f;
            // Divider lines
            CreateMarking(linesContainer.transform, new Vector2(1.0f, yPos - 3.75f), new Vector2(17.5f, yPos - 3.75f), 0.18f, Color.white, stripeSprite, $"Marking_StagingStall_{i+1}");

            // Parked 53' trailer dropped in stall (rotated 90 deg, facing East)
            CreateDroppedTrailer(slotsContainer.transform, new Vector2(9.2f, yPos), 90f, trailerSprite, $"Staging_Trailer_{i+1}");
        }
        CreateMarking(linesContainer.transform, new Vector2(1.0f, 89f), new Vector2(17.5f, 89f), 0.18f, Color.white, stripeSprite, "Marking_StagingStall_Top");

        // 11. Warehouse Loading Docks (South wall of warehouse at y = 40.0m)
        // 9 Dock stalls (4.5m wide each, y from 40m down to 20m)
        // Dock 1: x=24.5, Dock 2: x=29.5, Dock 3: x=34.5, Dock 4: x=40.0 (TARGET), Dock 5: x=45.5, Dock 6: x=50.5, Dock 7: x=55.5, Dock 8: x=60.5, Dock 9: x=65.5
        float[] dockX = new float[] { 24.5f, 29.5f, 34.5f, 40.0f, 45.5f, 50.5f, 55.5f, 60.5f, 65.5f };

        for (int i = 0; i < dockX.Length; i++)
        {
            int dockNum = i + 1;
            float xPos = dockX[i];

            if (dockNum == 4)
            {
                // TARGET PARKING SLOT (Dock 4)
                CreateTargetParkingSlot(slotsContainer.transform, new Vector2(xPos, 28.0f), 0f, stripeSprite, yellowArrowSprite, "Slot_Dock_4_TARGET");
            }
            else
            {
                // Standard Parked Truck at dock
                CreateParkedTruckSlot(slotsContainer.transform, new Vector2(xPos, 28.0f), 0f, stripeSprite, tractorSprite, trailerSprite, $"Slot_Dock_{dockNum}_Parked");
            }
        }

        // 12. Drop-Lot (Bottom row of dropped trailers at y = 7.0m, facing North 0 deg)
        // Stalls at x = 22, 28, 34, 40, 46, 52, 58, 64, 70, 76
        float[] dropLotX = new float[] { 22f, 28f, 34f, 40f, 46f, 52f, 58f, 64f, 70f, 76f };
        for (int i = 0; i < dropLotX.Length; i++)
        {
            float xPos = dropLotX[i];
            CreateDroppedTrailer(slotsContainer.transform, new Vector2(xPos, 7.5f), 0f, trailerSprite, $"DropLot_Trailer_{i+1}");

            // Stall marking lines
            CreateMarking(linesContainer.transform, new Vector2(xPos - 2.5f, 0.5f), new Vector2(xPos - 2.5f, 15.5f), 0.18f, Color.white, stripeSprite, $"Marking_DropLot_{i+1}_L");
            CreateMarking(linesContainer.transform, new Vector2(xPos + 2.5f, 0.5f), new Vector2(xPos + 2.5f, 15.5f), 0.18f, Color.white, stripeSprite, $"Marking_DropLot_{i+1}_R");
        }

        // 13. Create Playable Truck (Tractor + 53' Trailer) starting right after check-in booth
        // Position: x = 48.0m, y = 98.0m, facing 180 deg (South)
        CreatePlayableTruck(new Vector3(48f, 98f, 0f), new Vector3(48f, 106.9f, 0f), Quaternion.Euler(0f, 0f, 180f), tractorSprite, trailerSprite);

        // 14. Camera Follow Script
        CameraFollow camFollow = camGo.AddComponent<CameraFollow>();

        // 15. Setup Mobile Controls & MapSelectMenu Canvas
        SetupCanvasAndManagers();

        // 16. Save the scene
        EnsureFolder("Assets/Scenes/CustomMaps");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log($"<color=#55ff55>[GenerateKrogerRingRoadMap] Successfully generated and saved Kroger DC Ring Road map to: {ScenePath}</color>");
    }

    private static void CreateBorder(string name, Transform parent, Vector3 pos, Vector2 spriteSize, Vector2 colSize, Vector2 colOffset, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = spriteSize;
        sr.color = new Color(0.95f, 0.80f, 0.10f, 0.95f); // Bright safety yellow
        sr.sortingOrder = 5;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = colSize;
        col.offset = colOffset;
        col.isTrigger = true;
    }

    private static void CreateMarking(Transform parent, Vector2 start, Vector2 end, float thickness, Color col, Sprite sprite, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RoadMarkingLine line = go.AddComponent<RoadMarkingLine>();
        line.Setup(start, end, thickness, col, sprite);
    }

    private static void CreateArrow(Transform parent, Vector2 pos, float rot, float scale, Color col, Sprite sprite, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RoadDirectionArrow arrow = go.AddComponent<RoadDirectionArrow>();
        arrow.Setup(pos, rot, scale, col, sprite);
    }

    private static void CreateDroppedTrailer(Transform parent, Vector2 pos, float rot, Sprite sprite, string name)
    {
        GameObject trailerGo = new GameObject(name);
        trailerGo.transform.SetParent(parent, false);
        trailerGo.transform.position = new Vector3(pos.x, pos.y, 0f);
        trailerGo.transform.rotation = Quaternion.Euler(0f, 0f, rot);

        SpriteRenderer sr = trailerGo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.88f, 0.88f, 0.90f, 1.0f);
        sr.sortingOrder = 10;

        BoxCollider2D col = trailerGo.AddComponent<BoxCollider2D>();
        col.size = new Vector2(2.6f, 16.15f);
        col.offset = Vector2.zero;
        col.isTrigger = true;
    }

    private static void CreateParkedTruckSlot(Transform parent, Vector2 pos, float rot, Sprite stripeSp, Sprite tractorSp, Sprite trailerSp, string name)
    {
        GameObject slotGo = new GameObject(name);
        slotGo.transform.SetParent(parent, false);
        slotGo.transform.position = new Vector3(pos.x, pos.y, 0f);
        slotGo.transform.rotation = Quaternion.Euler(0f, 0f, rot);

        float width = 4.5f;
        float length = 24.0f;
        float halfW = width * 0.5f;

        // Stall lines
        CreateStallLine(slotGo.transform, new Vector3(-halfW, 0f, 0f), 0f, new Vector2(0.18f, length), stripeSp, Color.white);
        CreateStallLine(slotGo.transform, new Vector3(halfW, 0f, 0f), 0f, new Vector2(0.18f, length), stripeSp, Color.white);
        CreateStallLine(slotGo.transform, new Vector3(0f, length * 0.5f, 0f), 90f, new Vector2(0.18f, width), stripeSp, Color.white);

        // Parked Trailer (centered inside stall)
        GameObject trGo = new GameObject("ParkedTrailer");
        trGo.transform.SetParent(slotGo.transform, false);
        trGo.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        SpriteRenderer srTr = trGo.AddComponent<SpriteRenderer>();
        srTr.sprite = trailerSp;
        srTr.color = Color.white;
        srTr.sortingOrder = 10;

        BoxCollider2D colTr = trGo.AddComponent<BoxCollider2D>();
        colTr.size = new Vector2(2.6f, 16.15f);
        colTr.isTrigger = true;

        // Parked Tractor
        GameObject tcGo = new GameObject("ParkedTractor");
        tcGo.transform.SetParent(slotGo.transform, false);
        tcGo.transform.localPosition = new Vector3(0f, -8.0f, 0f);
        SpriteRenderer srTc = tcGo.AddComponent<SpriteRenderer>();
        srTc.sprite = tractorSp;
        srTc.color = new Color(0.20f, 0.50f, 0.90f, 1.0f);
        srTc.sortingOrder = 8;

        BoxCollider2D colTc = tcGo.AddComponent<BoxCollider2D>();
        colTc.size = new Vector2(2.6f, 8.2f);
        colTc.isTrigger = true;
    }

    private static void CreateTargetParkingSlot(Transform parent, Vector2 pos, float rot, Sprite stripeSp, Sprite arrowSp, string name)
    {
        GameObject slotGo = new GameObject(name);
        slotGo.transform.SetParent(parent, false);
        slotGo.transform.position = new Vector3(pos.x, pos.y, 0f);
        slotGo.transform.rotation = Quaternion.Euler(0f, 0f, rot);

        float width = 4.5f;
        float length = 24.0f;
        float halfW = width * 0.5f;
        Color yellow = new Color(1.0f, 0.85f, 0.05f, 1.0f);

        // Yellow stall stripes
        CreateStallLine(slotGo.transform, new Vector3(-halfW, 0f, 0f), 0f, new Vector2(0.35f, length), stripeSp, yellow);
        CreateStallLine(slotGo.transform, new Vector3(halfW, 0f, 0f), 0f, new Vector2(0.35f, length), stripeSp, yellow);
        CreateStallLine(slotGo.transform, new Vector3(0f, length * 0.5f, 0f), 90f, new Vector2(0.35f, width), stripeSp, yellow);

        // Yellow Parking Arrow
        GameObject arrowGo = new GameObject("YellowParkingArrow");
        arrowGo.transform.SetParent(slotGo.transform, false);
        arrowGo.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        SpriteRenderer srArrow = arrowGo.AddComponent<SpriteRenderer>();
        srArrow.sprite = arrowSp;
        srArrow.color = yellow;
        srArrow.sortingOrder = 3;

        // Target Parking Zone Trigger Component
        GameObject targetZoneGo = new GameObject("TargetParkingZone");
        targetZoneGo.transform.SetParent(slotGo.transform, false);
        targetZoneGo.transform.localPosition = Vector3.zero;

        ParkingTargetZone ptz = targetZoneGo.AddComponent<ParkingTargetZone>();
        BoxCollider2D triggerCol = targetZoneGo.AddComponent<BoxCollider2D>();
        triggerCol.size = new Vector2(width * 0.85f, length * 0.85f);
        triggerCol.isTrigger = true;
    }

    private static void CreateStallLine(Transform parent, Vector3 localPos, float rotZ, Vector2 size, Sprite sprite, Color col)
    {
        GameObject go = new GameObject("StallLine");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.color = col;
        sr.sortingOrder = 3;
    }

    private static void CreatePlayableTruck(Vector3 tractorPos, Vector3 trailerPos, Quaternion rot, Sprite tractorSp, Sprite trailerSp)
    {
        // 1. Tractor
        GameObject tractorGo = new GameObject("Tractor");
        tractorGo.transform.position = tractorPos;
        tractorGo.transform.rotation = rot;

        SpriteRenderer srTractor = tractorGo.AddComponent<SpriteRenderer>();
        srTractor.sprite = tractorSp;
        srTractor.sortingOrder = 8;

        Rigidbody2D rbTractor = tractorGo.AddComponent<Rigidbody2D>();
        rbTractor.gravityScale = 0f;
        rbTractor.mass = 8000f;
        rbTractor.linearDamping = 1.5f;
        rbTractor.angularDamping = 2.5f;

        BoxCollider2D colTractor = tractorGo.AddComponent<BoxCollider2D>();
        colTractor.size = new Vector2(2.6f, 8.2f);
        colTractor.offset = new Vector2(0f, 0f);

        tractorGo.AddComponent<TruckCollisionDetector>();
        TruckController tc = tractorGo.AddComponent<TruckController>();

        // 2. Trailer
        GameObject trailerGo = new GameObject("Trailer");
        trailerGo.transform.position = trailerPos;
        trailerGo.transform.rotation = rot;

        SpriteRenderer srTrailer = trailerGo.AddComponent<SpriteRenderer>();
        srTrailer.sprite = trailerSp;
        srTrailer.sortingOrder = 10;

        Rigidbody2D rbTrailer = trailerGo.AddComponent<Rigidbody2D>();
        rbTrailer.gravityScale = 0f;
        rbTrailer.mass = 12000f;
        rbTrailer.linearDamping = 1.5f;
        rbTrailer.angularDamping = 2.5f;

        BoxCollider2D colTrailer = trailerGo.AddComponent<BoxCollider2D>();
        colTrailer.size = new Vector2(2.6f, 16.15f);
        colTrailer.offset = new Vector2(0f, 0f);

        trailerGo.AddComponent<TruckCollisionDetector>();

        // Hitch Joint
        HingeJoint2D hitch = tractorGo.AddComponent<HingeJoint2D>();
        hitch.connectedBody = rbTrailer;
        hitch.autoConfigureConnectedAnchor = false;
        hitch.anchor = new Vector2(0f, -2.4f); // 5th wheel location on tractor
        hitch.connectedAnchor = new Vector2(0f, 6.5f); // Kingpin location on trailer
        hitch.useLimits = false;

        SerializedObject soTc = new SerializedObject(tc);
        soTc.FindProperty("trailerRb").objectReferenceValue = rbTrailer;
        soTc.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupCanvasAndManagers()
    {
        TruckSimulatorSetup.EnsureEventSystem();

        // UI Manager
        GameObject gmGo = new GameObject("GameManager");
        gmGo.AddComponent<MapSelectMenu>();
        gmGo.AddComponent<TruckCrashEffect>();
    }

    private static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
