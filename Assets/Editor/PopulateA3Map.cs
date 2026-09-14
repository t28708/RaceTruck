using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PopulateA3Map
{
    public const string ScenePath = "Assets/Scenes/CustomMaps/A3.unity";
    private const string SpritesDir = "Assets/GeneratedSprites";

    [MenuItem("Tools/Map Builder/Модернизировать карту A3 (Экстремальный слалом с заломом)", false, 61)]
    public static void PopulateA3()
    {
        Debug.Log("<color=#ffaa00>[PopulateA3Map] Пересборка карты A3: Экстремальный слалом с заломом автопоезда...</color>");

        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[PopulateA3Map] Файл сцены {ScenePath} не найден!");
            return;
        }

        // Open A3 scene
        Scene scene;
        bool wasOpen = SceneManager.GetActiveScene().path == ScenePath;
        if (!wasOpen)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[PopulateA3Map] Отмена открытия сцены пользователем.");
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
            Debug.LogError("[PopulateA3Map] Не найден объект MapBuilder_Workspace на сцене A3!");
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

        // Helper to clean/create container under workspace
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
        // 3. SPAWN 11 STANDALONE TRUCKS (EXPERT SLALOM WITH ARTICULATION & ANGLES)
        // =========================================================================
        var truckDefs = new (string name, Vector2 pos, float rot, Color cabColor)[]
        {
            // Zone 1: Launch & 45-degree bottleneck
            ("TruckObstacle_1",  new Vector2(2.8f,  26.0f),   0f,   new Color(0.18f, 0.48f, 0.88f)), // Blue - left wall next to start
            ("TruckObstacle_2",  new Vector2(11.2f, 48.0f),  45f,   new Color(0.95f, 0.48f, 0.10f)), // Orange - 45° diagonal wedge! Corner blocks direct lane
            // Zone 2: S-Chicane with forced jackknife/articulation
            ("TruckObstacle_3",  new Vector2(5.0f,  72.0f),   0f,   new Color(0.88f, 0.15f, 0.15f)), // Red - center-left block
            ("TruckObstacle_4",  new Vector2(14.8f, 92.0f),  180f,  new Color(0.96f, 0.82f, 0.12f)), // Yellow - right lane block, forces sharp left weave
            ("TruckObstacle_5",  new Vector2(4.2f, 115.0f),   0f,   new Color(0.18f, 0.65f, 0.28f)), // Green - left lane block, forces counter-weave right
            // Zone 3: Diagonal Chevron Zigzag (135° & 45°)
            ("TruckObstacle_6",  new Vector2(13.8f, 140.0f), 135f,  new Color(0.15f, 0.70f, 0.85f)), // Cyan - 135° diagonal wedge right
            ("TruckObstacle_7",  new Vector2(5.5f, 172.0f),  45f,   new Color(0.60f, 0.15f, 0.25f)), // Burgundy - 45° diagonal wedge left
            // Zone 4: Dock Approach, Pre-Dock Trap & Framing Trucks
            ("TruckObstacle_8",  new Vector2(15.2f, 198.0f), 180f,  new Color(0.20f, 0.40f, 0.70f)), // Steel Blue - right lane block
            ("TruckObstacle_9",  new Vector2(3.8f, 222.0f),   0f,   new Color(0.30f, 0.30f, 0.32f)), // Dark Grey - blocks left lane right before dock!
            ("TruckObstacle_10", new Vector2(14.5f, 237.0f),  0f,   new Color(0.85f, 0.85f, 0.88f)), // Silver/White - right frame of TargetParkingSlot
            ("TruckObstacle_11", new Vector2(2.8f, 237.0f),   0f,   new Color(0.85f, 0.85f, 0.88f))  // Silver/White - left frame of TargetParkingSlot
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
        // 4. SPAWN 6 PASSENGER CARS IN WALL ALCOVES
        // =========================================================================
        var carDefs = new (Vector2 pos, float rot, Color color)[]
        {
            (new Vector2(18.2f,  35.0f),  0f, new Color(0.82f, 0.84f, 0.88f)), // Silver - right wall
            (new Vector2(2.5f,   58.0f), 90f, new Color(0.88f, 0.15f, 0.15f)), // Red - left alcove
            (new Vector2(18.2f, 118.0f),  0f, Color.white),                    // White - right wall
            (new Vector2(2.5f,  150.0f), 90f, new Color(0.95f, 0.48f, 0.10f)), // Orange - left alcove
            (new Vector2(18.2f, 180.0f),  0f, new Color(0.96f, 0.82f, 0.12f)), // Yellow - right wall
            (new Vector2(18.2f, 222.0f),  0f, new Color(0.25f, 0.25f, 0.28f))  // Dark Grey - right wall opposite pre-dock trap
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
        // 5. SPAWN 14 PROPS (12 CORNER CONES + 2 DOCK BARRELS)
        // =========================================================================
        var propDefs = new (PropType type, Vector2 pos, float rot, string name)[]
        {
            // Traffic cones marking diagonal wedges and bottleneck apexes
            (PropType.Cone, new Vector2(4.3f,  38.0f), 0f, "Cone_T1_Corner"),
            (PropType.Cone, new Vector2(6.8f,  45.0f), 0f, "Cone_T2_ApexWedge"),
            (PropType.Cone, new Vector2(13.8f, 62.0f), 0f, "Cone_T2_RearCorner"),
            (PropType.Cone, new Vector2(6.5f,  83.0f), 0f, "Cone_T3_Corner"),
            (PropType.Cone, new Vector2(13.2f, 80.0f), 0f, "Cone_T4_ApexWedge"),
            (PropType.Cone, new Vector2(5.7f, 127.0f), 0f, "Cone_T5_Corner"),
            (PropType.Cone, new Vector2(9.8f, 137.0f), 0f, "Cone_T6_ApexWedge"),
            (PropType.Cone, new Vector2(15.2f, 152.0f), 0f, "Cone_T6_RearCorner"),
            (PropType.Cone, new Vector2(9.5f, 178.0f), 0f, "Cone_T7_ApexWedge"),
            (PropType.Cone, new Vector2(13.7f, 186.0f), 0f, "Cone_T8_Corner"),
            (PropType.Cone, new Vector2(5.3f, 210.0f), 0f, "Cone_T9_RearCorner"),
            (PropType.Cone, new Vector2(5.3f, 233.0f), 0f, "Cone_T9_FrontCorner"),
            // Dock safety barrels
            (PropType.Barrel, new Vector2(5.5f,  237.0f), 0f, "Barrel_DockLeft"),
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
        // 6. SPAWN EXPERT ROUTE GUIDE LINE (Centerline with articulation weaves)
        // =========================================================================
        GameObject routeGo = new GameObject("Route_Centerline");
        routeGo.transform.SetParent(routeContainer, false);
        RouteGuideLine route = routeGo.AddComponent<RouteGuideLine>();
        route.waypoints = new List<Vector2>
        {
            new Vector2(7.68f, 18.15f),
            new Vector2(7.68f, 28.0f),
            new Vector2(5.5f,  42.0f),   // Squeeze left around T2 45° wedge
            new Vector2(7.5f,  58.0f),   // Articulate cab right
            new Vector2(11.5f, 78.0f),   // Weave right around T3
            new Vector2(7.5f, 100.0f),   // Counter-weave left between T4 & T5
            new Vector2(11.0f, 125.0f),  // Weave right around T5
            new Vector2(7.0f,  150.0f),  // Snake left through diagonal chevron between T6 & T7
            new Vector2(11.5f, 180.0f),  // Pull right past T7
            new Vector2(8.0f,  205.0f),  // Weave left around T8
            new Vector2(11.0f, 225.0f),  // Swing right to clear T9 pre-dock trap!
            new Vector2(8.56f, 232.0f),  // Articulate cab and align trailer into dock
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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"<color=#55ff55>[PopulateA3Map] Сцена '{ScenePath}' успешно создана/обновлена (Экстремальный слалом с заломом)!</color>");
    }
}
