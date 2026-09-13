using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Realistic Freightliner Cascadia truck audio system:
/// 1. Level Start: Authentic Cascadia starter cranking, Detroit DD15 combustion catch and roar.
/// 2. Stationary / Idle: Steady, deep 600 RPM 6-cylinder diesel idle rumble.
/// 3. Gas Pedal / Acceleration: Throttle roar under load with dynamic pitch and volume scaling with speed.
/// 4. Gas Pedal Release: Smooth natural acoustic decay back down to idle.
/// 5. Braking: High-pressure pneumatic air brake hiss / purge valve burst.
/// 6. Reverse Driving: Engine throttle sound accompanied by industrial backup warning beeper pulses.
/// </summary>
[RequireComponent(typeof(TruckController))]
public class TruckAudioController : MonoBehaviour
{
    private TruckController controller;

    // Dedicated Audio Sources
    private AudioSource startupSource;
    private AudioSource idleSource;
    private AudioSource driveSource;
    private AudioSource airBrakeSource;
    private AudioSource reverseBeepSource;

    // Audio Clips
    private AudioClip startupClip;
    private AudioClip idleClip;
    private AudioClip driveClip;
    private AudioClip airBrakeClip;
    private AudioClip reverseBeepClip;

    private bool isStartingUp = true;
    private float startupTimer = 0f;
    private const float StartupDuration = 2.65f;

    private float currentDriveVolume = 0f;
    private float targetDriveVolume = 0f;
    private float lastBrakeTriggerTime = -1f;
    private bool wasMovingForward = false;
    private bool wasBraking = false;

    private void Awake()
    {
        controller = GetComponent<TruckController>();
        SetupAudioSources();
        LoadAudioClips();
    }

    private void Start()
    {
        StartEngine();
    }

    private void SetupAudioSources()
    {
        startupSource = CreateAudioSource("Audio_Startup", loop: false);
        idleSource = CreateAudioSource("Audio_Idle", loop: true);
        driveSource = CreateAudioSource("Audio_Drive", loop: true);
        airBrakeSource = CreateAudioSource("Audio_AirBrake", loop: false);
        reverseBeepSource = CreateAudioSource("Audio_ReverseBeep", loop: true);
    }

    private AudioSource CreateAudioSource(string childName, bool loop)
    {
        Transform child = transform.Find(childName);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(childName);
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = child.gameObject;
        }

