using UnityEngine;
using System.Collections.Generic;

public class SwarmTrajectoryDrawer : MonoBehaviour
{
    [Header("Trajectory Texture")]
    [SerializeField] private RenderTexture trajectoryTexture;

    [SerializeField] private int textureResolution = 1024;  

    [Header("Performance Settings")]
    [SerializeField] private float drawInterval = 0.1f; 

    [SerializeField][Range(1, 5)] private int frameSkip = 2;


    [SerializeField] private float minMoveDistance = 2f;  

    [SerializeField] private bool useGPUAcceleration = true;

    [Header("Drawing Settings")]
      [SerializeField] private float lineWidth = 2f;  

    [Tooltip("lineSmoothing (0=Fastest, 2=Most smooth)")]
    [SerializeField][Range(0, 2)] private int lineSmoothing = 0;

    [Header("Color Settings")]
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
    [SerializeField] private bool showDebugGizmos = false; 

   
    private Dictionary<Drone, DroneTrajectoryData> droneTrajectories;
    private Material lineMaterial;
    private float nextDrawTime;
    private int frameCounter;
    private bool isInitialized = false;

    [Header("Performance Statistics ")]
    private float totalDistance = 0f;
    private int totalDrawCalls = 0;
    private float lastFrameTime = 0f;
    private float avgFrameTime = 0f;

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
        Debug.Log("🚀 SwarmTrajectoryDrawer Initializing...");

        if (autoSyncSearchArea && searchAreaCollider == null)
        {
            FindSearchAreaCollider();
        }

        if (searchAreaCollider != null)
        {
            SyncWorldMappingFromCollider();
        }
        InitializeRenderTexture();
        InitializeMaterial();
        droneTrajectories = new Dictionary<Drone, DroneTrajectoryData>();
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
                    Debug.Log($"📍 Search area: {col.gameObject.name}");
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

        Debug.Log($"📐 World center={worldCenter}, World size={worldSize}m");
    }

    void FindAndSetupDrones()
    {
        Drone[] allDrones = FindObjectsOfType<Drone>();

        if (allDrones.Length == 0)
        {
            Debug.LogWarning("⚠️ There is no drone in the scene！");
            return;
        }

        Debug.Log($"✅ Find {allDrones.Length} Drones");

        for (int i = 0; i < allDrones.Length; i++)
        {
            Drone drone = allDrones[i];
            Color color = droneColors[i % droneColors.Length];

            DroneTrajectoryData data = new DroneTrajectoryData(drone, color);
            droneTrajectories[drone] = data;
        }

        isInitialized = true;
        Debug.Log($"✅ Initialized，track {droneTrajectories.Count} Drones");
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
            trajectoryTexture.antiAliasing = 1;  
            trajectoryTexture.Create();
        }

        // 清空纹理
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = rt;

        Debug.Log($"✅ RenderTexture generated: {textureResolution}x{textureResolution}");
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
        if (frameCounter % frameSkip != 0) return;
        if (Time.time < nextDrawTime) return;
        float startTime = Time.realtimeSinceStartup;
        CollectLineSegments();

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
                pendingLines.Add(new LineSegment
                {
                    start = data.lastPosition,
                    end = currentPos,
                    color = data.color
                });

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
            GL.Vertex3(start.x, start.y, 0);
            GL.Vertex3(end.x, end.y, 0);
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
        Debug.LogWarning("Using cpu drawing");
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

        Debug.Log("🧹 clear all the path history");
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

        GUILayout.Label("<b>SwarmTrajectoryDrawer (GPU)</b>");
        GUILayout.Label($"Drone count: {GetDroneCount()}");
        GUILayout.Label($"Total distance: {totalDistance:F1}m");
        GUILayout.Label($"Average frame time: {avgFrameTime:F2}ms");


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