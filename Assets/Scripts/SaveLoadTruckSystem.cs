using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum TruckGearState
{
    Neutral = 0,
    Drive = 1,
    Reverse = 2
}

/// <summary>
/// Serializable data contract storing the complete articulated state of the tractor,
/// 53ft trailer, steering geometry, UI wheel angle, and transmission state.
/// </summary>
[Serializable]
public class TruckSaveData
{
    // 1. Tractor Transform
    public Vector3 tractorPosition;
    public Quaternion tractorRotation;

    // 2. Trailer Transform & Articulation
    public bool hasTrailer;
    public Vector3 trailerPosition;
    public Quaternion trailerRotation;
    public float articulationAngle;
    public bool isJackknifed;

    // 3. Steering & UI Wheel State
    public float steerAngle;
    public float uiSteeringWheelAngle;

    // 4. Transmission / Gear State
    public TruckGearState currentGear;

    // 5. Metadata
    public string sceneName;
    public string savedAtTimestamp;
}

/// <summary>
/// Persistent Checkpoint Save & Load System for CDL Truck Parking Simulator.
/// Executes a strict 5-step Safe Teleport & Joint Restoration Sequence to prevent
/// physics explosions, joint tearing, or steering wheel UI desynchronization.
/// </summary>
public class SaveLoadTruckSystem : MonoBehaviour
{
    public static SaveLoadTruckSystem Instance { get; private set; }

    [Header("Core References (Auto-Resolved if Null)")]
    [SerializeField] private TruckController truckController;
    [SerializeField] private SteeringWheelUI steeringWheelUI;

    [Header("Optional 3D Physics & WheelCollider References")]
    [SerializeField] private Rigidbody tractorRigidbody3D;
    [SerializeField] private Rigidbody trailerRigidbody3D;
    [SerializeField] private WheelCollider[] steeringWheelColliders;
    [SerializeField] private WheelCollider[] allWheelColliders;
    [SerializeField] private float idleBrakeTorque = 800f;

    public event Action<TruckSaveData> OnCheckpointSaved;
    public event Action<TruckSaveData> OnCheckpointLoaded;

    private Coroutine activeRestoreCoroutine;
    private bool isRestoring = false;

    public bool IsRestoring => isRestoring;

    private const string PrefKeyPrefix = "CDL_Checkpoint_JSON_";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (truckController == null)
        {
            truckController = TruckController.Instance != null
                ? TruckController.Instance
                : FindFirstObjectByType<TruckController>();
        }

        if (steeringWheelUI == null)
        {
            steeringWheelUI = SteeringWheelUI.Instance != null
                ? SteeringWheelUI.Instance
                : FindFirstObjectByType<SteeringWheelUI>();
        }

