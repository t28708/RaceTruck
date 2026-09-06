using UnityEngine;

public class TruckCollisionDetector : MonoBehaviour
{
    private float lastCrashTime = -1f;
    private const float CrashCooldown = 0.4f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (TruckController.Instance != null)
        {
            TruckController.Instance.ClearBlock();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (TruckController.Instance != null)
        {
            TruckController.Instance.ClearBlock();
        }
    }

    private void HandleCollision(GameObject other)
    {
        // Don't collide between Tractor and Trailer parts
        if (other.name == "Tractor" || other.name == "Trailer" || 
            other.transform.IsChildOf(transform.root) || other.transform.root == transform.root)
        {
            return;
        }

        if (Time.time - lastCrashTime < CrashCooldown) return;
        lastCrashTime = Time.time;

        string obstacleName = FormatObstacleName(other.name);

        // Determine whether the rig was moving forward or backward
        bool forwardImpact = true;
        if (TruckController.Instance != null)
        {
            forwardImpact = TruckController.Instance.CurrentSpeed >= -0.05f;
            TruckController.Instance.OnCrash(obstacleName, forwardImpact);
        }

        // Trigger visual screen shake, metal impact sound, spark burst, and "БУХ!" UI banner
        if (TruckCrashEffect.Instance != null)
        {
            TruckCrashEffect.Instance.TriggerCrash(obstacleName, transform.position);
        }
    }

    private string FormatObstacleName(string rawName)
    {
        if (rawName.Contains("Cone")) return "конус";
        if (rawName.Contains("Barrier") || rawName.Contains("Wall")) return "стену";
        if (rawName.Contains("Barrel")) return "бочку";
        if (rawName.Contains("Bumper")) return "упор рампы";
        return "препятствие";
    }
}
