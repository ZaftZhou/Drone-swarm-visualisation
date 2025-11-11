using System.Collections;
using UnityEngine;
using System.IO;
using System;
using Random = UnityEngine.Random;
using UnityEngine.SceneManagement;

public class DetectionTester : MonoBehaviour
{
    public struct DetectionEvent
    {
        public Vector3 DetectionVector;
        public float Time;
    }

    public struct VertexDetectionData
    {
        public DetectionEvent[] RecordedEvents;
        public Vector3 Position;
        public Vector3 SummedDetectionVector;
        public int EventCount;
        public int TimesSeen;

        public VertexDetectionData(int size = 10)
        {
            RecordedEvents = new DetectionEvent[size];
            SummedDetectionVector = new Vector3();
            Position = new Vector3();
            EventCount = 0;
            TimesSeen = 0;
        }
    }

    public struct SimplePathSampler
    {
        public Vector3[] PathPositions;

        public readonly Vector3 SampleAt(float t)
        {
            var atLeastIndex = Mathf.FloorToInt((PathPositions.Length - 1) * t);
            var progressToNext = (PathPositions.Length - 1) * t % 1;
            var nextIndex = progressToNext < 0.001 ? atLeastIndex : atLeastIndex + 1;
            return Vector3.Lerp(PathPositions[atLeastIndex], PathPositions[nextIndex], progressToNext);
        }
    }

    public Terrain Terrain;
    public float DetectionAngle;
    public MeshFilter DetectionMeshFilter;
    public float MaxDetectionDistance = 500;
    public LayerMask DetectionLayer;
    public RenderTexture DetectionTexture;
    public int DetectionThreshold = 1;
    public int DetectionRayCount = 500;
    public int DetectionSimulationSteps = 1000;
    [Header ("Increases memory use A LOT, adjust carefully")]
    public int RecordedEventCount = 10;
    private VertexDetectionData[] _vertexDetectionData;
    private float _detectionCircleRelativeHeight;

    private void Awake()
    {
        var mesh = DetectionMeshFilter.mesh;
        var vertices = mesh.vertices;
        var terrainData = Terrain.terrainData;
        _detectionCircleRelativeHeight = -0.5f / Mathf.Tan(DetectionAngle * Mathf.Deg2Rad); // Trig: Adjacent = Opposite / Tan(Theta)

        for (int i = 0; i < vertices.Length; i++)
        {
            float height = terrainData.GetInterpolatedHeight((vertices[i].x + DetectionMeshFilter.transform.position.x) / terrainData.size.x,
                (vertices[i].z + DetectionMeshFilter.transform.position.z) / terrainData.size.z);
            vertices[i] = new Vector3(vertices[i].x, height, vertices[i].z);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        DetectionMeshFilter.GetComponent<MeshCollider>().sharedMesh = mesh;
        _vertexDetectionData = new VertexDetectionData[vertices.Length];
        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var data = _vertexDetectionData[i];
            data.RecordedEvents = new DetectionEvent[RecordedEventCount];
            _vertexDetectionData [i] = data;
        }
    }

    private void Start()
    {
        StartDetectionTest();
    }

    public void StartDetectionTest()
    {
        StartCoroutine(DetectingCoroutine(DetectionSimulationSteps,
            new SimplePathSampler
            {
                PathPositions = new Vector3[] { new(500, 350, 0), new(1000, 350, 500), new(500, 350, 1000), new(0, 350, 0) }
            }));
    }

    private IEnumerator DetectingCoroutine(int steps, SimplePathSampler pathSampler)
    {
        var vertices = DetectionMeshFilter.mesh.vertices;
        var triangles = DetectionMeshFilter.mesh.triangles;

        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var detectionVertex = _vertexDetectionData[i];
            detectionVertex.Position = DetectionMeshFilter.transform.TransformPoint(vertices[i]);
            _vertexDetectionData[i] = detectionVertex;
        }
        Debug.Log("finished setting positions");
        yield return null;

        for (int step = 0; step < steps; step++)
        {
            float progress = step / (float)steps;
            Vector3 location = pathSampler.SampleAt(progress);
            for (int i = 0; i < DetectionRayCount; i++)
            {
                var ease1 = SmoothedRandom01() * (Random.value < 0.5f ? -0.5f : 0.5f);
                var ease2 = SmoothedRandom01() * (Random.value < 0.5f ? -0.5f : 0.5f);
                if (Physics.Raycast(new Ray(location, new Vector3(ease1, _detectionCircleRelativeHeight, ease2)),
                    out RaycastHit hit, MaxDetectionDistance, DetectionLayer) && hit.collider.TryGetComponent<DetectionPlane>(out _))
                {
                    for (int v = 0; v < 3; v++) //for each vertex of hit triangle
                    {
                        var detectionVertex = _vertexDetectionData[triangles[hit.triangleIndex * 3 + v]];
                        if (detectionVertex.EventCount < detectionVertex.RecordedEvents.Length)
                        {
                            detectionVertex.RecordedEvents[detectionVertex.EventCount].DetectionVector = (location - detectionVertex.Position);
                            detectionVertex.RecordedEvents[detectionVertex.EventCount].Time = progress;
                            detectionVertex.EventCount++;
                        }
                        detectionVertex.SummedDetectionVector += (location - detectionVertex.Position).normalized
                           * Mathf.Lerp(1f, 0.1f, (location - detectionVertex.Position).magnitude / MaxDetectionDistance);
                        detectionVertex.TimesSeen++;
                        _vertexDetectionData[triangles[hit.triangleIndex * 3 + v]] = detectionVertex;
                    }
                }
            }
        }
        Debug.Log("finished detecting");
        yield return null;

