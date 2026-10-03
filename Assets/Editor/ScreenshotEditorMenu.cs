using System.IO;
using UnityEditor;
using UnityEngine;

public static class ScreenshotEditorMenu
{
    [MenuItem("RaceTruck/Скриншоты/Открыть папку со скриншотами", false, 10)]
    public static void OpenScreenshotsFolder()
    {
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        EditorUtility.RevealInFinder(folder);
    }
}
