using System;
using System.Collections;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Автоматический менеджер скриншотов.
/// Создается автоматически при запуске игры в любой сцене (MainMenu, Practice и др.).
/// При нажатии клавиши 'X' делает чистый скриншот в оригинальном разрешении экрана (размером с телефон)
/// и сохраняет его в папку Screenshots/ в корне проекта.
/// </summary>
public class ScreenshotManager : MonoBehaviour
{
    private static ScreenshotManager _instance;
    public static ScreenshotManager Instance => _instance;

    private float toastTimer = 0f;
    private string toastMessage = "";
    private GUIStyle toastBoxStyle;
    private GUIStyle toastTextStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance != null) return;

        GameObject go = new GameObject("[ScreenshotManager]");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ScreenshotManager>();
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        bool xPressed = false;

#if ENABLE_INPUT_SYSTEM
        try
        {
            if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
            {
                xPressed = true;
            }
        }
        catch { }
#endif

        if (!xPressed)
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.X))
                {
                    xPressed = true;
                }
            }
            catch { }
        }

        if (xPressed)
        {
            CaptureNow();
        }
    }

    public void CaptureNow()
    {
        // Скрываем активный тост, чтобы скриншот был абсолютно чистым
        toastTimer = 0f;
        StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        // Дожидаемся окончания отрисовки текущего кадра (все камеры, 3D объекты и весь UI)
        yield return new WaitForEndOfFrame();

        string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string fileName = $"Screenshot_{timestamp}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        // Если файл с таким именем уже есть, добавляем суффикс
        int counter = 1;
        while (File.Exists(fullPath))
        {
            fileName = $"Screenshot_{timestamp}_{counter}.png";
            fullPath = Path.Combine(folderPath, fileName);
            counter++;
        }

        // Захватываем пиксели экрана с полным UI
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        if (screenshot != null)
        {
            int width = screenshot.width;
            int height = screenshot.height;
            byte[] pngData = screenshot.EncodeToPNG();
            Destroy(screenshot);

            File.WriteAllBytes(fullPath, pngData);

            Debug.Log($"<color=#4AE371><b>📸 [Скриншот сделан]</b></color> Разрешение: <b>{width}x{height}</b> px\nФайл сохранен: <u>{fullPath}</u>");

            // Показываем уведомление на экране (появляется только после создания скриншота, в снимок не попадает)
            toastMessage = $"📸 Скриншот сохранен: {width}x{height} px\n{fileName}";
            toastTimer = 3.0f;
        }
    }

    private void OnGUI()
    {
        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;

            if (toastBoxStyle == null)
            {
                toastBoxStyle = new GUIStyle(GUI.skin.box);
            }
            if (toastTextStyle == null)
            {
                toastTextStyle = new GUIStyle(GUI.skin.label);
                toastTextStyle.alignment = TextAnchor.MiddleCenter;
                toastTextStyle.fontSize = 16;
                toastTextStyle.fontStyle = FontStyle.Bold;
                toastTextStyle.normal.textColor = Color.white;
                toastTextStyle.wordWrap = true;
            }

            float width = Mathf.Min(480f, Screen.width - 40f);
            float height = 65f;
            float x = (Screen.width - width) * 0.5f;
            float y = 25f;

            float alpha = Mathf.Clamp01(toastTimer);
            Color prevColor = GUI.color;

            // Полупрозрачный фон плашки
            GUI.color = new Color(0.08f, 0.12f, 0.16f, 0.92f * alpha);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none);

            // Текст уведомления
            GUI.color = new Color(0.95f, 0.98f, 1f, alpha);
            GUI.Label(new Rect(x + 10, y + 5, width - 20, height - 10), toastMessage, toastTextStyle);

            GUI.color = prevColor;
        }
    }
}
