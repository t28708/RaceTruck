using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class TruckSimulatorSetup
{
    private const string SpritesDir = "Assets/GeneratedSprites";

    [MenuItem("Tools/Open Map 1 (Polygon)")]
    public static void OpenMap1()
    {
        if (EditorApplication.isPlaying)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        }
        else
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        }
    }

    [MenuItem("Tools/Open Map 2 (Alley Dock)")]
    public static void OpenMap2()
    {
        if (EditorApplication.isPlaying)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Level2_AlleyDock");
        }
        else
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Level2_AlleyDock.unity");
        }
    }

    [MenuItem("Tools/Setup Truck Physics")]
    public static void SetupTruckPhysics()
    {
        // Clear selection to avoid InspectorElement trying to inspect destroyed objects
        Selection.activeObject = null;

        // 1. Set Physics2D gravity to (0, 0)
        Physics2D.gravity = Vector2.zero;

        // Clean up all existing truck objects and canvases to prevent duplicates
        CleanExistingObjects();

        // Ensure EventSystem exists with new Input System support
        EnsureEventSystem();

        // Ensure realistic sprite assets exist on disk (do not overwrite existing textures)
        EnsureSpriteAssets(false);

        Sprite tractorSprite = LoadSpriteSafe($"{SpritesDir}/Tractor.png");
        Sprite trailerSprite = LoadSpriteSafe($"{SpritesDir}/Trailer.png");
        Sprite steeringWheelSprite = LoadSpriteSafe($"{SpritesDir}/SteeringWheelRealistic.png");
        if (steeringWheelSprite == null) steeringWheelSprite = LoadSpriteSafe($"{SpritesDir}/SteeringWheel.png");
        Sprite pedalGasSprite = LoadSpriteSafe($"{SpritesDir}/PedalGas.png");
        Sprite pedalBrakeSprite = LoadSpriteSafe($"{SpritesDir}/PedalBrake.png");
        Sprite groundSprite = LoadSpriteSafe($"{SpritesDir}/AsphaltGround.png");
        Sprite coneSprite = LoadSpriteSafe($"{SpritesDir}/TrafficCone.png");
        Sprite barrierSprite = LoadSpriteSafe($"{SpritesDir}/ConcreteBarrier.png");
        Sprite barrelSprite = LoadSpriteSafe($"{SpritesDir}/Barrel.png");

        // 2. Build 100m x 80m CDL Training Yard & Obstacle Course
        BuildTrainingYard(groundSprite, barrierSprite, coneSprite, barrelSprite, tractorSprite, trailerSprite);

        // 3. Create UI Canvas with Steering Wheel, HUD, and Crash Feedback
        GameObject canvasGo = CreateControlsCanvas(steeringWheelSprite, pedalGasSprite, pedalBrakeSprite);

        // 4. Create 'Tractor' GameObject (Real Class 8 American Conventional Semi)
        // Length: 8.5m, Width: 2.6m
        GameObject tractor = new GameObject("Tractor");
        tractor.transform.position = new Vector3(0f, -24f, 0f); // Starting position facing North
        tractor.transform.localScale = Vector3.one;

        SpriteRenderer tractorSr = tractor.AddComponent<SpriteRenderer>();
        tractorSr.sprite = tractorSprite;
        tractorSr.sortingOrder = 8;

        BoxCollider2D tractorCollider = tractor.AddComponent<BoxCollider2D>();
        tractorCollider.size = new Vector2(2.55f, 8.2f);

        Rigidbody2D tractorRb = tractor.AddComponent<Rigidbody2D>();
        tractorRb.bodyType = RigidbodyType2D.Kinematic;
        tractorRb.useFullKinematicContacts = true;

        // Front steerable visual wheels (axle at y = +2.8m)
        GameObject frontLeftWheel = CreateWheelChild("FrontLeftWheel", tractor.transform, new Vector3(-1.08f, 2.8f, 0f), wheelSprite);
        GameObject frontRightWheel = CreateWheelChild("FrontRightWheel", tractor.transform, new Vector3(1.08f, 2.8f, 0f), wheelSprite);

        // Rear tandem fixed visual wheels (tandem centered around y = -2.8m)
        CreateWheelChild("RearLeftWheel1", tractor.transform, new Vector3(-1.08f, -2.25f, 0f), wheelSprite);
        CreateWheelChild("RearRightWheel1", tractor.transform, new Vector3(1.08f, -2.25f, 0f), wheelSprite);
        CreateWheelChild("RearLeftWheel2", tractor.transform, new Vector3(-1.08f, -3.35f, 0f), wheelSprite);
        CreateWheelChild("RearRightWheel2", tractor.transform, new Vector3(1.08f, -3.35f, 0f), wheelSprite);

        tractor.AddComponent<TruckCollisionDetector>();

        TruckController truckController = tractor.AddComponent<TruckController>();
        truckController.SetupWheelReferences(frontLeftWheel.transform, frontRightWheel.transform);

        // 5. Create 'Trailer' GameObject (Real 53-ft Semi-Trailer: 16.2m length)
        // Kingpin at +6.9m connects to Tractor hitch at -2.8m -> trailer position = -24 + (-2.8) - 6.9 = -33.7m
        GameObject trailer = new GameObject("Trailer");
        trailer.transform.position = new Vector3(0f, -33.7f, 0f);
        trailer.transform.localScale = Vector3.one;

        SpriteRenderer trailerSr = trailer.AddComponent<SpriteRenderer>();
        trailerSr.sprite = trailerSprite;
        trailerSr.sortingOrder = 10;

        BoxCollider2D trailerCollider = trailer.AddComponent<BoxCollider2D>();
        trailerCollider.size = new Vector2(2.58f, 15.9f);

        Rigidbody2D trailerRb = trailer.AddComponent<Rigidbody2D>();
        trailerRb.bodyType = RigidbodyType2D.Kinematic;
        trailerRb.useFullKinematicContacts = true;

        // Trailer rear tandem wheels (centered around y = -5.6m)
        CreateWheelChild("TrailerRearLeft1", trailer.transform, new Vector3(-1.08f, -5.05f, 0f), wheelSprite);
        CreateWheelChild("TrailerRearRight1", trailer.transform, new Vector3(1.08f, -5.05f, 0f), wheelSprite);
        CreateWheelChild("TrailerRearLeft2", trailer.transform, new Vector3(-1.08f, -6.15f, 0f), wheelSprite);
        CreateWheelChild("TrailerRearRight2", trailer.transform, new Vector3(1.08f, -6.15f, 0f), wheelSprite);

        trailer.AddComponent<TruckCollisionDetector>();

        // Link trailer to truck controller (exact closed-form tractrix kinematic joint)
        truckController.SetupTrailer(trailerRb);

        // Add My Trucking Skills clearance & trajectory guide lines (red trailer rear, green tractor push)
        tractor.AddComponent<TruckGuideLines>();

        // 2D Physics HingeJoint coupling with realistic jackknife contact geometry
        HingeJoint2D hinge = tractor.AddComponent<HingeJoint2D>();
        hinge.connectedBody = trailerRb;
        hinge.anchor = new Vector2(0f, -2.8f);
        hinge.autoConfigureConnectedAnchor = false;
        hinge.connectedAnchor = new Vector2(0f, 6.9f);
        hinge.enableCollision = true;
        hinge.useLimits = false;

        // 6. Setup Main Camera Follow & Snap Directly to Truck
        if (Camera.main != null)
        {
            Camera.main.orthographic = true;
            Camera.main.orthographicSize = 16.0f;

            // Position camera directly over tractor center
            Camera.main.transform.position = new Vector3(0f, -24f, -10f);
            Camera.main.transform.rotation = Quaternion.identity;

            CameraFollow camFollow = Camera.main.GetComponent<CameraFollow>();
            if (camFollow == null)
            {
                camFollow = Camera.main.gameObject.AddComponent<CameraFollow>();
            }
            camFollow.SetupTargets(tractor.transform, trailer.transform);
        }

        // Register Undo & Selection
        Undo.RegisterCreatedObjectUndo(canvasGo, "Setup Truck Canvas");
        Undo.RegisterCreatedObjectUndo(tractor, "Setup Truck Physics");
        Undo.RegisterCreatedObjectUndo(trailer, "Setup Truck Physics");
        Selection.activeGameObject = tractor;

        // Ensure scene is marked dirty and saved to disk (only in edit mode)
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log("Realistic My Trucking Skills 53-ft rig and CDL Training Yard created and saved successfully!");
    }

    private static void BuildTrainingYard(Sprite groundSprite, Sprite barrierSprite, Sprite coneSprite, Sprite barrelSprite, Sprite tractorSprite, Sprite trailerSprite)
    {
        GameObject yardParent = new GameObject("TrainingYard");

        // 1. Asphalt Ground (110m x 90m)
        GameObject ground = new GameObject("AsphaltGround");
        ground.transform.SetParent(yardParent.transform, false);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = Vector3.one;
        SpriteRenderer groundSr = ground.AddComponent<SpriteRenderer>();
        groundSr.sprite = groundSprite;
        groundSr.drawMode = SpriteDrawMode.Tiled;
        groundSr.size = new Vector2(112f, 92f);
        groundSr.sortingOrder = -10;

        // 2. Perimeter Walls (Enclosing 110m x 90m boundaries)
        CreateWall("Wall_North", yardParent.transform, new Vector2(0f, 45f), new Vector2(112f, 2.5f), barrierSprite);
        CreateWall("Wall_South", yardParent.transform, new Vector2(0f, -45f), new Vector2(112f, 2.5f), barrierSprite);
        CreateWall("Wall_West", yardParent.transform, new Vector2(-55f, 0f), new Vector2(2.5f, 90f), barrierSprite);
        CreateWall("Wall_East", yardParent.transform, new Vector2(55f, 0f), new Vector2(2.5f, 90f), barrierSprite);

        // 3. 90-Degree Alley Dock (Бокс 90° для отработки заезда задом, как на сдаче CDL)
        // Positioned at (x: 24, y: 18..38), bay width: 6.8m, depth: 20m
        GameObject dockParent = new GameObject("AlleyDockBox");
        dockParent.transform.SetParent(yardParent.transform, false);

        // Parked Red Trucks flanking the dock (exact My Trucking Skills obstacle layout!)
        CreateParkedTruck("ParkedTruck_Left", dockParent.transform, new Vector2(19.2f, 28f), 0f, tractorSprite, trailerSprite);
        CreateParkedTruck("ParkedTruck_Right", dockParent.transform, new Vector2(28.8f, 28f), 0f, tractorSprite, trailerSprite);
        CreateObstacle("Dock_BackBumper", dockParent.transform, new Vector2(24f, 38.5f), new Vector2(8f, 1.2f), barrierSprite);

        // Entry Gate Cones at the dock opening
        CreateCone("Dock_GateCone_L", dockParent.transform, new Vector2(20.5f, 17f), coneSprite);
        CreateCone("Dock_GateCone_R", dockParent.transform, new Vector2(27.5f, 17f), coneSprite);

        // 4. Slalom Cones Course DIRECTLY in front of the truck's starting path!
        GameObject conesParent = new GameObject("SlalomCourse");
        conesParent.transform.SetParent(yardParent.transform, false);

        // Start Gate Cones (flanking the truck's starting lane)
        CreateCone("StartGate_L", conesParent.transform, new Vector2(-4.5f, -24f), coneSprite);
        CreateCone("StartGate_R", conesParent.transform, new Vector2(4.5f, -24f), coneSprite);

        // Big Highway Cones aligned down the forward driving lane
        CreateCone("SlalomCone_1", conesParent.transform, new Vector2(0f, -12f), coneSprite);
        CreateCone("SlalomCone_2", conesParent.transform, new Vector2(-5.5f, -1f), coneSprite);
        CreateCone("SlalomCone_3", conesParent.transform, new Vector2(5.5f, 10f), coneSprite);
        CreateCone("SlalomCone_4", conesParent.transform, new Vector2(-5.5f, 21f), coneSprite);
        CreateCone("SlalomCone_5", conesParent.transform, new Vector2(0f, 32f), coneSprite);

        // Left boundary guidance cones
        for (int i = 0; i < 5; i++)
        {
            float yPos = -25f + i * 14f;
            CreateCone($"SideCone_L_{i + 1}", conesParent.transform, new Vector2(-22f, yPos), coneSprite);
        }

        // 5. Chicane Obstacles & Barrel Clusters
        GameObject obstacleParent = new GameObject("ChicaneObstacles");
        obstacleParent.transform.SetParent(yardParent.transform, false);

        CreateObstacle("Barrier_Block1", obstacleParent.transform, new Vector2(-32f, 10f), new Vector2(10f, 1.8f), barrierSprite);
        CreateObstacle("Barrier_Block2", obstacleParent.transform, new Vector2(-32f, -10f), new Vector2(10f, 1.8f), barrierSprite);

        CreateBarrel("Barrel_Cluster1", obstacleParent.transform, new Vector2(-15f, 5f), barrelSprite);
        CreateBarrel("Barrel_Cluster2", obstacleParent.transform, new Vector2(-13.5f, 5f), barrelSprite);
        CreateBarrel("Barrel_Cluster3", obstacleParent.transform, new Vector2(-14.2f, 6.5f), barrelSprite);
    }

    private static void CreateWall(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.position = pos;

        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = 1;

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
    }

    private static void CreateObstacle(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite)
    {
        GameObject obs = new GameObject(name);
        obs.transform.SetParent(parent, false);
        obs.transform.position = pos;

        SpriteRenderer sr = obs.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = size;
        sr.sortingOrder = 1;

        BoxCollider2D col = obs.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = true;
    }

    private static void CreateCone(string name, Transform parent, Vector2 pos, Sprite sprite)
    {
        GameObject cone = new GameObject(name);
        cone.transform.SetParent(parent, false);
        cone.transform.position = pos;
        cone.transform.localScale = Vector3.one * 1.6f;

        SpriteRenderer sr = cone.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 2;

        CircleCollider2D col = cone.AddComponent<CircleCollider2D>();
        col.radius = 0.5f;
        col.isTrigger = true;
    }

    private static void CreateBarrel(string name, Transform parent, Vector2 pos, Sprite sprite)
    {
        GameObject barrel = new GameObject(name);
        barrel.transform.SetParent(parent, false);
        barrel.transform.position = pos;
        barrel.transform.localScale = Vector3.one * 1.5f;

        SpriteRenderer sr = barrel.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 2;

        CircleCollider2D col = barrel.AddComponent<CircleCollider2D>();
        col.radius = 0.55f;
        col.isTrigger = true;
    }

    private static void CreateParkedTruck(string name, Transform parent, Vector2 pos, float rotAngleDeg, Sprite tractorSprite, Sprite trailerSprite)
    {
        GameObject rig = new GameObject(name);
        rig.transform.SetParent(parent, false);
        rig.transform.position = pos;
        rig.transform.rotation = Quaternion.Euler(0f, 0f, rotAngleDeg);

        // Trailer
        GameObject trailerGo = new GameObject("Trailer");
        trailerGo.transform.SetParent(rig.transform, false);
        trailerGo.transform.localPosition = Vector3.zero;

        SpriteRenderer trSr = trailerGo.AddComponent<SpriteRenderer>();
        trSr.sprite = trailerSprite;
        trSr.sortingOrder = 7;

        BoxCollider2D trCol = trailerGo.AddComponent<BoxCollider2D>();
        trCol.size = new Vector2(2.58f, 15.9f);
        trCol.isTrigger = true;

        // Tractor parked in front of trailer (hitch connected)
        // Trailer kingpin at +7.7m connects to Tractor hitch at -2.9m -> tractor offset = +10.6m
        GameObject tractorGo = new GameObject("Tractor");
        tractorGo.transform.SetParent(rig.transform, false);
        tractorGo.transform.localPosition = new Vector3(0f, 10.6f, 0f);

        SpriteRenderer tSr = tractorGo.AddComponent<SpriteRenderer>();
        tSr.sprite = tractorSprite;
        // Parked truck color: classic red semi cab
        tSr.color = new Color(0.95f, 0.28f, 0.28f, 1f);
        tSr.sortingOrder = 8;

        BoxCollider2D tCol = tractorGo.AddComponent<BoxCollider2D>();
        tCol.size = new Vector2(2.55f, 8.2f);
        tCol.isTrigger = true;
    }

    private static void CleanExistingObjects()
    {
        string[] targetNames = { "Tractor", "Trailer", "TruckControlsCanvas", "SteeringWheelCanvas", "TrainingYard" };
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var root in rootObjects)
        {
            if (root == null) continue;

            foreach (var target in targetNames)
            {
                if (root.name == target || root.name.StartsWith(target + " ") || root.name.StartsWith(target + "("))
                {
                    Undo.DestroyObjectImmediate(root);
                    break;
                }
            }
        }
    }

    private static void EnsureEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esGo, "Setup EventSystem");
        }
        else if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static GameObject CreateWheelChild(string name, Transform parent, Vector3 localPos, Sprite sprite)
    {
        GameObject wheel = new GameObject(name);
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPos;
        wheel.transform.localScale = Vector3.one; // Natural 1:1 scale (0.45m x 0.95m at 100 PPU)

        SpriteRenderer sr = wheel.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 6; // Underneath vehicle body & fenders
        return wheel;
    }

    private static GameObject CreateControlsCanvas(Sprite steeringWheelSprite, Sprite pedalGasSprite, Sprite pedalBrakeSprite)
    {
        GameObject canvasGo = new GameObject("TruckControlsCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Crash overlay image (red flash on collision)
        GameObject flashGo = new GameObject("CrashFlashOverlay");
        flashGo.transform.SetParent(canvasGo.transform, false);
        RectTransform flashRt = flashGo.AddComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.sizeDelta = Vector2.zero;
        Image flashImg = flashGo.AddComponent<Image>();
        flashImg.color = new Color(1f, 0.1f, 0.1f, 0f);
        flashImg.raycastTarget = false;
        flashGo.SetActive(false);

        // Steering Wheel (bottom-right corner, matching reference)
        GameObject wheelGo = new GameObject("SteeringWheel");
        wheelGo.transform.SetParent(canvasGo.transform, false);

        RectTransform wheelRt = wheelGo.AddComponent<RectTransform>();
        wheelRt.anchorMin = new Vector2(1f, 0f);
        wheelRt.anchorMax = new Vector2(1f, 0f);
        wheelRt.pivot = new Vector2(0.5f, 0.5f);
        wheelRt.anchoredPosition = new Vector2(-240f, 240f);
        wheelRt.sizeDelta = new Vector2(400f, 400f);

        Image wheelImg = wheelGo.AddComponent<Image>();
        wheelImg.sprite = steeringWheelSprite;
        wheelImg.raycastTarget = true;

        wheelGo.AddComponent<SteeringWheelUI>();

        // Pedals (bottom-left corner)
        // 1. Gas Pedal (Upper Left)
        GameObject gasGo = new GameObject("Pedal_Gas");
        gasGo.transform.SetParent(canvasGo.transform, false);

        RectTransform gasRt = gasGo.AddComponent<RectTransform>();
        gasRt.anchorMin = new Vector2(0f, 0f);
        gasRt.anchorMax = new Vector2(0f, 0f);
        gasRt.pivot = new Vector2(0.5f, 0.5f);
        gasRt.anchoredPosition = new Vector2(130f, 380f);
        gasRt.sizeDelta = new Vector2(120f, 240f);

        Image gasImg = gasGo.AddComponent<Image>();
        gasImg.sprite = pedalGasSprite;
        gasImg.raycastTarget = true;
        gasImg.color = new Color(1f, 1f, 1f, 0.8f);

        PedalUI gasPedal = gasGo.AddComponent<PedalUI>();
        gasPedal.SetPedalType(PedalUI.PedalType.Gas);

        // 2. Brake / Reverse Pedal (Lower Left)
        GameObject brakeGo = new GameObject("Pedal_Brake");
        brakeGo.transform.SetParent(canvasGo.transform, false);

        RectTransform brakeRt = brakeGo.AddComponent<RectTransform>();
        brakeRt.anchorMin = new Vector2(0f, 0f);
        brakeRt.anchorMax = new Vector2(0f, 0f);
        brakeRt.pivot = new Vector2(0.5f, 0.5f);
        brakeRt.anchoredPosition = new Vector2(130f, 160f);
        brakeRt.sizeDelta = new Vector2(150f, 150f);

        Image brakeImg = brakeGo.AddComponent<Image>();
        brakeImg.sprite = pedalBrakeSprite;
        brakeImg.raycastTarget = true;
        brakeImg.color = new Color(1f, 1f, 1f, 0.8f);

        PedalUI brakePedal = brakeGo.AddComponent<PedalUI>();
        brakePedal.SetPedalType(PedalUI.PedalType.BrakeReverse);

        // Dashboard HUD Text
        GameObject hudGo = new GameObject("TruckHUDText");
        hudGo.transform.SetParent(canvasGo.transform, false);

        RectTransform hudRt = hudGo.AddComponent<RectTransform>();
        hudRt.anchorMin = new Vector2(0.5f, 0.94f);
        hudRt.anchorMax = new Vector2(0.5f, 0.94f);
        hudRt.pivot = new Vector2(0.5f, 0.5f);
        hudRt.anchoredPosition = Vector2.zero;
        hudRt.sizeDelta = new Vector2(1200, 50);

        Text hudText = hudGo.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) hudText.font = font;
        hudText.fontSize = 22;
        hudText.alignment = TextAnchor.MiddleCenter;
        hudText.color = new Color(1f, 0.92f, 0.3f, 1f);
        hudText.text = "SPEED: 0 km/h | GEAR: [N] | РУЛЬ: 0° | СЦЕПКА: 0° | [C] КАМЕРА: СВЕРХУ";

        // Big Crash Banner Text
        GameObject crashTextGo = new GameObject("CrashBannerText");
        crashTextGo.transform.SetParent(canvasGo.transform, false);

        RectTransform crashRt = crashTextGo.AddComponent<RectTransform>();
        crashRt.anchorMin = new Vector2(0.5f, 0.65f);
        crashRt.anchorMax = new Vector2(0.5f, 0.65f);
        crashRt.pivot = new Vector2(0.5f, 0.5f);
        crashRt.anchoredPosition = Vector2.zero;
        crashRt.sizeDelta = new Vector2(950, 80);

        Text crashText = crashTextGo.AddComponent<Text>();
        if (font != null) crashText.font = font;
        crashText.fontSize = 38;
        crashText.fontStyle = FontStyle.Bold;
        crashText.alignment = TextAnchor.MiddleCenter;
        crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
        crashText.text = "💥 БУХ! ВРЕЗАЛСЯ В ПРЕПЯТСТВИЕ! 💥";
        crashTextGo.SetActive(false);

        // Map Switch Button in top-left corner
        GameObject switchBtnGo = new GameObject("MapSwitchButton");
        switchBtnGo.transform.SetParent(canvasGo.transform, false);

        RectTransform switchRt = switchBtnGo.AddComponent<RectTransform>();
        switchRt.anchorMin = new Vector2(0f, 1f);
        switchRt.anchorMax = new Vector2(0f, 1f);
        switchRt.pivot = new Vector2(0f, 1f);
        switchRt.anchoredPosition = new Vector2(25f, -25f);
        switchRt.sizeDelta = new Vector2(240f, 50f);

        Image switchImg = switchBtnGo.AddComponent<Image>();
        switchImg.color = new Color(0.12f, 0.45f, 0.85f, 1.0f);

        Button switchBtn = switchBtnGo.AddComponent<Button>();
        LevelSwitcher switcher = switchBtnGo.AddComponent<LevelSwitcher>();
        switcher.TargetSceneName = "Level2_AlleyDock";

        GameObject switchTextGo = new GameObject("Text");
        switchTextGo.transform.SetParent(switchBtnGo.transform, false);
        RectTransform textRt = switchTextGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.pivot = new Vector2(0.5f, 0.5f);

        Text switchText = switchTextGo.AddComponent<Text>();
        if (font != null) switchText.font = font;
        switchText.fontSize = 17;
        switchText.fontStyle = FontStyle.Bold;
        switchText.alignment = TextAnchor.MiddleCenter;
        switchText.color = Color.white;
        switchText.horizontalOverflow = HorizontalWrapMode.Overflow;
        switchText.verticalOverflow = VerticalWrapMode.Overflow;
        switchText.text = "КАРТА 2 (БОКС) [M]";

        // Crash effect manager
        canvasGo.AddComponent<TruckCrashEffect>();

        return canvasGo;
    }

    private static void EnsureSpriteAssets(bool forceOverwrite = false)
    {
        string fullDir = Path.Combine(Application.dataPath, "GeneratedSprites");
        if (!Directory.Exists(fullDir))
        {
            Directory.CreateDirectory(fullDir);
        }

        SaveAndImportPng($"{SpritesDir}/Tractor.png", GenerateTractorPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/Trailer.png", GenerateTrailerPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/Tire.png", GenerateTirePngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/SteeringWheel.png", GenerateSteeringWheelPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/TrafficCone.png", GenerateConePngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/ConcreteBarrier.png", GenerateBarrierPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/Barrel.png", GenerateBarrelPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/AsphaltGround.png", GenerateAsphaltPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/ImpactSpark.png", GenerateImpactSparkPngBytes(), forceOverwrite);
    }

    private static Sprite LoadSpriteSafe(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            bool needReimport = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needReimport = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritesheet = new SpriteMetaData[0];
                needReimport = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                needReimport = true;
            }
            if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.1f)
            {
                importer.spritePixelsPerUnit = 100f;
                needReimport = true;
            }

            if (needReimport)
            {
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        // 1. Try direct asset load
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;

        // 2. Try loading from sub-assets if any
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object obj in allAssets)
        {
            if (obj is Sprite s) return s;
        }

        // 3. Fallback: Load raw Texture2D and create Sprite in-memory
        Texture2D rawTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (rawTex != null)
        {
            return Sprite.Create(rawTex, new Rect(0, 0, rawTex.width, rawTex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        return null;
    }

    private static void SaveAndImportPng(string assetPath, byte[] pngBytes, bool forceOverwrite = false)
    {
        string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!File.Exists(fullPath) || forceOverwrite)
        {
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritesheet = new SpriteMetaData[0];
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
    }

    private static byte[] GenerateTractorPngBytes()
    {
        // 260 x 850 px = 2.6m x 8.5m real American Class 8 Conventional Semi
        int w = 260, h = 850;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];

        Color frameDark = new Color(0.12f, 0.12f, 0.14f, 1f);
        Color cabColor = new Color(0.82f, 0.12f, 0.14f, 1f); // Kenworth Crimson Red
        Color cabHighlight = new Color(0.95f, 0.22f, 0.24f, 1f);
        Color chrome = new Color(0.94f, 0.95f, 0.97f, 1f);
        Color chromeShade = new Color(0.68f, 0.70f, 0.74f, 1f);
        Color glass = new Color(0.12f, 0.22f, 0.32f, 1f);
        Color glassReflection = new Color(0.55f, 0.85f, 0.98f, 0.9f);
        Color lightGlow = new Color(1f, 0.96f, 0.55f, 1f);
        Color amberLight = new Color(1f, 0.65f, 0.1f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color col = Color.clear;

                // 1. Rear Frame Chassis (y: 40 to 340)
                if (x >= 70 && x <= 190 && y >= 40 && y <= 340)
                {
                    col = frameDark;

                    // Diamond plate deck
                    if ((x + y) % 6 == 0) col = new Color(0.18f, 0.18f, 0.20f, 1f);

                    // 5th Wheel Hitch Plate (centered around y = 165)
                    float distHitch = Vector2.Distance(new Vector2(x, y), new Vector2(130f, 165f));
                    if (distHitch <= 34f) col = chromeShade;
                    if (distHitch <= 10f) col = new Color(0.06f, 0.06f, 0.06f, 1f);
                }

                // Rear Mudflaps (y: 25 to 55)
                if (y >= 25 && y <= 55 && ((x >= 25 && x <= 75) || (x >= 185 && x <= 235)))
                {
                    col = new Color(0.08f, 0.08f, 0.08f, 1f);
                    if (y >= 30 && y <= 38 && (x == 50 || x == 210)) col = Color.red; // Red reflector
                }

                // Chrome quarter fenders over tandem wheels (y: 80 to 250)
                if (y >= 80 && y <= 250 && ((x >= 28 && x <= 40) || (x >= 220 && x <= 232)))
                {
                    col = chrome;
                }

                // 2. Chrome Cylindrical Side Fuel Tanks (y: 300 to 460)
                if (y >= 300 && y <= 460)
                {
                    if ((x >= 16 && x <= 58) || (x >= 202 && x <= 244))
                    {
                        col = chrome;
                        float xRatio = (x <= 58) ? (x - 16) / 42f : (x - 202) / 42f;
                        if (xRatio < 0.2f || xRatio > 0.8f) col = chromeShade;
                        // Tank mounting straps
                        if (y == 335 || y == 336 || y == 420 || y == 421) col = new Color(0.1f, 0.1f, 0.1f, 1f);
                    }
                }

                // 3. Dual Vertical Chrome Exhaust Stacks (y: 430 to 490)
                if (y >= 430 && y <= 490)
                {
                    if ((x >= 46 && x <= 66) || (x >= 194 && x <= 214))
                    {
                        col = chrome;
                        if (y >= 475) col = new Color(0.12f, 0.12f, 0.12f, 1f);
                    }
                }

                // 4. Sleeper Cab (y: 330 to 520, width: 60 to 200)
                if (x >= 60 && x <= 200 && y >= 330 && y <= 520)
                {
                    col = cabColor;
                    if (x < 66 || x > 194 || y < 336) col = cabHighlight;
                }

                // 5. Main Cab & Long Hood (y: 520 to 790)
                float hoodHalfWidth = Mathf.Lerp(62f, 50f, (y - 520f) / 270f);
                if (Mathf.Abs(x - 130f) <= hoodHalfWidth && y >= 520 && y <= 790)
                {
                    col = cabColor;

                    // Center chrome spear down hood
                    if (Mathf.Abs(x - 130f) <= 2.5f && y >= 550 && y <= 775)
                    {
                        col = chrome;
                    }

                    // Curved Windshield with sun tint (y: 525 to 585)
                    if (y >= 525 && y <= 585 && Mathf.Abs(x - 130f) <= hoodHalfWidth - 6f)
                    {
                        col = glass;
                        // Diagonal windshield glass reflection
                        if (Mathf.Abs((x - 100f) - (y - 525f) * 1.3f) < 8f) col = glassReflection;
                    }

                    // Chrome sun visor (y: 585 to 595)
                    if (y >= 585 && y <= 595 && Mathf.Abs(x - 130f) <= hoodHalfWidth - 2f)
                    {
                        col = chrome;
                    }

                    // 5 Amber Roof Clearance Lights (y: 520)
                    if (y >= 518 && y <= 524)
                    {
                        if (Mathf.Abs(x - 130f) <= 3f || Mathf.Abs(Mathf.Abs(x - 130f) - 22f) <= 3f || Mathf.Abs(Mathf.Abs(x - 130f) - 44f) <= 3f)
                        {
                            col = amberLight;
                        }
                    }
                }

                // 6. Chrome Front Bumper & Grill (y: 785 to 835)
                if (y >= 785 && y <= 835 && x >= 38 && x <= 222)
                {
                    col = chrome;

                    // Central dark vertical grill louvers
                    if (y >= 792 && y <= 830 && x >= 82 && x <= 178)
                    {
                        col = new Color(0.12f, 0.12f, 0.15f, 1f);
                        if (x % 6 == 0) col = chromeShade;
                    }

                    // Dual headlights on bumper corners
                    if (y >= 795 && y <= 822 && ((x >= 48 && x <= 72) || (x >= 188 && x <= 212)))
                    {
                        col = lightGlow;
                        if (x == 60 || x == 200) col = Color.white;
                    }
                }

                // 7. Chrome West Coast Side Mirrors (y: 545 to 595)
                if (y >= 545 && y <= 595)
                {
                    if ((x >= 22 && x <= 38) || (x >= 222 && x <= 238))
                    {
                        col = chrome;
                        if (y >= 552 && y <= 588 && (x >= 26 && x <= 34 || x >= 226 && x <= 234))
                        {
                            col = glass;
                        }
                    }
                    // Mirror support brackets
                    if ((x >= 38 && x <= 62 || x >= 198 && x <= 222) && (y == 555 || y == 585))
                    {
                        col = chromeShade;
                    }
                }

                colors[y * w + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateTrailerPngBytes()
    {
        // 260 x 1620 px = 2.6m x 16.2m real 53-ft Dry Van / Reefer Semi-Trailer
        int w = 260, h = 1620;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];

        Color trailerBody = new Color(0.93f, 0.93f, 0.95f, 1f); // Metallic silver/white
        Color trailerGroove = new Color(0.82f, 0.82f, 0.85f, 1f); // Corrugation shadow
        Color border = new Color(0.25f, 0.25f, 0.28f, 1f);
        Color reeferUnit = new Color(0.35f, 0.38f, 0.42f, 1f); // Front Thermo King
        Color tailLight = new Color(0.95f, 0.12f, 0.12f, 1f);
        Color chromeBar = new Color(0.88f, 0.88f, 0.92f, 1f);
        Color greenStatusLight = new Color(0.1f, 0.95f, 0.2f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color col = Color.clear;

                // Main Trailer Cargo Box (x: 12 to 248, y: 35 to 1585)
                if (x >= 12 && x <= 248 && y >= 35 && y <= 1585)
                {
                    if (x <= 18 || x >= 242 || y <= 40 || y >= 1580)
                    {
                        col = border;
                    }
                    else
                    {
                        col = trailerBody;

                        // Deep horizontal corrugation paneling
                        if ((y - 40) % 28 < 3)
                        {
                            col = trailerGroove;
                        }
                    }

                    // 1. Front Refrigeration Unit (Thermo King) at front top (y: 1510 to 1575)
                    if (y >= 1510 && y <= 1575 && x >= 70 && x <= 190)
                    {
                        col = reeferUnit;
                        float distFan = Vector2.Distance(new Vector2(x, y), new Vector2(130f, 1545f));
                        if (distFan <= 22f) col = new Color(0.15f, 0.15f, 0.18f, 1f);
                        if (distFan <= 8f) col = Color.gray;
                        // Status indicator light (green = active reefer)
                        if (y >= 1565 && y <= 1570 && x >= 78 && x <= 86) col = greenStatusLight;
                    }

                    // 2. Landing Gear (Trailer Jacks) behind kingpin (y: 1140 to 1180)
                    if (y >= 1140 && y <= 1180 && ((x >= 22 && x <= 34) || (x >= 226 && x <= 238)))
                    {
                        col = new Color(0.2f, 0.2f, 0.22f, 1f);
                    }

                    // 3. Rear Swing Barn Doors (y: 40 to 120)
                    if (y >= 40 && y <= 120)
                    {
                        // Center door split line
                        if (x == 129 || x == 130) col = border;

                        // Dual vertical chrome lock rods
                        if ((x >= 76 && x <= 81) || (x >= 179 && x <= 184))
                        {
                            col = chromeBar;
                        }

                        // Door latches
                        if ((y == 75 || y == 76) && ((x >= 72 && x <= 85) || (x >= 175 && x <= 188)))
                        {
                            col = new Color(0.12f, 0.12f, 0.12f, 1f);
                        }
                    }

                    // 4. Rear Safety DOT Red/White Reflective Tape along bumper (y: 35 to 42)
                    if (y >= 35 && y <= 42 && x >= 20 && x <= 240)
                    {
                        col = ((x / 14) % 2 == 0) ? Color.red : Color.white;
                    }

                    // Rear LED Brake & Turn Lights (y: 38 to 52)
                    if (y >= 38 && y <= 52 && ((x >= 24 && x <= 46) || (x >= 214 && x <= 236)))
                    {
                        col = tailLight;
                    }

                    // Front Kingpin indicator (y: 1475 to 1485)
                    if (y >= 1475 && y <= 1485 && Mathf.Abs(x - 130f) <= 10f)
                    {
                        col = new Color(0.1f, 0.1f, 0.12f, 1f);
                    }
                }

                colors[y * w + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateConePngBytes()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Color neonOrange = new Color(1f, 0.28f, 0.0f, 1f);
        Color baseOrange = new Color(0.85f, 0.22f, 0.0f, 1f);
        Color darkBaseBorder = new Color(0.12f, 0.12f, 0.12f, 1f);
        Color whiteStripe = new Color(0.98f, 0.98f, 1.0f, 1f);
        Color shadow = new Color(0.05f, 0.05f, 0.05f, 0.35f);
        Color blackTip = new Color(0.12f, 0.12f, 0.12f, 1f);
        Vector2 center = new Vector2(64f, 64f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color col = Color.clear;

                float distShadow = Vector2.Distance(new Vector2(x, y), new Vector2(66f, 60f));
                if (distShadow <= 52f) col = shadow;

                if (x >= 18 && x <= 110 && y >= 18 && y <= 110)
                {
                    col = (x <= 22 || x >= 106 || y <= 22 || y >= 106) ? darkBaseBorder : baseOrange;
                }

                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= 44f)
                {
                    col = neonOrange;
                    if (dist >= 21f && dist <= 33f) col = whiteStripe;
                    else if (dist <= 8f) col = blackTip;
                }

                colors[y * size + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateBarrierPngBytes()
    {
        int w = 128, h = 40;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];

        Color concrete = new Color(0.72f, 0.73f, 0.75f, 1f);
        Color concreteDark = new Color(0.55f, 0.56f, 0.58f, 1f);
        Color yellow = new Color(0.95f, 0.8f, 0.1f, 1f);
        Color black = new Color(0.15f, 0.15f, 0.15f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color col = concrete;
                if (x < 4 || x >= w - 4 || y < 4 || y >= h - 4) col = concreteDark;
                if (y >= 8 && y <= h - 8)
                {
                    bool stripe = ((x + y) / 14) % 2 == 0;
                    col = stripe ? yellow : black;
                }
                colors[y * w + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateBarrelPngBytes()
    {
        int size = 48;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Color blueDrum = new Color(0.12f, 0.38f, 0.75f, 1f);
        Color darkBlue = new Color(0.08f, 0.25f, 0.52f, 1f);
        Color bungCap = new Color(0.85f, 0.85f, 0.88f, 1f);
        Vector2 center = new Vector2(24f, 24f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color col = Color.clear;
                float dist = Vector2.Distance(new Vector2(x, y), center);

                if (dist <= 22f)
                {
                    col = blueDrum;
                    if (dist >= 19f && dist <= 22f) col = darkBlue;
                    if (dist >= 10f && dist <= 12f) col = darkBlue;

                    if (Vector2.Distance(new Vector2(x, y), new Vector2(16f, 24f)) <= 2.5f) col = bungCap;
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(30f, 28f)) <= 2f) col = bungCap;
                }

                colors[y * size + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateAsphaltPngBytes()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];

        Color asphaltBase = new Color(0.18f, 0.19f, 0.21f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noise = (Mathf.Sin(x * 12.3f + y * 7.7f) * 0.03f) + (Mathf.Cos(x * 3.1f - y * 19.1f) * 0.02f);
                Color col = new Color(
                    Mathf.Clamp01(asphaltBase.r + noise),
                    Mathf.Clamp01(asphaltBase.g + noise),
                    Mathf.Clamp01(asphaltBase.b + noise),
                    1f
                );
                colors[y * size + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateTirePngBytes()
    {
        // 45 x 95 px commercial truck dual tire
        int w = 45, h = 95;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];

        Color rubber = new Color(0.11f, 0.11f, 0.13f, 1f);
        Color rim = new Color(0.75f, 0.77f, 0.80f, 1f);
        Color rimShade = new Color(0.45f, 0.47f, 0.50f, 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color col = rubber;
                // Tread grooves
                if (x % 11 == 0 && (y < 20 || y > 75)) col = new Color(0.06f, 0.06f, 0.07f, 1f);

                // Chrome wheel rim in center
                if (x >= 10 && x <= 34 && y >= 25 && y <= 70)
                {
                    col = rim;
                    if (x <= 13 || x >= 31 || y <= 28 || y >= 67) col = rimShade;
                    if (x >= 18 && x <= 26 && y >= 43 && y <= 52) col = new Color(0.1f, 0.1f, 0.12f, 1f);
                }
                colors[y * w + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateSteeringWheelPngBytes()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.46f;
        float thickness = size * 0.08f;
        float hubRadius = size * 0.16f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                Color col = Color.clear;

                if (dist <= radius && dist >= radius - thickness)
                {
                    col = new Color(0.18f, 0.18f, 0.22f, 0.95f);
                    if (y > center.y + radius * 0.65f && Mathf.Abs(x - center.x) < size * 0.04f)
                    {
                        col = new Color(0.95f, 0.15f, 0.15f, 1f);
                    }
                }
                else if (dist <= hubRadius)
                {
                    col = new Color(0.25f, 0.25f, 0.3f, 0.95f);
                }
                else if (dist < radius - thickness && dist > hubRadius)
                {
                    float dy = y - center.y;
                    float dx = x - center.x;
                    if (Mathf.Abs(dy) < size * 0.035f) col = new Color(0.3f, 0.3f, 0.35f, 0.9f);
                    if (Mathf.Abs(dx) < size * 0.035f && dy < 0) col = new Color(0.3f, 0.3f, 0.35f, 0.9f);
                }

                colors[y * size + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }

    private static byte[] GenerateImpactSparkPngBytes()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(32f, 32f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                Color col = Color.clear;

                if (dist <= 28f)
                {
                    float factor = 1f - (dist / 28f);
                    col = Color.Lerp(new Color(1f, 0.45f, 0.05f, factor), Color.white, factor * factor);

                    float dx = Mathf.Abs(x - 32);
                    float dy = Mathf.Abs(y - 32);
                    if (dx <= 2 || dy <= 2 || Mathf.Abs(dx - dy) <= 2)
                    {
                        col = Color.Lerp(col, Color.white, 0.85f);
                    }
                }

                colors[y * size + x] = col;
            }
        }

        tex.SetPixels(colors);
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return bytes;
    }
}
