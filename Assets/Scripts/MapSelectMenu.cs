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
/// Start & Level Selection Menu:
/// Displays all available levels in a direct scrollable grid on startup.
/// Shows a thumbs up (👍) and completion status for levels that have been completed.
/// </summary>
public class MapSelectMenu : MonoBehaviour
{
    public static MapSelectMenu Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MapSelectMenu>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MapSelectMenuController");
                    _instance = go.AddComponent<MapSelectMenu>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }
    private static MapSelectMenu _instance;

    [Header("State")]
    [SerializeField] private bool isMenuOpen = false;

    private List<string> availableMaps = new List<string>();

    private GameObject menuCanvasGo;
    private Transform mapCardsContainer;
    private Text headerTitleText;
    private GameObject topButtonGo;

    private Dictionary<string, Sprite> cachedSprites = new Dictionary<string, Sprite>();

    public bool IsMenuOpen => isMenuOpen;

    private void Awake()
    {
        Instance = this;
        EnsureEventSystem();
        LoadUISprites();
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
            open = true; // Always visible in MainMenu
        }

        isMenuOpen = open;

        if (isMenuOpen)
        {
            EnsureEventSystem();
        }

        if (menuCanvasGo == null)
        {
            BuildUI();
        }

        if (menuCanvasGo != null)
        {
            menuCanvasGo.SetActive(isMenuOpen);
            if (isMenuOpen)
            {
                Canvas cv = menuCanvasGo.GetComponent<Canvas>();
                if (cv != null) cv.enabled = true;
                menuCanvasGo.transform.SetAsLastSibling();
            }
        }

        if (topButtonGo != null)
        {
            topButtonGo.SetActive(!isMenuOpen && currentScene != "MainMenu");
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

        // 1. Scan CustomMaps folder
        string customMapsDir = Path.Combine(Application.dataPath, "Scenes/CustomMaps");
        if (Directory.Exists(customMapsDir))
        {
            string[] files = Directory.GetFiles(customMapsDir, "*.unity");
            // Natural sort: Map_1, Map_2, ..., Map_10, Map_11
            Array.Sort(files, (a, b) => NaturalCompare(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));

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

        availableMaps.Sort(NaturalCompare);
    }

    public void LoadMap(string mapName)
    {
        Debug.Log($"<color=#55ff55>[MapSelectMenu] Loading Map: {mapName}...</color>");
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

    #region Sprite & Asset Helpers
    private void LoadUISprites()
    {
        string[] spriteNames = new string[]
        {
            "UI_Panel_Frame",
            "UI_Btn_Normal",
            "UI_Btn_Special",
            "UI_Btn_Back",
            "UI_Logo_RaceTruck",
            "UI_Status_LEDs",
            "UI_Checkered_Divider"
        };

        foreach (string sName in spriteNames)
        {
            if (cachedSprites.ContainsKey(sName)) continue;

            string filePath = Path.Combine(Application.dataPath, "GeneratedSprites/UI", sName + ".png");
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(data))
                    {
                        tex.filterMode = FilterMode.Bilinear;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                        cachedSprites[sName] = sprite;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MapSelectMenu] Failed to load sprite {sName}: {ex.Message}");
                }
            }
        }
    }

    private Sprite GetSprite(string name)
    {
        if (cachedSprites.TryGetValue(name, out Sprite s)) return s;
        return null;
    }

    private Font GetAppFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private void EnsureEventSystem()
    {
        EventSystem es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
        }

        es.gameObject.SetActive(true);
        es.enabled = true;

#if ENABLE_INPUT_SYSTEM
        var standalone = es.GetComponent<StandaloneInputModule>();
        if (standalone != null) Destroy(standalone);

        var inputModule = es.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        if (inputModule != null)
        {
            inputModule.enabled = true;
            inputModule.AssignDefaultActions();
        }
#else
        var standaloneModule = es.GetComponent<StandaloneInputModule>();
        if (standaloneModule == null)
        {
            standaloneModule = es.gameObject.AddComponent<StandaloneInputModule>();
        }
        standaloneModule.enabled = true;
