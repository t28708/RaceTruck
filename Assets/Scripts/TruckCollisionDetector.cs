using System.Collections.Generic;
using UnityEngine;

public class TruckCollisionDetector : MonoBehaviour
{
    private Collider2D myCollider;
    private ContactFilter2D contactFilter;
    private readonly List<Collider2D> overlapResults = new List<Collider2D>(16);
    private float lastCrashTime = -1f;
    private const float CrashCooldown = 0.35f;

    // Track active colliders touching this part so block is not cleared prematurely
    private readonly HashSet<Collider2D> touchingColliders = new HashSet<Collider2D>();

    private void Awake()
    {
        myCollider = GetComponent<Collider2D>();
        contactFilter = new ContactFilter2D();
        contactFilter.useTriggers = true; // Crucial: detects static triggers (parked trucks, cones, dock borders)
        contactFilter.useLayerMask = false; // detects all layers
    }

    private void FixedUpdate()
    {
        if (myCollider == null || !myCollider.enabled) return;

        overlapResults.Clear();
        int count = myCollider.Overlap(contactFilter, overlapResults);

        bool hasObstacle = false;
        Collider2D firstObstacle = null;
        HashSet<Collider2D> currentObstacles = new HashSet<Collider2D>();

        for (int i = 0; i < count; i++)
        {
            Collider2D other = overlapResults[i];
            if (other == null || !other.enabled) continue;
            if (IsPlayerVehicle(other.gameObject)) continue;
            if (IsIgnoredObstacle(other)) continue;

            hasObstacle = true;
            if (firstObstacle == null) firstObstacle = other;
            currentObstacles.Add(other);
            touchingColliders.Add(other);
        }

        touchingColliders.RemoveWhere(col => col == null || !col.enabled || !currentObstacles.Contains(col));

        if (hasObstacle && firstObstacle != null)
        {
            HandleCollision(firstObstacle.gameObject, firstObstacle);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerVehicle(other.gameObject) || IsIgnoredObstacle(other)) return;
        touchingColliders.Add(other);
        HandleCollision(other.gameObject, other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsPlayerVehicle(other.gameObject) || IsIgnoredObstacle(other)) return;
        touchingColliders.Add(other);
        HandleCollision(other.gameObject, other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        touchingColliders.Remove(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsPlayerVehicle(collision.gameObject) || IsIgnoredObstacle(collision.collider)) return;
        touchingColliders.Add(collision.collider);
        HandleCollision(collision.gameObject, collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsPlayerVehicle(collision.gameObject) || IsIgnoredObstacle(collision.collider)) return;
        touchingColliders.Add(collision.collider);
        HandleCollision(collision.gameObject, collision.collider);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        touchingColliders.Remove(collision.collider);
    }

    public static bool IsIgnoredObstacle(Collider2D col)
    {
        if (col == null) return true;

        string n = col.gameObject.name;
        Transform p = col.transform.parent;
        string pn = (p != null) ? p.name : "";

        // Ground markings, guide lines, hazard stripes, chevrons, dashes, and parking triggers/zones
        if (IsMarkingName(n) || IsMarkingName(pn))
        {
            return true;
        }

        // Physical obstacles
        if (IsPhysicalObstacleName(n) || IsPhysicalObstacleName(pn))
        {
            return false;
        }

        return false;
    }

    private static bool IsPhysicalObstacleName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("Boundary", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Border", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Fence", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bumper", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Truck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Trailer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tractor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Bus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Barrier", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Pole", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Barrel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Cone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Car", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Auto", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Hydrant", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Booth", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Checkin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Tire", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Stack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Prop", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Lawn", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Curb", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMarkingName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // Note: Map boundary walls ("Border" / "Boundary") are physical obstacles, not ignored markings
        if (name.IndexOf("Border", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Boundary", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return name.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Stripe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Line", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Dashes", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Chevron", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Arrow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Trigger", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Marking", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Target", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool IsPlayerVehicle(GameObject other)
    {
        if (other == null) return false;

        // Same object or child of this object (e.g. wheels of this vehicle part)
        if (other == gameObject || other.transform.IsChildOf(transform))
        {
            return true;
        }

        // Check if other belongs to the player's Tractor or Trailer
        TruckController playerController = TruckController.Instance ?? Object.FindFirstObjectByType<TruckController>();
        if (playerController != null)
        {
            Transform playerTractor = playerController.transform;
            if (other.transform == playerTractor || other.transform.IsChildOf(playerTractor))
            {
                return true;
            }

            if (playerController.TrailerRb != null)
            {
                Transform playerTrailer = playerController.TrailerRb.transform;
                if (other.transform == playerTrailer || other.transform.IsChildOf(playerTrailer))
                {
                    return true;
                }
            }
        }

        // Obstacles under map builder containers are NEVER player vehicles
        Transform p = other.transform.parent;
        while (p != null)
        {
            string pn = p.name;
            if (pn.StartsWith("MapBuilder_") || pn.StartsWith("Stall_") || pn.StartsWith("TruckObstacle") || pn.StartsWith("TrailerObstacle"))
            {
                return false;
            }
            p = p.parent;
        }

        return false;
    }

    private void HandleCollision(GameObject other, Collider2D hitCol = null)
    {
        if (IsPlayerVehicle(other)) return;
        if (hitCol != null && IsIgnoredObstacle(hitCol)) return;
        if (TruckController.Instance == null) return;

        float speed = TruckController.Instance.CurrentSpeed;

        // CRITICAL 1: Stationary truck (speed == 0) CANNOT crash!
        // A stationary truck is resting against an obstacle or waiting for player input to drive away.
        // DO NOT call OnCrash when stationary!
        if (Mathf.Abs(speed) < 0.01f)
        {
            return;
        }

        Collider2D actualCol = hitCol != null ? hitCol : other.GetComponent<Collider2D>();

        // If this vehicle already crashed and is actively moving in the ESCAPE direction away from the obstacle, DO NOT re-crash!
        if (actualCol != null && TruckController.IsSameObstacle(actualCol, TruckController.Instance.LastCrashedObstacle))
        {
            if (speed > 0.01f && TruckController.Instance.LastCrashDirection == -1)
            {
                return; // Escaping from a reverse crash!
            }
            if (speed < -0.01f && TruckController.Instance.LastCrashDirection == +1)
            {
                return; // Escaping from a forward crash!
            }
        }

        string obstacleName = FormatObstacleNameStatic(other.name, other.transform);
        bool forwardImpact = (speed > 0f);

        TruckController.Instance.OnCrash(obstacleName, forwardImpact, actualCol);

        // Trigger visual screen shake, metal impact sound, spark burst, and "БУХ!" UI banner
        if (Time.time - lastCrashTime >= CrashCooldown)
        {
            lastCrashTime = Time.time;

            // Best-effort contact point: iterative closest points
            Vector2 contactPoint = (Vector2)transform.position;
            if (myCollider != null && actualCol != null)
            {
                Vector2 myCenter = myCollider.bounds.center;
                Vector2 ptOnThem = actualCol.ClosestPoint(myCenter);
                Vector2 ptOnUs   = myCollider.ClosestPoint(ptOnThem);
                contactPoint     = (ptOnThem + ptOnUs) * 0.5f;
            }

            // NOTE: CollisionFlash.Spawn is called by TruckController's predictive system
            // which has the accurate bumper-level contact point. Do NOT call it here to
            // avoid a second misplaced flash when physics overlap also fires.

            if (TruckCrashEffect.Instance != null)
            {
                TruckCrashEffect.Instance.TriggerCrash(obstacleName, contactPoint, forwardImpact);
            }
        }
    }

    public static string FormatObstacleNameStatic(string rawName, Transform otherTransform)
    {
        string combinedName = rawName;
        if (otherTransform != null && otherTransform.parent != null)
        {
            combinedName += "_" + otherTransform.parent.name;
        }

        if (combinedName.Contains("Truck") || combinedName.Contains("Parked") || combinedName.Contains("Trailer") || combinedName.Contains("Tractor"))
        {
            return LocalizationManager.Get("OBS_TRUCK");
        }
        if (combinedName.Contains("Cone")) return LocalizationManager.Get("OBS_CONE");
        if (combinedName.Contains("Hydrant")) return LocalizationManager.Get("OBS_HYDRANT");
        if (combinedName.Contains("Booth") || combinedName.Contains("Checkin")) return LocalizationManager.Get("OBS_BOOTH");
        if (combinedName.Contains("Tire") || combinedName.Contains("Stack")) return LocalizationManager.Get("OBS_TIRES");
        if (combinedName.Contains("Car") || combinedName.Contains("Auto") || combinedName.Contains("PassengerCar")) return LocalizationManager.Get("OBS_CAR");
        if (combinedName.Contains("Boundary") || combinedName.Contains("Border") || combinedName.Contains("Fence")) return LocalizationManager.Get("OBS_BORDER");
        if (combinedName.Contains("Barrier") || combinedName.Contains("Wall")) return LocalizationManager.Get("OBS_BARRIER");
        if (combinedName.Contains("Barrel")) return LocalizationManager.Get("OBS_BARREL");
        if (combinedName.Contains("Pole")) return LocalizationManager.Get("OBS_BORDER");
        if (combinedName.Contains("Bumper")) return LocalizationManager.Get("OBS_BARRIER");
        if (combinedName.Contains("Hazard")) return LocalizationManager.Get("OBS_BORDER");
        return LocalizationManager.Get("OBS_BARRIER");
    }
}
