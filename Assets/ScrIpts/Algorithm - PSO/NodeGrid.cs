using UnityEngine;

public class NodeGrid : MonoBehaviour
{
    [Header("Grid Setup")]
    public LayerMask unsearchableMask;
    [Tooltip("The total width (x) and depth (y) of the grid area.")]
    public Vector2 gridSize;
    [Tooltip("Radius of each node (used for Physics checks).")]
    public float nodeRadius;
    [Tooltip("Height offset of the grid from the GameObject's position.")]
    public float gridHeight;

    // The 2D array that holds all node data.
    // NOTE: This array MUST be exposed as public to be accessed by ParticleSwarmAlgorithm.
    public NodeSquare[,] nodes;

    float nodeDiameter;
    int gridSizeX, gridSizeY;
    private void Start()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridSize.y / nodeDiameter);
        CreateNodeGrid();
    }

    void CreateNodeGrid()
    {
        nodes = new NodeSquare[gridSizeX, gridSizeY];


        Vector3 bottomLeftGrid = transform.position 
            - Vector3.right * gridSize.x / 2 
            - Vector3.forward * gridSize.y / 2 
            + Vector3.up * gridHeight;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 gridPoint = bottomLeftGrid 
                    + Vector3.right * (x * nodeDiameter + nodeRadius) 
                    + Vector3.forward * (y * nodeDiameter + nodeRadius);

                bool searchable = !(Physics.CheckSphere(gridPoint, nodeRadius, unsearchableMask));

                nodes[x, y] = new NodeSquare(searchable, false,  gridPoint);
            }
        }
    }

    /// <summary>
    /// Converts a world position (like a drone's current location) into the corresponding NodeSquare object.
    /// </summary>
    public NodeSquare GridFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = ((worldPosition.x - transform.position.x) / gridSize.x) + 0.5f;
        float percentY = ((worldPosition.z - transform.position.z) / gridSize.y) + 0.5f;

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX-1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY-1) * percentY);

        return nodes[x, y];
    }

    // --- Editor Visuals ---
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSize.x, 1, gridSize.y));

        if (nodes != null)
        {
            foreach (NodeSquare node in nodes)
            {
                //Gizmos.color = (node.searchable)?Color.white:(node.searched)?Color.blue:Color.black;
                if (!node.searchable)
                {
                    Gizmos.color = Color.red;
                }
                else if (node.searched)
                {
                    Gizmos.color = Color.green;
                }
                else
                {
                    Gizmos.color = Color.white;
                }

                Gizmos.DrawCube(node.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }
    }
}