using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 算法基类 - 现在继承 MonoBehaviour，可在 Inspector 中直接调整参数
/// Algorithm Base Class - Now inherits MonoBehaviour for Inspector editing
/// </summary>
public abstract class AlgorithmBase : MonoBehaviour
{
    [Header("Algorithm Info")]
    [Tooltip("算法名称 Algorithm Name")]
    [SerializeField] protected string algorithmName = "Unnamed Algorithm";

    [Tooltip("算法描述 Algorithm Description")]
    [TextArea(2, 4)]
    [SerializeField] protected string algorithmDescription = "";

    [Header("References")]
    [Tooltip("无人机列表 Drone List")]
    [SerializeField] protected List<Drone> drones = new List<Drone>();

    [Tooltip("搜索区域 Search Area Collider")]
    [SerializeField] protected Collider searchAreaCollider;

    [Header("Common Settings")]
    [Tooltip("是否启用此算法 Enable this algorithm")]
    [SerializeField] protected bool isEnabled = true;

    [Tooltip("显示调试信息 Show debug info")]
    [SerializeField] protected bool showDebugInfo = true;

    // 内部状态
    protected Bounds searchBounds;
    protected bool isInitialized = false;

    /// <summary>
    /// 算法名称（可被子类覆盖）
    /// </summary>
    public virtual string AlgorithmName
    {
        get { return algorithmName; }
        set { algorithmName = value; }
    }

    /// <summary>
    /// 算法描述
    /// </summary>
    public string Description => algorithmDescription;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// 初始化算法 - 由 AlgorithmManager 调用
    /// </summary>
    public virtual void Initialize(List<Drone> drones, Collider searchArea)
    {
        this.drones = drones;
        this.searchAreaCollider = searchArea;

        if (searchArea != null)
        {
            this.searchBounds = searchArea.bounds;
        }
        else
        {
            Debug.LogError($"{AlgorithmName}: 搜索区域 Collider 为空！");
            return;
        }

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log($"✅ {AlgorithmName} 已初始化");
            Debug.Log($"   无人机数量: {drones.Count}");
            Debug.Log($"   搜索区域: {searchBounds.size}");
        }
    }

    /// <summary>
    /// 算法核心逻辑 - 每帧调用
    /// </summary>
    public abstract void ExecuteAlgorithm();

    /// <summary>
    /// 算法结束时调用 - 清理资源
    /// </summary>
    public virtual void OnAlgorithmEnd()
    {
        isInitialized = false;

        if (showDebugInfo)
        {
            Debug.Log($"🛑 {AlgorithmName} 已停止");
        }
    }

    /// <summary>
    /// 算法暂停
    /// </summary>
    public virtual void OnAlgorithmPause()
    {
        if (showDebugInfo)
        {
            Debug.Log($"⏸️ {AlgorithmName} 已暂停");
        }
    }

    /// <summary>
    /// 算法恢复
    /// </summary>
    public virtual void OnAlgorithmResume()
    {
        if (showDebugInfo)
        {
            Debug.Log($"▶️ {AlgorithmName} 已恢复");
        }
    }

    #region 辅助方法 Helper Methods

    /// <summary>
    /// 获取搜索区域内的随机点
    /// </summary>
    protected Vector3 GetRandomPointInBounds()
    {
        return new Vector3(
            Random.Range(searchBounds.min.x, searchBounds.max.x),
            Random.Range(searchBounds.min.y, searchBounds.max.y),
            Random.Range(searchBounds.min.z, searchBounds.max.z)
        );
    }

    /// <summary>
    /// 获取指定无人机的位置
    /// </summary>
    protected Vector3 GetDronePosition(int droneIndex)
    {
        if (droneIndex >= 0 && droneIndex < drones.Count && drones[droneIndex] != null)
        {
            return drones[droneIndex].Position;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// 检查点是否在搜索区域内
    /// </summary>
    protected bool IsPointInBounds(Vector3 point)
    {
        return searchBounds.Contains(point);
    }

    /// <summary>
    /// 将点限制在搜索区域内
    /// </summary>
    protected Vector3 ClampPointToBounds(Vector3 point)
    {
        return new Vector3(
            Mathf.Clamp(point.x, searchBounds.min.x, searchBounds.max.x),
            Mathf.Clamp(point.y, searchBounds.min.y, searchBounds.max.y),
            Mathf.Clamp(point.z, searchBounds.min.z, searchBounds.max.z)
        );
    }

    #endregion

    #region Unity 生命周期（可选覆盖）

    protected virtual void Awake()
    {
        // 子类可以覆盖
    }

    protected virtual void Start()
    {
        // 子类可以覆盖
    }

    protected virtual void OnDestroy()
    {
        OnAlgorithmEnd();
    }

    #endregion

    #region Gizmos（可选覆盖）

    protected virtual void OnDrawGizmos()
    {
        // 子类可以覆盖以添加自定义可视化
        if (!showDebugInfo || searchAreaCollider == null) return;

        // 绘制搜索区域边界
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireCube(searchBounds.center, searchBounds.size);
    }

    #endregion
}