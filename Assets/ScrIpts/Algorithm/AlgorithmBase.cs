using System.Collections.Generic;
using UnityEngine;


public abstract class AlgorithmBase:MonoBehaviour
{

    protected List<GameObject> drones;
    protected Bounds searchBounds;

    public abstract string AlgorithmName { get; }

    /// <summary>
    /// Initializes the algorithm. Called by the manager when this algorithm is selected.
    /// </summary>
    public virtual void Initialize(List<GameObject> drones, Collider searchArea)
    {
        this.drones = drones;
        this.searchBounds = searchArea.bounds;
        Debug.Log($"Algorithm {AlgorithmName} Initialized.");
    }

    /// <summary>
    /// The core logic of the algorithm.
    /// </summary>
    public abstract void ExecuteAlgorithm();


    public virtual void OnAlgorithmEnd()
    {
        // default:do nothing
    }

    #region ExtendMethod
    protected Vector3 GetRandomPointInBounds()
    {
        return new Vector3(
            Random.Range(searchBounds.min.x, searchBounds.max.x),
            Random.Range(searchBounds.min.y, searchBounds.max.y),
            Random.Range(searchBounds.min.z, searchBounds.max.z)
        );
    }
    #endregion
}