using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class TruckSimulatorSetup
{
    private const string SpritesDir = "Assets/GeneratedSprites";

    public static void RebuildControlsCanvasInActiveScene()
    {
        EnsureEventSystem();
        EnsureSpriteAssets(false);
        Sprite steeringWheelSprite = LoadSpriteSafe($"{SpritesDir}/SteeringWheelRealistic.png");
        if (steeringWheelSprite == null) steeringWheelSprite = LoadSpriteSafe($"{SpritesDir}/SteeringWheel.png");
        Sprite pedalGasSprite = LoadSpriteSafe($"{SpritesDir}/PedalGas.png");
        Sprite pedalBrakeSprite = LoadSpriteSafe($"{SpritesDir}/PedalBrake.png");

        GameObject oldCanvas = GameObject.Find("TruckControlsCanvas");
        if (oldCanvas != null) Object.DestroyImmediate(oldCanvas);

        GameObject canvasGo = CreateControlsCanvas(steeringWheelSprite, pedalGasSprite, pedalBrakeSprite);
        if (!EditorApplication.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }

    public static void EnsureEventSystem()
    {
        EventSystem es = Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
            Undo.RegisterCreatedObjectUndo(esGo, "Setup EventSystem");
        }
        else if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    public static GameObject CreateWheelChild(string name, Transform parent, Vector3 localPos, Sprite sprite)
    {
        GameObject wheel = new GameObject(name);
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = localPos;
        wheel.transform.localScale = Vector3.one;

        SpriteRenderer sr = wheel.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 6;
        return wheel;
    }

    public static GameObject CreateControlsCanvas(Sprite steeringWheelSprite, Sprite pedalGasSprite, Sprite pedalBrakeSprite)
    {
        GameObject canvasGo = new GameObject("TruckControlsCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Crash overlay image (red flash on collision)
        GameObject flashGo = new GameObject("CrashFlashOverlay");
        flashGo.transform.SetParent(canvasGo.transform, false);
        RectTransform flashRt = flashGo.AddComponent<RectTransform>();
        flashRt.anchorMin = Vector2.zero;
        flashRt.anchorMax = Vector2.one;
        flashRt.sizeDelta = Vector2.zero;
        Image flashImg = flashGo.AddComponent<Image>();
        flashImg.color = new Color(1f, 0.1f, 0.1f, 0f);
        flashImg.raycastTarget = false;
        flashGo.SetActive(false);

        // Steering Wheel (bottom-right corner)
        GameObject wheelGo = new GameObject("SteeringWheel");
        wheelGo.transform.SetParent(canvasGo.transform, false);

        RectTransform wheelRt = wheelGo.AddComponent<RectTransform>();
        wheelRt.anchorMin = new Vector2(1f, 0f);
        wheelRt.anchorMax = new Vector2(1f, 0f);
        wheelRt.pivot = new Vector2(0.5f, 0.5f);
        wheelRt.anchoredPosition = new Vector2(-270f, 270f);
        wheelRt.sizeDelta = new Vector2(480f, 480f);

        Image wheelImg = wheelGo.AddComponent<Image>();
        wheelImg.sprite = steeringWheelSprite;
        wheelImg.raycastTarget = true;
        wheelImg.preserveAspect = true;

        wheelGo.AddComponent<SteeringWheelUI>();

        // Pedals (bottom-left corner)
        // 1. Gas Pedal (Upper Left: 180 x 340)
        GameObject gasGo = new GameObject("Pedal_Gas");
        gasGo.transform.SetParent(canvasGo.transform, false);

        RectTransform gasRt = gasGo.AddComponent<RectTransform>();
        gasRt.anchorMin = new Vector2(0f, 0f);
        gasRt.anchorMax = new Vector2(0f, 0f);
        gasRt.pivot = new Vector2(0.5f, 0.5f);
        gasRt.anchoredPosition = new Vector2(170f, 490f);
        gasRt.sizeDelta = new Vector2(180f, 340f);

        Image gasImg = gasGo.AddComponent<Image>();
        gasImg.sprite = pedalGasSprite;
        gasImg.raycastTarget = true;
        gasImg.preserveAspect = true;
        gasImg.color = new Color(1f, 1f, 1f, 0.9f);

        PedalUI gasPedal = gasGo.AddComponent<PedalUI>();
        gasPedal.SetPedalType(PedalUI.PedalType.Gas);

        // 2. Brake / Reverse Pedal (Lower Left: 340 x 240, wide horizontal)
        GameObject brakeGo = new GameObject("Pedal_Brake");
        brakeGo.transform.SetParent(canvasGo.transform, false);

        RectTransform brakeRt = brakeGo.AddComponent<RectTransform>();
        brakeRt.anchorMin = new Vector2(0f, 0f);
        brakeRt.anchorMax = new Vector2(0f, 0f);
        brakeRt.pivot = new Vector2(0.5f, 0.5f);
        brakeRt.anchoredPosition = new Vector2(190f, 170f);
        brakeRt.sizeDelta = new Vector2(340f, 240f);

        Image brakeImg = brakeGo.AddComponent<Image>();
        brakeImg.sprite = pedalBrakeSprite;
        brakeImg.raycastTarget = true;
        brakeImg.preserveAspect = true;
        brakeImg.color = new Color(1f, 1f, 1f, 0.9f);

        PedalUI brakePedal = brakeGo.AddComponent<PedalUI>();
        brakePedal.SetPedalType(PedalUI.PedalType.BrakeReverse);

        // Dashboard HUD Text
        GameObject hudGo = new GameObject("TruckHUDText");
        hudGo.transform.SetParent(canvasGo.transform, false);

        RectTransform hudRt = hudGo.AddComponent<RectTransform>();
        hudRt.anchorMin = new Vector2(0.5f, 0.94f);
        hudRt.anchorMax = new Vector2(0.5f, 0.94f);
        hudRt.pivot = new Vector2(0.5f, 0.5f);
        hudRt.anchoredPosition = Vector2.zero;
        hudRt.sizeDelta = new Vector2(1200, 50);

        Text hudText = hudGo.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) hudText.font = font;
        hudText.fontSize = 22;
        hudText.alignment = TextAnchor.MiddleCenter;
        hudText.color = new Color(1f, 0.92f, 0.3f, 1f);
        hudText.text = "SPEED: 0 km/h | GEAR: [N] | РУЛЬ: 0° | СЦЕПКА: 0° | [C] КАМЕРА: СВЕРХУ";

        // Big Crash Banner Text
        GameObject crashTextGo = new GameObject("CrashBannerText");
        crashTextGo.transform.SetParent(canvasGo.transform, false);

        RectTransform crashRt = crashTextGo.AddComponent<RectTransform>();
        crashRt.anchorMin = new Vector2(0.5f, 0.65f);
        crashRt.anchorMax = new Vector2(0.5f, 0.65f);
        crashRt.pivot = new Vector2(0.5f, 0.5f);
        crashRt.anchoredPosition = Vector2.zero;
        crashRt.sizeDelta = new Vector2(950, 80);

        Text crashText = crashTextGo.AddComponent<Text>();
        if (font != null) crashText.font = font;
        crashText.fontSize = 38;
        crashText.fontStyle = FontStyle.Bold;
        crashText.alignment = TextAnchor.MiddleCenter;
        crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
        crashText.text = "💥 БУХ! ВРЕЗАЛСЯ В ПРЕПЯТСТВИЕ! 💥";
        crashTextGo.SetActive(false);

        // Map Switch Button in top-left corner
        GameObject switchBtnGo = new GameObject("MapSwitchButton");
        switchBtnGo.transform.SetParent(canvasGo.transform, false);

        RectTransform switchRt = switchBtnGo.AddComponent<RectTransform>();
        switchRt.anchorMin = new Vector2(0f, 1f);
        switchRt.anchorMax = new Vector2(0f, 1f);
        switchRt.pivot = new Vector2(0f, 1f);
        switchRt.anchoredPosition = new Vector2(25f, -25f);
        switchRt.sizeDelta = new Vector2(240f, 50f);

        Image switchImg = switchBtnGo.AddComponent<Image>();
        switchImg.color = new Color(0.12f, 0.45f, 0.85f, 1.0f);

        Button switchBtn = switchBtnGo.AddComponent<Button>();
        LevelSwitcher switcher = switchBtnGo.AddComponent<LevelSwitcher>();

        GameObject switchTextGo = new GameObject("Text");
        switchTextGo.transform.SetParent(switchBtnGo.transform, false);
        RectTransform textRt = switchTextGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;
        textRt.pivot = new Vector2(0.5f, 0.5f);

        Text switchText = switchTextGo.AddComponent<Text>();
        if (font != null) switchText.font = font;
        switchText.fontSize = 17;
        switchText.fontStyle = FontStyle.Bold;
        switchText.alignment = TextAnchor.MiddleCenter;
        switchText.color = Color.white;
        switchText.horizontalOverflow = HorizontalWrapMode.Overflow;
        switchText.verticalOverflow = VerticalWrapMode.Overflow;
        switchText.text = "🗺 КАРТЫ [ESC]";

        // Map selection menu & crash manager
        canvasGo.AddComponent<MapSelectMenu>();
        canvasGo.AddComponent<TruckCrashEffect>();

        // Camera Zoom Multiplier Widget (top-right: "< 1x >")
        CameraZoomUI.CreateZoomWidget(canvasGo);

        return canvasGo;
    }

    public static void EnsureSpriteAssets(bool forceOverwrite = false)
    {
        string fullDir = Path.Combine(Application.dataPath, "GeneratedSprites");
        if (!Directory.Exists(fullDir))
        {
            Directory.CreateDirectory(fullDir);
        }

        SaveAndImportPng($"{SpritesDir}/Tractor.png", GenerateTractorPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/Trailer.png", GenerateTrailerPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/Tire.png", GenerateTirePngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/SteeringWheel.png", GenerateSteeringWheelPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/TrafficCone.png", GenerateConePngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/ConcreteBarrier.png", GenerateBarrierPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/Barrel.png", GenerateBarrelPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/AsphaltGround.png", GenerateAsphaltPngBytes(), forceOverwrite);
        SaveAndImportPng($"{SpritesDir}/ImpactSpark.png", GenerateImpactSparkPngBytes(), forceOverwrite);
    }

    public static Sprite LoadSpriteSafe(string assetPath)
    {
        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp != null) return sp;

        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object obj in allAssets)
        {
            if (obj is Sprite s) return s;
        }

        Texture2D rawTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (rawTex != null)
        {
            return Sprite.Create(rawTex, new Rect(0, 0, rawTex.width, rawTex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        return null;
    }

    private static void SaveAndImportPng(string assetPath, byte[] pngBytes, bool forceOverwrite = false)
    {
        string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!File.Exists(fullPath) || forceOverwrite)
        {
            File.WriteAllBytes(fullPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
    }

    private static byte[] GenerateTractorPngBytes()
    {
        int w = 260, h = 850;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color cabColor = new Color(0.82f, 0.12f, 0.14f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = cabColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateTrailerPngBytes()
    {
        int w = 260, h = 1620;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color trailerColor = new Color(0.90f, 0.90f, 0.92f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = trailerColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateTirePngBytes()
    {
        int w = 45, h = 95;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color tireColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = tireColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateSteeringWheelPngBytes()
    {
        int w = 256, h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color wheelColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = wheelColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateConePngBytes()
    {
        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color coneColor = new Color(1f, 0.45f, 0.05f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = coneColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateBarrierPngBytes()
    {
        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color barrierColor = new Color(0.65f, 0.65f, 0.68f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = barrierColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateBarrelPngBytes()
    {
        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color barrelColor = new Color(0.9f, 0.5f, 0.1f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = barrelColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateAsphaltPngBytes()
    {
        int w = 64, h = 64;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color asphaltColor = new Color(0.20f, 0.20f, 0.22f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = asphaltColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }

    private static byte[] GenerateImpactSparkPngBytes()
    {
        int w = 32, h = 32;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] colors = new Color[w * h];
        Color sparkColor = new Color(1f, 0.9f, 0.3f, 1f);
        for (int i = 0; i < colors.Length; i++) colors[i] = sparkColor;
        tex.SetPixels(colors);
        tex.Apply();
        return tex.EncodeToPNG();
    }
}
