using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Target Parking Slot Zone.
/// Highlights the target spot with bold yellow markings and detects when the truck is successfully parked.
/// </summary>
[ExecuteAlways]
public class ParkingTargetZone : MonoBehaviour
{
    public static List<ParkingTargetZone> AllZones { get; private set; } = new List<ParkingTargetZone>();
    public static ParkingTargetZone Instance { get; private set; }

    [Header("Detection Parameters")]
    [SerializeField] private Vector2 targetSlotSize = new Vector2(4.5f, 26.0f);
    [SerializeField] private float requiredStayTime = 0.2f; // seconds stationary inside slot

    private Transform tractorTr;
    private Transform trailerTr;
    private Rigidbody2D tractorRb;
    private Rigidbody2D trailerRb;
    private TruckController truckController;

    private float parkTimer = 0f;
    private bool isParkedSuccessfully = false;

    private GameObject winCanvasGo;

    public bool IsParkedSuccessfully => isParkedSuccessfully;

    private void OnEnable()
    {
        if (!AllZones.Contains(this))
        {
            AllZones.Add(this);
        }
    }

    private void OnDisable()
    {
        AllZones.Remove(this);
    }

    private void Awake()
    {
        Instance = this;
        if (!AllZones.Contains(this))
        {
            AllZones.Add(this);
        }
        requiredStayTime = 0.2f;
        targetSlotSize = new Vector2(4.5f, targetSlotSize.y < 25.0f ? 26.0f : targetSlotSize.y);
        UpdateVisualStripes();
    }

    private void OnValidate()
    {
        requiredStayTime = 0.2f;
        targetSlotSize = new Vector2(4.5f, targetSlotSize.y < 25.0f ? 26.0f : targetSlotSize.y);
        UpdateVisualStripes();
    }

    private void Start()
    {
        requiredStayTime = 0.2f;
        targetSlotSize = new Vector2(4.5f, targetSlotSize.y < 25.0f ? 26.0f : targetSlotSize.y);
        UpdateVisualStripes();
        FindTruckComponents();
    }

    private void UpdateVisualStripes()
    {
        float halfW = targetSlotSize.x * 0.5f;
        float halfL = targetSlotSize.y * 0.5f;

        Transform leftStripe = transform.Find("StallLine_Left");
        if (leftStripe != null)
        {
            leftStripe.localPosition = new Vector3(-halfW, 0f, 0f);
            SpriteRenderer sr = leftStripe.GetComponent<SpriteRenderer>();
            if (sr != null) sr.size = new Vector2(sr.size.x, targetSlotSize.y);
        }

        Transform rightStripe = transform.Find("StallLine_Right");
        if (rightStripe != null)
        {
            rightStripe.localPosition = new Vector3(halfW, 0f, 0f);
            SpriteRenderer sr = rightStripe.GetComponent<SpriteRenderer>();
            if (sr != null) sr.size = new Vector2(sr.size.x, targetSlotSize.y);
        }

        Transform backStripe = transform.Find("StallLine_Back");
        if (backStripe != null)
        {
            backStripe.localPosition = new Vector3(0f, halfL, 0f);
            SpriteRenderer sr = backStripe.GetComponent<SpriteRenderer>();
            if (sr != null) sr.size = new Vector2(sr.size.x, targetSlotSize.x);
        }
    }

    private void FindTruckComponents()
    {
        truckController = TruckController.Instance;
        if (truckController == null)
        {
            truckController = UnityEngine.Object.FindFirstObjectByType<TruckController>();
        }

        if (truckController != null)
        {
            tractorTr = truckController.transform;
            tractorRb = truckController.GetComponent<Rigidbody2D>();
            if (truckController.TrailerRb != null)
            {
                trailerTr = truckController.TrailerRb.transform;
                trailerRb = truckController.TrailerRb;
            }
        }
        else
        {
            TruckController[] allControllers = UnityEngine.Object.FindObjectsByType<TruckController>(FindObjectsSortMode.None);
            if (allControllers != null && allControllers.Length > 0)
            {
                truckController = allControllers[0];
                tractorTr = truckController.transform;
                tractorRb = truckController.GetComponent<Rigidbody2D>();
                if (truckController.TrailerRb != null)
                {
                    trailerTr = truckController.TrailerRb.transform;
                    trailerRb = truckController.TrailerRb;
                }
            }
        }
    }

