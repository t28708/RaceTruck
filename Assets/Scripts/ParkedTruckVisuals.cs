using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Automatically enriches parked trucks and trailers with realistic wheels,
/// wheel hubs, mudflaps, and soft ground contact shadows.
/// Works in both Editor mode and Play mode.
/// </summary>
[ExecuteAlways]
public class ParkedTruckVisuals : MonoBehaviour
{
    private static Sprite cachedTireSprite;
    private static Material cachedSpriteMaterial;

    private void Awake()
    {
        EnsureVisuals();
    }

    private void Start()
    {
        EnsureVisuals();
    }

    private void OnValidate()
    {
        EnsureVisuals();
    }

    [ContextMenu("Refresh Parked Truck Visuals")]
    public void EnsureVisuals()
    {
        // Load tire sprite if needed
        if (cachedTireSprite == null)
        {
            cachedTireSprite = Resources.Load<Sprite>("GeneratedSprites/Tire");
            if (cachedTireSprite == null)
            {
                // Fallback: search all loaded sprites
                Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in allSprites)
                {
                    if (s.name == "Tire")
                    {
                        cachedTireSprite = s;
                        break;
                    }
                }
            }
        }

        // 1. Scan for all parked stalls in scene
        GameObject[] stalls = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (var go in stalls)
        {
            if (go.name.StartsWith("Stall_Standard_Parked") || go.name.Contains("Parked"))
            {
                SetupParkedStall(go.transform);
            }
        }