#endif
    }
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        Font font = GetAppFont();

        if (menuCanvasGo != null)
        {
            Destroy(menuCanvasGo);
        }

        // 1. Root Canvas (720x1280 base)
        menuCanvasGo = new GameObject("MapSelectMenuCanvas");
        menuCanvasGo.transform.SetParent(null, false);

        Canvas canvas = menuCanvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = menuCanvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.matchWidthOrHeight = 0.5f;

        menuCanvasGo.AddComponent<GraphicRaycaster>();

        // 2. Fullscreen Dark Dimming Backdrop
        GameObject bgGo = CreateUIObject("DarkBackdrop", menuCanvasGo.transform);
        StretchFull(bgGo.GetComponent<RectTransform>());
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.07f, 0.09f, 0.96f);

        // 3. Top Header Bar (RACE TRUCK Logo on Left, LEDs on Right)
        GameObject topBarGo = CreateUIObject("TopBar", menuCanvasGo.transform);
        RectTransform topBarRt = topBarGo.GetComponent<RectTransform>();
        topBarRt.anchorMin = new Vector2(0.5f, 1f);
        topBarRt.anchorMax = new Vector2(0.5f, 1f);
        topBarRt.pivot = new Vector2(0.5f, 1f);
        topBarRt.sizeDelta = new Vector2(640, 90);
        topBarRt.anchoredPosition = new Vector2(0, -35);

        // Logo
        GameObject logoGo = CreateUIObject("LogoRaceTruck", topBarGo.transform);
        RectTransform logoRt = logoGo.GetComponent<RectTransform>();
        logoRt.anchorMin = new Vector2(0, 0.5f);
        logoRt.anchorMax = new Vector2(0, 0.5f);
        logoRt.pivot = new Vector2(0.5f, 0.5f);
        logoRt.sizeDelta = new Vector2(320, 75);
        logoRt.anchoredPosition = new Vector2(0, 0);
        Image logoImg = logoGo.AddComponent<Image>();
        Sprite logoSp = GetSprite("UI_Logo_RaceTruck");
        if (logoSp != null) logoImg.sprite = logoSp;
        else logoImg.color = Color.clear;

        // LEDs / Status Right
        GameObject ledsGo = CreateUIObject("StatusLEDs", topBarGo.transform);
        RectTransform ledsRt = ledsGo.GetComponent<RectTransform>();
        ledsRt.anchorMin = new Vector2(1, 0.5f);
        ledsRt.anchorMax = new Vector2(1, 0.5f);
        ledsRt.pivot = new Vector2(1, 0.5f);
        ledsRt.sizeDelta = new Vector2(200, 40);
        ledsRt.anchoredPosition = new Vector2(0, 5);
        Image ledsImg = ledsGo.AddComponent<Image>();
        Sprite ledsSp = GetSprite("UI_Status_LEDs");
        if (ledsSp != null) ledsImg.sprite = ledsSp;
        else ledsImg.color = Color.clear;

        // 4. Central Window Frame (640 x 1020)
        GameObject centerWindowGo = CreateUIObject("CenterWindowPanel", menuCanvasGo.transform);
        RectTransform centerRt = centerWindowGo.GetComponent<RectTransform>();
        centerRt.anchorMin = new Vector2(0.5f, 0.5f);
        centerRt.anchorMax = new Vector2(0.5f, 0.5f);
        centerRt.pivot = new Vector2(0.5f, 0.5f);
        centerRt.sizeDelta = new Vector2(640, 1020);
        centerRt.anchoredPosition = new Vector2(0, -30);

        // Frame Graphic
        Image centerImg = centerWindowGo.AddComponent<Image>();
        Sprite frameSp = GetSprite("UI_Panel_Frame");
        if (frameSp != null) centerImg.sprite = frameSp;
        else centerImg.color = new Color(0.14f, 0.17f, 0.21f, 1.0f);

        // Header Title Text ("ВЫБОР КАРТЫ")
        GameObject titleGo = CreateUIObject("HeaderTitle", centerWindowGo.transform);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.sizeDelta = new Vector2(0, 75);
        titleRt.anchoredPosition = new Vector2(0, -5);

        headerTitleText = titleGo.AddComponent<Text>();
        if (font != null) headerTitleText.font = font;
        headerTitleText.fontSize = 28;
        headerTitleText.fontStyle = FontStyle.Bold;
        headerTitleText.alignment = TextAnchor.MiddleCenter;
        headerTitleText.color = new Color(1f, 0.88f, 0.20f);
        headerTitleText.text = "ВЫБОР КАРТЫ";

        Shadow textShadow = titleGo.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0, 0, 0, 0.9f);
        textShadow.effectDistance = new Vector2(2, -2);

        // 5. Scroll View for Maps (Middle Area)
        GameObject scrollGo = CreateUIObject("MapsScrollView", centerWindowGo.transform);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);

        scrollRt.offsetMin = new Vector2(25, 30);
        scrollRt.offsetMax = new Vector2(-25, -85);

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        // Viewport
        GameObject viewportGo = CreateUIObject("Viewport", scrollGo.transform);
        StretchFull(viewportGo.GetComponent<RectTransform>());
        Image vpImg = viewportGo.AddComponent<Image>();
        vpImg.color = Color.white;
        Mask mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content Container (2 Columns Grid)
        GameObject contentGo = CreateUIObject("Content", viewportGo.transform);
        RectTransform contentRt = contentGo.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(1, 1);
        contentRt.pivot = new Vector2(0.5f, 1);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0, 0);

        GridLayoutGroup glg = contentGo.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(275, 125);
        glg.spacing = new Vector2(25, 20);
        glg.padding = new RectOffset(5, 5, 15, 25);
        glg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
        scrollRect.content = contentRt;
        mapCardsContainer = contentGo.transform;

        PopulateCards();
    }

    public void RestartCurrentMap()
    {
        SetMenuOpen(false);
        string currentScene = SceneManager.GetActiveScene().name;
        string currentPath = SceneManager.GetActiveScene().path;

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(currentPath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MapSelectMenu] EditorSceneManager.LoadSceneInPlayMode error: {ex.Message}");
            }
        }
