using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class AlgorithmManager : MonoBehaviour
{
    [SerializeField] private List<Drone> allDrones = new List<Drone>();
    [SerializeField] private int _dronesCount;

    [Tooltip("conside seach area")]
    [SerializeField] private Collider _searchArea;

    private Dictionary<string, AlgorithmBase> _availableStrategies;

    [SerializeField] private AlgorithmBase _currentAlgorithm;
    public string CurrentAlgorithmName => _currentAlgorithm?.AlgorithmName ?? "None";

    [Header("Coverage Map Settings")]
    [Tooltip("The Render Texture used as the coverage map.")]
    [SerializeField]
    private RenderTexture coverageMapToClear;
    void Start()
    {
  
        InitializeAvailableStrategies();

        if (_availableStrategies.Count > 0)
        {
            // Default set the first one
            SetAlgorithm(_availableStrategies.Keys.First());
        }
        else
        {
            Debug.LogError("Find no Algorithm,please register it");
        }

    }

    /// <summary>
    /// Register all Algorithm.
    /// </summary>
    private void InitializeAvailableStrategies()
    {
        _availableStrategies = new Dictionary<string, AlgorithmBase>();

        //to do

        //AddAlgorithm(new RandomWalkAlgorithm());
        AddAlgorithm(new PartitionedGridAlgorithm());
        //AddAlgorithm(new BoidsFlockingAlgorithm());
        //AddAlgorithm(new AntColonyAlgorithm());

    }

    private void AddAlgorithm(AlgorithmBase algorithm)
    {
        if (algorithm == null || string.IsNullOrEmpty(algorithm.AlgorithmName))
        {
            Debug.LogWarning("try to add unvaild Algorithm.");
            return;
        }

        if (!_availableStrategies.ContainsKey(algorithm.AlgorithmName))
        {
            _availableStrategies.Add(algorithm.AlgorithmName, algorithm);
        }
        else
        {
            Debug.LogWarning($"'{algorithm.AlgorithmName}' existed.");
        }
    }


    public void SetAlgorithm(string algorithmName)
    {
        if (_availableStrategies.TryGetValue(algorithmName, out AlgorithmBase newAlgorithm))
        {
            if (_currentAlgorithm == newAlgorithm)
            {
                // ...
                return;
            }

            // clean old algorithm
            _currentAlgorithm?.OnAlgorithmEnd();

            // --- 2. 在初始化新算法之前，清空画布！ ---

            _currentAlgorithm = newAlgorithm;
            _currentAlgorithm.Initialize(allDrones, _searchArea);

            Debug.Log($" currentAlgorithm: {_currentAlgorithm.AlgorithmName}");
        }

        else
        {
            Debug.LogError($"Can't find '{algorithmName}' Algorithm！");
        }
    }

 
    void Update()
    {
        if (_currentAlgorithm != null)
        {
            
            _currentAlgorithm.ExecuteAlgorithm();
        }
    }

    /// <summary>
    /// For UI dropdown use.
    /// </summary>
    public List<string> GetAvailableAlgorithmNames()
    {
        return new List<string>(_availableStrategies.Keys);
    }

    /// <summary>
    /// Manually clears the Coverage RenderTexture to black.
    /// </summary>

}