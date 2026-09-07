using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class StartGameMenu
{
    public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/Start New Game", false, 0)]
    public static void LaunchNewGame()
    {
        if (EditorApplication.isPlaying)
        {
            void OnPlayModeChanged(PlayModeStateChange state)
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                    EditorApplication.delayCall += LaunchNewGameInternal;
                }
            }

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = false;
            return;
        }

        LaunchNewGameInternal();
    }

    private static void LaunchNewGameInternal()
    {
        EnsureMainMenuSceneExists();
        RegisterScenesInBuild.RegisterAllScenes();

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(MainMenuScenePath);
            EditorApplication.isPlaying = true;
            Debug.Log("<color=#55ff55>[StartGame] Запуск новой игры с Главного Меню (Выбор карт)...</color>");
        }
    }

    public static void EnsureMainMenuSceneExists()
    {
        if (File.Exists(MainMenuScenePath))
        {
            return;
        }

        string dir = Path.GetDirectoryName(MainMenuScenePath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. 2D Camera
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 15f;
        cam.transform.position = new Vector3(0, 0, -10f);
        camGo.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        // 2. Global Light 2D
        GameObject lightGo = new GameObject("Global Light 2D");
        var light2d = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light2d.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light2d.color = Color.white;
        light2d.intensity = 1.0f;

        // 3. EventSystem
        GameObject esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<InputSystemUIInputModule>();

        // 4. MapSelectMenu Controller
        GameObject menuGo = new GameObject("MainMenuManager");
        menuGo.AddComponent<MapSelectMenu>();

        EditorSceneManager.SaveScene(newScene, MainMenuScenePath);
        AssetDatabase.Refresh();
    }
}
