using UnityEngine;
using System.Collections.Generic;

public class DroneCollisionCheck : MonoBehaviour
{
    [Header("Repulsion Settings")]
    [Tooltip("The critical distance (radius) at which repulsion force begins.")]
    public float repulsionRadius = 5f;
    [Tooltip("How strongly the drone pushes away from nearby objects.")]
    public float repulsionStrength = 5f;

    private List<Transform> _nearbyObstacles = new List<Transform>();

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle") || other.CompareTag("Drone"))
        {
            if (other.transform == transform)
            {
                return;
            }

            if (!_nearbyObstacles.Contains(other.transform))
            {
                _nearbyObstacles.Add(other.transform);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Obstacle") || other.CompareTag("Drone"))
        {
            _nearbyObstacles.Remove(other.transform);
        }
    }

    /// <summary>
    /// Calculates a net velocity vector pointing away from all targets currently inside the RepulsionRadius.
    /// </summary>
    /// <param name="currentPosition">The drone's current world position.</param>
    /// <returns>A Vector3 representing the velocity correction.</returns>
    public Vector3 GetRepulsionVector(Vector3 currentPosition)
    {
        Vector3 totalRepulsion = Vector3.zero;

        for (int i = _nearbyObstacles.Count - 1; i >= 0; i--)
        {
            Transform nearbyTarget = _nearbyObstacles[i];

            if (nearbyTarget == null)
            {
                _nearbyObstacles.RemoveAt(i);
                continue;
            }

            Vector3 direction = currentPosition - nearbyTarget.position;
            float distance = direction.magnitude;

            if (distance < repulsionRadius)
            {
                // Calculate an inverse-distance force (stronger when closer)
                // (1 - (distance / RepulsionRadius)) yields 1 when distance=0, and 0 when distance=RepulsionRadius
                float falloffFactor = Mathf.Clamp01(1 - (distance / repulsionRadius));
                float forceMagnitude = falloffFactor * repulsionStrength;

                totalRepulsion += direction.normalized * forceMagnitude;
            }
        }

        totalRepulsion.y = 0;

        return totalRepulsion;

    }
}
