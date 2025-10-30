using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 网格分区算法 - 继承 MonoBehaviour 版本
/// Partitioned Grid Algorithm - MonoBehaviour version
/// </summary>
public class PartitionedGridAlgorithm : AlgorithmBase
{
    [Header("Grid Scan Parameters")]
    [SerializeField] private float flightHeight = 20f;

    [SerializeField] private float scanRadius = 10f;

    [Header("Density Control")]
    [SerializeField]
    [Range(0.1f, 3f)]
    private float scanDensityMultiplier = 1f;

    [SerializeField]
    [Range(0.0f, 0.5f)]
    private float scanOverlap = 0.2f;

    [Tooltip("网格模式")]
    [SerializeField] private GridPattern gridPattern = GridPattern.Horizontal;

    [Header("Advanced Settings")]
    [Tooltip("Scan along with edge")]
    [SerializeField] private bool addEdgeScans = false;

    [SerializeField] private bool optimizePath = true;

    [Header("Visualization")]
    [Tooltip("show debug path in scene")]
    [SerializeField] private bool showDebugPath = true;

    [Tooltip("Show Partitions")]
    [SerializeField] private bool showPartitions = true;

    [Tooltip("Path color")]
    [SerializeField] private Color pathColor = Color.cyan;

    private Dictionary<Drone, Bounds> dronePartitions;
    private Dictionary<Drone, Queue<Vector3>> droneWaypoints;
    private Dictionary<Drone, List<Vector3>> droneCompletePaths;
    private HashSet<Drone> finishedDrones;
    private int totalValidDrones = 0;
    private bool isAlgorithmFinished = false;

    private int totalWaypoints = 0;
    private float totalPathLength = 0f;

    public enum GridPattern
    {
        Horizontal,
        Vertical,
        Diagonal,
        Spiral
    }

    public override string AlgorithmName
    {
        get { return algorithmName; }
        set { algorithmName = value; }
    }

    protected override void Awake()
    {
        base.Awake();
        algorithmName = "Partitioned Grid Sweep";
        algorithmDescription = "split scanning area into several partition，each drone start grid scan.";
    }

    public override void Initialize(List<Drone> drones, Collider searchArea)
    {
        base.Initialize(drones, searchArea);
        dronePartitions = new Dictionary<Drone, Bounds>();
        droneWaypoints = new Dictionary<Drone, Queue<Vector3>>();
        droneCompletePaths = new Dictionary<Drone, List<Vector3>>();
        finishedDrones = new HashSet<Drone>();
        isAlgorithmFinished = false;
        totalWaypoints = 0;
        totalPathLength = 0f;

        List<Drone> validDrones = this.drones.Where(d => d != null).ToList();
        totalValidDrones = validDrones.Count;

        if (totalValidDrones == 0)
        {
            Debug.LogError("❌ PartitionedGridAlgorithm: no vaild drones！");
            isAlgorithmFinished = true;
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"🚁 Initialize Algorithm: {totalValidDrones} Drons");
            Debug.Log($"📊 Setting: Radius={scanRadius}m, Density={scanDensityMultiplier}, Overlap={scanOverlap}");
        }

        
        CalculatePartitions(validDrones);
        GenerateAllWaypointQueues(validDrones);
        StartAllDrones(validDrones);

