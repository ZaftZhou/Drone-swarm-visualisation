using UnityEngine;

public class NodeSquare
{
    public bool searchable;
    public bool searched;
    public Vector3 worldPosition;

    /// <summary>
    /// Used in setting the nodes as searched, for the drones
    /// </summary>
    /// <param name="searchable"></param>
    /// <param name="searched"></param>
    /// <param name="worldPosition"></param>
    public NodeSquare(bool _searchable, bool _searched, Vector3 _worldPosition)
    {
        searchable = _searchable;
        searched = _searched;
        worldPosition = _worldPosition;
    }
}