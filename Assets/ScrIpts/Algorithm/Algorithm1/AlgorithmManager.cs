using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AlgorithmManager : MonoBehaviour
{
    [Header("Drone Configuration")]
    [Tooltip("All drones")]
    [SerializeField] private List<Drone> allDrones = new List<Drone>();

    [Tooltip("Auto-find drones in scene")]
    [SerializeField] private bool autoFindDrones = true;

    [Header("Search Area")]
    [SerializeField] private Collider searchAreaCollider;
    [SerializeField] private bool autoFindSearchArea = true;

    [Header("Available Algorithms")]

    [SerializeField] private List<AlgorithmBase> availableAlgorithms = new List<AlgorithmBase>();

    [Header("Algorithm Control")]
    [Tooltip(" Current active algorithm index")]
    [SerializeField] private int currentAlgorithmIndex = 0;

    [Tooltip(" uto-initialize on start")]
    [SerializeField] private bool autoInitializeOnStart = true;

    [Header("Debug")]
    [Tooltip("Show debug info")]
    [SerializeField] private bool showDebugInfo = true;

    private AlgorithmBase currentAlgorithm;
    public string CurrentAlgorithmName => currentAlgorithm != null ? currentAlgorithm.AlgorithmName : "None";

    public int CurrentAlgorithmIndex => currentAlgorithmIndex;
    public int AlgorithmCount => availableAlgorithms.Count;

    void Start()
    {
        if (autoFindDrones && allDrones.Count == 0)
        {
            FindAllDrones();
        }

        if (autoFindSearchArea && searchAreaCollider == null)
        {
            FindSearchArea();
        }

        if (!ValidateConfiguration())
        {
            Debug.LogError("❌ AlgorithmManager: faild！");
            enabled = false;
            return;
        }

        if (autoInitializeOnStart && availableAlgorithms.Count > 0)
        {
            SetAlgorithmByIndex(currentAlgorithmIndex);
        }
    }

    void Update()
    {
        if (currentAlgorithm != null && currentAlgorithm.IsInitialized)
        {
            currentAlgorithm.ExecuteAlgorithm();
        }
    }

    #region Algorithm Management

    public void SetAlgorithmByIndex(int index)
    {
        if (index < 0 || index >= availableAlgorithms.Count)
        {

            return;
        }

        AlgorithmBase newAlgorithm = availableAlgorithms[index];

        if (newAlgorithm == null)
        {
            Debug.LogError($"❌ Algorithm index: {index} is null！");
            return;
        }

        if (currentAlgorithm == newAlgorithm)
        {
            if (showDebugInfo)
            {
                Debug.Log($"ℹ️ Algorithm '{newAlgorithm.AlgorithmName}' is running");
            }
            return;
        }

        if (currentAlgorithm != null)
        {
            currentAlgorithm.OnAlgorithmEnd();
        }

        currentAlgorithm = newAlgorithm;
        currentAlgorithmIndex = index;
        currentAlgorithm.Initialize(allDrones, searchAreaCollider);

        if (showDebugInfo)
        {
            Debug.Log($"🔄 Change to: {currentAlgorithm.AlgorithmName} (Index: {index})");
        }
    }


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

        Debug.LogError($"❌ Can't find '{algorithmName}' ！");
    }


    public void NextAlgorithm()
    {
        int nextIndex = (currentAlgorithmIndex + 1) % availableAlgorithms.Count;
        SetAlgorithmByIndex(nextIndex);
    }


    public void PreviousAlgorithm()
    {
        int prevIndex = currentAlgorithmIndex - 1;
        if (prevIndex < 0) prevIndex = availableAlgorithms.Count - 1;
        SetAlgorithmByIndex(prevIndex);
    }

    public void PauseCurrentAlgorithm()
    {
        if (currentAlgorithm != null)
        {
            currentAlgorithm.OnAlgorithmPause();
            enabled = false; 
        }
    }

    public void ResumeCurrentAlgorithm()
    {
        if (currentAlgorithm != null)
        {
            currentAlgorithm.OnAlgorithmResume();
            enabled = true;
        }
    }


    public void RestartCurrentAlgorithm()
    {
        if (currentAlgorithm != null)
        {
            SetAlgorithmByIndex(currentAlgorithmIndex);
        }
    }

    #endregion

    #region  Configuration Management

    private void FindAllDrones()
    {
        Drone[] foundDrones = FindObjectsOfType<Drone>();
        allDrones = foundDrones.ToList();

        if (showDebugInfo)
        {
            Debug.Log($"🔍 Find {allDrones.Count} Drones");
        }
    }


    private void FindSearchArea()
    {

        Collider[] allColliders = FindObjectsOfType<Collider>();

        foreach (var col in allColliders)
        {
            if (col.gameObject.name.ToLower().Contains("search") ||
                col.gameObject.name.ToLower().Contains("area"))
            {
                searchAreaCollider = col;
                if (showDebugInfo)
                {
                    Debug.Log($"🔍 Find search area: {col.gameObject.name}");
                }
                return;
            }
        }

        Debug.LogWarning("⚠️ No search area collider!");
    }


    private bool ValidateConfiguration()
    {
        bool isValid = true;

        if (allDrones.Count == 0)
        {
            Debug.LogError("❌ no vaild drone！");
            isValid = false;
        }

        if (searchAreaCollider == null)
        {
            Debug.LogError("❌ have't set collider！");
            isValid = false;
        }

        if (availableAlgorithms.Count == 0)
        {
            Debug.LogError("❌ No vaild algorithm.");
            isValid = false;
        }
        availableAlgorithms.RemoveAll(a => a == null);

        return isValid;
    }


    public void AddAlgorithm(AlgorithmBase algorithm)
    {
        if (algorithm == null)
        {
            return;
        }

        if (!availableAlgorithms.Contains(algorithm))
        {
            availableAlgorithms.Add(algorithm);
            if (showDebugInfo)
            {
                Debug.Log($"➕ Add algorithm: {algorithm.AlgorithmName}");
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ Algorithm '{algorithm.AlgorithmName}' existed！");
        }
    }

 
    public void RemoveAlgorithm(AlgorithmBase algorithm)
    {
        if (algorithm == currentAlgorithm)
        {
            Debug.LogError("❌ cant move running algorithm！");
            return;
        }

        if (availableAlgorithms.Remove(algorithm))
        {
            if (showDebugInfo)
            {
                Debug.Log($"➖ Remove algorithm: {algorithm.AlgorithmName}");
            }
        }
    }

    #endregion

    #region Query Interface


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


    public AlgorithmBase GetCurrentAlgorithm()
    {
        return currentAlgorithm;
    }


    public AlgorithmBase GetAlgorithmByIndex(int index)
    {
        if (index >= 0 && index < availableAlgorithms.Count)
        {
            return availableAlgorithms[index];
        }
        return null;
    }

    #endregion


}