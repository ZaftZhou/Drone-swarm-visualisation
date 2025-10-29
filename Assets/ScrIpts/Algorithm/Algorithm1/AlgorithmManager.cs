using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 算法管理器 - 使用 List 管理，更直观易调试
/// Algorithm Manager - Uses List for better debugging and visualization
/// </summary>
public class AlgorithmManager : MonoBehaviour
{
    [Header("Drone Configuration")]
    [Tooltip("所有无人机列表 All drones")]
    [SerializeField] private List<Drone> allDrones = new List<Drone>();

    [Tooltip("自动查找场景中的无人机 Auto-find drones in scene")]
    [SerializeField] private bool autoFindDrones = true;

    [Header("Search Area")]
    [Tooltip("搜索区域 Collider")]
    [SerializeField] private Collider searchAreaCollider;

    [Tooltip("自动查找搜索区域 Auto-find search area")]
    [SerializeField] private bool autoFindSearchArea = true;

    [Header("Available Algorithms")]
    [Tooltip("所有可用的算法（直接在这里添加/管理）")]
    [SerializeField] private List<AlgorithmBase> availableAlgorithms = new List<AlgorithmBase>();

    [Header("Algorithm Control")]
    [Tooltip("当前活动的算法索引 Current active algorithm index")]
    [SerializeField] private int currentAlgorithmIndex = 0;

    [Tooltip("在开始时自动初始化算法 Auto-initialize on start")]
    [SerializeField] private bool autoInitializeOnStart = true;

    [Header("Debug")]
    [Tooltip("显示调试信息 Show debug info")]
    [SerializeField] private bool showDebugInfo = true;

    // 当前运行的算法
    private AlgorithmBase currentAlgorithm;

    // 属性：获取当前算法名称
    public string CurrentAlgorithmName => currentAlgorithm != null ? currentAlgorithm.AlgorithmName : "None";

    // 属性：获取当前算法索引
    public int CurrentAlgorithmIndex => currentAlgorithmIndex;

    // 属性：获取算法数量
    public int AlgorithmCount => availableAlgorithms.Count;

    void Start()
    {
        // 自动查找无人机
        if (autoFindDrones && allDrones.Count == 0)
        {
            FindAllDrones();
        }

        // 自动查找搜索区域
        if (autoFindSearchArea && searchAreaCollider == null)
        {
            FindSearchArea();
        }

        // 验证配置
        if (!ValidateConfiguration())
        {
            Debug.LogError("❌ AlgorithmManager: 配置验证失败！");
            enabled = false;
            return;
        }

        // 自动初始化第一个算法
        if (autoInitializeOnStart && availableAlgorithms.Count > 0)
        {
            SetAlgorithmByIndex(currentAlgorithmIndex);
        }
    }

    void Update()
    {
        // 执行当前算法
        if (currentAlgorithm != null && currentAlgorithm.IsInitialized)
        {
            currentAlgorithm.ExecuteAlgorithm();
        }
    }

    #region 算法管理 Algorithm Management

    /// <summary>
    /// 通过索引设置算法
    /// </summary>
    public void SetAlgorithmByIndex(int index)
    {
        if (index < 0 || index >= availableAlgorithms.Count)
        {
            Debug.LogError($"❌ 算法索引超出范围: {index} (总数: {availableAlgorithms.Count})");
            return;
        }

        AlgorithmBase newAlgorithm = availableAlgorithms[index];

        if (newAlgorithm == null)
        {
            Debug.LogError($"❌ 索引 {index} 的算法为空！");
            return;
        }

        // 如果已经是当前算法，不做处理
        if (currentAlgorithm == newAlgorithm)
        {
            if (showDebugInfo)
            {
                Debug.Log($"ℹ️ 算法 '{newAlgorithm.AlgorithmName}' 已经在运行");
            }
            return;
        }

        // 停止旧算法
        if (currentAlgorithm != null)
        {
            currentAlgorithm.OnAlgorithmEnd();
        }

        // 启动新算法
        currentAlgorithm = newAlgorithm;
        currentAlgorithmIndex = index;
        currentAlgorithm.Initialize(allDrones, searchAreaCollider);

        if (showDebugInfo)
        {
            Debug.Log($"🔄 切换到算法: {currentAlgorithm.AlgorithmName} (索引: {index})");
        }
    }

    /// <summary>
    /// 通过名称设置算法
    /// </summary>
    public void SetAlgorithmByName(string algorithmName)
    {
        for (int i = 0; i < availableAlgorithms.Count; i++)
        {
            if (availableAlgorithms[i] != null &&
                availableAlgorithms[i].AlgorithmName == algorithmName)
            {
                SetAlgorithmByIndex(i);
                return;
            }
        }

        Debug.LogError($"❌ 找不到名为 '{algorithmName}' 的算法！");
    }

    /// <summary>
    /// 切换到下一个算法
    /// </summary>
    public void NextAlgorithm()
    {
        int nextIndex = (currentAlgorithmIndex + 1) % availableAlgorithms.Count;
        SetAlgorithmByIndex(nextIndex);
    }

    /// <summary>
    /// 切换到上一个算法
    /// </summary>
    public void PreviousAlgorithm()
    {
        int prevIndex = currentAlgorithmIndex - 1;
        if (prevIndex < 0) prevIndex = availableAlgorithms.Count - 1;
        SetAlgorithmByIndex(prevIndex);
    }