        StartCoroutine(VisualizeDetectionData());
    }

    private IEnumerator VisualizeDetectionData()
    {
        float lastValidTime = 0.5f;
        int side = (int)Mathf.Sqrt(_vertexDetectionData.Length);
        Debug.Log($"Side length: {side}");
        var bake = new Texture2D(side, side);
        var pixels = new Color32[side * side];
        int mostSeen = 0;
        int mostValidEvents = 0;
        float largestMagnitude = 0;
        float smallestX = float.MaxValue;
        float largestX = 0;
        float smallestZ = float.MaxValue;
        float largestZ = 0;
        foreach (var d in _vertexDetectionData)
        {
            for (int i = d.EventCount - 1; i >= 0; i--)
            {
                if (d.RecordedEvents[i].Time <= lastValidTime && i > mostValidEvents)
                {
                    mostValidEvents = i + 1;
                    break;
                }
            }
            if (d.TimesSeen > mostSeen)
                mostSeen = d.TimesSeen;
            if (d.SummedDetectionVector.magnitude > largestMagnitude)
                largestMagnitude = d.SummedDetectionVector.magnitude;
            if (d.Position.x > largestX) largestX = d.Position.x;
            if (d.Position.x < smallestX) smallestX = d.Position.x;
            if (d.Position.z > largestZ) largestZ = d.Position.z;
            if (d.Position.z < smallestZ) smallestZ = d.Position.z;
        }
        //Debug.Log($"largestX {largestX} smallestX {smallestX} largestZ {largestZ} smallestZ {smallestZ}");

        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var e = _vertexDetectionData[i];
            var n = e.SummedDetectionVector.normalized;
            Color color = Color.red;
            if (e.TimesSeen >= DetectionThreshold)
            {
                var dimmingFactor = ((e.TimesSeen / (float)mostSeen) + (e.SummedDetectionVector.magnitude / largestMagnitude)) / 2;
                color = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
            }
            pixels[(int)Mathf.Clamp(e.Position.x, 0, 999) + ((int)Mathf.Clamp(e.Position.z, 0, 999)) * side] = color;
        }
        bake.SetPixels32(pixels);
        bake.Apply();
        var png = bake.EncodeToPNG();
        var path = Path.Combine(Application.persistentDataPath, $"DetectionAll{DateTime.Now.ToFileTime()}.png");
        Debug.Log($"baking data to {path}");
        File.WriteAllBytes(path, png);
        yield return null;

        
        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var data = _vertexDetectionData[i];
            Vector3 summedPartialDetectionVector = new ();
            int validEvents = data.EventCount;
            for (int eventIndex = 0; eventIndex < data.EventCount; eventIndex++)
            {
                var recordedEvent = data.RecordedEvents[eventIndex];
                if (recordedEvent.Time > lastValidTime) 
                {
                    validEvents = eventIndex;
                    break;
                }
                summedPartialDetectionVector += data.RecordedEvents[eventIndex].DetectionVector;
            }
            var n = summedPartialDetectionVector.normalized;
            Color color = Color.red;
            if (validEvents >= DetectionThreshold)
            {
                var dimmingFactor = ((validEvents / (float)mostValidEvents) + (data.SummedDetectionVector.magnitude / largestMagnitude)) / 2;
                color = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
            }
            pixels[(int)Mathf.Clamp(data.Position.x, 0, 999) + ((int)Mathf.Clamp(data.Position.z, 0, 999)) * side] = color;
        }
        bake.SetPixels32(pixels);
        bake.Apply();
        png = bake.EncodeToPNG();
        path = Path.Combine(Application.persistentDataPath, $"DetectionStoppedAt05{DateTime.Now.ToFileTime()}.png");
        Debug.Log($"baking data to {path}");
        File.WriteAllBytes(path, png);
        Destroy( bake );
    }

    private static float SmoothedRandom01()
    {
        return (Utils.Easing.EaseInSine(Mathf.Abs(Random.insideUnitCircle.x))
            + Utils.Easing.EaseInSine(Mathf.Abs(Random.insideUnitCircle.x))) * 0.5f;
    }

    private void OnApplicationQuit()
    {
        DetectionMeshFilter.GetComponent<MeshCollider>().sharedMesh = null;
        Destroy(DetectionMeshFilter);
        _vertexDetectionData = null;
        Resources.UnloadUnusedAssets();
    }

}
