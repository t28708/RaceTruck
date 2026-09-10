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
/// Start & Level Selection Menu matching the Arcade Tech SVG specification.
/// Features:
/// 1. Main Modes Screen:
///    - CDL PRACTICE MODE (special glowing amber button)
///    - ALLEY DOCK, ANGLE BACK, OFF SET BACK, PARALLEL PARKING, STRAIGHT BACK
/// 2. CDL Practice Mode Submenu:
///    - Dynamic 2-column scrollable grid of all custom maps (present and future).
///    - Identical arcade tech styling.
///    - Massive golden/amber BACK button returning to the main mode selector.
/// </summary>
public class MapSelectMenu : MonoBehaviour
{
    public static MapSelectMenu Instance { get; private set; }

    [Header("State")]
    [SerializeField] private bool isMenuOpen = false;
    [SerializeField] private bool isInSubmenu = false;

    private List<string> availableMaps = new List<string>();

    private GameObject menuCanvasGo;
    private GameObject mainModesContainerGo;
    private GameObject submenuContainerGo;
    private Transform submenuCardsContainer;
    private Text headerTitleText;
    private GameObject topButtonGo;

    private Dictionary<string, Sprite> cachedSprites = new Dictionary<string, Sprite>();

    public bool IsMenuOpen => isMenuOpen;
    public bool IsInSubmenu => isInSubmenu;

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
            if (isInSubmenu)
            {
                ShowMainModesView();
            }
            else
            {
                ToggleMenu();
            }
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

        if (menuCanvasGo != null)
        {
            menuCanvasGo.SetActive(isMenuOpen);
        }

        if (topButtonGo != null)
        {
            topButtonGo.SetActive(!isMenuOpen && currentScene != "MainMenu");
        }