    /// <summary>
    /// 暂停当前算法
    /// </summary>
    public void PauseCurrentAlgorithm()
    {
        if (currentAlgorithm != null)
        {
            currentAlgorithm.OnAlgorithmPause();
            enabled = false; // 停止 Update 调用
        }
    }

    /// <summary>
    /// 恢复当前算法
    /// </summary>
    public void ResumeCurrentAlgorithm()
    {
        if (currentAlgorithm != null)
        {
            currentAlgorithm.OnAlgorithmResume();
            enabled = true; // 恢复 Update 调用
        }
    }

    /// <summary>
    /// 重启当前算法
    /// </summary>
    public void RestartCurrentAlgorithm()
    {
        if (currentAlgorithm != null)
        {
            SetAlgorithmByIndex(currentAlgorithmIndex);
        }
    }

    #endregion

    #region 配置管理 Configuration Management

    /// <summary>
    /// 自动查找所有无人机
    /// </summary>
    private void FindAllDrones()
    {
        Drone[] foundDrones = FindObjectsOfType<Drone>();
        allDrones = foundDrones.ToList();

        if (showDebugInfo)
        {
            Debug.Log($"🔍 自动查找到 {allDrones.Count} 架无人机");
        }
    }

    /// <summary>
    /// 自动查找搜索区域
    /// </summary>
    private void FindSearchArea()
    {
        // 查找名称包含 "Search" 或 "Area" 的 Collider
        Collider[] allColliders = FindObjectsOfType<Collider>();

        foreach (var col in allColliders)
        {
            if (col.gameObject.name.ToLower().Contains("search") ||
                col.gameObject.name.ToLower().Contains("area"))
            {
                searchAreaCollider = col;
                if (showDebugInfo)
                {
                    Debug.Log($"🔍 自动找到搜索区域: {col.gameObject.name}");
                }
                return;
            }
        }

        Debug.LogWarning("⚠️ 未找到搜索区域 Collider！请手动指定。");
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    private bool ValidateConfiguration()
    {
        bool isValid = true;

        // 检查无人机
        if (allDrones.Count == 0)
        {
            Debug.LogError("❌ 没有可用的无人机！");
            isValid = false;
        }

        // 检查搜索区域
        if (searchAreaCollider == null)
        {
            Debug.LogError("❌ 未设置搜索区域 Collider！");
            isValid = false;
        }

        // 检查算法
        if (availableAlgorithms.Count == 0)
        {
            Debug.LogError("❌ 没有可用的算法！请在 Inspector 中添加算法组件。");
            isValid = false;
        }

        // 移除空的算法引用
        availableAlgorithms.RemoveAll(a => a == null);

        return isValid;
    }

    /// <summary>
    /// 手动添加算法（运行时）
    /// </summary>
    public void AddAlgorithm(AlgorithmBase algorithm)
    {
        if (algorithm == null)
        {
            Debug.LogError("❌ 尝试添加空的算法！");
            return;
        }

        if (!availableAlgorithms.Contains(algorithm))
        {
            availableAlgorithms.Add(algorithm);
            if (showDebugInfo)
            {
                Debug.Log($"➕ 添加算法: {algorithm.AlgorithmName}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ 算法 '{algorithm.AlgorithmName}' 已存在！");
        }
    }

    /// <summary>
    /// 移除算法（运行时）
    /// </summary>
    public void RemoveAlgorithm(AlgorithmBase algorithm)
    {
        if (algorithm == currentAlgorithm)
        {
            Debug.LogError("❌ 无法移除当前正在运行的算法！");
            return;
        }

        if (availableAlgorithms.Remove(algorithm))
        {
            if (showDebugInfo)
            {
                Debug.Log($"➖ 移除算法: {algorithm.AlgorithmName}");
            }
        }
    }

    #endregion

    #region 查询接口 Query Interface

    /// <summary>
    /// 获取所有算法名称列表（用于 UI 下拉菜单）
    /// </summary>
    public List<string> GetAlgorithmNames()
    {
        List<string> names = new List<string>();
        foreach (var algo in availableAlgorithms)
        {
            if (algo != null)
            {
                names.Add(algo.AlgorithmName);
            }
        }
        return names;
    }

    /// <summary>
    /// 获取当前算法
    /// </summary>
    public AlgorithmBase GetCurrentAlgorithm()
    {
        return currentAlgorithm;
    }

    /// <summary>
    /// 获取指定索引的算法
    /// </summary>
    public AlgorithmBase GetAlgorithmByIndex(int index)
    {
        if (index >= 0 && index < availableAlgorithms.Count)
        {
            return availableAlgorithms[index];
        }
        return null;
    }

    #endregion

    #region Debug & Visualization

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // 显示调试信息
        GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 150));

        GUI.Box(new Rect(0, 0, 300, 150), "");

        GUILayout.Label("<b>Algorithm Manager</b>");
        GUILayout.Label($"当前算法: {CurrentAlgorithmName}");
        GUILayout.Label($"算法索引: {currentAlgorithmIndex + 1}/{availableAlgorithms.Count}");
        GUILayout.Label($"无人机数: {allDrones.Count}");

        if (searchAreaCollider != null)
        {
            GUILayout.Label($"搜索区域: {searchAreaCollider.gameObject.name}");
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀ 上一个"))
        {
            PreviousAlgorithm();
        }
        if (GUILayout.Button("下一个 ▶"))
        {
            NextAlgorithm();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    #endregion
}