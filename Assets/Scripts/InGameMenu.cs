using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// In-Game Menu (Top-Left ☰ Button):
/// Provides:
/// 1. 🌐 Смена языка (RU -> EN -> FR -> ES -> RU)
/// 2. 🎮 Смена типа управления (Стрелки / Слайдер / Руль)
/// 3. 🔄 Перезапуск карты
/// 4. 🗺 Выход в главное меню
/// 5. ▶ Продолжить
/// </summary>
public class InGameMenu : MonoBehaviour
{
    public static InGameMenu Instance { get; private set; }

    private GameObject menuModalGo;
    private GameObject topMenuBtnGo;
    private Text topMenuBtnText;
    private Text titleText;
    private Text languageBtnText;
    private Text controlModeBtnText;
    private Text pedalSideBtnText;
    private Text autoCenterBtnText;
    private Text soundBtnText;
    private Text restartBtnText;
    private Text mainMenuBtnText;
    private Text resumeBtnText;

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
        EnsureUI();
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

    public void ToggleMenu()
    {
        SetMenuOpen(!isMenuOpen);
    }

    public void OpenMenu() => SetMenuOpen(true);
    public void CloseMenu() => SetMenuOpen(false);

    public void SetMenuOpen(bool open)
    {
        isMenuOpen = open;
        if (menuModalGo != null)
        {
            menuModalGo.SetActive(isMenuOpen);
            if (isMenuOpen)
            {
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
        SteeringControlType nextType = SteeringWheelUI.CycleControlType();
        if (controlModeBtnText != null)
        {
            controlModeBtnText.text = LocalizationManager.Get("MENU_CONTROLS_PREFIX") + SteeringWheelUI.GetControlTypeName(nextType);
        }
    }

    public void OnTogglePedalSide()
    {
        SteeringWheelUI.TogglePedalSide();
        UpdateAllTexts();
    }

    public void OnToggleAutoCenter()
    {
        bool newVal = SteeringWheelUI.ToggleAutoCenter();
        if (autoCenterBtnText != null)
        {
            autoCenterBtnText.text = LocalizationManager.GetAutoCenterButtonText(newVal);
        }
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
        bool newVal = ToggleSound();
        if (soundBtnText != null)
        {
            soundBtnText.text = LocalizationManager.GetSoundButtonText(newVal);
        }
    }

    public void UpdateAllTexts()
    {
        if (topMenuBtnText != null)
        {
            topMenuBtnText.text = LocalizationManager.Get("BTN_MENU");
        }
        if (titleText != null)
        {
            titleText.text = GetCurrentMapDisplayName();
        }
        if (languageBtnText != null)
        {
            languageBtnText.text = LocalizationManager.GetLanguageButtonText();
        }
        if (controlModeBtnText != null)
        {
            controlModeBtnText.text = LocalizationManager.Get("MENU_CONTROLS_PREFIX") + SteeringWheelUI.GetControlTypeName(SteeringWheelUI.CurrentControlType);
        }
        if (pedalSideBtnText != null)
        {
            bool isLeft = (SteeringWheelUI.CurrentPedalSide == SteeringWheelUI.PedalSide.Left);
            pedalSideBtnText.text = LocalizationManager.GetPedalSideButtonText(isLeft);
        }
        if (autoCenterBtnText != null)
        {
            autoCenterBtnText.text = LocalizationManager.GetAutoCenterButtonText(SteeringWheelUI.AutoCenterEnabled);
        }
        if (soundBtnText != null)
        {
            soundBtnText.text = LocalizationManager.GetSoundButtonText(SoundEnabled);
        }
        if (restartBtnText != null)
        {
            restartBtnText.text = LocalizationManager.Get("MENU_RESTART");
        }
        if (mainMenuBtnText != null)
        {
            mainMenuBtnText.text = LocalizationManager.Get("MENU_MAIN");
        }
        if (resumeBtnText != null)
        {
            resumeBtnText.text = LocalizationManager.Get("MENU_RESUME");
        }
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

    public void EnsureUI()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == "MainMenu") return; // Not needed in MainMenu scene

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

        // 2. In-Game Menu Modal (Popup)
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

        // Semi-transparent dim background (clicking empty area returns to map)
        Image dimImg = menuModalGo.AddComponent<Image>();
        dimImg.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);
        dimImg.raycastTarget = true;

        Button bgBtn = menuModalGo.AddComponent<Button>();
        bgBtn.transition = Selectable.Transition.None;
        bgBtn.onClick.AddListener(CloseMenu);

        // Modal Card Box (Height 710 to comfortably hold 8 buttons)
        GameObject cardGo = new GameObject("MenuCard");
        cardGo.transform.SetParent(menuModalGo.transform, false);
        RectTransform cardRt = cardGo.AddComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(560f, 710f);
        cardRt.anchoredPosition = Vector2.zero;

        Image cardImg = cardGo.AddComponent<Image>();
        cardImg.color = new Color(0.10f, 0.13f, 0.19f, 0.98f);
        cardImg.raycastTarget = true;

        Outline cardOutline = cardGo.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.20f, 0.40f, 0.70f, 0.8f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        // Title (displays current map name)
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(cardGo.transform, false);
        RectTransform titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.88f);
        titleRt.anchorMax = new Vector2(1f, 0.98f);
        titleRt.sizeDelta = Vector2.zero;

