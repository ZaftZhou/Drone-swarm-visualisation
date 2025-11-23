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
        Time,
        Overlap
    }

    public struct TerrainDetectionEvent
    {
        public Vector3 DetectionVector;
        public float Time;
        public int DroneIndex;
    }

    public struct VertexDetectionData
    {
        public TerrainDetectionEvent[] Events;
        public Vector3 Position;
        public Vector3 SummedDetectionVector;
        public int EventCount;
        public int TimesSeen;
    }

    public struct TargetDetectionEvent
    {
        public float Distance;
        public float Time;
        public int DroneIndex;
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
    public Vector3 DroneStart;
    public RectTransform FindTimeIndicator;
    public float FTIndicatorMinX;


    private IDronePathSampler _dronePathSampler;
    private VertexDetectionData[] _vertexDetectionData;
    private float _detectionCircleRelativeHeight;
    private Vector3[] _detectionVerts;
    private Texture2DArray _visTextureArray;
    private Color32[][] _visPixels;
    private int[] _detectionTris;
    private int _sideLength;
    private int _layerAmount;
    private bool _doneDetecting = false;
    private Material _visMaterial;
    private string _simulationId;
    private float _targetFoundTime;
    private int _targetFindingDrone;
    private bool _targetFound;

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
            var toggleIndex = i;
            VisToggles[i].onValueChanged.AddListener((value) => SetLayerVisibility(toggleIndex, value));
        }


        for (int i = 0; i < _detectionVerts.Length; i++)
        {
            var data = _vertexDetectionData[i];
            data.Events = new TerrainDetectionEvent[RecordedEventCount];
            data.Position = DetectionMeshFilter.transform.TransformPoint(_detectionVerts[i]);
            _vertexDetectionData[i] = data;
        }

        if (!DronePathSampler.TryGetComponent(out _dronePathSampler))
        {
            throw new MissingComponentException($"No drone path sampler found in {DronePathSampler.name}!");
        }

        DroneStart = new Vector3(DroneStart.x, Terrain.SampleHeight(DroneStart) + 0.1f, DroneStart.z);

        DroneVisualiser.Initialize(DroneCount);
        DroneVisualiser.Visualise(DroneStart, DetectionAngle, Enumerable.Repeat(1f, DroneCount).ToArray());
        DetectionAngleInput.text = DetectionAngle.ToString();
        MaxDetectionDistanceInput.text = MaxDetectionDistance.ToString();
        DetectionThresholdInput.text = DetectionThreshold.ToString();
        DetectionSimulationStepsInput.text = DetectionSimulationSteps.ToString();
        DetectionRayCountInput.text = DetectionRayCount.ToString();
    }

    public void StartDetectionTest()
    {
        FindTimeIndicator.gameObject.SetActive(false);
        DetectionAngle = float.Parse(DetectionAngleInput.text);
        _detectionCircleRelativeHeight = -0.5f / Mathf.Tan(DetectionAngle * Mathf.Deg2Rad);
        MaxDetectionDistance = float.Parse(MaxDetectionDistanceInput.text);
        DetectionThreshold = int.Parse(DetectionThresholdInput.text);
        DetectionRayCount = int.Parse(DetectionRayCountInput.text);
        DetectionSimulationSteps = int.Parse(DetectionSimulationStepsInput.text);
        _dronePathSampler.InitializePaths(DroneCount, DroneStart);
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
        VisualiseDetectionData(TimeSlider.value);
        Debug.Log("Done");
    }

    private async Awaitable DetectionAsync(int steps, IDronePathSampler pathSampler)
    {
        _targetFound = false;
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
                            if (detectionVertex.EventCount < detectionVertex.Events.Length)
                            {
                                detectionVertex.Events[detectionVertex.EventCount].DetectionVector = (location - detectionVertex.Position);
                                detectionVertex.Events[detectionVertex.EventCount].Time = progress;
                                detectionVertex.Events[detectionVertex.EventCount].DroneIndex = droneIndex;
                                detectionVertex.EventCount++;
                            }
                            detectionVertex.SummedDetectionVector += (location - detectionVertex.Position).normalized
                               * Mathf.Lerp(1f, 0.1f, (location - detectionVertex.Position).magnitude / MaxDetectionDistance);
                            detectionVertex.TimesSeen++;
                            _vertexDetectionData[_detectionTris[hit.triangleIndex * 3 + vertexIndex]] = detectionVertex;
                        }
                    } else if (hit.collider.TryGetComponent(out DetectionTarget _))
                    {
                        _targetFound = true;
                        _targetFoundTime = progress;
                        _targetFindingDrone = droneIndex;
                    }
                }
            }
        }
        droneCommands.Dispose();
        foreach (var droneResult in droneResults)
        {
            droneResult.Dispose();
        }
        _simulationId = $"Simulation {DateTime.Now.DayOfYear}-{DateTime.Now.Hour}-{DateTime.Now.Minute}";
        if (_targetFound)
        {
            FindTimeIndicator.gameObject.SetActive(true);
            FindTimeIndicator.anchoredPosition = new Vector2(Mathf.Lerp(FTIndicatorMinX, 0, _targetFoundTime), FindTimeIndicator.anchoredPosition.y);
        } 
        _doneDetecting = true;
        Debug.Log($"Target found: {_targetFound}");
    }

    public void Visualise()
    {
        VisualiseAtTime(TimeSlider.value);
    }
    public void VisualiseAtTime(float t)
    {
        if (!_doneDetecting) { return; }
        VisualiseDetectionData(t);
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

    private void VisualiseDetectionData(float t)
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
                if (d.Events[i].Time <= t && i > mostValidEvents)
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
            ClearColors(visColors);
            var data = _vertexDetectionData[i];
            Vector3 summedPartialDetectionVector = new();
            int validEvents = data.EventCount;
            int[] droneSeens = new int[DroneCount];
            for (int eventIndex = 0; eventIndex < data.EventCount; eventIndex++)
            {
                var recordedEvent = data.Events[eventIndex];
                if (recordedEvent.Time > t)
                {
                    validEvents = eventIndex;
                    break;
                }
                summedPartialDetectionVector += data.Events[eventIndex].DetectionVector;
                droneSeens[data.Events[eventIndex].DroneIndex]++;
            }

            if (validEvents >= DetectionThreshold)
            {
                var n = summedPartialDetectionVector.normalized;
                var dimmingFactor = ((validEvents / (float)mostValidEvents) + (data.SummedDetectionVector.magnitude / largestMagnitude)) / 2;
                var lastSeen = data.Events[data.EventCount - 1].Time;
                int mostSeenByDroneIndex = CalculateMostSeenByDroneIndex(droneSeens);
                int seenBy = 0;
                for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
                {
                    seenBy += droneSeens[droneIndex] > 0 ? 1 : 0;
                }
                bool hasOverlap = seenBy > 1;
                float overlapAmount = hasOverlap ? ((float)seenBy / DroneCount) : 0;
                visColors[(int)VisLayer.Detection] = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
                visColors[(int)VisLayer.Drone] = DroneColors[mostSeenByDroneIndex];
                visColors[(int)VisLayer.Time] = new(lastSeen, lastSeen, lastSeen);
                visColors[(int)VisLayer.Overlap] = new Color(overlapAmount, overlapAmount, overlapAmount, hasOverlap ? 1 : 0);
            }
            else
            {
                visColors[(int)VisLayer.Undetected] = Color.red;
            }
            var pixelIndex = _visPixels[0].Length - 1 - ((int)Mathf.Clamp(data.Position.x, 0, _sideLength - 1) + ((int)Mathf.Clamp(data.Position.z, 0, _sideLength - 1)) * _sideLength);
            for (int layer = 0; layer < _layerAmount; layer++)
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
    }

    private static int CalculateMostSeenByDroneIndex(int[] droneSeens)
    {
        int mostSeenByDroneIndex = 0;
        for (int droneIndex = 0; droneIndex < droneSeens.Length; droneIndex++)
        {
            if (droneSeens[droneIndex] > droneSeens[mostSeenByDroneIndex])
            {
                mostSeenByDroneIndex = droneIndex;
            }
        }

        return mostSeenByDroneIndex;
    }

    private static void ClearColors(Color[] visColors)
    {
        for (int c = 0; c < 5; c++)
        {
            visColors[c] = new(0, 0, 0, 0);
        }
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
        if (!_doneDetecting) { return; }
        Texture2D outputTexture = new (_sideLength, _sideLength);
        var simulationFolderPath = Path.Combine(Application.persistentDataPath, _simulationId);
        Directory.CreateDirectory(simulationFolderPath);
        Debug.Log($"baking data to {simulationFolderPath}");

        for (int i = 0; i < _layerAmount; i++)
        {
            if (!exportOnlyVisible || VisToggles[i].isOn)
            {
                outputTexture.SetPixels32(_visTextureArray.GetPixels32(i));
                outputTexture.Apply();
                var png = outputTexture.EncodeToPNG();
                var path = Path.Combine(simulationFolderPath, $"{(VisLayer)i} T-{TimeSlider.value}.png");
                File.WriteAllBytes(path, png);
            }
        }
    }

    public void VisualiseFindingMoment()
    {
        if (!_doneDetecting) { return; }
        TimeSlider.SetValueWithoutNotify(_targetFoundTime);
        VisualiseAtTime(_targetFoundTime);
    }

    private void SetLayerVisibility(int layer, bool on)
    {
        if (!_doneDetecting) { return; }
        Debug.Log($"layer: {layer} _Show{Enum.GetName(typeof(VisLayer), layer)}");
        _visMaterial.SetInt($"_Show{Enum.GetName(typeof(VisLayer), layer)}", on ? 1 : 0);
    }
}
