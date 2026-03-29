using System.Collections;
using System.Collections.Generic;
using TMPro;
using TreeEditor;
using Unity.Entities.UniversalDelegates;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

public class OctreeTreeNode
{
    public Bounds Bounds { get; private set; }
    public NodeType Type { get; private set; }

    public OctreeTreeNode Parent { get; private set; }
    public OctreeTreeNode[] Children { get; set; }

    public Vector4[] CornerDistances = new Vector4[8];
    public int CurrentLevel;

    public OctreeTreeNode(Bounds bounds, NodeType type, OctreeTreeNode parent)
    {
        Bounds = bounds;
        Type = type;
        Children = null;
        Parent = parent;
    }

    // Check if this node is a leaf (has no children)
    public bool IsLeaf()
    {
        return Children == null;
    }

    public void CheckSubdivision(int currentDepth, int maxDepth, Texture2D heightmap, float maxHeight,Dictionary<Vector3Int,OctreeTreeNode> lookup)
    {

        CurrentLevel = currentDepth;


        // Determine the node type before subdivision
        if (Type == NodeType.Undefined)
        {

        }
        Type = DetermineNodeType(heightmap, maxHeight);

        // Stop if the maximum depth is reached
        if (currentDepth >= maxDepth) return;

        //calc dist between node and playerpos
        Vector3 playerpos = OctreeTest.playerPos;
        float distanceToPlayer = Vector3.Distance(playerpos, Bounds.center);
        Vector3Int pos = new Vector3Int(Mathf.RoundToInt(Bounds.center.x), Mathf.RoundToInt(Bounds.center.y), Mathf.RoundToInt(Bounds.center.z));
        lookup.Add(pos, this);
        // Debug.Log(Type);



        // Calculate the maximum allowable distance for this depth
        //   float minDistance = 32f; // Base minimum distance for highest resolution
        //  float maxDistance = minDistance * Mathf.Pow(2, currentDepth);
        float test = RoundDistanceToLevel(distanceToPlayer);
        int level = CalcProperLevel(test);

        //add node to dictionary

        // Debug.Log($"min dist: {minDistance} max dist: {maxDistance} dist toplayer: {test} depth: {currentDepth} test:{level}");



        // If the node is mixed or near player
        if (Type == NodeType.Mixed)
        {
            if (currentDepth < level)
            {
                // Subdivide this node
                Subdivide(heightmap, maxHeight);

                // Recursively check subdivision for children
                foreach (var child in Children)
                {
                    child.CheckSubdivision(currentDepth + 1, maxDepth, heightmap, maxHeight,lookup);
                }
            }



        }

        return;






    }

    // Subdivide this node into 8 children
    private void Subdivide(Texture2D heightmap, float maxHeight)
    {
        Vector3 size = Bounds.size / 2f; // Half size for subdivision
        Vector3 center = Bounds.center;
        float quarter = Bounds.size.x / 4.0f;

        // var type = DetermineNodeType(heightmap, maxHeight);

        // Define positions for each octant
        Vector3[] childOffsets = new Vector3[]
        {
        new Vector3(-quarter, quarter, -quarter),
        new Vector3(quarter, quarter, -quarter),
        new Vector3(-quarter, quarter, quarter),
        new Vector3(quarter, quarter, quarter),
        new Vector3(-quarter, -quarter, -quarter),
        new Vector3(quarter, -quarter, -quarter),
        new Vector3(-quarter, -quarter, quarter),
        new Vector3(quarter, -quarter, quarter)
        };

        Children = new OctreeTreeNode[8];

        for (int i = 0; i < 8; i++)
        {
            Vector3 childCenter = center + childOffsets[i];
            Bounds childBounds = new Bounds(childCenter, size);

            // Create child with correct position
            Children[i] = new OctreeTreeNode(childBounds, NodeType.Empty, this);

            // Determine its type after creation
            Children[i].Type = DetermineNodeType(heightmap, maxHeight);
        }

        //Debug.Log(this.Bounds.size.x);
        //Children[0] = new OctreeTreeNode(new Bounds(center + new Vector3(-quarter, quarter, -quarter), size), type, this);
        //Children[1] = new OctreeTreeNode(new Bounds(center + new Vector3(quarter, quarter, -quarter), size), type, this);
        //Children[2] = new OctreeTreeNode(new Bounds(center + new Vector3(-quarter, quarter, quarter), size), type, this);
        //Children[3] = new OctreeTreeNode(new Bounds(center + new Vector3(quarter, quarter, quarter), size), type, this);
        //Children[4] = new OctreeTreeNode(new Bounds(center + new Vector3(-quarter, -quarter, -quarter), size), type, this);
        //Children[5] = new OctreeTreeNode(new Bounds(center + new Vector3(quarter, -quarter, -quarter), size), type, this);
        //Children[6] = new OctreeTreeNode(new Bounds(center + new Vector3(-quarter, -quarter, quarter), size), type, this);
        //Children[7] = new OctreeTreeNode(new Bounds(center + new Vector3(quarter, -quarter, quarter), size), type, this);

        // Debug.Log($"quarter: {quarter} center: {center} half size: {size}");
    }