        if (showDebugInfo)
        {
            Debug.Log($"✅ PathFinish: TotalWaypoints={totalWaypoints}, Predict distance={totalPathLength:F1}m");
            Debug.Log($"📏 Average waypoints: {totalWaypoints / totalValidDrones} ");
        }
    }

    public override void ExecuteAlgorithm()
    {
        if (isAlgorithmFinished) return;

        foreach (Drone drone in drones.Where(d => d != null))
        {
            if (finishedDrones.Contains(drone))
                continue;

            if (!droneWaypoints.ContainsKey(drone))
                continue;

            if (drone.IsCloseToTarget())
            {
                if (droneWaypoints[drone].Count > 0)
                {
                    Vector3 nextTarget = droneWaypoints[drone].Dequeue();
                    drone.SetNewTarget(nextTarget);
                }
                else
                {
                    finishedDrones.Add(drone);
                    if (showDebugInfo)
                    {
                        Debug.Log($"✅ Drone: {drone.name} has finished mission");
                    }
                }
            }
        }

        if (finishedDrones.Count >= totalValidDrones)
        {
            if (showDebugInfo)
            {
                Debug.Log("🎉 All drones have finished grid searching！");
            }
            isAlgorithmFinished = true;
        }
    }

    public override void OnAlgorithmEnd()
    {
        base.OnAlgorithmEnd();

        dronePartitions?.Clear();
        droneWaypoints?.Clear();
        droneCompletePaths?.Clear();
        finishedDrones?.Clear();
        isAlgorithmFinished = false;
    }

    // ===================================================================
    // Core method
    // ===================================================================

    private void CalculatePartitions(List<Drone> validDrones)
    {
        float totalWidth = searchBounds.size.x;
        float sliceWidth = totalWidth / validDrones.Count;
        float startX = searchBounds.min.x;

        for (int i = 0; i < validDrones.Count; i++)
        {
            Drone drone = validDrones[i];

            float partitionMinX = startX + (i * sliceWidth);
            float partitionMaxX = partitionMinX + sliceWidth;

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

            if (showDebugInfo)
            {
                Debug.Log($"📦 Drone {i}: partition [{partitionMinX:F1}, {partitionMaxX:F1}]");
            }
        }
    }

    private void GenerateAllWaypointQueues(List<Drone> validDrones)
    {
        foreach (Drone drone in validDrones)
        {
            Bounds partition = dronePartitions[drone];
            Queue<Vector3> waypoints = GenerateWaypointsForPartition(partition, drone);
            droneWaypoints.Add(drone, waypoints);
        }
    }

    private Queue<Vector3> GenerateWaypointsForPartition(Bounds partition, Drone drone)
    {
        List<Vector3> pathPoints = new List<Vector3>();

        switch (gridPattern)
        {
            case GridPattern.Horizontal:
                pathPoints = GenerateHorizontalPattern(partition);
                break;
            case GridPattern.Vertical:
                pathPoints = GenerateVerticalPattern(partition);
                break;
            case GridPattern.Diagonal:
                pathPoints = GenerateDiagonalPattern(partition);
                break;
            case GridPattern.Spiral:
                pathPoints = GenerateSpiralPattern(partition);
                break;
        }

        if (optimizePath && pathPoints.Count > 2)
        {
            pathPoints = OptimizePath(pathPoints, drone.Position);
        }

        droneCompletePaths[drone] = new List<Vector3>(pathPoints);

        totalWaypoints += pathPoints.Count;
        totalPathLength += CalculatePathLength(pathPoints);

        return new Queue<Vector3>(pathPoints);
    }

    private List<Vector3> GenerateHorizontalPattern(Bounds partition)
    {
        List<Vector3> points = new List<Vector3>();

        float xMin = partition.min.x;
        float xMax = partition.max.x;
        float zMin = searchBounds.min.z;
        float zMax = searchBounds.max.z;
        float y = flightHeight;

        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap);
        float zStep = effectiveScanWidth * scanDensityMultiplier;

        if (zStep <= 0.01f) zStep = 0.01f;

        bool scanForward = true;

        if (addEdgeScans)
        {
            points.Add(new Vector3(xMin, y, zMin));
            points.Add(new Vector3(xMax, y, zMin));
        }

        for (float z = zMin; z <= zMax; z += zStep)
        {
            if (scanForward)
            {
                points.Add(new Vector3(xMin, y, z));
                points.Add(new Vector3(xMax, y, z));
            }
            else
            {
                points.Add(new Vector3(xMax, y, z));
                points.Add(new Vector3(xMin, y, z));
            }

            scanForward = !scanForward;
        }

        if (addEdgeScans && points.Count > 0)
        {
            Vector3 lastPoint = points[points.Count - 1];
            if (Mathf.Abs(lastPoint.z - zMax) > 0.1f)
            {
                points.Add(new Vector3(lastPoint.x, y, zMax));
                points.Add(new Vector3(lastPoint.x == xMin ? xMax : xMin, y, zMax));
            }
        }

        return points;
    }

    private List<Vector3> GenerateVerticalPattern(Bounds partition)
    {
        List<Vector3> points = new List<Vector3>();

        float xMin = partition.min.x;
        float xMax = partition.max.x;
        float zMin = searchBounds.min.z;
        float zMax = searchBounds.max.z;
        float y = flightHeight;

        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap);
        float xStep = effectiveScanWidth * scanDensityMultiplier;

        if (xStep <= 0.01f) xStep = 0.01f;

        bool scanForward = true;

        for (float x = xMin; x <= xMax; x += xStep)
        {
            if (scanForward)
            {
                points.Add(new Vector3(x, y, zMin));
                points.Add(new Vector3(x, y, zMax));
            }
            else
            {
                points.Add(new Vector3(x, y, zMax));
                points.Add(new Vector3(x, y, zMin));
            }

            scanForward = !scanForward;
        }

        return points;
    }

    private List<Vector3> GenerateDiagonalPattern(Bounds partition)
    {
        List<Vector3> points = new List<Vector3>();

        float xMin = partition.min.x;
        float xMax = partition.max.x;
        float zMin = searchBounds.min.z;
        float zMax = searchBounds.max.z;
        float y = flightHeight;

        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap);
        float step = effectiveScanWidth * scanDensityMultiplier;
        if (step <= 0.01f) step = 0.01f;

        float partitionWidth = xMax - xMin;
        float partitionDepth = zMax - zMin;
        float diagonalLength = Mathf.Sqrt(partitionWidth * partitionWidth + partitionDepth * partitionDepth);

        int numLines = Mathf.CeilToInt(diagonalLength / step);

        bool leftToRight = true;

        for (int i = 0; i <= numLines; i++)
        {
            float offset = i * step;

            if (leftToRight)
            {
              
                Vector3 start = new Vector3(xMin, y, zMin + offset);
                Vector3 end = new Vector3(xMin + offset, y, zMin);

                start = ClampPointToPartition(start, partition);
                end = ClampPointToPartition(end, partition);

                if (offset <= partitionDepth)
                {
                    points.Add(new Vector3(xMin, y, zMin + offset));
                    points.Add(new Vector3(xMin + Mathf.Min(offset, partitionWidth), y, zMin));
                }
                else
                {
                    float excess = offset - partitionDepth;
                    points.Add(new Vector3(xMin + excess, y, zMax));
                    points.Add(new Vector3(xMax, y, zMax - Mathf.Min(excess, partitionDepth)));
                }
            }
            else
            {
                if (offset <= partitionDepth)
                {
                    points.Add(new Vector3(xMin + Mathf.Min(offset, partitionWidth), y, zMin));
                    points.Add(new Vector3(xMin, y, zMin + offset));
                }
                else
                {
                    float excess = offset - partitionDepth;
                    points.Add(new Vector3(xMax, y, zMax - Mathf.Min(excess, partitionDepth)));
                    points.Add(new Vector3(xMin + excess, y, zMax));
                }
            }

            leftToRight = !leftToRight;
        }

        return points;
    }

    private Vector3 ClampPointToPartition(Vector3 point, Bounds partition)
    {
        return new Vector3(
            Mathf.Clamp(point.x, partition.min.x, partition.max.x),
            point.y,
            Mathf.Clamp(point.z, searchBounds.min.z, searchBounds.max.z)
        );
    }

    private List<Vector3> GenerateSpiralPattern(Bounds partition)
    {
        List<Vector3> points = new List<Vector3>();

        float xMin = partition.min.x;
        float xMax = partition.max.x;
        float zMin = searchBounds.min.z;
        float zMax = searchBounds.max.z;
        float y = flightHeight;

        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap);
        float step = effectiveScanWidth * scanDensityMultiplier;
        if (step <= 0.01f) step = 0.01f;

        float currentXMin = xMin;
        float currentXMax = xMax;
        float currentZMin = zMin;
        float currentZMax = zMax;

        bool isFirstLayer = true;

        while (currentXMax - currentXMin > step && currentZMax - currentZMin > step)
        {
            if (isFirstLayer)
            {
       
                points.Add(new Vector3(currentXMin, y, currentZMin));
                points.Add(new Vector3(currentXMax, y, currentZMin));

              
                points.Add(new Vector3(currentXMax, y, currentZMax));

              
                points.Add(new Vector3(currentXMin, y, currentZMax));

        
                points.Add(new Vector3(currentXMin, y, currentZMin + step));

                isFirstLayer = false;
            }
            else
            {
              
                points.Add(new Vector3(currentXMin, y, currentZMin));
                points.Add(new Vector3(currentXMax, y, currentZMin));
                points.Add(new Vector3(currentXMax, y, currentZMax));
                points.Add(new Vector3(currentXMin, y, currentZMax));

                if (currentZMax - currentZMin > step * 2)
                {
                    points.Add(new Vector3(currentXMin, y, currentZMin + step));
                }
            }
            currentXMin += step;
            currentXMax -= step;
            currentZMin += step;
            currentZMax -= step;
        }

        if (currentXMax > currentXMin && currentZMax > currentZMin)
        {
            Vector3 center = new Vector3(
                (currentXMin + currentXMax) / 2f,
                y,
                (currentZMin + currentZMax) / 2f
            );
            points.Add(center);
        }

        return points;
    }

    private List<Vector3> OptimizePath(List<Vector3> originalPath, Vector3 startPosition)
    {
        if (originalPath.Count == 0) return originalPath;

        int closestIndex = 0;
        float minDistance = Vector3.Distance(startPosition, originalPath[0]);

        for (int i = 1; i < originalPath.Count; i++)
        {
            float dist = Vector3.Distance(startPosition, originalPath[i]);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }

        if (closestIndex > 0)
        {
            List<Vector3> optimized = new List<Vector3>();
            for (int i = closestIndex; i < originalPath.Count; i++)
            {
                optimized.Add(originalPath[i]);
            }
            for (int i = 0; i < closestIndex; i++)
            {
                optimized.Add(originalPath[i]);
            }
            return optimized;
        }

        return originalPath;
    }

    private float CalculatePathLength(List<Vector3> path)
    {
        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector3.Distance(path[i], path[i + 1]);
        }
        return length;
    }

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
                finishedDrones.Add(drone);
            }
        }
    }

    // ===================================================================
    // 公共接口
    // ===================================================================

    /// <summary>
    /// 根据时间t在路径上采样位置
    /// Sample position along the drone's path at time t
    /// </summary>
    /// <param name="t">Normalize property (0-1)，0=start，1=100%=end</param>

    public Vector3 SamplePosition(float t, int droneID)
    {
        t = Mathf.Clamp01(t);

        if (droneID < 0 || droneID >= drones.Count)
        {
            Debug.LogError($"❌ SamplePosition: un vaild droneID={droneID}，vaild range is 0-{drones.Count - 1}");
            return Vector3.zero;
        }

        Drone drone = drones[droneID];
        if (drone == null)
        {
            Debug.LogError($"❌ SamplePosition: droneID={droneID} is null");
            return Vector3.zero;
        }

   
        if (!droneCompletePaths.ContainsKey(drone) || droneCompletePaths[drone].Count == 0)
        {
            Debug.LogWarning($"⚠️ SamplePosition: droneID={droneID} no data of path,return  current position");
            return drone.Position;
        }

        List<Vector3> path = droneCompletePaths[drone];
        if (path.Count == 1)
        {
            return path[0];
        }
        if (t <= 0f)
        {
            return path[0];
        }

        if (t >= 1f)
        {
            return path[path.Count - 1];
        }

        float totalLength = 0f;
        List<float> cumulativeLengths = new List<float> { 0f }; // 第一个点的累积长度为0

        for (int i = 0; i < path.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(path[i], path[i + 1]);
            totalLength += segmentLength;
            cumulativeLengths.Add(totalLength);
        }

        float targetDistance = totalLength * t;


        for (int i = 0; i < cumulativeLengths.Count - 1; i++)
        {
            if (targetDistance >= cumulativeLengths[i] && targetDistance <= cumulativeLengths[i + 1])
            {
                Vector3 startPoint = path[i];
                Vector3 endPoint = path[i + 1];

                float segmentStartDist = cumulativeLengths[i];
                float segmentEndDist = cumulativeLengths[i + 1];
                float segmentLength = segmentEndDist - segmentStartDist;

  
                float segmentT = 0f;
                if (segmentLength > 0.001f) 
                {
                    segmentT = (targetDistance - segmentStartDist) / segmentLength;
                }

                return Vector3.Lerp(startPoint, endPoint, segmentT);
            }
        }


        return path[path.Count - 1];
    }

    public List<Vector3> GetDronePath(int droneID)
    {
        if (droneID < 0 || droneID >= drones.Count || drones[droneID] == null)
        {
            return new List<Vector3>();
        }

        Drone drone = drones[droneID];
        if (droneCompletePaths.ContainsKey(drone))
        {
            return new List<Vector3>(droneCompletePaths[drone]); // 返回副本
        }

        return new List<Vector3>();
    }

    public float GetDronePathLength(int droneID)
    {
        List<Vector3> path = GetDronePath(droneID);
        if (path.Count < 2) return 0f;

        float length = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            length += Vector3.Distance(path[i], path[i + 1]);
        }
        return length;
    }

    public void SetScanDensity(float density)
    {
        scanDensityMultiplier = Mathf.Clamp(density, 0.1f, 3f);
        if (showDebugInfo)
        {
            Debug.Log($"🔄 Density has updated: {scanDensityMultiplier}");
        }
    }

    public float GetScanDensity()
    {
        return scanDensityMultiplier;
    }

    public float GetTotalPathLength()
    {
        return totalPathLength;
    }

    public float GetProgress()
    {
        if (totalValidDrones == 0) return 1f;
        return (float)finishedDrones.Count / totalValidDrones;
    }

    // ===================================================================
    // Visualize
    // ===================================================================

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!Application.isPlaying) return;

        Color[] colors = new Color[]
        {
            Color.red,              
            Color.green,            
            Color.blue,             
            Color.yellow,           
            Color.cyan,             
            Color.magenta,          
            new Color(1, 0.5f, 0),  
            new Color(0.5f, 0, 1),  
            new Color(0, 1, 0.5f), 
            new Color(1, 0, 0.5f), 
            new Color(0.5f, 1, 0),  
            new Color(0, 0.5f, 1)   
        };

     
        if (showPartitions && dronePartitions != null)
        {
            int colorIndex = 0;

            foreach (var kvp in dronePartitions)
            {
                Color droneColor = colors[colorIndex % colors.Length];
                Gizmos.color = new Color(droneColor.r, droneColor.g, droneColor.b, 0.5f); 
                Gizmos.DrawWireCube(kvp.Value.center, kvp.Value.size);
                colorIndex++;
            }
        }


        if (showDebugPath && droneCompletePaths != null)
        {
            int colorIndex = 0;

            foreach (var kvp in droneCompletePaths)
            {
                Drone drone = kvp.Key;
                List<Vector3> path = kvp.Value;
                if (path.Count < 2) continue;
                Color droneColor = colors[colorIndex % colors.Length];
                Gizmos.color = droneColor;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Gizmos.DrawLine(path[i], path[i + 1]);
                }
                Gizmos.color = new Color(droneColor.r, droneColor.g, droneColor.b, 0.8f);
                Gizmos.DrawWireSphere(path[0], 2f);
                if (drone != null && !finishedDrones.Contains(drone))
                {
                    Gizmos.color = droneColor;
                    Gizmos.DrawSphere(drone.Position, 1.5f);
                }

                colorIndex++;
            }
        }
    }
}