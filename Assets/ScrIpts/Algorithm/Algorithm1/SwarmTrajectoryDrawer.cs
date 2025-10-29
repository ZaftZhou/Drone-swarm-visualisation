using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 高性能无人机群轨迹绘制系统 - GPU 加速版本
/// High-Performance Swarm Trajectory Drawer - GPU Accelerated
/// 
/// 性能优化：
/// 1. 使用 GL 直接绘制到 RenderTexture（GPU 操作）
/// 2. 批处理所有绘制操作
/// 3. 减少 CPU-GPU 数据传输
/// 4. 可选的帧跳过机制
/// </summary>
public class SwarmTrajectoryDrawer : MonoBehaviour
{
    [Header("Trajectory Texture")]
    [Tooltip("轨迹绘制的 RenderTexture")]
    [SerializeField] private RenderTexture trajectoryTexture;

    [Tooltip("纹理分辨率 (推荐: 1024-2048)")]
    [SerializeField] private int textureResolution = 1024;  // 降低默认分辨率

    [Header("Performance Settings")]
    [Tooltip("绘制更新间隔（秒）- 增大可提升性能")]
    [SerializeField] private float drawInterval = 0.1f;  // 增加默认间隔

    [Tooltip("每 N 帧绘制一次 (1=每帧, 2=隔帧)")]
    [SerializeField][Range(1, 5)] private int frameSkip = 2;

    [Tooltip("无人机移动多少距离才绘制")]
    [SerializeField] private float minMoveDistance = 2f;  // 增加阈值

    [Tooltip("使用 GPU 加速绘制")]
    [SerializeField] private bool useGPUAcceleration = true;

    [Header("Drawing Settings")]
    [Tooltip("轨迹线宽度（像素）")]
    [SerializeField] private float lineWidth = 2f;  // 减小默认宽度

    [Tooltip("线条平滑度 (0=最快, 2=最平滑)")]
    [SerializeField][Range(0, 2)] private int lineSmoothing = 0;

    [Header("Color Settings")]
    [Tooltip("无人机轨迹颜色数组")]
    [SerializeField]
    private Color[] droneColors = new Color[]
    {
        new Color(0, 1, 1, 1),      // Cyan
        new Color(1, 0, 1, 1),      // Magenta
        new Color(1, 1, 0, 1),      // Yellow
        new Color(0, 1, 0, 1),      // Green
        new Color(1, 0.5f, 0, 1),   // Orange
        new Color(0.5f, 0, 1, 1),   // Purple
        new Color(1, 0, 0, 1),      // Red
        new Color(0, 0.5f, 1, 1),   // Light Blue
    };

    [Header("World Mapping")]
    [SerializeField] private bool autoSyncSearchArea = true;
    [SerializeField] private Collider searchAreaCollider;
    [SerializeField] private Vector3 worldCenter = Vector3.zero;
    [SerializeField] private float worldSize = 200f;
    [SerializeField][Range(0f, 0.5f)] private float boundaryPadding = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showDebugGizmos = false;  // 默认关闭以提升性能

    // 私有变量
    private Dictionary<Drone, DroneTrajectoryData> droneTrajectories;
    private Material lineMaterial;
    private float nextDrawTime;
    private int frameCounter;
    private bool isInitialized = false;

    // 性能统计
    private float totalDistance = 0f;
    private int totalDrawCalls = 0;
    private float lastFrameTime = 0f;
    private float avgFrameTime = 0f;

    // 批处理缓冲
    private List<LineSegment> pendingLines = new List<LineSegment>();