    // Determine the node type based on sampled height
    public NodeType DetermineNodeType(Texture2D heightmap, float maxHeight)
    {
        // Get the bounds of the node
        Vector3 min = Bounds.min;
        Vector3 max = Bounds.max;

        // Define unique corner positions
        Vector3[] corners = new Vector3[8];
        corners[0] = new Vector3(min.x, min.y, min.z);
        corners[1] = new Vector3(max.x, min.y, min.z);
        corners[2] = new Vector3(min.x, min.y, max.z);
        corners[3] = new Vector3(max.x, min.y, max.z);
        corners[4] = new Vector3(min.x, max.y, min.z);
        corners[5] = new Vector3(max.x, max.y, min.z);
        corners[6] = new Vector3(min.x, max.y, max.z);
        corners[7] = new Vector3(max.x, max.y, max.z);

        // Sample heights and store in CornerDistances
        for (int i = 0; i < corners.Length; i++)
        {
            // Sample height from the heightmap
            float sampleHeight = SampleHeight(heightmap, corners[i].x, corners[i].z, maxHeight);

            // Compute signed distance (height relative to corner's y position)
            float distance = sampleHeight - corners[i].y;

            // Store as a Vector4 (x, y, z, distance)
            CornerDistances[i] = new Vector4(corners[i].x, corners[i].y, corners[i].z, distance);
        }

        // Check if all heights are below the bounds
        bool allBelow = true;
        bool allAbove = true;

        foreach (Vector4 corner in CornerDistances)
        {
            if (corner.w >= 0) allBelow = false; // Corner is above the surface
            if (corner.w <= 0) allAbove = false; // Corner is below the surface

            // Early exit if the node is mixed
            if (!allBelow && !allAbove) return NodeType.Mixed;
        }

        // If all are below, it's empty; if all are above, it's solid
        return allBelow ? NodeType.Empty : NodeType.Solid;
    }

    private float SampleHeight(Texture2D heightmap, float x, float y, float maxHeight)
    {

        int X = Mathf.FloorToInt(x);
        int Y = Mathf.FloorToInt(y);

        // Sample the texture
        Color pixel = heightmap.GetPixel(X, Y);
        return pixel.r * maxHeight;
    }


    //public OctreeTreeNode FindNeighbor(OctreeTreeNode node, Vector3 direction, OctreeTreeNode root)
    //{
    //    // Compute the bounds of the expected neighbor
    //    Bounds currentBounds = node.Bounds;
    //    float size = currentBounds.size.x;
    //    //  Debug.Log(size);
    //    Bounds neighborBounds = new Bounds(currentBounds.center + direction * size, currentBounds.size);




