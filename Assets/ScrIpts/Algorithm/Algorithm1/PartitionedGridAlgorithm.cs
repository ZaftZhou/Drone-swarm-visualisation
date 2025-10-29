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
    [Tooltip("固定飞行高度")]
    [SerializeField] private float flightAltitude = 20f;

    [Tooltip("无人机传感器有效半径")]
    [SerializeField] private float scanRadius = 10f;

    [Header("Density Control 密度控制")]
    [Tooltip("扫描密度 - 值越小越密集 (0.1-3.0)")]
    [SerializeField]
    [Range(0.1f, 3f)]
    private float scanDensityMultiplier = 1f;

    [Tooltip("扫描线重叠率 (0-0.5)")]
    [SerializeField]
    [Range(0.0f, 0.5f)]
    private float scanOverlap = 0.2f;

    [Tooltip("网格模式")]
    [SerializeField] private GridPattern gridPattern = GridPattern.Horizontal;

    [Header("Advanced Settings")]
    [Tooltip("沿边缘添加额外扫描")]
    [SerializeField] private bool addEdgeScans = false;

    [Tooltip("优化路径（减少转向）")]
    [SerializeField] private bool optimizePath = true;

    [Header("Visualization")]
    [Tooltip("在Scene视图中显示路径")]
    [SerializeField] private bool showDebugPath = true;

    [Tooltip("显示分区边界")]
    [SerializeField] private bool showPartitions = true;

    [Tooltip("路径颜色")]
    [SerializeField] private Color pathColor = Color.cyan;

    // 内部状态
    private Dictionary<Drone, Bounds> dronePartitions;
    private Dictionary<Drone, Queue<Vector3>> droneWaypoints;
    private Dictionary<Drone, List<Vector3>> droneCompletePaths;
    private HashSet<Drone> finishedDrones;
    private int totalValidDrones = 0;
    private bool isAlgorithmFinished = false;

    // 统计信息
    private int totalWaypoints = 0;
    private float totalPathLength = 0f;

    public enum GridPattern
    {
        Horizontal,
        Vertical,
        Diagonal,
        Spiral
    }

    // 覆盖算法名称
    public override string AlgorithmName
    {
        get { return algorithmName; }
        set { algorithmName = value; }
    }

    protected override void Awake()
    {
        base.Awake();
        algorithmName = "Partitioned Grid Sweep";
        algorithmDescription = "将搜索区域分区，每架无人机进行网格扫描。支持密度控制和多种扫描模式。";
    }

    public override void Initialize(List<Drone> drones, Collider searchArea)
    {
        base.Initialize(drones, searchArea);

        // 初始化数据结构
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
            Debug.LogError("❌ PartitionedGridAlgorithm: 没有有效的无人机！");
            isAlgorithmFinished = true;
            return;
        }

        if (showDebugInfo)
        {
            Debug.Log($"🚁 初始化网格算法: {totalValidDrones} 架无人机");
            Debug.Log($"📊 扫描参数: 半径={scanRadius}m, 密度={scanDensityMultiplier}, 重叠={scanOverlap}");
        }

        // 1. 划分搜索区域
        CalculatePartitions(validDrones);

        // 2. 生成路径点队列
        GenerateAllWaypointQueues(validDrones);

        // 3. 启动所有无人机
        StartAllDrones(validDrones);

        if (showDebugInfo)
        {
            Debug.Log($"✅ 路径生成完成: 总路径点={totalWaypoints}, 预计总距离={totalPathLength:F1}m");
            Debug.Log($"📏 平均每架无人机: {totalWaypoints / totalValidDrones} 个路径点");
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
                        Debug.Log($"✅ 无人机 {drone.name} 完成搜索任务");
                    }
                }
            }
        }

        if (finishedDrones.Count >= totalValidDrones)
        {
            if (showDebugInfo)
            {
                Debug.Log("🎉 所有无人机已完成网格搜索！");
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
    // 核心算法方法
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
                Debug.Log($"📦 无人机 {i}: 分区 [{partitionMinX:F1}, {partitionMaxX:F1}]");
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
        float y = flightAltitude;

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
        float y = flightAltitude;

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
        float y = flightAltitude;

        // 计算对角线扫描的步进
        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap);
        float step = effectiveScanWidth * scanDensityMultiplier;
        if (step <= 0.01f) step = 0.01f;

        float partitionWidth = xMax - xMin;
        float partitionDepth = zMax - zMin;
        float diagonalLength = Mathf.Sqrt(partitionWidth * partitionWidth + partitionDepth * partitionDepth);

        // 计算需要多少条对角线
        int numLines = Mathf.CeilToInt(diagonalLength / step);

        // 45度角的对角线扫描
        bool leftToRight = true;

        for (int i = 0; i <= numLines; i++)
        {
            float offset = i * step;

            if (leftToRight)
            {
                // 从左下到右上的对角线
                Vector3 start = new Vector3(xMin, y, zMin + offset);
                Vector3 end = new Vector3(xMin + offset, y, zMin);

                // 限制在分区范围内
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
                // 从右上到左下的对角线（反向）
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

    // 辅助方法：将点限制在分区范围内
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
        float y = flightAltitude;

        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap);
        float step = effectiveScanWidth * scanDensityMultiplier;
        if (step <= 0.01f) step = 0.01f;

        // 从外向内的矩形螺旋
        float currentXMin = xMin;
        float currentXMax = xMax;
        float currentZMin = zMin;
        float currentZMax = zMax;

        bool isFirstLayer = true;

        while (currentXMax - currentXMin > step && currentZMax - currentZMin > step)
        {
            if (isFirstLayer)
            {
                // 第一层：从左下角开始
                // 底边：从左到右
                points.Add(new Vector3(currentXMin, y, currentZMin));
                points.Add(new Vector3(currentXMax, y, currentZMin));

                // 右边：从下到上
                points.Add(new Vector3(currentXMax, y, currentZMax));

                // 顶边：从右到左
                points.Add(new Vector3(currentXMin, y, currentZMax));

                // 左边：从上到下（回到接近起点）
                points.Add(new Vector3(currentXMin, y, currentZMin + step));

                isFirstLayer = false;
            }
            else
            {
                // 后续层
                // 底边
                points.Add(new Vector3(currentXMin, y, currentZMin));
                points.Add(new Vector3(currentXMax, y, currentZMin));

                // 右边
                points.Add(new Vector3(currentXMax, y, currentZMax));

                // 顶边
                points.Add(new Vector3(currentXMin, y, currentZMax));

                // 左边（不完全闭合，为了连接到下一圈）
                if (currentZMax - currentZMin > step * 2)
                {
                    points.Add(new Vector3(currentXMin, y, currentZMin + step));
                }
            }

            // 向内收缩
            currentXMin += step;
            currentXMax -= step;
            currentZMin += step;
            currentZMax -= step;
        }

        // 添加中心点（如果还有空间）
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
    /// <param name="t">归一化时间 (0-1)，0=起点，1=终点</param>
    /// <param name="droneID">无人机索引</param>
    /// <returns>路径上t时刻的位置</returns>
    public Vector3 SamplePosition(float t, int droneID)
    {
        // 限制t在[0,1]范围内
        t = Mathf.Clamp01(t);

        // 验证droneID
        if (droneID < 0 || droneID >= drones.Count)
        {
            Debug.LogError($"❌ SamplePosition: 无效的 droneID={droneID}，有效范围是 0-{drones.Count - 1}");
            return Vector3.zero;
        }

        Drone drone = drones[droneID];
        if (drone == null)
        {
            Debug.LogError($"❌ SamplePosition: droneID={droneID} 的无人机为空");
            return Vector3.zero;
        }

        // 检查是否有路径数据
        if (!droneCompletePaths.ContainsKey(drone) || droneCompletePaths[drone].Count == 0)
        {
            Debug.LogWarning($"⚠️ SamplePosition: droneID={droneID} 没有路径数据，返回当前位置");
            return drone.Position;
        }

        List<Vector3> path = droneCompletePaths[drone];

        // 特殊情况：只有一个点
        if (path.Count == 1)
        {
            return path[0];
        }

        // 特殊情况：t=0 返回起点
        if (t <= 0f)
        {
            return path[0];
        }

        // 特殊情况：t=1 返回终点
        if (t >= 1f)
        {
            return path[path.Count - 1];
        }

        // 计算路径总长度和每段的累积长度
        float totalLength = 0f;
        List<float> cumulativeLengths = new List<float> { 0f }; // 第一个点的累积长度为0

        for (int i = 0; i < path.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(path[i], path[i + 1]);
            totalLength += segmentLength;
            cumulativeLengths.Add(totalLength);
        }

        // 根据t计算目标距离
        float targetDistance = totalLength * t;

        // 找到目标距离所在的路径段
        for (int i = 0; i < cumulativeLengths.Count - 1; i++)
        {
            if (targetDistance >= cumulativeLengths[i] && targetDistance <= cumulativeLengths[i + 1])
            {
                // 在第i段和第i+1段之间
                Vector3 startPoint = path[i];
                Vector3 endPoint = path[i + 1];

                float segmentStartDist = cumulativeLengths[i];
                float segmentEndDist = cumulativeLengths[i + 1];
                float segmentLength = segmentEndDist - segmentStartDist;

                // 计算在这一段内的插值参数
                float segmentT = 0f;
                if (segmentLength > 0.001f) // 避免除零
                {
                    segmentT = (targetDistance - segmentStartDist) / segmentLength;
                }

                // 在两点之间线性插值
                return Vector3.Lerp(startPoint, endPoint, segmentT);
            }
        }

        // 兜底：返回终点（理论上不应该到这里）
        return path[path.Count - 1];
    }

    /// <summary>
    /// 获取指定无人机的完整路径（用于外部可视化）
    /// </summary>
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

    /// <summary>
    /// 获取指定无人机路径的总长度
    /// </summary>
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
            Debug.Log($"🔄 扫描密度已更新: {scanDensityMultiplier}");
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
    // 可视化
    // ===================================================================

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        if (!Application.isPlaying) return;

        // 定义丰富的颜色数组 - 支持更多无人机
        Color[] colors = new Color[]
        {
            Color.red,              // 红色
            Color.green,            // 绿色
            Color.blue,             // 蓝色
            Color.yellow,           // 黄色
            Color.cyan,             // 青色
            Color.magenta,          // 洋红色
            new Color(1, 0.5f, 0),  // 橙色
            new Color(0.5f, 0, 1),  // 紫色
            new Color(0, 1, 0.5f),  // 青绿色
            new Color(1, 0, 0.5f),  // 粉红色
            new Color(0.5f, 1, 0),  // 黄绿色
            new Color(0, 0.5f, 1)   // 天蓝色
        };

        // 绘制分区边界 - 每个无人机的分区使用对应的颜色
        if (showPartitions && dronePartitions != null)
        {
            int colorIndex = 0;

            foreach (var kvp in dronePartitions)
            {
                Color droneColor = colors[colorIndex % colors.Length];
                Gizmos.color = new Color(droneColor.r, droneColor.g, droneColor.b, 0.5f); // 半透明
                Gizmos.DrawWireCube(kvp.Value.center, kvp.Value.size);
                colorIndex++;
            }
        }

        // 🎨 绘制完整路径 - 每个无人机使用独立的颜色！
        if (showDebugPath && droneCompletePaths != null)
        {
            int colorIndex = 0;

            foreach (var kvp in droneCompletePaths)
            {
                Drone drone = kvp.Key;
                List<Vector3> path = kvp.Value;
                if (path.Count < 2) continue;

                // 为每个无人机分配独特的颜色
                Color droneColor = colors[colorIndex % colors.Length];
                Gizmos.color = droneColor;

                // 绘制路径线段
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Gizmos.DrawLine(path[i], path[i + 1]);
                }

                // 绘制起始点（较大的空心球体）
                Gizmos.color = new Color(droneColor.r, droneColor.g, droneColor.b, 0.8f);
                Gizmos.DrawWireSphere(path[0], 2f);

                // 绘制当前无人机位置（实心球体）
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