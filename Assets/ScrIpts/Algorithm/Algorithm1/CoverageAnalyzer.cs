using System.Collections.Generic;
using UnityEngine;

public class CoverageAnalyzer : MonoBehaviour
{
    [Header("Referrence")]
    public Collider searchArea;
    public List<Drone> drones = new List<Drone>();
    public PartitionedGridAlgorithm algorithm;

    [Header("Grid setting")]
    [Range(1f, 10f)]
    public float gridResolution = 2f;
    [Range(1f, 50f)]
    public float detectionHeight = 30f;
    public bool autoSyncSensorRadius = true;

    [Header("Occlusion Detection")]
    public bool enableOcclusionDetection = true;
    public LayerMask groundLayer = ~0; 
    [Range(0f, 5f)]
    public float raycastOffsetHeight = 2f;

    [Range(0.1f, 2f)]
    public float updateInterval = 0.5f;

    [Header("Visualize")]
    public bool showCoverageGrid = true;
    public bool showStatistics = true;
    public Color coveredColor = new Color(0, 1, 0, 0.3f);
    public Color uncoveredColor = new Color(1, 0, 0, 0.3f);
    public Color occludedColor = new Color(1, 0.5f, 0, 0.3f);
    public bool showOnlyUncovered = false;
    public bool showRaycastDebug = false;

    private enum CellStatus
    {
        Uncovered,      
        Covered,        
        Occluded        
    }

    private CellStatus[,] coverageGrid;
    private Vector3 gridOrigin;
    private int gridWidth;
    private int gridDepth;
    private float nextUpdateTime;
    private int totalCells;
    private int coveredCells;
    private int occludedCells;
    private float actualSensorRadius;

    // 统计
    private float coveragePercentage = 0f;
    private float effectiveCoveragePercentage = 0f;
    private Dictionary<Drone, int> droneCoverageContribution = new Dictionary<Drone, int>();

    // 调试用
    private List<RaycastHit> lastRaycastHits = new List<RaycastHit>();

