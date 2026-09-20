using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Editor for SplineRoad2D.
/// Provides interactive SceneView editing (point dragging, Shift+Click to insert/append points)
/// and comprehensive Inspector controls for road dimensions, curbs, physics, and spline points.
/// </summary>
[CustomEditor(typeof(SplineRoad2D))]
public class SplineRoad2DEditor : Editor
{
    private SplineRoad2D road;
    private int selectedPointIndex = -1;

    private void OnEnable()
    {
        road = (SplineRoad2D)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("🛣️ Настройки 2D-дороги на сплайне", EditorStyles.boldLabel);

        // Spline & Dimensions
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Геометрия и сплайн", EditorStyles.boldLabel);

        SerializedProperty propInterpolation = serializedObject.FindProperty("interpolation");
        SerializedProperty propClosedLoop = serializedObject.FindProperty("isClosedLoop");
        SerializedProperty propRoadWidth = serializedObject.FindProperty("roadWidth");
        SerializedProperty propBorderWidth = serializedObject.FindProperty("borderWidth");
        SerializedProperty propSegments = serializedObject.FindProperty("segmentsPerCurve");

        EditorGUILayout.PropertyField(propInterpolation, new GUIContent("Интерполяция"));
        EditorGUILayout.PropertyField(propClosedLoop, new GUIContent("Замкнутое кольцо"));
        EditorGUILayout.PropertyField(propRoadWidth, new GUIContent("Ширина дороги (м)"));
        EditorGUILayout.PropertyField(propBorderWidth, new GUIContent("Ширина бордюра (м)"));
        EditorGUILayout.PropertyField(propSegments, new GUIContent("Детализация сегментов"));
        EditorGUILayout.EndVertical();

        // Tiling & Visuals
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Текстурирование и цвета", EditorStyles.boldLabel);

        SerializedProperty propAsphaltTile = serializedObject.FindProperty("asphaltTileSize");
        SerializedProperty propCurbTile = serializedObject.FindProperty("curbTileSize");
        SerializedProperty propAsphaltColor = serializedObject.FindProperty("asphaltColor");
        SerializedProperty propCurbColor = serializedObject.FindProperty("curbColor");
        SerializedProperty propSortingOrder = serializedObject.FindProperty("sortingOrder");
        SerializedProperty propCustomAsphaltMat = serializedObject.FindProperty("customAsphaltMaterial");
        SerializedProperty propCustomCurbMat = serializedObject.FindProperty("customCurbMaterial");

        EditorGUILayout.PropertyField(propAsphaltTile, new GUIContent("Тайлинг асфальта (м)"));
        EditorGUILayout.PropertyField(propCurbTile, new GUIContent("Тайлинг бордюра (м)"));
        EditorGUILayout.PropertyField(propAsphaltColor, new GUIContent("Цвет асфальта"));
        EditorGUILayout.PropertyField(propCurbColor, new GUIContent("Цвет бордюра"));
        EditorGUILayout.PropertyField(propSortingOrder, new GUIContent("Sorting Order"));
        EditorGUILayout.PropertyField(propCustomAsphaltMat, new GUIContent("Свой мат. асфальта"));
        EditorGUILayout.PropertyField(propCustomCurbMat, new GUIContent("Свой мат. бордюра"));
        EditorGUILayout.EndVertical();

        // Physics Colliders
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Физические коллайдеры (EdgeCollider2D)", EditorStyles.boldLabel);

        SerializedProperty propCreateColliders = serializedObject.FindProperty("createColliders");
        SerializedProperty propCollidersTrigger = serializedObject.FindProperty("collidersAreTrigger");
        SerializedProperty propColOffset = serializedObject.FindProperty("colliderPositionOffset");

        EditorGUILayout.PropertyField(propCreateColliders, new GUIContent("Создавать коллайдеры"));
        if (propCreateColliders.boolValue)
        {
            EditorGUILayout.PropertyField(propCollidersTrigger, new GUIContent("Is Trigger (Коллизии трака)"));
            EditorGUILayout.PropertyField(propColOffset, new GUIContent("Позиция (0=край, 1=бордюр)"));
        }
        EditorGUILayout.EndVertical();

        // Truck Turn Safety & Highlighting
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🚚 Проходимость трака (Безопасность поворотов)", EditorStyles.boldLabel);

        SerializedProperty propHighlightTurns = serializedObject.FindProperty("highlightImpassableTurns");
        SerializedProperty propMinRadius = serializedObject.FindProperty("minPassableRadius");

        EditorGUILayout.PropertyField(propHighlightTurns, new GUIContent("Оранжевые бордюры в опасных поворотах"));
        EditorGUILayout.PropertyField(propMinRadius, new GUIContent("Мин. радиус поворота R (м)"));
        EditorGUILayout.HelpBox("В поворотах, где радиус кривизны меньше минимального (трак не повернёт), бордюры автоматически окрашиваются в оранжевый цвет, а в окне сцены выводится предупреждение с радиусом.", MessageType.Info);
        EditorGUILayout.EndVertical();

        // Optimal parameters button
        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f, 1f);
        if (GUILayout.Button("Оптимальные параметры", GUILayout.Height(28)))
        {
            Undo.RecordObject(road, "Apply Optimal Road Parameters");
            road.roadWidth = 6.5f;
            road.borderWidth = 0.5f;
            road.segmentsPerCurve = 12;
            road.minPassableRadius = 15.0f;
            road.highlightImpassableTurns = true;
            serializedObject.Update();
            road.RebuildMesh();
            SceneView.RepaintAll();
            SceneView.lastActiveSceneView?.ShowNotification(new GUIContent("✓ Выставлены оптимальные параметры дороги"));
        }
        GUI.backgroundColor = Color.white;

