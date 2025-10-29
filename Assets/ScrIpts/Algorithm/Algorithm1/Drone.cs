// Drone.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a single drone agent in the swarm.
/// This class handles its own movement, physics, and local sensing.
/// It is "commanded" by the active algorithm in the SwarmAlgorithmManager.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Drone : MonoBehaviour
{
    [Header("Movement Parameters")]
    [Tooltip("Maximum flight speed.")]
    [SerializeField]
    private float maxSpeed = 15f;

    [Tooltip("How fast the drone can turn to face its target.")]
    [SerializeField]
    private float rotationSpeed = 8f;

    [Tooltip("The distance at which the drone is considered 'at' its target.")]
    [SerializeField]
    private float targetReachedThreshold = 1.5f;

    [Header("Sensing Parameters (for Boids, etc.)")]
    [Tooltip("How far the drone can 'see' other drones.")]
    [SerializeField]
    private float sensorRadius = 10f;

    [Tooltip("The LayerMask that contains ONLY other drones.")]
    [SerializeField]
    private LayerMask droneLayer; // IMPORTANT: Set this in the Inspector!

    // --- Private State ---
    private Rigidbody rb;
    private Vector3 currentTarget;
    private bool isMovingToTarget = false; // Flag to enable/disable target-seeking logic

    // --- Public Properties (Read-only for algorithms) ---

    /// <summary>
    /// Gets the current velocity of the drone.
    /// </summary>
    public Vector3 Velocity => rb.linearVelocity;

    /// <summary>
    /// Gets the current 3D position of the drone.
    /// </summary>
    public Vector3 Position => transform.position;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Drone prefab is missing a Rigidbody!", this);
        }

        // Drones shouldn't be affected by Unity's gravity
        rb.useGravity = false;

        // Initialize target to current position
        currentTarget = transform.position;
    }

    void FixedUpdate()
    {
        // This block handles automated movement *towards* a target.
        // It is only active if an algorithm calls SetNewTarget().
        if (isMovingToTarget)
        {
            HandleTargetMovement();
        }
        EnforceSpeedAndFlightLevel();
        // Regardless of mode, enforce the speed limit.
        EnforceSpeedLimit();
    }

    /// <summary>
    /// The main physics logic for steering towards the 'currentTarget'.
    /// </summary>
    private void HandleTargetMovement()
    {
        if (IsCloseToTarget())
        {
            // Arrived at target
            rb.linearVelocity = Vector3.zero; // Stop
            isMovingToTarget = false;
            return;
        }

        // 1. Calculate direction to target
        Vector3 directionToTarget = (currentTarget - Position).normalized;

        // 2. Calculate steering force (Desired Velocity - Current Velocity)
        Vector3 desiredVelocity = directionToTarget * maxSpeed;
        Vector3 steeringForce = desiredVelocity - Velocity;

        rb.AddForce(steeringForce);

        // 3. Rotate to face velocity (i.e., where it's going)
        if (Velocity.sqrMagnitude > 0.1f) // Avoid looking at (0,0,0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(Velocity);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
        if (Velocity.sqrMagnitude > 0.1f) // Avoid looking at (0,0,0)
        {
            // --- (关键修复!) ---
            // 我们只关心水平面上的旋转 (Y-axis rotation/yaw)
            // 创建一个“扁平”的速度矢量，忽略Y轴
            Vector3 flatVelocity = new Vector3(Velocity.x, 0, Velocity.z);

            // 仅当水平速度足够大时才旋转
            if (flatVelocity.sqrMagnitude > 0.1f)
            {
                // 使用这个扁平的矢量来计算目标旋转
                Quaternion targetRotation = Quaternion.LookRotation(flatVelocity);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            }
            // --- 结束修复 ---
        }
    }
    /// <summary>
    /// (方法名已更改)
    /// Clamps the rigidbody's velocity to the maxSpeed
    /// AND enforces level flight by damping vertical velocity.
    /// </summary>
    private void EnforceSpeedAndFlightLevel()
    {
        Vector3 currentVelocity = rb.linearVelocity;

        // 1. 限制垂直速度 (Y 轴)
        // 施加一个阻尼力来阻止它上下移动
        // (这是帮助无人机保持在 flightAltitude 的一种简单方法)
        float verticalDamp = 0.5f; // 调整这个值
        currentVelocity.y *= (1.0f - verticalDamp * Time.fixedDeltaTime);

        // 2. 限制总速度
        if (currentVelocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            currentVelocity = currentVelocity.normalized * maxSpeed;
        }

        rb.linearVelocity = currentVelocity;
    }
    /// <summary>
    /// Clamps the rigidbody's velocity to the maxSpeed.
    /// </summary>
    private void EnforceSpeedLimit()
    {
        if (Velocity.sqrMagnitude > maxSpeed * maxSpeed)
        {
            rb.linearVelocity = Velocity.normalized * maxSpeed;
        }
    }

    // ===================================================================
    // --- PUBLIC API (These are the methods your Algorithms will call) ---
    // ===================================================================

    /// <summary>
    /// (COMMAND) Tells the drone to fly to a specific 3D point.
    /// Used by Grid, RandomWalk, or target-seeking algorithms.
    /// </summary>
    /// <param name="newTarget">The world-space position to fly to.</param>
    public void SetNewTarget(Vector3 newTarget)
    {
        currentTarget = newTarget;
        isMovingToTarget = true; // Activates the logic in FixedUpdate
    }

    /// <summary>
    /// (COMMAND) Applies a raw physics force to the drone.
    /// Used by Boids/Flocking algorithm.
    /// </summary>
    /// <param name="force">The force vector to apply.</param>
    public void ApplyForce(Vector3 force)
    {
        // When applying Boids forces, we are in "direct control" mode,
        // so we disable the automated target-seeking logic.
        isMovingToTarget = false;

        rb.AddForce(force);
    }

    /// <summary>
    /// (SENSOR) Checks if the drone has reached its 'currentTarget'.
    /// </summary>
    public bool IsCloseToTarget()
    {
        return Vector3.Distance(Position, currentTarget) < targetReachedThreshold;
    }

    /// <summary>
    /// (SENSOR) Finds all other drones within the sensor radius.
    /// CRITICAL for the Boids algorithm.
    /// </summary>
    /// <returns>A list of nearby Drone components.</returns>
    public List<Drone> GetNeighbors()
    {
        List<Drone> neighbors = new List<Drone>();

        // This is the most efficient way to find nearby objects
        Collider[] hits = Physics.OverlapSphere(Position, sensorRadius, droneLayer);

        foreach (var hit in hits)
        {
            // Don't add ourselves to the list
            if (hit.gameObject == this.gameObject)
                continue;

            // Try to get the Drone component and add it
            Drone neighbor = hit.GetComponent<Drone>();
            if (neighbor != null)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }
}