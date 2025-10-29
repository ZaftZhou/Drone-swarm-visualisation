using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GPU-accelerated drone trajectory drawer using Graphics.DrawMeshInstancedProcedural
/// More efficient for real-time drawing of many trajectory points
/// </summary>
public class GPUTrajectoryDrawer : MonoBehaviour
{
    [Header("Render Texture")]
    [SerializeField] private RenderTexture trajectoryRT;
    [SerializeField] private int resolution = 2048;
    
    [Header("Drawing")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColor = new Color(0, 1, 1, 0.8f);
    [SerializeField] private float lineWidth = 3f;
    
    [Header("Trajectory")]
    [SerializeField] private Transform drone;
    [SerializeField] private float captureInterval = 0.05f;
    [SerializeField] private int maxPoints = 10000;
    [SerializeField] private bool continuousDrawing = true;
    
    [Header("Effects")]
    [SerializeField] private bool useGlow = true;
    [SerializeField] private float glowIntensity = 2f;
    [SerializeField] private bool fadeTrail = false;
    [SerializeField] [Range(0.9f, 1f)] private float fadeAmount = 0.98f;
    
    private Camera renderCamera;
    private List<Vector3> points = new List<Vector3>();
    private float nextCaptureTime;
    private Material fadeMaterial;
    private RenderTexture tempRT;

    void Start()
    {
        SetupRenderTexture();
        SetupCamera();
        CreateFadeMaterial();
        
        if (drone == null)
            drone = transform;
    }

    void SetupRenderTexture()
    {
        if (trajectoryRT == null)
        {
            trajectoryRT = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
            trajectoryRT.filterMode = FilterMode.Bilinear;
            trajectoryRT.wrapMode = TextureWrapMode.Clamp;
            trajectoryRT.Create();
        }
        
        // Clear to transparent
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryRT;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = rt;
        
        // Create temp texture for fade effect
        tempRT = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        tempRT.Create();
    }

    void SetupCamera()
    {
        GameObject camObj = new GameObject("TrajectoryCamera");
        camObj.transform.SetParent(transform);
        
        renderCamera = camObj.AddComponent<Camera>();
        renderCamera.orthographic = true;
        renderCamera.orthographicSize = 50f;
        renderCamera.nearClipPlane = 0.1f;
        renderCamera.farClipPlane = 1000f;
        renderCamera.clearFlags = CameraClearFlags.Nothing;
        renderCamera.cullingMask = 0; // Don't render any layers
        renderCamera.targetTexture = trajectoryRT;
        renderCamera.enabled = false; // Manual rendering
        
        // Position camera to look down
        camObj.transform.position = new Vector3(0, 100, 0);
        camObj.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    void CreateFadeMaterial()
    {
        Shader fadeShader = Shader.Find("Hidden/FadeTexture");
        if (fadeShader == null)
        {
            // Create simple fade shader
            fadeShader = Shader.Find("Unlit/Transparent");
        }
        fadeMaterial = new Material(fadeShader);
    }

    void Update()
    {
        if (drone == null || !continuousDrawing)
            return;
            
        // Capture new point
        if (Time.time >= nextCaptureTime)
        {
            CapturePoint(drone.position);
            nextCaptureTime = Time.time + captureInterval;
        }
        
        // Draw trajectory
        if (points.Count > 1)
        {
            DrawTrajectory();
        }
        
        // Apply fade effect
        if (fadeTrail)
        {
            ApplyFadeEffect();
        }
    }

    void CapturePoint(Vector3 worldPos)
    {
        points.Add(worldPos);
        
        // Limit number of points
        if (points.Count > maxPoints)
        {
            points.RemoveAt(0);
        }
    }

    void DrawTrajectory()
    {
        renderCamera.enabled = true;
        
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = trajectoryRT;
        
        GL.PushMatrix();
        GL.LoadProjectionMatrix(renderCamera.projectionMatrix);
        GL.modelview = renderCamera.worldToCameraMatrix;
        
        // Setup material
        if (lineMaterial != null)
        {
            lineMaterial.SetPass(0);
        }
        
        // Draw lines
        GL.Begin(GL.LINES);
        GL.Color(lineColor);
        
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            
            // Draw thick line as quad
            DrawThickLine(p1, p2, lineWidth);
        }
        
        GL.End();
        GL.PopMatrix();
        
        RenderTexture.active = currentRT;
        renderCamera.enabled = false;
    }

    void DrawThickLine(Vector3 start, Vector3 end, float width)
    {
        Vector3 direction = end - start;
        Vector3 perpendicular = Vector3.Cross(direction, Vector3.up).normalized * width * 0.5f;
        
        // Create quad vertices
        Vector3 v1 = start - perpendicular;
        Vector3 v2 = start + perpendicular;
        Vector3 v3 = end + perpendicular;
        Vector3 v4 = end - perpendicular;
        
        // Draw two triangles
        GL.Vertex(v1);
        GL.Vertex(v2);
        
        GL.Vertex(v2);
        GL.Vertex(v3);
        
        GL.Vertex(v3);
        GL.Vertex(v4);
        
        GL.Vertex(v4);
        GL.Vertex(v1);
    }

    void ApplyFadeEffect()
    {
        // Blit with fade
        Graphics.Blit(trajectoryRT, tempRT);
        
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryRT;
        
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        
        if (fadeMaterial != null)
        {
            fadeMaterial.color = new Color(1, 1, 1, fadeAmount);
            Graphics.Blit(tempRT, trajectoryRT, fadeMaterial);
        }
        
        RenderTexture.active = rt;
    }

    public void Clear()
    {
        points.Clear();
        
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = trajectoryRT;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = rt;
    }

    public void SetLineColor(Color color)
    {
        lineColor = color;
    }

    public void SetLineWidth(float width)
    {
        lineWidth = width;
    }

    public RenderTexture GetTexture() => trajectoryRT;
    
    public void SetCameraSize(float size)
    {
        if (renderCamera != null)
            renderCamera.orthographicSize = size;
    }

    void OnDestroy()
    {
        if (trajectoryRT != null)
        {
            trajectoryRT.Release();
            Destroy(trajectoryRT);
        }
        
        if (tempRT != null)
        {
            tempRT.Release();
            Destroy(tempRT);
        }
        
        if (fadeMaterial != null)
            Destroy(fadeMaterial);
    }
}
