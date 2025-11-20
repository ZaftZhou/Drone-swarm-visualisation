using System.Collections;
using UnityEngine;
using System.IO;
using System;
using Random = UnityEngine.Random;
using TMPro;
using UnityEngine.UI;
using Unity.Jobs;
using Unity.Collections;
using System.Linq;
using System.Collections.Generic;
using Detection;

public class DetectionTester : MonoBehaviour
{
    [Serializable]
    public enum VisLayer
    {
        Detection,
        Drone,
        Probability,
        Undetected,
        Time
    }

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
    //public GameObject DetectionMeshBasePrefab;
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
    public TMP_InputField DetectionAngleInput;
    public TMP_InputField MaxDetectionDistanceInput;
    public TMP_InputField DetectionThresholdInput;
    public TMP_InputField DetectionRayCountInput;
    public TMP_InputField DetectionSimulationStepsInput;
    public Slider TimeSlider;
    public List<Toggle> VisToggles;
    public GameObject VisPlane;
    public DroneVisualiser DroneVisualiser;
    public CameraController CameraController;
    private IDronePathSampler _dronePathSampler;
    private VertexDetectionData[] _vertexDetectionData;
    private float _detectionCircleRelativeHeight;
    private Vector3[] _detectionVerts;
    private Texture2DArray _visTextureArray;
    private Color32[][] _visPixels;
    private int[] _detectionTris;
    private int _sideLength;
    private int _layerAmount;
    [SerializeField]
    private Vector3 _droneStart;
    private bool _doneDetecting = false;
    private Material _visMaterial;
    private void Awake()
    {
        var mesh = DetectionMeshFilter.mesh;
        _visMaterial = VisPlane.GetComponent<MeshRenderer>().material;
        _layerAmount = Enum.GetValues(typeof(VisLayer)).Length;
        _detectionVerts = mesh.vertices;
        _detectionTris = mesh.triangles;
        _vertexDetectionData = new VertexDetectionData[_detectionVerts.Length];
        _sideLength = (int)Mathf.Sqrt(_detectionVerts.Length);
        _visPixels = new Color32[_layerAmount][];
        _visTextureArray = new(_sideLength, _sideLength, _layerAmount, TextureFormat.RGBA32, false);
        for (int i = 0; i < _layerAmount; i++)
        {
            _visPixels[i] = new Color32[_detectionVerts.Length];
        }
        VisToggles[0].onValueChanged.AddListener((value) => SetLayerVisibility(0, value));
        VisToggles[1].onValueChanged.AddListener((value) => SetLayerVisibility(1, value));
        VisToggles[2].onValueChanged.AddListener((value) => SetLayerVisibility(2, value));
        VisToggles[3].onValueChanged.AddListener((value) => SetLayerVisibility(3, value));
        VisToggles[4].onValueChanged.AddListener((value) => SetLayerVisibility(4, value));


        for (int i = 0; i < _detectionVerts.Length; i++)
        {
            var data = _vertexDetectionData[i];
            data.RecordedEvents = new DetectionEvent[RecordedEventCount];
            data.Position = DetectionMeshFilter.transform.TransformPoint(_detectionVerts[i]);
            _vertexDetectionData[i] = data;
        }

        if (!DronePathSampler.TryGetComponent(out _dronePathSampler))
        {
            throw new MissingComponentException($"No drone path sampler found in {DronePathSampler.name}!");
        }

        _droneStart = new Vector3(_droneStart.x, Terrain.SampleHeight(_droneStart), _droneStart.z);

        DroneVisualiser.Initialize(DroneCount);
        DroneVisualiser.Visualise(_droneStart, DetectionAngle, Enumerable.Repeat(1f, DroneCount).ToArray());
        DetectionAngleInput.text = DetectionAngle.ToString();
        MaxDetectionDistanceInput.text = MaxDetectionDistance.ToString();
        DetectionThresholdInput.text = DetectionThreshold.ToString();
        DetectionSimulationStepsInput.text = DetectionSimulationSteps.ToString();
        DetectionRayCountInput.text = DetectionRayCount.ToString();


    }

