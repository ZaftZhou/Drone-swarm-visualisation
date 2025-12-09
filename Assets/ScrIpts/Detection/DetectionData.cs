using UnityEngine;

namespace Detection
{
    public struct DetectionData
    {
        public DetectionEvent[] Events;
        public Vector3 Position;
        public Vector3 SummedDetectionVector;
        public int EventCount;
        public int TimesSeen;
    }
}
