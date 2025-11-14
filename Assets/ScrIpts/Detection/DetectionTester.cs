using System.Collections;
using UnityEngine;
using System.IO;
using System;
using Random = UnityEngine.Random;
using TMPro;
using UnityEngine.UI;
using Unity.Jobs;
using Unity.Collections;
using System.Threading;
using static UnityEditor.FilePathAttribute;
using UnityEditor;

public class DetectionTester : MonoBehaviour
{
    public struct DetectionEvent
    {
        public Vector3 DetectionVector;
        public float Time;
        public int DroneIndex;
    }

    public struct VertexDetectionData
    {
        public DetectionEvent[] RecordedEvents;
        public Vector3 Position;
        public Vector3 SummedDetectionVector;
        public int EventCount;
        public int TimesSeen;
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
    public float DetectionAngle = 45f;
    public MeshFilter DetectionMeshFilter;
    public float MaxDetectionDistance = 500;
    public LayerMask DetectionLayer;
    public int DroneCount = 10;
    public Color[] DroneColors = new Color[10];
    public int DetectionThreshold = 1;
    public int DetectionRayCount = 500;
    public int DetectionSimulationSteps = 1000;
    public Material DetectionVisMaterial;
    [Header("Increases memory use A LOT, adjust carefully")]
    public int RecordedEventCount = 10;
    public GameObject DronePathSampler;
    public GameObject DetectionVisLayer;
    public GameObject DroneVisLayer;
    public GameObject ProbabilityVisLayer;
    public TMP_InputField DetectionAngleInput;
    public TMP_InputField MaxDetectionDistanceInput;
    public TMP_InputField DetectionThresholdInput;
    public TMP_InputField DetectionRayCountInput;
    public TMP_InputField DetectionSimulationStepsInput;
    public Slider TimeSlider;
    private IDronePathSampler _dronePathSampler;
    private VertexDetectionData[] _vertexDetectionData;
    private float _detectionCircleRelativeHeight;
    private Texture2D _detectionColor;
    private Texture2D _droneColor;
    private Texture2D _probabilityColor;
    private Texture2D _undetectedColor;
    private Color32[] _pixelBuffer;
    private Vector3[] _detectionVerts;
    private int[] _detectionTris;
    private int _sideLength;

