using System.Collections;
using UnityEngine;

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
    public TerrainCollider TerrainCollider;
    public Terrain Terrain;
    public MeshFilter DetectionMeshFilter;
    public float MaxDetectionDistance = 500;
    public LayerMask DetectionLayer;
    private MeshCollider _collider;
    private DetectionEvent[] _events;
    private readonly Vector3[] directions = new Vector3[9];

    private void Awake()
    {
        var thing = new Vector3(1, -1, 0);
        for (int i = 0; i < 8; i++)
        {
            directions[i] = thing;
            thing = Quaternion.AngleAxis(360.0f / 8.0f, Vector3.up) * thing;
        }
        directions[8] = Vector3.down;
        var mesh = DetectionMeshFilter.mesh;
        Debug.Log($"verts  {mesh.vertices.Length}, tris {mesh.triangles.Length}");
        var vertices = mesh.vertices;
        var terrainData = Terrain.terrainData;

        for (int i = 0; i < vertices.Length; i++)
        {
            float height = terrainData.GetInterpolatedHeight((vertices[i].x + DetectionMeshFilter.transform.position.x) / terrainData.size.x, (vertices[i].z + DetectionMeshFilter.transform.position.z) / terrainData.size.z);
            vertices[i] = new Vector3(vertices[i].x, height, vertices[i].z);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        DetectionMeshFilter.GetComponent<MeshCollider>().sharedMesh = mesh;
        //DetectionMeshFilter.mesh = mesh;
        _events = new DetectionEvent[mesh.triangles.Length / 3];
    }

    private void Start()
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
            var p1 = vertices[triangles[i * 3]];
            var p2 = vertices[triangles[i * 3 + 1]];
            var p3 = vertices[triangles[i * 3 + 2]];
            detection.position = DetectionMeshFilter.transform.TransformPoint(new Vector3((p1.x + p2.x + p3.x) / 3, (p1.y + p2.y + p3.y) / 3, (p1.z + p2.z + p3.z) / 3));
            _events[i] = detection;
        }
        Debug.Log("finished setting positions");
        yield return null;

        Vector3 location = new(0, 400, 0);
        for (int z = 0; z < Terrain.terrainData.size.z; z++)
        {
            for (int x = 0; x < Terrain.terrainData.size.x; x++)
            {
                for (int i = 0; i < directions.Length; i++)
                {
                    if (Physics.Raycast(new Ray(location, directions[i]), out RaycastHit hit, MaxDetectionDistance, DetectionLayer))
                    {
                        var detection = _events[hit.triangleIndex];
                        if (detection.timesSeen == 0)
                        {
                            detection.FirstDiscovered = Time.time;
                        }
                        detection.LastSeen = Time.time;
                        detection.timesSeen++;
                        detection.SummedDetectionVector += (location - detection.position).normalized;
                        _events[hit.triangleIndex] = detection;
                    }
                }

                location += Vector3.right;
            }
            //yield return null;
            location.x = 0;
            location += Vector3.forward;
        }
        Debug.Log("finished detecting");
        yield return null;

        VisualizeDetectionData();
    }

    private void VisualizeDetectionData()
    {

        for (int i = 0; i < 4000; i++)
        {
            var n = _events[i * 300].SummedDetectionVector.normalized;
            Debug.DrawRay(_events[i*300].position, _events[i*300].SummedDetectionVector, new Color(n.x, n.y, n.z), 100);
        }

    }

    void Update()
    {

    }
}
