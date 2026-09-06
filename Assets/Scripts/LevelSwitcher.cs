using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LevelSwitcher : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Target scene to load when button is clicked or shortcut is pressed")]
    [SerializeField] private string targetSceneName = "Level2_AlleyDock";

    public string TargetSceneName
    {
        get => targetSceneName;
        set => targetSceneName = value;
    }

    private void Awake()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        targetSceneName = GetNextSceneName(currentScene);
    }

    private void Update()
    {
        bool switchPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            switchPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.M))
        {
            switchPressed = true;
        }
#endif

        if (switchPressed)
        {
            SwitchLevel();
        }
    }

    private void OnGUI()
    {
        // High-visibility, crisp button in top-left corner guaranteed to render across all screen sizes
        Color origBg = GUI.backgroundColor;
        Color origColor = GUI.contentColor;

        GUI.backgroundColor = new Color(0.12f, 0.45f, 0.85f, 1.0f);
        GUI.contentColor = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 15;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        string currentScene = SceneManager.GetActiveScene().name;
        string label = GetButtonLabel(currentScene);

        if (GUI.Button(new Rect(25, 25, 245, 48), label, style))
        {
            SwitchLevel();
        }

        GUI.backgroundColor = origBg;
        GUI.contentColor = origColor;
    }

    public static string GetNextSceneName(string currentScene)
    {
        if (currentScene.Contains("Level3") || currentScene.Contains("RestArea") || currentScene.Contains("GasStation"))
        {
            return "SampleScene";
        }
        else if (currentScene.Contains("Level2") || currentScene.Contains("AlleyDock"))
        {
            return "Level3_RestArea";
        }
        else
        {
            return "Level2_AlleyDock";
        }
    }

    public static string GetButtonLabel(string currentScene)
    {
        if (currentScene.Contains("Level3") || currentScene.Contains("RestArea") || currentScene.Contains("GasStation"))
        {
            return "КАРТА 1 (ПОЛИГОН) [M]";
        }
        else if (currentScene.Contains("Level2") || currentScene.Contains("AlleyDock"))
        {
            return "КАРТА 3 (РЕСТ ЭРИЯ) [M]";
        }
        else
        {
            return "КАРТА 2 (БОКС) [M]";
        }
    }

    public void SwitchLevel()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        string target = GetNextSceneName(currentScene);

        Debug.Log($"[LevelSwitcher] Switching scene from {currentScene} to: {target}");
        LoadSceneByName(target);
    }

    public void LoadMap1()
    {
        LoadSceneByName("SampleScene");
    }

    public void LoadMap2()
    {
        LoadSceneByName("Level2_AlleyDock");
    }

    public void LoadMap3()
    {
        LoadSceneByName("Level3_RestArea");
    }

    private void LoadSceneByName(string target)
    {
#if UNITY_EDITOR
        string scenePath = $"Assets/Scenes/{target}.unity";
        if (System.IO.File.Exists(scenePath))
        {
            var loadParams = new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath, loadParams);
            return;
        }
#endif
        SceneManager.LoadScene(target);
    }
}
