using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for RoundaboutGenerator2D with real-time AASHTO WB-67 Swept Path verification,
/// color-coded passability banners, quick presets, and live mesh rebuilding.
/// </summary>
[CustomEditor(typeof(RoundaboutGenerator2D))]
public class RoundaboutGenerator2DEditor : Editor
{
    private RoundaboutGenerator2D generator;

    private void OnEnable()
    {
        generator = (RoundaboutGenerator2D)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🔄 Процедурное круговое движение (Roundabout 2D)", EditorStyles.boldLabel);

        // 1. Swept Path / Off-tracking Verification Banner
        RoundaboutGenerator2D.PassabilityResult res = generator.CheckPassability();
        DrawPassabilityBanner(res);

        EditorGUILayout.Space(6);

        // 2. Quick Presets
        EditorGUILayout.LabelField("Быстрые пресеты колец:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Городское (1 полоса + Apron)", GUILayout.Height(24)))
        {
            Undo.RecordObject(generator, "Apply Roundabout Preset 1");
            generator.outerRadius = 20.0f;
            generator.laneCount = 1;
            generator.laneWidth = 5.2f;
            generator.curbWidth = 0.5f;
            generator.hasTruckApron = true;
            generator.truckApronWidth = 3.0f;
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Двухполосное (2 полосы)", GUILayout.Height(24)))
        {
            Undo.RecordObject(generator, "Apply Roundabout Preset 2");
            generator.outerRadius = 26.0f;
            generator.laneCount = 2;
            generator.laneWidth = 5.0f;
            generator.curbWidth = 0.5f;
            generator.hasTruckApron = true;
            generator.truckApronWidth = 2.5f;
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Трассовое (WB-67 Highway)", GUILayout.Height(24)))
        {
            Undo.RecordObject(generator, "Apply Roundabout Preset 3");
            generator.outerRadius = 34.0f;
            generator.laneCount = 2;
            generator.laneWidth = 5.5f;
            generator.curbWidth = 0.5f;
            generator.hasTruckApron = true;
            generator.truckApronWidth = 3.0f;
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // 2.1 Arm Presets (Entry & Exit Legs)
        EditorGUILayout.LabelField("🛣️ Заезды и выезды с кольца (Примыкания дорог):", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.95f, 0.75f, 1f);
        if (GUILayout.Button("1 Заезд + 2 Выезда (3 рукава)", GUILayout.Height(26)))
        {
            Undo.RecordObject(generator, "Preset 1 Entry 2 Exits");
            generator.SetPreset_1Entry_2Exits(generator.armWidth);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("1 Заезд + 3 Выезда (4 рукава)", GUILayout.Height(26)))
        {
            Undo.RecordObject(generator, "Preset 1 Entry 3 Exits");
            generator.SetPreset_1Entry_3Exits(generator.armWidth);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("4 Двусторонних рукава", GUILayout.Height(24)))
        {
            Undo.RecordObject(generator, "Preset 4-Way");
            generator.SetPreset_4Way(generator.armWidth);
            SceneView.RepaintAll();
        }
        if (GUILayout.Button("Очистить рукава (Только кольцо)", GUILayout.Height(24)))
        {
            Undo.RecordObject(generator, "Clear Arms");
            generator.arms.Clear();
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        DrawArmsInspectorSection();

        EditorGUILayout.Space(6);

        // 3. Geometry Settings
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📐 Геометрия кольца", EditorStyles.boldLabel);

        SerializedProperty propOuterRadius = serializedObject.FindProperty("outerRadius");
        SerializedProperty propLaneCount = serializedObject.FindProperty("laneCount");
        SerializedProperty propLaneWidth = serializedObject.FindProperty("laneWidth");
        SerializedProperty propCurbWidth = serializedObject.FindProperty("curbWidth");
        SerializedProperty propSegments = serializedObject.FindProperty("segments");

        EditorGUILayout.PropertyField(propOuterRadius, new GUIContent("Внешний радиус (м)"));
        EditorGUILayout.PropertyField(propLaneCount, new GUIContent("Число полос (1-3)"));
        EditorGUILayout.PropertyField(propLaneWidth, new GUIContent("Ширина полосы (м)"));
        EditorGUILayout.PropertyField(propCurbWidth, new GUIContent("Ширина бордюра (м)"));
        EditorGUILayout.PropertyField(propSegments, new GUIContent("Сегментов круга"));

        float totalRoadWidth = (propLaneCount.intValue * propLaneWidth.floatValue);
        EditorGUILayout.HelpBox($"Суммарная ширина проезжих полос: {totalRoadWidth:F1} м (Внешний диаметр: {propOuterRadius.floatValue * 2f:F1} м)", MessageType.None);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 4. Truck Apron (Mountable Paved Inner Ring)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🚛 Наездной островок для грузовиков (Truck Apron)", EditorStyles.boldLabel);

        SerializedProperty propHasApron = serializedObject.FindProperty("hasTruckApron");
        SerializedProperty propApronWidth = serializedObject.FindProperty("truckApronWidth");

        EditorGUILayout.PropertyField(propHasApron, new GUIContent("Включить Truck Apron"));
        if (propHasApron.boolValue)
        {
            EditorGUILayout.PropertyField(propApronWidth, new GUIContent("Ширина островка (м)"));
            EditorGUILayout.HelpBox("Truck Apron — специальное мощеное кольцо брусчаткой перед клумбой, на которое может заезжать задняя тележка 53' полуприцепа при офф-трекинге без аварии.", MessageType.None);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 5. Central Island & Visuals
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🎨 Оформление и центральная клумба", EditorStyles.boldLabel);

        SerializedProperty propHasCenter = serializedObject.FindProperty("hasCenterIsland");
        SerializedProperty propSorting = serializedObject.FindProperty("sortingOrder");
        SerializedProperty propHighlight = serializedObject.FindProperty("highlightImpassable");

        EditorGUILayout.PropertyField(propHasCenter, new GUIContent("Центральный газон"));
        EditorGUILayout.PropertyField(propHighlight, new GUIContent("Оранжевый бордюр при срезе"));
        EditorGUILayout.PropertyField(propSorting, new GUIContent("Sorting Order"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 6. Physics Colliders
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🧱 Физические коллайдеры (EdgeCollider2D)", EditorStyles.boldLabel);

        SerializedProperty propCreateCol = serializedObject.FindProperty("createColliders");
        SerializedProperty propTriggerCol = serializedObject.FindProperty("collidersAreTrigger");

        EditorGUILayout.PropertyField(propCreateCol, new GUIContent("Создавать коллайдеры"));
        if (propCreateCol.boolValue)
        {
            EditorGUILayout.PropertyField(propTriggerCol, new GUIContent("Is Trigger (Коллизии трака)"));
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 7. Truck Specs (Foldable)
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚙️ Параметры трака для расчета (WB-67 Semi)", EditorStyles.boldLabel);

        SerializedProperty propTractorWb = serializedObject.FindProperty("tractorWheelbase");
        SerializedProperty propKingpin = serializedObject.FindProperty("kingpinToRearAxle");
        SerializedProperty propVehWidth = serializedObject.FindProperty("vehicleWidth");
        SerializedProperty propMaxSteer = serializedObject.FindProperty("maxSteerAngle");
        SerializedProperty propMargin = serializedObject.FindProperty("safetyMargin");

        EditorGUILayout.PropertyField(propTractorWb, new GUIContent("База тягача (м)"));
        EditorGUILayout.PropertyField(propKingpin, new GUIContent("Шкворень ➔ Тележка (м)"));
        EditorGUILayout.PropertyField(propVehWidth, new GUIContent("Ширина кузова (м)"));
        EditorGUILayout.PropertyField(propMaxSteer, new GUIContent("Макс. угол руля (°)"));
        EditorGUILayout.PropertyField(propMargin, new GUIContent("Запас до бордюра (м)"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // Action Buttons
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f, 1f);
        if (GUILayout.Button("⚡ Перестроить кольцо (Rebuild)", GUILayout.Height(30)))
        {
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }

        if (!res.isPassable && !res.isPhysicallyImpossible)
        {
            GUI.backgroundColor = new Color(0.25f, 0.9f, 0.4f, 1f);
            if (GUILayout.Button("📐 Подогнать ширину (+1.0м)", GUILayout.Height(30)))
            {
                Undo.RecordObject(generator, "Auto-Fit Roundabout Width");
                float deficit = Mathf.Abs(res.margin) + 1.0f;
                if (generator.hasTruckApron)
                {
                    generator.truckApronWidth += deficit;
                }
                else
                {
                    generator.laneWidth += (deficit / generator.laneCount);
                }
                generator.RebuildRoundabout();
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (serializedObject.ApplyModifiedProperties())
        {
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }
    }

    private void DrawPassabilityBanner(RoundaboutGenerator2D.PassabilityResult res)
    {
        if (res.isPhysicallyImpossible)
        {
            GUI.backgroundColor = new Color(0.85f, 0.15f, 0.15f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle errTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            EditorGUILayout.LabelField("⛔ ГЕОМЕТРИЧЕСКИЙ ТУПИК ДЛЯ АВТОПОЕЗДА", errTitleStyle);
            EditorGUILayout.LabelField(res.statusMessage, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
            return;
        }

        if (res.isPassable)
        {
            GUI.backgroundColor = new Color(0.18f, 0.55f, 0.22f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle passTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            EditorGUILayout.LabelField($"✓ ПРОХОДИМО ДЛЯ WB-67 (ЗАПАС: +{res.margin:F1} м)", passTitleStyle);

            GUIStyle subStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.9f, 1.0f, 0.9f) },
                wordWrap = true
            };
            EditorGUILayout.LabelField($"• Требуемый коридор движения (Swept Path): {res.sweptPathWidth:F1} м\n" +
                                      $"• Доступная ширина проезжей части: {res.availableLaneWidth:F1} м\n" +
                                      $"• Автопоезд 53' проходит кольцо без заезда на клумбу.", subStyle);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(0.85f, 0.2f, 0.2f, 1f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle failTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            EditorGUILayout.LabelField($"✗ НЕПРОХОДИМО: ТЕЛЕЖКА СРЕЖЕТ БОРДЮР НА {Mathf.Abs(res.margin):F1} м", failTitleStyle);

            GUIStyle subStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(1.0f, 0.9f, 0.9f) },
                wordWrap = true
            };
            EditorGUILayout.LabelField($"• Требуемый коридор движения (Swept Path): {res.sweptPathWidth:F1} м\n" +
                                      $"• Доступная ширина полос: {res.availableLaneWidth:F1} м\n" +
                                      $"• Дефицит ширины: {Mathf.Abs(res.margin):F1} м (задние колеса сорвут бордюр/газон)!\n" +
                                      $"• Совет: увеличьте радиус кольца, ширину полосы или включите Truck Apron.", subStyle);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
    }

    private void DrawArmsInspectorSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"🛣️ Рукава кольца (Примыкания дорог: {generator.arms.Count})", EditorStyles.boldLabel);
        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f, 1f);
        if (GUILayout.Button("+ Добавить рукав", GUILayout.Width(130), GUILayout.Height(22)))
        {
            Undo.RecordObject(generator, "Add Roundabout Arm");
            float nextAngle = 0f;
            if (generator.arms.Count > 0)
            {
                nextAngle = (generator.arms[generator.arms.Count - 1].angleDeg + 90f) % 360f;
            }
            generator.arms.Add(new RoundaboutArm($"Рукав {generator.arms.Count + 1}", nextAngle, RoundaboutArmType.TwoWay, generator.armWidth, 5.0f));
            generator.RebuildRoundabout();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        float newArmWidth = EditorGUILayout.Slider("Ширина заездной дороги (м)", generator.armWidth, 4.0f, 16.0f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(generator, "Change Arm Width");
            generator.SetAllArmsWidth(newArmWidth);
            SceneView.RepaintAll();
        }

        if (generator.arms.Count == 0)
        {
            EditorGUILayout.HelpBox("У кольца пока нет рукавов примыкания дорог. Нажмите кнопки быстрых пресетов '1 Заезд + 2 Выезда' или '1 Заезд + 3 Выезда' выше, либо '+ Добавить рукав'.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < generator.arms.Count; i++)
            {
                RoundaboutArm arm = generator.arms[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                string typeBadge = arm.armType == RoundaboutArmType.Entry ? "🟢 [ЗАЕЗД]" :
                                   arm.armType == RoundaboutArmType.Exit ? "🔵 [ВЫЕЗД]" : "🟡 [ДВУСТОРОННИЙ]";
                EditorGUILayout.LabelField($"{typeBadge} #{i + 1}", EditorStyles.boldLabel, GUILayout.Width(140));

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.TextField(arm.name);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(generator, "Rename Roundabout Arm");
                    arm.name = newName;
                }

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
                if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(18)))
                {
                    Undo.RecordObject(generator, "Remove Roundabout Arm");
                    generator.arms.RemoveAt(i);
                    generator.RebuildRoundabout();
                    SceneView.RepaintAll();
                    GUIUtility.ExitGUI();
                    return;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                RoundaboutArmType newType = (RoundaboutArmType)EditorGUILayout.EnumPopup("Тип примыкания", arm.armType);
                float newAngle = EditorGUILayout.Slider("Угол на кольце (°)", arm.angleDeg, 0f, 360f);
                float newWidth = EditorGUILayout.Slider("Ширина проезжей части (м)", arm.width, 3.5f, 15.0f);
                float newExt = EditorGUILayout.Slider("Длина рукава (м)", arm.extensionLength, 1.0f, 15.0f);
                SplineRoad2D newRoad = (SplineRoad2D)EditorGUILayout.ObjectField("Присоединенная дорога", arm.connectedRoad, typeof(SplineRoad2D), true);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(generator, "Modify Roundabout Arm");
                    arm.armType = newType;
                    arm.angleDeg = newAngle;
                    arm.width = newWidth;
                    arm.extensionLength = newExt;
                    arm.connectedRoad = newRoad;
                    generator.RebuildRoundabout();
                    SceneView.RepaintAll();
                }

                EditorGUILayout.BeginHorizontal();
                GUI.backgroundColor = new Color(0.2f, 0.75f, 1.0f, 1f);
                string btnText = arm.connectedRoad != null ? "🛣️ Выбрать подключенную дорогу" : "🛣️ Создать дорогу (SplineRoad2D)";
                if (GUILayout.Button(btnText, GUILayout.Height(24)))
                {
                    if (arm.connectedRoad != null)
                    {
                        Selection.activeGameObject = arm.connectedRoad.gameObject;
                        EditorGUIUtility.PingObject(arm.connectedRoad.gameObject);
                    }
                    else
                    {
                        CreateRoadFromArm(i);
                    }
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        EditorGUILayout.EndVertical();
    }

    public void CreateRoadFromArm(int armIndex)
    {
        if (generator == null || generator.arms == null || armIndex < 0 || armIndex >= generator.arms.Count) return;
        RoundaboutArm arm = generator.arms[armIndex];

        Vector2 socketPos = generator.GetArmSocketWorldPos(armIndex);
        Vector2 outwardDir = generator.GetArmSocketDirection(armIndex);

        GameObject workspace = GameObject.Find("MapBuilder_Workspace");
        Transform roadsContainer = null;
        if (workspace != null)
        {
            roadsContainer = workspace.transform.Find("MapBuilder_SplineRoads");
            if (roadsContainer == null)
            {
                GameObject containerGo = new GameObject("MapBuilder_SplineRoads");
                containerGo.transform.SetParent(workspace.transform, false);
                roadsContainer = containerGo.transform;
                Undo.RegisterCreatedObjectUndo(containerGo, "Create SplineRoads Container");
            }
        }

        int roadIndex = roadsContainer != null ? roadsContainer.childCount + 1 : 1;
        string safeArmName = arm.name.Replace(" ", "_");
        GameObject roadGo = new GameObject($"SplineRoad_{safeArmName}_{roadIndex}");
        if (roadsContainer != null)
        {
            roadGo.transform.SetParent(roadsContainer, false);
        }

        SplineRoad2D road = roadGo.AddComponent<SplineRoad2D>();
        road.roadWidth = arm.width;
        road.borderWidth = generator.curbWidth;
        road.sortingOrder = generator.sortingOrder;
        road.minPassableRadius = 13.5f;
        road.highlightImpassableTurns = true;
        road.createColliders = true;

        Vector2 pt0 = socketPos;
        Vector2 pt1 = socketPos + outwardDir * 12.0f;
        road.points = new List<Vector2> { pt0, pt1 };
        road.RebuildMesh();

        arm.connectedRoad = road;

        Undo.RegisterCreatedObjectUndo(roadGo, $"Create Road for Arm {arm.name}");
        Undo.RecordObject(generator, "Connect Road to Arm");

        Selection.activeGameObject = roadGo;
        EditorGUIUtility.PingObject(roadGo);
        SceneView.RepaintAll();
        SceneView.lastActiveSceneView?.ShowNotification(new GUIContent($"✓ Дорога создана от рукава '{arm.name}'"));
    }

    private void OnSceneGUI()
    {
        if (generator == null || generator.arms == null) return;

        Vector3 centerPos = generator.transform.position;

        for (int i = 0; i < generator.arms.Count; i++)
        {
            RoundaboutArm arm = generator.arms[i];
            Vector3 socketWorld = generator.GetArmSocketWorldPos(i);
            Vector3 outwardDir = generator.GetArmSocketDirection(i);

            Color armColor = arm.armType == RoundaboutArmType.Entry ? new Color(0.2f, 0.95f, 0.4f, 0.95f) :
                             arm.armType == RoundaboutArmType.Exit ? new Color(0.2f, 0.7f, 1.0f, 0.95f) :
                             new Color(1.0f, 0.85f, 0.2f, 0.95f);

            Handles.color = armColor;
            float handleSize = HandleUtility.GetHandleSize(socketWorld) * 0.22f;

            // Interactive rotation handle around roundabout center
            EditorGUI.BeginChangeCheck();
            Vector3 newSocketPos = Handles.FreeMoveHandle(socketWorld, handleSize, Vector3.zero, Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(generator, "Rotate Roundabout Arm");
                Vector3 delta = generator.transform.InverseTransformPoint(newSocketPos);
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                arm.angleDeg = Mathf.Round(angle);
                generator.RebuildRoundabout();
                SceneView.RepaintAll();
            }

            // Direction arrow
            Vector3 arrowStart, arrowEnd;
            if (arm.armType == RoundaboutArmType.Entry)
            {
                arrowStart = socketWorld;
                arrowEnd = socketWorld - outwardDir * 3.5f;
            }
            else
            {
                arrowStart = socketWorld - outwardDir * 3.5f;
                arrowEnd = socketWorld;
            }
            Handles.DrawLine(arrowStart, arrowEnd);
            Handles.ConeHandleCap(0, arrowEnd, Quaternion.LookRotation(Vector3.forward, (arrowEnd - arrowStart).normalized), handleSize * 0.9f, EventType.Repaint);

            // Arm label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = armColor }
            };
            string typeStr = arm.armType == RoundaboutArmType.Entry ? "Въезд" :
                             arm.armType == RoundaboutArmType.Exit ? "Выезд" : "Двусторонний";
            Handles.Label(socketWorld + new Vector3(0.5f, 0.5f, 0f), $"[#{i + 1}] {arm.name} ({typeStr})\n{arm.width:F1}м", labelStyle);
        }
    }
}
