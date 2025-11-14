using UnityEngine;

public class DroneDetails : MonoBehaviour 
{
    public Vector3 startLocation;
    public Vector3 pBest;
    public Vector3 position;
    public Vector3 velocity;

    public DroneDetails(Vector3 _startLocation, Vector3 _pBest, Vector3 _position, Vector3 _velocity)
    {
        startLocation = _startLocation;
        pBest = _pBest;
        position = _position;
        velocity = _velocity;
    }
}