    private void Awake()
    {
        var mesh = DetectionMeshFilter.mesh;
        _detectionVerts = mesh.vertices;
        _detectionTris = mesh.triangles;
        var terrainData = Terrain.terrainData;
        _vertexDetectionData = new VertexDetectionData[_detectionVerts.Length];
        _sideLength = (int)Mathf.Sqrt(_detectionVerts.Length);
        _detectionColor = new Texture2D(_sideLength, _sideLength);
        _pixelBuffer = new Color32[_detectionVerts.Length];
        for (int i = 0; i < _detectionVerts.Length; i++)
        {
            float height = terrainData.GetInterpolatedHeight((_detectionVerts[i].x + DetectionMeshFilter.transform.position.x) / terrainData.size.x,
                (_detectionVerts[i].z + DetectionMeshFilter.transform.position.z) / terrainData.size.z);
            _detectionVerts[i] = new Vector3(_detectionVerts[i].x, height, _detectionVerts[i].z);
            var data = _vertexDetectionData[i];
            data.RecordedEvents = new DetectionEvent[RecordedEventCount];
            data.Position = DetectionMeshFilter.transform.TransformPoint(_detectionVerts[i]);
            _vertexDetectionData[i] = data;
        }
        mesh.vertices = _detectionVerts;
        mesh.RecalculateNormals();
        DetectionMeshFilter.GetComponent<MeshCollider>().sharedMesh = mesh;


        if (!DronePathSampler.TryGetComponent(out _dronePathSampler))
        {
            throw new MissingComponentException($"No drone path sampler found in {DronePathSampler.name}!");
        }
        DetectionVisLayer.GetComponent<MeshFilter>().sharedMesh = mesh;
        DroneVisLayer.GetComponent<MeshFilter>().sharedMesh = mesh;
        ProbabilityVisLayer.GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    private void Start()
    {
        //_dronePathSampler.InitializePaths(DroneCount, new Vector3(0, Terrain.SampleHeight(Vector3.zero), 0));
        //StartDetectionTest();
    }

    public void StartDetectionTest()
    {
        DetectionAngle = float.Parse(DetectionAngleInput.text);
        _detectionCircleRelativeHeight = -0.5f / Mathf.Tan(DetectionAngle * Mathf.Deg2Rad); // Trig: Adjacent = Opposite / Tan(Theta)
        MaxDetectionDistance = float.Parse(MaxDetectionDistanceInput.text);
        DetectionThreshold = int.Parse(DetectionThresholdInput.text);
        DetectionRayCount = int.Parse(DetectionRayCountInput.text);
        DetectionSimulationSteps = int.Parse(DetectionSimulationStepsInput.text);
        TimeSlider.SetValueWithoutNotify(1);

        _dronePathSampler.InitializePaths(DroneCount, new Vector3(0, Terrain.SampleHeight(Vector3.zero), 0));
        //StartCoroutine(DetectingCoroutine(DetectionSimulationSteps, _dronePathSampler));

        _ = StartAsyncDetection();
    }

    private async Awaitable StartAsyncDetection()
    {
        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var vertex = _vertexDetectionData[i];
            vertex.EventCount = 0;
            vertex.TimesSeen = 0;
            vertex.SummedDetectionVector = new Vector3();
            _vertexDetectionData[i] = vertex;
        }
        Debug.Log("Detecting");
        try
        {
            await DetectionAsync(DetectionSimulationSteps, _dronePathSampler);
            Debug.Log("Done detecting");
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
        Debug.Log("Visualising");
        VisualiseAtTime(1);
        Debug.Log("Done");
        //Debug.Log("Visualising!");
        //await CreateVisualizations(TimeSlider.value);
    }

    private async Awaitable DetectionAsync(int steps, IDronePathSampler pathSampler)
    {
        Vector3[,] locations = new Vector3[DroneCount, steps];
        Vector3[,,] randomDirections = new Vector3[DroneCount, steps, DetectionRayCount];
        for (int step = 0; step < steps; step++)
        {
            float progress = step / (float)steps;
            for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
            {
                locations[droneIndex, step] = pathSampler.SamplePositionAt(progress, droneIndex);
                for (int i = 0; i < DetectionRayCount; i++)
                {
                    randomDirections[droneIndex, step, i] = new Vector3(OffsetRandom(), _detectionCircleRelativeHeight, OffsetRandom());
                }
            }
        }
        var droneResults = new NativeArray<RaycastHit>[DroneCount];
        var droneCommands = new NativeArray<RaycastCommand>(DetectionRayCount, Allocator.Persistent);
        for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
        {
            await Awaitable.EndOfFrameAsync();

            droneResults[droneIndex] = new NativeArray<RaycastHit>(DetectionRayCount, Allocator.Persistent);
            for (int step = 0; step < steps; step++)
            {
                Vector3 location = locations[droneIndex, step];
                float progress = step / (float)steps;
                for (int j = 0; j < DetectionRayCount; j++)
                {
                    droneCommands[j] = 
                        new RaycastCommand(locations[droneIndex, step], randomDirections[droneIndex, step, j], QueryParameters.Default);
                }
                
                JobHandle handle = RaycastCommand.ScheduleBatch(droneCommands, droneResults[droneIndex], 1, 1);
                //await Awaitable.NextFrameAsync();
                handle.Complete();
                foreach (var hit in droneResults[droneIndex])
                {
                    if (hit.collider != null)
                    {
                        for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                        {
                            var detectionVertex = _vertexDetectionData[_detectionTris[hit.triangleIndex * 3 + vertexIndex]];
                            if (detectionVertex.EventCount < detectionVertex.RecordedEvents.Length)
                            {
                                detectionVertex.RecordedEvents[detectionVertex.EventCount].DetectionVector = (location - detectionVertex.Position);
                                detectionVertex.RecordedEvents[detectionVertex.EventCount].Time = progress;
                                detectionVertex.RecordedEvents[detectionVertex.EventCount].DroneIndex = droneIndex;
                                detectionVertex.EventCount++;
                            }
                            detectionVertex.SummedDetectionVector += (location - detectionVertex.Position).normalized
                               * Mathf.Lerp(1f, 0.1f, (location - detectionVertex.Position).magnitude / MaxDetectionDistance);
                            detectionVertex.TimesSeen++;
                            _vertexDetectionData[_detectionTris[hit.triangleIndex * 3 + vertexIndex]] = detectionVertex;
                        }
                    }
                }
            }
        }
        droneCommands.Dispose();
        foreach (var droneResult in droneResults)
        {
            droneResult.Dispose();
        }
    }

    //private IEnumerator BetterDetecting(int steps, IDronePathSampler pathSampler)
    //{
    //    Vector3[,] locations = new Vector3[DroneCount, steps];
    //    Vector3[,,] randomDirections = new Vector3[DroneCount, steps, DetectionRayCount];
    //    for (int step = 0; step < steps; step++)
    //    {
    //        float progress = step / (float)steps;
    //        for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
    //        {
    //            locations[droneIndex, step] = pathSampler.SamplePositionAt(progress, droneIndex);
    //            for (int i = 0; i < DetectionRayCount; i++)
    //            {
    //                randomDirections[droneIndex, step, i] = new Vector3(OffsetRandom(), _detectionCircleRelativeHeight, OffsetRandom());
    //            }
    //        }
    //    }
    //    for 
    //}
    private IEnumerator DetectingCoroutine(int steps, IDronePathSampler pathSampler)
    {
        for (int step = 0; step < steps; step++)
        {
            float progress = step / (float)steps;
            for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
            {
                Vector3 location = pathSampler.SamplePositionAt(progress, droneIndex);
                for (int i = 0; i < DetectionRayCount; i++)
                {
                    var ease1 = SmoothedRandom01() * (Random.value < 0.5f ? -0.5f : 0.5f);
                    var ease2 = SmoothedRandom01() * (Random.value < 0.5f ? -0.5f : 0.5f);
                    if (Physics.Raycast(new Ray(location, new Vector3(ease1, _detectionCircleRelativeHeight, ease2)),
                        out RaycastHit hit, MaxDetectionDistance, DetectionLayer) && hit.collider.TryGetComponent<DetectionPlane>(out _))
                    {
                        for (int vertexIndex = 0; vertexIndex < 3; vertexIndex++)
                        {
                            var detectionVertex = _vertexDetectionData[_detectionTris[hit.triangleIndex * 3 + vertexIndex]];
                            if (detectionVertex.EventCount < detectionVertex.RecordedEvents.Length)
                            {
                                detectionVertex.RecordedEvents[detectionVertex.EventCount].DetectionVector = (location - detectionVertex.Position);
                                detectionVertex.RecordedEvents[detectionVertex.EventCount].Time = progress;
                                detectionVertex.RecordedEvents[detectionVertex.EventCount].DroneIndex = droneIndex;
                                detectionVertex.EventCount++;
                            }
                            detectionVertex.SummedDetectionVector += (location - detectionVertex.Position).normalized
                               * Mathf.Lerp(1f, 0.1f, (location - detectionVertex.Position).magnitude / MaxDetectionDistance);
                            detectionVertex.TimesSeen++;
                            _vertexDetectionData[_detectionTris[hit.triangleIndex * 3 + vertexIndex]] = detectionVertex;
                        }
                    }
                }
            }
        }
        Debug.Log("finished detecting");
        yield return null;

        StartCoroutine(VisualizeDetectionData(1));
    }

    private async Awaitable CreateVisualizations(float t)
    {
        await Awaitable.BackgroundThreadAsync();
        var pixels = _pixelBuffer;
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
                if (d.RecordedEvents[i].Time <= t && i > mostValidEvents)
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

        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var data = _vertexDetectionData[i];
            Vector3 summedPartialDetectionVector = new();
            int validEvents = data.EventCount;
            for (int eventIndex = 0; eventIndex < data.EventCount; eventIndex++)
            {
                var recordedEvent = data.RecordedEvents[eventIndex];
                if (recordedEvent.Time > t)
                {
                    validEvents = eventIndex;
                    break;
                }
                summedPartialDetectionVector += data.RecordedEvents[eventIndex].DetectionVector;
            }
            var n = summedPartialDetectionVector.normalized;
            Color color = new(0, 0, 0, 0);
            if (validEvents >= DetectionThreshold)
            {
                var dimmingFactor = ((validEvents / (float)mostValidEvents) + (data.SummedDetectionVector.magnitude / largestMagnitude)) / 2;
                color = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
            }
            pixels[(int)Mathf.Clamp(data.Position.x, 0, _sideLength - 1) + ((int)Mathf.Clamp(data.Position.z, 0, _sideLength - 1)) * _sideLength] = color;
        }
        _detectionColor.SetPixels32(pixels);
        _detectionColor.Apply();
        var rend = DetectionVisLayer.GetComponent<MeshRenderer>();
        rend.enabled = true;
        var mat = rend.material;
        mat.SetTexture("_BaseMap", _detectionColor);
        mat.SetTexture("_EmissionMap", _detectionColor);
    }

