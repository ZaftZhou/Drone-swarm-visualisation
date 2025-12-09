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

namespace Detection
{

    public class DetectionTester : MonoBehaviour
    {
        [Header("Simulation settings")]
        public Vector3 DroneStart;
        public float MaxDetectionDistance = 500;
        public LayerMask DetectionLayer;
        public int DroneCount = 10;
        public int DetectionRayCount = 500;
        public int DetectionSimulationSteps = 1000;
        public float DetectionAngle = 45f;
        [Tooltip("Increases memory use A LOT, adjust carefully!")]
        public int RecordedEventCount = 10;

        [Header("Visualisation settings")]
        public Color[] DroneColors = new Color[10];
        public int DetectionThreshold = 1;

        [Header("Scene References")]
        public GameObject DronePathSampler;
        public Terrain Terrain;
        public GameObject VisPlane;
        public MeshFilter DetectionMeshFilter;
        public DroneVisualiser DroneVisualiser;
        public CameraController CameraController;
        public List<Toggle> VisToggles;
        public Toggle DrawTargetsToggle;
        public TMP_InputField DetectionAngleInput;
        public TMP_InputField MaxDetectionDistanceInput;
        public TMP_InputField DetectionThresholdInput;
        public TMP_InputField DetectionRayCountInput;
        public TMP_InputField DetectionSimulationStepsInput;
        public TMP_Dropdown PatternSelection;
        public Slider TimeSlider;
        public Button ExportButton;

        [Header("Asset References")]
        public GameObject TargetPrefab;
        public Material DetectionVisMaterial;
        public Material TargetDetectedMaterial;
        public Material TargetUndetectedMaterial;


        [Header("Export settings")]

        private IDronePathSampler _dronePathSampler;
        private float _detectionCircleRelativeHeight;
        private Vector3[] _detectionVerts;
        private int[] _detectionTris;
        private DetectionData[] _vertexDetectionData;
        private DetectionData[] _targetDetectionData;
        private Transform _targetParent;
        private readonly List<GameObject> _targets = new();
        private int _sideLength;
        private int _layerAmount;
        private Texture2DArray _visTextureArray;
        private Color32[][] _visPixels;
        private Material _visMaterial;
        private string _simulationId;
        private bool _doneDetecting = false;
        private SimulationRunSummaryData _simulationRunSummaryData;
        private SimulationSettingData _simulationSettingData;
        private DroneFlightData _droneFlightData;
        private Vector3[,] _droneLocations;
        private void Awake()
        {
            InitializeUIElements();

            var mesh = DetectionMeshFilter.mesh;

            _detectionVerts = mesh.vertices;
            _detectionTris = mesh.triangles;

            InitializeDetectionDataStructures();
            InitializeVisualisationSystem();
            InitializeDroneVisualiser();
            PopulateSettingFieldsWithInternalValues();
        }

        private void InitializeDroneVisualiser()
        {
            if (!DronePathSampler.TryGetComponent(out _dronePathSampler))
            {
                throw new MissingComponentException($"No drone path sampler found in {DronePathSampler.name}!");
            }

            DroneStart = new Vector3(DroneStart.x, Terrain.SampleHeight(DroneStart) + 0.1f, DroneStart.z);

            DroneVisualiser.Initialize(DroneCount);
            DroneVisualiser.Visualise(DroneStart, DetectionAngle, Enumerable.Repeat(1f, DroneCount).ToArray());
        }

        private void InitializeVisualisationSystem()
        {
            _layerAmount = Enum.GetValues(typeof(VisLayer)).Length;
            _visMaterial = VisPlane.GetComponent<MeshRenderer>().material;
            _sideLength = (int)Mathf.Sqrt(_detectionVerts.Length);
            _visPixels = new Color32[_layerAmount][];
            _visTextureArray = new(_sideLength, _sideLength, _layerAmount, TextureFormat.RGBA32, false);
            for (int i = 0; i < _layerAmount; i++)
            {
                _visPixels[i] = new Color32[_detectionVerts.Length];
            }
        }

