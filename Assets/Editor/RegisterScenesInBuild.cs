using System;
using System.Collections.Generic;
using System.Linq;
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

    [MenuItem("Tools/Register Scenes in Build Profiles")]
    public static void RegisterAllScenes()
    {
        string[] requiredScenes = new string[]
        {
            "Assets/Scenes/SampleScene.unity",
            "Assets/Scenes/Level2_AlleyDock.unity"
        };

        // 1. Update global EditorBuildSettings.scenes
        List<EditorBuildSettingsScene> editorScenes = new List<EditorBuildSettingsScene>();
        foreach (string scenePath in requiredScenes)
        {
            editorScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        }
        EditorBuildSettings.scenes = editorScenes.ToArray();

        // 2. Update Unity 6 BuildProfile if available
#if UNITY_6000_0_OR_NEWER
        try
        {
            BuildProfile activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                var profileScenes = activeProfile.scenes != null ? activeProfile.scenes.ToList() : new List<EditorBuildSettingsScene>();
                foreach (string scenePath in requiredScenes)
                {
                    if (!profileScenes.Any(s => s.path == scenePath))
                    {
                        profileScenes.Add(new EditorBuildSettingsScene(scenePath, true));
                    }
                }
                activeProfile.scenes = profileScenes.ToArray();
                EditorUtility.SetDirty(activeProfile);
                AssetDatabase.SaveAssetIfDirty(activeProfile);
                Debug.Log($"[RegisterScenesInBuild] Registered scenes in active BuildProfile: {activeProfile.name}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RegisterScenesInBuild] BuildProfile registration exception: {ex.Message}");
        }
#endif

        AssetDatabase.SaveAssets();
        Debug.Log("[RegisterScenesInBuild] Scenes registered in EditorBuildSettings & BuildProfile successfully!");
    }
}
