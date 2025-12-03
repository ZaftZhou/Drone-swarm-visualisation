using UnityEngine;
using System.Collections.Generic;

public class ParticleSwarmAlgorithm : MonoBehaviour
{
    [Header("Algorithm Parameters")]
    public int maxIter;
    public float inertiaWeight;
    public float cognitiveWeight;
    public float socialWeight;

    [Header("Search Area")]
    public float forestArea;

    [Header("Drone Setup")]
    public int droneAmount;
    public float droneFlightHeight;
    public GameObject dronePrefab;

    [Header("Fitness Score Setup")]
    public NodeGrid nodeGrid;
    public GameObject[] obstacleList;
    [Tooltip("The distance threshold at which a node is considered 'searched' by a drone.")]
    public float searchThreshold = 3f; // Added threshold for coverage check

    [Header("Coverage Target - Global Best")]
    [Tooltip("The unsearched node the entire swarm is currently targeting.")]
    public Vector3 gBestTarget = Vector3.zero;
    [Tooltip("The fitness score of the best PATH found so far (high clearance/low target distance).")]
    public float gBestFScore = float.MinValue;

    // --- Internal Variables ---
    private DroneDetails[] _droneDetails;
    private Transform[] _droneTransforms;
    private int loopCount;

    private void Start()
    {
        _droneDetails = new DroneDetails[droneAmount];
        _droneTransforms = new Transform[droneAmount];

        if (obstacleList == null || obstacleList.Length == 0)
        {
            Debug.LogError("Obstacle list is empty! Cannot calculate path risk!");
        }

        SpawnDrones();

        gBestTarget = _droneDetails[0].pBestPos;
        gBestFScore = _droneDetails[0].pbestFScore;

        //MainLoop();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) // && loopCount < maxIter 
        {
            MainLoop();
            //loopCount++;
        }
    }

    void SpawnDrones()
    {
        Vector3 startNodePos = (nodeGrid != null && nodeGrid.nodes != null && 
                                nodeGrid.nodes.GetLength(0) > 0 && nodeGrid.nodes.GetLength(1) > 0) ?
                                nodeGrid.nodes[0,0].worldPosition : Vector3.zero;

        for (int i = 0; i < droneAmount; i++)
        {
            Vector3 spawnPosition = SpawnRange();

            var spawnedDrone = Instantiate(dronePrefab, spawnPosition, Quaternion.identity);
            spawnedDrone.name = $"Drone[{i}]";
            _droneTransforms[i] = spawnedDrone.transform;

            DroneDetails droneDetails = spawnedDrone.GetComponent<DroneDetails>();
            DroneCollisionCheck droneCollisionCheck = spawnedDrone.GetComponent<DroneCollisionCheck>();

            if (droneDetails == null)
            { Debug.LogError("Missing DroneDetails component!"); return;}
            _droneDetails[i] = droneDetails;
            droneDetails.droneCollisionCheck = droneCollisionCheck;

            droneDetails.droneId = i;
            droneDetails.Initialize(spawnPosition, 0f, 0f, startNodePos);
        }
    }

    Vector3 SpawnRange()
    {
        return new Vector3(
            Random.Range(-forestArea, forestArea), 
            droneFlightHeight, 
            Random.Range(-forestArea, forestArea)
        );
    }


    void MainLoop()
    {
        for(int iter = 0; iter < maxIter; iter++)
        {

            // DroneBest -Loop
            NodeSquare currentTargetNode = nodeGrid.GridFromWorldPoint(gBestTarget);
            if (currentTargetNode != null && currentTargetNode.searched)
            {
                FindNextUnsearchedTarget();
            }

            for (int i = 0; i < droneAmount; i++)
            {
                DroneDetails dDetails = _droneDetails[i];
                Transform dTransform = _droneTransforms[i];

                float r1 = Random.value;
                float r2 = Random.value;
                
                Vector3 cognitiveTerm = cognitiveWeight * r1 * (dDetails.pBestPos - dDetails.position);
                Vector3 socialTerm = socialWeight * r2 * (gBestTarget - dDetails.position);

                Vector3 repulsionTerm = dDetails.droneCollisionCheck.GetRepulsionVector(dDetails.position);

                dDetails.velocity = (inertiaWeight * dDetails.velocity) + cognitiveTerm + socialTerm + repulsionTerm;


                dTransform.position += dDetails.velocity;
                dDetails.position = dTransform.position;


                CheckLimits(dTransform);


                CalculateAndSetPBest(dDetails, dTransform);

                // COVERAGE LOGIC: Mark the node the drone is currently flying over as searched.
                MarkNodeAsSearched(dDetails.position);
            }

            UpdateGBest();

            Debug.Log($"PSO Iteration: {iter}/{maxIter}. gBest Score: {gBestTarget}");
        }
    }

