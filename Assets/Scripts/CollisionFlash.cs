using System.Collections;
using UnityEngine;

/// <summary>
/// Fully standalone collision flash: no singleton dependency.
/// Call CollisionFlash.Spawn(worldPos) from anywhere — works even if TruckCrashEffect.Instance is null.
/// </summary>
public class CollisionFlash : MonoBehaviour
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Spawn a collision explosion at the given world position.</summary>
    public static void Spawn(Vector2 worldPos)
    {
        GameObject runner = new GameObject("CollisionFlash_Runner");
        DontDestroyOnLoad(runner);
        runner.AddComponent<CollisionFlash>().StartExplosion(worldPos);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static float s_lastSpawnTime = -99f;
    private const  float CooldownSec     = 0.35f;

    private void StartExplosion(Vector2 worldPos)
    {
        // Rate-limit: max one burst every CooldownSec seconds
        if (Time.time - s_lastSpawnTime < CooldownSec)
        {
            Destroy(gameObject);
            return;
        }
        s_lastSpawnTime = Time.time;
        StartCoroutine(RunExplosion(worldPos));
    }

    private IEnumerator RunExplosion(Vector2 worldPos)
    {
        const float dur     = 0.55f;
        const int   sortTop = 500; // well above trucks (8-10), UI canvas (100)

        // ── Build layers ──────────────────────────────────────────────────────

        // 1. Strong red impact circle / glow (rich vivid red, higher opacity)
        GameObject glowGo  = MakeSR("CF_Glow",  worldPos, CircleSpr(64, new Color(1f, 0.10f, 0.05f, 0.88f), new Color(0.9f, 0.05f, 0f, 0.35f)), sortTop);
        glowGo.transform.localScale = Vector3.one * 0.05f;

        // 2. Spiky orange burst — 14 spikes
        GameObject spikeGo = MakeSR("CF_Spike", worldPos, SpikeSpr(128, 14, new Color(1f, 0.5f, 0f, 1f)),  sortTop + 1);
        spikeGo.transform.localScale = Vector3.one * 0.05f;

        // 3. Hot white-yellow core
        GameObject coreGo  = MakeSR("CF_Core",  worldPos, CircleSpr(32, Color.white, new Color(1f, 0.9f, 0.3f, 0f)), sortTop + 2);
        coreGo.transform.localScale = Vector3.one * 0.05f;

        // 4. Expanding shockwave ring — strong fiery red
        GameObject ringGo  = MakeSR("CF_Ring",  worldPos, RingSpr(96, 0.5f, new Color(1f, 0.08f, 0.02f, 0.95f)), sortTop);
        ringGo.transform.localScale = Vector3.one * 0.05f;

        // 5. Flying sparks
        const int N = 12;
        GameObject[] sparks = new GameObject[N];
        Vector2[]    dirs   = new Vector2[N];
        float[]      spds   = new float[N];
        for (int i = 0; i < N; i++)
        {
            float a  = Random.Range(0f, 2f * Mathf.PI);
            dirs[i]  = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            spds[i]  = Random.Range(1.0f, 3.0f);
            Color sc = (Random.value > 0.5f)
                ? new Color(1f,0.95f,0.2f,1f)
                : new Color(1f,0.4f,0.05f,1f);
            sparks[i] = MakeSR("CF_Spark" + i, worldPos, CircleSpr(16, sc, new Color(1f,0.1f,0f,0f)), sortTop + 3);
            sparks[i].transform.localScale = Vector3.one * Random.Range(0.07f, 0.16f);
        }

        // ── Animate ───────────────────────────────────────────────────────────

        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / dur;
            float ease  = 1f - (1f - t) * (1f - t); // ease-out quad

            // Red circle glow — bold expansion and smooth fade
            if (glowGo) {
                glowGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 2.4f, ease);
                Alpha(glowGo, Mathf.Lerp(0.88f, 0f, t));
            }
            // Spike burst
            if (spikeGo) {
                spikeGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 1.3f, ease);
                Alpha(spikeGo, Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.2f) / 0.8f)));
            }
            // Core — pops fast
            if (coreGo) {
                coreGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.8f, Mathf.Min(1f, ease * 3f));
                Alpha(coreGo, Mathf.Lerp(1f, 0f, Mathf.Min(1f, t * 3f)));
            }
            // Red Ring — flies outward
            if (ringGo) {
                ringGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 3.0f, ease);
                Alpha(ringGo, Mathf.Lerp(0.95f, 0f, t));
            }
            // Sparks
            for (int i = 0; i < N; i++) {
                if (!sparks[i]) continue;
                sparks[i].transform.position = (Vector3)(worldPos + dirs[i] * (spds[i] * elapsed));
                Alpha(sparks[i], Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.25f) / 0.75f)));
            }

            yield return null;
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        if (glowGo)  Destroy(glowGo);
        if (spikeGo) Destroy(spikeGo);
        if (coreGo)  Destroy(coreGo);
        if (ringGo)  Destroy(ringGo);
        for (int i = 0; i < N; i++) if (sparks[i]) Destroy(sparks[i]);
        Destroy(gameObject); // remove the runner itself
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void Alpha(GameObject go, float a)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr) { Color c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c; }
    }

    private static GameObject MakeSR(string name, Vector2 pos, Sprite spr, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.position = new Vector3(pos.x, pos.y, -1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = order;
        return go;
    }

    // ── Procedural textures ───────────────────────────────────────────────────

    private static Sprite CircleSpr(int sz, Color inner, Color outer)
    {
        Texture2D t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        Color[] px = new Color[sz * sz];
        float r = sz * 0.5f;
        Vector2 cen = new Vector2(r, r);
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), cen);
                float k = Mathf.Clamp01(d / r);
                Color col = Color.Lerp(inner, outer, k); col.a *= (1f - k);
                px[y * sz + x] = col;
            }
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0,0,sz,sz), new Vector2(0.5f,0.5f), 100f);
    }

    private static Sprite SpikeSpr(int sz, int spikes, Color col)
    {
        Texture2D t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        Color[] px = new Color[sz * sz];
        float r = sz * 0.5f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                Vector2 p = new Vector2(x - r, y - r);
                float dist  = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x);
                float spike = (Mathf.Cos(angle * spikes) + 1f) * 0.5f;
                float outerR = r * (0.4f + 0.6f * spike);
                Color c2 = col;
                c2.a = (dist < outerR) ? (1f - Mathf.Clamp01(dist / Mathf.Max(outerR, 0.001f))) : 0f;
                px[y * sz + x] = c2;
            }
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0,0,sz,sz), new Vector2(0.5f,0.5f), 100f);
    }

    private static Sprite RingSpr(int sz, float innerFrac, Color col)
    {
        Texture2D t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        Color[] px = new Color[sz * sz];
        float r = sz * 0.5f;
        Vector2 cen = new Vector2(r, r);
        float ri   = r * innerFrac;
        float mid  = ri + (r - ri) * 0.5f;
        float half = (r - ri) * 0.5f;
        for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float d    = Vector2.Distance(new Vector2(x, y), cen);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / Mathf.Max(half, 0.001f));
                Color c2 = col; c2.a = ring * col.a;
                px[y * sz + x] = c2;
            }
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0,0,sz,sz), new Vector2(0.5f,0.5f), 100f);
    }
}
