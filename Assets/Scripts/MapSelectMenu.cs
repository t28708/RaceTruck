using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Full-Screen, Mobile-Optimized uGUI Level Selection Menu.
/// Uses native Unity UI Canvas, Buttons, and EventSystem for 100% reliable mouse and touch clicks.
/// </summary>
public class MapSelectMenu : MonoBehaviour
{
    public static MapSelectMenu Instance { get; private set; }

    [Header("State")]
    [SerializeField] private bool isMenuOpen = false;

    private List<string> availableMaps = new List<string>();

    private GameObject menuCanvasGo;
    private GameObject topButtonGo;
    private Transform cardContainer;
    private GameObject closeBtnGo;
    private GameObject returnBtnGo;

    public bool IsMenuOpen => isMenuOpen;

    private void Awake()
    {
        Instance = this;
        EnsureEventSystem();
        RefreshMapsList();
        BuildUI();

        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu" || currentScene == "SampleScene")
        {
            SetMenuOpen(true);
        }
        else
        {
            SetMenuOpen(false);
        }
    }

    private void Update()
    {
        // Toggle menu with ESC or M keys
        bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.mKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
        {
            togglePressed = true;
        }
#endif

        if (togglePressed)
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu")
        {
            SetMenuOpen(true);
            return;
        }

        SetMenuOpen(!isMenuOpen);
    }

    public void OpenMenu() => SetMenuOpen(true);
    public void CloseMenu() => SetMenuOpen(false);

    public void SetMenuOpen(bool open)
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu")
        {
            open = true; // Always open in MainMenu
        }

        isMenuOpen = open;

        if (menuCanvasGo != null)
        {
            menuCanvasGo.SetActive(isMenuOpen);
        }

        if (topButtonGo != null)
        {
            topButtonGo.SetActive(!isMenuOpen && currentScene != "MainMenu");
        }

        if (closeBtnGo != null)
        {
            closeBtnGo.SetActive(currentScene != "MainMenu");
        }

        if (returnBtnGo != null)
        {
            returnBtnGo.SetActive(currentScene != "MainMenu");
        }

        if (isMenuOpen)
        {
            RefreshMapsList();
            PopulateCards();
        }
    }

    public void RefreshMapsList()
    {
        availableMaps.Clear();

        // 1. Check CustomMaps folder
        string customMapsDir = Path.Combine(Application.dataPath, "Scenes/CustomMaps");
        if (Directory.Exists(customMapsDir))
        {
            string[] files = Directory.GetFiles(customMapsDir, "*.unity");
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrEmpty(name) && !availableMaps.Contains(name))
                {
                    availableMaps.Add(name);
                }
            }
        }

        // 2. Also check registered scenes in build settings
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (!string.IsNullOrEmpty(sceneName) && sceneName != "SampleScene" && sceneName != "MainMenu" && !availableMaps.Contains(sceneName))
            {
                availableMaps.Add(sceneName);
            }
        }
    }

    public void LoadMap(string mapName)
    {
        Debug.Log($"<color=#55ff55>[MapSelectMenu] Загрузка карты: {mapName}...</color>");
        SetMenuOpen(false);

#if UNITY_EDITOR
        string customPath = $"Assets/Scenes/CustomMaps/{mapName}.unity";
        if (File.Exists(customPath))
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(customPath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MapSelectMenu] EditorSceneManager.LoadSceneInPlayMode error: {ex.Message}");
            }
        }
#endif

        try
        {
            SceneManager.LoadScene(mapName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MapSelectMenu] SceneManager.LoadScene('{mapName}') failed: {ex.Message}");
        }
    }

    private void EnsureEventSystem()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGo.AddComponent<InputSystemUIInputModule>();
