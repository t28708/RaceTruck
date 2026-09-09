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
/// Provides two main actions:
/// 1. 🔄 Перезапустить карту (Restart current map)
/// 2. 🗺 В главное меню (Main menu / Select all maps)
/// 3. ▶ Продолжить (Resume game)
/// </summary>
public class InGameMenu : MonoBehaviour
{
    public static InGameMenu Instance { get; private set; }

    private GameObject menuModalGo;
    private GameObject topMenuBtnGo;
    private bool isMenuOpen = false;

    public bool IsMenuOpen => isMenuOpen;

    private void Awake()
    {
        Instance = this;
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

    private Text controlModeBtnText;

    public void SetMenuOpen(bool open)
    {
        isMenuOpen = open;
        if (menuModalGo != null)
        {
            menuModalGo.SetActive(isMenuOpen);
            if (isMenuOpen && controlModeBtnText != null)
            {
                controlModeBtnText.text = "🎮 УПРАВЛЕНИЕ: " + SteeringWheelUI.GetControlTypeName(SteeringWheelUI.CurrentControlType);
            }
        }
    }

    public void OnToggleControlMode()
    {
        SteeringControlType nextType = SteeringWheelUI.CycleControlType();
        if (controlModeBtnText != null)
        {
            controlModeBtnText.text = "🎮 УПРАВЛЕНИЕ: " + SteeringWheelUI.GetControlTypeName(nextType);
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
        if (MapSelectMenu.Instance != null)
        {
            MapSelectMenu.Instance.OpenMenu();
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
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

        // 1. Top-Left Menu Button (Lowered down to Y: -180 so it doesn't touch the steering slider)
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

            Text t = textGo.AddComponent<Text>();
            if (font != null) t.font = font;
            t.fontSize = 24;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = "☰ МЕНЮ";
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
            Text t = topMenuBtnGo.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.fontSize = 24;
            }
        }

        // 2. In-Game Menu Modal (Popup)
        Transform existingModal = canvasGo.transform.Find("InGameMenu_Modal");
        if (existingModal == null)
        {
            menuModalGo = new GameObject("InGameMenu_Modal");
            menuModalGo.transform.SetParent(canvasGo.transform, false);

            RectTransform modalRt = menuModalGo.AddComponent<RectTransform>();
            modalRt.anchorMin = Vector2.zero;
            modalRt.anchorMax = Vector2.one;
            modalRt.sizeDelta = Vector2.zero;

            // Semi-transparent dim background
            Image dimImg = menuModalGo.AddComponent<Image>();
            dimImg.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);
            dimImg.raycastTarget = true;

            // Modal Card Box
            GameObject cardGo = new GameObject("MenuCard");
            cardGo.transform.SetParent(menuModalGo.transform, false);
            RectTransform cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 470f);
            cardRt.anchoredPosition = Vector2.zero;

            Image cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.10f, 0.13f, 0.19f, 0.98f);

            Outline cardOutline = cardGo.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.20f, 0.40f, 0.70f, 0.8f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(cardGo.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.78f);
            titleRt.anchorMax = new Vector2(1f, 0.98f);
            titleRt.sizeDelta = Vector2.zero;

            Text titleText = titleGo.AddComponent<Text>();
            if (font != null) titleText.font = font;
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.85f, 0.20f, 1f);
            titleText.text = "⏸ МЕНЮ ИГРЫ";

            // Button 1: Сменить тип управления
            controlModeBtnText = CreateModalButton(cardGo.transform, "ControlModeBtn",
                "🎮 УПРАВЛЕНИЕ: " + SteeringWheelUI.GetControlTypeName(SteeringWheelUI.CurrentControlType),
                new Vector2(0f, 95f), new Color(0.20f, 0.50f, 0.90f, 1f), font, OnToggleControlMode);

            // Button 2: Перезапустить карту
            CreateModalButton(cardGo.transform, "RestartBtn", "🔄 ПЕРЕЗАПУСТИТЬ КАРТУ",
                new Vector2(0f, 25f), new Color(0.18f, 0.65f, 0.85f, 1f), font, RestartCurrentMap);

            // Button 3: В главное меню (Выбор всех карт)
            CreateModalButton(cardGo.transform, "MainMenuBtn", "🗺 В ГЛАВНОЕ МЕНЮ",
                new Vector2(0f, -45f), new Color(0.18f, 0.75f, 0.45f, 1f), font, GoToMainMenu);

            // Button 4: Продолжить
            CreateModalButton(cardGo.transform, "ResumeBtn", "▶ ПРОДОЛЖИТЬ",
                new Vector2(0f, -115f), new Color(0.35f, 0.38f, 0.45f, 1f), font, CloseMenu);

            menuModalGo.SetActive(false);
        }
        else
        {
            menuModalGo = existingModal.gameObject;
            Transform controlBtn = menuModalGo.transform.Find("MenuCard/ControlModeBtn");
            if (controlBtn != null)
            {
                controlModeBtnText = controlBtn.GetComponentInChildren<Text>();
            }
            menuModalGo.SetActive(false);
        }
    }

    private Text CreateModalButton(Transform parent, string name, string text, Vector2 anchoredPos, Color bgColor, Font font, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnGo = new GameObject(name);
        btnGo.transform.SetParent(parent, false);

        RectTransform rt = btnGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480f, 54f);
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
}
