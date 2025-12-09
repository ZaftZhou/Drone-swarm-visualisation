using System.Collections.Generic;
using UnityEngine;

namespace Detection
{
    public class SimplePathSampler : MonoBehaviour, IDronePathSampler
    {
        [SerializeField]
        private Terrain _terrain;
        [SerializeField]
        private float _flightHeight;

        private List<Vector3>[] _dronePaths;
        private float[] _totalPathLengths;
        private int _droneCount;

        public int GetDroneCount()
        {
            return _droneCount;
        }

        public float[] GetTotalDistances()
        {
            return _totalPathLengths;
        }

        public void InitializePaths(int droneCount, Vector3 droneStartingLocation)
        {
            _dronePaths = new List<Vector3>[droneCount];
            _totalPathLengths = new float[droneCount];
            _droneCount = droneCount;
            for (int i = 0; i < _droneCount; i++)
            {
                _dronePaths[i].Add(droneStartingLocation);
                _dronePaths[i].Add(_terrain.terrainData.bounds.max);
            }
        }

        public Vector3 SamplePositionAt(float t, int droneIndex)
        {
            if (droneIndex < 0 || droneIndex > _droneCount)
            {
                Debug.LogError("Drone position sampling drone index out of bounds");
                return Vector3.zero;
            }
            t = Mathf.Clamp01(t);
            List<Vector3> path = _dronePaths[droneIndex];
            if (path.Count == 1 || t == 0f)
            {
                return path[0];
            }
            if (t == 1f)
            {
                return path[^1];
            }
            var position = Vector3.Lerp(path[0], path[^1], t);
            return new Vector3(position.x, _terrain.SampleHeight(position) + _flightHeight, position.z);
        }
    }
}
