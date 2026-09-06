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

        if (col.isTrigger)
        {
            string n = col.gameObject.name;
            // Never ignore actual physical obstacles that happen to use triggers
            if (n.IndexOf("Bumper", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Truck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Trailer", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Tractor", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Bus", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Tree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Barrier", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Pole", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Barrel", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Cone", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            // Ground markings, guide lines, hazard stripes, chevrons, dashes, and parking triggers
            if (n.IndexOf("Hazard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Stripe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Line", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Dashes", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Chevron", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Arrow", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Trigger", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Guide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Marking", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Border", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
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
        if (TruckController.Instance != null)
        {
            Transform playerTractor = TruckController.Instance.transform;
            if (other.transform == playerTractor || other.transform.IsChildOf(playerTractor))
            {
                return true;
            }

            if (TruckController.Instance.TrailerRb != null)
            {
                Transform playerTrailer = TruckController.Instance.TrailerRb.transform;
                if (other.transform == playerTrailer || other.transform.IsChildOf(playerTrailer))
                {
                    return true;
                }
            }
        }
        else
        {
            // Fallback before TruckController.Instance is initialized
            if (other.transform.root == transform.root)
            {
                return true;
            }
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

        // Relative position to this vehicle part (Tractor or Trailer)
        Vector2 localObstaclePos = transform.InverseTransformPoint(other.transform.position);
        bool isFrontObstacle = (localObstaclePos.y >= 0f);

        // CRITICAL 2: If moving AWAY from this obstacle, DO NOT crash or block!
        if (isFrontObstacle && speed < -0.01f)
        {
            return; // Moving backward away from front obstacle
        }

        if (!isFrontObstacle && speed > 0.01f)
        {
            return; // Moving forward away from rear obstacle
        }

        // CRITICAL 3: If this vehicle already crashed and is actively moving in the ESCAPE direction, DO NOT crash!
        if (speed > 0.01f && TruckController.Instance.LastCrashDirection == -1)
        {
            return; // Escaping from a reverse crash!
        }
        if (speed < -0.01f && TruckController.Instance.LastCrashDirection == +1)
        {
            return; // Escaping from a forward crash!
        }

        string obstacleName = FormatObstacleNameStatic(other.name, other.transform);
        bool forwardImpact = (speed > 0f);

        Collider2D actualCol = hitCol != null ? hitCol : other.GetComponent<Collider2D>();
        TruckController.Instance.OnCrash(obstacleName, forwardImpact, actualCol);

        // Trigger visual screen shake, metal impact sound, spark burst, and "БУХ!" UI banner
        if (Time.time - lastCrashTime >= CrashCooldown)
        {
            lastCrashTime = Time.time;
            if (TruckCrashEffect.Instance != null)
            {
                TruckCrashEffect.Instance.TriggerCrash(obstacleName, transform.position, forwardImpact);
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
            return "припаркованный грузовик";
        }
        if (combinedName.Contains("Cone")) return "конус";
        if (combinedName.Contains("Barrier") || combinedName.Contains("Wall")) return "стену";
        if (combinedName.Contains("Barrel")) return "бочку";
        if (combinedName.Contains("Bumper")) return "упор рампы";
        if (combinedName.Contains("Hazard") || combinedName.Contains("Border")) return "границу бокса";
        return "препятствие";
    }
}
