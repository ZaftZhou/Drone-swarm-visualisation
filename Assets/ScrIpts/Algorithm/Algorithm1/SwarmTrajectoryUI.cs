using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 优化的UI显示系统 - 支持TextMeshPro和自动地图大小同步
/// Optimized UI system - Supports TextMeshPro and auto map size sync
/// </summary>
public class SwarmTrajectoryUI : MonoBehaviour
{
    [Header("引用 References")]
    [Tooltip("轨迹绘制器（会自动查找）")]
    public SwarmTrajectoryDrawer trajectoryDrawer;

    [Tooltip("搜索区域Collider（用于自动设置地图大小）")]
    public Collider searchAreaCollider;

    [Header("UI设置 UI Settings")]
    [Tooltip("地图大小（如果autoSyncMapSize为true则自动计算）")]
    public Vector2 mapSize = new Vector2(300, 300);

    [Tooltip("自动同步地图大小到搜索区域")]
    public bool autoSyncMapSize = true;

    [Tooltip("地图显示比例（用于缩放显示）")]
    [Range(0.1f, 2f)]
    public float mapDisplayScale = 1f;

    [Tooltip("地图位置偏移")]
    public Vector2 mapPosition = new Vector2(20, 20);

    [Tooltip("更新频率")]
    public float updateInterval = 0.2f;

    [Header("文本设置 Text Settings")]
    [Tooltip("使用TextMeshPro（推荐）")]
    public bool useTextMeshPro = true;

    [Tooltip("字体大小")]
    public int fontSize = 14;

    [Tooltip("清空按钮文本")]
    public string clearButtonText = "清空轨迹 Clear";

    [Header("颜色设置 Color Settings")]
    [Tooltip("面板背景颜色")]
    public Color panelBackgroundColor = new Color(0, 0, 0, 0.8f);

    [Tooltip("按钮颜色")]
    public Color buttonColor = new Color(0.8f, 0.2f, 0.2f, 0.8f);

    [Tooltip("文本颜色")]
    public Color textColor = Color.white;

    [Tooltip("边框颜色")]
    public Color borderColor = new Color(1, 1, 1, 0.5f);

    // 私有变量
    private RawImage trajectoryMapImage;
    private TextMeshProUGUI statsTextTMP;
    private Text statsTextLegacy;
    private Button clearButton;
    private GameObject uiPanel;
    private float nextUpdateTime;

    void Start()
    {
        // 自动查找轨迹绘制器
        if (trajectoryDrawer == null)
        {
            trajectoryDrawer = FindFirstObjectByType<SwarmTrajectoryDrawer>();
            if (trajectoryDrawer == null)
            {
                Debug.LogError("❌ TrajectoryUI: 找不到 SwarmTrajectoryDrawer！");
                enabled = false;
                return;
            }
        }

        // 自动查找搜索区域Collider
        if (searchAreaCollider == null && autoSyncMapSize)
        {
            // 尝试从AlgorithmManager获取
            AlgorithmManager manager = FindFirstObjectByType<AlgorithmManager>();
            if (manager != null)
            {
                // 使用反射获取私有字段（如果需要的话）
                var field = manager.GetType().GetField("_searchArea",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    searchAreaCollider = field.GetValue(manager) as Collider;
                }
            }

            // 如果还是没找到，尝试查找场景中的BoxCollider
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

        // 同步地图大小
        SyncMapSizeWithSearchArea();

        // 创建UI
        CreateUI();

        Debug.Log("✅ TrajectoryUI 初始化完成");
    }

    /// <summary>
    /// 根据搜索区域自动调整地图大小
    /// </summary>
    void SyncMapSizeWithSearchArea()
    {
        if (!autoSyncMapSize || searchAreaCollider == null)
            return;

        Bounds bounds = searchAreaCollider.bounds;
        float maxDimension = Mathf.Max(bounds.size.x, bounds.size.z);

        // 计算合适的地图显示大小（保持正方形，添加缩放）
        float baseSize = 300f; // 基础大小
        mapSize = new Vector2(baseSize, baseSize) * mapDisplayScale;

        // 同步到轨迹绘制器
        if (trajectoryDrawer != null)
        {
            trajectoryDrawer.SetWorldCenter(bounds.center);
            trajectoryDrawer.SetWorldSize(maxDimension);
        }

        Debug.Log($"📐 地图大小已同步: {mapSize}, 搜索区域: {bounds.size}, 最大维度: {maxDimension}");
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

        // 创建主面板
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

        // 创建轨迹地图
        CreateTrajectoryMap();

        // 创建统计文本
        CreateStatsText();

        // 创建清空按钮
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

        // 添加边框
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
            // 使用 TextMeshPro
            statsTextTMP = textObj.AddComponent<TextMeshProUGUI>();
            statsTextTMP.fontSize = fontSize;
            statsTextTMP.color = textColor;
            statsTextTMP.alignment = TextAlignmentOptions.TopLeft;
            statsTextTMP.text = "Loading stats...";

            Debug.Log("✅ 使用 TextMeshPro");
        }
        else
        {
            // 使用传统 Text
            statsTextLegacy = textObj.AddComponent<Text>();
            statsTextLegacy.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statsTextLegacy.fontSize = fontSize;
            statsTextLegacy.color = textColor;
            statsTextLegacy.alignment = TextAnchor.UpperLeft;
            statsTextLegacy.text = "Loading stats...";

            Debug.Log("ℹ️ 使用传统 Text");
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
            $"无人机数 Drones: {droneCount}\n" +
            $"总距离 Distance: {distance:F1} m\n" +
            $"状态 Status: 追踪中 Tracking...";

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
            Debug.Log("🧹 已清空轨迹");
        }
    }

    /// <summary>
    /// 手动重新同步地图大小
    /// </summary>
    public void ResyncMapSize()
    {
        SyncMapSizeWithSearchArea();

        // 更新UI面板大小
        if (uiPanel != null)
        {
            RectTransform panelRect = uiPanel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(mapSize.x + 20, mapSize.y + 150);
        }

        // 更新地图大小
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

    /// <summary>
    /// 设置搜索区域引用
    /// </summary>
    public void SetSearchAreaCollider(Collider collider)
    {
        searchAreaCollider = collider;
        if (autoSyncMapSize)
        {
            ResyncMapSize();
        }
    }
}