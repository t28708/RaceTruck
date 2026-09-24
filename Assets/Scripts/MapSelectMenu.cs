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
    [SerializeField] private string currentCategory = null;

    public static string PendingCategory = null;
    public const string PrefKey_LastCategory = "LastSelectedCategory";
    public const string PrefKey_JustCompletedMap = "JustCompletedMap";
    public static string JustCompletedMapName = null;
    private static string activeAnimatedCompletedMap = null;

    private List<string> availableMaps = new List<string>();

    private GameObject menuCanvasGo;
    private Transform mapCardsContainer;
    private ScrollRect menuScrollRect;
    private Text headerTitleText;
    private GameObject topButtonGo;
    private GameObject backButtonGo;
    private GameObject closeButtonGo;
    private GameObject languageButtonGo;
    private Text backButtonText;
    private Text closeButtonText;
    private Text languageButtonText;

    private Dictionary<string, Sprite> cachedSprites = new Dictionary<string, Sprite>();

    public bool IsMenuOpen => isMenuOpen;
    public string CurrentCategory => currentCategory;

    [System.Serializable]
    public class CategoryInfo
    {
        public string id;
        public string title;
        public string icon;
        public string keyword;
    }

    public static readonly CategoryInfo[] Categories = new CategoryInfo[]
    {
        new CategoryInfo { id = "Alley dock",       title = "ALLEY DOCK",       icon = "", keyword = "alleydock" },
        new CategoryInfo { id = "Angle Back",       title = "ANGLE BACK",       icon = "", keyword = "angleback" },
        new CategoryInfo { id = "Facility",         title = "FACILITY",         icon = "", keyword = "facility" },
        new CategoryInfo { id = "Parallel Parking", title = "PARALLEL PARKING", icon = "", keyword = "parallel" },
        new CategoryInfo { id = "Rest Area",        title = "REST AREA",        icon = "", keyword = "restarea" },
        new CategoryInfo { id = "Truck Stop",       title = "TRUCK STOP",       icon = "", keyword = "truckstop" }
    };

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        UpdateLocalizedTexts();
        if (isMenuOpen)
        {
            PopulateCards();
        }
    }

    private void UpdateLocalizedTexts()
    {
        if (backButtonText != null)
        {
            backButtonText.text = LocalizationManager.Get("BTN_BACK");
        }
        if (closeButtonText != null)
        {
            closeButtonText.text = LocalizationManager.Get("BTN_CLOSE");
        }
        if (languageButtonText != null)
        {
            languageButtonText.text = LocalizationManager.GetLanguageShortButtonText();
        }
        if (headerTitleText != null)
        {
            if (string.IsNullOrEmpty(currentCategory))
            {
                headerTitleText.text = LocalizationManager.Get("MENU_SELECT_MODE");
            }
            else
            {
                CategoryInfo catInfo = GetCategoryInfo(currentCategory);
                string iconPrefix = (catInfo != null && !string.IsNullOrEmpty(catInfo.icon)) ? (catInfo.icon + " ") : "";
                string displayTitle = GetCategoryTitle(currentCategory);
                headerTitleText.text = $"{iconPrefix}{displayTitle}";
            }
        }
    }

    public static string GetCategoryTitle(string catId)
    {
        if (string.IsNullOrEmpty(catId)) return "";
        switch (catId)
        {
            case "Alley dock":       return "ALLEY DOCK";
            case "Angle Back":       return "ANGLE BACK";
            case "Facility":         return "FACILITY";
            case "Parallel Parking": return "PARALLEL PARKING";
            case "Rest Area":        return "REST AREA";
            case "Truck Stop":       return "TRUCK STOP";
            case "Other":            return "OTHER MAPS";
            default:                 return catId.ToUpperInvariant();
        }
    }

    private void Awake()
    {
        Instance = this;

        if (!string.IsNullOrEmpty(JustCompletedMapName))
        {
            activeAnimatedCompletedMap = JustCompletedMapName;
            JustCompletedMapName = null;
            PlayerPrefs.DeleteKey(PrefKey_JustCompletedMap);
            PlayerPrefs.Save();
        }
        else if (PlayerPrefs.HasKey(PrefKey_JustCompletedMap))
        {
            activeAnimatedCompletedMap = PlayerPrefs.GetString(PrefKey_JustCompletedMap, "");
            PlayerPrefs.DeleteKey(PrefKey_JustCompletedMap);
            PlayerPrefs.Save();
        }
        else
        {
            activeAnimatedCompletedMap = null;
        }

        EnsureEventSystem();
        LoadUISprites();
        RefreshMapsList();
        BuildUI();

        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu" || currentScene == "SampleScene")
        {
            string catToOpen = !string.IsNullOrEmpty(PendingCategory)
                ? PendingCategory
                : PlayerPrefs.GetString(PrefKey_LastCategory, "");

            if (!string.IsNullOrEmpty(catToOpen) && GetCategoryInfo(catToOpen) != null)
            {
                currentCategory = catToOpen;
            }
            else
            {
                currentCategory = null;
            }
            SetMenuOpen(true);
        }
        else
        {
            string cat = GetMapCategory(currentScene);
            currentCategory = cat;
            PendingCategory = cat;
            PlayerPrefs.SetString(PrefKey_LastCategory, cat);
            SetMenuOpen(false);
        }
    }

    private void Update()
    {
        // Toggle menu with ESC or M keys
        bool togglePressed = false;
        bool backPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.mKey.wasPressedThisFrame)
            {
                togglePressed = true;
            }
            if (Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                backPressed = true;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
        {
            togglePressed = true;
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            backPressed = true;
        }
#endif

        if (isMenuOpen && !string.IsNullOrEmpty(currentCategory) && (backPressed || togglePressed))
        {
            BackToCategories();
            return;
        }

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

        if (closeButtonGo != null)
        {
            closeButtonGo.SetActive(currentScene != "MainMenu");
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
#if UNITY_EDITOR
                if (!File.Exists(scenePath)) continue;
#endif
                availableMaps.Add(sceneName);
            }
        }

        availableMaps.Sort(NaturalCompare);
    }

    public void LoadMap(string mapName)
    {
        Debug.Log($"<color=#55ff55>[MapSelectMenu] Loading Map: {mapName}...</color>");
        activeAnimatedCompletedMap = null;
        JustCompletedMapName = null;
        PlayerPrefs.DeleteKey(PrefKey_JustCompletedMap);

        string cat = GetMapCategory(mapName);
        PendingCategory = cat;
        PlayerPrefs.SetString(PrefKey_LastCategory, cat);
        PlayerPrefs.SetString("CurrentActiveMap", mapName);
        PlayerPrefs.SetString("LastPlayedMap", mapName);
        PlayerPrefs.Save();
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
            "UI_Btn_Active",
            "UI_Card_Category",
            "UI_Card_Category_Gold",
            "UI_Btn_Back",
            "UI_Btn_Close",
            "UI_ProgressBar_Slot",
            "UI_ProgressBar_Fill",
            "UI_ProgressBar_Fill_Gold",
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

        // 2. Fullscreen Dark Dimming Backdrop (clicking empty area outside returns to map if not MainMenu)
        GameObject bgGo = CreateUIObject("DarkBackdrop", menuCanvasGo.transform);
        StretchFull(bgGo.GetComponent<RectTransform>());
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.07f, 0.09f, 0.96f);
        bgImg.raycastTarget = true;

        Button bgBtn = bgGo.AddComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(() =>
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
            {
                CloseMenu();
            }
        });

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
        centerImg.raycastTarget = true;
        Sprite frameSp = GetSprite("UI_Panel_Frame");
        if (frameSp != null) centerImg.sprite = frameSp;
        else centerImg.color = new Color(0.14f, 0.17f, 0.21f, 1.0f);

        // Header Title Text
        GameObject titleGo = CreateUIObject("HeaderTitle", centerWindowGo.transform);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.sizeDelta = new Vector2(-280, 75);
        titleRt.anchoredPosition = new Vector2(0, -5);

        headerTitleText = titleGo.AddComponent<Text>();
        if (font != null) headerTitleText.font = font;
        headerTitleText.fontSize = 26;
        headerTitleText.fontStyle = FontStyle.Bold;
        headerTitleText.alignment = TextAnchor.MiddleCenter;
        headerTitleText.color = new Color(1f, 0.88f, 0.20f);
        headerTitleText.text = LocalizationManager.Get("MENU_SELECT_MODE");

        Shadow textShadow = titleGo.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0, 0, 0, 0.9f);
        textShadow.effectDistance = new Vector2(2, -2);

        // Header Back Button (Left: visible when inside a category)
        GameObject backGo = CreateUIObject("Btn_BackToCategories", centerWindowGo.transform);
        RectTransform backRt = backGo.GetComponent<RectTransform>();
        backRt.anchorMin = new Vector2(0, 1);
        backRt.anchorMax = new Vector2(0, 1);
        backRt.pivot = new Vector2(0, 1);
        backRt.sizeDelta = new Vector2(125, 46);
        backRt.anchoredPosition = new Vector2(20, -18);

        Image backImg = backGo.AddComponent<Image>();
        Sprite backSp = GetSprite("UI_Btn_Back");
        if (backSp != null) backImg.sprite = backSp;
        backImg.color = Color.white;

        Button backBtn = backGo.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        ColorBlock backCb = backBtn.colors;
        backCb.normalColor = Color.white;
        backCb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        backCb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        backBtn.colors = backCb;
        backBtn.onClick.AddListener(BackToCategories);

        GameObject backTextGo = CreateUIObject("Label", backGo.transform);
        StretchFull(backTextGo.GetComponent<RectTransform>());
        backTextGo.GetComponent<RectTransform>().offsetMin = new Vector2(4, 6);
        backTextGo.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -2);
        Text backText = backTextGo.AddComponent<Text>();
        if (font != null) backText.font = font;
        backText.fontSize = 18;
        backText.fontStyle = FontStyle.Bold;
        backText.alignment = TextAnchor.MiddleCenter;
        backText.color = new Color(0.15f, 0.10f, 0.02f);
        backText.raycastTarget = false;
        backText.text = LocalizationManager.Get("BTN_BACK");
        Shadow backShadow = backTextGo.AddComponent<Shadow>();
        backShadow.effectColor = new Color(1f, 1f, 1f, 0.35f);
        backShadow.effectDistance = new Vector2(0, -1f);
        backButtonText = backText;
        backButtonGo = backGo;
        backButtonGo.SetActive(!string.IsNullOrEmpty(currentCategory));

        // Header Close Button (Right: visible in gameplay scene, hidden in MainMenu)
        string curScene = SceneManager.GetActiveScene().name;
        GameObject closeGo = CreateUIObject("Btn_CloseMenu", centerWindowGo.transform);
        RectTransform closeRt = closeGo.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1, 1);
        closeRt.anchorMax = new Vector2(1, 1);
        closeRt.pivot = new Vector2(1, 1);
        closeRt.sizeDelta = new Vector2(110, 46);
        closeRt.anchoredPosition = new Vector2(-20, -18);

        Image closeImg = closeGo.AddComponent<Image>();
        Sprite closeSp = GetSprite("UI_Btn_Close") ?? GetSprite("UI_Btn_Back");
        if (closeSp != null) closeImg.sprite = closeSp;
        closeImg.color = Color.white;

        Button closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        ColorBlock closeCb = closeBtn.colors;
        closeCb.normalColor = Color.white;
        closeCb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        closeCb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        closeBtn.colors = closeCb;
        closeBtn.onClick.AddListener(CloseMenu);

        GameObject closeTextGo = CreateUIObject("Label", closeGo.transform);
        StretchFull(closeTextGo.GetComponent<RectTransform>());
        closeTextGo.GetComponent<RectTransform>().offsetMin = new Vector2(4, 6);
        closeTextGo.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -2);
        Text closeText = closeTextGo.AddComponent<Text>();
        if (font != null) closeText.font = font;
        closeText.fontSize = 17;
        closeText.fontStyle = FontStyle.Bold;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = new Color(0.90f, 0.94f, 1.0f);
        closeText.raycastTarget = false;
        closeText.text = LocalizationManager.Get("BTN_CLOSE");
        Shadow closeShadow = closeTextGo.AddComponent<Shadow>();
        closeShadow.effectColor = new Color(0, 0, 0, 0.85f);
        closeShadow.effectDistance = new Vector2(1f, -1f);
        closeButtonText = closeText;
        closeButtonGo = closeGo;
        closeButtonGo.SetActive(curScene != "MainMenu");

        // Header Language Switch Button (Right: visible in MainMenu where Close button is hidden)
        GameObject langGo = CreateUIObject("Btn_Language", centerWindowGo.transform);
        RectTransform langRt = langGo.GetComponent<RectTransform>();
        langRt.anchorMin = new Vector2(1, 1);
        langRt.anchorMax = new Vector2(1, 1);
        langRt.pivot = new Vector2(1, 1);
        langRt.sizeDelta = new Vector2(110, 46);
        langRt.anchoredPosition = new Vector2(-20, -18);

        Image langImg = langGo.AddComponent<Image>();
        Sprite langSp = GetSprite("UI_Btn_Active") ?? GetSprite("UI_Btn_Back");
        if (langSp != null) langImg.sprite = langSp;
        langImg.color = Color.white;

        Button langBtn = langGo.AddComponent<Button>();
        langBtn.targetGraphic = langImg;
        ColorBlock langCb = langBtn.colors;
        langCb.normalColor = Color.white;
        langCb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        langCb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        langBtn.colors = langCb;
        langBtn.onClick.AddListener(() =>
        {
            LocalizationManager.CycleLanguage();
        });

        GameObject langTextGo = CreateUIObject("Label", langGo.transform);
        StretchFull(langTextGo.GetComponent<RectTransform>());
        langTextGo.GetComponent<RectTransform>().offsetMin = new Vector2(4, 6);
        langTextGo.GetComponent<RectTransform>().offsetMax = new Vector2(-4, -2);
        Text langText = langTextGo.AddComponent<Text>();
        if (font != null) langText.font = font;
        langText.fontSize = 17;
        langText.fontStyle = FontStyle.Bold;
        langText.alignment = TextAnchor.MiddleCenter;
        langText.color = new Color(0.08f, 0.22f, 0.12f);
        langText.raycastTarget = false;
        langText.text = LocalizationManager.GetLanguageShortButtonText();
        Shadow langShadow = langTextGo.AddComponent<Shadow>();
        langShadow.effectColor = new Color(1f, 1f, 1f, 0.4f);
        langShadow.effectDistance = new Vector2(0, -1f);
        languageButtonText = langText;
        languageButtonGo = langGo;
        languageButtonGo.SetActive(curScene == "MainMenu");

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
        menuScrollRect = scrollRect;

        // Viewport
        GameObject viewportGo = CreateUIObject("Viewport", scrollGo.transform);
        StretchFull(viewportGo.GetComponent<RectTransform>());
        Image vpImg = viewportGo.AddComponent<Image>();
        vpImg.color = Color.white;
        Mask mask = viewportGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        // Content Container
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
        activeAnimatedCompletedMap = null;
        JustCompletedMapName = null;
        PlayerPrefs.DeleteKey(PrefKey_JustCompletedMap);

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
        img.color = Color.white;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        btn.colors = cb;

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        // Text
        GameObject textGo = CreateUIObject("Label", btnGo.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<RectTransform>().offsetMin = new Vector2(5, 7);
        textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-5, -3);

        Text text = textGo.AddComponent<Text>();
        if (font != null) text.font = font;
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.15f, 0.10f, 0.02f);
        text.raycastTarget = false;
        text.text = label;

        Shadow textShadow = textGo.AddComponent<Shadow>();
        textShadow.effectColor = new Color(1f, 1f, 1f, 0.35f);
        textShadow.effectDistance = new Vector2(0, -1f);

        return btnGo;
    }

    public void OpenCategory(string category)
    {
        currentCategory = category;
        PendingCategory = category;
        PlayerPrefs.SetString(PrefKey_LastCategory, category);
        PlayerPrefs.Save();
        PopulateCards();
    }

    public void BackToCategories()
    {
        currentCategory = null;
        PendingCategory = null;
        PlayerPrefs.SetString(PrefKey_LastCategory, "");
        PlayerPrefs.Save();
        PopulateCards();
    }

    private CategoryInfo GetCategoryInfo(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var c in Categories)
        {
            if (string.Equals(c.id, id, StringComparison.OrdinalIgnoreCase)) return c;
        }
        if (id == "Other")
        {
            return new CategoryInfo { id = "Other", title = "OTHER MAPS", icon = "", keyword = "" };
        }
        return null;
    }

    public static string GetMapCategory(string mapName)
    {
        if (string.IsNullOrEmpty(mapName)) return "Other";
        string clean = mapName.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

        if (clean.Contains("alleydock")) return "Alley dock";
        if (clean.Contains("angleback")) return "Angle Back";
        if (clean.Contains("facility")) return "Facility";
        if (clean.Contains("parallelparking") || clean.Contains("parallel")) return "Parallel Parking";
        if (clean.Contains("restarea")) return "Rest Area";
        if (clean.Contains("truckstop")) return "Truck Stop";

        return "Other";
    }

    public List<string> GetMapsForCategory(string category)
    {
        List<string> list = new List<string>();
        foreach (string map in availableMaps)
        {
            if (string.Equals(GetMapCategory(map), category, StringComparison.OrdinalIgnoreCase))
            {
                list.Add(map);
            }
        }
        return list;
    }

    public (int completed, int total) GetCategoryProgress(string category)
    {
        int total = 0;
        int completed = 0;
        foreach (string map in availableMaps)
        {
            if (string.Equals(GetMapCategory(map), category, StringComparison.OrdinalIgnoreCase))
            {
                total++;
                if (PlayerPrefs.GetInt("MapCompleted_" + map, 0) == 1)
                {
                    completed++;
                }
            }
        }
        return (completed, total);
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
        GridLayoutGroup glg = mapCardsContainer.GetComponent<GridLayoutGroup>();

        if (string.IsNullOrEmpty(currentCategory))
        {
            if (menuScrollRect != null)
            {
                menuScrollRect.normalizedPosition = new Vector2(0, 1);
            }

            // ===== 1. CATEGORIES VIEW =====
            if (backButtonGo != null) backButtonGo.SetActive(false);
            if (headerTitleText != null) headerTitleText.text = LocalizationManager.Get("MENU_SELECT_MODE");

            if (glg != null)
            {
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = 1;
                glg.cellSize = new Vector2(560, 115);
                glg.spacing = new Vector2(0, 14);
                glg.padding = new RectOffset(5, 5, 15, 25);
            }

            // 1. "OTHER MAPS" (Другие карты) at the very top of the menu
            var (otherCompleted, otherTotal) = GetCategoryProgress("Other");
            CategoryInfo otherCat = new CategoryInfo
            {
                id = "Other",
                title = "OTHER MAPS",
                icon = "",
                keyword = ""
            };
            CreateCategoryCard(otherCat, otherCompleted, otherTotal, font);

            // 2. Standard categories
            foreach (var cat in Categories)
            {
                var (completed, total) = GetCategoryProgress(cat.id);
                CreateCategoryCard(cat, completed, total, font);
            }
        }
        else
        {
            // ===== 2. LEVEL CARDS VIEW =====
            if (backButtonGo != null) backButtonGo.SetActive(true);
            CategoryInfo currentCatInfo = GetCategoryInfo(currentCategory);
            string iconStr = (currentCatInfo != null && !string.IsNullOrEmpty(currentCatInfo.icon)) ? (currentCatInfo.icon + " ") : "";
            string catDisplayName = GetCategoryTitle(currentCategory);
            if (headerTitleText != null) headerTitleText.text = $"{iconStr}{catDisplayName}";

            if (glg != null)
            {
                glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                glg.constraintCount = 2;
                glg.cellSize = new Vector2(275, 125);
                glg.spacing = new Vector2(25, 20);
                glg.padding = new RectOffset(5, 5, 15, 25);
            }

            List<string> categoryMaps = GetMapsForCategory(currentCategory);

            if (categoryMaps.Count == 0)
            {
                if (menuScrollRect != null)
                {
                    menuScrollRect.normalizedPosition = new Vector2(0, 1);
                }

                GameObject emptyGo = CreateUIObject("EmptyText", mapCardsContainer);
                LayoutElement le = emptyGo.AddComponent<LayoutElement>();
                le.minWidth = 550;
                le.minHeight = 150;

                Text t = emptyGo.AddComponent<Text>();
                if (font != null) t.font = font;
                t.fontSize = 22;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
                t.text = LocalizationManager.Get("MAP_NO_MAPS_IN_CAT");
                return;
            }

            RectTransform targetCardRt = null;
            int targetIndex = -1;
            string curScene = SceneManager.GetActiveScene().name;
            string targetMap = !string.IsNullOrEmpty(activeAnimatedCompletedMap)
                ? activeAnimatedCompletedMap
                : PlayerPrefs.GetString("CurrentActiveMap", PlayerPrefs.GetString("LastPlayedMap", ""));
            if (string.IsNullOrEmpty(targetMap) && curScene != "MainMenu" && curScene != "SampleScene")
            {
                targetMap = curScene;
            }

            for (int i = 0; i < categoryMaps.Count; i++)
            {
                string mapName = categoryMaps[i];
                GameObject cardGo = CreateMapCard(mapName, font);
                if (!string.IsNullOrEmpty(targetMap) && string.Equals(mapName, targetMap, StringComparison.OrdinalIgnoreCase))
                {
                    targetCardRt = cardGo.GetComponent<RectTransform>();
                    targetIndex = i;
                }
            }

            if (targetCardRt != null)
            {
                ScrollToTargetCard(targetCardRt, targetIndex, glg);
            }
            else
            {
                if (menuScrollRect != null)
                {
                    menuScrollRect.normalizedPosition = new Vector2(0, 1);
                }
            }
        }
    }

    private void CreateCategoryCard(CategoryInfo cat, int completed, int total, Font font)
    {
        bool isAllCompleted = total > 0 && completed >= total;
        string capturedCatId = cat.id;

        GameObject cardGo = CreateUIObject($"CatCard_{cat.id}", mapCardsContainer);
        RectTransform cardRt = cardGo.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(560, 115);

        Image cardImg = cardGo.AddComponent<Image>();
        Sprite sp = GetSprite(isAllCompleted ? "UI_Card_Category_Gold" : "UI_Card_Category") ??
                    GetSprite(isAllCompleted ? "UI_Btn_Special" : "UI_Btn_Normal");
        if (sp != null) cardImg.sprite = sp;
        cardImg.color = Color.white;

        Button cardBtn = cardGo.AddComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        ColorBlock cb = cardBtn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cardBtn.colors = cb;

        cardBtn.onClick.AddListener(() =>
        {
            OpenCategory(capturedCatId);
        });

        // 1. Top Row: Title (Left) with optional icon prefix
        string iconPrefix = !string.IsNullOrEmpty(cat.icon) ? (cat.icon + " ") : "";
        string displayTitle = GetCategoryTitle(cat.id);

        GameObject titleGo = CreateUIObject("Title", cardGo.transform);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(0.68f, 1);
        titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = new Vector2(25, -16);
        titleRt.sizeDelta = new Vector2(0, 28);

        Text titleText = titleGo.AddComponent<Text>();
        if (font != null) titleText.font = font;
        titleText.fontSize = 21;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = isAllCompleted ? new Color(1.0f, 0.95f, 0.60f) : Color.white;
        titleText.raycastTarget = false;
        titleText.text = $"{iconPrefix}{displayTitle}";

        Shadow titleShadow = titleGo.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0, 0, 0, 0.90f);
        titleShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // 2. Top Row: Ratio & Percentage (Right)
        GameObject ratioGo = CreateUIObject("Ratio", cardGo.transform);
        RectTransform ratioRt = ratioGo.GetComponent<RectTransform>();
        ratioRt.anchorMin = new Vector2(0.68f, 1);
        ratioRt.anchorMax = new Vector2(1, 1);
        ratioRt.pivot = new Vector2(1, 1);
        ratioRt.anchoredPosition = new Vector2(-25, -16);
        ratioRt.sizeDelta = new Vector2(0, 28);

        Text ratioText = ratioGo.AddComponent<Text>();
        if (font != null) ratioText.font = font;
        ratioText.fontSize = 16;
        ratioText.fontStyle = FontStyle.Bold;
        ratioText.alignment = TextAnchor.MiddleRight;
        ratioText.raycastTarget = false;

        if (isAllCompleted)
        {
            ratioText.color = new Color(1.0f, 0.95f, 0.60f);
            ratioText.text = "🏆 100%";
        }
        else if (total > 0 && completed > 0)
        {
            int pct = Mathf.RoundToInt((float)completed / total * 100f);
            ratioText.text = $"<color=#55ff88>{completed}/{total}</color>  <color=#9cb0c8>({pct}%)</color>";
        }
        else if (total > 0)
        {
            ratioText.text = $"<color=#9cb0c8>0/{total} (0%)</color>";
        }
        else
        {
            ratioText.text = "<color=#9cb0c8>0</color>";
        }

        Shadow ratioShadow = ratioGo.AddComponent<Shadow>();
        ratioShadow.effectColor = new Color(0, 0, 0, 0.90f);
        ratioShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // 3. Middle Row: Detailed Localized Progress Text
        GameObject summaryGo = CreateUIObject("Summary", cardGo.transform);
        RectTransform summaryRt = summaryGo.GetComponent<RectTransform>();
        summaryRt.anchorMin = new Vector2(0, 1);
        summaryRt.anchorMax = new Vector2(1, 1);
        summaryRt.pivot = new Vector2(0, 1);
        summaryRt.anchoredPosition = new Vector2(25, -46);
        summaryRt.sizeDelta = new Vector2(-50, 20);

        Text summaryText = summaryGo.AddComponent<Text>();
        if (font != null) summaryText.font = font;
        summaryText.fontSize = 14;
        summaryText.alignment = TextAnchor.MiddleLeft;
        summaryText.color = isAllCompleted ? new Color(0.95f, 0.88f, 0.70f, 0.95f) : new Color(0.68f, 0.78f, 0.90f, 0.95f);
        summaryText.raycastTarget = false;
        summaryText.text = LocalizationManager.GetProgressText(completed, total);

        Shadow summaryShadow = summaryGo.AddComponent<Shadow>();
        summaryShadow.effectColor = new Color(0, 0, 0, 0.90f);
        summaryShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // 4. Bottom Row: Progress Bar (3D Inset Cavity Slot & 3D Glossy Fill)
        GameObject barBgGo = CreateUIObject("ProgressBarBg", cardGo.transform);
        RectTransform barBgRt = barBgGo.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0, 0);
        barBgRt.anchorMax = new Vector2(1, 0);
        barBgRt.pivot = new Vector2(0.5f, 0);
        barBgRt.anchoredPosition = new Vector2(0, 16);
        barBgRt.sizeDelta = new Vector2(-50, 18);

        Image barBgImg = barBgGo.AddComponent<Image>();
        Sprite slotSp = GetSprite("UI_ProgressBar_Slot");
        if (slotSp != null)
        {
            barBgImg.sprite = slotSp;
        }
        else
        {
            barBgImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        }

        // Fill Bar
        float fillRatio = (total > 0) ? Mathf.Clamp01((float)completed / total) : 0f;
        if (fillRatio > 0f)
        {
            GameObject barFillGo = CreateUIObject("ProgressBarFill", barBgGo.transform);
            RectTransform barFillRt = barFillGo.GetComponent<RectTransform>();
            barFillRt.anchorMin = new Vector2(0, 0);
            barFillRt.anchorMax = new Vector2(fillRatio, 1f);
            barFillRt.pivot = new Vector2(0, 0.5f);
            barFillRt.offsetMin = new Vector2(2, 2);
            barFillRt.offsetMax = new Vector2(-2, -2);

            Image barFillImg = barFillGo.AddComponent<Image>();
            Sprite fillSp = GetSprite(isAllCompleted ? "UI_ProgressBar_Fill_Gold" : "UI_ProgressBar_Fill");
            if (fillSp != null)
            {
                barFillImg.sprite = fillSp;
            }
            else
            {
                barFillImg.color = isAllCompleted
                    ? new Color(1.0f, 0.85f, 0.20f, 1f)
                    : new Color(0.20f, 0.88f, 0.44f, 1f);
            }
        }
    }

    private GameObject CreateMapCard(string mapName, Font font)
    {
        string capturedName = mapName;
        bool isCompleted = PlayerPrefs.GetInt("MapCompleted_" + capturedName, 0) == 1;
        string activeMap = PlayerPrefs.GetString("CurrentActiveMap", PlayerPrefs.GetString("LastPlayedMap", ""));
        bool isCurrentPlaying = string.Equals(capturedName, activeMap, StringComparison.OrdinalIgnoreCase);
        bool isJustCompleted = !string.IsNullOrEmpty(activeAnimatedCompletedMap) &&
                               string.Equals(capturedName, activeAnimatedCompletedMap, StringComparison.OrdinalIgnoreCase);

        string displayName = FormatMapDisplayName(capturedName);

        GameObject cardGo = CreateUIObject($"Card_{capturedName}", mapCardsContainer);
        RectTransform cardRt = cardGo.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(275, 125);

        Image cardImg = cardGo.AddComponent<Image>();
        Sprite sp;
        if (isJustCompleted)
        {
            // Starts in green active appearance, will animate to gold!
            sp = GetSprite("UI_Btn_Active") ?? GetSprite("UI_Btn_Normal");
        }
        else if (isCompleted)
        {
            // ── 1. ЖЁЛТАЯ/ЗОЛОТАЯ КАРТА (ВСЕГДА, КОГДА ПРОЙДЕНА) ──
            sp = GetSprite("UI_Btn_Special");
        }
        else if (isCurrentPlaying)
        {
            // ── 2. ЗЕЛЁНАЯ КАРТА (ТОЛЬКО ОДНА: НАЧАТА И НЕ ПРОЙДЕНА) ──
            sp = GetSprite("UI_Btn_Active") ?? GetSprite("UI_Btn_Normal");
        }
        else
        {
            // ── 3. ОБЫЧНАЯ КАРТА (СТАНДАРТНЫЙ 3D ТИТАНОВЫЙ СТИЛЬ) ──
            sp = GetSprite("UI_Btn_Normal");
        }

        if (sp != null) cardImg.sprite = sp;
        cardImg.color = Color.white;

        Button cardBtn = cardGo.AddComponent<Button>();
        cardBtn.targetGraphic = cardImg;
        ColorBlock cb = cardBtn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cardBtn.colors = cb;

        cardBtn.onClick.AddListener(() =>
        {
            LoadMap(capturedName);
        });

        if (isJustCompleted)
        {
            // ── ANIMATED JUST-COMPLETED CARD SETUP ──
            Sprite goldSp = GetSprite("UI_Btn_Special");

            // 1. Pulsing border glow aura behind the card
            GameObject borderGlowGo = CreateUIObject("BorderGlow", cardGo.transform);
            borderGlowGo.transform.SetAsFirstSibling();
            StretchFull(borderGlowGo.GetComponent<RectTransform>());
            RectTransform bgRt = borderGlowGo.GetComponent<RectTransform>();
            bgRt.offsetMin = new Vector2(-6, -6);
            bgRt.offsetMax = new Vector2(6, 6);
            Image borderGlowImg = borderGlowGo.AddComponent<Image>();
            if (goldSp != null) borderGlowImg.sprite = goldSp;
            borderGlowImg.color = new Color(1.0f, 0.85f, 0.20f, 0f);
            borderGlowImg.raycastTarget = false;

            // 2. Animated outline on card body
            Outline outline = cardGo.AddComponent<Outline>();
            outline.effectColor = new Color(1.0f, 0.88f, 0.25f, 0f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            // 3. Gold transition overlay (fades in)
            GameObject goldOverlayGo = CreateUIObject("GoldOverlay", cardGo.transform);
            StretchFull(goldOverlayGo.GetComponent<RectTransform>());
            Image goldOverlayImg = goldOverlayGo.AddComponent<Image>();
            if (goldSp != null) goldOverlayImg.sprite = goldSp;
            goldOverlayImg.color = new Color(1f, 1f, 1f, 0f);
            goldOverlayImg.raycastTarget = false;

            // 4. Gold flash sparkle overlay
            GameObject flashOverlayGo = CreateUIObject("FlashOverlay", cardGo.transform);
            StretchFull(flashOverlayGo.GetComponent<RectTransform>());
            Image flashOverlayImg = flashOverlayGo.AddComponent<Image>();
            if (goldSp != null) flashOverlayImg.sprite = goldSp;
            flashOverlayImg.color = new Color(1f, 0.95f, 0.50f, 0f);
            flashOverlayImg.raycastTarget = false;

            // 5. Text Label
            GameObject textGo = CreateUIObject("Title", cardGo.transform);
            textGo.transform.SetAsLastSibling();
            StretchFull(textGo.GetComponent<RectTransform>());
            textGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 14);
            textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -4);

            Text text = textGo.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = 17;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            string inProgressLabel = LocalizationManager.Get("MAP_IN_PROGRESS");
            string completedLabel = LocalizationManager.Get("MAP_COMPLETED");

            text.color = Color.white;
            text.text = $"▶ {displayName}\n<size=13><color=#8AFFB8>{inProgressLabel}</color></size>";

            Shadow ts = textGo.AddComponent<Shadow>();
            ts.effectColor = new Color(0, 0, 0, 0.95f);
            ts.effectDistance = new Vector2(1.5f, -1.8f);

            // 6. Attach animation driver
            JustCompletedCardAnimator anim = cardGo.AddComponent<JustCompletedCardAnimator>();
            anim.Initialize(cardImg, goldOverlayImg, flashOverlayImg, borderGlowImg, outline, text, displayName, completedLabel);

            return cardGo;
        }

        // Standard Card Text Label (centered on raised 3D faceplate)
        GameObject stdTextGo = CreateUIObject("Title", cardGo.transform);
        StretchFull(stdTextGo.GetComponent<RectTransform>());
        stdTextGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 14);
        stdTextGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -4);

        Text stdText = stdTextGo.AddComponent<Text>();
        if (font != null) stdText.font = font;
        stdText.fontSize = 17;
        stdText.fontStyle = FontStyle.Bold;
        stdText.alignment = TextAnchor.MiddleCenter;
        stdText.horizontalOverflow = HorizontalWrapMode.Wrap;
        stdText.verticalOverflow = VerticalWrapMode.Truncate;
        stdText.raycastTarget = false;

        if (isCompleted)
        {
            stdText.color = new Color(1.0f, 0.95f, 0.70f);
            string completedLabel = LocalizationManager.Get("MAP_COMPLETED");
            stdText.text = $"{displayName}\n<size=13><color=#FFE066>★ {completedLabel}</color></size>";
        }
        else if (isCurrentPlaying)
        {
            stdText.color = Color.white;
            string inProgressLabel = LocalizationManager.Get("MAP_IN_PROGRESS");
            stdText.text = $"▶ {displayName}\n<size=13><color=#8AFFB8>{inProgressLabel}</color></size>";
        }
        else
        {
            stdText.color = new Color(0.88f, 0.92f, 0.98f);
            stdText.text = displayName;
        }

        Shadow stdTs = stdTextGo.AddComponent<Shadow>();
        stdTs.effectColor = new Color(0, 0, 0, 0.95f);
        stdTs.effectDistance = new Vector2(1.5f, -1.8f);

        return cardGo;
    }

    #region Auto-Scroll to Active / Completed Card
    private Coroutine scrollToCardCoroutine;

    public void ScrollToTargetCard(RectTransform targetCardRt, int targetIndex, GridLayoutGroup glg)
    {
        if (targetCardRt == null || menuScrollRect == null) return;
        if (scrollToCardCoroutine != null)
        {
            StopCoroutine(scrollToCardCoroutine);
        }
        scrollToCardCoroutine = StartCoroutine(ScrollToCardRoutine(targetCardRt, targetIndex, glg));
    }

    private System.Collections.IEnumerator ScrollToCardRoutine(RectTransform targetCardRt, int targetIndex, GridLayoutGroup glg)
    {
        // 1. Initial immediate pass
        Canvas.ForceUpdateCanvases();
        if (menuScrollRect != null && menuScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuScrollRect.content);
        }
        ApplyScrollToCard(targetCardRt, targetIndex, glg);

        // 2. Wait for end of frame (after Unity UI layout pass completes)
        yield return new WaitForEndOfFrame();

        if (menuScrollRect != null && menuScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuScrollRect.content);
        }
        ApplyScrollToCard(targetCardRt, targetIndex, glg);

        // 3. Extra safety pass on next frame
        yield return null;
        ApplyScrollToCard(targetCardRt, targetIndex, glg);
    }

    private void ApplyScrollToCard(RectTransform targetCardRt, int targetIndex, GridLayoutGroup glg)
    {
        if (targetCardRt == null || menuScrollRect == null || menuScrollRect.content == null || menuScrollRect.viewport == null)
            return;

        RectTransform contentRt = menuScrollRect.content;
        RectTransform viewportRt = menuScrollRect.viewport;

        float contentHeight = contentRt.rect.height;
        float viewportHeight = viewportRt.rect.height;

        // Fallback estimate if ContentSizeFitter has not yet calculated rect
        if (contentHeight <= viewportHeight && glg != null && targetIndex >= 0)
        {
            int totalRows = (mapCardsContainer.childCount + glg.constraintCount - 1) / Mathf.Max(1, glg.constraintCount);
            float estimatedHeight = glg.padding.top + glg.padding.bottom + totalRows * glg.cellSize.y + Mathf.Max(0, totalRows - 1) * glg.spacing.y;
            if (estimatedHeight > contentHeight)
            {
                contentHeight = estimatedHeight;
            }
        }

        if (contentHeight <= viewportHeight)
        {
            menuScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        float scrollableDistance = contentHeight - viewportHeight;
        if (scrollableDistance <= 0.001f)
        {
            menuScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        // Calculate card center Y from top of content
        float cardCenterY = -targetCardRt.localPosition.y;
        if (cardCenterY <= 1f && glg != null && targetIndex >= 0)
        {
            int row = targetIndex / Mathf.Max(1, glg.constraintCount);
            cardCenterY = glg.padding.top + row * (glg.cellSize.y + glg.spacing.y) + glg.cellSize.y * 0.5f;
        }

        // Target scroll to center the card inside the viewport
        float targetScrollY = cardCenterY - (viewportHeight * 0.5f);
        targetScrollY = Mathf.Clamp(targetScrollY, 0f, scrollableDistance);

        float normPos = 1.0f - (targetScrollY / scrollableDistance);
        menuScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normPos);
    }
    #endregion

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

    /// <summary>
    /// Animates the freshly completed map card:
    /// 1. Plays a smooth color transition from green/active to standard gold completed style with flash & badge pop.
    /// 2. Keeps an active pulsating golden border glow around the card for the rest of the menu visit.
    /// </summary>
    public class JustCompletedCardAnimator : MonoBehaviour
    {
        private Image baseImg;
        private Image goldOverlayImg;
        private Image flashOverlayImg;
        private Image borderGlowImg;
        private Outline outline;
        private Text titleText;
        private string displayName;
        private string completedLabel;

        public void Initialize(
            Image baseCardImg,
            Image goldOverlay,
            Image flashOverlay,
            Image borderGlow,
            Outline cardOutline,
            Text textComponent,
            string mapDisplayName,
            string completedText)
        {
            baseImg = baseCardImg;
            goldOverlayImg = goldOverlay;
            flashOverlayImg = flashOverlay;
            borderGlowImg = borderGlow;
            outline = cardOutline;
            titleText = textComponent;
            displayName = mapDisplayName;
            completedLabel = completedText;

            StartCoroutine(AnimateCompletionRoutine());
        }

        private System.Collections.IEnumerator AnimateCompletionRoutine()
        {
            // Brief wait so scroll rect centers and player eye focuses on the card
            yield return new WaitForSecondsRealtime(0.28f);

            float duration = 0.85f;
            float elapsed = 0f;
            bool textSwitched = false;
            Vector3 origScale = Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 1. Smooth gold transition overlay fade in
                if (goldOverlayImg != null)
                {
                    goldOverlayImg.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t));
                }

                // 2. Bright gold flash at peak (~0.35s)
                if (flashOverlayImg != null)
                {
                    float flash;
                    if (t < 0.40f)
                    {
                        flash = Mathf.Lerp(0f, 0.85f, t / 0.40f);
                    }
                    else
                    {
                        flash = Mathf.Lerp(0.85f, 0f, (t - 0.40f) / 0.60f);
                    }
                    flashOverlayImg.color = new Color(1f, 0.95f, 0.50f, flash);
                }

                // 3. Punch scale at peak
                float scaleMultiplier;
                if (t < 0.40f)
                {
                    scaleMultiplier = Mathf.Lerp(1.0f, 1.07f, t / 0.40f);
                }
                else
                {
                    scaleMultiplier = Mathf.Lerp(1.07f, 1.0f, (t - 0.40f) / 0.60f);
                }
                transform.localScale = origScale * scaleMultiplier;

                // 4. Switch text to completed ★ badge at peak
                if (t >= 0.35f && !textSwitched && titleText != null)
                {
                    textSwitched = true;
                    titleText.color = new Color(1.0f, 0.95f, 0.70f);
                    titleText.text = $"{displayName}\n<size=13><color=#FFE066>★ {completedLabel}</color></size>";
                }

                yield return null;
            }

            transform.localScale = origScale;
            if (flashOverlayImg != null) flashOverlayImg.color = new Color(1f, 0.95f, 0.50f, 0f);
            if (baseImg != null && goldOverlayImg != null && goldOverlayImg.sprite != null)
            {
                baseImg.sprite = goldOverlayImg.sprite;
            }
            if (goldOverlayImg != null)
            {
                goldOverlayImg.color = Color.white;
            }
        }

        private void Update()
        {
            // Continuous breathing border glow animation while standing in menu
            float wave = (Mathf.Sin(Time.unscaledTime * 4.0f) + 1f) * 0.5f; // 0..1

            if (borderGlowImg != null)
            {
                borderGlowImg.color = new Color(1.0f, 0.85f, 0.20f, Mathf.Lerp(0.35f, 0.90f, wave));
            }
            if (outline != null)
            {
                float dist = Mathf.Lerp(2.0f, 4.5f, wave);
                outline.effectDistance = new Vector2(dist, -dist);
                outline.effectColor = new Color(1.0f, 0.92f, 0.35f, Mathf.Lerp(0.50f, 0.95f, wave));
            }
        }
    }
}