        if (truckController != null)
        {
            if (tractorRigidbody3D == null)
            {
                tractorRigidbody3D = truckController.GetComponent<Rigidbody>();
            }
            if (trailerRigidbody3D == null && truckController.TrailerRb != null)
            {
                trailerRigidbody3D = truckController.TrailerRb.GetComponent<Rigidbody>();
            }
            if (allWheelColliders == null || allWheelColliders.Length == 0)
            {
                allWheelColliders = truckController.GetComponentsInChildren<WheelCollider>(true);
            }
        }
    }

    /// <summary>
    /// Returns the absolute JSON file path for a specific scene's checkpoint.
    /// Format: Application.persistentDataPath + "/checkpoint_" + sceneName + ".json"
    /// </summary>
    public static string GetCheckpointFilePath(string sceneName)
    {
        string safeScene = string.IsNullOrEmpty(sceneName)
            ? SceneManager.GetActiveScene().name
            : sceneName.Trim();
        return Path.Combine(Application.persistentDataPath, "checkpoint_" + safeScene + ".json");
    }

    /// <summary>
    /// Checks whether a valid checkpoint exists for the specified scene name.
    /// </summary>
    public static bool HasCheckpoint(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = SceneManager.GetActiveScene().name;
        }

        try
        {
            string filePath = GetCheckpointFilePath(sceneName);
            if (File.Exists(filePath))
            {
                FileInfo fi = new FileInfo(filePath);
                if (fi.Length > 10) return true;
            }
        }
        catch (Exception)
        {
            // Fallback to PlayerPrefs check if file access fails
        }

        return PlayerPrefs.HasKey(PrefKeyPrefix + sceneName);
    }

    /// <summary>
    /// Captures the current Tractor, 53ft Trailer, Steering, and Transmission state
    /// and writes it to disk (plus PlayerPrefs backup) for the active scene.
    /// </summary>
    public bool SaveCheckpoint()
    {
        ResolveReferences();
        if (truckController == null)
        {
            Debug.LogWarning("[SaveLoadTruckSystem] Cannot save checkpoint: TruckController not found.");
            return false;
        }

        string currentScene = SceneManager.GetActiveScene().name;
        Transform tractorTr = truckController.transform;
        Rigidbody2D trailerRb2D = truckController.TrailerRb;

        float uiWheelAngle = (steeringWheelUI != null)
            ? steeringWheelUI.CurrentWheelAngle
            : truckController.SteerAngle * (450f / 40f);

        TruckSaveData data = new TruckSaveData
        {
            tractorPosition = tractorTr.position,
            tractorRotation = tractorTr.rotation,
            hasTrailer = (trailerRb2D != null),
            trailerPosition = (trailerRb2D != null) ? trailerRb2D.transform.position : Vector3.zero,
            trailerRotation = (trailerRb2D != null) ? trailerRb2D.transform.rotation : Quaternion.identity,
            articulationAngle = truckController.CurrentArticulationAngle,
            isJackknifed = truckController.IsJackknifed,
            steerAngle = truckController.SteerAngle,
            uiSteeringWheelAngle = uiWheelAngle,
            currentGear = truckController.GetCurrentGearState(),
            sceneName = currentScene,
            savedAtTimestamp = DateTime.UtcNow.ToString("O")
        };

        string json = JsonUtility.ToJson(data, true);

        bool savedToDisk = false;
        try
        {
            string filePath = GetCheckpointFilePath(currentScene);
            File.WriteAllText(filePath, json);
            savedToDisk = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveLoadTruckSystem] Disk write fallback to PlayerPrefs: {ex.Message}");
        }

        // Always store a redundant copy in PlayerPrefs using scene-unique key
        PlayerPrefs.SetString(PrefKeyPrefix + currentScene, json);
        PlayerPrefs.Save();

        Debug.Log($"<color=#44ff88>[SaveLoadTruckSystem] Checkpoint saved for '{currentScene}' (Steer: {data.steerAngle:F1}°, UI Wheel: {data.uiSteeringWheelAngle:F1}°, Hitch: {data.articulationAngle:F1}°, Disk: {savedToDisk})</color>");

        OnCheckpointSaved?.Invoke(data);
        return true;
    }

    /// <summary>
    /// Reads the saved checkpoint for the active scene and triggers the 5-step safe restoration coroutine.
    /// </summary>
    public bool LoadCheckpoint()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (!TryReadCheckpointData(currentScene, out TruckSaveData data) || data == null)
        {
            Debug.LogWarning($"[SaveLoadTruckSystem] No checkpoint found for scene '{currentScene}'.");
            return false;
        }

        if (activeRestoreCoroutine != null)
        {
            StopCoroutine(activeRestoreCoroutine);
        }

        activeRestoreCoroutine = StartCoroutine(ExecuteSafeRestoreSequence(data));
        return true;
    }

    /// <summary>
    /// Reads and deserializes the TruckSaveData for a given scene.
    /// </summary>
    public static bool TryReadCheckpointData(string sceneName, out TruckSaveData data)
    {
        data = null;
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = SceneManager.GetActiveScene().name;
        }

        string json = null;

        try
        {
            string filePath = GetCheckpointFilePath(sceneName);
            if (File.Exists(filePath))
            {
                json = File.ReadAllText(filePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveLoadTruckSystem] Failed reading checkpoint file: {ex.Message}");
        }

        if (string.IsNullOrEmpty(json) && PlayerPrefs.HasKey(PrefKeyPrefix + sceneName))
        {
            json = PlayerPrefs.GetString(PrefKeyPrefix + sceneName, string.Empty);
        }

        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        try
        {
            data = JsonUtility.FromJson<TruckSaveData>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveLoadTruckSystem] Corrupt checkpoint JSON for '{sceneName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Executes the strict 5-Step Safe Teleport & Joint Restoration Sequence:
    /// Step A: Physics Freeze
    /// Step B: Zero Out Wheel Physics
    /// Step C: Position & Articulation Restoration + Physics.SyncTransforms()
    /// Step D: Steer & UI Synchronization
    /// Step E: Physics Unfreeze on Next FixedUpdate
    /// </summary>
    private IEnumerator ExecuteSafeRestoreSequence(TruckSaveData data)
    {
        isRestoring = true;
        ResolveReferences();

        if (truckController == null)
        {
            isRestoring = false;
            yield break;
        }

        Rigidbody2D tractorRb2D = truckController.GetComponent<Rigidbody2D>();
        Rigidbody2D trailerRb2D = truckController.TrailerRb;

        // =====================================================================
        // STEP A - Physics Freeze
        // =====================================================================
        truckController.BeginTeleportFreeze();

        if (TruckCrashEffect.Instance != null)
        {
            TruckCrashEffect.Instance.ClearActiveCrashFeedback();
        }

        if (tractorRigidbody3D != null)
        {
            tractorRigidbody3D.isKinematic = true;
            tractorRigidbody3D.linearVelocity = Vector3.zero;
            tractorRigidbody3D.angularVelocity = Vector3.zero;
        }
        if (trailerRigidbody3D != null)
        {
            trailerRigidbody3D.isKinematic = true;
            trailerRigidbody3D.linearVelocity = Vector3.zero;
            trailerRigidbody3D.angularVelocity = Vector3.zero;
        }

        if (tractorRb2D != null)
        {
            tractorRb2D.bodyType = RigidbodyType2D.Kinematic;
            tractorRb2D.linearVelocity = Vector2.zero;
            tractorRb2D.angularVelocity = 0f;
        }
        if (trailerRb2D != null)
        {
            trailerRb2D.bodyType = RigidbodyType2D.Kinematic;
            trailerRb2D.linearVelocity = Vector2.zero;
            trailerRb2D.angularVelocity = 0f;
        }

        // =====================================================================
        // STEP B - Zero Out Wheel Physics
        // =====================================================================
        if (allWheelColliders != null)
        {
            for (int i = 0; i < allWheelColliders.Length; i++)
            {
                WheelCollider wc = allWheelColliders[i];
                if (wc == null) continue;
                wc.steerAngle = 0f;
                wc.motorTorque = 0f;
                wc.brakeTorque = Mathf.Infinity;
            }
        }

        truckController.SetGasPedal(false);
        truckController.SetBrakePedal(false);

        // =====================================================================
        // STEP C - Position & Articulation Restoration
        // =====================================================================
        Transform tractorTr = truckController.transform;
        tractorTr.position = data.tractorPosition;
        tractorTr.rotation = data.tractorRotation;

        if (tractorRb2D != null)
        {
            tractorRb2D.position = new Vector2(data.tractorPosition.x, data.tractorPosition.y);
            tractorRb2D.rotation = data.tractorRotation.eulerAngles.z;
        }

        if (trailerRb2D != null && data.hasTrailer)
        {
            Transform trailerTr = trailerRb2D.transform;

            // Ensure exact 5th-wheel to kingpin lock in world space while preserving saved trailer orientation
            float tractorAngleRad = data.tractorRotation.eulerAngles.z * Mathf.Deg2Rad;
            Vector2 tractorForward = new Vector2(-Mathf.Sin(tractorAngleRad), Mathf.Cos(tractorAngleRad));
            Vector2 hitchWorldPos = (Vector2)data.tractorPosition + tractorForward * truckController.HitchLocalOffset;

            float trailerAngleDeg = data.trailerRotation.eulerAngles.z;
            float trailerAngleRad = trailerAngleDeg * Mathf.Deg2Rad;
            Vector2 trailerForward = new Vector2(-Mathf.Sin(trailerAngleRad), Mathf.Cos(trailerAngleRad));
            Vector2 exactTrailerPos2D = hitchWorldPos - trailerForward * truckController.KingpinLocalOffset;

            Vector3 restoredTrailerPos = new Vector3(exactTrailerPos2D.x, exactTrailerPos2D.y, data.trailerPosition.z);

            trailerTr.position = restoredTrailerPos;
            trailerTr.rotation = data.trailerRotation;

            trailerRb2D.position = exactTrailerPos2D;
            trailerRb2D.rotation = trailerAngleDeg;
        }

        // Force immediate engine transform synchronization across 3D and 2D physics worlds
        Physics.SyncTransforms();
        Physics2D.SyncTransforms();

        // Synchronize historical tractrix positions so FixedUpdate sees 0 displacement on resume
        truckController.SyncTractrixAfterTeleport(data.isJackknifed);

        // =====================================================================
        // STEP D - Steer & UI Synchronization
        // =====================================================================
        if (steeringWheelColliders != null)
        {
            for (int i = 0; i < steeringWheelColliders.Length; i++)
            {
                if (steeringWheelColliders[i] != null)
                {
                    steeringWheelColliders[i].steerAngle = data.steerAngle;
                }
            }
        }

        if (steeringWheelUI != null)
        {
            steeringWheelUI.RestoreWheelAngle(data.uiSteeringWheelAngle);
        }

        truckController.RestoreSteerAndGear(data.steerAngle, data.currentGear);

        // Snap camera immediately so it doesn't lurch across the map
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.SnapToTarget();
        }

        // =====================================================================
        // STEP E - Physics Unfreeze (Next Fixed Frame)
        // =====================================================================
        yield return new WaitForFixedUpdate();

        if (tractorRigidbody3D != null)
        {
            tractorRigidbody3D.isKinematic = false;
            tractorRigidbody3D.linearVelocity = Vector3.zero;
            tractorRigidbody3D.angularVelocity = Vector3.zero;
        }
        if (trailerRigidbody3D != null)
        {
            trailerRigidbody3D.isKinematic = false;
            trailerRigidbody3D.linearVelocity = Vector3.zero;
            trailerRigidbody3D.angularVelocity = Vector3.zero;
        }

        if (allWheelColliders != null)
        {
            for (int i = 0; i < allWheelColliders.Length; i++)
            {
                WheelCollider wc = allWheelColliders[i];
                if (wc == null) continue;
                wc.motorTorque = 0f;
                wc.brakeTorque = idleBrakeTorque;
            }
        }

        truckController.EndTeleportFreeze();

        isRestoring = false;
        activeRestoreCoroutine = null;

        Debug.Log($"<color=#44ff88>[SaveLoadTruckSystem] Checkpoint restored for '{data.sceneName}' (Steer: {data.steerAngle:F1}°, UI Wheel: {data.uiSteeringWheelAngle:F1}°, Hitch: {truckController.CurrentArticulationAngle:F1}°)</color>");

        OnCheckpointLoaded?.Invoke(data);
    }
}
