using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PopulateA2Map
{
    public const string ScenePath = "Assets/Scenes/CustomMaps/A2.unity";
    private const string SpritesDir = "Assets/GeneratedSprites";

    static PopulateA2Map()
    {
        EditorApplication.delayCall += () =>
        {
            if (!SessionState.GetBool("A2_Map_Slalom_Done_V2", false))
            {
                SessionState.SetBool("A2_Map_Slalom_Done_V2", true);
                PopulateA2();
            }
        };
    }

    [MenuItem("Tools/Map Builder/Модернизировать карту A2 (Слаломный лабиринт)", false, 60)]
    public static void PopulateA2()
    {
        Debug.Log("<color=#33ccff>[PopulateA2Map] Пересборка карты A2: Слаломный лабиринт с препятствиями...</color>");

        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[PopulateA2Map] Файл сцены {ScenePath} не найден!");
            return;
        }

        // Open A2 scene
        Scene scene;
        bool wasOpen = SceneManager.GetActiveScene().path == ScenePath;
        if (!wasOpen)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[PopulateA2Map] Пользователь отменил открытие сцены.");
                return;
            }
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        else
        {
            scene = SceneManager.GetActiveScene();
        }

        GameObject workspace = GameObject.Find("MapBuilder_Workspace");
        if (workspace == null)
        {
            Debug.LogError("[PopulateA2Map] Не найден объект MapBuilder_Workspace на сцене A2!");
            return;
        }

        // 1. Ensure Map Dimensions
        MapData mapData = workspace.GetComponent<MapData>();
        if (mapData != null)
        {
            mapData.SetDimensions(19.5f, 250.0f);
        }

        // 2. Load Sprites
        Sprite carSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/PassengerCar.png");
        Sprite tractorSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/Tractor_HD.png") 
                            ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/Tractor.png");
        Sprite trailerSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/Trailer.png");
        Sprite wheelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/Tire.png");

        // Helper function to clean or create containers under workspace
        Transform GetCleanContainer(string name)
        {
            Transform existing = workspace.transform.Find(name);
            if (existing != null)
            {
                List<GameObject> toDestroy = new List<GameObject>();
                for (int i = 0; i < existing.childCount; i++)
                {
                    toDestroy.Add(existing.GetChild(i).gameObject);
                }
                foreach (var go in toDestroy)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return existing;
            }
            GameObject newGo = new GameObject(name);
            newGo.transform.SetParent(workspace.transform, false);
            return newGo.transform;
        }

        Transform truckContainer = GetCleanContainer("StandaloneTrucks");
        Transform carContainer = GetCleanContainer("PassengerCars");
        Transform propContainer = GetCleanContainer("Props");
        Transform routeContainer = GetCleanContainer("RouteLines");

        // =========================================================================
        // 3. SPAWN 8 STANDALONE TRUCKS IN STAGGERED SLALOM PATTERN
        // (Positioned directly across lanes to force swerving and lane changes)
        // =========================================================================
        var truckDefs = new (string name, Vector2 pos, float rot, Color cabColor)[]
        {
            ("TruckObstacle_1", new Vector2(2.8f, 25.0f),   0f,   new Color(0.18f, 0.48f, 0.88f)), // Blue - left wall next to start
            ("TruckObstacle_2", new Vector2(7.68f, 52.0f),  0f,   new Color(0.95f, 0.48f, 0.10f)), // Orange - directly in front of start! Forces swerve right
            ("TruckObstacle_3", new Vector2(5.8f, 90.0f),   0f,   new Color(0.88f, 0.15f, 0.15f)), // Red - center-left obstacle, keep right
            ("TruckObstacle_4", new Vector2(2.2f, 105.0f),  0f,   new Color(0.15f, 0.70f, 0.85f)), // Cyan - far-left obstacle
            ("TruckObstacle_5", new Vector2(15.0f, 130.0f), 180f, new Color(0.96f, 0.82f, 0.12f)), // Yellow - blocks right channel! Forces swerve left
            ("TruckObstacle_6", new Vector2(3.2f, 162.0f),  0f,   new Color(0.18f, 0.65f, 0.28f)), // Green - blocks left channel! Forces swerve right
            ("TruckObstacle_7", new Vector2(15.5f, 195.0f), 180f, new Color(0.60f, 0.15f, 0.25f)), // Burgundy - blocks right channel! Forces swerve left
            ("TruckObstacle_8", new Vector2(15.0f, 235.0f), 0f,   new Color(0.82f, 0.84f, 0.88f))  // Silver - parked in bay adjacent to Target Dock
        };

        for (int i = 0; i < truckDefs.Length; i++)
        {
            var def = truckDefs[i];
            GameObject truckGo = new GameObject(def.name);
            truckGo.transform.SetParent(truckContainer, false);
            StandaloneTruckObstacle truckObs = truckGo.AddComponent<StandaloneTruckObstacle>();
            truckObs.Setup(def.pos, def.rot, def.cabColor, tractorSprite, trailerSprite, wheelSprite);
        }

        // =========================================================================
        // 4. SPAWN 7 PASSENGER CARS IN WALL POCKETS
        // =========================================================================
        var carDefs = new (Vector2 pos, float rot, Color color)[]
        {
            (new Vector2(2.5f, 40.0f),  90f, new Color(0.82f, 0.84f, 0.88f)), // Silver - left wall
            (new Vector2(2.5f, 45.0f),  90f, new Color(0.88f, 0.15f, 0.15f)), // Red - left wall
            (new Vector2(18.2f, 85.0f),  0f,  new Color(0.18f, 0.48f, 0.88f)), // Blue - right wall
            (new Vector2(2.5f, 135.0f), 90f, Color.white),                    // White - left wall
            (new Vector2(2.5f, 140.0f), 90f, new Color(0.95f, 0.48f, 0.10f)), // Orange - left wall
            (new Vector2(18.2f, 175.0f), 0f,  new Color(0.96f, 0.82f, 0.12f)), // Yellow - right wall
            (new Vector2(2.5f, 220.0f), 90f, new Color(0.25f, 0.25f, 0.28f))  // Dark Grey - near dock
        };

        for (int i = 0; i < carDefs.Length; i++)
        {
            var def = carDefs[i];
            GameObject carGo = new GameObject($"PassengerCar_{i + 1}");
            carGo.transform.SetParent(carContainer, false);
            PassengerCarObstacle carObs = carGo.AddComponent<PassengerCarObstacle>();
            carObs.Setup(def.pos, def.rot, def.color, carSprite);
        }

        // =========================================================================
        // 5. SPAWN 12 PROPS (Corner cones and dock barrels)
        // =========================================================================
        var propDefs = new (PropType type, Vector2 pos, float rot, string name)[]
        {
            // Traffic cones marking truck corners for clear visibility
            (PropType.Cone, new Vector2(9.2f, 40.0f), 0f, "Cone_T2_RearRight"),
            (PropType.Cone, new Vector2(9.2f, 63.0f), 0f, "Cone_T2_FrontRight"),
            (PropType.Cone, new Vector2(7.3f, 79.0f), 0f, "Cone_T3_RearRight"),
            (PropType.Cone, new Vector2(7.3f, 101.0f), 0f, "Cone_T3_FrontRight"),
            (PropType.Cone, new Vector2(13.5f, 118.0f), 0f, "Cone_T5_FrontLeft"),
            (PropType.Cone, new Vector2(13.5f, 142.0f), 0f, "Cone_T5_RearLeft"),
            (PropType.Cone, new Vector2(4.7f, 151.0f), 0f, "Cone_T6_RearRight"),
            (PropType.Cone, new Vector2(4.7f, 173.0f), 0f, "Cone_T6_FrontRight"),
            (PropType.Cone, new Vector2(14.0f, 183.0f), 0f, "Cone_T7_FrontLeft"),
            (PropType.Cone, new Vector2(14.0f, 207.0f), 0f, "Cone_T7_RearLeft"),
            // Dock safety barrels
            (PropType.Barrel, new Vector2(5.5f, 237.0f), 0f, "Barrel_DockLeft"),
            (PropType.Barrel, new Vector2(11.6f, 237.0f), 0f, "Barrel_DockRight")
        };

        for (int i = 0; i < propDefs.Length; i++)
        {
            var def = propDefs[i];
            GameObject propGo = new GameObject(def.name);
            propGo.transform.SetParent(propContainer, false);
            PropObstacle propObs = propGo.AddComponent<PropObstacle>();
            Sprite propSp = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/{PropObstacle.GetSpriteFileName(def.type)}");
            propObs.Setup(def.type, def.pos, def.rot, null, propSp);
        }

        // =========================================================================
        // 6. SPAWN SLALOM ROUTE GUIDE LINE (Exact centerline matching the weave)
        // =========================================================================
        GameObject routeGo = new GameObject("Route_Centerline");
        routeGo.transform.SetParent(routeContainer, false);
        RouteGuideLine route = routeGo.AddComponent<RouteGuideLine>();
        route.waypoints = new List<Vector2>
        {
            new Vector2(7.68f, 18.15f),
            new Vector2(7.68f, 26.0f),
            new Vector2(14.0f, 48.0f),   // Swerve right around T2
            new Vector2(14.2f, 85.0f),   // Right channel
            new Vector2(14.0f, 100.0f),  // Start weaving left before T5
            new Vector2(7.2f, 122.0f),   // Weave left past T3/T4 and T5
            new Vector2(7.2f, 142.0f),   // Center-left channel
            new Vector2(12.2f, 165.0f),  // Weave right past T6
            new Vector2(12.0f, 175.0f),  // Right channel before T7
            new Vector2(8.56f, 198.0f),  // Weave left into dock approach
            new Vector2(8.56f, 237.13f)  // Straight into TargetParkingSlot
        };
        route.lineWidth = 0.35f;
        route.lineColor = new Color(1.0f, 0.835f, 0.0f, 0.55f);
        route.smoothing = RouteGuideLine.SmoothingMode.CatmullRom;
        route.curveResolution = 12;
        route.showEndArrow = true;
        route.arrowSize = 1.25f;
        route.displayMode = RouteGuideLine.DisplayMode.AlwaysVisible;
        route.UpdateVisuals();

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"<color=#55ff55>[PopulateA2Map] Сцена '{ScenePath}' успешно пересобрана (Слаломный лабиринт)!</color>");
    }
}
