using System.Linq;
using UnityEngine;

public class DroneVisualiser : MonoBehaviour
{
    public GameObject DronePrefab;
    private Transform[] _drones;

    public void Initialize(int droneCount)
    {
        _drones = new Transform[droneCount];
        for (int i = 0; i < droneCount; i++)
        {
            _drones[i] = Instantiate(DronePrefab).transform;
        }
    }

    public void Visualise(Vector3[] dronePositions, float detectionAngle, float[] distances)
    {
        var scale = new Vector3(Mathf.Tan(detectionAngle * 0.5f * Mathf.Deg2Rad), 1, Mathf.Tan(detectionAngle * 0.5f * Mathf.Deg2Rad));
        for (int i = 0; i < _drones.Length; i++)
        {
            _drones[i].position = dronePositions[i];
            _drones[i].GetChild(0).localScale = scale * distances[i];
        }
    }

    public void Visualise(Vector3 dronePosition, float detectionAngle, float[] distances)
    {
        var scale = new Vector3(Mathf.Tan(detectionAngle * 0.5f * Mathf.Deg2Rad), 1, Mathf.Tan(detectionAngle * 0.5f * Mathf.Deg2Rad));
        for (int i = 0; i < _drones.Length; i++)
        {
            _drones[i].position = dronePosition;
            _drones[i].GetChild(0).localScale = scale * distances[i];
        }
    }

    public Vector3[] GetDronePositions()
    {
        return _drones.Select((drone) => drone.position).ToArray();
    }

    public Vector3 GetDronePosition(int index)
    {
        return _drones[index].position;
    }

    public Transform GetDrone(int index)
    {
        return _drones[index];
    }
}
