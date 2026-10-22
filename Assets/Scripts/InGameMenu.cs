using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// In-Game Pause Menu & Top-Bar Checkpoint HUD Controller.
/// Implements the exact Industrial Trucker Pause Modal design:
/// - Trapezoid "PAUSE / MAP NAME" header with diamond-plate corner accents, 4 corner rivets, and golden-amber border
/// - 2-column dark-slate settings tiles with pill toggle switches (SOUND, AUTO-CENTER) and custom vector icons (CONTROLS, PEDALS, LANGUAGE)
/// - Metallic horizontal divider bar
/// - Side-by-side "RESTART MAP" (bronze/amber) & "MAIN MENU" (crimson/terracotta) buttons + full-width neon-emerald "▶ RESUME" button
/// </summary>
public class InGameMenu : MonoBehaviour
{
    public static InGameMenu Instance { get; private set; }

    private GameObject menuModalGo;
    private GameObject topMenuBtnGo;
    private Text topMenuBtnText;

    // Top-Bar Save & Load HUD Buttons
    private GameObject topSaveBtnGo;
    private Button topSaveBtn;
    private Image topSaveBtnBg;
    private Image topSaveFloppyIcon;
    private Text topSaveBtnText;

    private GameObject topLoadBtnGo;
    private Button topLoadBtn;
    private Image topLoadBtnBg;
    private Text topLoadBtnText;

    // Restore Saved Position Banner & Toast
    private GameObject restoreBannerGo;
    private Text restoreBannerText;
    private GameObject toastBannerGo;
    private Text toastBannerText;

    // Pause Modal UI References
    private Text pauseTitleText;
    private Text levelSubTitleText;

    // Tile 1: Sound
    private Text soundTitleText;
    private RectTransform soundThumbRt;
    private Image soundThumbImg;
    private Text soundOnText;
    private Text soundOffText;

    // Tile 2: Controls
    private Text controlsTitleText;
    private Text controlsValueText;

    // Tile 3: Auto-Center
    private Text autoCenterTitleText;
    private RectTransform autoCenterThumbRt;
    private Image autoCenterThumbImg;
    private Text autoCenterOnText;
    private Text autoCenterOffText;

    // Tile 4: Pedals
    private Text pedalsTitleText;
    private Text pedalsValueText;

    // Tile 5: Language
    private Text languageTitleText;
    private Text languageValueText;

    // Bottom Action Buttons
    private Text restartBtnText;
    private Text mainMenuBtnText;
    private Text resumeBtnText;

    private Coroutine saveFlashCoroutine;
    private Coroutine toastCoroutine;
    private Coroutine restoreBannerAutoHideCoroutine;

    // Cached Procedural Sprites
    private static Sprite cachedFloppySprite;
    private static Sprite cachedPausePanelSprite;
    private static Sprite cachedRoundedBoxSprite;
    private static Sprite cachedPillSprite;
    private static Sprite cachedDividerSprite;
    private static Sprite cachedSpeakerSprite;
    private static Sprite cachedWheelIconSprite;
    private static Sprite cachedReturnArrowSprite;
    private static Sprite cachedPedalsIconSprite;
    private static Sprite cachedGlobeIconSprite;
    private static Sprite cachedReloadIconSprite;
    private static Sprite cachedHomeIconSprite;
    private static Sprite cachedPlayIconSprite;

    private static readonly Color SaveBtnDefaultBg = new Color(0.14f, 0.36f, 0.62f, 0.95f);
    private static readonly Color SaveBtnFlashGreenBg = new Color(0.12f, 0.82f, 0.36f, 1.0f);
    private static readonly Color LoadBtnActiveBg = new Color(0.16f, 0.54f, 0.42f, 0.95f);
    private static readonly Color LoadBtnDisabledBg = new Color(0.18f, 0.20f, 0.25f, 0.45f);

    private static readonly Color GoldHeaderColor = new Color(1.00f, 0.77f, 0.28f, 1.0f);
    private static readonly Color GoldValueColor = new Color(0.95f, 0.72f, 0.28f, 1.0f);
    private static readonly Color ToggleGreenOn = new Color(0.20f, 0.78f, 0.38f, 1.0f);
    private static readonly Color ToggleSlateOff = new Color(0.24f, 0.27f, 0.32f, 1.0f);
    private static readonly Color IconSilverColor = new Color(0.74f, 0.78f, 0.83f, 1.0f);

    private bool isMenuOpen = false;
    public bool IsMenuOpen => isMenuOpen;

    private void Awake()
    {
        Instance = this;
        ApplySoundSetting();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += UpdateAllTexts;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdateAllTexts;
    }

