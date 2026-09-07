using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CameraZoomUI : MonoBehaviour
{
    [SerializeField] private Text zoomLabel;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Image leftBtnImage;
    [SerializeField] private Image rightBtnImage;
    [SerializeField] private Text leftBtnText;
    [SerializeField] private Text rightBtnText;

    private CameraFollow cameraFollow;
    private int currentDisplayedMultiplier = -1;
    private float lastLeftClickTime = 0f;
    private float lastRightClickTime = 0f;

    private void Awake()
    {
        FindComponents();
    }

    private void Start()
    {
        ConnectCameraFollow();
        UpdateUI(cameraFollow != null ? cameraFollow.ZoomMultiplier : 1);
    }

    private void OnEnable()
    {
        ConnectCameraFollow();
        if (cameraFollow != null)
        {
            cameraFollow.OnZoomMultiplierChanged -= OnZoomMultiplierChanged;
            cameraFollow.OnZoomMultiplierChanged += OnZoomMultiplierChanged;
            UpdateUI(cameraFollow.ZoomMultiplier);
        }
    }

    private void OnDisable()
    {
        if (cameraFollow != null)
        {
            cameraFollow.OnZoomMultiplierChanged -= OnZoomMultiplierChanged;
        }
    }

    private void Update()
    {
        if (cameraFollow == null)
        {
            ConnectCameraFollow();
            if (cameraFollow != null)
            {
                cameraFollow.OnZoomMultiplierChanged -= OnZoomMultiplierChanged;
                cameraFollow.OnZoomMultiplierChanged += OnZoomMultiplierChanged;
                UpdateUI(cameraFollow.ZoomMultiplier);
            }
        }
        else if (cameraFollow.ZoomMultiplier != currentDisplayedMultiplier)
        {
            UpdateUI(cameraFollow.ZoomMultiplier);
        }
    }

    private void ConnectCameraFollow()
    {
        if (cameraFollow == null)
        {
            cameraFollow = CameraFollow.Instance;
            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow>();
            }
            if (cameraFollow == null)
            {
                cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
            }
            if (cameraFollow == null)
            {
                Camera cam = Camera.main ?? Object.FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    cameraFollow = cam.GetComponent<CameraFollow>() ?? cam.gameObject.AddComponent<CameraFollow>();
                }
            }
        }
    }

    public void FindComponents()
    {
        if (zoomLabel == null)
        {
            Transform t = transform.Find("ZoomLabel");
            if (t != null) zoomLabel = t.GetComponent<Text>();
        }

        if (leftButton == null)
        {
            Transform t = transform.Find("ZoomLeftButton");
            if (t != null)
            {
                leftButton = t.GetComponent<Button>();
                leftBtnImage = t.GetComponent<Image>();
                leftBtnText = t.GetComponentInChildren<Text>();
            }
        }

        if (rightButton == null)
        {
            Transform t = transform.Find("ZoomRightButton");
            if (t != null)
            {
                rightButton = t.GetComponent<Button>();
                rightBtnImage = t.GetComponent<Image>();
                rightBtnText = t.GetComponentInChildren<Text>();
            }
        }

        // Fix raycast targets and rect bounds on existing elements so they never block buttons
        if (zoomLabel != null)
        {
            zoomLabel.raycastTarget = false;
            RectTransform labelRt = zoomLabel.rectTransform;
            labelRt.anchorMin = new Vector2(0.28f, 0f);
            labelRt.anchorMax = new Vector2(0.72f, 1f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }

        if (leftBtnText != null)
        {
            leftBtnText.raycastTarget = false;
        }

        if (rightBtnText != null)
        {
            rightBtnText.raycastTarget = false;
        }

        if (leftBtnImage != null)
        {
            leftBtnImage.raycastTarget = true;
        }

        if (rightBtnImage != null)
        {
            rightBtnImage.raycastTarget = true;
        }

        if (leftButton != null)
        {
            leftButton.onClick.RemoveListener(OnLeftClicked);
            leftButton.onClick.AddListener(OnLeftClicked);

            ZoomPointerTrigger trigger = leftButton.GetComponent<ZoomPointerTrigger>();
            if (trigger == null) trigger = leftButton.gameObject.AddComponent<ZoomPointerTrigger>();
            trigger.onClick = OnLeftClicked;
        }

        if (rightButton != null)
        {
            rightButton.onClick.RemoveListener(OnRightClicked);
            rightButton.onClick.AddListener(OnRightClicked);

            ZoomPointerTrigger trigger = rightButton.GetComponent<ZoomPointerTrigger>();
            if (trigger == null) trigger = rightButton.gameObject.AddComponent<ZoomPointerTrigger>();
            trigger.onClick = OnRightClicked;
        }
    }

    public void OnLeftClicked()
    {
        if (Time.unscaledTime - lastLeftClickTime < 0.05f) return;
        lastLeftClickTime = Time.unscaledTime;

        ConnectCameraFollow();
        if (cameraFollow != null)
        {
            cameraFollow.StepZoom(-1);
            UpdateUI(cameraFollow.ZoomMultiplier);
        }
    }

    public void OnRightClicked()
    {
        if (Time.unscaledTime - lastRightClickTime < 0.05f) return;
        lastRightClickTime = Time.unscaledTime;

        ConnectCameraFollow();
        if (cameraFollow != null)
        {
            cameraFollow.StepZoom(+1);
            UpdateUI(cameraFollow.ZoomMultiplier);
        }
    }

    private void OnZoomMultiplierChanged(int mult)
    {
        UpdateUI(mult);
    }

    public void UpdateUI(int mult)
    {
        currentDisplayedMultiplier = mult;

        if (zoomLabel != null)
        {
            zoomLabel.text = $"{mult}x";
        }

        bool canDecrease = mult > CameraFollow.MinZoomMultiplier;
        bool canIncrease = mult < CameraFollow.MaxZoomMultiplier;

        if (leftButton != null) leftButton.interactable = canDecrease;
        if (rightButton != null) rightButton.interactable = canIncrease;

        if (leftBtnImage != null)
        {
            leftBtnImage.color = canDecrease
                ? new Color(0.24f, 0.30f, 0.40f, 0.95f)
                : new Color(0.18f, 0.20f, 0.25f, 0.35f);
        }
        if (leftBtnText != null)
        {
            leftBtnText.color = canDecrease
                ? Color.white
                : new Color(0.6f, 0.6f, 0.6f, 0.35f);
        }

        if (rightBtnImage != null)
        {
            rightBtnImage.color = canIncrease
                ? new Color(0.24f, 0.30f, 0.40f, 0.95f)
                : new Color(0.18f, 0.20f, 0.25f, 0.35f);
        }
        if (rightBtnText != null)
        {
            rightBtnText.color = canIncrease
                ? Color.white
                : new Color(0.6f, 0.6f, 0.6f, 0.35f);
        }
    }

    public static GameObject CreateZoomWidget(GameObject canvasGo)
    {
        Transform existing = canvasGo.transform.Find("CameraZoomWidget");
        if (existing != null)
        {
            CameraZoomUI existingUi = existing.GetComponent<CameraZoomUI>();
            if (existingUi == null) existingUi = existing.gameObject.AddComponent<CameraZoomUI>();
            existingUi.FindComponents();
            return existing.gameObject;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Container
        GameObject widgetGo = new GameObject("CameraZoomWidget");
        widgetGo.transform.SetParent(canvasGo.transform, false);

        RectTransform widgetRt = widgetGo.AddComponent<RectTransform>();
        widgetRt.anchorMin = new Vector2(1f, 1f);
        widgetRt.anchorMax = new Vector2(1f, 1f);
        widgetRt.pivot = new Vector2(1f, 1f);
        widgetRt.anchoredPosition = new Vector2(-25f, -25f);
        widgetRt.sizeDelta = new Vector2(210f, 54f);

        Image bg = widgetGo.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.12f, 0.16f, 0.88f);
        bg.raycastTarget = false;

        Outline outline = widgetGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.42f, 0.55f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // 1. Center Text "1x" (Constrained to center 44%, never raycastable)
        GameObject labelGo = new GameObject("ZoomLabel");
        labelGo.transform.SetParent(widgetGo.transform, false);
        RectTransform labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.28f, 0f);
        labelRt.anchorMax = new Vector2(0.72f, 1f);
        labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        Text label = labelGo.AddComponent<Text>();
        if (font != null) label.font = font;
        label.text = "1x";
        label.fontSize = 24;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.92f, 0.3f, 1f);
        label.raycastTarget = false;

        // 2. Left button "<"
        GameObject leftGo = new GameObject("ZoomLeftButton");
        leftGo.transform.SetParent(widgetGo.transform, false);
        RectTransform leftRt = leftGo.AddComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0f, 0f);
        leftRt.anchorMax = new Vector2(0f, 1f);
        leftRt.pivot = new Vector2(0f, 0.5f);
        leftRt.anchoredPosition = new Vector2(5f, 0f);
        leftRt.sizeDelta = new Vector2(54f, -8f);

        Image leftImg = leftGo.AddComponent<Image>();
        leftImg.color = new Color(0.24f, 0.30f, 0.40f, 0.95f);
        leftImg.raycastTarget = true;
        Button leftBtn = leftGo.AddComponent<Button>();
        leftBtn.targetGraphic = leftImg;

        GameObject leftTextGo = new GameObject("Text");
        leftTextGo.transform.SetParent(leftGo.transform, false);
        RectTransform leftTextRt = leftTextGo.AddComponent<RectTransform>();
        leftTextRt.anchorMin = Vector2.zero;
        leftTextRt.anchorMax = Vector2.one;
        leftTextRt.sizeDelta = Vector2.zero;
        leftTextRt.pivot = new Vector2(0.5f, 0.5f);
        Text leftText = leftTextGo.AddComponent<Text>();
        if (font != null) leftText.font = font;
        leftText.text = "<";
        leftText.fontSize = 24;
        leftText.fontStyle = FontStyle.Bold;
        leftText.alignment = TextAnchor.MiddleCenter;
        leftText.color = Color.white;
        leftText.raycastTarget = false;

        // 3. Right button ">"
        GameObject rightGo = new GameObject("ZoomRightButton");
        rightGo.transform.SetParent(widgetGo.transform, false);
        RectTransform rightRt = rightGo.AddComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(1f, 0f);
        rightRt.anchorMax = new Vector2(1f, 1f);
        rightRt.pivot = new Vector2(1f, 0.5f);
        rightRt.anchoredPosition = new Vector2(-5f, 0f);
        rightRt.sizeDelta = new Vector2(54f, -8f);

        Image rightImg = rightGo.AddComponent<Image>();
        rightImg.color = new Color(0.24f, 0.30f, 0.40f, 0.95f);
        rightImg.raycastTarget = true;
        Button rightBtn = rightGo.AddComponent<Button>();
        rightBtn.targetGraphic = rightImg;

        GameObject rightTextGo = new GameObject("Text");
        rightTextGo.transform.SetParent(rightGo.transform, false);
        RectTransform rightTextRt = rightTextGo.AddComponent<RectTransform>();
        rightTextRt.anchorMin = Vector2.zero;
        rightTextRt.anchorMax = Vector2.one;
        rightTextRt.sizeDelta = Vector2.zero;
        rightTextRt.pivot = new Vector2(0.5f, 0.5f);
        Text rightText = rightTextGo.AddComponent<Text>();
        if (font != null) rightText.font = font;
        rightText.text = ">";
        rightText.fontSize = 24;
        rightText.fontStyle = FontStyle.Bold;
        rightText.alignment = TextAnchor.MiddleCenter;
        rightText.color = Color.white;
        rightText.raycastTarget = false;

        CameraZoomUI zoomUI = widgetGo.AddComponent<CameraZoomUI>();
        zoomUI.FindComponents();
        zoomUI.UpdateUI(1);

        return widgetGo;
    }
}

public class ZoomPointerTrigger : MonoBehaviour, IPointerClickHandler
{
    public System.Action onClick;
    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke();
    }
}
