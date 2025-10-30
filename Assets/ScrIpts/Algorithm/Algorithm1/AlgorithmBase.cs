using System.Collections.Generic;
using UnityEngine;


public abstract class AlgorithmBase : MonoBehaviour
{
    [Header("Algorithm Info")]
    [SerializeField] protected string algorithmName = "Unnamed Algorithm";

    [Tooltip("Algorithm Description")]
    [TextArea(2, 4)]
    [SerializeField] protected string algorithmDescription = "";

    [Header("References")]
    [Tooltip("Drone List")]
    [SerializeField] protected List<Drone> drones = new List<Drone>();

    [Tooltip("Search Area Collider")]
    [SerializeField] protected Collider searchAreaCollider;

    [Header("Common Settings")]
    [Tooltip("Enable this algorithm")]
    [SerializeField] protected bool isEnabled = true;

    [Tooltip("Show debug info")]
    [SerializeField] protected bool showDebugInfo = true;


    protected Bounds searchBounds;
    protected bool isInitialized = false;

 
    public virtual string AlgorithmName
    {
        get { return algorithmName; }
        set { algorithmName = value; }
    }


    public string Description => algorithmDescription;


    public bool IsInitialized => isInitialized;


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
            Debug.LogError($"{AlgorithmName}:  Collider is null！");
            return;
        }

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log($"✅ {AlgorithmName} initialized");
            Debug.Log($"   Drone count: {drones.Count}");
            Debug.Log($"   Search area: {searchBounds.size}");
        }
    }


    public abstract void ExecuteAlgorithm();


    public virtual void OnAlgorithmEnd()
    {
        isInitialized = false;

        if (showDebugInfo)
        {
            Debug.Log($"🛑 {AlgorithmName} Stopped");
        }
    }

 
    public virtual void OnAlgorithmPause()
    {
        if (showDebugInfo)
        {
            Debug.Log($"⏸️ {AlgorithmName} Paused");
        }
    }

    public virtual void OnAlgorithmResume()
    {
        if (showDebugInfo)
        {
            Debug.Log($"▶️ {AlgorithmName} Resumed");
        }
    }

    #region  Helper Methods


    protected Vector3 GetRandomPointInBounds()
    {
        return new Vector3(
            Random.Range(searchBounds.min.x, searchBounds.max.x),
            Random.Range(searchBounds.min.y, searchBounds.max.y),
            Random.Range(searchBounds.min.z, searchBounds.max.z)
        );
    }

 
    protected Vector3 GetDronePosition(int droneIndex)
    {
        if (droneIndex >= 0 && droneIndex < drones.Count && drones[droneIndex] != null)
        {
            return drones[droneIndex].Position;
        }
        return Vector3.zero;
    }


    protected bool IsPointInBounds(Vector3 point)
    {
        return searchBounds.Contains(point);
    }

    protected Vector3 ClampPointToBounds(Vector3 point)
    {
        return new Vector3(
            Mathf.Clamp(point.x, searchBounds.min.x, searchBounds.max.x),
            Mathf.Clamp(point.y, searchBounds.min.y, searchBounds.max.y),
            Mathf.Clamp(point.z, searchBounds.min.z, searchBounds.max.z)
        );
    }

    #endregion

    #region 

    protected virtual void Awake()
    {
        
    }

    protected virtual void Start()
    {
        
    }

    protected virtual void OnDestroy()
    {
        OnAlgorithmEnd();
    }

    #endregion


#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        if (!showDebugInfo || searchAreaCollider == null) return;
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireCube(searchBounds.center, searchBounds.size);
    }
#endif
}