    //    //// Start the search from the parent node (or root of the tree)
    //    //return FindNeighborByPosition(root, neighborBounds.center, node.CurrentLevel);
        

    //    // Try finding a direct neighbor at the same level
    //    OctreeTreeNode neighbor = FindNeighborByPosition(root, neighborBounds.center, node.CurrentLevel);

    //    if (neighbor == null)
    //    {
    //        // If no same-level neighbor, check finer resolution (children of a nearby coarser node)
    //        neighbor = FindNeighborAtFinerLevel(node, direction);
    //    }

    //    if (neighbor == null)
    //    {
    //        // If still no valid neighbor, move up and check coarser levels (parent level)
    //        neighbor = FindNeighborAtCoarserLevel(node, direction);
    //    }

    //    return neighbor;

    //}

    //// 🔹 Check finer resolution first: search a neighbor's children
    //public OctreeTreeNode FindNeighborAtFinerLevel(OctreeTreeNode node, Vector3 direction)
    //{
    //    if (node.Parent == null) return null; // No parent means no finer-level search possible

    //    // Get potential coarse-level neighbor first
    //    OctreeTreeNode parentNeighbor = FindNeighbor(node.Parent, direction, node.Parent);

    //    if (parentNeighbor == null || parentNeighbor.Children == null) return null;

    //    // Search within that neighbor's children for a more refined match
    //    foreach (OctreeTreeNode child in parentNeighbor.Children)
    //    {
    //        if (child.Bounds.Intersects(node.Bounds))
    //        {
    //            return child; // Found a finer-level match
    //        }
    //    }

    //    return null; // No fine match found
    //}

    //// 🔹 If no fine-level match, move up to a coarser level and try again
    //private OctreeTreeNode FindNeighborAtCoarserLevel(OctreeTreeNode node, Vector3 direction)
    //{
    //    if (node.Parent == null) return null;

    //    return FindNeighbor(node.Parent, direction, node.Parent);
    //}

    //public OctreeTreeNode FindNeighborByPosition(OctreeTreeNode startNode, Vector3 targetPosition, int targetLevel)
    //{
        
    //    //return null; // Neighbor not found
    //    OctreeTreeNode currentNode = startNode;
    //    int maxDepth = 12; // Safety limit to avoid infinite loops

    //    for (int i = 0; i < maxDepth; i++)
    //    {
    //        if (currentNode == null) return null;

    //        // ✅ If we found a matching node at the correct level
    //        if (currentNode.CurrentLevel == targetLevel && currentNode.Bounds.Contains(targetPosition))
    //        {
    //            return currentNode;
    //        }

    //        // 🔹 If there are children, traverse downwards
    //        if (currentNode.Children != null)
    //        {
    //            foreach (OctreeTreeNode child in currentNode.Children)
    //            {
    //                if (child.Bounds.Contains(targetPosition))
    //                {
    //                    currentNode = child;
    //                    break; // Continue traversal with the matching child
    //                }
    //            }
    //        }
    //        else
    //        {
    //            // 🔺 Move up the tree if no match is found
    //            currentNode = currentNode.Parent;
    //        }
    //    }

    //    return null;


    //}

    private float RoundDistanceToLevel(float distance)
    {
        // Define the allowable distance levels
        float[] levels = { 16, 32, 64, 128, 512, 1024, 2048 };

        // Find the closest value by minimizing the absolute difference
        float closest = levels[0];
        foreach (float level in levels)
        {
            if (Mathf.Abs(distance - level) < Mathf.Abs(distance - closest))
            {
                closest = level;
            }
        }

        return closest;
    }

    private int CalcProperLevel(float distance)
    {
        OctreeTest.NodeLevels.TryGetValue((int)distance, out int level);



        return level;
    }
}
// Enum to represent the type of terrain in a node
public enum NodeType
{
    Solid, // Fully solid
    Empty, // Fully empty
    Mixed,  // Contains both solid and empty regions
    Undefined // Node not set yet
}