        AudioSource src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();

        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f; // 2D clean stereo audio across cockpit & follow camera
        src.volume = 0f;
        src.pitch = 1.0f;
        return src;
    }

    private void LoadAudioClips()
    {
        startupClip = Resources.Load<AudioClip>("Audio/Truck_Startup_Cascadia");
        idleClip = Resources.Load<AudioClip>("Audio/Truck_Engine_Idle");
        driveClip = Resources.Load<AudioClip>("Audio/Truck_Engine_Drive");
        airBrakeClip = Resources.Load<AudioClip>("Audio/Truck_AirBrake_Release");
        reverseBeepClip = Resources.Load<AudioClip>("Audio/Truck_Reverse_Beep");

        // Fallback procedural generators in case resource files are missing
        if (startupClip == null) startupClip = FallbackAudio.GetStartupClip();
        if (idleClip == null) idleClip = FallbackAudio.GetIdleClip();
        if (driveClip == null) driveClip = FallbackAudio.GetDriveClip();
        if (airBrakeClip == null) airBrakeClip = FallbackAudio.GetAirBrakeClip();
        if (reverseBeepClip == null) reverseBeepClip = FallbackAudio.GetReverseBeepClip();

        if (startupSource != null) startupSource.clip = startupClip;
        if (idleSource != null) idleSource.clip = idleClip;
        if (driveSource != null) driveSource.clip = driveClip;
        if (airBrakeSource != null) airBrakeSource.clip = airBrakeClip;
        if (reverseBeepSource != null) reverseBeepSource.clip = reverseBeepClip;
    }

    public void StartEngine()
    {
        isStartingUp = true;
        startupTimer = 0f;

        // 1. Play Cascadia Starter Sequence
        if (startupSource != null && startupClip != null)
        {
            startupSource.volume = 0.90f;
            startupSource.pitch = 1.0f;
            startupSource.Play();
        }

        // 2. Prepare Idle Loop (starts muted, fades in during combustion catch)
        if (idleSource != null && idleClip != null)
        {
            idleSource.volume = 0f;
            idleSource.pitch = 1.0f;
            idleSource.Play();
        }

        // 3. Prepare Drive Loop (muted)
        if (driveSource != null && driveClip != null)
        {
            driveSource.volume = 0f;
            driveSource.pitch = 1.0f;
            driveSource.Play();
        }
    }

    private void Update()
    {
        if (controller == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed = controller.CurrentSpeed; // positive = forward, negative = reverse
        float absSpeed = Mathf.Abs(speed);
        float maxSpeed = (speed >= 0f) ? controller.MaxForwardSpeed : controller.MaxReverseSpeed;
        if (maxSpeed < 0.1f) maxSpeed = 2.78f; // ~10 km/h

        float speedRatio = Mathf.Clamp01(absSpeed / maxSpeed);
        bool gasPressed = controller.IsGasPedalPressed;
        bool brakePressed = controller.IsBrakePedalPressed;

        // ----------------------------------------------------
        // 1. Startup Phase (Cascadia Cranking -> Roar -> Idle)
        // ----------------------------------------------------
        if (isStartingUp)
        {
            startupTimer += dt;
            if (startupTimer < 0.70f)
            {
                // Electric starter cranking: engine not yet idling
                if (idleSource != null) idleSource.volume = 0f;
            }
            else if (startupTimer < StartupDuration)
            {
                // Engine catches fire and surges: fade in idle loop
                float catchRatio = (startupTimer - 0.70f) / (StartupDuration - 0.70f);
                if (idleSource != null) idleSource.volume = Mathf.Lerp(0f, 0.75f, catchRatio);
            }
            else
            {
                isStartingUp = false;
                if (idleSource != null) idleSource.volume = 0.75f;
            }
        }

        // ----------------------------------------------------
        // 2. Throttle / Acceleration Dynamics
        // ----------------------------------------------------
        bool isUnderThrottle = (gasPressed && speed >= -0.05f) || (brakePressed && speed <= 0.05f);

        if (isUnderThrottle)
        {
            // Heavy diesel under throttle: rich deep exhaust roar
            targetDriveVolume = Mathf.Lerp(0.55f, 0.90f, speedRatio);
        }
        else if (absSpeed > 0.1f)
        {
            // Coasting / engine braking: quiet low rumble
            targetDriveVolume = Mathf.Lerp(0.10f, 0.25f, speedRatio);
        }
        else
        {
            targetDriveVolume = 0f;
        }

        // Attack when gas pressed: 9.0/s, decay when released: 3.2/s (smooth natural acoustic dissipation)
        float lerpSpeed = (targetDriveVolume > currentDriveVolume) ? 9.0f : 3.2f;
        currentDriveVolume = Mathf.MoveTowards(currentDriveVolume, targetDriveVolume, lerpSpeed * dt);

        if (driveSource != null)
        {
            driveSource.volume = currentDriveVolume;
            // Realistic heavy diesel pitch: narrow 0.92 to 1.08 band (no high-pitch whine or sped-up sound!)
            float targetPitch = 0.92f + speedRatio * 0.16f;
            driveSource.pitch = Mathf.Lerp(driveSource.pitch, targetPitch, 5f * dt);
        }

        // Idle volume ducks slightly under heavy throttle, holds firm and deep when stopped
        if (!isStartingUp && idleSource != null)
        {
            idleSource.volume = Mathf.Lerp(0.72f, 0.35f, currentDriveVolume);
            idleSource.pitch = 1.0f; // rock-solid diesel idle, no wobble
        }

        // ----------------------------------------------------
        // 3. Air Brakes Pneumatic Hiss
        // ----------------------------------------------------
        bool isMovingForward = speed > 0.3f;
        bool isActivelyBraking = brakePressed && isMovingForward;

        if (isActivelyBraking && !wasBraking && (Time.time - lastBrakeTriggerTime > 0.8f))
        {
            PlayAirBrakes();
        }
        else if (wasMovingForward && absSpeed < 0.05f && (Time.time - lastBrakeTriggerTime > 1.2f))
        {
            // Final pneumatic purge hiss when coming to a dead stop
            PlayAirBrakes(volume: 0.70f);
        }

        wasMovingForward = isMovingForward;
        wasBraking = isActivelyBraking;

        // ----------------------------------------------------
        // 4. Reverse Driving & Backup Beeper Warning
        // ----------------------------------------------------
        bool inReverse = (speed < -0.1f) || (brakePressed && speed <= 0.05f);
        if (reverseBeepSource != null)
        {
            if (inReverse && !reverseBeepSource.isPlaying)
            {
                reverseBeepSource.volume = 0.55f;
                reverseBeepSource.pitch = 1.0f;
                reverseBeepSource.Play();
            }
            else if (!inReverse && reverseBeepSource.isPlaying)
            {
                reverseBeepSource.Stop();
            }
        }
    }

    public void PlayAirBrakes(float volume = 0.85f)
    {
        lastBrakeTriggerTime = Time.time;
        if (airBrakeSource != null && airBrakeClip != null)
        {
            airBrakeSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
            airBrakeSource.PlayOneShot(airBrakeClip, volume);
        }
    }

    private void OnDisable()
    {
        if (startupSource != null) startupSource.Stop();
        if (idleSource != null) idleSource.Stop();
        if (driveSource != null) driveSource.Stop();
        if (airBrakeSource != null) airBrakeSource.Stop();
        if (reverseBeepSource != null) reverseBeepSource.Stop();
    }

    // =========================================================================
    // Procedural Fallback Audio Synthesizer
    // Guarantees audio is never silent even if asset files are moved or deleted
    // =========================================================================
    private static class FallbackAudio
    {
        private const int SR = 44100;

        public static AudioClip GetStartupClip()
        {
            int count = (int)(SR * 3.2f);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SR;
                if (t < 1.55f)
                {
                    float chug = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 4.2f * t)), 3f);
                    data[i] = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 55f * t) * chug * 0.7f;
                }
                else
                {
                    float decay = Mathf.Exp(-(t - 1.55f) * 1.5f);
                    data[i] = Mathf.Sin(2f * Mathf.PI * (30f + 30f * decay) * t) * 0.7f + (UnityEngine.Random.value * 2f - 1f) * 0.08f;
                }
            }
            AudioClip clip = AudioClip.Create("Procedural_Startup", count, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip GetIdleClip()
        {
            int count = SR * 2;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SR;
                float phase = 2f * Mathf.PI * 30f * t;
                data[i] = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2f) * 0.35f + Mathf.Sin(phase * 3f) * 0.2f + (UnityEngine.Random.value * 2f - 1f) * 0.05f;
            }
            AudioClip clip = AudioClip.Create("Procedural_Idle", count, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip GetDriveClip()
        {
            int count = SR * 2;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SR;
                float phase = 2f * Mathf.PI * 68f * t;
                data[i] = Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 2f) * 0.40f + Mathf.Sin(phase * 3f) * 0.25f + (UnityEngine.Random.value * 2f - 1f) * 0.06f;
            }
            AudioClip clip = AudioClip.Create("Procedural_Drive", count, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip GetAirBrakeClip()
        {
            int count = (int)(SR * 1.15f);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SR;
                data[i] = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 3.5f) * 0.7f;
            }
            AudioClip clip = AudioClip.Create("Procedural_AirBrake", count, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip GetReverseBeepClip()
        {
            int count = (int)(SR * 0.8f);
            float[] data = new float[count];
            int beepCount = (int)(SR * 0.22f);
            for (int i = 0; i < beepCount; i++)
            {
                float t = i / (float)SR;
                data[i] = Mathf.Sin(2f * Mathf.PI * 950f * t) * 0.65f;
            }
            AudioClip clip = AudioClip.Create("Procedural_ReverseBeep", count, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
