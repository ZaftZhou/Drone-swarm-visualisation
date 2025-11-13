using System.Collections.Generic;
using UnityEngine;

public interface IDronePathSampler
{
    public void InitializePaths(int droneCount, Vector3 droneStartingLocation);
    public Vector3 SamplePositionAt(float t, int droneIndex);
}

public class PartitionGridPathGenerator : MonoBehaviour, IDronePathSampler
{
    [SerializeField]
    [Range(0.1f, 3f)]
    private float scanDensityMultiplier = 1f;
    [SerializeField] 
    private float scanRadius = 10f;
    [SerializeField]
    [Range(0.0f, 0.5f)]
    private float scanOverlap = 0.2f;
    [SerializeField] private float flightHeight = 20f;
    [SerializeField] 
    private Bounds _searchBounds;
    [SerializeField] 
    private bool _addEdgeScans;
    [SerializeField] 
    private Terrain _terrain;

    private Bounds[] _dronePartitions;
    private List<Vector3>[] _dronePaths;
    private List<float>[] _cumulativePathLengthLists;
    private float[] _totalPathLengths;
    private int _droneCount;
    private Vector3 _droneStartingLocation;



    public void InitializePaths(int droneCount, Vector3 droneStartingLocation)
    {
        _droneCount = droneCount;
        _dronePartitions = new Bounds[_droneCount];
        _dronePaths = new List<Vector3>[_droneCount];
        _cumulativePathLengthLists = new List<float>[_droneCount];
        _totalPathLengths = new float[_droneCount];

        CalculatePartitions();
        GenerateDronePaths();
        CalculatePathLengths();
    }

    private void CalculatePartitions()
    {
        float totalWidth = _searchBounds.size.x;
        float sliceWidth = totalWidth / _droneCount;
        float startX = _searchBounds.min.x;

        for (int i = 0; i < _droneCount; i++)
        {
            float partitionMinX = startX + (i * sliceWidth);
            //float partitionMaxX = partitionMinX + sliceWidth;

            Vector3 partitionCenter = new(
                partitionMinX + (sliceWidth / 2f),
                _searchBounds.center.y,
                _searchBounds.center.z
            );

            Vector3 partitionSize = new(
                sliceWidth,
                _searchBounds.size.y,
                _searchBounds.size.z
            );

            Bounds partition = new(partitionCenter, partitionSize);
            _dronePartitions[i] = partition;
        }
    }

    private void GenerateDronePaths()
    {

        for (int i = 0; i < _droneCount; i++)
        {
            _dronePaths[i] = GenerateHorizontalPattern(i);
        }
    }

    private void CalculatePathLengths()
    {
        for (int droneIndex = 0; droneIndex < _droneCount; droneIndex++)
        {
            float totalLength = 0f;
            List<float> cumulativeLengths = new() { 0f }; // 第一个点的累积长度为0
            var path = _dronePaths[droneIndex];
            for (int i = 0; i < path.Count - 1; i++)
            {
                float segmentLength = Vector3.Distance(path[i], path[i + 1]);
                totalLength += segmentLength;
                cumulativeLengths.Add(totalLength);
            }
            _cumulativePathLengthLists[droneIndex] = cumulativeLengths;
            _totalPathLengths[droneIndex] = totalLength;
        }
    }

    private List<Vector3> GenerateHorizontalPattern(int droneIndex)
    {
        List<Vector3> points = new ();
        Bounds partition = _dronePartitions[droneIndex];
        float xMin = partition.min.x;
        float xMax = partition.max.x;
        float zMin = _searchBounds.min.z;
        float zMax = _searchBounds.max.z;
        float y = flightHeight;

        float effectiveScanWidth = scanRadius * 2 * (1.0f - scanOverlap); //do these need to change?
        float zStep = effectiveScanWidth * scanDensityMultiplier;

        if (zStep <= 0.01f) zStep = 0.01f;
        points.Add(_droneStartingLocation);
        bool scanForward = true;

        if (_addEdgeScans)
        {
            points.Add(new Vector3(xMin, y, zMin));
            points.Add(new Vector3(xMax, y, zMin));
        }

        for (float z = zMin; z <= zMax; z += zStep)
        {
            if (scanForward)
            {
                points.Add(new Vector3(xMin, _terrain.SampleHeight(new Vector3(xMin, 200, z)) + flightHeight, z)); //these might be shit
                points.Add(new Vector3(xMax, _terrain.SampleHeight(new Vector3(xMax, 200, z)) + flightHeight, z));
            }
            else
            {
                points.Add(new Vector3(xMax, _terrain.SampleHeight(new Vector3(xMax, 200, z)) + flightHeight, z));
                points.Add(new Vector3(xMin, _terrain.SampleHeight(new Vector3(xMin, 200, z)) + flightHeight, z));
            }

            scanForward = !scanForward;
        }

        if (_addEdgeScans && points.Count > 0)
        {
            Vector3 lastPoint = points[points.Count - 1];
            if (Mathf.Abs(lastPoint.z - zMax) > 0.1f)
            {
                points.Add(new Vector3(lastPoint.x, y, zMax));
                points.Add(new Vector3(lastPoint.x == xMin ? xMax : xMin, y, zMax));
            }
        }
        return points;
    }


    private static List<Vector3> OptimizePath(List<Vector3> originalPath, Vector3 startPosition)
    {
        if (originalPath.Count == 0) return originalPath;

        int closestIndex = 0;
        float minDistance = Vector3.Distance(startPosition, originalPath[0]);

        for (int i = 1; i < originalPath.Count; i++)
        {
            float dist = Vector3.Distance(startPosition, originalPath[i]);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestIndex = i;
            }
        }

        if (closestIndex > 0)
        {
            List<Vector3> optimized = new List<Vector3>();
            for (int i = closestIndex; i < originalPath.Count; i++)
            {
                optimized.Add(originalPath[i]);
            }
            for (int i = 0; i < closestIndex; i++)
            {
                optimized.Add(originalPath[i]);
            }
            return optimized;
        }
        return originalPath;
    }


    public Vector3 SamplePositionAt(float t, int droneIndex)
    {
        t = Mathf.Clamp01(t);

        List<Vector3> path = _dronePaths[droneIndex];
        if (path.Count == 1 || t == 0f)
        {
            return path[0];
        }
        if (t == 1f)
        {
            return path[path.Count - 1];
        }

        float totalLength = _totalPathLengths[droneIndex];
        var cumulativeLengths = _cumulativePathLengthLists[droneIndex];

        float targetDistance = totalLength * t;

        for (int i = 0; i < cumulativeLengths.Count - 1; i++)
        {
            if (targetDistance >= cumulativeLengths[i] && targetDistance <= cumulativeLengths[i + 1])
            {
                Vector3 startPoint = path[i];
                Vector3 endPoint = path[i + 1];

                float segmentStartDist = cumulativeLengths[i];
                float segmentEndDist = cumulativeLengths[i + 1];
                float segmentLength = segmentEndDist - segmentStartDist;


                float segmentT = 0f;
                if (segmentLength > 0.001f)
                {
                    segmentT = (targetDistance - segmentStartDist) / segmentLength;
                }
                var position = Vector3.Lerp(startPoint, endPoint, segmentT);
                return new Vector3(position.x, _terrain.SampleHeight(position) + flightHeight, position.z);
            }
        }
        return path[path.Count - 1]; 
    }
}
