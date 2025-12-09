using UnityEngine;

namespace Detection
{
    public struct SimulationSettingData
    {
        public string SimulationId;
        public string DroneFlightPattern;
        public Vector3 DroneStart;
        public float MaxDetectionDistance;
        public int DroneCount;
        public int DetectionRayCount;
        public int DetectionSimulationSteps;
        public float DetectionAngle;
        public float TerrainWidth;
        public float TerrainDepth;
        public int TreeCount;
        public int TargetCount;
        public Vector3 TargetSize;
    }
}
