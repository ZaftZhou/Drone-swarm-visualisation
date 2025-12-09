using UnityEngine;

namespace Detection
{
    public interface IDronePathSampler
    {
        public void InitializePaths(int droneCount, Vector3 droneStartingLocation);
        public Vector3 SamplePositionAt(float t, int droneIndex);
        public float[] GetTotalDistances();
        public int GetDroneCount();
    }
}