    private void Update()
    {
        if (isParkedSuccessfully) return;

        if (tractorTr == null || trailerTr == null || truckController == null)
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
            parkTimer = 0f;
        }
    }

    public bool CheckTruckInsideAndStopped()
    {
        if (tractorTr == null || trailerTr == null || truckController == null)
        {
            FindTruckComponents();
            if (tractorTr == null || trailerTr == null) return false;
        }
        return IsTruckInsideTarget() && IsTruckStopped();
    }

    private bool IsTruckInsideTarget()
    {
        if (tractorTr == null || trailerTr == null) return false;

        float width = targetSlotSize.x > 0.1f ? targetSlotSize.x : 4.5f;
        float length = targetSlotSize.y > 0.1f ? targetSlotSize.y : 26.0f;

        float halfW = width * 0.5f;   // 2.25m
        float halfL = length * 0.5f;  // 13.0m

        // Yellow stripe thickness is 0.36m.
        // Usable inner parking boundary (strictly inside the lines, not touching or standing on them):
        float stripeThickness = 0.36f;
        float maxInnerX = halfW - (stripeThickness * 0.5f); // 2.25 - 0.18 = 2.07m
        float minInnerX = -maxInnerX;                       // -2.07m
        float maxInnerY = halfL - (stripeThickness * 0.5f); // 13.0 - 0.18 = 12.82m (back cap line)
        float minInnerY = -halfL;                           // -13.0m (front entrance threshold)

        // 1. Check Tractor (all 4 corners of tractor body must be strictly inside the inner boundaries)
        Vector2 tractorSize = new Vector2(2.55f, 8.2f);
        Vector2 tractorOffset = Vector2.zero;
        if (tractorTr.TryGetComponent<BoxCollider2D>(out var trCol))
        {
            tractorSize = trCol.size;
            tractorOffset = trCol.offset;
        }

        float trHalfW = tractorSize.x * 0.5f;
        float trHalfL = tractorSize.y * 0.5f;

        Vector3[] tractorCorners = new Vector3[]
        {
            tractorTr.TransformPoint(tractorOffset + new Vector2(-trHalfW, -trHalfL)),
            tractorTr.TransformPoint(tractorOffset + new Vector2(trHalfW, -trHalfL)),
            tractorTr.TransformPoint(tractorOffset + new Vector2(-trHalfW, trHalfL)),
            tractorTr.TransformPoint(tractorOffset + new Vector2(trHalfW, trHalfL))
        };

        foreach (var worldCorner in tractorCorners)
        {
            Vector3 localP = transform.InverseTransformPoint(worldCorner);
            if (localP.x < minInnerX || localP.x > maxInnerX || localP.y < minInnerY || localP.y > maxInnerY)
            {
                return false; // Part of the tractor is on or past the yellow lines / entrance
            }
        }

        // 2. Check Trailer (all 4 corners of trailer body must be strictly inside the inner boundaries)
        Vector2 trailerSize = new Vector2(2.58f, 15.9f);
        Vector2 trailerOffset = Vector2.zero;
        if (trailerTr.TryGetComponent<BoxCollider2D>(out var tlCol))
        {
            trailerSize = tlCol.size;
            trailerOffset = tlCol.offset;
        }

        float tlHalfW = trailerSize.x * 0.5f;
        float tlHalfL = trailerSize.y * 0.5f;

        Vector3[] trailerCorners = new Vector3[]
        {
            trailerTr.TransformPoint(trailerOffset + new Vector2(-tlHalfW, -tlHalfL)),
            trailerTr.TransformPoint(trailerOffset + new Vector2(tlHalfW, -tlHalfL)),
            trailerTr.TransformPoint(trailerOffset + new Vector2(-tlHalfW, tlHalfL)),
            trailerTr.TransformPoint(trailerOffset + new Vector2(tlHalfW, tlHalfL))
        };

        foreach (var worldCorner in trailerCorners)
        {
            Vector3 localP = transform.InverseTransformPoint(worldCorner);
            if (localP.x < minInnerX || localP.x > maxInnerX || localP.y < minInnerY || localP.y > maxInnerY)
            {
                return false; // Part of the trailer is on or past the yellow lines / entrance
            }
        }

        return true;
    }

    private bool IsTruckStopped()
    {
        float speed = 0f;
        if (truckController != null)
        {
            speed = Mathf.Abs(truckController.CurrentSpeed);
        }
        else if (tractorRb != null)
        {
            speed = tractorRb.linearVelocity.magnitude;
        }
        return speed < 0.3f; // genuine complete stop (< 1 km/h)
    }

    public void TriggerSuccess()
    {
        if (isParkedSuccessfully) return;
        isParkedSuccessfully = true;
        Debug.Log("<color=#55ff55>[ParkingTargetZone] 🏆 ЗАДАНИЕ ВЫПОЛНЕНО! Трак и прицеп полностью внутри целевой зоны!</color>");
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

        // Button 1: Continue Playing on THIS map
        GameObject continueGo = CreateModalButton("ContinueButton", boxGo.transform, new Vector2(0.06f, 0.12f), new Vector2(0.46f, 0.36f), new Color(0.15f, 0.72f, 0.32f, 1f), "▶ ПРОДОЛЖИТЬ", font, 24);
        continueGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            ContinueCurrentLevel();
        });

        // Button 2: Return to Level Select Menu
        GameObject menuGo = CreateModalButton("MenuButton", boxGo.transform, new Vector2(0.50f, 0.12f), new Vector2(0.94f, 0.36f), new Color(0.18f, 0.48f, 0.88f, 1f), "🗺 В ОКНО ВЫБОРА УРОВНЯ", font, 18);
        menuGo.GetComponent<Button>().onClick.AddListener(() =>
        {
            ReturnToLevelSelectMenu();
        });
    }

    private void ContinueCurrentLevel()
    {
        // Dismiss the victory modal so player can freely continue driving on the current map
        if (winCanvasGo != null)
        {
            winCanvasGo.SetActive(false);
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