    private void Start()
    {
        EnsureSaveLoadSystemExists();
        EnsureUI();

        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene != "MainMenu")
        {
            bool hasCheckpoint = SaveLoadTruckSystem.HasCheckpoint(currentScene);
            SetLoadButtonState(hasCheckpoint);

            if (hasCheckpoint)
            {
                ShowRestoreSavedPositionBanner();
            }
        }
    }

    private void EnsureSaveLoadSystemExists()
    {
        if (SaveLoadTruckSystem.Instance == null && FindFirstObjectByType<SaveLoadTruckSystem>() == null)
        {
            if (TruckController.Instance != null)
            {
                TruckController.Instance.gameObject.AddComponent<SaveLoadTruckSystem>();
            }
            else
            {
                gameObject.AddComponent<SaveLoadTruckSystem>();
            }
        }
    }

    private void Update()
    {
        bool toggle = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.mKey.wasPressedThisFrame)
            {
                toggle = true;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
        {
            toggle = true;
        }
#endif
        if (toggle)
        {
            if (MapSelectMenu.Instance != null && MapSelectMenu.Instance.IsMenuOpen)
            {
                MapSelectMenu.Instance.CloseMenu();
            }
            else
            {
                ToggleMenu();
            }
        }
    }

    // =========================================================================
    // CHECKPOINT SAVE & LOAD UI HOOKS
    // =========================================================================

    public void OnSaveClicked()
    {
        EnsureSaveLoadSystemExists();
        if (SaveLoadTruckSystem.Instance == null) return;

        bool success = SaveLoadTruckSystem.Instance.SaveCheckpoint();
        if (success)
        {
            SetLoadButtonState(true);
            HideRestoreSavedPositionBanner();

            if (saveFlashCoroutine != null)
            {
                StopCoroutine(saveFlashCoroutine);
            }
            saveFlashCoroutine = StartCoroutine(FlashSaveIconGreenRoutine());

            ShowToastNotification(LocalizationManager.Get("CHECKPOINT_SAVED"), new Color(0.12f, 0.78f, 0.36f, 0.96f));
        }
    }

    public void OnLoadClicked()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (!SaveLoadTruckSystem.HasCheckpoint(currentScene))
        {
            SetLoadButtonState(false);
            return;
        }

        EnsureSaveLoadSystemExists();
        if (SaveLoadTruckSystem.Instance == null) return;

        if (isMenuOpen)
        {
            SetMenuOpen(false);
        }

        HideRestoreSavedPositionBanner();

        bool started = SaveLoadTruckSystem.Instance.LoadCheckpoint();
        if (started)
        {
            ShowToastNotification(LocalizationManager.Get("CHECKPOINT_LOADED"), new Color(0.16f, 0.58f, 0.88f, 0.96f));
        }
    }

    public void SetLoadButtonState(bool interactable)
    {
        if (topLoadBtn != null)
        {
            topLoadBtn.interactable = interactable;
        }
        if (topLoadBtnBg != null)
        {
            topLoadBtnBg.color = interactable ? LoadBtnActiveBg : LoadBtnDisabledBg;
        }
        if (topLoadBtnText != null)
        {
            topLoadBtnText.color = interactable ? Color.white : new Color(0.65f, 0.68f, 0.72f, 0.45f);
        }
    }

    private IEnumerator FlashSaveIconGreenRoutine()
    {
        if (topSaveBtnBg != null) topSaveBtnBg.color = SaveBtnFlashGreenBg;
        if (topSaveFloppyIcon != null) topSaveFloppyIcon.color = new Color(0.55f, 1.0f, 0.65f, 1.0f);

        const float holdDuration = 0.35f;
        const float fadeDuration = 0.45f;

        yield return new WaitForSecondsRealtime(holdDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            Color bgCol = Color.Lerp(SaveBtnFlashGreenBg, SaveBtnDefaultBg, t);
            Color iconCol = Color.Lerp(new Color(0.55f, 1.0f, 0.65f, 1.0f), Color.white, t);

            if (topSaveBtnBg != null) topSaveBtnBg.color = bgCol;
            if (topSaveFloppyIcon != null) topSaveFloppyIcon.color = iconCol;

            yield return null;
        }

        if (topSaveBtnBg != null) topSaveBtnBg.color = SaveBtnDefaultBg;
        if (topSaveFloppyIcon != null) topSaveFloppyIcon.color = Color.white;
        saveFlashCoroutine = null;
    }

    public void ShowRestoreSavedPositionBanner()
    {
        if (restoreBannerGo == null) return;
        restoreBannerGo.SetActive(true);
        if (restoreBannerText != null)
        {
            restoreBannerText.text = LocalizationManager.Get("BANNER_RESTORE_PROMPT");
        }

        if (restoreBannerAutoHideCoroutine != null)
        {
            StopCoroutine(restoreBannerAutoHideCoroutine);
        }
        restoreBannerAutoHideCoroutine = StartCoroutine(AutoHideRestoreBannerRoutine(8.0f));
    }

    public void HideRestoreSavedPositionBanner()
    {
        if (restoreBannerAutoHideCoroutine != null)
        {
            StopCoroutine(restoreBannerAutoHideCoroutine);
            restoreBannerAutoHideCoroutine = null;
        }
        if (restoreBannerGo != null)
        {
            restoreBannerGo.SetActive(false);
        }
    }

    private IEnumerator AutoHideRestoreBannerRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        if (restoreBannerGo != null)
        {
            restoreBannerGo.SetActive(false);
        }
        restoreBannerAutoHideCoroutine = null;
    }

    private void ShowToastNotification(string message, Color bgColor)
    {
        if (toastBannerGo == null || toastBannerText == null) return;

        if (toastCoroutine != null)
        {
            StopCoroutine(toastCoroutine);
        }
        toastCoroutine = StartCoroutine(ToastRoutine(message, bgColor));
    }

    private IEnumerator ToastRoutine(string message, Color bgColor)
    {
        toastBannerGo.SetActive(true);
        toastBannerText.text = message;
        Image img = toastBannerGo.GetComponent<Image>();
        if (img != null) img.color = bgColor;

        yield return new WaitForSecondsRealtime(1.6f);

        toastBannerGo.SetActive(false);
        toastCoroutine = null;
    }

    // =========================================================================
    // MENU NAVIGATION & SETTINGS
    // =========================================================================

    public void ToggleMenu()
    {
        SetMenuOpen(!isMenuOpen);
    }

    public void OpenMenu() => SetMenuOpen(true);
    public void CloseMenu() => SetMenuOpen(false);

    public void SetMenuOpen(bool open)
    {
        isMenuOpen = open;

        if (isMenuOpen)
        {
            MapSelectMenu.LockOrientationToPortrait();
        }
        else
        {
            if (MapSelectMenu.Instance == null || !MapSelectMenu.Instance.IsMenuOpen)
            {
                MapSelectMenu.UnlockOrientation();
            }
        }

        if (menuModalGo != null)
        {
            menuModalGo.SetActive(isMenuOpen);
            if (isMenuOpen)
            {
                string currentScene = SceneManager.GetActiveScene().name;
                SetLoadButtonState(SaveLoadTruckSystem.HasCheckpoint(currentScene));
                UpdateAllTexts();
            }
        }
    }

    public void OnToggleLanguage()
    {
        LocalizationManager.CycleLanguage();
        UpdateAllTexts();
    }

    public void OnToggleControlMode()
    {
        SteeringWheelUI.CycleControlType();
        UpdateAllTexts();
    }

    public void OnTogglePedalSide()
    {
        SteeringWheelUI.TogglePedalSide();
        UpdateAllTexts();
    }

    public void OnToggleAutoCenter()
    {
        SteeringWheelUI.ToggleAutoCenter();
        UpdateAllTexts();
    }

    public const string PrefKey_Sound = "GameSoundEnabled";

    public static bool SoundEnabled
    {
        get => PlayerPrefs.GetInt(PrefKey_Sound, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey_Sound, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplySoundSetting();
        }
    }

    public static void ApplySoundSetting()
    {
        bool enabled = SoundEnabled;
        AudioListener.volume = enabled ? 1.0f : 0.0f;
    }

    public static bool ToggleSound()
    {
        bool newVal = !SoundEnabled;
        SoundEnabled = newVal;
        return newVal;
    }

    public void OnToggleSound()
    {
        ToggleSound();
        UpdateAllTexts();
    }

    private static string GetCleanControlModeName(SteeringControlType type)
    {
        switch (type)
        {
            case SteeringControlType.Wheel:   return LocalizationManager.Get("VAL_CTRL_WHEEL");
            case SteeringControlType.Buttons: return LocalizationManager.Get("VAL_CTRL_ARROWS");
            case SteeringControlType.Slider:  return LocalizationManager.Get("VAL_CTRL_SLIDER");
            default:                          return LocalizationManager.Get("VAL_CTRL_ARROWS");
        }
    }

    public void UpdateAllTexts()
    {
        // Top HUD
        if (topMenuBtnText != null) topMenuBtnText.text = LocalizationManager.Get("BTN_MENU");
        if (topSaveBtnText != null) topSaveBtnText.text = LocalizationManager.Get("BTN_SAVE_SHORT");
        if (topLoadBtnText != null) topLoadBtnText.text = LocalizationManager.Get("BTN_LOAD_SHORT");
        if (restoreBannerText != null) restoreBannerText.text = LocalizationManager.Get("BANNER_RESTORE_PROMPT");

        // Header
        if (pauseTitleText != null) pauseTitleText.text = LocalizationManager.Get("PAUSE_TITLE");
        if (levelSubTitleText != null) levelSubTitleText.text = GetCurrentMapDisplayName();

        string onStr = LocalizationManager.Get("VAL_ON_SHORT");
        string offStr = LocalizationManager.Get("VAL_OFF_SHORT");

        // 1. Sound Tile
        bool soundOn = SoundEnabled;
        if (soundTitleText != null) soundTitleText.text = LocalizationManager.Get("LBL_SOUND");
        if (soundOnText != null)
        {
            soundOnText.text = onStr;
            soundOnText.color = soundOn ? Color.white : new Color(0.75f, 0.78f, 0.82f, 0.75f);
        }
        if (soundOffText != null)
        {
            soundOffText.text = offStr;
            soundOffText.color = !soundOn ? Color.white : new Color(0.75f, 0.78f, 0.82f, 0.75f);
        }
        if (soundThumbRt != null)
        {
            soundThumbRt.anchoredPosition = soundOn ? new Vector2(-35f, 0f) : new Vector2(35f, 0f);
        }
        if (soundThumbImg != null)
        {
            soundThumbImg.color = soundOn ? ToggleGreenOn : ToggleSlateOff;
        }

        // 2. Controls Tile
        if (controlsTitleText != null) controlsTitleText.text = LocalizationManager.Get("LBL_CONTROLS");
        if (controlsValueText != null) controlsValueText.text = GetCleanControlModeName(SteeringWheelUI.CurrentControlType);

        // 3. Auto-Center Tile
        bool autoOn = SteeringWheelUI.AutoCenterEnabled;
        if (autoCenterTitleText != null) autoCenterTitleText.text = LocalizationManager.Get("LBL_AUTOCENTER");
        if (autoCenterOnText != null)
        {
            autoCenterOnText.text = onStr;
            autoCenterOnText.color = autoOn ? Color.white : new Color(0.75f, 0.78f, 0.82f, 0.75f);
        }
        if (autoCenterOffText != null)
        {
            autoCenterOffText.text = offStr;
            autoCenterOffText.color = !autoOn ? Color.white : new Color(0.75f, 0.78f, 0.82f, 0.75f);
        }
        if (autoCenterThumbRt != null)
        {
            autoCenterThumbRt.anchoredPosition = autoOn ? new Vector2(-35f, 0f) : new Vector2(35f, 0f);
        }
        if (autoCenterThumbImg != null)
        {
            autoCenterThumbImg.color = autoOn ? ToggleGreenOn : ToggleSlateOff;
        }

        // 4. Pedals Tile
        bool isLeft = (SteeringWheelUI.CurrentPedalSide == SteeringWheelUI.PedalSide.Left);
        if (pedalsTitleText != null) pedalsTitleText.text = LocalizationManager.Get("LBL_PEDALS");
        if (pedalsValueText != null)
        {
            pedalsValueText.text = isLeft
                ? LocalizationManager.Get("VAL_SIDE_LEFT")
                : LocalizationManager.Get("VAL_SIDE_RIGHT");
        }

        // 5. Language Tile
        if (languageTitleText != null) languageTitleText.text = LocalizationManager.Get("LBL_LANGUAGE");
        if (languageValueText != null)
        {
            languageValueText.text = LocalizationManager.GetLanguageName(LocalizationManager.CurrentLanguage);
        }

        // Bottom Buttons
        if (restartBtnText != null) restartBtnText.text = LocalizationManager.Get("BTN_RESTART_MAP");
        if (mainMenuBtnText != null) mainMenuBtnText.text = LocalizationManager.Get("BTN_MAIN_MENU");
        if (resumeBtnText != null) resumeBtnText.text = LocalizationManager.Get("BTN_RESUME_CLEAN");
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
                Debug.LogWarning($"[InGameMenu] EditorSceneManager.LoadSceneInPlayMode error: {ex.Message}");
            }
        }