    void Start()
    {
        InitializeReferences();

        if (searchArea == null)
        {
            Debug.LogError("❌ CoverageAnalyzer: no search area！");
            enabled = false;
            return;
        }

        SyncWithAlgorithm();
        InitializeGrid();

    }

    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            UpdateCoverage();
            nextUpdateTime = Time.time + updateInterval;
        }
    }

    void InitializeReferences()
    {
        if (searchArea == null)
        {
            searchArea = FindFirstObjectByType<BoxCollider>();
        }

        if (drones.Count == 0)
        {
            drones.AddRange(FindObjectsByType<Drone>(FindObjectsSortMode.None));
        }

        if (algorithm == null)
        {
            algorithm = FindFirstObjectByType<PartitionedGridAlgorithm>();
        }
    }

    void SyncWithAlgorithm()
    {
        if (algorithm != null && autoSyncSensorRadius)
        {
            var field = algorithm.GetType().GetField("scanRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                actualSensorRadius = (float)field.GetValue(algorithm);

                // 同步到所有无人机
                foreach (var drone in drones)
                {
                    if (drone != null)
                    {
                        drone.SetSensorRadius(actualSensorRadius);
                    }
                }

            }
        }
        else
        {
            if (drones.Count > 0 && drones[0] != null)
            {
                actualSensorRadius = drones[0].SensorRadius;
            }
        }
    }

    void InitializeGrid()
    {
        Bounds bounds = searchArea.bounds;

        gridWidth = Mathf.CeilToInt(bounds.size.x / gridResolution);
        gridDepth = Mathf.CeilToInt(bounds.size.z / gridResolution);
        totalCells = gridWidth * gridDepth;

        gridOrigin = new Vector3(
            bounds.min.x,
            bounds.min.y+ detectionHeight,
            bounds.min.z
        );

        coverageGrid = new CellStatus[gridWidth, gridDepth];

        if (enableOcclusionDetection)
        {
            PreCheckOcclusion();
        }
        Debug.Log($"📏 Grid Resolution: {gridResolution}m, Total Grids: {totalCells}");
    }

    void PreCheckOcclusion()
    {
        occludedCells = 0;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                Vector3 cellCenter = GridToWorld(x, z);

                Vector3 rayStart = cellCenter + Vector3.up * detectionHeight;

                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit,
                    detectionHeight * 2f, groundLayer))
                {
                      if (hit.collider.CompareTag("Tree") || hit.collider.CompareTag("Obstacle"))
                    {
                        coverageGrid[x, z] = CellStatus.Occluded;
                        occludedCells++;
                    }
                }
            }
        }

        if (occludedCells > 0)
        {
        }
    }

 
    void UpdateCoverage()
    {
        int previousCovered = coveredCells;
        coveredCells = 0;

        foreach (var drone in drones)
        {
            if (!droneCoverageContribution.ContainsKey(drone))
            {
                droneCoverageContribution[drone] = 0;
            }
        }

        lastRaycastHits.Clear();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (coverageGrid[x, z] == CellStatus.Occluded)
                {
                    continue;
                }

                Vector3 cellCenter = GridToWorld(x, z);
                bool wasCovered = coverageGrid[x, z] == CellStatus.Covered;
                bool isCovered = false;

                foreach (var drone in drones)
                {
                    if (drone == null) continue;

                    if (IsCellCoveredByDrone(cellCenter, drone))
                    {
                        isCovered = true;

                        if (!wasCovered)
                        {
                            droneCoverageContribution[drone]++;
                        }
                        break;
                    }
                }

                if (isCovered || wasCovered)
                {
                    coverageGrid[x, z] = CellStatus.Covered;
                    coveredCells++;
                }
            }
        }

        coveragePercentage = (float)coveredCells / totalCells * 100f;

        int validCells = totalCells - occludedCells;
        if (validCells > 0)
        {
            effectiveCoveragePercentage = (float)coveredCells / validCells * 100f;
        }

        int newlyCovered = coveredCells - previousCovered;
        if (newlyCovered > 0 && showStatistics)
        {
            Debug.Log($"📊 New cover grid: {newlyCovered} 格 | Total grids: {coveredCells}/{totalCells} ({coveragePercentage:F1}%) | Effective Coverage Percent: {effectiveCoveragePercentage:F1}%");
        }
    }

    bool IsCellCoveredByDrone(Vector3 cellCenter, Drone drone)
    {
        float heightDiff = Mathf.Abs(drone.Position.y - cellCenter.y);
        if (heightDiff > detectionHeight)
            return false;

        if (!drone.IsPointInSensorRange(cellCenter))
            return false;

        if (enableOcclusionDetection)
        {
            return IsGroundVisible(drone.Position, cellCenter);
        }

        return true;
    }


    bool IsGroundVisible(Vector3 dronePos, Vector3 groundPos)
    {
        Vector3 rayStart = dronePos;
        Vector3 rayTarget = groundPos + Vector3.up * raycastOffsetHeight;
        Vector3 rayDirection = rayTarget - rayStart;
        float rayDistance = rayDirection.magnitude;

        if (Physics.Raycast(rayStart, rayDirection.normalized, out RaycastHit hit,
            rayDistance, groundLayer))
        {
            float hitDistance = Vector3.Distance(hit.point, rayTarget);

            if (showRaycastDebug)
            {
                lastRaycastHits.Add(hit);
            }

            if (hitDistance < gridResolution * 0.5f)
            {
                return true;
            }

            return false;
        }

        return true;
    }

    Vector3 GridToWorld(int x, int z)
    {
        return new Vector3(
            gridOrigin.x + (x + 0.5f) * gridResolution,
            gridOrigin.y,
            gridOrigin.z + (z + 0.5f) * gridResolution
        );
    }

    // ===================================================================
    // public interface
    // ===================================================================

    public float GetCoveragePercentage()
    {
        return coveragePercentage;
    }

    public float GetEffectiveCoveragePercentage()
    {
        return effectiveCoveragePercentage;
    }

    public int GetCoveredCells()
    {
        return coveredCells;
    }

    public int GetTotalCells()
    {
        return totalCells;
    }

    public int GetOccludedCells()
    {
        return occludedCells;
    }

    public bool IsFullyCovered()
    {
        int validCells = totalCells - occludedCells;
        return coveredCells >= validCells;
    }

    public List<Vector3> GetUncoveredAreas()
    {
        List<Vector3> uncovered = new List<Vector3>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (coverageGrid[x, z] == CellStatus.Uncovered)
                {
                    uncovered.Add(GridToWorld(x, z));
                }
            }
        }

        return uncovered;
    }

    public void ResetCoverage()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                if (coverageGrid[x, z] != CellStatus.Occluded)
                {
                    coverageGrid[x, z] = CellStatus.Uncovered;
                }
            }
        }

        coveredCells = 0;
        coveragePercentage = 0f;
        effectiveCoveragePercentage = 0f;
        droneCoverageContribution.Clear();

        Debug.Log("🔄 Coverage reset");
    }


    void OnDrawGizmos()
    {
        if (!showCoverageGrid || coverageGrid == null) return;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridDepth; z++)
            {
                CellStatus status = coverageGrid[x, z];

                if (showOnlyUncovered && status != CellStatus.Uncovered)
                    continue;

                Vector3 cellCenter = GridToWorld(x, z);
                Color color;

                switch (status)
                {
                    case CellStatus.Covered:
                        color = coveredColor;
                        break;
                    case CellStatus.Occluded:
                        color = occludedColor;
                        break;
                    default:
                        color = uncoveredColor;
                        break;
                }

                Gizmos.color = color;
                Vector3 size = new Vector3(gridResolution * 0.9f, 0.1f, gridResolution * 0.9f);
                Gizmos.DrawCube(cellCenter, size);

                Gizmos.color = new Color(color.r, color.g, color.b, 1f);
                Gizmos.DrawWireCube(cellCenter, size);
            }
        }

        if (showRaycastDebug && lastRaycastHits.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var hit in lastRaycastHits)
            {
                Gizmos.DrawWireSphere(hit.point, 0.3f);
            }
        }
    }


    void PrintDetailedStats()
    {
        Debug.Log("=== Detailed statistics ===");
        Debug.Log($"CoveragePercentage: {coveragePercentage:F2}%");

        if (occludedCells > 0)
        {
            Debug.Log($"EffectiveCoveragePercentage: {effectiveCoveragePercentage:F2}%");
            Debug.Log($"OccludedGrids: {occludedCells}");
        }

        Debug.Log($"Grid count: {coveredCells}/{totalCells}");
        Debug.Log($"Grid size: {gridWidth} x {gridDepth}");
        Debug.Log($"Sensor Radius: {actualSensorRadius}m");

        foreach (var kvp in droneCoverageContribution)
        {
            if (kvp.Key != null)
            {
                float contribution = (float)kvp.Value / totalCells * 100f;
                Debug.Log($"  {kvp.Key.name}: {kvp.Value} Grid ({contribution:F1}%)");
            }
        }

        List<Vector3> uncovered = GetUncoveredAreas();
        if (uncovered.Count > 0)
        {
            Debug.Log($"\n⚠️ Still has {uncovered.Count} uncovered region");
            int showCount = Mathf.Min(5, uncovered.Count);
            for (int i = 0; i < showCount; i++)
            {
                Debug.Log($"  Uncover region{i + 1}: {uncovered[i]}");
            }
        }
        else
        {
            Debug.Log("\n✅ Covered all grids");
        }
    }
}