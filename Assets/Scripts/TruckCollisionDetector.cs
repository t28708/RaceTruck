using System.Collections.Generic;
using UnityEngine;

public class TruckCollisionDetector : MonoBehaviour
{
    private float lastCrashTime = -1f;
    private const float CrashCooldown = 0.35f;

    // Track active colliders touching this part so block is not cleared prematurely
    private readonly HashSet<Collider2D> touchingColliders = new HashSet<Collider2D>();

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

        string obstacleName = FormatObstacleName(other.name, other.transform);

        // Determine whether impact was while moving forward or backward
        bool forwardImpact = true;
        if (TruckController.Instance != null)
        {
            float speed = TruckController.Instance.CurrentSpeed;
            if (Mathf.Abs(speed) > 0.02f)
            {
                forwardImpact = (speed > 0f);
            }
            else if (TruckController.Instance.IsBrakePedalPressed)
            {
                forwardImpact = false;
            }
            else if (TruckController.Instance.IsGasPedalPressed)
            {
                forwardImpact = true;
            }
            else
            {
                forwardImpact = (gameObject == TruckController.Instance.gameObject);
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

    private string FormatObstacleName(string rawName, Transform otherTransform)
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
