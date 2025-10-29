// Algorithms/PartitionedGridAlgorithm.cs

using System.Collections.Generic;
using System.Linq; // <-- 确保你引用了 LINQ
using UnityEngine;

/// <summary>
/// (Algorithm Implementation)
/// Divides the search area into 'N' partitions (one for each drone)
/// and assigns each drone a "lawnmower" (grid sweep) path to cover its partition.
/// This algorithm prioritizes 100% coverage in a systematic way.
/// </summary>
public class PartitionedGridAlgorithm : AlgorithmBase
{
    [Header("Grid Scan Parameters")]

    [Tooltip("The fixed altitude (Y-level) for all drones to fly at.")]
    [SerializeField]
    private float flightAltitude = 20f; // 在Inspector中设置一个固定的飞行高度

    [Tooltip("The effective radius of the drone's sensor (e.g., camera range).")]
    [SerializeField]
    private float scanRadius = 10f;

    [Tooltip("How much each scan line should overlap (0.2 = 20% overlap).")]
    [SerializeField]
    [Range(0.01f, 0.9f)]
    private float scanOverlap = 0.2f;

    // --- Internal State ---

    /// <summary>
    /// Stores the unique partition (slice) of the search area for each drone.
    /// </summary>
    private Dictionary<Drone, Bounds> dronePartitions;

    /// <summary>
    /// Stores the pre-calculated queue of waypoints for each drone.
    /// </summary>
    private Dictionary<Drone, Queue<Vector3>> droneWaypoints;

    // --- State Management ---
    private HashSet<Drone> finishedDrones;
    private int totalValidDrones = 0;
    private bool isAlgorithmFinished = false;

    /// <summary>
    /// The public name of this algorithm.
    /// </summary>
    public override string AlgorithmName => "Partitioned Grid Sweep";

    /// <summary>
    /// Initializes the algorithm.
    /// This is where we calculate all partitions and generate all paths.
    /// </summary>
    public override void Initialize(List<Drone> drones, Collider searchArea)
    {
        // Call the base class Initialize (it sets up this.drones and this.searchBounds)
        base.Initialize(drones, searchArea);

        // Initialize our data structures
        dronePartitions = new Dictionary<Drone, Bounds>();
        droneWaypoints = new Dictionary<Drone, Queue<Vector3>>();
        finishedDrones = new HashSet<Drone>();
        isAlgorithmFinished = false;

        // Use a robust list of valid drones (filters out nulls)
        List<Drone> validDrones = this.drones.Where(d => d != null).ToList();
        totalValidDrones = validDrones.Count;

        if (totalValidDrones == 0)
        {
            Debug.LogError("PartitionedGridAlgorithm: No valid drones available!");
            isAlgorithmFinished = true; // Stop immediately
            return;
        }

        // 1. Divide the search area
        CalculatePartitions(validDrones);

        // 2. Generate the waypoint path for each drone's partition
        GenerateAllWaypointQueues(validDrones);

        // 3. Give each drone its *first* target
        StartAllDrones(validDrones);
    }

    /// <summary>
    /// Main loop, called every frame by the manager.
    /// This method checks if a drone has reached its target, and if so,
    /// gives it the next waypoint from its queue.
    /// </summary>
    public override void ExecuteAlgorithm()
    {
        // If algorithm is finished, do nothing.
        if (isAlgorithmFinished) return;

        // Loop only through valid drones
        foreach (Drone drone in drones.Where(d => d != null))
        {
            // Skip drones that have finished their path
            if (finishedDrones.Contains(drone))
                continue;

            // Skip drones that somehow weren't assigned a path (shouldn't happen)
            if (!droneWaypoints.ContainsKey(drone))
                continue;

            // Check if the drone has arrived at its current target
            if (drone.IsCloseToTarget())
            {
                // Drone has arrived. Does it have more waypoints?
                if (droneWaypoints[drone].Count > 0)
                {
                    // Yes: Dequeue the next waypoint and assign it
                    Vector3 nextTarget = droneWaypoints[drone].Dequeue();
                    drone.SetNewTarget(nextTarget);
                }
                else
                {
                    // No: This drone has finished its partition.
                    finishedDrones.Add(drone);
                }
            }
        }

        // Check if all drones have finished
        if (finishedDrones.Count >= totalValidDrones)
        {
            Debug.Log("Partitioned Grid Sweep: All drones have completed their paths!");
            isAlgorithmFinished = true;
            // The algorithm will now stop executing (due to the check at the top)
        }
    }

