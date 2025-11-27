using UnityEngine;

public class DroneDetails : MonoBehaviour 
{
    public Vector3 position;
    public Vector3 velocity;
    
    public Vector3 pBestPos;
    public float pbestFScore;
    public float pbestObjDist;
    public float pbestNodeDist;
    public Vector3 pbestNodePos;

    public int droneId;

    public void Initialize(Vector3 _startPosition, float _startFScore, float _startObstacleDist, Vector3 _startNodePos)
    {
        position = _startPosition;
        velocity = Vector3.zero;

        pBestPos = _startPosition;
        pbestFScore = float.MinValue;
        pbestObjDist = float.MaxValue;

        pbestNodeDist = Vector3.Distance(_startNodePos, _startPosition);
        pbestNodePos = _startNodePos;
    }
}