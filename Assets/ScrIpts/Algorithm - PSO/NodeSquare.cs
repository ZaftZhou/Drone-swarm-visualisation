using UnityEngine;

/// <summary>
/// Represents a single unit (node) within the search grid. This class holds the 
/// state required for the coverage simulation aspect of the PSO algorithm.
/// </summary>
public class NodeSquare
{
    /// <summary>
    /// True if the node is clear of obstacles and can be searched by a drone. (Determined at startup)
    /// </summary>
    public bool searchable;

    /// <summary>
    /// True if a drone has successfully visited this node's area. (Updated during simulation)
    /// </summary>
    public bool searched;

    /// <summary>
    /// The world coordinate position of the center of this node.
    /// </summary>
    public Vector3 worldPosition;

    /// <summary>
    /// Constructor for the NodeSquare.
    /// </summary>
    /// <param name="_searchable">Initial state of searchability (based on Physics check).</param>
    /// <param name="_searched">Initial state of coverage (always starts as false).</param>
    /// <param name="_worldPosition">The precise world coordinate for the node center.</param>
    public NodeSquare(bool _searchable, bool _searched, Vector3 _worldPosition)
    {
        searchable = _searchable;
        searched = _searched;
        worldPosition = _worldPosition;
    }
}