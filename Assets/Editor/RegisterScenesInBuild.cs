using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using UnityEditor.Build.Profile;
#endif

[InitializeOnLoad]
public static class RegisterScenesInBuild
{
    static RegisterScenesInBuild()
    {
        EditorApplication.delayCall += RegisterAllScenes;
    }

    public static void RegisterAllScenes()
    {
        ApplyPlayerSettings();

        List<string> sceneList = new List<string>();

        // 1. Main menu at index 0
        if (File.Exists("Assets/Scenes/MainMenu.unity"))
        {
            sceneList.Add("Assets/Scenes/MainMenu.unity");
        }
        else if (File.Exists("Assets/Scenes/SampleScene.unity"))
        {
            sceneList.Add("Assets/Scenes/SampleScene.unity");
        }

        // 2. Custom Maps
        string customMapsDir = "Assets/Scenes/CustomMaps";
        if (Directory.Exists(customMapsDir))
        {
            string[] files = Directory.GetFiles(customMapsDir, "*.unity");
            Array.Sort(files, (a, b) => MapSelectMenu.NaturalCompare(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b)));
            foreach (string file in files)
            {
                string norm = file.Replace("\\", "/");
                if (!sceneList.Contains(norm))
                {
                    sceneList.Add(norm);
                }
            }
        }

        // 3. Update global EditorBuildSettings.scenes
        List<EditorBuildSettingsScene> editorScenes = new List<EditorBuildSettingsScene>();
        foreach (string scenePath in sceneList)
        {
            editorScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }
        EditorBuildSettings.scenes = editorScenes.ToArray();

        // 4. Update Unity 6 BuildProfile if available
#if UNITY_6000_0_OR_NEWER
        try
        {
            BuildProfile activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                activeProfile.scenes = editorScenes.ToArray();
                EditorUtility.SetDirty(activeProfile);
                AssetDatabase.SaveAssetIfDirty(activeProfile);
            }
        }
        catch (Exception)
        {
        }
#endif
    }

    public static void ApplyPlayerSettings()
    {
        try
        {
            PlayerSettings.productName = "CDL TRUCK PARKING";
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            try
            {
                var pAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (pAssets != null && pAssets.Length > 0)
                {
                    SerializedObject so = new SerializedObject(pAssets[0]);
                    SerializedProperty submitProp = so.FindProperty("submitAnalytics");
                    if (submitProp != null && submitProp.boolValue)
                    {
                        submitProp.boolValue = false;
                        so.ApplyModifiedProperties();
                    }
                }
            }
            catch (Exception) {}

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/AppIcon.png");
            if (icon != null)
            {
#if UNITY_2021_2_OR_NEWER
                var unknownTarget = UnityEditor.Build.NamedBuildTarget.Unknown;
                int[] defSizes = PlayerSettings.GetIconSizes(unknownTarget, IconKind.Any);
                Texture2D[] defIcons = new Texture2D[defSizes != null && defSizes.Length > 0 ? defSizes.Length : 1];
                for (int i = 0; i < defIcons.Length; i++) defIcons[i] = icon;
                PlayerSettings.SetIcons(unknownTarget, defIcons, IconKind.Any);

                var androidTarget = UnityEditor.Build.NamedBuildTarget.Android;
                int[] androidSizes = PlayerSettings.GetIconSizes(androidTarget, IconKind.Any);
                Texture2D[] androidIcons = new Texture2D[androidSizes != null && androidSizes.Length > 0 ? androidSizes.Length : 1];
                for (int i = 0; i < androidIcons.Length; i++) androidIcons[i] = icon;
                PlayerSettings.SetIcons(androidTarget, androidIcons, IconKind.Any);

                var standaloneTarget = UnityEditor.Build.NamedBuildTarget.Standalone;
                int[] standaloneSizes = PlayerSettings.GetIconSizes(standaloneTarget, IconKind.Any);
                Texture2D[] standaloneIcons = new Texture2D[standaloneSizes != null && standaloneSizes.Length > 0 ? standaloneSizes.Length : 1];
                for (int i = 0; i < standaloneIcons.Length; i++) standaloneIcons[i] = icon;
                PlayerSettings.SetIcons(standaloneTarget, standaloneIcons, IconKind.Any);
#else
                int[] defSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Unknown);
                Texture2D[] defIcons = new Texture2D[defSizes != null && defSizes.Length > 0 ? defSizes.Length : 1];
                for (int i = 0; i < defIcons.Length; i++) defIcons[i] = icon;
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, defIcons);

                int[] androidSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android);
                Texture2D[] androidIcons = new Texture2D[androidSizes != null && androidSizes.Length > 0 ? androidSizes.Length : 1];
                for (int i = 0; i < androidIcons.Length; i++) androidIcons[i] = icon;
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, androidIcons);

                int[] standaloneSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
                Texture2D[] standaloneIcons = new Texture2D[standaloneSizes != null && standaloneSizes.Length > 0 ? standaloneSizes.Length : 1];
                for (int i = 0; i < standaloneIcons.Length; i++) standaloneIcons[i] = icon;
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, standaloneIcons);
#endif
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RegisterScenesInBuild] Could not set player settings: {ex.Message}");
        }
    }
}