#endif
        SceneManager.LoadScene(currentScene);
    }

    public void GoToMainMenu()
    {
        SetMenuOpen(false);
        string currentScene = SceneManager.GetActiveScene().name;
        string cat = MapSelectMenu.GetMapCategory(currentScene);
        MapSelectMenu.PendingCategory = cat;
        PlayerPrefs.SetString(MapSelectMenu.PrefKey_LastCategory, cat);
        PlayerPrefs.SetString("CurrentActiveMap", currentScene);
        PlayerPrefs.SetString("LastPlayedMap", currentScene);
        PlayerPrefs.Save();
#if UNITY_EDITOR
        string mainMenuPath = "Assets/Scenes/MainMenu.unity";
        if (File.Exists(mainMenuPath))
        {
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(mainMenuPath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
            catch (Exception) {}
        }
#endif
        SceneManager.LoadScene("MainMenu");
    }

    // =========================================================================
    // UI CONSTRUCTION (MATCHING INDUSTRIAL PAUSE MOCKUP)
    // =========================================================================

    public void EnsureUI()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu") return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        GameObject canvasGo = canvas != null ? canvas.gameObject : GameObject.Find("TruckControlsCanvas");
        if (canvasGo == null) return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Top-Left Menu Button
        Transform existingBtn = canvasGo.transform.Find("TopLeft_MenuButton");
        if (existingBtn == null)
        {
            topMenuBtnGo = new GameObject("TopLeft_MenuButton");
            topMenuBtnGo.transform.SetParent(canvasGo.transform, false);

            RectTransform btnRt = topMenuBtnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 1f);
            btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            btnRt.anchoredPosition = new Vector2(30f, -180f);
            btnRt.sizeDelta = new Vector2(210f, 68f);

            Image btnImg = topMenuBtnGo.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.45f, 0.85f, 0.95f);

            Outline outline = topMenuBtnGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.65f, 1.0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = topMenuBtnGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            shadow.effectDistance = new Vector2(3f, -3f);

            Button btn = topMenuBtnGo.AddComponent<Button>();
            btn.onClick.AddListener(ToggleMenu);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(topMenuBtnGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            topMenuBtnText = textGo.AddComponent<Text>();
            if (font != null) topMenuBtnText.font = font;
            topMenuBtnText.fontSize = 24;
            topMenuBtnText.fontStyle = FontStyle.Bold;
            topMenuBtnText.alignment = TextAnchor.MiddleCenter;
            topMenuBtnText.color = Color.white;
            topMenuBtnText.text = LocalizationManager.Get("BTN_MENU");
        }
        else
        {
            topMenuBtnGo = existingBtn.gameObject;
            RectTransform btnRt = topMenuBtnGo.GetComponent<RectTransform>();
            if (btnRt != null)
            {
                btnRt.anchorMin = new Vector2(0f, 1f);
                btnRt.anchorMax = new Vector2(0f, 1f);
                btnRt.pivot = new Vector2(0f, 1f);
                btnRt.anchoredPosition = new Vector2(30f, -180f);
                btnRt.sizeDelta = new Vector2(210f, 68f);
            }
            topMenuBtnText = topMenuBtnGo.GetComponentInChildren<Text>();
            if (topMenuBtnText != null)
            {
                topMenuBtnText.fontSize = 24;
                topMenuBtnText.text = LocalizationManager.Get("BTN_MENU");
            }
        }

        // 1B. Top-Bar Save (Floppy Icon) & Load Checkpoint Buttons
        EnsureTopBarSaveLoadButtons(canvasGo.transform, font);

        // 1C. Optional "Restore Saved Position" Banner & Status Toast
        EnsureRestoreBannerAndToast(canvasGo.transform, font);

        // 2. Industrial Pause Modal (Popup)
        Transform existingModal = canvasGo.transform.Find("InGameMenu_Modal");
        if (existingModal != null)
        {
            Destroy(existingModal.gameObject);
        }

        menuModalGo = new GameObject("InGameMenu_Modal");
        menuModalGo.transform.SetParent(canvasGo.transform, false);

        RectTransform modalRt = menuModalGo.AddComponent<RectTransform>();
        modalRt.anchorMin = Vector2.zero;
        modalRt.anchorMax = Vector2.one;
        modalRt.sizeDelta = Vector2.zero;

        Image dimImg = menuModalGo.AddComponent<Image>();
        dimImg.color = new Color(0.04f, 0.05f, 0.08f, 0.84f);
        dimImg.raycastTarget = true;

        Button bgBtn = menuModalGo.AddComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(CloseMenu);

        // Main Industrial Pause Frame (620 x 880)
        GameObject cardGo = new GameObject("MenuCard");
        cardGo.transform.SetParent(menuModalGo.transform, false);
        RectTransform cardRt = cardGo.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(620f, 880f);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = cardGo.AddComponent<Image>();
        cardImg.sprite = GetOrCreatePausePanelSprite();
        cardImg.color = Color.white;
        cardImg.raycastTarget = true;

        Shadow cardShadow = cardGo.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0f, 0f, 0f, 0.80f);
        cardShadow.effectDistance = new Vector2(0f, -10f);

        // ── Trapezoid Header Texts: "PAUSE" + "PRACTICE V16" ──
        GameObject pauseTitleGo = new GameObject("PauseTitle");
        pauseTitleGo.transform.SetParent(cardGo.transform, false);
        RectTransform pauseTitleRt = pauseTitleGo.AddComponent<RectTransform>();
        pauseTitleRt.anchorMin = new Vector2(0.5f, 0.5f);
        pauseTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
        pauseTitleRt.pivot = new Vector2(0.5f, 0.5f);
        pauseTitleRt.sizeDelta = new Vector2(360f, 58f);
        pauseTitleRt.anchoredPosition = new Vector2(0f, 378f);

        pauseTitleText = pauseTitleGo.AddComponent<Text>();
        if (font != null) pauseTitleText.font = font;
        pauseTitleText.fontSize = 46;
        pauseTitleText.fontStyle = FontStyle.Bold;
        pauseTitleText.alignment = TextAnchor.MiddleCenter;
        pauseTitleText.color = GoldHeaderColor;
        pauseTitleText.resizeTextForBestFit = true;
        pauseTitleText.resizeTextMinSize = 28;
        pauseTitleText.resizeTextMaxSize = 48;
        pauseTitleText.raycastTarget = false;

        Shadow pauseShadow = pauseTitleGo.AddComponent<Shadow>();
        pauseShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        pauseShadow.effectDistance = new Vector2(2f, -2f);

        GameObject subTitleGo = new GameObject("LevelSubTitle");
        subTitleGo.transform.SetParent(cardGo.transform, false);
        RectTransform subTitleRt = subTitleGo.AddComponent<RectTransform>();
        subTitleRt.anchorMin = new Vector2(0.5f, 0.5f);
        subTitleRt.anchorMax = new Vector2(0.5f, 0.5f);
        subTitleRt.pivot = new Vector2(0.5f, 0.5f);
        subTitleRt.sizeDelta = new Vector2(360f, 36f);
        subTitleRt.anchoredPosition = new Vector2(0f, 330f);

        levelSubTitleText = subTitleGo.AddComponent<Text>();
        if (font != null) levelSubTitleText.font = font;
        levelSubTitleText.fontSize = 25;
        levelSubTitleText.fontStyle = FontStyle.Bold;
        levelSubTitleText.alignment = TextAnchor.MiddleCenter;
        levelSubTitleText.color = GoldValueColor;
        levelSubTitleText.resizeTextForBestFit = true;
        levelSubTitleText.resizeTextMinSize = 16;
        levelSubTitleText.resizeTextMaxSize = 26;
        levelSubTitleText.raycastTarget = false;

        Shadow subShadow = subTitleGo.AddComponent<Shadow>();
        subShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        subShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // ── Settings Grid (2 Columns) ──
        // Row 1 Left: SOUND (Toggle Pill + Speaker Icon)
        CreateToggleSwitchTile(
            cardGo.transform,
            "Tile_Sound",
            new Vector2(-139f, 180f),
            GetOrCreateSpeakerSprite(),
            new Vector2(52f, 52f),
            font,
            OnToggleSound,
            out soundTitleText,
            out soundThumbRt,
            out soundThumbImg,
            out soundOnText,
            out soundOffText);

        // Row 1 Right: CONTROLS (Steering Wheel Icon + Mode Text)
        CreateIconValueTile(
            cardGo.transform,
            "Tile_Controls",
            new Vector2(139f, 180f),
            GetOrCreateWheelIconSprite(),
            new Vector2(56f, 56f),
            font,
            OnToggleControlMode,
            out controlsTitleText,
            out controlsValueText);

        // Row 2 Left: AUTO-CENTER (Toggle Pill + Return Arrow Icon)
        CreateToggleSwitchTile(
            cardGo.transform,
            "Tile_AutoCenter",
            new Vector2(-139f, 54f),
            GetOrCreateReturnArrowSprite(),
            new Vector2(48f, 48f),
            font,
            OnToggleAutoCenter,
            out autoCenterTitleText,
            out autoCenterThumbRt,
            out autoCenterThumbImg,
            out autoCenterOnText,
            out autoCenterOffText);

        // Row 2 Right: PEDALS (Dual Pedals Icon + Left/Right Text)
        CreateIconValueTile(
            cardGo.transform,
            "Tile_Pedals",
            new Vector2(139f, 54f),
            GetOrCreatePedalsIconSprite(),
            new Vector2(58f, 58f),
            font,
            OnTogglePedalSide,
            out pedalsTitleText,
            out pedalsValueText);

        // Row 3 Left: LANGUAGE (Globe Icon + Language Name)
        CreateIconValueTile(
            cardGo.transform,
            "Tile_Language",
            new Vector2(-139f, -72f),
            GetOrCreateGlobeIconSprite(),
            new Vector2(54f, 54f),
            font,
            OnToggleLanguage,
            out languageTitleText,
            out languageValueText);

        // ── Metallic Horizontal Divider Bar ──
        GameObject dividerGo = new GameObject("MetallicDivider");
        dividerGo.transform.SetParent(cardGo.transform, false);
        RectTransform divRt = dividerGo.AddComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.5f, 0.5f);
        divRt.anchorMax = new Vector2(0.5f, 0.5f);
        divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.sizeDelta = new Vector2(540f, 8f);
        divRt.anchoredPosition = new Vector2(0f, -146f);

        Image divImg = dividerGo.AddComponent<Image>();
        divImg.sprite = GetOrCreateDividerSprite();
        divImg.raycastTarget = false;

        // ── Bottom Action Buttons ──
        // 1. RESTART MAP (Left, Bronze/Amber)
        restartBtnText = CreateActionCardButton(
            cardGo.transform,
            "RestartBtn",
            new Vector2(-139f, -222f),
            new Vector2(262f, 108f),
            new Color(0.45f, 0.28f, 0.07f, 1.0f),
            new Color(0.88f, 0.58f, 0.16f, 1.0f),
            GetOrCreateReloadIconSprite(),
            new Color(0.98f, 0.64f, 0.12f, 1.0f),
            font,
            RestartCurrentMap);

        // 2. MAIN MENU (Right, Terracotta/Crimson)
        mainMenuBtnText = CreateActionCardButton(
            cardGo.transform,
            "MainMenuBtn",
            new Vector2(139f, -222f),
            new Vector2(262f, 108f),
            new Color(0.45f, 0.20f, 0.18f, 1.0f),
            new Color(0.82f, 0.42f, 0.38f, 1.0f),
            GetOrCreateHomeIconSprite(),
            new Color(0.88f, 0.39f, 0.35f, 1.0f),
            font,
            GoToMainMenu);

        // 3. RESUME (Full-Width Bottom Primary CTA, Deep Emerald with Neon-Mint Border)
        resumeBtnText = CreateResumeButton(
            cardGo.transform,
            new Vector2(0f, -348f),
            new Vector2(540f, 110f),
            font,
            CloseMenu);

        bool hasSavedCheckpoint = SaveLoadTruckSystem.HasCheckpoint(currentScene);
        SetLoadButtonState(hasSavedCheckpoint);

        UpdateAllTexts();
        menuModalGo.SetActive(false);
    }

    /// <summary>
    /// Creates a dark-slate tile with a top-left title, bottom-left ON/OFF pill switch, and right-side icon.
    /// Used for SOUND and AUTO-CENTER.
    /// </summary>
    private void CreateToggleSwitchTile(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Sprite rightIconSprite,
        Vector2 iconSize,
        Font font,
        UnityEngine.Events.UnityAction onClick,
        out Text titleLabel,
        out RectTransform thumbRt,
        out Image thumbImg,
        out Text onLabel,
        out Text offLabel)
    {
        GameObject tileGo = CreateSlateTileBase(parent, name, anchoredPos, new Vector2(262f, 110f), onClick);

        // Top-Left Label ("SOUND" / "AUTO-CENTER")
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(tileGo.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.offsetMin = new Vector2(18f, -44f);
        titleRt.offsetMax = new Vector2(-12f, -10f);

        titleLabel = titleGo.AddComponent<Text>();
        if (font != null) titleLabel.font = font;
        titleLabel.fontSize = 20;
        titleLabel.fontStyle = FontStyle.Bold;
        titleLabel.alignment = TextAnchor.MiddleLeft;
        titleLabel.color = Color.white;
        titleLabel.resizeTextForBestFit = true;
        titleLabel.resizeTextMinSize = 13;
        titleLabel.resizeTextMaxSize = 21;
        titleLabel.raycastTarget = false;

        // Pill Track Container (Bottom-Left)
        GameObject trackGo = new GameObject("PillTrack");
        trackGo.transform.SetParent(tileGo.transform, false);
        RectTransform trackRt = trackGo.AddComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0f);
        trackRt.anchorMax = new Vector2(0f, 0f);
        trackRt.pivot = new Vector2(0f, 0f);
        trackRt.anchoredPosition = new Vector2(16f, 14f);
        trackRt.sizeDelta = new Vector2(146f, 42f);

        Image trackImg = trackGo.AddComponent<Image>();
        trackImg.sprite = GetOrCreatePillSprite();
        trackImg.type = Image.Type.Sliced;
        trackImg.color = new Color(0.33f, 0.36f, 0.41f, 1.0f);
        trackImg.raycastTarget = false;

        // Sliding Pill Thumb
        GameObject thumbGo = new GameObject("Thumb");
        thumbGo.transform.SetParent(trackGo.transform, false);
        thumbRt = thumbGo.AddComponent<RectTransform>();
        thumbRt.anchorMin = new Vector2(0.5f, 0.5f);
        thumbRt.anchorMax = new Vector2(0.5f, 0.5f);
        thumbRt.pivot = new Vector2(0.5f, 0.5f);
        thumbRt.sizeDelta = new Vector2(70f, 34f);
        thumbRt.anchoredPosition = new Vector2(-35f, 0f);

        thumbImg = thumbGo.AddComponent<Image>();
        thumbImg.sprite = GetOrCreatePillSprite();
        thumbImg.type = Image.Type.Sliced;
        thumbImg.color = ToggleGreenOn;
        thumbImg.raycastTarget = false;

        // "ON" Label (Left half of track)
        GameObject onGo = new GameObject("OnText");
        onGo.transform.SetParent(trackGo.transform, false);
        RectTransform onRt = onGo.AddComponent<RectTransform>();
        onRt.anchorMin = new Vector2(0f, 0f);
        onRt.anchorMax = new Vector2(0.5f, 1f);
        onRt.offsetMin = new Vector2(4f, 2f);
        onRt.offsetMax = new Vector2(-2f, -2f);

        onLabel = onGo.AddComponent<Text>();
        if (font != null) onLabel.font = font;
        onLabel.fontSize = 15;
        onLabel.fontStyle = FontStyle.Bold;
        onLabel.alignment = TextAnchor.MiddleCenter;
        onLabel.color = Color.white;
        onLabel.resizeTextForBestFit = true;
        onLabel.resizeTextMinSize = 10;
        onLabel.resizeTextMaxSize = 16;
        onLabel.raycastTarget = false;

        // "OFF" Label (Right half of track)
        GameObject offGo = new GameObject("OffText");
        offGo.transform.SetParent(trackGo.transform, false);
        RectTransform offRt = offGo.AddComponent<RectTransform>();
        offRt.anchorMin = new Vector2(0.5f, 0f);
        offRt.anchorMax = new Vector2(1f, 1f);
        offRt.offsetMin = new Vector2(2f, 2f);
        offRt.offsetMax = new Vector2(-4f, -2f);

        offLabel = offGo.AddComponent<Text>();
        if (font != null) offLabel.font = font;
        offLabel.fontSize = 15;
        offLabel.fontStyle = FontStyle.Bold;
        offLabel.alignment = TextAnchor.MiddleCenter;
        offLabel.color = new Color(0.82f, 0.85f, 0.88f, 0.85f);
        offLabel.resizeTextForBestFit = true;
        offLabel.resizeTextMinSize = 10;
        offLabel.resizeTextMaxSize = 16;
        offLabel.raycastTarget = false;

        // Right-Side Icon
        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(tileGo.transform, false);
        RectTransform iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(1f, 0.5f);
        iconRt.anchorMax = new Vector2(1f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(-42f, -8f);
        iconRt.sizeDelta = iconSize;

        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = rightIconSprite;
        iconImg.color = IconSilverColor;
        iconImg.raycastTarget = false;
    }

    /// <summary>
    /// Creates a dark-slate tile with a left-side icon, upper white title, and lower golden-amber value text.
    /// Used for CONTROLS, PEDALS, and LANGUAGE.
    /// </summary>
    private void CreateIconValueTile(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Sprite leftIconSprite,
        Vector2 iconSize,
        Font font,
        UnityEngine.Events.UnityAction onClick,
        out Text titleLabel,
        out Text valueLabel)
    {
        GameObject tileGo = CreateSlateTileBase(parent, name, anchoredPos, new Vector2(262f, 110f), onClick);

        // Left-Side Icon
        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(tileGo.transform, false);
        RectTransform iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(46f, 0f);
        iconRt.sizeDelta = iconSize;

        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = leftIconSprite;
        iconImg.color = IconSilverColor;
        iconImg.raycastTarget = false;

        // Upper Title ("CONTROLS" / "PEDALS" / "LANGUAGE")
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(tileGo.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(86f, 0f);
        titleRt.offsetMax = new Vector2(-12f, -14f);

        titleLabel = titleGo.AddComponent<Text>();
        if (font != null) titleLabel.font = font;
        titleLabel.fontSize = 20;
        titleLabel.fontStyle = FontStyle.Bold;
        titleLabel.alignment = TextAnchor.LowerLeft;
        titleLabel.color = Color.white;
        titleLabel.resizeTextForBestFit = true;
        titleLabel.resizeTextMinSize = 13;
        titleLabel.resizeTextMaxSize = 21;
        titleLabel.raycastTarget = false;

        // Lower Value ("ROUND WHEEL" / "LEFT" / "ENGLISH")
        GameObject valGo = new GameObject("Value");
        valGo.transform.SetParent(tileGo.transform, false);
        RectTransform valRt = valGo.AddComponent<RectTransform>();
        valRt.anchorMin = new Vector2(0f, 0f);
        valRt.anchorMax = new Vector2(1f, 0.5f);
        valRt.offsetMin = new Vector2(86f, 14f);
        valRt.offsetMax = new Vector2(-12f, -2f);

        valueLabel = valGo.AddComponent<Text>();
        if (font != null) valueLabel.font = font;
        valueLabel.fontSize = 17;
        valueLabel.fontStyle = FontStyle.Bold;
        valueLabel.alignment = TextAnchor.UpperLeft;
        valueLabel.color = GoldValueColor;
        valueLabel.resizeTextForBestFit = true;
        valueLabel.resizeTextMinSize = 11;
        valueLabel.resizeTextMaxSize = 18;
        valueLabel.raycastTarget = false;
    }

    private static GameObject CreateSlateTileBase(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject tileGo = new GameObject(name);
        tileGo.transform.SetParent(parent, false);
        RectTransform rt = tileGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        // Outer Border Layer
        Image borderImg = tileGo.AddComponent<Image>();
        borderImg.sprite = GetOrCreateRoundedBoxSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.35f, 0.40f, 0.48f, 1.0f);

        Shadow dropShadow = tileGo.AddComponent<Shadow>();
        dropShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        dropShadow.effectDistance = new Vector2(0f, -4f);

        // Inner Slate Fill Layer (inset by 2px for crisp rounded border)
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(tileGo.transform, false);
        RectTransform fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);

        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = GetOrCreateRoundedBoxSprite();
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(0.15f, 0.18f, 0.22f, 1.0f);
        fillImg.raycastTarget = false;

        Button btn = tileGo.AddComponent<Button>();
        btn.targetGraphic = borderImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        return tileGo;
    }

    private static Text CreateActionCardButton(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        Color fillColor,
        Color borderColor,
        Sprite topIcon,
        Color iconColor,
        Font font,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image borderImg = btnGo.AddComponent<Image>();
        borderImg.sprite = GetOrCreateRoundedBoxSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = borderColor;

        Shadow shadow = btnGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.70f);
        shadow.effectDistance = new Vector2(0f, -4f);

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(btnGo.transform, false);
        RectTransform fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2.5f, 2.5f);
        fillRt.offsetMax = new Vector2(-2.5f, -2.5f);

        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = GetOrCreateRoundedBoxSprite();
        fillImg.type = Image.Type.Sliced;
        fillImg.color = fillColor;
        fillImg.raycastTarget = false;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        // Top Centered Icon
        GameObject iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(btnGo.transform, false);
        RectTransform iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(0f, 18f);
        iconRt.sizeDelta = new Vector2(44f, 44f);

        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = topIcon;
        iconImg.color = iconColor;
        iconImg.raycastTarget = false;

        // Bottom Centered Text
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 0f);
        textRt.anchorMax = new Vector2(1f, 0.5f);
        textRt.offsetMin = new Vector2(10f, 8f);
        textRt.offsetMax = new Vector2(-10f, 2f);

        Text t = textGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = 21;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 13;
        t.resizeTextMaxSize = 22;
        t.raycastTarget = false;

        Shadow textShadow = textGo.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.80f);
        textShadow.effectDistance = new Vector2(1.5f, -1.5f);

        return t;
    }

    private static Text CreateResumeButton(
        Transform parent,
        Vector2 anchoredPos,
        Vector2 size,
        Font font,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject("ResumeBtn");
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        // Bright Neon-Emerald Border
        Image borderImg = btnGo.AddComponent<Image>();
        borderImg.sprite = GetOrCreateRoundedBoxSprite();
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(0.24f, 0.90f, 0.52f, 1.0f);

        Outline outerGlow = btnGo.AddComponent<Outline>();
        outerGlow.effectColor = new Color(0.18f, 0.85f, 0.45f, 0.38f);
        outerGlow.effectDistance = new Vector2(3f, -3f);

        Shadow shadow = btnGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(0f, -5f);

        // Deep Emerald Fill
        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(btnGo.transform, false);
        RectTransform fillRt = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(3f, 3f);
        fillRt.offsetMax = new Vector2(-3f, -3f);

        Image fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = GetOrCreateRoundedBoxSprite();
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(0.06f, 0.24f, 0.19f, 1.0f);
        fillImg.raycastTarget = false;

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = fillImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        cb.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        // Centered Horizontal Content (Play Triangle + RESUME Text)
        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(btnGo.transform, false);
        RectTransform contentRt = contentGo.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot = new Vector2(0.5f, 0.5f);
        contentRt.sizeDelta = new Vector2(460f, 64f);
        contentRt.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject playIconGo = new GameObject("PlayIcon");
        playIconGo.transform.SetParent(contentGo.transform, false);
        RectTransform playIconRt = playIconGo.AddComponent<RectTransform>();
        playIconRt.sizeDelta = new Vector2(42f, 42f);

        Image playIconImg = playIconGo.AddComponent<Image>();
        playIconImg.sprite = GetOrCreatePlayIconSprite();
        playIconImg.color = new Color(0.30f, 1.0f, 0.52f, 1.0f);
        playIconImg.raycastTarget = false;

        Shadow playGlow = playIconGo.AddComponent<Shadow>();
        playGlow.effectColor = new Color(0.20f, 0.95f, 0.45f, 0.55f);
        playGlow.effectDistance = new Vector2(0f, 0f);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(contentGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(240f, 60f);

        Text t = textGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = 34;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 20;
        t.resizeTextMaxSize = 36;
        t.raycastTarget = false;

        Shadow textShadow = textGo.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        textShadow.effectDistance = new Vector2(2f, -2f);

        return t;
    }

    // =========================================================================
    // TOP-BAR SAVE/LOAD BUTTONS & RESTORE BANNER
    // =========================================================================

    private void EnsureTopBarSaveLoadButtons(Transform canvasTransform, Font font)
    {
        Transform existingSave = canvasTransform.Find("TopBar_SaveButton");
        if (existingSave != null) Destroy(existingSave.gameObject);

        topSaveBtnGo = new GameObject("TopBar_SaveButton");
        topSaveBtnGo.transform.SetParent(canvasTransform, false);

        RectTransform saveRt = topSaveBtnGo.AddComponent<RectTransform>();
        saveRt.anchorMin = new Vector2(0f, 1f);
        saveRt.anchorMax = new Vector2(0f, 1f);
        saveRt.pivot = new Vector2(0f, 1f);
        saveRt.anchoredPosition = new Vector2(252f, -180f);
        saveRt.sizeDelta = new Vector2(150f, 68f);

        topSaveBtnBg = topSaveBtnGo.AddComponent<Image>();
        topSaveBtnBg.color = SaveBtnDefaultBg;

        Outline saveOutline = topSaveBtnGo.AddComponent<Outline>();
        saveOutline.effectColor = new Color(0.35f, 0.65f, 1.0f, 0.85f);
        saveOutline.effectDistance = new Vector2(2f, -2f);

        Shadow saveShadow = topSaveBtnGo.AddComponent<Shadow>();
        saveShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        saveShadow.effectDistance = new Vector2(3f, -3f);

        topSaveBtn = topSaveBtnGo.AddComponent<Button>();
        topSaveBtn.targetGraphic = topSaveBtnBg;
        topSaveBtn.onClick.AddListener(OnSaveClicked);

        GameObject iconGo = new GameObject("FloppyIcon");
        iconGo.transform.SetParent(topSaveBtnGo.transform, false);
        RectTransform iconRt = iconGo.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(12f, 0f);
        iconRt.sizeDelta = new Vector2(34f, 34f);

        topSaveFloppyIcon = iconGo.AddComponent<Image>();
        topSaveFloppyIcon.sprite = GetOrCreateFloppySprite();
        topSaveFloppyIcon.color = Color.white;
        topSaveFloppyIcon.raycastTarget = false;

        GameObject saveTextGo = new GameObject("Text");
        saveTextGo.transform.SetParent(topSaveBtnGo.transform, false);
        RectTransform saveTextRt = saveTextGo.AddComponent<RectTransform>();
        saveTextRt.anchorMin = new Vector2(0f, 0f);
        saveTextRt.anchorMax = new Vector2(1f, 1f);
        saveTextRt.offsetMin = new Vector2(50f, 0f);
        saveTextRt.offsetMax = new Vector2(-6f, 0f);

        topSaveBtnText = saveTextGo.AddComponent<Text>();
        if (font != null) topSaveBtnText.font = font;
        topSaveBtnText.fontSize = 20;
        topSaveBtnText.fontStyle = FontStyle.Bold;
        topSaveBtnText.alignment = TextAnchor.MiddleCenter;
        topSaveBtnText.color = Color.white;
        topSaveBtnText.resizeTextForBestFit = true;
        topSaveBtnText.resizeTextMinSize = 14;
        topSaveBtnText.resizeTextMaxSize = 21;
        topSaveBtnText.raycastTarget = false;
        topSaveBtnText.text = LocalizationManager.Get("BTN_SAVE_SHORT");

        Transform existingLoad = canvasTransform.Find("TopBar_LoadButton");
        if (existingLoad != null) Destroy(existingLoad.gameObject);

        topLoadBtnGo = new GameObject("TopBar_LoadButton");
        topLoadBtnGo.transform.SetParent(canvasTransform, false);

        RectTransform loadRt = topLoadBtnGo.AddComponent<RectTransform>();
        loadRt.anchorMin = new Vector2(0f, 1f);
        loadRt.anchorMax = new Vector2(0f, 1f);
        loadRt.pivot = new Vector2(0f, 1f);
        loadRt.anchoredPosition = new Vector2(414f, -180f);
        loadRt.sizeDelta = new Vector2(140f, 68f);

        topLoadBtnBg = topLoadBtnGo.AddComponent<Image>();
        topLoadBtnBg.color = LoadBtnActiveBg;

        Outline loadOutline = topLoadBtnGo.AddComponent<Outline>();
        loadOutline.effectColor = new Color(0.30f, 0.80f, 0.65f, 0.85f);
        loadOutline.effectDistance = new Vector2(2f, -2f);

        Shadow loadShadow = topLoadBtnGo.AddComponent<Shadow>();
        loadShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        loadShadow.effectDistance = new Vector2(3f, -3f);

        topLoadBtn = topLoadBtnGo.AddComponent<Button>();
        topLoadBtn.targetGraphic = topLoadBtnBg;
        topLoadBtn.onClick.AddListener(OnLoadClicked);

        GameObject loadTextGo = new GameObject("Text");
        loadTextGo.transform.SetParent(topLoadBtnGo.transform, false);
        RectTransform loadTextRt = loadTextGo.AddComponent<RectTransform>();
        loadTextRt.anchorMin = Vector2.zero;
        loadTextRt.anchorMax = Vector2.one;
        loadTextRt.offsetMin = new Vector2(6f, 0f);
        loadTextRt.offsetMax = new Vector2(-6f, 0f);

        topLoadBtnText = loadTextGo.AddComponent<Text>();
        if (font != null) topLoadBtnText.font = font;
        topLoadBtnText.fontSize = 20;
        topLoadBtnText.fontStyle = FontStyle.Bold;
        topLoadBtnText.alignment = TextAnchor.MiddleCenter;
        topLoadBtnText.color = Color.white;
        topLoadBtnText.resizeTextForBestFit = true;
        topLoadBtnText.resizeTextMinSize = 14;
        topLoadBtnText.resizeTextMaxSize = 21;
        topLoadBtnText.raycastTarget = false;
        topLoadBtnText.text = LocalizationManager.Get("BTN_LOAD_SHORT");
    }

    private void EnsureRestoreBannerAndToast(Transform canvasTransform, Font font)
    {
        Transform existingBanner = canvasTransform.Find("RestoreSavedPosition_Banner");
        if (existingBanner != null) Destroy(existingBanner.gameObject);

        restoreBannerGo = new GameObject("RestoreSavedPosition_Banner");
        restoreBannerGo.transform.SetParent(canvasTransform, false);

        RectTransform bannerRt = restoreBannerGo.AddComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 1f);
        bannerRt.anchorMax = new Vector2(0.5f, 1f);
        bannerRt.pivot = new Vector2(0.5f, 1f);
        bannerRt.anchoredPosition = new Vector2(0f, -95f);
        bannerRt.sizeDelta = new Vector2(540f, 64f);

        Image bannerBg = restoreBannerGo.AddComponent<Image>();
        bannerBg.color = new Color(0.10f, 0.44f, 0.34f, 0.96f);

        Outline bannerOutline = restoreBannerGo.AddComponent<Outline>();
        bannerOutline.effectColor = new Color(0.30f, 0.92f, 0.65f, 0.90f);
        bannerOutline.effectDistance = new Vector2(2f, -2f);

        Shadow bannerShadow = restoreBannerGo.AddComponent<Shadow>();
        bannerShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        bannerShadow.effectDistance = new Vector2(3f, -3f);

        GameObject restoreActionGo = new GameObject("RestoreActionBtn");
        restoreActionGo.transform.SetParent(restoreBannerGo.transform, false);
        RectTransform actionRt = restoreActionGo.AddComponent<RectTransform>();
        actionRt.anchorMin = new Vector2(0f, 0f);
        actionRt.anchorMax = new Vector2(0.86f, 1f);
        actionRt.offsetMin = new Vector2(12f, 4f);
        actionRt.offsetMax = new Vector2(-4f, -4f);

        Image actionImg = restoreActionGo.AddComponent<Image>();
        actionImg.color = Color.clear;

        Button actionBtn = restoreActionGo.AddComponent<Button>();
        actionBtn.targetGraphic = bannerBg;
        actionBtn.onClick.AddListener(OnLoadClicked);

        restoreBannerText = CreateBannerChildText(restoreActionGo.transform, font,
            LocalizationManager.Get("BANNER_RESTORE_PROMPT"), 21);

        GameObject dismissGo = new GameObject("DismissBtn");
        dismissGo.transform.SetParent(restoreBannerGo.transform, false);
        RectTransform dismissRt = dismissGo.AddComponent<RectTransform>();
        dismissRt.anchorMin = new Vector2(0.86f, 0f);
        dismissRt.anchorMax = new Vector2(1f, 1f);
        dismissRt.offsetMin = new Vector2(2f, 6f);
        dismissRt.offsetMax = new Vector2(-6f, -6f);

        Image dismissImg = dismissGo.AddComponent<Image>();
        dismissImg.color = new Color(0.08f, 0.22f, 0.18f, 0.85f);

        Button dismissBtn = dismissGo.AddComponent<Button>();
        dismissBtn.targetGraphic = dismissImg;
        dismissBtn.onClick.AddListener(HideRestoreSavedPositionBanner);

        CreateBannerChildText(dismissGo.transform, font, "✕", 22);
        restoreBannerGo.SetActive(false);

        Transform existingToast = canvasTransform.Find("CheckpointToast_Banner");
        if (existingToast != null) Destroy(existingToast.gameObject);

        toastBannerGo = new GameObject("CheckpointToast_Banner");
        toastBannerGo.transform.SetParent(canvasTransform, false);

        RectTransform toastRt = toastBannerGo.AddComponent<RectTransform>();
        toastRt.anchorMin = new Vector2(0.5f, 1f);
        toastRt.anchorMax = new Vector2(0.5f, 1f);
        toastRt.pivot = new Vector2(0.5f, 1f);
        toastRt.anchoredPosition = new Vector2(0f, -95f);
        toastRt.sizeDelta = new Vector2(420f, 56f);

        Image toastBg = toastBannerGo.AddComponent<Image>();
        toastBg.color = new Color(0.12f, 0.78f, 0.36f, 0.96f);
        toastBg.raycastTarget = false;

        Outline toastOutline = toastBannerGo.AddComponent<Outline>();
        toastOutline.effectColor = new Color(1f, 1f, 1f, 0.75f);
        toastOutline.effectDistance = new Vector2(1.5f, -1.5f);

        toastBannerText = CreateBannerChildText(toastBannerGo.transform, font, "", 22);
        toastBannerGo.SetActive(false);
    }

    private static Text CreateBannerChildText(Transform parent, Font font, string content, int fontSize)
    {
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(parent, false);
        RectTransform rt = textGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        Text t = textGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 14;
        t.resizeTextMaxSize = fontSize;
        t.raycastTarget = false;
        t.text = content;
        return t;
    }

    // =========================================================================
    // PROCEDURAL SPRITE GENERATORS (INDUSTRIAL PAUSE PANEL & VECTOR ICONS)
    // =========================================================================

    /// <summary>
    /// Generates the complete Industrial Pause Frame with:
    /// - Outer rounded dark metal frame & 4 corner rivets
    /// - Diamond-plate (tread plate) top-left & top-right corner triangles
    /// - Trapezoid top header badge ("PAUSE / PRACTICE V16")
    /// - Glowing golden-amber inner contour
    /// </summary>
    private static Sprite GetOrCreatePausePanelSprite()
    {
        if (cachedPausePanelSprite != null) return cachedPausePanelSprite;

        const int w = 310;
        const int h = 440;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[w * h];

        Color outerMetal = new Color(0.22f, 0.25f, 0.29f, 1.0f);
        Color outerEdgeBevel = new Color(0.34f, 0.38f, 0.43f, 1.0f);
        Color innerBodyDark = new Color(0.11f, 0.12f, 0.15f, 0.98f);
        Color headerDark = new Color(0.09f, 0.10f, 0.12f, 1.0f);
        Color amberGold = new Color(1.00f, 0.74f, 0.22f, 1.0f);
        Color amberGlow = new Color(0.95f, 0.62f, 0.12f, 0.45f);
        Color plateBase = new Color(0.31f, 0.34f, 0.38f, 1.0f);
        Color plateHighlight = new Color(0.46f, 0.50f, 0.55f, 1.0f);
        Color plateShadow = new Color(0.20f, 0.22f, 0.25f, 1.0f);

        const float outerR = 14f;
        const float innerMargin = 10f;
        const float innerR = 9f;

        // Trapezoid header geometry (y from 352 to 430)
        const float trapBottomY = 352f;
        const float trapTopY = 430f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float distOuter = SdfRoundedBox(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.5f), new Vector2(w * 0.5f - 1f, h * 0.5f - 1f), outerR);
                if (distOuter > 0.5f)
                {
                    pixels[y * w + x] = Color.clear;
                    continue;
                }

                float outerAlpha = Mathf.Clamp01(0.5f - distOuter);
                Color col = outerMetal;

                // Subtle outer rim highlight
                if (distOuter > -2.2f)
                {
                    col = Color.Lerp(outerMetal, outerEdgeBevel, 0.65f);
                }

                // Inner body SDF
                float distInner = SdfRoundedBox(
                    new Vector2(x, y),
                    new Vector2(w * 0.5f, h * 0.5f),
                    new Vector2(w * 0.5f - innerMargin, h * 0.5f - innerMargin),
                    innerR);

                if (distInner <= 0f)
                {
                    col = innerBodyDark;

                    // Top header region (y >= trapBottomY)
                    if (y >= trapBottomY - 4f)
                    {
                        float tY = Mathf.Clamp01((y - trapBottomY) / (trapTopY - trapBottomY));
                        // Trapezoid half-width widens from 86 at bottom (y=352) to 112 at top (y=430)
                        float halfTrapW = Mathf.Lerp(86f, 112f, tY);
                        float dxFromCenter = Mathf.Abs(x - w * 0.5f);

                        // Smooth bottom corners of trapezoid
                        float cornerRound = 0f;
                        if (y < trapBottomY + 12f)
                        {
                            float dy = (trapBottomY + 12f) - y;
                            cornerRound = 12f - Mathf.Sqrt(Mathf.Max(0f, 144f - dy * dy));
                        }

                        float trapEdgeDist = dxFromCenter - (halfTrapW - cornerRound);

                        if (y >= trapBottomY && trapEdgeDist < -1.5f)
                        {
                            // Inside dark trapezoid header
                            col = headerDark;
                        }
                        else if (y >= trapBottomY && trapEdgeDist > 1.5f)
                        {
                            // Outside trapezoid in top corners -> Diamond-Plate Metallic Texture!
                            col = plateBase;
                            int cellX = x % 14;
                            int cellY = y % 14;
                            bool alternate = (((x / 14) + (y / 14)) & 1) == 0;
                            float u = cellX - 7f;
                            float v = cellY - 7f;
                            float diag1 = alternate ? (u + v) : (u - v);
                            float diag2 = alternate ? (u - v) : (u + v);
                            if (Mathf.Abs(diag1) < 2.1f && Mathf.Abs(diag2) < 5.2f)
                            {
                                col = (diag1 > 0f) ? plateHighlight : plateShadow;
                            }

                            // Dark separator line at bottom of diamond-plate (y == trapBottomY..trapBottomY+2)
                            if (y < trapBottomY + 2.5f)
                            {
                                col = Color.Lerp(col, new Color(0.08f, 0.09f, 0.11f, 1f), 0.85f);
                            }
                        }

                        // Glowing Golden-Amber border along trapezoid sides & bottom
                        float distToTrapBorder = float.MaxValue;
                        if (y >= trapBottomY)
                        {
                            distToTrapBorder = Mathf.Abs(trapEdgeDist);
                        }
                        if (dxFromCenter <= halfTrapW - cornerRound + 1.5f)
                        {
                            float dBottom = Mathf.Abs(y - trapBottomY);
                            if (dBottom < distToTrapBorder) distToTrapBorder = dBottom;
                        }

                        if (distToTrapBorder < 4.0f)
                        {
                            float core = Mathf.Clamp01(1.8f - distToTrapBorder);
                            float glow = Mathf.Clamp01(1f - distToTrapBorder / 4.0f);
                            col = Color.Lerp(col, amberGlow, glow * 0.65f);
                            col = Color.Lerp(col, amberGold, core);
                        }
                    }

                    // Golden-Amber Inner Perimeter Border (around main body and top of trapezoid)
                    bool onDiamondPlate = (y > trapBottomY + 1f) && (Mathf.Abs(x - w * 0.5f) > Mathf.Lerp(86f, 112f, Mathf.Clamp01((y - trapBottomY) / (trapTopY - trapBottomY))) + 1.5f);
                    if (!onDiamondPlate)
                    {
                        float borderDist = Mathf.Abs(distInner + 1.2f);
                        if (borderDist < 3.8f)
                        {
                            float core = Mathf.Clamp01(1.7f - borderDist);
                            float glow = Mathf.Clamp01(1f - borderDist / 3.8f);
                            col = Color.Lerp(col, amberGlow, glow * 0.6f);
                            col = Color.Lerp(col, amberGold, core);
                        }
                    }
                }

                // 4 Corner Rivets / Bolts
                Vector2[] rivets =
                {
                    new Vector2(18f, 16f),
                    new Vector2(w - 18f, 16f),
                    new Vector2(18f, h - 16f),
                    new Vector2(w - 18f, h - 16f)
                };
                for (int r = 0; r < rivets.Length; r++)
                {
                    float dRivet = Vector2.Distance(new Vector2(x, y), rivets[r]);
                    if (dRivet < 4.5f)
                    {
                        if (dRivet > 3.2f)
                            col = new Color(0.10f, 0.11f, 0.13f, 1f);
                        else
                        {
                            col = new Color(0.55f, 0.59f, 0.64f, 1f);
                            // Screw slot line
                            if (Mathf.Abs(y - rivets[r].y) < 0.8f && Mathf.Abs(x - rivets[r].x) < 2.3f)
                                col = new Color(0.22f, 0.25f, 0.28f, 1f);
                        }
                    }
                }

                col.a *= outerAlpha;
                pixels[y * w + x] = col;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedPausePanelSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return cachedPausePanelSprite;
    }

    private static Sprite GetOrCreateRoundedBoxSprite()
    {
        if (cachedRoundedBoxSprite != null) return cachedRoundedBoxSprite;

        const int size = 64;
        const float radius = 10f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Vector2 half = new Vector2(size * 0.5f - 1f, size * 0.5f - 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = SdfRoundedBox(new Vector2(x, y), center, half, radius);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedRoundedBoxSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14, 14, 14, 14));
        return cachedRoundedBoxSprite;
    }

    private static Sprite GetOrCreatePillSprite()
    {
        if (cachedPillSprite != null) return cachedPillSprite;

        const int w = 64;
        const int h = 36;
        const float radius = 16.5f;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[w * h];
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        Vector2 half = new Vector2(w * 0.5f - 1f, h * 0.5f - 1f);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float d = SdfRoundedBox(new Vector2(x, y), center, half, radius);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedPillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18, 16, 18, 16));
        return cachedPillSprite;
    }

    private static Sprite GetOrCreateDividerSprite()
    {
        if (cachedDividerSprite != null) return cachedDividerSprite;

        const int w = 128;
        const int h = 8;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float edgeFade = Mathf.Sin(Mathf.Clamp01((float)x / (w - 1)) * Mathf.PI);
                edgeFade = Mathf.Pow(edgeFade, 0.35f);

                Color c;
                if (y >= 5) c = new Color(0.55f, 0.60f, 0.66f, 0.90f * edgeFade);
                else if (y >= 3) c = new Color(0.32f, 0.36f, 0.40f, 0.95f * edgeFade);
                else c = new Color(0.08f, 0.09f, 0.11f, 0.95f * edgeFade);

                pixels[y * w + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedDividerSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return cachedDividerSprite;
    }

    /// <summary>
    /// Crisp Speaker with sound waves icon (for SOUND tile).
    /// </summary>
    private static Sprite GetOrCreateSpeakerSprite()
    {
        if (cachedSpeakerSprite != null) return cachedSpeakerSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 coneOrigin = new Vector2(44f, 48f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;

                // Speaker rear box (x: 14..28, y: 36..60)
                if (x >= 14 && x <= 28 && y >= 36 && y <= 60)
                {
                    alpha = 1f;
                }

                // Speaker cone flaring from x=26 to x=48
                if (x >= 26 && x <= 48)
                {
                    float t = (x - 26f) / 22f;
                    float halfH = Mathf.Lerp(12f, 28f, t);
                    float dy = Mathf.Abs(y - 48f);
                    if (dy <= halfH + 1f)
                    {
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(halfH + 0.5f - dy));
                    }
                }

                // Sound wave arcs on the right (x > 50)
                Vector2 p = new Vector2(x, y) - coneOrigin;
                float r = p.magnitude;
                float ang = Mathf.Atan2(Mathf.Abs(p.y), p.x) * Mathf.Rad2Deg;
                if (ang <= 48f)
                {
                    float dWave1 = Mathf.Abs(r - 17f);
                    float dWave2 = Mathf.Abs(r - 28f);
                    if (dWave1 < 3.0f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.5f - dWave1));
                    if (dWave2 < 3.0f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.5f - dWave2));
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedSpeakerSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedSpeakerSprite;
    }

    /// <summary>
    /// Crisp Steering Wheel icon (for CONTROLS tile).
    /// </summary>
    private static Sprite GetOrCreateWheelIconSprite()
    {
        if (cachedWheelIconSprite != null) return cachedWheelIconSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 c = new Vector2(48f, 48f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float r = p.magnitude;
                float alpha = 0f;

                // Outer wheel rim (r: 32..40)
                float dRim = Mathf.Abs(r - 36f);
                if (dRim < 4.5f) alpha = Mathf.Max(alpha, Mathf.Clamp01(4.2f - dRim));

                // Center hub ring (r: 7..13)
                float dHub = Mathf.Abs(r - 10f);
                if (dHub < 3.5f) alpha = Mathf.Max(alpha, Mathf.Clamp01(3.2f - dHub));

                // 3 Spokes (Left, Right, Bottom) between r=11 and r=35
                if (r >= 10f && r <= 36f)
                {
                    // Horizontal spoke slightly angled down
                    if (p.y <= 3f && p.y >= -6f && Mathf.Abs(p.x) >= 9f)
                    {
                        float dSpokeH = Mathf.Abs(p.y + 1.5f);
                        if (dSpokeH < 4.0f) alpha = Mathf.Max(alpha, Mathf.Clamp01(3.5f - dSpokeH));
                    }
                    // Bottom vertical spoke
                    if (p.y < -8f && Mathf.Abs(p.x) < 5.0f)
                    {
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(4.2f - Mathf.Abs(p.x)));
                    }
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedWheelIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedWheelIconSprite;
    }

    /// <summary>
    /// Crisp U-Turn / Return Arrow icon (for AUTO-CENTER tile).
    /// </summary>
    private static Sprite GetOrCreateReturnArrowSprite()
    {
        if (cachedReturnArrowSprite != null) return cachedReturnArrowSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 arcCenter = new Vector2(54f, 40f);
        const float arcRadius = 20f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                Vector2 p = new Vector2(x, y);

                // Top horizontal segment going left from x=54 to x=28 at y=60
                if (x >= 28 && x <= 55)
                {
                    float dTop = Mathf.Abs(y - 60f);
                    if (dTop < 4.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(3.8f - dTop));
                }

                // Bottom horizontal segment from x=36 to x=54 at y=20
                if (x >= 36 && x <= 55)
                {
                    float dBot = Mathf.Abs(y - 20f);
                    if (dBot < 4.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(3.8f - dBot));
                }

                // Right semi-circle arc (x >= 54)
                if (x >= 53)
                {
                    float dArc = Mathf.Abs(Vector2.Distance(p, arcCenter) - arcRadius);
                    if (dArc < 4.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(3.8f - dArc));
                }

                // Left-pointing arrowhead at (22, 60)
                if (x >= 16 && x <= 38)
                {
                    float dx = x - 18f;
                    float halfW = dx * 0.78f;
                    float dy = Mathf.Abs(y - 60f);
                    if (dy <= halfW + 1f && dx >= 0f && dx <= 18f)
                    {
                        alpha = Mathf.Max(alpha, Mathf.Clamp01(halfW + 0.5f - dy));
                    }
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedReturnArrowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedReturnArrowSprite;
    }

    /// <summary>
    /// Crisp Dual Pedals icon (Small Brake Pedal + Tall Gas Pedal with grip dots, matching mockup).
    /// </summary>
    private static Sprite GetOrCreatePedalsIconSprite()
    {
        if (cachedPedalsIconSprite != null) return cachedPedalsIconSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                Vector2 pt = new Vector2(x, y);

                // 1. Left Brake Pedal (center: 24, 30, halfSize: 13 x 15)
                float dBrake = SdfRoundedBox(pt, new Vector2(24f, 30f), new Vector2(12f, 15f), 4f);
                if (dBrake < 0.5f)
                {
                    alpha = Mathf.Clamp01(0.5f - dBrake);
                    // 2x3 grip dots cutout
                    for (int r = -1; r <= 1; r++)
                    {
                        for (int c = -1; c <= 1; c += 2)
                        {
                            Vector2 dotCenter = new Vector2(24f + c * 4.5f, 30f + r * 6f);
                            if (Vector2.Distance(pt, dotCenter) < 2.3f) alpha = 0f;
                        }
                    }
                }

                // 2. Right Tall Gas Pedal (center: 62, 46, halfSize: 15 x 31)
                float dGas = SdfRoundedBox(pt, new Vector2(62f, 46f), new Vector2(15f, 30f), 5f);
                if (dGas < 0.5f)
                {
                    float gasAlpha = Mathf.Clamp01(0.5f - dGas);
                    // 2x4 grip dots cutout
                    for (int r = 0; r < 4; r++)
                    {
                        for (int c = -1; c <= 1; c += 2)
                        {
                            Vector2 dotCenter = new Vector2(62f + c * 5.5f, 25f + r * 14f);
                            if (Vector2.Distance(pt, dotCenter) < 3.0f) gasAlpha = 0f;
                        }
                    }
                    alpha = Mathf.Max(alpha, gasAlpha);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedPedalsIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedPedalsIconSprite;
    }

    /// <summary>
    /// Crisp Globe with meridians and parallels icon (for LANGUAGE tile).
    /// </summary>
    private static Sprite GetOrCreateGlobeIconSprite()
    {
        if (cachedGlobeIconSprite != null) return cachedGlobeIconSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 c = new Vector2(48f, 48f);
        const float radius = 36f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float r = p.magnitude;
                if (r > radius + 2.5f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                float alpha = 0f;

                // Outer circle rim
                float dRim = Mathf.Abs(r - radius);
                if (dRim < 3.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.8f - dRim));

                if (r <= radius)
                {
                    // Vertical & Horizontal center lines
                    if (Mathf.Abs(p.x) < 2.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.2f - Mathf.Abs(p.x)));
                    if (Mathf.Abs(p.y) < 2.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.2f - Mathf.Abs(p.y)));

                    // Upper & lower latitude lines (y = +16, -16)
                    float dLat = Mathf.Abs(Mathf.Abs(p.y) - 16f);
                    if (dLat < 2.2f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.2f - dLat));

                    // Elliptical meridian curves ((x/18)^2 + (y/36)^2 = 1)
                    float nx = p.x / 19f;
                    float ny = p.y / radius;
                    float ell = Mathf.Sqrt(nx * nx + ny * ny);
                    float dMeridian = Mathf.Abs(ell - 1f) * 20f;
                    if (dMeridian < 2.5f) alpha = Mathf.Max(alpha, Mathf.Clamp01(2.3f - dMeridian));
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedGlobeIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedGlobeIconSprite;
    }

    /// <summary>
    /// Crisp Circular Reload Arrow icon (for RESTART MAP button).
    /// </summary>
    private static Sprite GetOrCreateReloadIconSprite()
    {
        if (cachedReloadIconSprite != null) return cachedReloadIconSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        Vector2 c = new Vector2(48f, 46f);
        const float radius = 26f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float r = p.magnitude;
                float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg; // -180..180
                float alpha = 0f;

                // Circular arc with a gap in top-right (ang between 15 and 65 degrees)
                bool inGap = (ang > 10f && ang < 62f);
                if (!inGap)
                {
                    float dArc = Mathf.Abs(r - radius);
                    if (dArc < 4.5f) alpha = Mathf.Max(alpha, Mathf.Clamp01(4.2f - dArc));
                }

                // Arrowhead at top-right pointing clockwise/down-right around (63, 66)
                Vector2 arrowTip = new Vector2(72f, 58f);
                Vector2 arrowBase = new Vector2(56f, 70f);
                float dHead = DistanceToTriangle(new Vector2(x, y), arrowTip, new Vector2(54f, 56f), new Vector2(68f, 76f));
                if (dHead < 0.5f)
                {
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(0.5f - dHead));
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedReloadIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedReloadIconSprite;
    }

    /// <summary>
    /// Crisp Home / House icon (for MAIN MENU button).
    /// </summary>
    private static Sprite GetOrCreateHomeIconSprite()
    {
        if (cachedHomeIconSprite != null) return cachedHomeIconSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 0f;
                Vector2 pt = new Vector2(x, y);

                // House lower body (x: 24..72, y: 16..50)
                if (x >= 24 && x <= 72 && y >= 16 && y <= 50)
                {
                    alpha = 1f;
                    // Door cutout (x: 42..54, y: 16..34)
                    if (x >= 42 && x <= 54 && y >= 16 && y <= 34)
                    {
                        alpha = 0f;
                    }
                }

                // Pitched roof triangle: apex (48, 80), base (12, 46) to (84, 46)
                float dRoof = DistanceToTriangle(pt, new Vector2(48f, 80f), new Vector2(12f, 46f), new Vector2(84f, 46f));
                if (dRoof < 0.5f)
                {
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(0.5f - dRoof));
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedHomeIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedHomeIconSprite;
    }

    /// <summary>
    /// Crisp Play Triangle icon (for RESUME button).
    /// </summary>
    private static Sprite GetOrCreatePlayIconSprite()
    {
        if (cachedPlayIconSprite != null) return cachedPlayIconSprite;

        const int size = 96;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];

        Vector2 a = new Vector2(22f, 16f);
        Vector2 b = new Vector2(22f, 80f);
        Vector2 c = new Vector2(78f, 48f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = DistanceToTriangle(new Vector2(x, y), a, b, c);
                float alpha = Mathf.Clamp01(1.5f - d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        cachedPlayIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedPlayIconSprite;
    }

    private static Sprite GetOrCreateFloppySprite()
    {
        if (cachedFloppySprite != null) return cachedFloppySprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        Color bodyCol = new Color(0.92f, 0.95f, 1.0f, 1.0f);
        Color shutterCol = new Color(0.20f, 0.26f, 0.36f, 1.0f);
        Color labelCol = new Color(0.22f, 0.52f, 0.88f, 1.0f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                bool insideBody = (x >= 6 && x <= 57 && y >= 6 && y <= 57);
                if (insideBody && (x + y > 104)) insideBody = false;

                if (insideBody)
                {
                    c = bodyCol;
                    if (y >= 38 && y <= 57 && x >= 16 && x <= 44)
                    {
                        c = shutterCol;
                        if (y >= 42 && y <= 53 && x >= 32 && x <= 39) c = bodyCol;
                    }
                    if (y >= 8 && y <= 30 && x >= 13 && x <= 50)
                    {
                        c = labelCol;
                        if ((y == 15 || y == 22) && x >= 17 && x <= 46) c = bodyCol;
                    }
                }
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        cachedFloppySprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedFloppySprite;
    }

    private static float SdfRoundedBox(Vector2 p, Vector2 center, Vector2 b, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - b + new Vector2(r, r);
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float DistanceToTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
        Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
        Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
        Vector2 d = Vector2.Min(
            Vector2.Min(
                new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
            new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));
        return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
    }

    public static string GetCurrentMapDisplayName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.Replace("-", " ").Replace("_", " ").ToUpperInvariant();
    }
}