        private void InitializeDetectionDataStructures()
        {
            _targetParent = new GameObject("Targets").transform;
            _vertexDetectionData = new DetectionData[_detectionVerts.Length];
            var min = _detectionVerts.Min((vert) => vert.x);
            var max = _detectionVerts.Max((vert) => vert.x);
            //Debug.Log("min" + min);
            //Debug.Log("max" + max);
            for (int i = 0; i < _detectionVerts.Length; i++)
            {
                var data = _vertexDetectionData[i];
                var position = DetectionMeshFilter.transform.TransformPoint(_detectionVerts[i]);
                data.Events = new DetectionEvent[RecordedEventCount];
                data.Position = position;
                _vertexDetectionData[i] = data;
                if (Mathf.FloorToInt(_detectionVerts[i].x) % 5 == 0
                    && Mathf.FloorToInt(_detectionVerts[i].z) % 5 == 0
                    && _detectionVerts[i].x != min
                    && _detectionVerts[i].x != max
                    && _detectionVerts[i].z != min
                    && _detectionVerts[i].z != max)
                {
                    var target = Instantiate(TargetPrefab, position + Vector3.up, Quaternion.identity, _targetParent);
                    target.GetComponent<DetectionTarget>().index = _targets.Count;
                    _targets.Add(target);
                }
            }
            _targetDetectionData = new DetectionData[_targets.Count];

            for (int i = 0; i < _targets.Count; i++)
            {
                var data = _targetDetectionData[i];
                data.Position = _targets[i].transform.position;
                data.Events = new DetectionEvent[RecordedEventCount];
                _targetDetectionData[i] = data;
            }
            //Debug.Log($"_targets.Count {_targets.Count}");
        }

        private void PopulateSettingFieldsWithInternalValues()
        {
            DetectionAngleInput.text = DetectionAngle.ToString();
            MaxDetectionDistanceInput.text = MaxDetectionDistance.ToString();
            DetectionThresholdInput.text = DetectionThreshold.ToString();
            DetectionSimulationStepsInput.text = DetectionSimulationSteps.ToString();
            DetectionRayCountInput.text = DetectionRayCount.ToString();
        }

        private void InitializeUIElements()
        {
            PatternSelection.AddOptions(Enum.GetNames(typeof(PatternSelection)).ToList());
            for (int i = 0; i < VisToggles.Count; i++)
            {
                if (VisToggles[i].TryGetComponent(out VisLayerToggle visLayerToggle))
                {
                    var toggleLayer = visLayerToggle.VisLayer;
                    VisToggles[i].onValueChanged.AddListener((value) => SetLayerVisibility(toggleLayer, value));
                }
                else
                {
                    throw new Exception("Missing VisLayerToggle component!");
                }
            }
            DrawTargetsToggle.onValueChanged.AddListener((value) => SetTargetsEnabled(value));
        }

        public void StartDetectionTest()
        {
            //FindTimeIndicator.gameObject.SetActive(false);
            MaxDetectionDistance = float.Parse(MaxDetectionDistanceInput.text);
            DetectionThreshold = int.Parse(DetectionThresholdInput.text);
            DetectionRayCount = int.Parse(DetectionRayCountInput.text);
            DetectionSimulationSteps = int.Parse(DetectionSimulationStepsInput.text);
            DetectionAngle = float.Parse(DetectionAngleInput.text);
            _detectionCircleRelativeHeight = -0.5f / Mathf.Tan(DetectionAngle * Mathf.Deg2Rad);
            var patternSelection = (PatternSelection)PatternSelection.value;
            ((PartitionGridPathGenerator)_dronePathSampler).SetPatternSelection(patternSelection);
            _dronePathSampler.InitializePaths(DroneCount, DroneStart);
            _simulationId = $"Simulation {DateTime.Now.DayOfYear}-{DateTime.Now.Hour}-{DateTime.Now.Minute}";
            _simulationSettingData = new()
            {
                SimulationId = _simulationId,
                DroneStart = DroneStart,
                MaxDetectionDistance = MaxDetectionDistance,
                DroneCount = DroneCount,
                DetectionRayCount = DetectionRayCount,
                DetectionSimulationSteps = DetectionSimulationSteps,
                DetectionAngle = DetectionAngle,
                TerrainWidth = Terrain.terrainData.size.x,
                TerrainDepth = Terrain.terrainData.size.z,
                TreeCount = Terrain.terrainData.treeInstanceCount,
                TargetCount = _targets.Count,
                TargetSize = _targets[0].GetComponent<Collider>().bounds.size,
            };
            ExportButton.interactable = false;
            Debug.Log("Detecting");
            _ = StartAsyncDetection();
        }