    private struct LineSegment
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;
    }

    private class DroneTrajectoryData
    {
        public Drone drone;
        public Color color;
        public Vector3 lastPosition;
        public float distanceTraveled;
        public int pointCount;

        public DroneTrajectoryData(Drone drone, Color color)
        {
            this.drone = drone;
            this.color = color;
            this.lastPosition = drone.Position;
            this.distanceTraveled = 0f;
            this.pointCount = 0;
        }
    }

    void Start()
    {
        InitializeSystem();
    }

    void InitializeSystem()
    {
        Debug.Log("🚀 SwarmTrajectoryDrawer (GPU优化版): 开始初始化...");

        // 自动查找搜索区域
        if (autoSyncSearchArea && searchAreaCollider == null)
        {
            FindSearchAreaCollider();
        }

        // 同步世界映射参数
        if (searchAreaCollider != null)
        {
            SyncWorldMappingFromCollider();
        }

        // 初始化纹理和材质
        InitializeRenderTexture();
        InitializeMaterial();

        // 初始化轨迹数据
        droneTrajectories = new Dictionary<Drone, DroneTrajectoryData>();

        // 延迟查找无人机
        Invoke(nameof(FindAndSetupDrones), 0.5f);
    }

    void FindSearchAreaCollider()
    {
        AlgorithmManager manager = FindFirstObjectByType<AlgorithmManager>();
        if (manager != null)
        {
            var field = manager.GetType().GetField("searchAreaCollider",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                searchAreaCollider = field.GetValue(manager) as Collider;
            }
        }

        if (searchAreaCollider == null)
        {
            BoxCollider[] colliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            foreach (var col in colliders)
            {
                if (col.gameObject.name.ToLower().Contains("search") ||
                    col.gameObject.name.ToLower().Contains("area"))
                {
                    searchAreaCollider = col;
                    Debug.Log($"📍 找到搜索区域: {col.gameObject.name}");
                    break;
                }
            }
        }
    }

    void SyncWorldMappingFromCollider()
    {
        if (searchAreaCollider == null) return;

        Bounds bounds = searchAreaCollider.bounds;
        worldCenter = bounds.center;
        worldCenter.y = 0;

        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.z);
        worldSize = maxDimension * (1f + boundaryPadding);

        Debug.Log($"📐 世界映射: 中心={worldCenter}, 大小={worldSize}m");
    }

    void FindAndSetupDrones()
    {
        Drone[] allDrones = FindObjectsOfType<Drone>();

        if (allDrones.Length == 0)
        {
            Debug.LogWarning("⚠️ 场景中没有找到无人机！");
            return;
        }

        Debug.Log($"✅ 找到 {allDrones.Length} 架无人机");

        for (int i = 0; i < allDrones.Length; i++)
        {
            Drone drone = allDrones[i];
            Color color = droneColors[i % droneColors.Length];

            DroneTrajectoryData data = new DroneTrajectoryData(drone, color);
            droneTrajectories[drone] = data;
        }

        isInitialized = true;
        Debug.Log($"✅ 系统初始化完成，追踪 {droneTrajectories.Count} 架无人机");
    }

    void InitializeRenderTexture()
    {
        if (trajectoryTexture == null)
        {
            trajectoryTexture = new RenderTexture(
                textureResolution,
                textureResolution,
                0,
                RenderTextureFormat.ARGB32
            );
            trajectoryTexture.filterMode = FilterMode.Bilinear;
            trajectoryTexture.antiAliasing = 1;  // 禁用抗锯齿以提升性能
            trajectoryTexture.Create();
        }

        // 清空纹理
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = rt;

        Debug.Log($"✅ RenderTexture 已创建: {textureResolution}x{textureResolution}");
    }

    void InitializeMaterial()
    {
        // 创建用于 GL 绘制的材质
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        lineMaterial = new Material(shader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    void Update()
    {
        if (!isInitialized) return;

        frameCounter++;

        // 帧跳过机制
        if (frameCounter % frameSkip != 0) return;

        // 时间间隔检查
        if (Time.time < nextDrawTime) return;

        float startTime = Time.realtimeSinceStartup;

        // 收集需要绘制的线段
        CollectLineSegments();

        // 批量绘制
        if (pendingLines.Count > 0)
        {
            if (useGPUAcceleration)
            {
                DrawLinesGPU();
            }
            else
            {
                DrawLinesCPU();
            }
            pendingLines.Clear();
        }

        nextDrawTime = Time.time + drawInterval;

        // 性能统计
        lastFrameTime = (Time.realtimeSinceStartup - startTime) * 1000f;
        avgFrameTime = Mathf.Lerp(avgFrameTime, lastFrameTime, 0.1f);
    }

    void CollectLineSegments()
    {
        foreach (var kvp in droneTrajectories)
        {
            Drone drone = kvp.Key;
            DroneTrajectoryData data = kvp.Value;

            if (drone == null) continue;

            Vector3 currentPos = drone.Position;
            float distance = Vector3.Distance(currentPos, data.lastPosition);

            if (distance >= minMoveDistance)
            {
                // 添加到批处理队列
                pendingLines.Add(new LineSegment
                {
                    start = data.lastPosition,
                    end = currentPos,
                    color = data.color
                });

                // 更新统计
                data.lastPosition = currentPos;
                data.distanceTraveled += distance;
                data.pointCount++;
                totalDistance += distance;
                totalDrawCalls++;
            }
        }
    }

    void DrawLinesGPU()
    {
        // 使用 GL 直接绘制到 RenderTexture（GPU 操作）
        RenderTexture.active = trajectoryTexture;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, textureResolution, textureResolution, 0);

        lineMaterial.SetPass(0);

        GL.Begin(GL.LINES);

        foreach (var line in pendingLines)
        {
            Vector2 start = WorldToTextureCoordinates(line.start);
            Vector2 end = WorldToTextureCoordinates(line.end);

            GL.Color(line.color);

            // 绘制主线
            GL.Vertex3(start.x, start.y, 0);
            GL.Vertex3(end.x, end.y, 0);

            // 如果需要更粗的线，绘制额外的偏移线
            if (lineWidth > 1f)
            {
                Vector2 dir = (end - start).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);

                for (int i = 1; i <= lineWidth / 2; i++)
                {
                    Vector2 offset = perp * i;

                    GL.Vertex3(start.x + offset.x, start.y + offset.y, 0);
                    GL.Vertex3(end.x + offset.x, end.y + offset.y, 0);

                    GL.Vertex3(start.x - offset.x, start.y - offset.y, 0);
                    GL.Vertex3(end.x - offset.x, end.y - offset.y, 0);
                }
            }
        }

        GL.End();
        GL.PopMatrix();

        RenderTexture.active = null;
    }

    void DrawLinesCPU()
    {
        // 备用的 CPU 绘制方法（保留以防 GPU 方法不兼容）
        Debug.LogWarning("使用 CPU 绘制模式，性能较低");
    }

    Vector2 WorldToTextureCoordinates(Vector3 worldPos)
    {
        Vector3 relativePos = worldPos - worldCenter;
        float normalizedX = (relativePos.x / worldSize) + 0.5f;
        float normalizedZ = (relativePos.z / worldSize) + 0.5f;

        float x = Mathf.Clamp(normalizedX * textureResolution, 0, textureResolution - 1);
        float y = Mathf.Clamp(normalizedZ * textureResolution, 0, textureResolution - 1);

        return new Vector2(x, y);
    }

    // ==================== 公共方法 ====================

    public void ClearAllTrajectories()
    {
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = rt;

        if (droneTrajectories != null)
        {
            foreach (var data in droneTrajectories.Values)
            {
                data.pointCount = 0;
                data.distanceTraveled = 0f;
                if (data.drone != null)
                {
                    data.lastPosition = data.drone.Position;
                }
            }
        }

        totalDistance = 0f;
        totalDrawCalls = 0;
        pendingLines.Clear();

        Debug.Log("🧹 已清空所有轨迹");
    }

    public RenderTexture GetTrajectoryTexture() => trajectoryTexture;
    public float GetTotalDistance() => totalDistance;
    public int GetDroneCount() => droneTrajectories != null ? droneTrajectories.Count : 0;
    public Vector3 GetWorldCenter() => worldCenter;
    public float GetWorldSize() => worldSize;

    public void SetWorldCenter(Vector3 center)
    {
        worldCenter = center;
    }

    public void SetWorldSize(float size)
    {
        worldSize = size;
    }

    public void SetSearchAreaCollider(Collider collider)
    {
        searchAreaCollider = collider;
        if (collider != null)
        {
            SyncWorldMappingFromCollider();
        }
    }

    public void ResyncSearchArea()
    {
        if (searchAreaCollider != null)
        {
            SyncWorldMappingFromCollider();
        }
    }

    // ==================== 性能统计 ====================

    public float GetAverageFrameTime()
    {
        return avgFrameTime;
    }

    public int GetPendingLineCount()
    {
        return pendingLines.Count;
    }

    // ==================== Debug ====================

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(worldCenter, new Vector3(worldSize, 1f, worldSize));

        if (searchAreaCollider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(searchAreaCollider.bounds.center, searchAreaCollider.bounds.size);
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 180, 400, 170));
        GUI.Box(new Rect(0, 0, 400, 170), "");

        GUILayout.Label("<b>SwarmTrajectoryDrawer (GPU优化版)</b>");
        GUILayout.Label($"无人机数: {GetDroneCount()}");
        GUILayout.Label($"总距离: {totalDistance:F1}m");
        GUILayout.Label($"绘制调用: {totalDrawCalls}");
        GUILayout.Label($"待绘制线段: {pendingLines.Count}");
        GUILayout.Label($"帧时间: {avgFrameTime:F2}ms");
        GUILayout.Label($"分辨率: {textureResolution}x{textureResolution}");
        GUILayout.Label($"GPU加速: {(useGPUAcceleration ? "启用" : "禁用")}");

        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
}