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
    private Sprite impactSparkSprite;
    private float lastCrashTriggerTime = -1f;
    private const float MinCrashInterval = 0.45f;

    private void Awake()
    {
        Instance = this;
        // Enforce minimal gentle shake settings even if older scene serialized values exist
        if (shakeDuration > 0.15f) shakeDuration = 0.12f;
        if (shakeMagnitude > 0.10f) shakeMagnitude = 0.08f;
    }

    private void Start()
    {
        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
        }

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

    private void EnsureAudio()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (impactAudioClip == null)
        {
            impactAudioClip = GenerateMetalImpactClip();
        }
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
            // Softer, damped bump thud instead of harsh loud boom
            float decayFast = Mathf.Exp(-t * 35f);
            float decaySlow = Mathf.Exp(-t * 18f);
            float boom = Mathf.Sin(2f * Mathf.PI * 75f * t) * decaySlow * 0.4f;
            float clang = Mathf.Sin(2f * Mathf.PI * 220f * t) * decayFast * 0.25f;
            float noise = (Random.value * 2f - 1f) * decayFast * 0.2f;

            samples[i] = Mathf.Clamp(boom + clang + noise, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("MetalImpactThud", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private Sprite GetOrCreateImpactSprite()
    {
        if (impactSparkSprite != null) return impactSparkSprite;

        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(32f, 32f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float dist = Vector2.Distance(pos, center);
                Color col = Color.clear;

                if (dist <= 28f)
                {
                    float factor = 1f - (dist / 28f);
                    col = Color.Lerp(new Color(1f, 0.45f, 0.05f, factor), Color.white, factor * factor);

                    // 8-point explosive star rays
                    float dx = Mathf.Abs(x - 32);
                    float dy = Mathf.Abs(y - 32);
                    if (dx <= 2 || dy <= 2 || Mathf.Abs(dx - dy) <= 2)
                    {
                        col = Color.Lerp(col, Color.white, 0.85f);
                    }
                }
                cols[y * size + x] = col;
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        impactSparkSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return impactSparkSprite;
    }

    public void SpawnImpactBurst(Vector2 worldPos)
    {
        GameObject burstGo = new GameObject("ImpactBurst");
        burstGo.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        SpriteRenderer sr = burstGo.AddComponent<SpriteRenderer>();
        sr.sprite = GetOrCreateImpactSprite();
        sr.sortingOrder = 25; // Render above tractor and trailer

        StartCoroutine(AnimateBurst(burstGo, sr));
    }

    private IEnumerator AnimateBurst(GameObject burstGo, SpriteRenderer sr)
    {
        float duration = 0.2f;
        float elapsed = 0f;
        Vector3 initialScale = Vector3.one * 0.4f;
        Vector3 targetScale = Vector3.one * 1.2f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (burstGo != null)
            {
                burstGo.transform.localScale = Vector3.Lerp(initialScale, targetScale, t);
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = Mathf.Lerp(0.8f, 0f, t * t);
                    sr.color = c;
                }
            }
            yield return null;
        }

        if (burstGo != null)
        {
            Destroy(burstGo);
        }
    }

    public void TriggerJackknifeCrash(Vector2 contactWorldPos)
    {
        if (Time.time - lastCrashTriggerTime < MinCrashInterval)
        {
            return;
        }
        lastCrashTriggerTime = Time.time;

        EnsureAudio();
        if (audioSource != null && impactAudioClip != null)
        {
            audioSource.PlayOneShot(impactAudioClip, 0.45f);
        }

        SpawnImpactBurst(contactWorldPos);

        if (currentCrashRoutine != null)
        {
            StopCoroutine(currentCrashRoutine);
        }
        currentCrashRoutine = StartCoroutine(JackknifeCrashSequence());
    }

    private IEnumerator JackknifeCrashSequence()
    {
        // 1. Camera Shake (Gentle minimal bump)
        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
        }
        if (cameraFollow != null)
        {
            cameraFollow.Shake(shakeDuration, shakeMagnitude);
        }

        // 2. Banner & Red Flash (Subtle flash)
        if (crashText != null)
        {
            crashText.gameObject.SetActive(true);
            crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
            crashText.text = "💥 СКЛАДЫВАНИЕ! УДАР ТЯГАЧА О ПРИЦЕП! 💥\n<size=22><color=#FFFF66>НАЖМИТЕ [W] (ВПЕРЕД), ЧТОБЫ ВЫПРЯМИТЬ АВТОПОЕЗД</color></size>";
        }

        if (redFlashImage != null)
        {
            redFlashImage.gameObject.SetActive(true);
            redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f);
        }

        // 3. Fade out flash
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float percent = 1f - (elapsed / shakeDuration);
            if (redFlashImage != null)
            {
                redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f * percent);
            }
            yield return null;
        }

        // 4. Fade out banner
        float textFadeDuration = 0.8f;
        float textElapsed = 0f;
        while (textElapsed < textFadeDuration)
        {
            textElapsed += Time.deltaTime;
            float alpha = 1f - (textElapsed / textFadeDuration);
            if (crashText != null)
            {
                crashText.color = new Color(1f, 0.2f, 0.2f, alpha);
            }
            yield return null;
        }

        if (crashText != null)
        {
            crashText.gameObject.SetActive(false);
            crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
        }
        if (redFlashImage != null)
        {
            redFlashImage.gameObject.SetActive(false);
        }

        currentCrashRoutine = null;
    }

    public void TriggerCrash(string obstacleName, Vector2? contactPos = null, bool forwardImpact = true)
    {
        if (Time.time - lastCrashTriggerTime < MinCrashInterval)
        {
            return;
        }
        lastCrashTriggerTime = Time.time;

        EnsureAudio();
        if (audioSource != null && impactAudioClip != null)
        {
            audioSource.PlayOneShot(impactAudioClip, 0.40f);
        }

        if (contactPos.HasValue)
        {
            SpawnImpactBurst(contactPos.Value);
        }

        if (currentCrashRoutine != null)
        {
            StopCoroutine(currentCrashRoutine);
        }
        currentCrashRoutine = StartCoroutine(CrashSequence(obstacleName, forwardImpact));
    }

    private IEnumerator CrashSequence(string obstacleName, bool forwardImpact)
    {
        // 1. Trigger Camera Shake (Gentle minimal bump)
        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
        }
        if (cameraFollow != null)
        {
            cameraFollow.Shake(shakeDuration, shakeMagnitude);
        }

        // 2. Show Red Flash & Crash Banner Text (Subtle vignette)
        if (crashText != null)
        {
            crashText.gameObject.SetActive(true);
            crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
            string escapeHint = forwardImpact
                ? "НАЖМИТЕ [ТОРМОЗ / S] (НАЗАД), ЧТОБЫ СДАТЬ НАЗАД"
                : "НАЖМИТЕ [ГАЗ / W] (ВПЕРЕД), ЧТОБЫ ОТЪЕХАТЬ";
            crashText.text = $"💥 БУХ! ВРЕЗАЛСЯ В {obstacleName.ToUpper()}! 💥\n<size=22><color=#FFFF66>{escapeHint}</color></size>";
        }

        if (redFlashImage != null)
        {
            redFlashImage.gameObject.SetActive(true);
            redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f);
        }

        // 3. Fade out flash
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float percent = 1f - (elapsed / shakeDuration);

            if (redFlashImage != null)
            {
                redFlashImage.color = new Color(1f, 0.1f, 0.1f, 0.18f * percent);
            }
            yield return null;
        }

        // 4. Fade out text banner
        float textFadeDuration = 0.7f;
        float textElapsed = 0f;
        while (textElapsed < textFadeDuration)
        {
            textElapsed += Time.deltaTime;
            float alpha = 1f - (textElapsed / textFadeDuration);
            if (crashText != null)
            {
                crashText.color = new Color(1f, 0.2f, 0.2f, alpha);
            }
            yield return null;
        }

        if (crashText != null)
        {
            crashText.gameObject.SetActive(false);
            crashText.color = new Color(1f, 0.2f, 0.2f, 1f);
        }
        if (redFlashImage != null)
        {
            redFlashImage.gameObject.SetActive(false);
        }

        currentCrashRoutine = null;
    }
}
