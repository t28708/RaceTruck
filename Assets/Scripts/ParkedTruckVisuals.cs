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
    private static Sprite cachedTrailerSprite;
    private static Sprite cachedTractorSprite;
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
        // 1. Scan for all parked stalls in scene
        GameObject[] stalls = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (var go in stalls)
        {
            if (go.name.StartsWith("Stall_Standard_Parked") || go.name.StartsWith("Stall_Narrow_Parked") || go.name.Contains("Parked"))
            {
                SetupParkedStall(go.transform);
            }
        }

        // 2. Also check any standalone obstacle Tractors/Trailers
        SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var sr in allRenderers)
        {
            string n = sr.gameObject.name;
            if ((n == "Tractor" || n == "Preview_Tractor") && sr.GetComponent<TruckController>() == null)
            {
                if (n == "Preview_Tractor") sr.gameObject.name = "Tractor";
                SetupTractorVisuals(sr.transform);
            }
            else if (n == "Trailer" || n == "Preview_Trailer")
            {
                if (n == "Preview_Trailer") sr.gameObject.name = "Trailer";
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
            if (child.name == "Tractor" || child.name == "Preview_Tractor")
            {
                if (child.name == "Preview_Tractor") child.name = "Tractor";
                SetupTractorVisuals(child);
            }
            else if (child.name == "Trailer" || child.name == "Preview_Trailer")
            {
                if (child.name == "Preview_Trailer") child.name = "Trailer";
                SetupTrailerVisuals(child);
            }
        }
    }

    public static void SetupTractorVisuals(Transform tractorTr)
    {
        if (tractorTr == null) return;

        SpriteRenderer sr = tractorTr.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (sr.sprite == null || sr.sprite.name == "Tractor")
            {
                sr.sprite = GetTractorSprite();
            }
            if (sr.sprite != null && sr.sprite.name.Contains("HD"))
            {
                sr.color = Color.white;
            }
            else if (sr.color.a < 0.95f)
            {
                sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1.0f);
            }
            sr.sortingOrder = 8;
        }

        BoxCollider2D col = tractorTr.GetComponent<BoxCollider2D>();
        if (col == null) col = tractorTr.gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(2.55f, 8.2f);
        col.offset = Vector2.zero;
        col.isTrigger = true;

        Sprite tireSprite = GetTireSprite();
        Material mat = GetDefaultSpriteMaterial();

        // 6 Tractor Wheels matching HD model wheel arches:
        // Steer Axle: Y = 2.80, X = +/- 1.08
        // Drive Tandem 1: Y = -2.25, X = +/- 1.08
        // Drive Tandem 2: Y = -3.35, X = +/- 1.08
        EnsureWheel(tractorTr, "FrontLeftWheel", new Vector3(-1.08f, 2.80f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "FrontRightWheel", new Vector3(1.08f, 2.80f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearLeftWheel1", new Vector3(-1.08f, -2.25f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearLeftWheel2", new Vector3(-1.08f, -3.35f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearRightWheel1", new Vector3(1.08f, -2.25f, 0f), tireSprite, mat, 7);
        EnsureWheel(tractorTr, "RearRightWheel2", new Vector3(1.08f, -3.35f, 0f), tireSprite, mat, 7);

        // Ground shadow under tractor
        EnsureShadow(tractorTr, "TractorShadow", new Vector3(0f, 0f, 0f), new Vector2(3.1f, 8.8f), mat);
    }

    public static void SetupTrailerVisuals(Transform trailerTr)
    {
        if (trailerTr == null) return;

        SpriteRenderer sr = trailerTr.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (sr.sprite == null)
            {
                sr.sprite = GetTrailerSprite();
            }
            // Always ensure pure crisp white for trailer (never tinted blue)
            sr.color = Color.white;
            sr.sortingOrder = 10;
        }

        BoxCollider2D col = trailerTr.GetComponent<BoxCollider2D>();
        if (col == null) col = trailerTr.gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(2.58f, 15.9f);
        col.offset = Vector2.zero;
        col.isTrigger = true;

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

    public static Sprite GetTireSprite()
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

    public static Sprite GetTrailerSprite()
    {
        if (cachedTrailerSprite != null) return cachedTrailerSprite;
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s.name == "Trailer")
            {
                cachedTrailerSprite = s;
                return s;
            }
        }
        return null;
    }

    public static Sprite GetTractorSprite()
    {
        if (cachedTractorSprite != null) return cachedTractorSprite;
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        // 1. Prefer Tractor_Blue_HD (gorgeous detailed blue HD model)
        foreach (var s in allSprites)
        {
            if (s.name == "Tractor_Blue_HD")
            {
                cachedTractorSprite = s;
                return s;
            }
        }
        // 2. Fallback to standard Tractor sprite
        foreach (var s in allSprites)
        {
            if (s.name == "Tractor")
            {
                cachedTractorSprite = s;
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
