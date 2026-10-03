using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateModernUISprites
{
    private const string TargetFolder = "Assets/Resources/UI";

    [MenuItem("RaceTruck/UI/Сгенерировать спрайты Modern UI", false, 50)]
    public static void GenerateAllSprites()
    {
        if (!Directory.Exists(TargetFolder))
        {
            Directory.CreateDirectory(TargetFolder);
        }

        // 1. UI_Card_Completed (Dark slate with amber gold border)
        GenerateRoundedBox(
            "UI_Card_Completed",
            128, 128, 22f, 3.5f,
            new Color32(35, 45, 61, 255),  // top fill #232d3d
            new Color32(21, 27, 36, 255),  // bottom fill #151b24
            new Color32(245, 158, 11, 255), // border #f59e0b
            34
        );

        // 2. UI_Card_Active (Vibrant Emerald with mint border)
        GenerateRoundedBox(
            "UI_Card_Active",
            128, 128, 22f, 4.0f,
            new Color32(16, 185, 129, 255), // top fill #10b981
            new Color32(4, 120, 87, 255),   // bottom fill #047857
            new Color32(52, 211, 153, 255), // border #34d399
            34
        );

        // 3. UI_Card_Locked (Dark graphite with subtle slate border)
        GenerateRoundedBox(
            "UI_Card_Locked",
            128, 128, 22f, 2.5f,
            new Color32(20, 24, 32, 255),  // top fill #141820
            new Color32(12, 15, 20, 255),  // bottom fill #0c0f14
            new Color32(30, 41, 59, 255),  // border #1e293b
            34
        );

        // 4. UI_Btn_DarkPill (Dark slate button for Back / Language)
        GenerateRoundedBox(
            "UI_Btn_DarkPill",
            96, 96, 18f, 2.5f,
            new Color32(30, 38, 51, 255),  // #1e2633
            new Color32(24, 30, 40, 255),
            new Color32(51, 65, 85, 255),  // #334155
            26
        );

        // 5. UI_Badge_Play (Dark green pill for PLAY label)
        GenerateRoundedBox(
            "UI_Badge_Play",
            96, 48, 16f, 1.5f,
            new Color32(6, 78, 59, 255),   // #064e3b
            new Color32(6, 78, 59, 255),
            new Color32(10, 99, 76, 255),
            20
        );

        // 6. UI_Panel_Modern (Dark frame background)
        GenerateRoundedBox(
            "UI_Panel_Modern",
            128, 128, 24f, 2.5f,
            new Color32(19, 25, 34, 255),  // #131922
            new Color32(14, 18, 25, 255),  // #0e1219
            new Color32(37, 49, 65, 255),  // #253141
            32
        );

        AssetDatabase.Refresh();
        Debug.Log("<color=#4AE371><b>[GenerateModernUISprites] Все спрайты успешно созданы и настроены!</b></color>");
    }

    private static void GenerateRoundedBox(
        string name,
        int width,
        int height,
        float radius,
        float borderThickness,
        Color32 topFill,
        Color32 bottomFill,
        Color32 borderColor,
        int sliceBorder)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];

        float cx = width * 0.5f;
        float cy = height * 0.5f;
        float halfW = width * 0.5f - 1.5f;
        float halfH = height * 0.5f - 1.5f;

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            Color32 fillColor = Color32.Lerp(bottomFill, topFill, v);

            for (int x = 0; x < width; x++)
            {
                // Signed distance to rounded rectangle
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - cx) - (halfW - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - cy) - (halfH - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;

                int idx = y * width + x;

                if (dist > 0.5f)
                {
                    pixels[idx] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    // Outer edge anti-aliasing
                    float outerAlpha = Mathf.Clamp01(0.5f - dist);

                    if (dist > -borderThickness + 0.5f)
                    {
                        // On border
                        float borderAlpha = outerAlpha;
                        pixels[idx] = new Color32(
                            borderColor.r,
                            borderColor.g,
                            borderColor.b,
                            (byte)(borderColor.a * borderAlpha)
                        );
                    }
                    else if (dist > -borderThickness - 0.5f)
                    {
                        // Blend between border and inner fill
                        float t = Mathf.Clamp01((-borderThickness + 0.5f - dist));
                        Color32 blended = Color32.Lerp(borderColor, fillColor, t);
                        pixels[idx] = new Color32(
                            blended.r,
                            blended.g,
                            blended.b,
                            (byte)(blended.a * outerAlpha)
                        );
                    }
                    else
                    {
                        // Inside fill
                        pixels[idx] = new Color32(
                            fillColor.r,
                            fillColor.g,
                            fillColor.b,
                            (byte)(fillColor.a * outerAlpha)
                        );
                    }
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        string pngPath = Path.Combine(TargetFolder, name + ".png");
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        // Configure Sprite importer with 9-slice borders
        string assetPath = TargetFolder + "/" + name + ".png";
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
