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
}
