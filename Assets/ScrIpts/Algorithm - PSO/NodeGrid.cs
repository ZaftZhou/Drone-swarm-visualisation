using UnityEngine;

public class NodeGrid : MonoBehaviour
{
    public LayerMask unsearchableMask;
    public Vector2 gridSize;
    public float nodeRadius;
    NodeSquare[,] nodes;

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
        Vector3 bottomLeftGrid = transform.position - Vector3.right * gridSize.x / 2 - Vector3.forward * gridSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 gridPoint = bottomLeftGrid + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.forward * (y * nodeDiameter + nodeRadius);
                bool searchable = !(Physics.CheckSphere(gridPoint, nodeRadius, unsearchableMask));
                nodes[x, y] = new NodeSquare(searchable, gridPoint);
            }
        }
    }
    public NodeSquare GridFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x / gridSize.x) + 0.5f;
        float percentY = (worldPosition.z / gridSize.y) + 0.5f;
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX-1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY-1) * percentY);
        return nodes[x, y];
    }
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridSize.x, 1, gridSize.y));

        if (nodes != null)
        {
            foreach (NodeSquare node in nodes)
            {
                Gizmos.color = (node.searchable)?Color.white:Color.black;
                Gizmos.DrawCube(node.worldPosition, Vector3.one * (nodeDiameter - 0.1f));
            }
        }
    }
}