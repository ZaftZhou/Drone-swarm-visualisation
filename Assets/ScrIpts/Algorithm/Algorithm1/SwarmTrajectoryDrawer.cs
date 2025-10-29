using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 无人机群轨迹绘制系统 - 修复版
/// Draws trajectories for drone swarm - Fixed version
/// </summary>
public class SwarmTrajectoryDrawer : MonoBehaviour
{
    [Header("轨迹纹理设置 Trajectory Texture")]
    [Tooltip("轨迹绘制的 RenderTexture")]
    [SerializeField] private RenderTexture trajectoryTexture;

    [Tooltip("纹理分辨率")]
    [SerializeField] private int textureResolution = 2048;

    [Header("绘制设置 Drawing Settings")]
    [Tooltip("轨迹线宽度")]
    [SerializeField] private float lineWidth = 3f;

    [Tooltip("绘制更新间隔（秒）")]
    [SerializeField] private float drawInterval = 0.05f;

    [Tooltip("无人机移动多少距离才绘制")]
    [SerializeField] private float minMoveDistance = 1f;

    [Header("颜色设置 Color Settings")]
    [Tooltip("无人机轨迹颜色数组")]
    [SerializeField]
    private Color[] droneColors = new Color[]
    {
        new Color(0, 1, 1, 1),      // 青色
        new Color(1, 0, 1, 1),      // 洋红
        new Color(1, 1, 0, 1),      // 黄色
        new Color(0, 1, 0, 1),      // 绿色
        new Color(1, 0.5f, 0, 1),   // 橙色
        new Color(0.5f, 0, 1, 1),   // 紫色
        new Color(1, 0, 0, 1),      // 红色
        new Color(0, 0.5f, 1, 1),   // 浅蓝
    };

    [Header("世界映射 World Mapping")]
    [Tooltip("世界中心点（搜索区域中心）")]
    [SerializeField] private Vector3 worldCenter = Vector3.zero;

    [Tooltip("世界范围（世界单位）")]
    [SerializeField] private float worldSize = 200f;

    [Header("调试 Debug")]
    [Tooltip("在场景中显示 Gizmos")]
    [SerializeField] private bool showDebugGizmos = true;

    [Tooltip("显示调试信息")]
    [SerializeField] private bool showDebugInfo = true;

    // 私有变量
    private Dictionary<Drone, DroneTrajectoryData> droneTrajectories;
    private Texture2D drawTexture;
    private float nextDrawTime;
    private bool isInitialized = false;

    // 统计
    private float totalDistance = 0f;
    private int totalDrawCalls = 0;

    /// <summary>
    /// 单个无人机的轨迹数据
    /// </summary>
    private class DroneTrajectoryData
    {
        public Drone drone;
        public Color color;
        public Vector3 lastPosition;
        public List<Vector3> points;
        public float distanceTraveled;

        public DroneTrajectoryData(Drone drone, Color color)
        {
            this.drone = drone;
            this.color = color;
            this.lastPosition = drone.Position;
            this.points = new List<Vector3>();
            this.distanceTraveled = 0f;
        }
    }

    void Start()
    {
        InitializeSystem();
    }

    void InitializeSystem()
    {
        Debug.Log("🚀 SwarmTrajectoryDrawer: 开始初始化...");

        // 初始化纹理
        InitializeRenderTexture();

        // 初始化轨迹数据字典
        droneTrajectories = new Dictionary<Drone, DroneTrajectoryData>();

        // 延迟查找无人机（等待场景加载完成）
        Invoke(nameof(FindAndSetupDrones), 0.5f);
    }

    void FindAndSetupDrones()
    {
        // 查找场景中所有无人机
        Drone[] allDrones = FindObjectsOfType<Drone>();

        if (allDrones.Length == 0)
        {
            Debug.LogWarning("⚠️ SwarmTrajectoryDrawer: 场景中没有找到无人机！");
            return;
        }

        Debug.Log($"✅ SwarmTrajectoryDrawer: 找到 {allDrones.Length} 架无人机");

        // 自动设置世界中心（使用第一架无人机的位置）
        if (allDrones.Length > 0)
        {
            worldCenter = allDrones[0].Position;
            worldCenter.y = 0; // 使用地面高度
            Debug.Log($"📍 设置世界中心: {worldCenter}");
        }

        // 为每架无人机创建轨迹数据
        for (int i = 0; i < allDrones.Length; i++)
        {
            Drone drone = allDrones[i];
            Color color = droneColors[i % droneColors.Length];

            DroneTrajectoryData data = new DroneTrajectoryData(drone, color);
            droneTrajectories[drone] = data;

            Debug.Log($"🎨 无人机 {i}: {drone.name} - 颜色: {color}");
        }

        isInitialized = true;
        Debug.Log($"✅ SwarmTrajectoryDrawer: 系统初始化完成，追踪 {droneTrajectories.Count} 架无人机");
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
            trajectoryTexture.Create();
            Debug.Log($"✅ 创建 RenderTexture: {textureResolution}x{textureResolution}");
        }

        // 清空纹理为黑色
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = rt;