        titleText = titleGo.AddComponent<Text>();
        if (font != null) titleText.font = font;
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 0.85f, 0.20f, 1f);
        titleText.resizeTextForBestFit = true;
        titleText.resizeTextMinSize = 16;
        titleText.resizeTextMaxSize = 26;
        titleText.text = GetCurrentMapDisplayName();

        Shadow titleShadow = titleGo.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0, 0, 0, 0.85f);
        titleShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Button 1 (Top item): Смена языка (RU -> EN -> FR -> ES -> RU)
        languageBtnText = CreateModalButton(cardGo.transform, "LanguageBtn",
            LocalizationManager.GetLanguageButtonText(),
            new Vector2(0f, 202f), new Color(0.42f, 0.28f, 0.82f, 1f), font, OnToggleLanguage);

        // Button 2: Сменить тип управления (Стрелки / Слайдер / Руль)
        controlModeBtnText = CreateModalButton(cardGo.transform, "ControlModeBtn",
            LocalizationManager.Get("MENU_CONTROLS_PREFIX") + SteeringWheelUI.GetControlTypeName(SteeringWheelUI.CurrentControlType),
            new Vector2(0f, 146f), new Color(0.20f, 0.50f, 0.90f, 1f), font, OnToggleControlMode);

        // Button 3: Положение педалей (Педали слева / Педали справа)
        bool isPedalsLeft = (SteeringWheelUI.CurrentPedalSide == SteeringWheelUI.PedalSide.Left);
        pedalSideBtnText = CreateModalButton(cardGo.transform, "PedalSideBtn",
            LocalizationManager.GetPedalSideButtonText(isPedalsLeft),
            new Vector2(0f, 90f), new Color(0.85f, 0.45f, 0.15f, 1f), font, OnTogglePedalSide);

        // Button 4: Возврат руля (ВКЛ / ВЫКЛ)
        autoCenterBtnText = CreateModalButton(cardGo.transform, "AutoCenterBtn",
            LocalizationManager.GetAutoCenterButtonText(SteeringWheelUI.AutoCenterEnabled),
            new Vector2(0f, 34f), new Color(0.12f, 0.60f, 0.55f, 1f), font, OnToggleAutoCenter);

        // Button 5 (Под возвратом руля): Звук (ВКЛ / ВЫКЛ)
        soundBtnText = CreateModalButton(cardGo.transform, "SoundBtn",
            LocalizationManager.GetSoundButtonText(SoundEnabled),
            new Vector2(0f, -22f), new Color(0.72f, 0.28f, 0.60f, 1f), font, OnToggleSound);

        // Button 6: Перезапустить карту
        restartBtnText = CreateModalButton(cardGo.transform, "RestartBtn",
            LocalizationManager.Get("MENU_RESTART"),
            new Vector2(0f, -78f), new Color(0.18f, 0.65f, 0.85f, 1f), font, RestartCurrentMap);

        // Button 7: В главное меню (Выбор всех карт)
        mainMenuBtnText = CreateModalButton(cardGo.transform, "MainMenuBtn",
            LocalizationManager.Get("MENU_MAIN"),
            new Vector2(0f, -134f), new Color(0.18f, 0.75f, 0.45f, 1f), font, GoToMainMenu);

        // Button 8: Продолжить
        resumeBtnText = CreateModalButton(cardGo.transform, "ResumeBtn",
            LocalizationManager.Get("MENU_RESUME"),
            new Vector2(0f, -190f), new Color(0.35f, 0.38f, 0.45f, 1f), font, CloseMenu);

        UpdateAllTexts();
        menuModalGo.SetActive(false);
    }

    private Text CreateModalButton(Transform parent, string name, string text, Vector2 anchoredPos, Color bgColor, Font font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480f, 50f);
        rt.anchoredPosition = anchoredPos;

        Image img = btnGo.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(btnGo.transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        Text t = textGo.AddComponent<Text>();
        if (font != null) t.font = font;
        t.fontSize = 20;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;

        return t;
    }

    public static string GetCurrentMapDisplayName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.Replace("-", " ").Replace("_", " ").ToUpperInvariant();
    }
}
