using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MyMapMenu
{
    public const string CustomMapsFolder = "Assets/Scenes/CustomMaps";

    [MenuItem("Tools/MyMap/Map_1", false, 1)]
    public static void Launch_Map_1()
    {
        PlayMap("Map_1");
    }

    [MenuItem("Tools/MyMap/Map_2", false, 2)]
    public static void Launch_Map_2()
    {
        PlayMap("Map_2");
    }

    [MenuItem("Tools/MyMap/Выбрать карту (Launcher)...", false, 100)]
    public static void OpenLauncher()
    {
        MyMapLauncherWindow.Open();
    }

    public static void PlayMap(string mapName)
    {
        if (EditorApplication.isPlaying)
        {
            void OnPlayModeChanged(PlayModeStateChange state)
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                    EditorApplication.delayCall += () => PlayMapInternal(mapName);
                }
            }
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = false;
            return;
        }
        PlayMapInternal(mapName);
    }

    private static void PlayMapInternal(string mapName)
    {
        string scenePath = $"{CustomMapsFolder}/{mapName}.unity";
        if (!File.Exists(scenePath))
        {
            EditorUtility.DisplayDialog("Ошибка", $"Файл сцены '{mapName}' не найден по пути: {scenePath}", "OK");
            return;
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            if (MapBuilderEditor.FindPlayerTractor() == null || MapBuilderEditor.FindPlayerTrailer() == null || GameObject.Find("TruckControlsCanvas") == null)
            {
                MapBuilderEditor window = EditorWindow.GetWindow<MapBuilderEditor>("Map Builder");
                window.EnsureCleanWorkPlane(false);
                window.SaveCurrentMap(mapName);
            }
            EditorApplication.isPlaying = true;
            Debug.Log($"<color=#55ff55>[MyMap] Сцена '{mapName}' успешно загружена и запущена в режиме Play!</color>");
        }
    }

    public static void RegenerateMenu()
    {
        // Menu is up to date
    }
}

public class MyMapLauncherWindow : EditorWindow
{
    private Vector2 scrollPos;
    private List<string> maps = new List<string>();

    public static void Open()
    {
        MyMapLauncherWindow window = GetWindow<MyMapLauncherWindow>(true, "MyMap — Выбор карты", true);
        window.minSize = new Vector2(320, 380);
        window.maxSize = new Vector2(450, 600);
        window.ShowUtility();
        window.RefreshList();
    }

    private void OnEnable() => RefreshList();

    private void RefreshList()
    {
        maps.Clear();
        if (Directory.Exists(MyMapMenu.CustomMapsFolder))
        {
            string[] files = Directory.GetFiles(MyMapMenu.CustomMapsFolder, "*.unity");
            foreach (string f in files)
                maps.Add(Path.GetFileNameWithoutExtension(f));
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎮 MyMap — Запуск карты", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Выберите карту для мгновенного запуска в игре или редактирования:", MessageType.Info);
        EditorGUILayout.Space(6);

        if (maps.Count == 0)
        {
            EditorGUILayout.HelpBox("Нет сохранённых карт. Создайте карту через Tools -> Map Builder.", MessageType.Warning);
            if (GUILayout.Button("🛠 Открыть Map Builder", GUILayout.Height(30)))
            {
                MapBuilderEditor.OpenEditor();
                Close();
            }
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (string mapName in maps)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"🗺 <b>{mapName}</b>", new GUIStyle(EditorStyles.label) { richText = true, fontSize = 13 });
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.25f, 0.9f, 0.35f, 1f);
            if (GUILayout.Button("▶ Запустить (Play)", GUILayout.Height(28)))
            {
                MyMapMenu.PlayMap(mapName);
                Close();
            }
            GUI.backgroundColor = new Color(0.2f, 0.85f, 1f, 1f);
            if (GUILayout.Button("✏️ Править", GUILayout.Width(75), GUILayout.Height(28)))
            {
                string path = $"{MyMapMenu.CustomMapsFolder}/{mapName}.unity";
                MapBuilderEditor.LoadAndEditMap(path);
                Close();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("➕ Создать новую карту", GUILayout.Height(26)))
        {
            MapBuilderEditor.NewCleanMap();
            Close();
        }
    }
}