        private async Awaitable StartAsyncDetection()
        {
            try
            {
                ResetTerrainDetectionData();
                ResetTargets();
                await Awaitable.NextFrameAsync();
                await DetectionAsync(DetectionSimulationSteps, _dronePathSampler);
                Debug.Log("Done detecting");
                await Awaitable.NextFrameAsync();
                Debug.Log("Visualising");
                await Awaitable.NextFrameAsync();
                VisualiseDetectionData(TimeSlider.value);
                Debug.Log("Done");
                ExportButton.interactable = true;
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void ResetTerrainDetectionData()
        {
            for (int i = 0; i < _vertexDetectionData.Length; i++)
            {
                var vertex = _vertexDetectionData[i];
                vertex.EventCount = 0;
                vertex.TimesSeen = 0;
                vertex.SummedDetectionVector = new Vector3();
                _vertexDetectionData[i] = vertex;
            }
        }

        private void ResetTargets()
        {
            for (int i = 0; i < _targetDetectionData.Length; i++)
            {
                var target = _targetDetectionData[i];
                target.EventCount = 0;
                target.TimesSeen = 0;
                target.SummedDetectionVector = new Vector3();
                _targetDetectionData[i] = target;
                _targets[i].GetComponent<MeshRenderer>().material = TargetUndetectedMaterial;
            }
        }
        private async Awaitable DetectionAsync(int steps, IDronePathSampler pathSampler)
        {
            await Awaitable.EndOfFrameAsync();
            _droneLocations = new Vector3[DroneCount, steps];
            var minCommandsPerJob = DetectionRayCount < 32 ? DetectionRayCount : 32;
            Vector3[,,] randomDirections = new Vector3[DroneCount, steps, DetectionRayCount];
            for (int step = 0; step < steps; step++)
            {
                float progress = step / (float)steps;
                for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
                {
                    _droneLocations[droneIndex, step] = pathSampler.SamplePositionAt(progress, droneIndex);
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
                    Vector3 droneLocation = _droneLocations[droneIndex, step];
                    float progress = step / (float)steps;
                    for (int j = 0; j < DetectionRayCount; j++)
                    {
                        droneCommands[j] =
                            new RaycastCommand(_droneLocations[droneIndex, step], randomDirections[droneIndex, step, j], new QueryParameters() { layerMask = DetectionLayer }, MaxDetectionDistance);
                    }

                    JobHandle handle = RaycastCommand.ScheduleBatch(droneCommands, droneResults[droneIndex], minCommandsPerJob: minCommandsPerJob, maxHits: 1);
                    //await Awaitable.NextFrameAsync();
                    handle.Complete();
                    foreach (var hit in droneResults[droneIndex])
                    {
                        if (hit.collider == null) continue;
                        if (hit.collider.TryGetComponent(out DetectionPlane _))
                        {
                            HandeDetectionPlaneHit(droneIndex, droneLocation, progress, hit);
                        }
                        else if (hit.collider.TryGetComponent(out DetectionTarget target))
                        {
                            HandleTargetHit(droneIndex, progress, droneLocation, target);
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

        }

        private void HandeDetectionPlaneHit(int droneIndex, Vector3 droneLocation, float progress, RaycastHit hit)
        {
            //https://discussions.unity.com/t/pinpointing-one-vertice-with-raycasthit/181509
            var b = hit.barycentricCoordinate;
            int index = hit.triangleIndex * 3;

            if (b.x > b.y)
            {
                if (!(b.x > b.z))
                    index += 2; // z
            }
            else if (b.y > b.z)
                index += 1; // y
            else
                index += 2; // z

            var detectionVertex = _vertexDetectionData[_detectionTris[index]];
            if (detectionVertex.EventCount < detectionVertex.Events.Length)
            {
                detectionVertex.Events[detectionVertex.EventCount].DetectionVector = droneLocation - detectionVertex.Position;
                detectionVertex.Events[detectionVertex.EventCount].Time = progress;
                detectionVertex.Events[detectionVertex.EventCount].DroneIndex = droneIndex;
                detectionVertex.EventCount++;
            }
            detectionVertex.SummedDetectionVector += (droneLocation - detectionVertex.Position).normalized
                * Mathf.Lerp(1f, 0.1f, (droneLocation - detectionVertex.Position).magnitude / MaxDetectionDistance);
            detectionVertex.TimesSeen++;
            _vertexDetectionData[_detectionTris[index]] = detectionVertex;
            
        }

        private void HandleTargetHit(int droneIndex, float progress, Vector3 droneLocation, DetectionTarget target)
        {

            var targetData = _targetDetectionData[target.index];

            if (targetData.EventCount < targetData.Events.Length)
            {
                targetData.Events[targetData.EventCount].DetectionVector = droneLocation - targetData.Position;
                targetData.Events[targetData.EventCount].Time = progress;
                targetData.Events[targetData.EventCount].DroneIndex = droneIndex;
                targetData.EventCount++;
            }
            targetData.SummedDetectionVector += (droneLocation - targetData.Position).normalized
               * Mathf.Lerp(1f, 0.1f, (droneLocation - targetData.Position).magnitude / MaxDetectionDistance);
            targetData.TimesSeen++;
            _targetDetectionData[target.index] = targetData;
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
            var newDetectionThreshold = Mathf.Clamp(int.Parse(DetectionThresholdInput.text), 1, RecordedEventCount);
            DetectionThresholdInput.text = newDetectionThreshold.ToString();
            DetectionThreshold = newDetectionThreshold;
            if (_doneDetecting)
            {
                Visualise();
            }
        }

        private void VisualiseDetectionData(float t)
        {
            AnalyseSimulationData(t);
            VisualiseDrones(t);
            CameraController.Retarget();
        }

        private void AnalyseSimulationData(float t)
        {
            DetectionThreshold = int.Parse(DetectionThresholdInput.text);

            //Terrain detection analysis
            int areaCovered = 0;
            int totalOverlap = 0;
            int mostSeen = 0;
            int mostValidEvents = 0;
            float largestMagnitude = 0;
            Color[] visColors = new Color[_layerAmount];
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
                    areaCovered++;
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
                    totalOverlap += hasOverlap ? seenBy : 0;
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
            //Terrain detection visualisation
            for (int i = 0; i < VisToggles.Count; i++)
            {
                var layer = VisToggles[i].GetComponent<VisLayerToggle>().VisLayer;
                _visTextureArray.SetPixels32(_visPixels[(int)layer], (int)layer);
                _visMaterial.SetInt($"_Show{Enum.GetName(typeof(VisLayer), layer)}", VisToggles[i].isOn ? 1 : 0);
            }
            _visTextureArray.Apply();
            _visMaterial.SetTexture("_VisTextures", _visTextureArray);

            int targetsFound = 0;
            //Target detection analysis & visualisation
            for (int i = 0; i < _targetDetectionData.Length; i++)
            {
                var data = _targetDetectionData[i];
                Vector3 summedPartialDetectionVector = new();
                int validEvents = data.EventCount;
                for (int eventIndex = 0; eventIndex < data.EventCount; eventIndex++)
                {
                    var recordedEvent = data.Events[eventIndex];
                    if (recordedEvent.Time > t)
                    {
                        validEvents = eventIndex;
                        break;
                    }
                    summedPartialDetectionVector += data.Events[eventIndex].DetectionVector;
                }
                if (validEvents >= DetectionThreshold)
                {
                    _targets[i].GetComponent<MeshRenderer>().material = TargetDetectedMaterial;
                    targetsFound++;
                }
            }

            _simulationRunSummaryData = CreateRunSummary(t, areaCovered, totalOverlap, targetsFound);
            _droneFlightData = CreateDroneFlightData();
        }

        private DroneFlightData CreateDroneFlightData()
        {
            var droneDistances = _dronePathSampler.GetTotalDistances();
            var droneLocations = new Vector3[DroneCount][];
            for (int droneIndex = 0; droneIndex < DroneCount; droneIndex++)
            {
                droneLocations[droneIndex] = new Vector3[DetectionSimulationSteps];
                for (int step = 0; step < DetectionSimulationSteps; step++)
                {
                    droneLocations[droneIndex][step] = _droneLocations[droneIndex, step];
                }
            }
            return new ()
            {
                DroneTotalFlightDistances = droneDistances,
                //Do I hate this? Yes
                Drone1FlightPath = droneLocations[0],
                Drone2FlightPath = droneLocations[1],
                Drone3FlightPath = droneLocations[2],
                Drone4FlightPath = droneLocations[3],
                Drone5FlightPath = droneLocations[4],
                Drone6FlightPath = droneLocations[5],
                Drone7FlightPath = droneLocations[6],
                Drone8FlightPath = droneLocations[7],
                Drone9FlightPath = droneLocations[8],
                Drone10FlightPath = droneLocations[9],
            };
        }

        private SimulationRunSummaryData CreateRunSummary(float t, int areaCovered, int totalOverlap, int targetsFound)
        {


            return new()
            {
                SimulationId = _simulationId,
                Time = t,
                DetectionThreshold = DetectionThreshold,
                TargetsFound = targetsFound,
                AreaCovered = areaCovered,
                OverlapAmount = totalOverlap,
            };
        }

        private void VisualiseDrones(float t)
        {
            var dronePositions = new Vector3[DroneCount];
            var distances = new float[DroneCount];
            for (int i = 0; i < DroneCount; i++)
            {
                dronePositions[i] = _dronePathSampler.SamplePositionAt(t, i);
                distances[i] = (dronePositions[i] - new Vector3(dronePositions[i].x, Terrain.SampleHeight(dronePositions[i]), dronePositions[i].z)).magnitude; // this is so stupid but my brain is fried
            }
            DroneVisualiser.Visualise(dronePositions, DetectionAngle, distances);
        }

        private void SetLayerVisibility(VisLayer layer, bool on)
        {
            if (!_doneDetecting) { return; }
            //Debug.Log($"layer: {layer} _Show{Enum.GetName(typeof(VisLayer), layer)}");
            _visMaterial.SetInt($"_Show{Enum.GetName(typeof(VisLayer), layer)}", on ? 1 : 0);
        }

        public void SetLayersOpacity(float opacity)
        {
            _visMaterial.SetFloat($"_Opacity", Mathf.Clamp01(opacity));
        }

        public void SetTargetsEnabled(bool value)
        {
            _targetParent.gameObject.SetActive(value);
        }
        public void ExportEverything()
        {
            var simulationFolderPath = Path.Combine(Application.persistentDataPath, _simulationId);
            Debug.Log($"Exporting data to {simulationFolderPath}");
            Directory.CreateDirectory(simulationFolderPath);
            ExportImages(false, simulationFolderPath);
            ExportJsons(simulationFolderPath);
        }

        private void ExportJsons(string simulationFolderPath)
        {
            File.WriteAllText(Path.Combine(simulationFolderPath, $"SimulationSettingData.json"), JsonUtility.ToJson(_simulationSettingData, true));
            File.WriteAllText(Path.Combine(simulationFolderPath, $"SimulationRunSummaryData.json"), JsonUtility.ToJson(_simulationRunSummaryData, true));
            File.WriteAllText(Path.Combine(simulationFolderPath, $"DroneFlightData.json"), JsonUtility.ToJson(_droneFlightData, true));
        }

        public void ExportImages(bool exportOnlyVisible, string folderPath)
        {
            if (!_doneDetecting) { return; }
            Texture2D outputTexture = new(_sideLength, _sideLength);
            for (int i = 0; i < VisToggles.Count; i++)
            {
                if (!exportOnlyVisible || VisToggles[i].isOn)
                {
                    outputTexture.SetPixels32(_visTextureArray.GetPixels32(i));
                    outputTexture.Apply();
                    var png = outputTexture.EncodeToPNG();
                    var filePath = Path.Combine(folderPath, $"{(VisLayer)i} T-{TimeSlider.value}.png");
                    File.WriteAllBytes(filePath, png);
                }
            }
        }

        public void VisualiseFindingMoment()
        {
            if (!_doneDetecting) { return; }
            //TimeSlider.SetValueWithoutNotify(_targetFoundTime);
            //VisualiseAtTime(_targetFoundTime);
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
    }

}