        // Action Buttons
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.2f, 0.85f, 0.35f, 1f);
        if (GUILayout.Button("+ Добавить точку", GUILayout.Height(28)))
        {
            Undo.RecordObject(road, "Add Spline Road Point");
            Vector2 newPt = Vector2.zero;
            if (road.points.Count >= 2)
            {
                Vector2 last = road.points[road.points.Count - 1];
                Vector2 prev = road.points[road.points.Count - 2];
                newPt = last + (last - prev).normalized * 5f;
            }
            else if (road.points.Count == 1)
            {
                newPt = road.points[0] + new Vector2(0f, 5f);
            }
            road.points.Add(newPt);
            road.RebuildMesh();
            selectedPointIndex = road.points.Count - 1;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f, 1f);
        if (GUILayout.Button(road.IsClosedLoop ? "Разомкнуть" : "Замкнуть", GUILayout.Height(28)))
        {
            Undo.RecordObject(road, "Toggle Closed Loop");
            road.SetClosedLoop(!road.IsClosedLoop);
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Инвертировать направление", GUILayout.Height(24)))
        {
            Undo.RecordObject(road, "Reverse Spline Road");
            road.points.Reverse();
            road.RebuildMesh();
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
        if (GUILayout.Button("Очистить точки", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Очистить дорогу", "Удалить все контрольные точки дороги?", "Да, удалить", "Отмена"))
            {
                Undo.RecordObject(road, "Clear Spline Road Points");
                road.ClearPoints();
                selectedPointIndex = -1;
                SceneView.RepaintAll();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("⚡ Принудительно обновить меш", GUILayout.Height(26)))
        {
            road.UpdateMaterials();
            road.RebuildMesh();
            SceneView.RepaintAll();
        }

        // Control Points List
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Контрольные точки ({road.points.Count}):", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("Совет: Зажмите Shift и кликните в SceneView для добавления точки прямо на карте!", MessageType.Info);

        for (int i = 0; i < road.points.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            bool isSelected = (i == selectedPointIndex);
            Color origColor = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(1f, 0.85f, 0.2f, 1f);

            if (GUILayout.Button($"#{i + 1}", GUILayout.Width(35), GUILayout.Height(20)))
            {
                selectedPointIndex = i;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = origColor;

            EditorGUI.BeginChangeCheck();
            Vector2 pt = EditorGUILayout.Vector2Field("", road.points[i]);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(road, "Edit Spline Road Point");
                road.points[i] = pt;
                road.RebuildMesh();
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if (GUILayout.Button("✕", GUILayout.Width(24), GUILayout.Height(20)))
            {
                Undo.RecordObject(road, "Remove Spline Road Point");
                road.RemovePoint(i);
                if (selectedPointIndex >= road.points.Count) selectedPointIndex = road.points.Count - 1;
                SceneView.RepaintAll();
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = origColor;

            EditorGUILayout.EndHorizontal();
        }

        if (serializedObject.ApplyModifiedProperties())
        {
            road.RebuildMesh();
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI()
    {
        if (road == null || road.points == null) return;

        Event e = Event.current;
        Transform tr = road.transform;

        // 1. Shift + Click to add / insert points in SceneView
        if (e.type == EventType.MouseDown && e.button == 0 && e.shift)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.forward, tr.position);

            if (plane.Raycast(ray, out float enter))
            {
                Vector3 worldHit = ray.GetPoint(enter);
                Vector2 localPos = tr.InverseTransformPoint(worldHit);

                Undo.RecordObject(road, "Add Spline Road Point");

                // Check if clicking near any existing segment to insert
                int insertIdx = FindBestInsertIndex(localPos);
                if (insertIdx >= 0)
                {
                    road.InsertPoint(insertIdx, localPos);
                    selectedPointIndex = insertIdx;
                }
                else
                {
                    road.AddPoint(localPos);
                    selectedPointIndex = road.points.Count - 1;
                }

                e.Use();
                SceneView.RepaintAll();
                return;
            }
        }

        // 2. Delete / Backspace key to remove selected point
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
        {
            if (selectedPointIndex >= 0 && selectedPointIndex < road.points.Count && road.points.Count > 2)
            {
                Undo.RecordObject(road, "Remove Spline Road Point");
                road.RemovePoint(selectedPointIndex);
                if (selectedPointIndex >= road.points.Count) selectedPointIndex = road.points.Count - 1;
                e.Use();
                SceneView.RepaintAll();
                return;
            }
        }

        // 3. Draw and manipulate control point handles
        for (int i = 0; i < road.points.Count; i++)
        {
            Vector3 worldPt = tr.TransformPoint(new Vector3(road.points[i].x, road.points[i].y, 0f));
            bool isSelected = (i == selectedPointIndex);

            float handleSize = HandleUtility.GetHandleSize(worldPt) * 0.16f;

            Handles.color = isSelected ? new Color(0.2f, 0.95f, 1.0f, 0.95f) : new Color(1.0f, 0.85f, 0.15f, 0.90f);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPt = Handles.FreeMoveHandle(
                worldPt,
                handleSize,
                Vector3.zero,
                Handles.CircleHandleCap
            );

            // Also show position handle if point is selected
            if (isSelected)
            {
                newWorldPt = Handles.PositionHandle(worldPt, Quaternion.identity);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(road, "Move Spline Road Point");
                road.points[i] = tr.InverseTransformPoint(newWorldPt);
                road.RebuildMesh();
            }

            // Labels
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = isSelected ? new Color(0.2f, 0.95f, 1.0f) : new Color(1.0f, 0.85f, 0.2f);
            Handles.Label(worldPt + new Vector3(handleSize * 1.2f, handleSize * 1.2f, 0f), $"#{i + 1}", labelStyle);

            // Check if user clicked on this point
            if (e.type == EventType.MouseDown && e.button == 0 && !e.shift)
            {
                float screenDist = HandleUtility.DistanceToCircle(worldPt, handleSize);
                if (screenDist < 12f)
                {
                    selectedPointIndex = i;
                    Repaint();
                }
            }
        }

        // 4. Draw helper line connecting control points
        Handles.color = new Color(1.0f, 0.85f, 0.15f, 0.35f);
        for (int i = 0; i < road.points.Count - 1; i++)
        {
            Vector3 pA = tr.TransformPoint(road.points[i]);
            Vector3 pB = tr.TransformPoint(road.points[i + 1]);
            Handles.DrawDottedLine(pA, pB, 4f);
        }
        if (road.IsClosedLoop && road.points.Count > 2)
        {
            Vector3 pA = tr.TransformPoint(road.points[road.points.Count - 1]);
            Vector3 pB = tr.TransformPoint(road.points[0]);
            Handles.DrawDottedLine(pA, pB, 4f);
        }

        // 5. Highlight impassable turns
        if (road.highlightImpassableTurns && road.points.Count >= 3)
        {
            var impassableTurns = road.GetImpassableTurns();
            for (int t = 0; t < impassableTurns.Count; t++)
            {
                var turn = impassableTurns[t];
                Vector3 pWorld = new Vector3(turn.position.x, turn.position.y, tr.position.z);

                Handles.color = new Color(1.0f, 0.55f, 0.0f, 0.25f);
                Handles.DrawSolidDisc(pWorld, Vector3.forward, 1.4f);
                Handles.color = new Color(1.0f, 0.55f, 0.0f, 0.95f);
                Handles.DrawWireDisc(pWorld, Vector3.forward, 1.4f);
                Handles.DrawWireDisc(pWorld, Vector3.forward, 1.48f);

                GUIStyle warnStyle = new GUIStyle(EditorStyles.boldLabel);
                warnStyle.normal.textColor = new Color(1.0f, 0.55f, 0.0f);
                warnStyle.fontSize = 11;
                Handles.Label(pWorld + new Vector3(0.7f, 0.7f, 0f),
                    $"⚠️ Опасный поворот!\nR = {turn.radius:F1}м (мин. {road.minPassableRadius:F1}м)\nТрак не повернёт!", warnStyle);
            }
        }
    }

    private int FindBestInsertIndex(Vector2 localPos)
    {
        if (road.points.Count < 2) return -1;

        float bestDist = 2.5f; // Threshold distance to segment for insertion
        int bestIdx = -1;

        int segCount = road.IsClosedLoop ? road.points.Count : (road.points.Count - 1);
        for (int i = 0; i < segCount; i++)
        {
            Vector2 a = road.points[i];
            Vector2 b = road.points[(i + 1) % road.points.Count];

            float dist = HandleUtility.DistancePointToLineSegment(localPos, a, b);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i + 1;
            }
        }

        return bestIdx;
    }
}
