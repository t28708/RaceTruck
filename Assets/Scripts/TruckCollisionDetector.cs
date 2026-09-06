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

            hasObstacle = true;
            if (firstObstacle == null) firstObstacle = other;
            currentObstacles.Add(other);
            touchingColliders.Add(other);
        }

        // Clean up colliders no longer overlapping
        touchingColliders.RemoveWhere(col => col == null || !col.enabled || !currentObstacles.Contains(col));

        if (hasObstacle && firstObstacle != null)
        {
            HandleCollision(firstObstacle.gameObject);
        }
        else if (touchingColliders.Count == 0 && TruckController.Instance != null)
        {
            TruckController.Instance.ClearBlock();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayerVehicle(other.gameObject)) return;
        touchingColliders.Add(other);
        HandleCollision(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (IsPlayerVehicle(other.gameObject)) return;
        touchingColliders.Add(other);

        // Keep truck stopped if it attempts to push further into the obstacle
        if (TruckController.Instance != null && Mathf.Abs(TruckController.Instance.CurrentSpeed) > 0.02f)
        {
            HandleCollision(other.gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        touchingColliders.Remove(other);
        if (touchingColliders.Count == 0 && TruckController.Instance != null)
        {
            TruckController.Instance.ClearBlock();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsPlayerVehicle(collision.gameObject)) return;
        HandleCollision(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsPlayerVehicle(collision.gameObject)) return;
        if (TruckController.Instance != null && Mathf.Abs(TruckController.Instance.CurrentSpeed) > 0.02f)
        {
            HandleCollision(collision.gameObject);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (TruckController.Instance != null)
        {
            TruckController.Instance.ClearBlock();
        }
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

    private void HandleCollision(GameObject other)
    {
        if (IsPlayerVehicle(other)) return;

        string obstacleName = FormatObstacleNameStatic(other.name, other.transform);

        // Determine whether impact was while moving forward or backward
        bool forwardImpact = true;
        if (TruckController.Instance != null)
        {
            float speed = TruckController.Instance.CurrentSpeed;
            if (Mathf.Abs(speed) > 0.01f)
            {
                forwardImpact = (speed > 0f);
            }
            else if (TruckController.Instance.IsBlockedForward)
            {
                // Already blocked forward: maintain forward block so reverse can escape
                forwardImpact = true;
            }
            else if (TruckController.Instance.IsBlockedReverse)
            {
                // Already blocked reverse: maintain reverse block so forward can escape
                forwardImpact = false;
            }
            else
            {
                // Determine by relative local position of the obstacle
                Vector2 localObstaclePos = transform.InverseTransformPoint(other.transform.position);
                forwardImpact = (localObstaclePos.y >= 0f);
            }

            TruckController.Instance.OnCrash(obstacleName, forwardImpact);
        }

        // Trigger visual screen shake, metal impact sound, spark burst, and "БУХ!" UI banner
        if (Time.time - lastCrashTime >= CrashCooldown)
        {
            lastCrashTime = Time.time;
            if (TruckCrashEffect.Instance != null)
            {
                TruckCrashEffect.Instance.TriggerCrash(obstacleName, transform.position);
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
