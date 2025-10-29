using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the drone trajectory render texture on a UI element or world space quad
/// </summary>
public class TrajectoryDisplay : MonoBehaviour
{
    [Header("Display Target")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.WorldQuad;
    [SerializeField] private RawImage uiImage;
    [SerializeField] private MeshRenderer quadRenderer;
    
    [Header("Trajectory Source")]
    [SerializeField] private DroneTrajectoryDrawer trajectoryDrawer;
    [SerializeField] private GPUTrajectoryDrawer gpuDrawer;
    
    [Header("Display Settings")]
    [SerializeField] private bool autoUpdate = true;
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private Material displayMaterial;
    
    [Header("Mini-Map Settings")]
    [SerializeField] private bool isMiniMap = false;
    [SerializeField] private Vector2 miniMapSize = new Vector2(200, 200);
    [SerializeField] private Vector2 miniMapPosition = new Vector2(10, 10);
    
    private RenderTexture currentTexture;
    private float nextUpdateTime;

    public enum DisplayMode
    {
        WorldQuad,
        UIRawImage,
        MiniMap
    }

    void Start()
    {
        SetupDisplay();
        UpdateTexture();
    }

    void SetupDisplay()
    {
        // Get the render texture from the drawer
        if (trajectoryDrawer != null)
        {
            currentTexture = trajectoryDrawer.GetTrajectoryTexture();
        }
        else if (gpuDrawer != null)
        {
            currentTexture = gpuDrawer.GetTexture();
        }
        
        if (currentTexture == null)
        {
            Debug.LogWarning("TrajectoryDisplay: No render texture found!");
            return;
        }

        switch (displayMode)
        {
            case DisplayMode.WorldQuad:
                SetupWorldQuad();
                break;
                
            case DisplayMode.UIRawImage:
                SetupUIImage();
                break;
                
            case DisplayMode.MiniMap:
                SetupMiniMap();
                break;
        }
    }

    void SetupWorldQuad()
    {
        if (quadRenderer == null)
        {
            // Create a quad if not assigned
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(transform);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            quad.transform.localScale = new Vector3(100, 100, 1);
            
            quadRenderer = quad.GetComponent<MeshRenderer>();
            
            // Remove collider
            Destroy(quad.GetComponent<Collider>());
        }
        
        // Setup material
        if (displayMaterial == null)
        {
            displayMaterial = new Material(Shader.Find("Unlit/Transparent"));
        }
        
        displayMaterial.mainTexture = currentTexture;
        quadRenderer.material = displayMaterial;
    }

    void SetupUIImage()
    {
        if (uiImage == null)
        {
            Debug.LogWarning("TrajectoryDisplay: UI RawImage not assigned!");
            return;
        }
        
        uiImage.texture = currentTexture;
    }

    void SetupMiniMap()
    {
        // Create UI canvas if needed
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("TrajectoryCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
        
        // Create RawImage for mini-map
        GameObject miniMapObj = new GameObject("MiniMap");
        miniMapObj.transform.SetParent(canvas.transform);
        
        uiImage = miniMapObj.AddComponent<RawImage>();
        uiImage.texture = currentTexture;
        
        // Set size and position
        RectTransform rect = miniMapObj.GetComponent<RectTransform>();
        rect.sizeDelta = miniMapSize;
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = miniMapPosition;
        
        // Add border
        GameObject border = new GameObject("Border");
        border.transform.SetParent(miniMapObj.transform);
        Image borderImage = border.AddComponent<Image>();
        borderImage.color = new Color(1, 1, 1, 0.3f);
        
        RectTransform borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        border.transform.SetAsFirstSibling();
    }

    void Update()
    {
        if (autoUpdate && Time.time >= nextUpdateTime)
        {
            UpdateTexture();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    void UpdateTexture()
    {
        // Texture updates automatically since it's a RenderTexture
        // This method is here for manual updates if needed
    }

    /// <summary>
    /// Manually set the texture to display
    /// </summary>
    public void SetTexture(RenderTexture texture)
    {
        currentTexture = texture;
        
        switch (displayMode)
        {
            case DisplayMode.WorldQuad:
                if (quadRenderer != null && quadRenderer.material != null)
                    quadRenderer.material.mainTexture = texture;
                break;
                
            case DisplayMode.UIRawImage:
            case DisplayMode.MiniMap:
                if (uiImage != null)
                    uiImage.texture = texture;
                break;
        }
    }

    /// <summary>
    /// Toggle display visibility
    /// </summary>
    public void SetVisible(bool visible)
    {
        switch (displayMode)
        {
            case DisplayMode.WorldQuad:
                if (quadRenderer != null)
                    quadRenderer.enabled = visible;
                break;
                
            case DisplayMode.UIRawImage:
            case DisplayMode.MiniMap:
                if (uiImage != null)
                    uiImage.enabled = visible;
                break;
        }
    }
}
