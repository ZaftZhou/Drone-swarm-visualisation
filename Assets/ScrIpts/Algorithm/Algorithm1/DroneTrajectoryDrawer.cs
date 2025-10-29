using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Draws drone scan trajectories onto a RenderTexture
/// Supports real-time drawing, path preview, and multiple visualization modes
/// </summary>
public class DroneTrajectoryDrawer : MonoBehaviour
{
    [Header("Render Texture Settings")]
    [SerializeField] private RenderTexture trajectoryTexture;
    [SerializeField] private int textureWidth = 1024;
    [SerializeField] private int textureHeight = 1024;
    
    [Header("Drawing Settings")]
    [SerializeField] private Material drawMaterial;
    [SerializeField] private Color trajectoryColor = Color.cyan;
    [SerializeField] private float lineWidth = 2f;
    [SerializeField] private bool antialiasing = true;
    
    [Header("Trajectory Settings")]
    [SerializeField] private Transform droneTransform;
    [SerializeField] private float drawInterval = 0.1f; // Seconds between draw updates
    [SerializeField] private bool fadeOverTime = false;
    [SerializeField] private float fadeSpeed = 0.95f;
    
    [Header("Visualization")]
    [SerializeField] private bool showDebugLines = true;
    [SerializeField] private Material displayMaterial; // Optional: for displaying on a quad
    
    private List<Vector3> trajectoryPoints = new List<Vector3>();
    private float nextDrawTime;
    private Vector3 lastDrawnPosition;
    private Texture2D drawTexture;
    private bool isInitialized = false;
    
    // Bounds for world-to-texture mapping
    private Vector3 worldCenter;
    private float worldScale = 100f; // World units that map to texture space

    void Start()
    {
        InitializeRenderTexture();
        
        if (droneTransform == null)
            droneTransform = transform;
            
        worldCenter = droneTransform.position;
        lastDrawnPosition = droneTransform.position;
    }

    void InitializeRenderTexture()
    {
        // Create RenderTexture if not assigned
        if (trajectoryTexture == null)
        {
            trajectoryTexture = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);
            trajectoryTexture.filterMode = FilterMode.Bilinear;
            trajectoryTexture.Create();
        }
        
        // Clear the render texture
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = rt;
        
        // Create temporary texture for drawing
        drawTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
        
        // Setup display material if assigned
        if (displayMaterial != null)
        {
            displayMaterial.mainTexture = trajectoryTexture;
        }
        
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized || droneTransform == null)
            return;
            
        // Check if it's time to draw
        if (Time.time >= nextDrawTime)
        {
            nextDrawTime = Time.time + drawInterval;
            
            Vector3 currentPosition = droneTransform.position;
            
            // Only draw if drone has moved significantly
            if (Vector3.Distance(currentPosition, lastDrawnPosition) > 0.1f)
            {
                DrawTrajectorySegment(lastDrawnPosition, currentPosition);
                trajectoryPoints.Add(currentPosition);
                lastDrawnPosition = currentPosition;
            }
        }
        
        // Apply fade effect if enabled
        if (fadeOverTime)
        {
            ApplyFade();
        }
    }

    /// <summary>
    /// Draws a line segment on the render texture
    /// </summary>
    void DrawTrajectorySegment(Vector3 worldStart, Vector3 worldEnd)
    {
        Vector2 texStart = WorldToTextureCoordinates(worldStart);
        Vector2 texEnd = WorldToTextureCoordinates(worldEnd);
        
        DrawLineOnTexture(texStart, texEnd);
    }

    /// <summary>
    /// Converts world coordinates to texture coordinates
    /// </summary>
    Vector2 WorldToTextureCoordinates(Vector3 worldPos)
    {
        // Convert world position relative to center
        Vector3 relativePos = worldPos - worldCenter;
        
        // Map to texture coordinates (0 to textureWidth/Height)
        float x = (relativePos.x / worldScale + 0.5f) * textureWidth;
        float z = (relativePos.z / worldScale + 0.5f) * textureHeight;
        
        return new Vector2(x, z);
    }

    /// <summary>
    /// Draws a line on the render texture using Bresenham's algorithm
    /// </summary>
    void DrawLineOnTexture(Vector2 start, Vector2 end)
    {
        RenderTexture.active = trajectoryTexture;
        
        // Read current texture
        drawTexture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
        drawTexture.Apply();
        
        // Draw line using Bresenham's algorithm
        int x0 = Mathf.RoundToInt(start.x);
        int y0 = Mathf.RoundToInt(start.y);
        int x1 = Mathf.RoundToInt(end.x);
        int y1 = Mathf.RoundToInt(end.y);
        
        DrawBresenhamLine(x0, y0, x1, y1);
        
        // Apply changes back to render texture
        drawTexture.Apply();
        Graphics.Blit(drawTexture, trajectoryTexture);
        
        RenderTexture.active = null;
    }

    /// <summary>
    /// Bresenham's line algorithm with width support
    /// </summary>
    void DrawBresenhamLine(int x0, int y0, int x1, int y1)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        
        int halfWidth = Mathf.RoundToInt(lineWidth / 2f);
        
        while (true)
        {
            // Draw thick line by drawing circle at each point
            DrawCircle(x0, y0, halfWidth);
            
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

    /// <summary>
    /// Draws a filled circle on the texture
    /// </summary>
    void DrawCircle(int centerX, int centerY, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                {
                    int px = centerX + x;
                    int py = centerY + y;
                    
                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        Color currentColor = drawTexture.GetPixel(px, py);
                        Color blendedColor = Color.Lerp(currentColor, trajectoryColor, trajectoryColor.a);
                        drawTexture.SetPixel(px, py, blendedColor);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Applies fade effect to the entire texture
    /// </summary>
    void ApplyFade()
    {
        RenderTexture.active = trajectoryTexture;
        drawTexture.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
        
        Color[] pixels = drawTexture.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] *= fadeSpeed;
        }
        
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();
        Graphics.Blit(drawTexture, trajectoryTexture);
        
        RenderTexture.active = null;
    }

    /// <summary>
    /// Clears the trajectory texture
    /// </summary>
    public void ClearTrajectory()
    {
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = rt;
        
        trajectoryPoints.Clear();
        lastDrawnPosition = droneTransform.position;
    }

    /// <summary>
    /// Draws a complete path from a list of points
    /// </summary>
    public void DrawPath(List<Vector3> pathPoints)
    {
        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            DrawTrajectorySegment(pathPoints[i], pathPoints[i + 1]);
        }
    }

    /// <summary>
    /// Sets the world-to-texture mapping scale
    /// </summary>
    public void SetWorldScale(float scale)
    {
        worldScale = scale;
    }

    /// <summary>
    /// Sets the center point for world-to-texture mapping
    /// </summary>
    public void SetWorldCenter(Vector3 center)
    {
        worldCenter = center;
    }

    void OnDrawGizmos()
    {
        if (!showDebugLines || trajectoryPoints.Count < 2)
            return;
            
        Gizmos.color = trajectoryColor;
        for (int i = 0; i < trajectoryPoints.Count - 1; i++)
        {
            Gizmos.DrawLine(trajectoryPoints[i], trajectoryPoints[i + 1]);
        }
    }

    void OnDestroy()
    {
        if (drawTexture != null)
            Destroy(drawTexture);
    }

    // Public getters
    public RenderTexture GetTrajectoryTexture() => trajectoryTexture;
    public List<Vector3> GetTrajectoryPoints() => new List<Vector3>(trajectoryPoints);
}