    public void StartDetectionTest()
    {
        DetectionAngle = float.Parse(DetectionAngleInput.text);
        _detectionCircleRelativeHeight = -0.5f / Mathf.Tan(DetectionAngle * Mathf.Deg2Rad);
        MaxDetectionDistance = float.Parse(MaxDetectionDistanceInput.text);
        DetectionThreshold = int.Parse(DetectionThresholdInput.text);
        DetectionRayCount = int.Parse(DetectionRayCountInput.text);
        DetectionSimulationSteps = int.Parse(DetectionSimulationStepsInput.text);
        _dronePathSampler.InitializePaths(DroneCount, _droneStart);
        Debug.Log("Detecting");
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
        await Awaitable.NextFrameAsync();
        try
        {
            await DetectionAsync(DetectionSimulationSteps, _dronePathSampler);
            Debug.Log("Done detecting");
            await Awaitable.NextFrameAsync();
        }
        catch (Exception ex)
        {
            Debug.Log(ex);
        }
        Debug.Log("Visualising");
        await Awaitable.NextFrameAsync();
        StartCoroutine(VisualiseDetectionData(TimeSlider.value));
        Debug.Log("Done");
    }

    private async Awaitable DetectionAsync(int steps, IDronePathSampler pathSampler)
    {
        bool targetFound = false;
        await Awaitable.EndOfFrameAsync();
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
                        new RaycastCommand(locations[droneIndex, step], randomDirections[droneIndex, step, j], new QueryParameters() { layerMask = DetectionLayer}, MaxDetectionDistance);
                }

