using UnityEngine;


/// <summary>
/// A data container script attached to each drone prefab. 
/// It holds all the necessary state variables for the Particle Swarm Optimization (PSO) algorithm
/// and provides the reference to the local avoidance component.
/// </summary>
public class DroneDetails : MonoBehaviour 
{
    [Header("Current State")]
    public Vector3 position;
    public Vector3 velocity;

    [Header("Personal Best (pBest)")]
    public Vector3 pBestPos;
    public float pbestFScore;
    public float pbestObjDist;
    public float pbestNodeDist;
    public Vector3 pbestNodePos;

    [Header("Identification")]
    public int droneId;

    [Header("Components")]
    [HideInInspector] // Hidden because it's assigned programmatically in ParticleSwarmAlgorithm
    public DroneCollisionCheck droneCollisionCheck;


    /// <summary>
    /// Initializes the drone's position and sets initial pBest values for the PSO loop.
    /// </summary>
    /// <param name="_startPosition">Initial spawn position.</param>
    /// <param name="_startNodePos">The initial global target node position.</param>
    public void Initialize(Vector3 _startPosition, float _startFScore, float _startObstacleDist, Vector3 _startNodePos)
    {
        // Current State initialization
        position = _startPosition;
        velocity = Vector3.zero;

        // pBest initialization: Setting initial conditions so the first calculated values always update pBest
        pBestPos = _startPosition;

        // The current fitness score should be initialized to the lowest possible value 
        // to ensure the first calculation immediately establishes the first pBest.
        pbestFScore = float.MinValue;

        // The distance to an obstacle should be initialized high (safe) to ensure the 
        // first movement that gets closer to an obstacle correctly tracks the closest point.
        pbestObjDist = float.MaxValue;

        // Node targeting initialization
        pbestNodeDist = Vector3.Distance(_startNodePos, _startPosition);
        pbestNodePos = _startNodePos;
    }
}