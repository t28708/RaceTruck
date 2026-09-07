using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Target Parking Slot Zone.
/// Highlights the target spot with bold yellow markings and detects when the truck is successfully parked.
/// </summary>
public class ParkingTargetZone : MonoBehaviour
{
    public static ParkingTargetZone Instance { get; private set; }

    [Header("Detection Parameters")]
    [SerializeField] private Vector2 targetSlotSize = new Vector2(4.5f, 22.0f);
    [SerializeField] private float requiredStayTime = 0.5f; // seconds stationary inside slot

    private Transform tractorTr;
    private Transform trailerTr;
    private Rigidbody2D tractorRb;
    private Rigidbody2D trailerRb;

    private float parkTimer = 0f;
    private bool isParkedSuccessfully = false;

    private GameObject winCanvasGo;

    public bool IsParkedSuccessfully => isParkedSuccessfully;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        FindTruckComponents();
    }

    private void FindTruckComponents()
    {
        GameObject tractor = GameObject.Find("Tractor");
        if (tractor != null)
        {
            tractorTr = tractor.transform;
            tractorRb = tractor.GetComponent<Rigidbody2D>();
        }

        GameObject trailer = GameObject.Find("Trailer");
        if (trailer != null)
        {
            trailerTr = trailer.transform;
            trailerRb = trailer.GetComponent<Rigidbody2D>();
        }
    }

    private void Update()
    {
        if (isParkedSuccessfully) return;

        if (tractorTr == null || trailerTr == null)
        {
            FindTruckComponents();
            if (tractorTr == null || trailerTr == null) return;
        }

        bool inside = IsTruckInsideTarget();
        bool stopped = IsTruckStopped();

        if (inside && stopped)
        {
            parkTimer += Time.deltaTime;
            if (parkTimer >= requiredStayTime)
            {
                TriggerSuccess();
            }
        }
        else
        {
            parkTimer = Mathf.Max(0f, parkTimer - Time.deltaTime * 2f);
        }
    }

    private bool IsTruckInsideTarget()
    {
        if (tractorTr == null || trailerTr == null) return false;

        float halfW = targetSlotSize.x * 0.5f;
        float halfL = targetSlotSize.y * 0.5f;

        // Tractor corners & key points
        Vector3 localTractorCenter = transform.InverseTransformPoint(tractorTr.position);
        Vector3 localTractorFront = transform.InverseTransformPoint(tractorTr.position + tractorTr.up * 4.1f);
        Vector3 localTractorRear = transform.InverseTransformPoint(tractorTr.position - tractorTr.up * 4.1f);

        // Trailer corners & key points
        Vector3 localTrailerCenter = transform.InverseTransformPoint(trailerTr.position);
        Vector3 localTrailerFront = transform.InverseTransformPoint(trailerTr.position + trailerTr.up * 7.95f);
        Vector3 localTrailerRear = transform.InverseTransformPoint(trailerTr.position - trailerTr.up * 7.95f);

        // Tolerance of 0.35m to account for small angle alignment in 4.5m slot
        float maxAllowedX = halfW + 0.35f;
        float maxAllowedY = halfL + 0.35f;

        bool tractorIn = Mathf.Abs(localTractorCenter.x) <= maxAllowedX && Mathf.Abs(localTractorCenter.y) <= maxAllowedY &&
                         Mathf.Abs(localTractorFront.x) <= maxAllowedX && Mathf.Abs(localTractorFront.y) <= maxAllowedY &&
                         Mathf.Abs(localTractorRear.x) <= maxAllowedX && Mathf.Abs(localTractorRear.y) <= maxAllowedY;

        bool trailerIn = Mathf.Abs(localTrailerCenter.x) <= maxAllowedX && Mathf.Abs(localTrailerCenter.y) <= maxAllowedY &&
                         Mathf.Abs(localTrailerFront.x) <= maxAllowedX && Mathf.Abs(localTrailerFront.y) <= maxAllowedY &&
                         Mathf.Abs(localTrailerRear.x) <= maxAllowedX && Mathf.Abs(localTrailerRear.y) <= maxAllowedY;

        return tractorIn && trailerIn;
    }

    private bool IsTruckStopped()
    {
        float speed = 0f;
        if (TruckController.Instance != null)
        {
            speed = Mathf.Abs(TruckController.Instance.CurrentSpeed);
        }
        else if (tractorRb != null)
        {
            speed = tractorRb.linearVelocity.magnitude;
        }
        return speed < 0.25f;
    }

    private void TriggerSuccess()
    {
        isParkedSuccessfully = true;
        Debug.Log("<color=#55ff55>[ParkingTargetZone] 🏆 ЗАДАНИЕ ВЫПОЛНЕНО! Трак и прицеп припаркованы!</color>");
        ShowWinUI();
    }

    private void ShowWinUI()
    {
        if (winCanvasGo != null)
        {
            winCanvasGo.SetActive(true);
            return;
        }

        winCanvasGo = new GameObject("VictoryCanvas");
        Canvas canvas = winCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 995;

        CanvasScaler scaler = winCanvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        winCanvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Dim background
        GameObject dimGo = new GameObject("DimBackground");
        dimGo.transform.SetParent(winCanvasGo.transform, false);
        RectTransform dimRt = dimGo.AddComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.sizeDelta = Vector2.zero;
        Image dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0.04f, 0.06f, 0.09f, 0.88f);

        // Win Modal Box
        GameObject boxGo = new GameObject("WinModalBox");
        boxGo.transform.SetParent(winCanvasGo.transform, false);
        RectTransform boxRt = boxGo.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(880, 440);
        boxRt.anchoredPosition = Vector2.zero;

        Image boxImg = boxGo.AddComponent<Image>();
        boxImg.color = new Color(0.10f, 0.13f, 0.18f, 0.98f);

        Outline outline = boxGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Win Title
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(boxGo.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.65f);
        titleRt.anchorMax = new Vector2(1, 0.95f);
        titleRt.sizeDelta = Vector2.zero;
        Text titleText = titleGo.AddComponent<Text>();
        if (font != null) titleText.font = font;
        titleText.fontSize = 42;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.15f, 1f);
        titleText.text = "🏆 ЗАДАНИЕ ВЫПОЛНЕНО";

        // Win Subtitle
        GameObject subGo = new GameObject("Subtitle");
        subGo.transform.SetParent(boxGo.transform, false);
        RectTransform subRt = subGo.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0, 0.45f);
        subRt.anchorMax = new Vector2(1, 0.65f);
        subRt.sizeDelta = Vector2.zero;
        Text subText = subGo.AddComponent<Text>();
        if (font != null) subText.font = font;
        subText.fontSize = 22;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = new Color(0.85f, 0.92f, 1f, 0.95f);
        subText.text = "Грузовик успешно припаркован в целевую зону!";

        // Button 1: Continue / Next Level
        GameObject continueGo = CreateModalButton("ContinueButton", boxGo.transform, new Vector2(0.06f, 0.12f), new Vector2(0.46f, 0.36f), new Color(0.15f, 0.72f, 0.32f, 1f), "▶ ПРОДОЛЖИТЬ", font, 24);
        continueGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            LoadNextLevel();
        });

        // Button 2: Return to Level Select Menu
        GameObject menuGo = CreateModalButton("MenuButton", boxGo.transform, new Vector2(0.50f, 0.12f), new Vector2(0.94f, 0.36f), new Color(0.18f, 0.48f, 0.88f, 1f), "🗺 ВЕРНУТЬСЯ В ОКНО ВЫБОРА УРОВНЯ", font, 18);
        menuGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            ReturnToLevelSelectMenu();
        });
    }

    private void LoadNextLevel()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        List<string> maps = new List<string>();

        // 1. Collect maps from CustomMaps directory
        string customMapsDir = System.IO.Path.Combine(Application.dataPath, "Scenes/CustomMaps");
        if (System.IO.Directory.Exists(customMapsDir))
        {
            string[] files = System.IO.Directory.GetFiles(customMapsDir, "*.unity");
            foreach (string file in files)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(name) && !maps.Contains(name))
                {
                    maps.Add(name);
                }
            }
        }

        // 2. Also check Build Settings
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (!string.IsNullOrEmpty(name) && name != "SampleScene" && name != "MainMenu" && !maps.Contains(name))
            {
                maps.Add(name);
            }
        }

        int currentIndex = maps.IndexOf(currentScene);
        if (currentIndex >= 0 && currentIndex + 1 < maps.Count)
        {
            string nextMap = maps[currentIndex + 1];
            if (MapSelectMenu.Instance != null)
            {
                MapSelectMenu.Instance.LoadMap(nextMap);
            }
            else
            {
                SceneManager.LoadScene(nextMap);
            }
        }
        else
        {
            ReturnToLevelSelectMenu();
        }
    }

    private void ReturnToLevelSelectMenu()
    {
        if (winCanvasGo != null)
        {
            winCanvasGo.SetActive(false);
        }

        if (MapSelectMenu.Instance != null)
        {
            MapSelectMenu.Instance.OpenMenu();
        }
        else
        {
            if (Application.CanStreamedLevelBeLoaded("MainMenu"))
            {
                SceneManager.LoadScene("MainMenu");
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }

    private GameObject CreateModalButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, string label, Font font, int fontSize)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = btnGo.AddComponent<Image>();
        img.color = color;
        Button btn = btnGo.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        Text t = textGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        t.text = label;

        return btnGo;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.05f, 0.4f);
        Matrix4x4 oldMat = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(targetSlotSize.x, targetSlotSize.y, 0.1f));
        Gizmos.matrix = oldMat;
    }
}