        // 2. Also check any standalone obstacle Tractors/Trailers
        SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in allRenderers)
        {
            if (sr.gameObject.name == "Tractor" && sr.GetComponent<TruckController>() == null)
            {
                // Parked Tractor
                SetupTractorVisuals(sr.transform);
            }
            else if (sr.gameObject.name == "Trailer")
            {
                // Check if this trailer belongs to player
                bool isPlayerTrailer = false;
                TruckController playerController = TruckController.Instance ?? Object.FindFirstObjectByType<TruckController>();
                if (playerController != null && playerController.TrailerRb != null && playerController.TrailerRb.gameObject == sr.gameObject)
                {
                    isPlayerTrailer = true;
                }

                if (!isPlayerTrailer)
                {
                    SetupTrailerVisuals(sr.transform);
                }
            }
        }
    }

    private void SetupParkedStall(Transform stallTr)
    {
        for (int i = 0; i < stallTr.childCount; i++)
        {
            Transform child = stallTr.GetChild(i);
            if (child.name == "Tractor")
            {
                SetupTractorVisuals(child);
            }
            else if (child.name == "Trailer")
            {
                SetupTrailerVisuals(child);
            }
        }
    }

    public static void SetupTractorVisuals(Transform tractorTr)
    {
        if (tractorTr == null) return;

        Sprite tireSprite = GetTireSprite();
        Material mat = GetDefaultSpriteMaterial();

        // 6 Tractor Wheels:
        // Steer Axle: Y = 2.45, X = +/- 1.02
        // Drive Tandem 1: Y = -2.25, X = +/- 1.05
        // Drive Tandem 2: Y = -3.35, X = +/- 1.05
        EnsureWheel(tractorTr, "FrontLeftWheel", new Vector3(-1.02f, 2.45f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "FrontRightWheel", new Vector3(1.02f, 2.45f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearLeftWheel1", new Vector3(-1.05f, -2.25f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearLeftWheel2", new Vector3(-1.05f, -3.35f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearRightWheel1", new Vector3(1.05f, -2.25f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearRightWheel2", new Vector3(1.05f, -3.35f, 0f), tireSprite, mat, 7);

        // Ground shadow under tractor
        EnsureShadow(tractorTr, "TractorShadow", new Vector3(0f, 0f, 0f), new Vector2(2.8f, 8.8f), mat);
    }

    public static void SetupTrailerVisuals(Transform trailerTr)
    {
        if (trailerTr == null) return;

        Sprite tireSprite = GetTireSprite();
        Material mat = GetDefaultSpriteMaterial();

        // 4 Trailer Wheels:
        // Tandem 1: Y = -5.05, X = +/- 1.12
        // Tandem 2: Y = -6.15, X = +/- 1.12
        EnsureWheel(trailerTr, "TrailerRearLeft1", new Vector3(-1.12f, -5.05f, 0f), tireSprite, mat, 7);
        EnsureWheel(trailerTr, "TrailerRearLeft2", new Vector3(-1.12f, -6.15f, 0f), tireSprite, mat, 7);
        EnsureWheel(trailerTr, "TrailerRearRight1", new Vector3(1.12f, -5.05f, 0f), tireSprite, mat, 7);
        EnsureWheel(trailerTr, "TrailerRearRight2", new Vector3(1.12f, -6.15f, 0f), tireSprite, mat, 7);

        // Ground shadow under trailer
        EnsureShadow(trailerTr, "TrailerShadow", new Vector3(0f, 0f, 0f), new Vector2(2.8f, 16.5f), mat);
    }

    private static void EnsureWheel(Transform parent, string wheelName, Vector3 localPos, Sprite sprite, Material mat, int sortingOrder)
    {
        Transform wheelTr = parent.Find(wheelName);
        GameObject wheelGo;
        if (wheelTr == null)
        {
            wheelGo = new GameObject(wheelName);
            wheelGo.transform.SetParent(parent, false);
            wheelGo.transform.localPosition = localPos;
            wheelGo.transform.localRotation = Quaternion.identity;
            wheelGo.transform.localScale = Vector3.one;
        }
        else
        {
            wheelGo = wheelTr.gameObject;
            wheelTr.localPosition = localPos;
        }

        SpriteRenderer sr = wheelGo.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = wheelGo.AddComponent<SpriteRenderer>();
        }

        if (sprite != null && sr.sprite == null)
        {
            sr.sprite = sprite;
        }
        if (mat != null && (sr.sharedMaterial == null || sr.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
        {
            sr.sharedMaterial = mat;
        }
        sr.sortingOrder = sortingOrder;
        sr.color = Color.white;
    }

    private static void EnsureShadow(Transform parent, string shadowName, Vector3 localPos, Vector2 size, Material mat)
    {
        Transform shadowTr = parent.Find(shadowName);
        GameObject shadowGo;
        if (shadowTr == null)
        {
            shadowGo = new GameObject(shadowName);
            shadowGo.transform.SetParent(parent, false);
            shadowGo.transform.localPosition = localPos;
            shadowGo.transform.localRotation = Quaternion.identity;
            shadowGo.transform.localScale = Vector3.one;
        }
        else
        {
            shadowGo = shadowTr.gameObject;
            shadowTr.localPosition = localPos;
        }

        SpriteRenderer sr = shadowGo.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = shadowGo.AddComponent<SpriteRenderer>();
        }

        Sprite squareSprite = GetSquareSprite();
        if (squareSprite != null)
        {
            sr.sprite = squareSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
        }
        if (mat != null && (sr.sharedMaterial == null || sr.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
        {
            sr.sharedMaterial = mat;
        }
        sr.sortingOrder = 2; // soft shadow slightly above ground
        sr.color = new Color(0f, 0f, 0f, 0.28f);
    }

    private static Sprite GetTireSprite()
    {
        if (cachedTireSprite != null) return cachedTireSprite;
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "Tire")
            {
                cachedTireSprite = s;
                return s;
            }
        }
        return null;
    }

    private static Sprite GetSquareSprite()
    {
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "Square")
            {
                return s;
            }
        }
        return null;
    }

    private static Material GetDefaultSpriteMaterial()
    {
        if (cachedSpriteMaterial != null) return cachedSpriteMaterial;
        cachedSpriteMaterial = new Material(Shader.Find("Sprites/Default"));
        return cachedSpriteMaterial;
    }
}