    /// <summary>
    /// Searches the grid for the nearest unsearched node and sets it as the new global target.
    /// </summary>
    void FindNextUnsearchedTarget()
    {
        if (nodeGrid == null || nodeGrid.nodes == null) return;

        Vector3 swarmCenter = Vector3.zero;
        foreach (var drone in _droneDetails)
        {
            swarmCenter += drone.position;
        }
        swarmCenter /= _droneDetails.Length;

        float longestDistance = float.MinValue;
        //float shortestDistance = float.MaxValue;
        Vector3 currentPotentialTarget = gBestTarget;

        int xMax = nodeGrid.nodes.GetLength(0);
        int yMax = nodeGrid.nodes.GetLength(1);
        bool foundNewTarget = false;

        for (int x = 0; x < xMax; x++)
        {
            for (int y = 0; y < yMax; y++)
            {
                var node = nodeGrid.nodes[x, y];

                if (node.searchable && !node.searched)
                {
                    float distance = Vector3.Distance(swarmCenter, node.worldPosition);

                    if (distance > longestDistance)
                    {
                        longestDistance = distance;
                        currentPotentialTarget = node.worldPosition;
                        foundNewTarget = true;
                    }
                }
            }
        }

        if (foundNewTarget)
        {
            gBestTarget = currentPotentialTarget;
        }
        else
        {
            if (nodeGrid.GridFromWorldPoint(gBestTarget) == null || nodeGrid.GridFromWorldPoint(gBestTarget).searched)
            {
                Debug.Log("Search complete: All aviable nodes have been searched.");
                // sim stop logic
            }
        }
    }

    /// <summary>
    /// Marks the grid node corresponding to the world position as searched.
    /// </summary>
    void MarkNodeAsSearched(Vector3 worldPosition)
    {
        if (nodeGrid == null) return;

        NodeSquare nodeToMark = nodeGrid.GridFromWorldPoint(worldPosition);

        if (nodeToMark != null && nodeToMark.searchable)
        {
            nodeToMark.searched = true;
        }
    }

    void CalculateAndSetPBest(DroneDetails dDetails, Transform dTransform)
    {
        float obstacleDist = float.MaxValue;
        //float nodeDist;

        foreach (var obstacle in obstacleList)
        {
            float currentDist = Vector3.Distance(obstacle.transform.position, dTransform.position);

            if (currentDist < obstacleDist)
            {
                obstacleDist = currentDist;
            }
        }
        dDetails.pbestObjDist = obstacleDist;


        //Vector3 currentPbestNodePos = Vector3.zero;
        float currentPbestNodeDist = Vector3.Distance(gBestTarget, dTransform.position); //<-- Changed from float.MinValue

        //foreach (var node in nodeGrid.nodes)
        //{
        //    nodeDist = Vector3.Distance(node.worldPosition, dTransform.position);
        //
        //    if (nodeDist > currentPbestNodeDist)
        //    {
        //        currentPbestNodeDist = nodeDist;
        //        currentPbestNodePos = node.worldPosition;
        //    }
        //}

        dDetails.pbestNodeDist = currentPbestNodeDist;
        dDetails.pbestNodePos = gBestTarget; //<-- Changed from currentPbestNodePos


        float fScore = dDetails.pbestObjDist - dDetails.pbestNodeDist;

        if (dDetails.pbestFScore < fScore)
        {
            dDetails.pbestFScore = fScore;
            dDetails.pBestPos = dDetails.position;
        }
    }

    void UpdateGBest()
    {
        // GlobalBest -Loop
        for (int i = 0; i < droneAmount; i++)
        {
            DroneDetails dDetails = _droneDetails[i];

            if (dDetails.pbestFScore > gBestFScore)
            {
                gBestFScore = dDetails.pbestFScore;
                //gBestTarget = dDetails.pBestPos; <--- Note: gBestTarget is NOT updated here, as it's set by FindNextUnsearchedTarget
            }
        }
    }
    
    void CheckLimits(Transform dTransform)
    {
        dTransform.position = new Vector3 (
            Mathf.Clamp(dTransform.position.x, -forestArea, forestArea),
            dTransform.position.y,
            Mathf.Clamp(dTransform.position.z, -forestArea, forestArea)
        );

        if (dTransform.position.y != droneFlightHeight)
        {
            dTransform.position = new Vector3(dTransform.position.x, droneFlightHeight, dTransform.position.z);
        }
    }


    // !!! IMPORTANT NOTES !!!
    // Need to make a funtional inertiaWeight decrease with each iter,
    // and a way to get a fitness score. I was planning on using the distance
    // to nearest obstacle and distance to furthest unsearched node to get it
    // !!! DON'T IGNORE !!!

    // For the fitness score, I need to get a list of all the obstacles
    // and/or their positions in the scene, also need the position of each node in the grid
    // and drone.
    // Then using those positions I just get the distances to them and make the drones
    // move towards the furthest unsearched node and avoid all the obstacles and other drones
    // -----ALTERNATIVE-----
    // I use the colliders to prevent collisions and simulate the ideal
    // collision avoidance, but this most likely can't be translated
    // to real life methods. Can't have invisible colliders IRL
}