#endif
        SceneManager.LoadScene(currentScene);
    }

    private GameObject CreateBackButton(Transform parent, string label, Vector2 centerPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        Font font = GetAppFont();

        GameObject btnGo = CreateUIObject("Btn_" + label, parent);
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = centerPos;

        Image img = btnGo.AddComponent<Image>();
        Sprite sp = GetSprite("UI_Btn_Back");
        if (sp != null) img.sprite = sp;
        else img.color = new Color(1.0f, 0.70f, 0.0f);

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        // Text
        GameObject textGo = CreateUIObject("Label", btnGo.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<RectTransform>().offsetMin = new Vector2(5, 5);
        textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-5, -5);

        Text text = textGo.AddComponent<Text>();
        if (font != null) text.font = font;
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.08f, 0.10f, 0.15f); // Dark bold text on amber
        text.raycastTarget = false;
        text.text = label;

        return btnGo;
    }

    private void PopulateCards()
    {
        if (mapCardsContainer == null) return;

        // Clear previous cards
        for (int i = mapCardsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(mapCardsContainer.GetChild(i).gameObject);
        }

        Font font = GetAppFont();

        if (availableMaps.Count == 0)
        {
            GameObject emptyGo = CreateUIObject("EmptyText", mapCardsContainer);
            LayoutElement le = emptyGo.AddComponent<LayoutElement>();
            le.minWidth = 550;
            le.minHeight = 150;

            Text t = emptyGo.AddComponent<Text>();
            if (font != null) t.font = font;
            t.fontSize = 22;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            t.text = "🗺 Пока нет созданных карт.\nСоздайте новую карту через Tools -> Map Builder.";
            return;
        }

        foreach (string mapName in availableMaps)
        {
            string capturedName = mapName;
            bool isCompleted = PlayerPrefs.GetInt("MapCompleted_" + capturedName, 0) == 1;
            string displayName = FormatMapDisplayName(capturedName);

            GameObject cardGo = CreateUIObject($"Card_{capturedName}", mapCardsContainer);
            RectTransform cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(275, 125);

            Image cardImg = cardGo.AddComponent<Image>();
            Sprite sp = GetSprite(isCompleted ? "UI_Btn_Special" : "UI_Btn_Normal");
            if (sp != null) cardImg.sprite = sp;
            else cardImg.color = isCompleted ? new Color(0.18f, 0.28f, 0.18f) : new Color(0.18f, 0.22f, 0.28f);

            Button cardBtn = cardGo.AddComponent<Button>();
            cardBtn.targetGraphic = cardImg;
            ColorBlock cb = cardBtn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.3f, 0.8f, 0.4f, 1f);
            cardBtn.colors = cb;

            cardBtn.onClick.AddListener(() =>
            {
                LoadMap(capturedName);
            });

            // Card Text Label
            GameObject textGo = CreateUIObject("Title", cardGo.transform);
            StretchFull(textGo.GetComponent<RectTransform>());
            textGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 8);
            textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -6);

            Text text = textGo.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = 19;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;

            if (isCompleted)
            {
                text.color = new Color(1.0f, 0.92f, 0.35f); // Golden yellow
                text.text = $"👍 {displayName}\n<size=15><color=#55ff88>ВЫПОЛНЕНО</color></size>";
            }
            else
            {
                text.color = Color.white;
                text.text = displayName;
            }

            Shadow ts = textGo.AddComponent<Shadow>();
            ts.effectColor = new Color(0, 0, 0, 0.85f);
            ts.effectDistance = new Vector2(1.5f, -1.5f);

            // If completed, add a distinctive outline
            if (isCompleted)
            {
                Outline outline = cardGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.85f, 0.35f, 0.75f); // Bright green outline
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }
    }

    public static string FormatMapDisplayName(string rawName)
    {
        string formatted = rawName.Replace("-", " ").Replace("_", " ").ToUpperInvariant();
        string[] parts = formatted.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2 && parts[0] == "MAP")
        {
            string num = parts[1];
            string rest = string.Join(" ", parts, 2, parts.Length - 2);
            if (string.IsNullOrEmpty(rest)) return $"MAP {num}";
            if (rest.Length > 16)
            {
                rest = rest.Replace("STANDART", "STD").Replace("SMALLER", "SM").Replace("SMALL", "SM");
            }
            return $"MAP {num}\n{rest}";
        }

        return formatted;
    }

    public static int NaturalCompare(string a, string b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        int ia = 0, ib = 0;
        int lenA = a.Length, lenB = b.Length;

        while (ia < lenA && ib < lenB)
        {
            char ca = a[ia];
            char cb = b[ib];

            if (char.IsDigit(ca) && char.IsDigit(cb))
            {
                int startA = ia;
                while (ia < lenA && char.IsDigit(a[ia])) ia++;
                int startB = ib;
                while (ib < lenB && char.IsDigit(b[ib])) ib++;

                string numStrA = a.Substring(startA, ia - startA).TrimStart('0');
                string numStrB = b.Substring(startB, ib - startB).TrimStart('0');

                if (numStrA.Length != numStrB.Length)
                {
                    return numStrA.Length.CompareTo(numStrB.Length);
                }

                int numComp = string.CompareOrdinal(numStrA, numStrB);
                if (numComp != 0) return numComp;

                int origLenComp = (ia - startA).CompareTo(ib - startB);
                if (origLenComp != 0) return origLenComp;
            }
            else
            {
                int charComp = char.ToLowerInvariant(ca).CompareTo(char.ToLowerInvariant(cb));
                if (charComp != 0) return charComp;
                ia++;
                ib++;
            }
        }

        return lenA.CompareTo(lenB);
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
    #endregion
}