        // 创建临时绘制纹理
        drawTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);

        // 初始化为黑色
        Color[] pixels = new Color[textureResolution * textureResolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.black;
        }
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();

        Debug.Log("✅ 纹理初始化完成");
    }

    void Update()
    {
        if (!isInitialized) return;

        // 绘制轨迹
        if (Time.time >= nextDrawTime)
        {
            nextDrawTime = Time.time + drawInterval;
            DrawTrajectories();
        }
    }

    void DrawTrajectories()
    {
        bool needsUpdate = false;

        foreach (var kvp in droneTrajectories)
        {
            Drone drone = kvp.Key;
            DroneTrajectoryData data = kvp.Value;

            if (drone == null) continue;

            Vector3 currentPos = drone.Position;

            // 检查是否移动了足够的距离
            float distance = Vector3.Distance(currentPos, data.lastPosition);
            if (distance >= minMoveDistance)
            {
                // 绘制线段
                DrawLineSegment(data.lastPosition, currentPos, data.color);

                // 更新数据
                data.points.Add(currentPos);
                data.distanceTraveled += distance;
                data.lastPosition = currentPos;
                totalDistance += distance;
                totalDrawCalls++;

                needsUpdate = true;

                if (showDebugInfo && totalDrawCalls % 20 == 0)
                {
                    Debug.Log($"📊 已绘制 {totalDrawCalls} 次，总距离: {totalDistance:F1}m");
                }
            }
        }

        if (needsUpdate)
        {
            ApplyDrawTexture();
        }
    }

    void DrawLineSegment(Vector3 worldStart, Vector3 worldEnd, Color color)
    {
        // 转换世界坐标到纹理坐标
        Vector2 texStart = WorldToTextureCoordinates(worldStart);
        Vector2 texEnd = WorldToTextureCoordinates(worldEnd);

        // 绘制线条
        DrawBresenhamLine(
            Mathf.RoundToInt(texStart.x),
            Mathf.RoundToInt(texStart.y),
            Mathf.RoundToInt(texEnd.x),
            Mathf.RoundToInt(texEnd.y),
            color
        );
    }

    Vector2 WorldToTextureCoordinates(Vector3 worldPos)
    {
        // 计算相对位置
        Vector3 relativePos = worldPos - worldCenter;

        // 归一化到 [0, 1]
        float normalizedX = (relativePos.x / worldSize) + 0.5f;
        float normalizedZ = (relativePos.z / worldSize) + 0.5f;

        // 映射到纹理坐标
        float x = Mathf.Clamp(normalizedX * textureResolution, 0, textureResolution - 1);
        float y = Mathf.Clamp(normalizedZ * textureResolution, 0, textureResolution - 1);

        return new Vector2(x, y);
    }

    void DrawBresenhamLine(int x0, int y0, int x1, int y1, Color color)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int halfWidth = Mathf.RoundToInt(lineWidth / 2f);

        while (true)
        {
            DrawCircle(x0, y0, halfWidth, color);

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    void DrawCircle(int centerX, int centerY, int radius, Color color)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = centerX + x;
                    int py = centerY + y;

                    if (px >= 0 && px < textureResolution && py >= 0 && py < textureResolution)
                    {
                        drawTexture.SetPixel(px, py, color);
                    }
                }
            }
        }
    }

    void ApplyDrawTexture()
    {
        drawTexture.Apply();
        Graphics.Blit(drawTexture, trajectoryTexture);
    }

    // ==================== 公共方法 ====================

    public void ClearAllTrajectories()
    {
        // 清空纹理
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = rt;

        // 重置绘制纹理
        Color[] pixels = new Color[textureResolution * textureResolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.black;
        }
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();

        // 重置数据
        if (droneTrajectories != null)
        {
            foreach (var data in droneTrajectories.Values)
            {
                data.points.Clear();
                data.distanceTraveled = 0f;
                if (data.drone != null)
                {
                    data.lastPosition = data.drone.Position;
                }
            }
        }

        totalDistance = 0f;
        totalDrawCalls = 0;

        Debug.Log("🧹 已清空所有轨迹");
    }

    public RenderTexture GetTrajectoryTexture()
    {
        return trajectoryTexture;
    }

    public float GetTotalDistance()
    {
        return totalDistance;
    }

    public int GetDroneCount()
    {
        return droneTrajectories != null ? droneTrajectories.Count : 0;
    }

    public void SetWorldCenter(Vector3 center)
    {
        worldCenter = center;
        Debug.Log($"📍 设置世界中心: {worldCenter}");
    }

    public void SetWorldSize(float size)
    {
        worldSize = size;
        Debug.Log($"📏 设置世界大小: {worldSize}");
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // 绘制世界边界
        Gizmos.color = Color.yellow;
        Vector3 size = new Vector3(worldSize, 1f, worldSize);
        Gizmos.DrawWireCube(worldCenter, size);

        // 绘制轨迹
        if (droneTrajectories != null)
        {
            foreach (var kvp in droneTrajectories)
            {
                DroneTrajectoryData data = kvp.Value;

                if (data.points.Count < 2) continue;

                Gizmos.color = data.color;

                for (int i = 0; i < data.points.Count - 1; i++)
                {
                    Gizmos.DrawLine(data.points[i], data.points[i + 1]);
                }

                // 绘制无人机当前位置
                if (data.drone != null)
                {
                    Gizmos.DrawWireSphere(data.drone.Position, 2f);
                }
            }
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, Screen.height - 120, 300, 110));
        GUILayout.Box("SwarmTrajectoryDrawer 调试信息");
        GUILayout.Label($"无人机数: {GetDroneCount()}");
        GUILayout.Label($"总距离: {totalDistance:F1}m");
        GUILayout.Label($"绘制次数: {totalDrawCalls}");
        GUILayout.Label($"世界中心: {worldCenter}");
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (drawTexture != null)
        {
            Destroy(drawTexture);
        }
    }
}