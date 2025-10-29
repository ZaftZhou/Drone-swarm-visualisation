using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 简化的UI显示系统 - 修复版
/// Simplified UI system - Fixed version
/// </summary>
public class SwarmTrajectoryUI : MonoBehaviour
{
    [Header("引用 References")]
    [Tooltip("轨迹绘制器（会自动查找）")]
    public SwarmTrajectoryDrawer trajectoryDrawer;

    [Header("UI设置 UI Settings")]
    [Tooltip("地图大小")]
    public Vector2 mapSize = new Vector2(300, 300);

    [Tooltip("地图位置偏移")]
    public Vector2 mapPosition = new Vector2(20, 20);

    [Tooltip("更新频率")]
    public float updateInterval = 0.2f;

    [Tooltip("清空按钮文本")]
    public string clearButtonText = "清空轨迹 Clear";

    private RawImage trajectoryMapImage;
    private TMP_Text statsText;
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

        CreateUI();
        Debug.Log("✅ TrajectoryUI 初始化完成");
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
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
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
        panelBg.color = new Color(0, 0, 0, 0.8f);

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
        borderImage.color = new Color(1, 1, 1, 0.5f);
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

        statsText = textObj.AddComponent<TMP_Text>();
        statsText.fontSize = 14;
        statsText.color = Color.white;
        statsText.alignment = TextAlignmentOptions.TopLeft;
        statsText.text = "Loading stats...";
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
        buttonImage.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);

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

        Text buttonText = textObj.AddComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.text = clearButtonText;
        buttonText.fontSize = 14;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
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
        if (trajectoryDrawer == null || statsText == null) return;

        float distance = trajectoryDrawer.GetTotalDistance();
        int droneCount = trajectoryDrawer.GetDroneCount();

        statsText.text =
            $"无人机数: {droneCount}\n" +
            $"总距离: {distance:F1} m\n" +
            $"状态: 追踪中...";
    }

    void OnClearButtonClicked()
    {
        if (trajectoryDrawer != null)
        {
            trajectoryDrawer.ClearAllTrajectories();
            Debug.Log("🧹 已清空轨迹");
        }
    }

    public void SetVisible(bool visible)
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(visible);
        }
    }
}