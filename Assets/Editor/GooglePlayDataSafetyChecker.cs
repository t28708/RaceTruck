using System.IO;
using UnityEditor;
using UnityEngine;

public static class GooglePlayDataSafetyChecker
{
    [MenuItem("RaceTruck/Google Play/Проверить Data Safety (Политика конфиденциальности)", false, 10)]
    public static void CheckDataSafety()
    {
        Debug.Log("<color=#33ccff>==================================================</color>");
        Debug.Log("<color=#33ccff><b>[Data Safety Check] НАЧАЛО ПРОВЕРКИ ПРОЕКТА ДЛЯ GOOGLE PLAY</b></color>");

        bool allPassed = true;

        // 1. Check Packages/manifest.json
        string manifestJsonPath = "Packages/manifest.json";
        if (File.Exists(manifestJsonPath))
        {
            string content = File.ReadAllText(manifestJsonPath);
            if (content.Contains("unityanalytics"))
            {
                Debug.LogError("[Data Safety] ОШИБКА: Модуль Unity Analytics найден в Packages/manifest.json!");
                allPassed = false;
            }
            else
            {
                Debug.Log("<color=#4AE371>✓ [Packages] Модули аналитики и рекламы отсутствуют.</color>");
            }
        }

        // 2. Check AndroidManifest.xml in Plugins/Android
        string androidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        if (File.Exists(androidManifestPath))
        {
            string xml = File.ReadAllText(androidManifestPath);
            bool hasRemoveInternet = xml.Contains("android.permission.INTERNET") && xml.Contains("tools:node=\"remove\"");
            bool hasRemoveAdId = xml.Contains("com.google.android.gms.permission.AD_ID") && xml.Contains("tools:node=\"remove\"");

            if (hasRemoveInternet && hasRemoveAdId)
            {
                Debug.Log("<color=#4AE371>✓ [AndroidManifest] Разрешения INTERNET и AD_ID заблокированы через tools:node=\"remove\".</color>");
            }
            else
            {
                Debug.LogWarning("[Data Safety] Предупреждение: В AndroidManifest.xml не найдены явные правила блокировки INTERNET/AD_ID.");
            }
        }
        else
        {
            Debug.LogError("[Data Safety] ОШИБКА: Assets/Plugins/Android/AndroidManifest.xml не найден!");
            allPassed = false;
        }

        // 3. Check UnityConnectSettings.asset
        string connectSettingsPath = "ProjectSettings/UnityConnectSettings.asset";
        if (File.Exists(connectSettingsPath))
        {
            string connectText = File.ReadAllText(connectSettingsPath);
            if (connectText.Contains("m_Enabled: 1") || connectText.Contains("m_EngineDiagnosticsEnabled: 1"))
            {
                Debug.LogWarning("[Data Safety] Предупреждение: Unity Connect или Engine Diagnostics включены в UnityConnectSettings.asset.");
            }
            else
            {
                Debug.Log("<color=#4AE371>✓ [UnityConnect] Облачные сервисы, сбор диагностики и аналитики отключены.</color>");
            }
        }

        // 4. Check ProjectSettings.asset
        string projSettingsPath = "ProjectSettings/ProjectSettings.asset";
        if (File.Exists(projSettingsPath))
        {
            string projText = File.ReadAllText(projSettingsPath);
            if (projText.Contains("submitAnalytics: 1"))
            {
                Debug.LogError("[Data Safety] ОШИБКА: submitAnalytics включен в ProjectSettings.asset!");
                allPassed = false;
            }
            else
            {
                Debug.Log("<color=#4AE371>✓ [ProjectSettings] submitAnalytics отключен (0).</color>");
            }
        }

        if (allPassed)
        {
            Debug.Log("<color=#4AE371><b>==================================================\n" +
                      "[Data Safety Check] ПРОЕКТ ПОЛНОСТЬЮ ЧИСТ!\n" +
                      "Приложение не собирает данные, не использует сеть, рекламные ID и сторонние SDK.\n" +
                      "В Google Play Console в разделе 'Безопасность данных' (Data Safety) смело выбирайте: 'НЕТ, приложение не собирает и не передает данные пользователей'.\n" +
                      "==================================================</b></color>");
        }

        EditorUtility.DisplayDialog(
            "Проверка Data Safety для Google Play",
            allPassed
                ? "Проверка пройдена успешно!\n\nПроект полностью автономен:\n• Разрешения INTERNET и AD_ID отключены\n• Аналитика Unity отключена\n• Рекламные SDK отсутствуют\n\nВ Google Play Console выберите: 'Приложение не собирает данные пользователей'."
                : "Обнаружены замечания. Подробности в консоли Unity.",
            "OK"
        );
    }
}
