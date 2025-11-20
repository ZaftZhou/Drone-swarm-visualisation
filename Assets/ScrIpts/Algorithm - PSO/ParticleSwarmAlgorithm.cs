using UnityEngine;

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

    private GameObject[] dronePrefabs;
    public Vector3 gBest;

    private int loopCount;

    private void Start()
    {
        dronePrefabs = new GameObject[droneAmount];
        SpawnDrones();
        //MainLoop();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && loopCount < maxIter) 
        {
            MainLoop();
            loopCount++;
        }
    }

    void SpawnDrones()
    {
        DroneDetails dDet;

        for (int i = 0; i < droneAmount; i++)
        {
            var spawnedDrone = Instantiate(dronePrefab, SpawnRange(), Quaternion.identity);
            spawnedDrone.name = $"Drone[{i}]";
            dronePrefabs[i] = spawnedDrone;

            dDet = dronePrefabs[i].GetComponent<DroneDetails>();
            dDet.startLocation = dronePrefabs[i].transform.position;
        }
    }

    Vector3 SpawnRange()
    {
        Vector3 limitedSpawn = new (Random.Range(-forestArea, forestArea), droneFlightHeight, Random.Range(-forestArea, forestArea));
        return limitedSpawn;
    }


    void MainLoop()
    {
        DroneDetails dDet;
        Transform dPos;

        //for(int iter = 0; iter < maxIter; iter++)
        //{
        for(int i = 0; i < droneAmount; i++)
        {
            dDet = dronePrefabs[i].GetComponent<DroneDetails>();
            dPos = dronePrefabs[i].transform;

            dDet.position = dPos.position;
            dDet.velocity += inertiaWeight * dDet.velocity +
                             cognitiveWeight * (dDet.pBest -  dDet.position) +
                             socialWeight * (gBest - dDet.position);
            dPos.position += dDet.velocity;

            CheckLimits(dPos);

            if (dDet.pBest.magnitude < Vector3.Distance(dDet.position, dDet.startLocation)) // Needs fixin, startLocation is zero vectornow, needs to be the startLocation of drone
                                                                                            // the location of farthest unsearched gridnode. I.E. Fitness score
            {
                dDet.pBest = dDet.position;
            }

            if (dDet.pBest.magnitude < gBest.magnitude)
            {
                gBest = dDet.position;
            }

            //if (Vector3.Distance(dDet.pBest, dDet.startLocation) < Vector3.Distance(dDet.position, dDet.startLocation)) { }
        }
        //}
    }

    void CheckLimits(Transform dPos)
    {
        // TO-DO: Limit drone area
        if (dPos.position.x < -forestArea)
        {
            dPos.position = Vector3.right * 2f;
        }
        if (dPos.position.z < -forestArea)
        {
            dPos.position = Vector3.back * 2f;
        }
        if (dPos.position.x > forestArea)
        {
            dPos.position = Vector3.left * 2f;
        }
        if (dPos.position.z > forestArea)
        {
            dPos.position = Vector3.forward * 2f;
        }

        if (dPos.position.y != 2)
        {
            dPos.position = new Vector3(dPos.position.x, 2, dPos.position.z);
        }
    }

    // !!! IMPORTANT NOTES !!!
    // Need to make a funtional inertiaWeight decrease with each iter,
    // and a way to get a fitness score. I was planning on using the distance
    // to nearest obstacle and distance to furthest unsearched node to get it
    // !!! DON'T IGNORE !!!
}