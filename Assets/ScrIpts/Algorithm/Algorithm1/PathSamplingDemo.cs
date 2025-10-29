// PathSamplingDemo.cs
// 演示如何使用 PartitionedGridAlgorithm 的 SamplePosition 方法
// Demo for using the SamplePosition method in PartitionedGridAlgorithm

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 路径采样演示脚本
/// 展示 SamplePosition 方法的多种使用场景
/// </summary>
public class PathSamplingDemo : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("PartitionedGridAlgorithm 实例")]
    public PartitionedGridAlgorithm algorithm;

    [Header("演示模式")]
    [Tooltip("选择演示模式")]
    public DemoMode mode = DemoMode.AnimatedPreview;

    [Header("设置")]
    [Tooltip("目标无人机ID")]
    public int targetDroneID = 0;

    [Tooltip("动画速度")]
    [Range(0.1f, 5f)]
    public float animationSpeed = 1f;

    [Tooltip("采样点数量（用于路径绘制）")]
    [Range(10, 200)]
    public int sampleCount = 50;

    [Tooltip("预览标记颜色")]
    public Color markerColor = Color.yellow;

    // 私有变量
    private GameObject previewMarker;
    private float currentProgress = 0f;
    private List<GameObject> pathMarkers = new List<GameObject>();

    // 演示模式枚举
    public enum DemoMode
    {
        AnimatedPreview,    // 动画预览
        StaticPathPoints,   // 静态路径点
        ProgressMonitor,    // 进度监控
        MultiDroneCompare   // 多无人机对比
    }

    void Start()
    {
        // 自动查找算法实例
        if (algorithm == null)
        {
            algorithm = FindFirstObjectByType<PartitionedGridAlgorithm>();
            if (algorithm == null)
            {
                Debug.LogError("❌ PathSamplingDemo: 找不到 PartitionedGridAlgorithm！");
                enabled = false;
                return;
            }
        }

        // 等待算法初始化
        Invoke(nameof(InitializeDemo), 1f);
    }

    void InitializeDemo()
    {
        switch (mode)
        {
            case DemoMode.AnimatedPreview:
                SetupAnimatedPreview();
                break;

            case DemoMode.StaticPathPoints:
                SetupStaticPathPoints();
                break;

            case DemoMode.ProgressMonitor:
                SetupProgressMonitor();
                break;

            case DemoMode.MultiDroneCompare:
                SetupMultiDroneCompare();
                break;
        }

        Debug.Log($"✅ PathSamplingDemo 初始化完成 - 模式: {mode}");
    }

    void Update()
    {
        if (mode == DemoMode.AnimatedPreview)
        {
            UpdateAnimatedPreview();
        }
        else if (mode == DemoMode.ProgressMonitor)
        {
            UpdateProgressMonitor();
        }
    }

    // ===================================================================
    // 模式1: 动画预览 - 沿路径移动的标记
    // ===================================================================

    void SetupAnimatedPreview()
    {
        // 创建预览标记
        previewMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        previewMarker.name = $"PathPreviewMarker_Drone{targetDroneID}";
        previewMarker.transform.localScale = Vector3.one * 2f;
        previewMarker.GetComponent<Renderer>().material.color = markerColor;

        // 移除碰撞体（可选）
        Destroy(previewMarker.GetComponent<Collider>());

        Debug.Log($"🎬 动画预览已启动 - 无人机 {targetDroneID}");
    }

    void UpdateAnimatedPreview()
    {
        if (previewMarker == null) return;

        // 更新进度（循环）
        currentProgress += animationSpeed * Time.deltaTime * 0.1f;
        if (currentProgress > 1f) currentProgress = 0f;

        // 采样位置
        Vector3 position = algorithm.SamplePosition(currentProgress, targetDroneID);
        previewMarker.transform.position = position;

        // 显示信息（每秒一次）
        if (Time.frameCount % 60 == 0)
        {
            float pathLength = algorithm.GetDronePathLength(targetDroneID);
            Debug.Log($"📍 进度: {currentProgress * 100f:F1}% | 位置: {position} | 路径总长: {pathLength:F1}m");
        }
    }

    // ===================================================================
    // 模式2: 静态路径点 - 显示采样的路径点
    // ===================================================================

    void SetupStaticPathPoints()
    {
        // 沿路径创建多个标记点
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = (float)i / sampleCount;
            Vector3 position = algorithm.SamplePosition(t, targetDroneID);

            // 创建标记
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"PathPoint_{i}";
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.5f;

            // 颜色渐变：从绿色（起点）到红色（终点）
            Color color = Color.Lerp(Color.green, Color.red, t);
            marker.GetComponent<Renderer>().material.color = color;

            // 移除碰撞体
            Destroy(marker.GetComponent<Collider>());

            pathMarkers.Add(marker);
        }

        Debug.Log($"📍 已创建 {pathMarkers.Count} 个路径点标记");
    }

    // ===================================================================
    // 模式3: 进度监控 - 显示无人机当前进度
    // ===================================================================

    void SetupProgressMonitor()
    {
        Debug.Log("📊 进度监控已启动");
    }

    void UpdateProgressMonitor()
    {
        // 获取无人机当前位置（假设你有一个Drone引用）
        // 这里简化为获取算法中的无人机
        if (algorithm == null) return;

        // 每秒更新一次
        if (Time.frameCount % 60 == 0)
        {
            float pathLength = algorithm.GetDronePathLength(targetDroneID);
            Vector3 startPos = algorithm.SamplePosition(0f, targetDroneID);
            Vector3 endPos = algorithm.SamplePosition(1f, targetDroneID);

            Debug.Log($"📊 无人机 {targetDroneID} 状态:");
            Debug.Log($"   起点: {startPos}");
            Debug.Log($"   终点: {endPos}");
            Debug.Log($"   路径长度: {pathLength:F1}m");

            // 可以在这里计算实际进度
            // float progress = CalculateActualProgress(drone.Position, targetDroneID);
        }
    }

    // ===================================================================
    // 模式4: 多无人机对比 - 显示多架无人机的关键点
    // ===================================================================

    void SetupMultiDroneCompare()
    {
        Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan };

        // 假设有5架无人机
        int droneCount = Mathf.Min(5, 10); // 最多显示5架

        for (int droneID = 0; droneID < droneCount; droneID++)
        {
            Color color = colors[droneID % colors.Length];

            // 为每架无人机创建起点、中点、终点标记
            CreateComparisonMarker(droneID, 0f, "Start", color, 1.5f);
            CreateComparisonMarker(droneID, 0.5f, "Mid", color, 1.0f);
            CreateComparisonMarker(droneID, 1f, "End", color, 1.5f);
        }

        Debug.Log($"🎯 已创建 {droneCount} 架无人机的对比标记");
    }

    void CreateComparisonMarker(int droneID, float t, string label, Color color, float size)
    {
        Vector3 position = algorithm.SamplePosition(t, droneID);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = $"Drone{droneID}_{label}";
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * size;
        marker.GetComponent<Renderer>().material.color = color;

        Destroy(marker.GetComponent<Collider>());

        pathMarkers.Add(marker);
    }

    // ===================================================================
    // 辅助方法
    // ===================================================================

    /// <summary>
    /// 计算无人机在路径上的实际进度（0-1）
    /// </summary>
    float CalculateActualProgress(Vector3 currentPosition, int droneID)
    {
        float minDistance = float.MaxValue;
        float closestT = 0f;

        // 在路径上采样多个点，找到最接近的
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            Vector3 samplePos = algorithm.SamplePosition(t, droneID);
            float distance = Vector3.Distance(currentPosition, samplePos);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestT = t;
            }
        }

        return closestT;
    }

    /// <summary>
    /// Gizmos 绘制（Scene视图可视化）
    /// </summary>
    void OnDrawGizmos()
    {
        if (algorithm == null || !Application.isPlaying) return;

        // 根据模式绘制不同的Gizmos
        if (mode == DemoMode.AnimatedPreview && previewMarker != null)
        {
            // 绘制当前位置到起点和终点的连线
            Vector3 start = algorithm.SamplePosition(0f, targetDroneID);
            Vector3 end = algorithm.SamplePosition(1f, targetDroneID);
            Vector3 current = previewMarker.transform.position;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(start, current);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(current, end);
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    void OnDestroy()
    {
        // 清理创建的标记
        if (previewMarker != null)
        {
            Destroy(previewMarker);
        }

        foreach (GameObject marker in pathMarkers)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        pathMarkers.Clear();
    }
}

// ===================================================================
// 额外示例：简单的碰撞检测
// ===================================================================

/// <summary>
/// 简单的碰撞检测示例
/// </summary>
public class CollisionPredictor : MonoBehaviour
{
    public PartitionedGridAlgorithm algorithm;
    public int drone1ID = 0;
    public int drone2ID = 1;
    public float safeDistance = 5f;
    public int checkPoints = 20; // 检查点数量

    void Start()
    {
        if (algorithm == null)
        {
            algorithm = FindFirstObjectByType<PartitionedGridAlgorithm>();
        }

        Invoke(nameof(CheckForPotentialCollisions), 1f);
    }

    void CheckForPotentialCollisions()
    {
        Debug.Log("🔍 开始检查潜在碰撞...");

        List<float> collisionTimes = new List<float>();

        // 在多个时间点检查
        for (int i = 0; i <= checkPoints; i++)
        {
            float t = (float)i / checkPoints;

            Vector3 pos1 = algorithm.SamplePosition(t, drone1ID);
            Vector3 pos2 = algorithm.SamplePosition(t, drone2ID);

            float distance = Vector3.Distance(pos1, pos2);

            if (distance < safeDistance)
            {
                collisionTimes.Add(t);
                Debug.LogWarning($"⚠️ 在 t={t:F2} ({t * 100f:F0}%) 时，距离仅 {distance:F1}m");
            }
        }

        if (collisionTimes.Count == 0)
        {
            Debug.Log("✅ 未发现潜在碰撞风险");
        }
        else
        {
            Debug.LogWarning($"⚠️ 发现 {collisionTimes.Count} 个潜在碰撞时刻");
        }
    }
}
