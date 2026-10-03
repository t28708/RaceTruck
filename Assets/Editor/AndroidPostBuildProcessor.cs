using System;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;

public class AndroidPostBuildProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string basePath)
    {
        Debug.Log($"<color=#33ccff>[AndroidPostBuildProcessor] Checking Android manifests in: {basePath}</color>");

        string[] manifestPaths = new string[]
        {
            Path.Combine(basePath, "src/main/AndroidManifest.xml"),
            Path.Combine(basePath, "../launcher/src/main/AndroidManifest.xml")
        };

        foreach (string manifestPath in manifestPaths)
        {
            if (!File.Exists(manifestPath)) continue;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(manifestPath);

                XmlNode manifestNode = doc.SelectSingleNode("/manifest");
                if (manifestNode == null) continue;

                bool modified = false;

                // Strip any uses-permission nodes that grant internet, network, ad_id or storage
                XmlNodeList permissionNodes = doc.SelectNodes("//uses-permission");
                if (permissionNodes != null)
                {
                    foreach (XmlNode pNode in permissionNodes)
                    {
                        var nameAttr = pNode.Attributes?["android:name"];
                        var toolsNodeAttr = pNode.Attributes?["tools:node"];

                        // If it's not explicitly a tools:node="remove", strip it
                        if (toolsNodeAttr == null || toolsNodeAttr.Value != "remove")
                        {
                            pNode.ParentNode?.RemoveChild(pNode);
                            modified = true;
                            Debug.Log($"[AndroidPostBuildProcessor] Removed permission '{nameAttr?.Value}' from {Path.GetFileName(manifestPath)}");
                        }
                    }
                }

                if (modified)
                {
                    doc.Save(manifestPath);
                    Debug.Log($"<color=#4AE371>[AndroidPostBuildProcessor] Manifest sanitized successfully: {manifestPath}</color>");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AndroidPostBuildProcessor] Warning while checking {manifestPath}: {ex.Message}");
            }
        }
    }
}
#endif