    /// <summary>
    /// Cleans up when the algorithm is stopped or switched.
    /// </summary>
    public override void OnAlgorithmEnd()
    {
        // Clear dictionaries to free memory
        dronePartitions?.Clear();
        droneWaypoints?.Clear();
        finishedDrones?.Clear();
        isAlgorithmFinished = false; // Reset state
    }

    // ===================================================================
    // --- Helper Methods ---
    // ===================================================================

    /// <summary>
    /// Slices the main 'searchBounds' along its X-axis
    /// and stores the resulting smaller Bounds for each drone.
    /// </summary>
    private void CalculatePartitions(List<Drone> validDrones)
    {
        // Partition along the X-axis.
        float totalWidth = searchBounds.size.x;
        float sliceWidth = totalWidth / validDrones.Count;
        float startX = searchBounds.min.x;

        for (int i = 0; i < validDrones.Count; i++)
        {
            Drone drone = validDrones[i];

            // Calculate the min and max X for this drone's slice
            float partitionMinX = startX + (i * sliceWidth);
            float partitionMaxX = partitionMinX + sliceWidth;

            // Create the new Bounds for this partition
            Vector3 partitionCenter = new Vector3(
                partitionMinX + (sliceWidth / 2f),
                searchBounds.center.y,
                searchBounds.center.z
            );

            Vector3 partitionSize = new Vector3(
                sliceWidth,
                searchBounds.size.y,
                searchBounds.size.z
            );

            Bounds partition = new Bounds(partitionCenter, partitionSize);
            dronePartitions.Add(drone, partition);
        }
    }

    /// <summary>
    /// Iterates through all drones and calls the waypoint generator for each.
    /// </summary>
    private void GenerateAllWaypointQueues(List<Drone> validDrones)
    {
        foreach (Drone drone in validDrones)
        {
            Bounds partition = dronePartitions[drone];
            Queue<Vector3> waypoints = GenerateWaypointsForPartition(partition);
            droneWaypoints.Add(drone, waypoints);
        }
    }

    /// <summary>
    /// Generates a "lawnmower" path for a given partition at a fixed altitude.
    /// Scans back-and-forth along the X-axis (within the partition)
    /// and steps along the Z-axis.
    /// </summary>
    /// <param name="partition">The drone's assigned search area.</param>
    /// <returns>A Queue of Vector3 waypoints.</returns>
    private Queue<Vector3> GenerateWaypointsForPartition(Bounds partition)
    {
        Queue<Vector3> waypoints = new Queue<Vector3>();

        // This is the X-axis range this drone must scan
        float xMin = partition.min.x;
        float xMax = partition.max.x;

        // This is the Z-axis range *of the whole search area*
        float zMin = searchBounds.min.z;
        float zMax = searchBounds.max.z;

        // All waypoints are at the fixed flight altitude
        float y = flightAltitude;

        // We step along the Z-axis, with a step distance based on sensor radius
        float zStep = (scanRadius * 2) * (1.0f - scanOverlap);
        if (zStep <= 0.01f) zStep = 0.01f; // Avoid divide-by-zero or infinite loop

        bool scanForward = true; // To alternate between xMin and xMax

        // Loop from zMin to zMax
        for (float z = zMin; z <= zMax; z += zStep)
        {
            if (scanForward)
            {
                // Path: (xMin, y, z) -> (xMax, y, z)
                waypoints.Enqueue(new Vector3(xMin, y, z));
                waypoints.Enqueue(new Vector3(xMax, y, z));
            }
            else
            // Scan backward
            {
                // Path: (xMax, y, z) -> (xMin, y, z)
                waypoints.Enqueue(new Vector3(xMax, y, z));
                waypoints.Enqueue(new Vector3(xMin, y, z));
            }

            scanForward = !scanForward; // Flip direction for the next line
        }
        return waypoints;
    }

    /// <summary>
    /// Gives all drones their first waypoint to get them started.
    /// </summary>
    private void StartAllDrones(List<Drone> validDrones)
    {
        foreach (Drone drone in validDrones)
        {
            if (droneWaypoints.ContainsKey(drone) && droneWaypoints[drone].Count > 0)
            {
                Vector3 firstTarget = droneWaypoints[drone].Dequeue();
                drone.SetNewTarget(firstTarget);
            }
            else
            {
                // This drone has no waypoints, mark it as finished immediately
                finishedDrones.Add(drone);
            }
        }
    }
}