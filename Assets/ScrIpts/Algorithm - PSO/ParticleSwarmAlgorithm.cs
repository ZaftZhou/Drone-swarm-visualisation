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
    private void Start()
    {
        dronePrefabs = new GameObject[droneAmount];
        SpawnDrones();
        MainLoop();
    }

    void SpawnDrones()
    {
        for (int i = 0; i < droneAmount; i++)
        {
            var spawnedDrone = Instantiate(dronePrefab, SpawnRange(), Quaternion.identity);
            spawnedDrone.name = $"Drone[{i}]";
            dronePrefabs[i] = spawnedDrone;
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

        for(int iter = 0; iter < maxIter; iter++)
        {
            for(int i = 0; i < droneAmount; i++)
            {
                dDet = dronePrefabs[i].GetComponent<DroneDetails>();
                dDet.position = dronePrefabs[i].transform.position;
                dDet.velocity += inertiaWeight * dDet.velocity +
                                 cognitiveWeight * (dDet.pBest -  dDet.position) +
                                 socialWeight * (gBest - dDet.position);
                dronePrefabs[i].transform.position += dDet.velocity;

                // TO-DO: Limit drone area
                // if (dronePrefabs[i].transform.position.x < -forestArea || dronePrefabs[i].transform.position.z < -forestArea)
                // {
                //     
                // }
                // else if (dronePrefabs[i].transform.position.x > forestArea || dronePrefabs[i].transform.position.z > forestArea)
                // {
                // 
                // }

                if (dDet.pBest.magnitude < Vector3.Distance(dDet.position, dDet.startLocation)) // Needs fixin, startLocation is zero vectornow, need to be
                                                                                                // the location of farthest unsearched gridnode. I.E. Fitness score
                {
                    dDet.pBest = dDet.position;
                }

                if (dDet.pBest.magnitude < gBest.magnitude)
                {
                    gBest = dDet.position;
                }
            }
        }
    }

    // !!! IMPORTANT NOTES !!!
    // Need to make a funtional inertiaWeight decrease with each iter,
    // and a way to get a fitness score was planning on using the distance
    // to nearest obstacle and distance to furthest unsearched node to get it
    // !!! DON'T IGNORE !!!
}