    public void VisualiseAtTime(float t)
    {
        StartCoroutine(VisualizeDetectionData(t));
    }
    private IEnumerator VisualizeDetectionData(float t)
    {
        var pixels = _pixelBuffer;
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
                if (d.RecordedEvents[i].Time <= t && i > mostValidEvents)
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

        /*
        Debug.Log($"largestX {largestX} smallestX {smallestX} largestZ {largestZ} smallestZ {smallestZ}");
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
            pixels[(int)Mathf.Clamp(e.Position.x, 0, side - 1) + ((int)Mathf.Clamp(e.Position.z, 0, side - 1)) * side] = color;
        }
        bake.SetPixels32(pixels);
        bake.Apply();
        var png = bake.EncodeToPNG();
        var path = Path.Combine(Application.persistentDataPath, $"DetectionAll{DateTime.Now.ToFileTime()}.png");
        Debug.Log($"baking data to {path}");
        File.WriteAllBytes(path, png);
        yield return null;*/


        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var data = _vertexDetectionData[i];
            Vector3 summedPartialDetectionVector = new();
            int validEvents = data.EventCount;
            for (int eventIndex = 0; eventIndex < data.EventCount; eventIndex++)
            {
                var recordedEvent = data.RecordedEvents[eventIndex];
                if (recordedEvent.Time > t)
                {
                    validEvents = eventIndex;
                    break;
                }
                summedPartialDetectionVector += data.RecordedEvents[eventIndex].DetectionVector;
            }
            var n = summedPartialDetectionVector.normalized;
            Color color = new(0, 0, 0, 0);
            if (validEvents >= DetectionThreshold)
            {
                var dimmingFactor = ((validEvents / (float)mostValidEvents) + (data.SummedDetectionVector.magnitude / largestMagnitude)) / 2;
                color = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
            }
            pixels[(int)Mathf.Clamp(data.Position.x, 0, _sideLength - 1) + ((int)Mathf.Clamp(data.Position.z, 0, _sideLength - 1)) * _sideLength] = color;
        }
        _detectionColor.SetPixels32(pixels);
        _detectionColor.Apply();
        var rend = DetectionVisLayer.GetComponent<MeshRenderer>();
        rend.enabled = true;
        var mat = rend.material;
        mat.SetTexture("_BaseMap", _detectionColor);
        mat.SetTexture("_EmissionMap", _detectionColor);
        yield return null;
    }

    private static float SmoothedRandom01()
    {
        return (Utils.Easing.EaseInSine(Mathf.Abs(Random.insideUnitCircle.x))
            + Utils.Easing.EaseInSine(Mathf.Abs(Random.insideUnitCircle.x))) * 0.5f;
    }

    static float OffsetRandom()
    {
        return SmoothedRandom01() * (Random.value < 0.5f ? -0.5f : 0.5f);
    }

    private void OnApplicationQuit()
    {
        DetectionMeshFilter.GetComponent<MeshCollider>().sharedMesh = null;
        Destroy(DetectionMeshFilter);
        _vertexDetectionData = null;
        Resources.UnloadUnusedAssets();
    }

}
