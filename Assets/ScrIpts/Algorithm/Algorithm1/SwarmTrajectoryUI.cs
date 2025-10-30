using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 优化的UI显示系统 - 支持TextMeshPro和自动地图大小同步
/// Optimized UI system - Supports TextMeshPro and auto map size sync
/// </summary>
public class SwarmTrajectoryUI : MonoBehaviour
{
    [Header("References")]
    public SwarmTrajectoryDrawer trajectoryDrawer;
    public Collider searchAreaCollider;

    [Header("UI Settings")]
    public Vector2 mapSize = new Vector2(300, 300);
    public bool autoSyncMapSize = true;
    [Range(0.1f, 2f)]
    public float mapDisplayScale = 1f;
    public Vector2 mapPosition = new Vector2(20, 20);
    public float updateInterval = 0.2f;

    [Header("Text Settings")]
    public bool useTextMeshPro = true;
    public int fontSize = 14;
    public string clearButtonText = " lear";

    [Header("Color Settings")]
    public Color panelBackgroundColor = new Color(0, 0, 0, 0.8f);
    public Color buttonColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);
    public Color textColor = Color.white;
    public Color borderColor = new Color(1, 1, 1, 0.5f);

    private RawImage trajectoryMapImage;
    private TextMeshProUGUI statsTextTMP;
    private Text statsTextLegacy;
    private Button clearButton;
    private GameObject uiPanel;
    private float nextUpdateTime;

    void Start()
    {
        if (trajectoryDrawer == null)
        {
            trajectoryDrawer = FindFirstObjectByType<SwarmTrajectoryDrawer>();
            if (trajectoryDrawer == null)
            {
                Debug.LogError("❌ TrajectoryUI: cant find SwarmTrajectoryDrawer！");
                enabled = false;
                return;
            }
        }


        if (searchAreaCollider == null && autoSyncMapSize)
        {
            AlgorithmManager manager = FindFirstObjectByType<AlgorithmManager>();
            if (manager != null)
            {
                var field = manager.GetType().GetField("_searchArea",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
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
                    if (col.gameObject.name.Contains("Search") ||
                        col.gameObject.name.Contains("Area"))
                    {
                        searchAreaCollider = col;
                        break;
                    }
                }
            }
        }

        SyncMapSizeWithSearchArea();

        CreateUI();

        Debug.Log("✅ TrajectoryUI Initialized");
    }

    void SyncMapSizeWithSearchArea()
    {
        if (!autoSyncMapSize || searchAreaCollider == null)
            return;

        Bounds bounds = searchAreaCollider.bounds;
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.z);
        float baseSize = 300f; 
        mapSize = new Vector2(baseSize, baseSize) * mapDisplayScale;
        if (trajectoryDrawer != null)
        {
            trajectoryDrawer.SetWorldCenter(bounds.center);
            trajectoryDrawer.SetWorldSize(maxDimension);
        }

        Debug.Log($"📐 Map size has synced: {mapSize}, Search size: {bounds.size}, MaxDimension: {maxDimension}");
    }

    void CreateUI()
    {
        // 创建或查找 Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("TrajectoryCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        uiPanel = new GameObject("TrajectoryPanel");
        uiPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = uiPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = mapPosition;
        panelRect.sizeDelta = new Vector2(mapSize.x + 20, mapSize.y + 150);

        Image panelBg = uiPanel.AddComponent<Image>();
        panelBg.color = panelBackgroundColor;
        CreateTrajectoryMap();
        CreateStatsText();
        CreateClearButton();
    }

    void CreateTrajectoryMap()
    {
        GameObject mapObj = new GameObject("TrajectoryMap");
        mapObj.transform.SetParent(uiPanel.transform, false);

        RectTransform mapRect = mapObj.AddComponent<RectTransform>();
        mapRect.anchorMin = new Vector2(0.5f, 1);
        mapRect.anchorMax = new Vector2(0.5f, 1);
        mapRect.pivot = new Vector2(0.5f, 1);
        mapRect.anchoredPosition = new Vector2(0, -10);
        mapRect.sizeDelta = mapSize;

        trajectoryMapImage = mapObj.AddComponent<RawImage>();
        trajectoryMapImage.texture = trajectoryDrawer.GetTrajectoryTexture();

        GameObject border = new GameObject("Border");
        border.transform.SetParent(mapObj.transform, false);
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        Image borderImage = border.AddComponent<Image>();
        borderImage.color = borderColor;
        border.transform.SetAsFirstSibling();
    }

    void CreateStatsText()
    {
        GameObject textObj = new GameObject("StatsText");
        textObj.transform.SetParent(uiPanel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 0);
        textRect.pivot = new Vector2(0.5f, 0);
        textRect.anchoredPosition = new Vector2(0, 40);
        textRect.sizeDelta = new Vector2(-20, 80);

        if (useTextMeshPro)
        {
            statsTextTMP = textObj.AddComponent<TextMeshProUGUI>();
            statsTextTMP.fontSize = fontSize;
            statsTextTMP.color = textColor;
            statsTextTMP.alignment = TextAlignmentOptions.TopLeft;
            statsTextTMP.text = "Loading stats...";

            Debug.Log("✅ Use TextMeshPro");
        }
        else
        {
             statsTextLegacy = textObj.AddComponent<Text>();
            statsTextLegacy.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statsTextLegacy.fontSize = fontSize;
            statsTextLegacy.color = textColor;
            statsTextLegacy.alignment = TextAnchor.UpperLeft;
            statsTextLegacy.text = "Loading stats...";

            Debug.Log("ℹ️ Use Text");
        }
    }

    void CreateClearButton()
    {
        GameObject buttonObj = new GameObject("ClearButton");
        buttonObj.transform.SetParent(uiPanel.transform, false);

        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0, 0);
        buttonRect.anchorMax = new Vector2(1, 0);
        buttonRect.pivot = new Vector2(0.5f, 0);
        buttonRect.anchoredPosition = new Vector2(0, 5);
        buttonRect.sizeDelta = new Vector2(-20, 30);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = buttonColor;

        clearButton = buttonObj.AddComponent<Button>();
        clearButton.onClick.AddListener(OnClearButtonClicked);

        // 按钮文本
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        if (useTextMeshPro)
        {
            TextMeshProUGUI buttonText = textObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = clearButtonText;
            buttonText.fontSize = fontSize;
            buttonText.color = textColor;
            buttonText.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            Text buttonText = textObj.AddComponent<Text>();
            buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            buttonText.text = clearButtonText;
            buttonText.fontSize = fontSize;
            buttonText.color = textColor;
            buttonText.alignment = TextAnchor.MiddleCenter;
        }
    }

    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updateInterval;
            UpdateStats();
        }
    }

    void UpdateStats()
    {
        if (trajectoryDrawer == null) return;

        float distance = trajectoryDrawer.GetTotalDistance();
        int droneCount = trajectoryDrawer.GetDroneCount();

        string statsInfo =
            $"Total Drones: {droneCount}\n" +
            $"Total Distance: {distance:F1} m\n" +
            $"Status: Tracking...";

        if (useTextMeshPro && statsTextTMP != null)
        {
            statsTextTMP.text = statsInfo;
        }
        else if (statsTextLegacy != null)
        {
            statsTextLegacy.text = statsInfo;
        }
    }

    void OnClearButtonClicked()
    {
        if (trajectoryDrawer != null)
        {
            trajectoryDrawer.ClearAllTrajectories();
            Debug.Log("🧹 Clear");
        }
    }


    public void ResyncMapSize()
    {
        SyncMapSizeWithSearchArea();
        if (uiPanel != null)
        {
            RectTransform panelRect = uiPanel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(mapSize.x + 20, mapSize.y + 150);
        }
        if (trajectoryMapImage != null)
        {
            RectTransform mapRect = trajectoryMapImage.GetComponent<RectTransform>();
            mapRect.sizeDelta = mapSize;
        }
    }

    public void SetVisible(bool visible)
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(visible);
        }
    }

    public void SetSearchAreaCollider(Collider collider)
    {
        searchAreaCollider = collider;
        if (autoSyncMapSize)
        {
            ResyncMapSize();
        }
    }
}