        if (isMenuOpen)
        {
            RefreshMapsList();
            ShowMainModesView();
        }
    }

    public void ShowMainModesView()
    {
        isInSubmenu = false;
        if (headerTitleText != null) headerTitleText.text = "SELECT GAME MODE";
        if (mainModesContainerGo != null) mainModesContainerGo.SetActive(true);
        if (submenuContainerGo != null) submenuContainerGo.SetActive(false);
    }

    public void ShowCdlPracticeSubmenu()
    {
        isInSubmenu = true;
        if (headerTitleText != null) headerTitleText.text = "CDL PRACTICE MODE";
        if (mainModesContainerGo != null) mainModesContainerGo.SetActive(false);
        if (submenuContainerGo != null) submenuContainerGo.SetActive(true);

        PopulateSubmenuCards();
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

    #region Maneuver Specific Handlers
    public void LaunchManeuver(string maneuverType)
    {
        RefreshMapsList();
        string targetMap = FindBestMapForManeuver(maneuverType);
        if (!string.IsNullOrEmpty(targetMap))
        {
            LoadMap(targetMap);
        }
        else
        {
            Debug.LogWarning($"[MapSelectMenu] No specific map found for {maneuverType}, opening CDL Practice mode.");
            ShowCdlPracticeSubmenu();
        }
    }

    private string FindBestMapForManeuver(string maneuver)
    {
        maneuver = maneuver.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
        
        // Priority map lookup table
        string[] candidates = null;
        if (maneuver.Contains("alley"))
        {
            candidates = new string[] { "Map_12_truck_stop_standart", "Map_12_truck_stop_small", "Map_12_truck_stop_left_V3", "Map_1", "Map_3" };
        }
        else if (maneuver.Contains("angle"))
        {
            candidates = new string[] { "Map_11_angle_back-standart", "Map_11_angle_back-smaller" };
        }
        else if (maneuver.Contains("off_set") || maneuver.Contains("offset"))
        {
            candidates = new string[] { "Map_4-rest-area", "Map_8-kroger", "Map_4-rest-area-small", "Map_8-kroger-small", "Map_5" };
        }
        else if (maneuver.Contains("parallel"))
        {
            candidates = new string[] { "Map_10_parallel_parking", "Map_10_parallel_parking-2" };
        }
        else if (maneuver.Contains("straight"))
        {
            candidates = new string[] { "Map_6-standart-parking-small", "Map_7-standart-parking-smaller", "Map_2" };
        }

        if (candidates != null)
        {
            foreach (string cand in candidates)
            {
                if (availableMaps.Contains(cand)) return cand;
            }
        }

        // Fuzzy search in available maps
        foreach (string m in availableMaps)
        {
            string lower = m.ToLowerInvariant();
            if (maneuver.Contains("alley") && (lower.Contains("alley") || lower.Contains("truck_stop"))) return m;
            if (maneuver.Contains("angle") && lower.Contains("angle")) return m;
            if (maneuver.Contains("off") && (lower.Contains("off") || lower.Contains("rest") || lower.Contains("kroger"))) return m;
            if (maneuver.Contains("parallel") && lower.Contains("parallel")) return m;
            if (maneuver.Contains("straight") && (lower.Contains("straight") || lower.Contains("standart"))) return m;
        }

        return availableMaps.Count > 0 ? availableMaps[0] : null;
    }
    #endregion

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
    #endregion

    #region UI Construction
    private void BuildUI()
    {
        Font font = GetAppFont();

        // 1. Root Canvas (720x1280 base)
        menuCanvasGo = new GameObject("MapSelectMenuCanvas");
        menuCanvasGo.transform.SetParent(transform, false);

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
        logoRt.pivot = new Vector2(0, 0.5f);
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

        // 4. Central Window Frame (640 x 1000)
        GameObject centerWindowGo = CreateUIObject("CenterWindowPanel", menuCanvasGo.transform);
        RectTransform centerRt = centerWindowGo.GetComponent<RectTransform>();
        centerRt.anchorMin = new Vector2(0.5f, 0.5f);
        centerRt.anchorMax = new Vector2(0.5f, 0.5f);
        centerRt.pivot = new Vector2(0.5f, 0.5f);
        centerRt.sizeDelta = new Vector2(640, 1000);
        centerRt.anchoredPosition = new Vector2(0, -35);

        // Frame Graphic
        Image centerImg = centerWindowGo.AddComponent<Image>();
        Sprite frameSp = GetSprite("UI_Panel_Frame");
        if (frameSp != null) centerImg.sprite = frameSp;
        else centerImg.color = new Color(0.14f, 0.17f, 0.21f, 1.0f);

        // Header Title Text ("SELECT GAME MODE" / "CDL PRACTICE MODE")
        GameObject titleGo = CreateUIObject("HeaderTitle", centerWindowGo.transform);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.pivot = new Vector2(0.5f, 1);
        titleRt.sizeDelta = new Vector2(0, 80);
        titleRt.anchoredPosition = new Vector2(0, 0);

        headerTitleText = titleGo.AddComponent<Text>();
        if (font != null) headerTitleText.font = font;
        headerTitleText.fontSize = 28;
        headerTitleText.fontStyle = FontStyle.Bold;
        headerTitleText.alignment = TextAnchor.MiddleCenter;
        headerTitleText.color = Color.white;
        headerTitleText.text = "SELECT GAME MODE";

        // Add subtle shadow component
        Shadow textShadow = titleGo.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0, 0, 0, 0.9f);
        textShadow.effectDistance = new Vector2(2, -2);

        // -------------------------------------------------------------
        // VIEW 1: MainModesContainer (6 Mode Buttons + Checkered Line)
        // -------------------------------------------------------------
        mainModesContainerGo = CreateUIObject("MainModesContainer", centerWindowGo.transform);
        RectTransform mmRt = mainModesContainerGo.GetComponent<RectTransform>();
        mmRt.anchorMin = Vector2.zero;
        mmRt.anchorMax = Vector2.one;
        mmRt.sizeDelta = Vector2.zero;
        mmRt.anchoredPosition = Vector2.zero;

        // Button Positions: 2 Columns (X: 165, 475), 3 Rows (Y: -175, -325, -475)
        // Button 1: CDL PRACTICE MODE (Special Glow)
        CreateModeButton(mainModesContainerGo.transform, "CDL PRACTICE\nMODE", new Vector2(165, -175), true, () =>
        {
            ShowCdlPracticeSubmenu();
        });

        // Button 2: ALLEY DOCK
        CreateModeButton(mainModesContainerGo.transform, "ALLEY DOCK", new Vector2(475, -175), false, () =>
        {
            LaunchManeuver("alley");
        });

        // Button 3: ANGLE BACK
        CreateModeButton(mainModesContainerGo.transform, "ANGLE BACK", new Vector2(165, -325), false, () =>
        {
            LaunchManeuver("angle");
        });

        // Button 4: OFF SET BACK
        CreateModeButton(mainModesContainerGo.transform, "OFF SET BACK", new Vector2(475, -325), false, () =>
        {
            LaunchManeuver("offset");
        });

        // Button 5: PARALLEL PARKING
        CreateModeButton(mainModesContainerGo.transform, "PARALLEL\nPARKING", new Vector2(165, -475), false, () =>
        {
            LaunchManeuver("parallel");
        });

        // Button 6: STRAIGHT BACK
        CreateModeButton(mainModesContainerGo.transform, "STRAIGHT BACK", new Vector2(475, -475), false, () =>
        {
            LaunchManeuver("straight");
        });

        // Checkered Divider
        GameObject divGo = CreateUIObject("CheckeredDivider", mainModesContainerGo.transform);
        RectTransform divRt = divGo.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.5f, 1);
        divRt.anchorMax = new Vector2(0.5f, 1);
        divRt.pivot = new Vector2(0.5f, 1);
        divRt.sizeDelta = new Vector2(580, 18);
        divRt.anchoredPosition = new Vector2(0, -640);
        Image divImg = divGo.AddComponent<Image>();
        Sprite divSp = GetSprite("UI_Checkered_Divider");
        if (divSp != null) divImg.sprite = divSp;
        else divImg.color = new Color(0.2f, 0.25f, 0.32f, 0.8f);

        // Resume / In-Game Return Button (Shown only when in gameplay scene)
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            CreateBackButton(mainModesContainerGo.transform, "RESUME GAME", new Vector2(320, -850), () =>
            {
                CloseMenu();
            });
        }

        // -------------------------------------------------------------
        // VIEW 2: SubmenuContainer (CDL Practice Mode Maps + BACK Button)
        // -------------------------------------------------------------
        submenuContainerGo = CreateUIObject("SubmenuContainer", centerWindowGo.transform);
        RectTransform smRt = submenuContainerGo.GetComponent<RectTransform>();
        smRt.anchorMin = Vector2.zero;
        smRt.anchorMax = Vector2.one;
        smRt.sizeDelta = Vector2.zero;
        smRt.anchoredPosition = Vector2.zero;
        submenuContainerGo.SetActive(false);

        // Scroll View for Maps (Middle Area: top -100 to bottom -150)
        GameObject scrollGo = CreateUIObject("MapsScrollView", submenuContainerGo.transform);
        RectTransform scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0, 0);
        scrollRt.anchorMax = new Vector2(1, 1);
        scrollRt.offsetMin = new Vector2(25, 145);
        scrollRt.offsetMax = new Vector2(-25, -95);

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 25f;

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
        glg.cellSize = new Vector2(275, 120);
        glg.spacing = new Vector2(30, 20);
        glg.padding = new RectOffset(5, 5, 15, 25);
        glg.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
        scrollRect.content = contentRt;
        submenuCardsContainer = contentGo.transform;

        // Golden/Amber BACK Button at bottom of CDL Submenu
        CreateBackButton(submenuContainerGo.transform, "BACK", new Vector2(320, -920), () =>
        {
            ShowMainModesView();
        });
    }

    private GameObject CreateModeButton(Transform parent, string label, Vector2 centerPos, bool isSpecial, UnityEngine.Events.UnityAction onClick)
    {
        Font font = GetAppFont();

        GameObject btnGo = CreateUIObject("ModeBtn_" + label.Replace("\n", " "), parent);
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(275, 128);
        rt.anchoredPosition = centerPos;

        Image img = btnGo.AddComponent<Image>();
        Sprite sp = GetSprite(isSpecial ? "UI_Btn_Special" : "UI_Btn_Normal");
        if (sp != null) img.sprite = sp;
        else img.color = isSpecial ? new Color(0.24f, 0.18f, 0.12f) : new Color(0.18f, 0.22f, 0.28f);

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = cb;

        if (onClick != null) btn.onClick.AddListener(onClick);

        // Text
        GameObject textGo = CreateUIObject("Label", btnGo.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 8);
        textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -4);

        Text text = textGo.AddComponent<Text>();
        if (font != null) text.font = font;
        text.fontSize = label.Contains("\n") ? 20 : 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = isSpecial ? Color.white : new Color(0.95f, 0.96f, 0.98f);
        text.text = label;

        Shadow ts = textGo.AddComponent<Shadow>();
        ts.effectColor = new Color(0, 0, 0, 0.85f);
        ts.effectDistance = new Vector2(1.5f, -1.5f);

        return btnGo;
    }

    private GameObject CreateBackButton(Transform parent, string label, Vector2 centerPos, UnityEngine.Events.UnityAction onClick)
    {
        Font font = GetAppFont();

        GameObject btnGo = CreateUIObject("Btn_" + label, parent);
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240, 105);
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

        if (onClick != null) btn.onClick.AddListener(onClick);

        // Text
        GameObject textGo = CreateUIObject("Label", btnGo.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<RectTransform>().offsetMin = new Vector2(5, 10);
        textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-5, -5);

        Text text = textGo.AddComponent<Text>();
        if (font != null) text.font = font;
        text.fontSize = 32;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.08f, 0.10f, 0.15f); // Dark bold text on amber
        text.text = label;

        return btnGo;
    }

    private void PopulateSubmenuCards()
    {
        if (submenuCardsContainer == null) return;

        // Clear previous cards
        for (int i = submenuCardsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(submenuCardsContainer.GetChild(i).gameObject);
        }

        Font font = GetAppFont();

        if (availableMaps.Count == 0)
        {
            GameObject emptyGo = CreateUIObject("EmptyText", submenuCardsContainer);
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
            string displayName = FormatMapDisplayName(capturedName);

            GameObject cardGo = CreateUIObject($"Card_{capturedName}", submenuCardsContainer);
            RectTransform cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(275, 120);

            Image cardImg = cardGo.AddComponent<Image>();
            Sprite sp = GetSprite("UI_Btn_Normal");
            if (sp != null) cardImg.sprite = sp;
            else cardImg.color = new Color(0.18f, 0.22f, 0.28f);

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
            text.fontSize = 20;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = displayName;

            Shadow ts = textGo.AddComponent<Shadow>();
            ts.effectColor = new Color(0, 0, 0, 0.85f);
            ts.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }

    private string FormatMapDisplayName(string rawName)
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

    private static int NaturalCompare(string a, string b)
    {
        int na = ExtractNumber(a);
        int nb = ExtractNumber(b);
        if (na != -1 && nb != -1 && na != nb) return na.CompareTo(nb);
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractNumber(string s)
    {
        string numStr = "";
        foreach (char c in s)
        {
            if (char.IsDigit(c)) numStr += c;
            else if (numStr.Length > 0) break;
        }
        if (int.TryParse(numStr, out int val)) return val;
        return -1;
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