                JobHandle handle = RaycastCommand.ScheduleBatch(droneCommands, droneResults[droneIndex], 1, 1);
                //await Awaitable.NextFrameAsync();
                handle.Complete();
                foreach (var hit in droneResults[droneIndex])
                {
                    if (hit.collider == null) continue;
                    if (hit.collider.TryGetComponent(out DetectionPlane _))
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
                    } else if (hit.collider.TryGetComponent(out DetectionTarget _))
                    {
                        targetFound = true;
                    }
                }
            }
        }
        droneCommands.Dispose();
        foreach (var droneResult in droneResults)
        {
            droneResult.Dispose();
        }
        _doneDetecting = true;
        Debug.Log($"Target found: {targetFound}");
    }

    public void Visualise()
    {
        VisualiseAtTime(TimeSlider.value);
    }
    public void VisualiseAtTime(float t)
    {
        if (!_doneDetecting) { return; }
        StartCoroutine(VisualiseDetectionData(t));
    }

    public void UpdateDetectionThreshold()
    {
        var newDetectionThreshold = int.Parse(DetectionThresholdInput.text);
        if (newDetectionThreshold < 0 || newDetectionThreshold > RecordedEventCount)
        {
            Debug.Log("Invalid detection threshold");
            DetectionThresholdInput.text = DetectionThreshold.ToString();
        }
        else
        {
            DetectionThreshold = newDetectionThreshold;
            if (_doneDetecting)
            {
                Visualise();
            }
        }
    }

    private IEnumerator VisualiseDetectionData(float t)
    {
        int mostSeen = 0;
        int mostValidEvents = 0;
        float largestMagnitude = 0;
        Color[] visColors = new Color[_layerAmount];
        DetectionThreshold = int.Parse(DetectionThresholdInput.text);
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
        }

        for (int i = 0; i < _vertexDetectionData.Length; i++)
        {
            var data = _vertexDetectionData[i];
            Vector3 summedPartialDetectionVector = new();
            int validEvents = data.EventCount;
            int[] droneSeens = new int[DroneCount];

            for (int eventIndex = 0; eventIndex < data.EventCount; eventIndex++)
            {
                var recordedEvent = data.RecordedEvents[eventIndex];
                if (recordedEvent.Time > t)
                {
                    validEvents = eventIndex;
                    break;
                }
                summedPartialDetectionVector += data.RecordedEvents[eventIndex].DetectionVector;
                droneSeens[data.RecordedEvents[eventIndex].DroneIndex]++;
            }
            int mostSeenByDroneIndex = 0;
            for (int droneIndex = 0; droneIndex < droneSeens.Length; droneIndex++)
            {
                if (droneSeens[droneIndex] > droneSeens[mostSeenByDroneIndex])
                {
                    mostSeenByDroneIndex = droneIndex;
                }
            }
            var n = summedPartialDetectionVector.normalized;

            for (int c = 0; c < 5; c++)
            {
                visColors[c] = new(0, 0, 0, 0);
            }

            if (validEvents >= DetectionThreshold)
            {
                var dimmingFactor = ((validEvents / (float)mostValidEvents) + (data.SummedDetectionVector.magnitude / largestMagnitude)) / 2;
                visColors[(int)VisLayer.Detection] = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
                visColors[(int)VisLayer.Drone] = DroneColors[mostSeenByDroneIndex];
                var lastSeen = data.RecordedEvents[data.EventCount - 1].Time;
                visColors[(int)VisLayer.Time] = new(lastSeen, lastSeen, lastSeen);
            }
            else
            {
                visColors[(int)VisLayer.Undetected] = Color.red;
            }
            var pixelIndex = _visPixels[(int)VisLayer.Detection].Length - 1 - ((int)Mathf.Clamp(data.Position.x, 0, _sideLength - 1) + ((int)Mathf.Clamp(data.Position.z, 0, _sideLength - 1)) * _sideLength);
            for (int layer = 0; layer < 5; layer++)
            {
                _visPixels[layer][pixelIndex] = visColors[layer];
            }
        }

        for (int i = 0; i < _layerAmount; i++)
        {
            _visTextureArray.SetPixels32(_visPixels[i], i);
            _visMaterial.SetInt($"_Show{(VisLayer)i}", VisToggles[i].isOn ? 1 : 0);
        }
        _visTextureArray.Apply();
        _visMaterial.SetTexture("_VisTextures", _visTextureArray);

        var dronePositions = new Vector3[DroneCount];
        var distances = new float[DroneCount];
        for (int i = 0; i < DroneCount; i++)
        {
            dronePositions[i] = _dronePathSampler.SamplePositionAt(t, i);
            distances[i] = (dronePositions[i] - new Vector3(dronePositions[i].x, Terrain.SampleHeight(dronePositions[i]), dronePositions[i].z)).magnitude; // this is so stupid but my brain is fried
        }
        DroneVisualiser.Visualise(dronePositions, DetectionAngle, distances);
        CameraController.Retarget();
        yield return null;
    }
    private static bool IsEmptyPixel(Color32 pixel)
    {
        return pixel.r == 0 && pixel.g == 0 && pixel.b == 0 && pixel.a == 0;
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

    public void ExportImages(bool exportOnlyVisible)
    {
        Debug.LogError("FIX THIS");
        for (int i = 0; i < _layerAmount; i++)
        {
            if (exportOnlyVisible || VisToggles[i].isOn)
            {
                //var png = _visTextureArray.Get[(int)VisLayer.Detection].EncodeToPNG();
                //var path = Path.Combine(Application.persistentDataPath, $"{(VisLayer)i}AtTime{TimeSlider.value}{DateTime.Now.ToUniversalTime():u}.png");
                //Debug.Log($"baking data to {path}");
                //File.WriteAllBytes(path, png);
            }
        }
    }

    private void SetLayerVisibility(int layer, bool on)
    {
        
        Debug.Log($"layer: {layer} _Show{Enum.GetName(typeof(VisLayer), layer)}");
        _visMaterial.SetInt($"_Show{Enum.GetName(typeof(VisLayer), layer)}", on ? 1 : 0);
    }
}
