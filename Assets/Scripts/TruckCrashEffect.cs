using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TruckCrashEffect : MonoBehaviour
{
    public static TruckCrashEffect Instance { get; private set; }

    [Header("UI Feedback")]
    [SerializeField] private Text crashText;
    [SerializeField] private Image redFlashImage;

    [Header("Camera Shake")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeMagnitude = 0.08f;

    private Coroutine currentCrashRoutine;
    private AudioSource audioSource;
    private AudioClip impactAudioClip;
    private AudioClip jackknifeAudioClip;
    private float lastCrashTriggerTime = -1f;
    private const float MinCrashInterval = 0.30f;

    private void Awake()
    {
        Instance = this;
        if (shakeDuration > 0.15f) shakeDuration = 0.12f;
        if (shakeMagnitude > 0.10f) shakeMagnitude = 0.08f;
    }

    private void Start()
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        if (crashText == null)
        {
            GameObject go = GameObject.Find("CrashBannerText");
            if (go != null) crashText = go.GetComponent<Text>();
        }

        if (redFlashImage == null)
        {
            GameObject go = GameObject.Find("CrashFlashOverlay");
            if (go != null) redFlashImage = go.GetComponent<Image>();
        }

        if (crashText != null) crashText.gameObject.SetActive(false);
        if (redFlashImage != null) redFlashImage.gameObject.SetActive(false);

        EnsureAudio();
    }

    // ── Audio ─────────────────────────────────────────────────────────────────

    private void EnsureAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        if (impactAudioClip == null)
            impactAudioClip = Resources.Load<AudioClip>("Audio/Truck_Crash_Impact");
        if (jackknifeAudioClip == null)
            jackknifeAudioClip = Resources.Load<AudioClip>("Audio/Truck_Jackknife_Crunch");
        if (impactAudioClip == null)
            impactAudioClip = GenerateMetalImpactClip();
    }

    private static AudioClip GenerateMetalImpactClip()
    {
        int sampleRate = 22050;
        float duration = 0.22f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float decayFast = Mathf.Exp(-t * 35f);
            float decaySlow = Mathf.Exp(-t * 18f);
            float boom  = Mathf.Sin(2f * Mathf.PI * 75f  * t) * decaySlow * 0.4f;
            float clang = Mathf.Sin(2f * Mathf.PI * 220f * t) * decayFast * 0.25f;
            float noise = (Random.value * 2f - 1f) * decayFast * 0.2f;
            samples[i] = Mathf.Clamp(boom + clang + noise, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("MetalImpactThud", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ── Procedural sprite factories ───────────────────────────────────────────

    private static Sprite MakeCircleSprite(int size, Color inner, Color outer)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        float r = size * 0.5f;
        Vector2 center = new Vector2(r, r);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float t = Mathf.Clamp01(d / r);
                Color col = Color.Lerp(inner, outer, t);
                col.a *= (1f - t);
                cols[y * size + x] = col;
            }
        tex.SetPixels(cols); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite MakeSpikeSprite(int size, int spikes, Color col)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x - r, y - r);
                float dist = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x);
                float spike = (Mathf.Cos(angle * spikes) + 1f) * 0.5f;
                float outerR = r * (0.45f + 0.55f * spike);
                float t = Mathf.Clamp01(dist / Mathf.Max(outerR, 0.001f));
                Color c2 = col;
                c2.a = (dist < outerR) ? (1f - t) : 0f;
                cols[y * size + x] = c2;
            }
        tex.SetPixels(cols); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite MakeRingSprite(int size, float innerFrac, Color col)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        float r = size * 0.5f;
        Vector2 center = new Vector2(r, r);
        float innerR = r * innerFrac;
        float midR = innerR + (r - innerR) * 0.5f;
        float halfW = (r - innerR) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - midR) / Mathf.Max(halfW, 0.001f));
                Color c2 = col; c2.a = ring * col.a;
                cols[y * size + x] = c2;
            }
        tex.SetPixels(cols); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static GameObject MakeSR(string name, Vector2 pos, Sprite spr, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.position = new Vector3(pos.x, pos.y, -0.5f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.sortingOrder = order;
        return go;
    }

    // ── Impact burst (world-space explosion at contact point) ─────────────────

    public void SpawnImpactBurst(Vector2 worldPos)
    {
        StartCoroutine(AnimateExplosion(worldPos));
    }

    private IEnumerator AnimateExplosion(Vector2 worldPos)
    {
        const float totalDuration = 0.55f;
        const int sortBase = 30;

        // Layer 1 – outer strong red glow
        GameObject glowGo = MakeSR("FX_Glow", worldPos,
            MakeCircleSprite(64, new Color(1f, 0.10f, 0.05f, 0.88f), new Color(0.9f, 0.05f, 0f, 0.35f)), sortBase);
        glowGo.transform.localScale = Vector3.one * 0.05f;

        // Layer 2 – spiky orange burst (12 spikes)
        GameObject spikeGo = MakeSR("FX_Spike", worldPos,
            MakeSpikeSprite(128, 12, new Color(1f, 0.45f, 0f, 1f)), sortBase + 1);
        spikeGo.transform.localScale = Vector3.one * 0.05f;

        // Layer 3 – hot white-yellow core
        GameObject coreGo = MakeSR("FX_Core", worldPos,
            MakeCircleSprite(32, Color.white, new Color(1f, 0.9f, 0.3f, 0f)), sortBase + 2);
        coreGo.transform.localScale = Vector3.one * 0.05f;

        // Layer 4 – expanding ring (strong fiery red)
        GameObject ringGo = MakeSR("FX_Ring", worldPos,
            MakeRingSprite(96, 0.5f, new Color(1f, 0.08f, 0.02f, 0.95f)), sortBase);
        ringGo.transform.localScale = Vector3.one * 0.05f;

        // Layer 5 – flying sparks
        const int numSparks = 10;
        GameObject[] sparks    = new GameObject[numSparks];
        Vector2[]    sparkDirs = new Vector2[numSparks];
        float[]      sparkSpd  = new float[numSparks];
        for (int i = 0; i < numSparks; i++)
        {
            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            sparkDirs[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            sparkSpd[i]  = Random.Range(1.2f, 2.8f);
            Color sparkCol = (Random.value > 0.5f)
                ? new Color(1f, 0.9f, 0.2f, 1f)
                : new Color(1f, 0.4f, 0.05f, 1f);
            sparks[i] = MakeSR("FX_Spark" + i, worldPos,
                MakeCircleSprite(16, sparkCol, new Color(1f, 0.2f, 0f, 0f)), sortBase + 3);
            sparks[i].transform.localScale = Vector3.one * Random.Range(0.06f, 0.13f);
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / totalDuration;
            float tEase = 1f - (1f - t) * (1f - t);  // ease-out quad

            // Glow: expand fast → fade
            if (glowGo)
            {
                glowGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 2.0f, tEase);
                SetAlpha(glowGo, Mathf.Lerp(0.55f, 0f, Mathf.Min(1f, t * 1.2f)));
            }
            // Spike burst: expand → fade after 25%
            if (spikeGo)
            {
                spikeGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 1.2f, tEase);
                SetAlpha(spikeGo, Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.25f) / 0.75f)));
            }
            // Core: pops fast → fades fast
            if (coreGo)
            {
                coreGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.7f, Mathf.Min(1f, tEase * 2.5f));
                SetAlpha(coreGo, Mathf.Lerp(1f, 0f, Mathf.Min(1f, t * 2.5f)));
            }
            // Ring: expands outward → fades
            if (ringGo)
            {
                ringGo.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, 2.5f, tEase);
                SetAlpha(ringGo, Mathf.Lerp(0.7f, 0f, t));
            }
            // Sparks: fly outward → fade after 30%
            for (int i = 0; i < numSparks; i++)
            {
                if (sparks[i] == null) continue;
                sparks[i].transform.position =
                    (Vector3)(worldPos + sparkDirs[i] * (sparkSpd[i] * elapsed)) + Vector3.back * 0.01f;
                SetAlpha(sparks[i], Mathf.Lerp(1f, 0f, Mathf.Max(0f, (t - 0.3f) / 0.7f)));
            }

            yield return null;
        }

        // Cleanup
        if (glowGo)  Destroy(glowGo);
        if (spikeGo) Destroy(spikeGo);
        if (coreGo)  Destroy(coreGo);
        if (ringGo)  Destroy(ringGo);
        for (int i = 0; i < numSparks; i++)
            if (sparks[i]) Destroy(sparks[i]);
    }

    private static void SetAlpha(GameObject go, float a)
    {
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        Color c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    // ── Public crash triggers ─────────────────────────────────────────────────

    public void TriggerJackknifeCrash(Vector2 contactWorldPos)
    {
        if (Time.time - lastCrashTriggerTime < MinCrashInterval) return;
        lastCrashTriggerTime = Time.time;

        if (TruckAudioController.Instance == null)
        {
            EnsureAudio();
            AudioClip clip = jackknifeAudioClip != null ? jackknifeAudioClip : impactAudioClip;
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip, 0.90f);
        }

        SpawnImpactBurst(contactWorldPos);

        if (currentCrashRoutine != null) StopCoroutine(currentCrashRoutine);
        currentCrashRoutine = StartCoroutine(JackknifeCrashSequence());
    }

    private IEnumerator JackknifeCrashSequence()
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.Shake(shakeDuration, shakeMagnitude);

        if (crashText != null)
        {
            crashText.gameObject.SetActive(true);
            crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
            crashText.text  = LocalizationManager.Get("JACKKNIFE_TITLE");
        }
        if (redFlashImage != null)
        {
            redFlashImage.gameObject.SetActive(true);
            redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f);
        }

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float pct = 1f - elapsed / shakeDuration;
            if (redFlashImage != null)
                redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f * pct);
            yield return null;
        }

        float textFade = 0.8f, textEl = 0f;
        while (textEl < textFade)
        {
            textEl += Time.deltaTime;
            if (crashText != null)
                crashText.color = new Color(1f, 0.2f, 0.2f, 1f - textEl / textFade);
            yield return null;
        }

        if (crashText     != null) { crashText.gameObject.SetActive(false);     crashText.color     = new Color(1f, 0.2f, 0.2f, 1f); }
        if (redFlashImage != null) { redFlashImage.gameObject.SetActive(false); }
        currentCrashRoutine = null;
    }

    public void TriggerCrash(string obstacleName, Vector2? contactPos = null, bool forwardImpact = true)
    {
        if (Time.time - lastCrashTriggerTime < MinCrashInterval) return;
        lastCrashTriggerTime = Time.time;

        if (TruckAudioController.Instance == null)
        {
            EnsureAudio();
            if (audioSource != null && impactAudioClip != null)
                audioSource.PlayOneShot(impactAudioClip, 0.90f);
        }

        if (contactPos.HasValue)
            SpawnImpactBurst(contactPos.Value);

        if (currentCrashRoutine != null) StopCoroutine(currentCrashRoutine);
        currentCrashRoutine = StartCoroutine(CrashSequence(obstacleName, forwardImpact));
    }

    private IEnumerator CrashSequence(string obstacleName, bool forwardImpact)
    {
        if (cameraFollow == null && Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
        if (cameraFollow != null)
            cameraFollow.Shake(shakeDuration, shakeMagnitude);

        if (crashText != null)
        {
            crashText.gameObject.SetActive(true);
            crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
            string hint = forwardImpact
                ? LocalizationManager.Get("CRASH_HINT_REVERSE")
                : LocalizationManager.Get("CRASH_HINT_FORWARD");
            crashText.text = $"{LocalizationManager.Get("CRASH_HIT_PREFIX")}{obstacleName.ToUpper()}! 💥\n<size=22><color=#FFFF66>{hint}</color></size>";
        }
        if (redFlashImage != null)
        {
            redFlashImage.gameObject.SetActive(true);
            redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f);
        }

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float pct = 1f - elapsed / shakeDuration;
            if (redFlashImage != null)
                redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f * pct);
            yield return null;
        }

        float textFade = 0.40f, textEl = 0f;
        while (textEl < textFade)
        {
            textEl += Time.deltaTime;
            if (crashText != null)
                crashText.color = new Color(1f, 0.2f, 0.2f, 1f - textEl / textFade);
            yield return null;
        }

        if (crashText     != null) { crashText.gameObject.SetActive(false);     crashText.color     = new Color(1f, 0.2f, 0.2f, 1f); }
        if (redFlashImage != null) { redFlashImage.gameObject.SetActive(false); }
        currentCrashRoutine = null;
    }
}
