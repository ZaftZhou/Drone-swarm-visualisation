using UnityEngine;
using UnityEngine.UIElements;

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

    [Header("Best Values - Global Best")]
    public Vector3 gBestPos;
    public float gBestFScore;

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

        gBestPos = _droneDetails[0].pBestPos;
        gBestFScore = _droneDetails[0].pbestFScore;

        //MainLoop();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && loopCount < maxIter) 
        {
            MainLoop();
            loopCount++;
            Debug.Log($"PSO Iteration: {loopCount}/{maxIter}. gBest Score: {gBestPos}");
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
            if (droneDetails == null)
            { Debug.LogError("Missing DroneDetails component!"); return;}
            _droneDetails[i] = droneDetails;

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
        //Vector3 possibleGlobalBestPosition;

        //for(int iter = 0; iter < maxIter; iter++)
        //{

        // DroneBest -Loop
        for (int i = 0; i < droneAmount; i++)
        {
            DroneDetails dDetails = _droneDetails[i];
            Transform dTransform = _droneTransforms[i];

            float r1 = Random.value;
            float r2 = Random.value;
            
            Vector3 cognitiveTerm = cognitiveWeight * r1 * (dDetails.pBestPos - dDetails.position);
            Vector3 socialTerm = socialWeight * r2 * (gBestPos - dDetails.position);

            dDetails.velocity = (inertiaWeight * dDetails.velocity) + cognitiveTerm + socialTerm;


            dTransform.position += dDetails.velocity;
            dDetails.position = dTransform.position;


            CheckLimits(dTransform);


            CalculateAndSetPBest(dDetails, dTransform);
        }

        UpdateGBest();
    }

    void CalculateAndSetPBest(DroneDetails dDetails, Transform dTransform)
    {
        float obstacleDist = float.MaxValue;
        float nodeDist;

        foreach (var obstacle in obstacleList)
        {
            float currentDist = Vector3.Distance(obstacle.transform.position, dTransform.position);

            if (currentDist < obstacleDist)
            {
                obstacleDist = currentDist;
            }
        }
        dDetails.pbestObjDist = obstacleDist;


        Vector3 currentPbestNodePos = Vector3.zero;
        float currentPbestNodeDist = float.MinValue;

        foreach (var node in nodeGrid.nodes)
        {
            nodeDist = Vector3.Distance(node.worldPosition, dTransform.position);

            if (nodeDist > currentPbestNodeDist)
            {
                currentPbestNodeDist = nodeDist;
                currentPbestNodePos = node.worldPosition;
            }
        }
        dDetails.pbestNodeDist = currentPbestNodeDist;
        dDetails.pbestNodePos = currentPbestNodePos;


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
                gBestPos = dDetails.pBestPos;
            }
        }
    }
    //}
    

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