#else
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }
#if ENABLE_INPUT_SYSTEM
        else if (es.GetComponent<InputSystemUIInputModule>() == null && es.GetComponent<StandaloneInputModule>() == null)
        {
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
#endif
    }

    private Font GetAppFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private void BuildUI()
    {
        Font font = GetAppFont();

        // 1. Menu Full-Screen Canvas
        menuCanvasGo = new GameObject("MapSelectMenuCanvas");
        menuCanvasGo.transform.SetParent(transform, false);

        Canvas canvas = menuCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // On top of everything

        CanvasScaler scaler = menuCanvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        menuCanvasGo.AddComponent<GraphicRaycaster>();

        // Background Panel
        GameObject bgGo = CreateUIObject("DarkBackground", menuCanvasGo.transform);
        StretchFull(bgGo.GetComponent<RectTransform>());
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.08f, 0.12f, 1.0f);

        // Header Panel (Top: 140px)
        GameObject headerGo = CreateUIObject("HeaderPanel", menuCanvasGo.transform);
        RectTransform headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0, 1);
        headerRt.anchorMax = new Vector2(1, 1);
        headerRt.pivot = new Vector2(0.5f, 1);
        headerRt.sizeDelta = new Vector2(0, 140);
        headerRt.anchoredPosition = Vector2.zero;

        Image headerImg = headerGo.AddComponent<Image>();
        headerImg.color = new Color(0.10f, 0.13f, 0.19f, 1.0f);

        // Header Title
        GameObject titleGo = CreateUIObject("TitleText", headerGo.transform);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.45f);
        titleRt.anchorMax = new Vector2(1, 0.95f);
        titleRt.sizeDelta = Vector2.zero;
        titleRt.anchoredPosition = Vector2.zero;

        Text titleText = titleGo.AddComponent<Text>();
        if (font != null) titleText.font = font;
        titleText.fontSize = 38;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.20f, 1.0f);
        titleText.text = "🚛 TRUCK PARKING SIMULATOR";

        // Header Subtitle
        GameObject subGo = CreateUIObject("SubtitleText", headerGo.transform);
        RectTransform subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0, 0.05f);
        subRt.anchorMax = new Vector2(1, 0.45f);
        subRt.sizeDelta = Vector2.zero;
        subRt.anchoredPosition = Vector2.zero;

        Text subText = subGo.AddComponent<Text>();
        if (font != null) subText.font = font;
        subText.fontSize = 20;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = new Color(0.75f, 0.88f, 0.98f, 1.0f);
        subText.text = "НАЖМИТЕ НА ЛЮБУЮ КАРТУ В СПИСКЕ ДЛЯ ЗАПУСКА";

        // Close '✕' Button (Top-Right)
        closeBtnGo = CreateUIObject("CloseButton", headerGo.transform);
        RectTransform closeRt = closeBtnGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 0.5f);
        closeRt.anchorMax = new Vector2(1, 0.5f);
        closeRt.pivot = new Vector2(1, 0.5f);
        closeRt.sizeDelta = new Vector2(70, 70);
        closeRt.anchoredPosition = new Vector2(-30, 0);

        Image closeImg = closeBtnGo.AddComponent<Image>();
        closeImg.color = new Color(0.85f, 0.20f, 0.20f, 1.0f);
        Button closeBtn = closeBtnGo.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseMenu);

        GameObject closeTextGo = CreateUIObject("Text", closeBtnGo.transform);
        StretchFull(closeTextGo.GetComponent<RectTransform>());
        Text closeText = closeTextGo.AddComponent<Text>();
        if (font != null) closeText.font = font;
        closeText.fontSize = 32;
        closeText.fontStyle = FontStyle.Bold;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.text = "✕";

        // Header Divider
        GameObject headerDiv = CreateUIObject("Divider", headerGo.transform);
        RectTransform divRt = headerDiv.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0, 0);
        divRt.anchorMax = new Vector2(1, 0);
        divRt.sizeDelta = new Vector2(0, 3);
        divRt.anchoredPosition = Vector2.zero;
        Image divImg = headerDiv.AddComponent<Image>();
        divImg.color = new Color(0.20f, 0.38f, 0.65f, 0.8f);

        // Footer Panel (Bottom: 90px)
        GameObject footerGo = CreateUIObject("FooterPanel", menuCanvasGo.transform);
        RectTransform footerRt = footerGo.GetComponent<RectTransform>();
        footerRt.anchorMin = new Vector2(0, 0);
        footerRt.anchorMax = new Vector2(1, 0);
        footerRt.pivot = new Vector2(0.5f, 0);
        footerRt.sizeDelta = new Vector2(0, 90);
        footerRt.anchoredPosition = Vector2.zero;

        Image footerImg = footerGo.AddComponent<Image>();
        footerImg.color = new Color(0.10f, 0.13f, 0.19f, 1.0f);

        // Footer Divider
        GameObject footerDiv = CreateUIObject("Divider", footerGo.transform);
        RectTransform fDivRt = footerDiv.GetComponent<RectTransform>();
        fDivRt.anchorMin = new Vector2(0, 1);
        fDivRt.anchorMax = new Vector2(1, 1);
        fDivRt.sizeDelta = new Vector2(0, 3);
        fDivRt.anchoredPosition = Vector2.zero;
        Image fDivImg = footerDiv.AddComponent<Image>();
        fDivImg.color = new Color(0.20f, 0.38f, 0.65f, 0.8f);

        // Footer Text
        GameObject footerTextGo = CreateUIObject("FooterText", footerGo.transform);
        RectTransform fTextRt = footerTextGo.GetComponent<RectTransform>();
        fTextRt.anchorMin = new Vector2(0, 0);
        fTextRt.anchorMax = new Vector2(0.7f, 1);
        fTextRt.offsetMin = new Vector2(40, 0);
        fTextRt.offsetMax = Vector2.zero;

        Text fText = footerTextGo.AddComponent<Text>();
        if (font != null) fText.font = font;
        fText.fontSize = 20;
        fText.alignment = TextAnchor.MiddleLeft;
        fText.color = new Color(0.70f, 0.80f, 0.90f, 0.95f);
        fText.text = "💡 Управление: W/S — Газ / Тормоз, A/D — Руль, Пробел — Ручник, C — Камера";

        // Return Button in Footer
        returnBtnGo = CreateUIObject("ReturnButton", footerGo.transform);
        RectTransform retRt = returnBtnGo.GetComponent<RectTransform>();
        retRt.anchorMin = new Vector2(1, 0.5f);
        retRt.anchorMax = new Vector2(1, 0.5f);
        retRt.pivot = new Vector2(1, 0.5f);
        retRt.sizeDelta = new Vector2(260, 56);
        retRt.anchoredPosition = new Vector2(-30, 0);

        Image retImg = returnBtnGo.AddComponent<Image>();
        retImg.color = new Color(0.18f, 0.48f, 0.88f, 1.0f);
        Button retBtn = returnBtnGo.AddComponent<Button>();
        retBtn.onClick.AddListener(CloseMenu);

        GameObject retTextGo = CreateUIObject("Text", returnBtnGo.transform);
        StretchFull(retTextGo.GetComponent<RectTransform>());
        Text retText = retTextGo.AddComponent<Text>();
        if (font != null) retText.font = font;
        retText.fontSize = 20;
        retText.fontStyle = FontStyle.Bold;
        retText.alignment = TextAnchor.MiddleCenter;
        retText.color = Color.white;
        retText.text = "← В ИГРУ";

        // Scroll View (Middle Area)
        GameObject scrollGo = CreateUIObject("MapScrollView", menuCanvasGo.transform);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(60, 110);
        scrollRt.offsetMax = new Vector2(-60, -160);

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject viewportGo = CreateUIObject("Viewport", scrollGo.transform);
        StretchFull(viewportGo.GetComponent<RectTransform>());
        Image vpImg = viewportGo.AddComponent<Image>();
        vpImg.color = Color.white;
        Mask mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content
        GameObject contentGo = CreateUIObject("Content", viewportGo.transform);
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 18;
        vlg.padding = new RectOffset(10, 10, 15, 15);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
        scrollRect.content = contentRt;

        cardContainer = contentGo.transform;

        // 2. In-Game Top-Left Switcher Button (Standalone Canvas)
        topButtonGo = new GameObject("TopLeftMapButtonCanvas");
        topButtonGo.transform.SetParent(transform, false);

        Canvas topCanvas = topButtonGo.AddComponent<Canvas>();
        topCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        topCanvas.sortingOrder = 998;

        CanvasScaler topScaler = topButtonGo.AddComponent<CanvasScaler>();
        topScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        topScaler.referenceResolution = new Vector2(1920, 1080);
        topScaler.matchWidthOrHeight = 0.5f;

        topButtonGo.AddComponent<GraphicRaycaster>();

        GameObject topBtnItem = CreateUIObject("MapButton", topButtonGo.transform);
        RectTransform topRt = topBtnItem.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0, 1);
        topRt.anchorMax = new Vector2(0, 1);
        topRt.pivot = new Vector2(0, 1);
        topRt.anchoredPosition = new Vector2(30, -30);
        topRt.sizeDelta = new Vector2(240, 60);

        Image topBtnImg = topBtnItem.AddComponent<Image>();
        topBtnImg.color = new Color(0.12f, 0.48f, 0.92f, 0.95f);
        Button topBtn = topBtnItem.AddComponent<Button>();
        topBtn.onClick.AddListener(ToggleMenu);

        GameObject topBtnTextGo = CreateUIObject("Text", topBtnItem.transform);
        StretchFull(topBtnTextGo.GetComponent<RectTransform>());
        Text topBtnText = topBtnTextGo.AddComponent<Text>();
        if (font != null) topBtnText.font = font;
        topBtnText.fontSize = 20;
        topBtnText.fontStyle = FontStyle.Bold;
        topBtnText.alignment = TextAnchor.MiddleCenter;
        topBtnText.color = Color.white;
        topBtnText.text = "🗺 ВЫБОР КАРТ [ESC]";
    }

    private void PopulateCards()
    {
        if (cardContainer == null) return;

        // Clear previous cards
        for (int i = cardContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(cardContainer.GetChild(i).gameObject);
        }

        Font font = GetAppFont();

        if (availableMaps.Count == 0)
        {
            GameObject emptyGo = CreateUIObject("EmptyText", cardContainer);
            LayoutElement le = emptyGo.AddComponent<LayoutElement>();
            le.minHeight = 120;

            Text t = emptyGo.AddComponent<Text>();
            if (font != null) t.font = font;
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            t.text = "🗺 Пока нет созданных карт.\nСоздайте первую карту через верхнее меню Unity: Tools -> Map Builder.";
            return;
        }

        foreach (string mapName in availableMaps)
        {
            string capturedName = mapName;

            GameObject cardGo = CreateUIObject($"Card_{capturedName}", cardContainer);
            LayoutElement cardLe = cardGo.AddComponent<LayoutElement>();
            cardLe.minHeight = 130;
            cardLe.preferredHeight = 130;

            Image cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.15f, 0.20f, 0.28f, 1.0f);
            cardImg.raycastTarget = true;

            Button cardBtn = cardGo.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            ColorBlock cb = cardBtn.colors;
            cb.normalColor = new Color(0.15f, 0.20f, 0.28f, 1.0f);
            cb.highlightedColor = new Color(0.24f, 0.34f, 0.50f, 1.0f);
            cb.pressedColor = new Color(0.18f, 0.65f, 0.32f, 1.0f);
            cb.selectedColor = new Color(0.24f, 0.34f, 0.50f, 1.0f);
            cardBtn.colors = cb;

            cardBtn.onClick.AddListener(() =>
            {
                LoadMap(capturedName);
            });

            // Map Title & Details Text (Left Side)
            GameObject textGo = CreateUIObject("InfoText", cardGo.transform);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = new Vector2(0.85f, 1);
            textRt.offsetMin = new Vector2(35, 10);
            textRt.offsetMax = new Vector2(0, -10);

            Text text = textGo.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = true;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = $"🗺  <b>{capturedName}</b>\n<size=20><color=#44E877>● Нажмите для открытия карты | Сетка 4.5м | Трак + Полуприцеп</color></size>";

            // Play Arrow Icon (Right Side)
            GameObject arrowGo = CreateUIObject("ArrowIcon", cardGo.transform);
            RectTransform arrowRt = arrowGo.GetComponent<RectTransform>();
            arrowRt.anchorMin = new Vector2(0.85f, 0);
            arrowRt.anchorMax = Vector2.one;
            arrowRt.offsetMin = Vector2.zero;
            arrowRt.offsetMax = new Vector2(-30, 0);

            Text arrowText = arrowGo.AddComponent<Text>();
            if (font != null) arrowText.font = font;
            arrowText.fontSize = 44;
            arrowText.fontStyle = FontStyle.Bold;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = new Color(0.25f, 0.90f, 0.45f, 1.0f);
            arrowText.raycastTarget = false;
            arrowText.text = "▶";
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
