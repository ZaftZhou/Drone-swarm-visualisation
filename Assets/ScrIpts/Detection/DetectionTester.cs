using System.Collections;
using UnityEngine;
using System.IO;
using System;
using Random = UnityEngine.Random;

public class DetectionTester : MonoBehaviour
{
    public struct DetectionEvent
    {
        public Vector3 position;
        public Vector3 SummedDetectionVector;
        public float FirstDiscovered;
        public float LastSeen;
        public int timesSeen;
    }

    public Terrain Terrain;
    public float DetectionAngle;
    public GameObject DetectionCircle;
    public MeshFilter DetectionMeshFilter;
    public float MaxDetectionDistance = 500;
    public LayerMask DetectionLayer;
    public RenderTexture DetectionTexture;
    public int DetectionThreshold = 1;
    private DetectionEvent[] _events;
    private Vector3[] _directions;

    private void Awake()
    {
        var mesh = DetectionMeshFilter.mesh;
        var vertices = mesh.vertices;
        var terrainData = Terrain.terrainData;
        _directions = GenerateRaycastDirections();
        for (int i = 0; i < vertices.Length; i++)
        {
            float height = terrainData.GetInterpolatedHeight((vertices[i].x + DetectionMeshFilter.transform.position.x) / terrainData.size.x, (vertices[i].z + DetectionMeshFilter.transform.position.z) / terrainData.size.z);
            vertices[i] = new Vector3(vertices[i].x, height, vertices[i].z);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        DetectionMeshFilter.GetComponent<MeshCollider>().sharedMesh = mesh;
        _events = new DetectionEvent[vertices.Length];
        Debug.Log($"vertex count {vertices.Length}");
    }

    private Vector3[] GenerateRaycastDirections()
    {
        var mesh = DetectionCircle.GetComponent<MeshFilter>().mesh;
        var height = -0.5f / Mathf.Tan(DetectionAngle * Mathf.Deg2Rad); // Trig: Adjacent = Opposite / Tan(Theta)
        Vector3[] directions = new Vector3[mesh.vertices.Length];
        for (int i = 0; i < directions.Length; i++)
        {
            directions[i] = new Vector3(mesh.vertices[i].x, height, mesh.vertices[i].z);
        }
        return directions;
    }

    private void Start()
    {        
        StartDetectionTest();
    }

    public void StartDetectionTest()
    {
        StartCoroutine(DetectingCoroutine());
    }

    private IEnumerator DetectingCoroutine()
    {
        var vertices = DetectionMeshFilter.mesh.vertices;
        var triangles = DetectionMeshFilter.mesh.triangles;

        for (int i = 0; i < _events.Length; i++)
        {
            var detection = _events[i];
            //var p1 = vertices[triangles[i * 3]];
            //var p2 = vertices[triangles[i * 3 + 1]];
            //var p3 = vertices[triangles[i * 3 + 2]];
            //detection.position = DetectionMeshFilter.transform.TransformPoint(new Vector3((p1.x + p2.x + p3.x) / 3, (p1.y + p2.y + p3.y) / 3, (p1.z + p2.z + p3.z) / 3));
            detection.position = DetectionMeshFilter.transform.TransformPoint(vertices[i]);
            _events[i] = detection;
        }
        Debug.Log("finished setting positions");
        yield return null;

        Vector3 location = new(500, 350, 0);
        for (int z = 0; z < Terrain.terrainData.size.z; z++)
        {
            //for (int x = 0; x < Terrain.terrainData.size.x; x++)
            //{
                for (int i = 0; i < _directions.Length; i++)
                {
                
                    Quaternion randomRotation = Quaternion.AngleAxis(Random.Range(0, 360), Vector3.up);
                    if (Physics.Raycast(new Ray(location, randomRotation * _directions[i]), out RaycastHit hit, MaxDetectionDistance, DetectionLayer) && hit.collider.TryGetComponent<DetectionPlane>(out _))
                    {
                        for (int t = 0; t < 3; t++)
                        {
                            var detection = _events[triangles[hit.triangleIndex * 3 + t]];
                            if (detection.timesSeen == 0)
                            {
                                detection.FirstDiscovered = Time.time;
                            }
                            detection.LastSeen = Time.time;
                            detection.timesSeen++;
                            detection.SummedDetectionVector += (location - detection.position).normalized * Mathf.Lerp(1f, 0.1f, (location - detection.position).magnitude / MaxDetectionDistance);
                            _events[triangles[hit.triangleIndex * 3 + t]] = detection;
                        }
                    }
                }
            //}
            location += Vector3.forward;
        }
        Debug.Log("finished detecting");
        yield return null;

        VisualizeDetectionData();
    }

    private void VisualizeDetectionData()
    {
        int side = (int)Mathf.Sqrt(_events.Length);
        Debug.Log($"Side length: {side}");
        var bake = new Texture2D(side, side);
        var pixels = new Color32[side * side];
        int mostSeen = 0;
        float largestMagnitude = 0;
        float smallestX = float.MaxValue;
        float largestX = 0;
        float smallestZ = float.MaxValue;
        float largestZ = 0;
        foreach (var e in _events)
        {
            if (e.timesSeen > mostSeen)
                mostSeen = e.timesSeen;
            if (e.SummedDetectionVector.magnitude > largestMagnitude)
                largestMagnitude = e.SummedDetectionVector.magnitude;
            if (e.position.x > largestX) largestX = e.position.x;
            if (e.position.x < smallestX) smallestX = e.position.x;
            if (e.position.z > largestZ) largestZ = e.position.z;
            if (e.position.z < smallestZ) smallestZ = e.position.z;
        }
        Debug.Log($"largestX {largestX} smallestX {smallestX} largestZ {largestZ} smallestZ {smallestZ}");

        for (int i = 0; i < _events.Length; i++)
        {
            var e = _events[i];
            var n = e.SummedDetectionVector.normalized;
            Color color = Color.red;
            if (e.timesSeen > DetectionThreshold)
            {
                var dimmingFactor = ((e.timesSeen / (float)mostSeen) + (e.SummedDetectionVector.magnitude / largestMagnitude))/2;
                color = new Color(n.x, n.y, n.z) * new Color(dimmingFactor, dimmingFactor, dimmingFactor);
            }
            ////Debug.DrawRay(_events[i * 100].position, _events[i * 100].SummedDetectionVector, new Color(n.x, n.y, n.z), 100);
            try
            {
                pixels[(int)Mathf.Clamp(e.position.x - 1, 0, 1000) + ((int)Mathf.Clamp(e.position.z - 1, 0, 1000)) * side] = color;
            } catch {
                Debug.LogError(i);
                Debug.LogError((int)(Mathf.Clamp(e.position.x, 0, 1000) + Mathf.Clamp(e.position.z, 0, 1000) * side));
            }
        }
        bake.SetPixels32(pixels);
        bake.Apply();
        var png = bake.EncodeToPNG();
        var path = Path.Combine(Application.persistentDataPath, $"Detection{DateTime.Now.ToFileTime()}.png");
        Debug.Log($"baking data to {path}");
        File.WriteAllBytes(path, png);
    }

}
