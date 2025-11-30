using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Profiling;
using UnityEngine.TextCore;
using static UnityEditor.Progress;





public class Octree
{
    // Root node of the quadtree
    public OctreeTreeNode Root { get; private set; }

    // Maximum depth of the octree
    public int MaxDepth { get; private set; }
    private Texture2D heightmap; // The heightmap to sample from
    private float maxHeight; // Maximum height of the terrain
    List<VertexDebug> vertexDebuggerlist = new List<VertexDebug>();
    List<Vector3> DebugVertices = new List<Vector3>();
    Dictionary<Vector3Int, OctreeTreeNode> nodeLookup = new Dictionary<Vector3Int, OctreeTreeNode>();
    //debug list
    List<OctreeTreeNode> neighbors = new List<OctreeTreeNode>();

    // Constructor to initialize the octree
    public Octree(Bounds bounds, int maxDepth, Texture2D heightmap, float maxHeight)
    {
        Root = new OctreeTreeNode(bounds, NodeType.Undefined, null);
        MaxDepth = maxDepth;
        this.heightmap = heightmap;
        this.maxHeight = maxHeight;
    }

    // Subdivide a node into 4 children
    public void Subdivide(OctreeTreeNode node)
    {
        Root.CheckSubdivision(0, MaxDepth, heightmap, maxHeight, nodeLookup);

    }

    //returns a list of nodes at the desired level
    public List<OctreeTreeNode> GetNodesAtLevel(int desiredLevel)
    {
        List<OctreeTreeNode> nodes = new List<OctreeTreeNode>();
        TraverseNodes(this.Root, 0, desiredLevel, nodes);

        return nodes;
    }

    public void GetNodeWithinChunk(OctreeTreeNode node, Bounds chunkBounds, int chunkSize, List<OctreeTreeNode> result, List<OctreeTreeNode> neighbors)
    {
        // OctreeTreeNode currentNode = null;

        //early exit if node is spacially out of bounds
        if (!node.Bounds.Intersects(chunkBounds))
        {
            return;
        }

        //valid node found
        if (node.Bounds.size.x <= chunkSize)
        {
            if (chunkBounds.Contains(node.Bounds.center))
            {
                result.Add(node);
            }
            else
            {
                //found neighbor. iterating nearest neighborchild & adds to neighborlist
                if (node.Children != null)
                {
                    if (node.Bounds.center.x >= chunkBounds.center.x && node.Bounds.center.z >= chunkBounds.center.z)
                    {
                        //foreach (OctreeTreeNode child in node.Children)
                        //{
                        //    if (chunkBounds.Intersects(child.Bounds) && child.IsLeaf())
                        //    {

                        //        neighbors.Add(child);


                        //    }
                        //}
                        CollectIntersectingLeafNeighbors(node,chunkBounds,neighbors);
                    }

                   
                }



            }

            return;
        }

        if (node.Children != null)
        {

            foreach (OctreeTreeNode child in node.Children)
            {
                if (child != null)
                {
                    chunkBounds.center = new Vector3(chunkBounds.center.x, child.Bounds.center.y, chunkBounds.center.z);
                    GetNodeWithinChunk(child, chunkBounds, chunkSize, result, neighbors);

                }
            }
        }

    }

    void CollectIntersectingLeafNeighbors(OctreeTreeNode node, Bounds chunkBounds, List<OctreeTreeNode> neighbors)
    {
        if (!node.Bounds.Intersects(chunkBounds))
            return;

        if (node.IsLeaf())
        {
            neighbors.Add(node);
            return;
        }

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (child != null)
                {
                    CollectIntersectingLeafNeighbors(child, chunkBounds, neighbors);
                }
            }
        }
    }

    public OctreeTreeNode GetNodeAt(Vector3Int position)
    {
        if (nodeLookup.TryGetValue(position, out OctreeTreeNode foundNode))
        {
            return foundNode;
        }
        else
        {
            return null;
        }
    }

    //recursively traverses the octree and returns the nodes at specific level
    private void TraverseNodes(OctreeTreeNode node, int currentLevel, int desiredLevel, List<OctreeTreeNode> result)
    {

        if (currentLevel == desiredLevel)
        {
            result.Add(node);

            return;
        }

        if (node.Children == null) return;

        foreach (var child in node.Children)
        {
            TraverseNodes(child, currentLevel + 1, desiredLevel, result);
        }
    }





    public void DrawBounds(OctreeTreeNode node, int depth, int excludeBounds, int maxBounds)
    {
        if (node == null) return;





        // Draw this node's bounds
        Vector3 center = new Vector3(node.Bounds.center.x, node.Bounds.center.y, node.Bounds.center.z);
        Vector3 size = new Vector3(node.Bounds.size.x, node.Bounds.size.z, node.Bounds.size.y);
        if (depth >= excludeBounds && depth <= maxBounds)
        {
            // Gizmos.color = Color.Lerp(Color.green, Color.red, (float)depth / MaxDepth);
            if (node.Type == NodeType.Mixed || node.Type == NodeType.Empty)
            {
                //Gizmos.color = Color.blue;
            }
            else if (node.Type == NodeType.Solid)
            {

                // Gizmos.color = Color.red;
            }

            Gizmos.color = Color.blue;
            if (/*node.Type == NodeType.Mixed &&*/ size.x <= 64)
            {
                Gizmos.DrawWireCube(center, size);

            }

        }
        // Recursively draw children
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {

                DrawBounds(child, depth + 1, excludeBounds, maxBounds);


            }
        }
    }

    public void DrawVertices(float debugSize)
    {
        if (DebugVertices.Count == 0)
        {
            return;
        }

        foreach (var vertex in DebugVertices)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(vertex, debugSize / 2);


        }


    }

    public void DrawDebugBounds(float debugSize)
    {
        if (vertexDebuggerlist.Count == 0)
        {
            return;
        }



        for (int i = 0; i < vertexDebuggerlist.Count; i++)
        {
            var vertex = vertexDebuggerlist[i].vertex;
            Vector3 node = vertexDebuggerlist[i].NodePos;
            float size = vertexDebuggerlist[i].NodeSize;
            Color color = vertexDebuggerlist[i].Color;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(vertex, debugSize / 2);

            Gizmos.color = color;
            Gizmos.DrawWireCube(node, new Vector3(debugSize, debugSize, debugSize));

            if (i == 5)
            {
                // return;
            }
        }
    }

    public void DrawNeighbors()
    {
        UnityEngine.Color color = UnityEngine.Color.green;

        foreach (var neighbor in neighbors)
        {
            if (neighbor != null)
            {
                Vector3 center = new Vector3(neighbor.Bounds.center.x, neighbor.Bounds.center.y, neighbor.Bounds.center.z);
                Vector3 size = new Vector3(neighbor.Bounds.size.x, neighbor.Bounds.size.z, neighbor.Bounds.size.y);
                Gizmos.color = color;
                Gizmos.DrawWireCube(center, size);

            }

        }
    }

    public void GenerateMesh()
    {

        List<OctreeTreeNode> nodes = new List<OctreeTreeNode>();
        Dictionary<OctreeTreeNode, int> VerticeMap = new Dictionary<OctreeTreeNode, int>();
        Dictionary<Vector3, int> VerticIndexMap = new Dictionary<Vector3, int>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        List<OctreeTreeNode> Validneighbors = new List<OctreeTreeNode>();

        // Face directions and their corresponding normals
        Vector3[] faceDirections = new Vector3[]
        {
    new Vector3(0, 0, 1),  // +Z face
    new Vector3(-1, 0, 0), // -X face
    new Vector3(0, 0, -1), // -Z face
    new Vector3(1, 0, 0),  // +X face
    //new Vector3(0, 1, 0), // +y face
    //new Vector3(0, -1, 0), // -y face
            
        };

        // Diagonal positions (same horizontal axis, y stays constant) for each face, ordered clockwise
        Vector3[] faceDiagonals = new Vector3[]
        {
        new Vector3(1, 0, 1),    // Diagonal for +Z face
        new Vector3(0, 0, 1),   // Diagonal for -X face
        new Vector3(-1, 0, -1),  // Diagonal for -Z face
        new Vector3(1, 0, 0),   // Diagonal for +X face
        new Vector3(1, 1, 1), // Diagonal for +y face
        new Vector3(-1, -1, 1), // Diagonal for -y face
        };




        //traverse octree
        int minLevel = 4;
        Profiler.BeginSample("debug_vertices");
        GenerateVertices(this.Root, minLevel, vertices, indices, VerticIndexMap, VerticeMap);
        Profiler.EndSample();
        //get nodes at desired level
        //for (int level = MaxDepth; level <= MaxDepth; level++)
        //{
        //    var currentnodes = GetNodesAtLevel(level);
        //    Debug.Log($"level: {level} node level: {currentnodes[0].CurrentLevel} size: {currentnodes[0].Bounds.size.x}");
        //    nodes.AddRange(currentnodes);

        //}

        //check if nodes are intersecting 
        //foreach (var node in nodes)
        //{


        //    if (node.Type == NodeType.Mixed)
        //    {
        //        //calculate qef vertices
        //        Vector3 vertex = CalculateWeightedVertex(node);
        //        // Debug.Log(vertex);


        //        //add debug vertices 
        //        float dist = Vector3.Distance(vertex, new Vector3(863, 1243, 906));
        //        if (dist <= 32)
        //        {
        //            DebugVertices.Add(vertex);

        //        }

        //        // If the vertex already exists, skip adding it
        //        if (VerticIndexMap.TryGetValue(vertex, out int existingIndex))
        //        {
        //            VerticeMap[node] = existingIndex;
        //            continue;
        //        }


        //        // Check for an existing vertex in neighboring nodes (up/down)
        //        var up = node.FindNeighbor(node, Vector3.up, Root);
        //        var down = up == null ? node.FindNeighbor(node, Vector3.down, Root) : null;

        //        if (up != null && VerticeMap.TryGetValue(up, out int upIndex))
        //        {
        //            VerticeMap[node] = upIndex;
        //            VerticIndexMap[vertex] = upIndex;
        //            continue; // We found an existing vertex, no need to add a new one
        //        }
        //        else if (down != null && VerticeMap.TryGetValue(down, out int downIndex))
        //        {
        //            VerticeMap[node] = downIndex;
        //            VerticIndexMap[vertex] = downIndex;
        //            continue;
        //        }

        //        // If no existing vertex is found, add a new one
        //        int newIndex = vertices.Count;
        //        vertices.Add(vertex);
        //        VerticeMap[node] = newIndex;
        //        VerticIndexMap[vertex] = newIndex;

        //    }
        //}

        // Generate quads (pass 2)
        Profiler.BeginSample("debug_triangulation");
        GenerateIndices(vertices, indices, VerticIndexMap, VerticeMap);
        Profiler.EndSample();
        //int counter = 0;
        //foreach (var node in nodes)
        //{


        //    if (node.Type == NodeType.Mixed)
        //    {
        //        //testing a single node for debugging
        //        if (!VerticeMap.ContainsKey(node))
        //        {
        //            continue;
        //        }

        //        // neighbors.Add(node);
        //        // Debug.Log("origin node: " + node.Bounds.center);
        //        // For each face direction
        //        for (int faceindex = 0; faceindex < faceDirections.Length; faceindex++)
        //        {


        //            // Find the four nodes sharing the face
        //            OctreeTreeNode neighbor = node.FindNeighbor(node, faceDirections[faceindex], Root);
        //            if (neighbor != null)
        //            {
        //                if (!VerticeMap.ContainsKey(neighbor))
        //                {
        //                    //  Debug.Log($"neighbor not found: {neighbor}");
        //                    neighbor = FindAdjacentNeighbor(neighbor, VerticeMap, faceDirections[faceindex], node);
        //                    if (neighbor == null)
        //                    {
        //                        // Debug.LogError("neighbor not found!");
        //                        var vertex = vertices[VerticeMap[node]];
        //                        vertexDebuggerlist.Add(new VertexDebug(vertex, node.Bounds.center, 16));
        //                        //  continue;
        //                    }
        //                }


        //            }

        //            //check diagonal neighbor
        //            if (neighbor != null && VerticeMap.ContainsKey(neighbor))
        //            {
        //                Vector3 corner = faceDiagonals[faceindex];


        //                float size = neighbor.Bounds.size.x;
        //                //  Debug.Log($"neighbor exist: {neighbor.Bounds.center + corner * size} face: {faceindex}");
        //                OctreeTreeNode diagonalNeighbor = node.FindNeighbor(node, corner, Root);

        //                //check if diagonal neighbor is valid
        //                if (diagonalNeighbor != null)
        //                {
        //                    if (!VerticeMap.ContainsKey(diagonalNeighbor))
        //                    {
        //                        // Debug.Log($"neighbor not found: {neighbor}");
        //                        diagonalNeighbor = FindAdjacentNeighbor(diagonalNeighbor, VerticeMap, corner, node);
        //                        if (diagonalNeighbor == null)
        //                        {
        //                            //   Debug.LogError("diagonal neighbor not found!");
        //                            var vertex = vertices[VerticeMap[node]];
        //                            vertexDebuggerlist.Add(new VertexDebug(vertex, node.Bounds.center, 16));

        //                            //  continue;
        //                        }
        //                    }
        //                }



        //                //  Debug.Log($"face index: {faceindex} ");
        //                // Add connections if the neighbor exists
        //                if (diagonalNeighbor != null && VerticeMap.ContainsKey(diagonalNeighbor))
        //                {
        //                    // Debug.Log($"diag neighbor: {diagonalNeighbor} corner: {node.Bounds.center + corner * node.Bounds.size.x} face: {faceindex} level: {level}");
        //                    //  neighbors.Add(diagonalNeighbor);
        //                    try
        //                    {
        //                        int v0 = VerticeMap[node]; // Current node's vertex
        //                        int v1 = VerticeMap[neighbor]; // Face neighbor's vertex
        //                        int v2 = VerticeMap[diagonalNeighbor]; // Diagonal neighbor's vertex


        //                        indices.Add(v1);
        //                        indices.Add(v2);
        //                        indices.Add(v0);

        //                        if (faceindex < 4)
        //                        {

        //                        }
        //                        else
        //                        {
        //                            //  indices.Add(v0);
        //                            //  indices.Add(v2);
        //                            //  indices.Add(v1);
        //                            //globalvertices.Add(node.Bounds.center);
        //                            //globalvertices.Add(neighbor.Bounds.center);
        //                            //globalvertices.Add(diagonalNeighbor.Bounds.center);
        //                        }



        //                        //  Debug.Log($"doing node: {node.Bounds.center} neighbor: {neighbor.Bounds.center} diagonal: {diagonalNeighbor.Bounds.center}" );

        //                    }
        //                    catch (System.Exception e)
        //                    {

        //                        Debug.LogWarning(e.Message);

        //                    }

        //                }

        //            }

        //        }
        //        counter++;
        //        //if (counter == 30)
        //        //{
        //        //    break;
        //        //}
        //    }

        //}
        Profiler.BeginSample("debug_building mesh");
        CreateMesh(vertices, indices);
        Profiler.EndSample();
        ////find neighbor with valid vertex
        //OctreeTreeNode FindAdjacentNeighbor(OctreeTreeNode neighbor, Dictionary<OctreeTreeNode, int> VerticeMap, Vector3 direction, OctreeTreeNode node)
        //{


        //    Vector3[] updown = new Vector3[2] { Vector3.up, Vector3.down };
        //    for (int i = 0; i < updown.Length; i++)
        //    {
        //        neighbor = node.FindNeighbor(node, direction + updown[i], Root);
        //        if (neighbor != null)
        //        {
        //            if (VerticeMap.ContainsKey(neighbor))
        //            {
        //                return neighbor;
        //            }
        //        }

        //    }


        //    return null;
        //}
    }

    private void GenerateVertices(OctreeTreeNode node, int minLevel, List<Vector3> vertices, List<int> indices, Dictionary<Vector3, int> VerticIndexMap, Dictionary<OctreeTreeNode, int> VerticeMap)
    {
        if (node.IsLeaf() /*|| node.CurrentLevel <= minLevel*/)
        {
            if (node.Type == NodeType.Mixed)
            {
                //calculate qef vertices
                Vector3 vertex = CalculateWeightedVertex(node);
                // Debug.Log(vertex);


                //add debug vertices 
                //float dist = Vector3.Distance(vertex, new Vector3(863, 1243, 906));
                //if (dist <= 32)
                //{
                //    DebugVertices.Add(vertex);

                //}

                // If the vertex already exists, skip adding it
                if (VerticIndexMap.TryGetValue(vertex, out int existingIndex))
                {
                    VerticeMap[node] = existingIndex;
                    return;
                }


                // Check for an existing vertex in neighboring nodes (up/down)
                var up = FindNeighbor(node, Vector3.up, Root);
                var down = up == null ? FindNeighbor(node, Vector3.down, Root) : null;

                if (up != null && VerticeMap.TryGetValue(up, out int upIndex))
                {
                    VerticeMap[node] = upIndex;
                    VerticIndexMap[vertex] = upIndex;
                    return; // We found an existing vertex, no need to add a new one
                }
                else if (down != null && VerticeMap.TryGetValue(down, out int downIndex))
                {
                    VerticeMap[node] = downIndex;
                    VerticIndexMap[vertex] = downIndex;
                    return;
                }

                // If no existing vertex is found, add a new one
                int newIndex = vertices.Count;
                vertices.Add(vertex);
                VerticeMap[node] = newIndex;
                VerticIndexMap[vertex] = newIndex;
                DebugVertices.Add(vertex);
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                GenerateVertices(child, minLevel, vertices, indices, VerticIndexMap, VerticeMap);
            }
        }
    }

    private void GenerateIndices(List<Vector3> vertices, List<int> indices, Dictionary<Vector3, int> VerticIndexMap, Dictionary<OctreeTreeNode, int> VerticeMap)
    {

        // Face directions and their corresponding normals
        Vector3[] faceDirections = new Vector3[]
        {
    new Vector3(0, 0, 1),  // +Z face
    new Vector3(-1, 0, 0), // -X face
    new Vector3(0, 0, -1), // -Z face
    new Vector3(1, 0, 0),  // +X face
    new Vector3(0, 1, 0), // +y face
    new Vector3(0, -1, 0), // -y face
            
        };

        // Diagonal positions (same horizontal axis, y stays constant) for each face, ordered clockwise
        Vector3[] faceDiagonals = new Vector3[]
        {
        new Vector3(1, 0, 1),    // Diagonal for +Z face
        new Vector3(0, 0, 1),   // Diagonal for -X face
        new Vector3(-1, 0, -1),  // Diagonal for -Z face
        new Vector3(1, 0, 0),   // Diagonal for +X face
        new Vector3(1, 1, 1), // Diagonal for +y face
        new Vector3(-1, -1, 1), // Diagonal for -y face
        };

        foreach (var node in VerticeMap.Keys)
        {



            // neighbors.Add(node);
            // Debug.Log("origin node: " + node.Bounds.center);
            // For each face direction
            bool surroundedByLowlevel = false;
            for (int faceindex = 0; faceindex < faceDirections.Length; faceindex++)
            {


                // Find the four nodes sharing the face
                OctreeTreeNode neighbor = FindNeighbor(node, faceDirections[faceindex], Root);
                OctreeTreeNode diagonalNeighbor = null;
                if (neighbor != null)
                {


                    if (!VerticeMap.ContainsKey(neighbor))
                    {


                        if (neighbor.Children != null)
                        {


                            OctreeTreeNode childNeighbor = null;
                            List<OctreeTreeNode> validnodes = neighbor.Children.Where(child => VerticeMap.ContainsKey(child)).ToList();

                            float quarter = neighbor.Bounds.size.x / 4.0f;
                            Vector3 size = new Vector3(neighbor.Children[0].Bounds.size.x, neighbor.Bounds.size.y, neighbor.Children[0].Bounds.size.z);
                            Vector3 relativeOffset = faceDirections[faceindex];
                            Vector3 adjustedOffset = new Vector3(-quarter * relativeOffset.x, 0, -quarter * relativeOffset.z);
                            Bounds bottomRight = new Bounds(neighbor.Bounds.center + adjustedOffset, size);
                            List<OctreeTreeNode> childNodes = new List<OctreeTreeNode>();


                            foreach (OctreeTreeNode child in validnodes)
                            {
                                // validVertex = vertices[VerticeMap[child]];
                                float maxDist = node.Bounds.size.x + (quarter * 2);
                                var currentChild = child;

                                if (node.Bounds.Intersects(child.Bounds))
                                {
                                    childNodes.Add(child);



                                }

                            }
                            validnodes = childNodes;


                            //remove excess children
                            if (childNodes.Count > 2)
                            {
                                // Find the most common Y value among the child nodes
                                float commonY = childNodes
                                    .GroupBy(child => child.Bounds.center.y)
                                    .OrderByDescending(group => group.Count()) // Get the most frequent Y value
                                    .First()
                                    .Key;

                                // Remove child nodes that are NOT in the most common Y plane
                                childNodes = childNodes.Where(child => Mathf.Approximately(child.Bounds.center.y, commonY)).ToList();
                            }





                            //add debug bounds
                            int counter = 0;
                            foreach (OctreeTreeNode child in childNodes)
                            {
                                var validVertex = vertices[VerticeMap[child]];
                                Color col = Color.Lerp(Color.red, Color.green, counter);
                                // vertexDebuggerlist.Add(new VertexDebug(validVertex, child.Bounds.center, col));
                                counter++;
                            }



                            if (validnodes.Count > 1)
                            {
                                bool isNegativeAxis = (faceindex == 1 || faceindex == 2); // -X or -Z faces
                                surroundedByLowlevel = true;

                                // Sort based on the relevant axis
                                if (faceindex == 0 || faceindex == 2) // Sorting along X-axis for +Z and -Z
                                {
                                    validnodes = validnodes.OrderBy(child => child.Bounds.center.x).ToList();

                                }
                                else // Sorting along Z-axis for -X and +X
                                {
                                    validnodes = validnodes.OrderBy(child => child.Bounds.center.z).ToList();
                                    validnodes.Reverse();
                                }

                                // Flip order if on a negative axis (-X or -Z)
                                if (isNegativeAxis)
                                {
                                    validnodes.Reverse();
                                }



                                childNeighbor = validnodes[0];
                                diagonalNeighbor = validnodes[1];
                            }



                            // Debug.Log("done---------------------------");
                            if (childNeighbor != null)
                            {
                                // vertexDebuggerlist.Add(new VertexDebug(validVertex, childNode.Bounds.center, 16));
                                neighbor = childNeighbor;
                            }

                        }


                    }


                }

                //check diagonal neighbor
                if (neighbor != null && VerticeMap.ContainsKey(neighbor))
                {


                    Vector3 corner = faceDiagonals[faceindex];
                    float size = neighbor.Bounds.size.x;
                    //  Debug.Log($"neighbor exist: {neighbor.Bounds.center + corner * size} face: {faceindex}");

                    //find diagonal neighbor at same level as neighbor
                    if (diagonalNeighbor == null)
                    {
                        diagonalNeighbor = FindNeighbor(node, corner, Root);
                    }

                    //check if diagonal neighbor is found
                    if (diagonalNeighbor != null)
                    {
                        //if(diagonalNeighbor.CurrentLevel > node.CurrentLevel)
                        //{
                        //    var vertex = vertices[VerticeMap[node]];
                        //    vertexDebuggerlist.Add(new VertexDebug(vertex, node.Bounds.center, 16));
                        //}

                        //check if valid vertex exist
                        if (!VerticeMap.ContainsKey(diagonalNeighbor))
                        {
                            if (surroundedByLowlevel)
                            {
                                var axis = faceDirections[faceindex];


                                var offset = -diagonalNeighbor.Bounds.size.x / 2;
                                var vertex = vertices[VerticeMap[node]];
                                vertexDebuggerlist.Add(new VertexDebug(vertex, diagonalNeighbor.Bounds.center, Color.blue));
                                var pos = diagonalNeighbor.Bounds.center + new Vector3(offset * axis.x, 0, offset * axis.z);

                                // Debug.Log($"center: {diagonalNeighbor.Bounds.center} offset: {pos}");
                                Vector3Int neighborpos = new Vector3Int(Mathf.RoundToInt(neighbor.Bounds.center.x), Mathf.RoundToInt(neighbor.Bounds.center.y), Mathf.RoundToInt(neighbor.Bounds.center.z));
                                var adjacentNode = GetNodeAt(neighborpos);

                                if (adjacentNode != null)
                                {
                                    Debug.Log($"neighbor: {faceDirections[faceindex]} diagonal: {faceDiagonals[faceindex]}");

                                    vertexDebuggerlist.Add(new VertexDebug(vertex, adjacentNode.Bounds.center, Color.red));
                                    if (adjacentNode.Children != null)
                                    {
                                        List<OctreeTreeNode> validnodes = adjacentNode.Children.Where(child => VerticeMap.ContainsKey(child)).ToList();
                                        Debug.Log(validnodes.Count);
                                    }
                                }


                            }

                        }
                    }



                    //  Debug.Log($"face index: {faceindex} ");
                    // Add connections if the neighbor exists
                    if (diagonalNeighbor != null && VerticeMap.ContainsKey(diagonalNeighbor))
                    {
                        // Debug.Log($"diag neighbor: {diagonalNeighbor} corner: {node.Bounds.center + corner * node.Bounds.size.x} face: {faceindex} level: {level}");
                        //  neighbors.Add(diagonalNeighbor);
                        try
                        {
                            int v0 = VerticeMap[node]; // Current node's vertex
                            int v1 = VerticeMap[neighbor]; // Face neighbor's vertex
                            int v2 = VerticeMap[diagonalNeighbor]; // Diagonal neighbor's vertex


                            indices.Add(v1);
                            indices.Add(v2);
                            indices.Add(v0);

                        }
                        catch (System.Exception e)
                        {

                            Debug.LogWarning(e.Message);
                        }

                    }

                }

            }

            //find neighbor with valid vertex
            OctreeTreeNode FindAdjacentNeighbor(OctreeTreeNode neighbor, Dictionary<OctreeTreeNode, int> VerticeMap, Vector3 direction, OctreeTreeNode node)
            {


                Vector3[] updown = new Vector3[2] { Vector3.up, Vector3.down };
                for (int i = 0; i < updown.Length; i++)
                {
                    neighbor = FindNeighbor(node, direction + updown[i], Root);
                    if (neighbor != null)
                    {
                        if (VerticeMap.ContainsKey(neighbor))
                        {
                            return neighbor;
                        }
                    }

                }


                return null;
            }

        }



    }

    private void CreateMesh(List<Vector3> vertices, List<int> indices)
    {
        //test if valid triangles exist
        if (indices.Count > 0)
        {



            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = indices.ToArray();
            mesh.RecalculateNormals();
            GameObject go = new GameObject();
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Terrain");
            go.GetComponent<MeshFilter>().mesh = mesh;
            go.name = heightmap.name;
            //go.transform.position = Root.Bounds.center;
            // globalvertices = vertices;
            Debug.Log($"triangles: {indices.Count / 3}");
            Debug.Log($"vertices: {vertices.Count}");

        }
        else
        {

            Debug.Log($"vertices: {vertices.Count}");
            Debug.Log($"triangles: {indices.Count}");
        }
    }
    private Vector3 CalculateWeightedVertex(OctreeTreeNode node)
    {
        // Collect intersection planes
        List<Vector3> positions = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();

        Vector4[] corners = node.CornerDistances;

        for (int i = 0; i < 8; i++)
        {
            //Debug.Log($"node dist: {node.CornerDistances[i].w}");
            // Check for intersection
            for (int j = i + 1; j < 8; j++)
            {



                if ((corners[i].w < 0 && corners[j].w >= 0) || (corners[i].w >= 0 && corners[j].w < 0))
                {

                    // Interpolate position
                    float t = Mathf.Abs(node.CornerDistances[i].w) /
                              (Mathf.Abs(node.CornerDistances[i].w) + Mathf.Abs(node.CornerDistances[j].w));
                    Vector3 intersection = Vector3.Lerp(corners[i], corners[j], t);
                    positions.Add(intersection);

                    // Estimate normal (gradient)
                    Vector3 normal = EstimateNormal(intersection, node.CornerDistances);
                    //  Debug.Log(intersection);
                    normals.Add(normal);
                }
            }
        }
        // Debug.Log("--------------------------break----------------------");
        // Debug.Log($"node {node.Bounds.center} intersections: {positions.Count}");
        Vector3 vertex = WeightedAverage(positions, normals);

        //  vertex.x = node.Bounds.center.x;
        //  vertex.z = node.Bounds.center.z;
        float dist = Vector3.Distance(vertex, node.Bounds.center);
        if (dist >= (node.Bounds.size.x / 2))
        {

            //Debug.LogWarning($"vertex out of bounds: dist: {dist} nodesize: {node.Bounds.size.x}\n vertex: {vertex.x} node: {node.Bounds.center.x}");
            //  vertex = Vector3.Lerp(vertex, node.Bounds.center, 0.5f);

            //  VertexDebug debug = new VertexDebug(vertex, node.Bounds.center, node.Bounds.size.x / 2);
            //  vertexDebuggerlist.Add(debug);

        }

        if (vertex == Vector3.positiveInfinity)
            vertex = node.Bounds.center;

        return vertex;
    }

    private Vector3 EstimateNormal(Vector3 position, Vector4[] corners)
    {
        float epsilon = 0.01f; // Small offset for central difference method
        Vector3 gradient = Vector3.zero;

        // Loop through each axis (x, y, z)
        for (int axis = 0; axis < 3; axis++)
        {
            // Create positive and negative offset vectors
            Vector3 offset = Vector3.zero;
            if (axis == 0) offset = new Vector3(epsilon, 0, 0);
            if (axis == 1) offset = new Vector3(0, epsilon, 0);
            if (axis == 2) offset = new Vector3(0, 0, epsilon);

            // Sample SDF values at offset positions
            float sdfPositive = InterpolateSDF(position + offset, corners);
            float sdfNegative = InterpolateSDF(position - offset, corners);

            // Compute central difference for the current axis
            gradient[axis] = (sdfPositive - sdfNegative) / (2 * epsilon);
        }
        // Debug.Log(gradient);
        return gradient.normalized;
    }

    private float InterpolateSDF(Vector3 position, Vector4[] corners)
    {
        float totalWeight = 0f;
        float sdfValue = 0f;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 cornerPos = new Vector3(corners[i].x, corners[i].y, corners[i].z);
            float distance = Vector3.Distance(position, cornerPos);

            // Calculate weight (inverse distance weighting)
            float weight = 1f / Mathf.Max(distance, 0.001f); // Avoid division by zero
            totalWeight += weight;

            // Accumulate weighted SDF value
            sdfValue += weight * corners[i].w;
        }

        return sdfValue / totalWeight; // Normalize by total weight
    }

    private Vector3 WeightedAverage(List<Vector3> positions, List<Vector3> normals)
    {
        if (positions.Count == 0)
            return Vector3.positiveInfinity; // Return invalid vertex if no intersections

        Vector3 weightedSum = Vector3.zero;
        float weightTotal = 0;

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 p = positions[i];
            Vector3 n = normals[i];

            // Use dot product as a weight (higher alignment gets higher weight)
            float weight = Mathf.Abs(Vector3.Dot(n, p.normalized));


            weightedSum += p * weight;
            weightTotal += weight;
        }

        return weightedSum / weightTotal;
    }




    //moved find neighbor logic
    public OctreeTreeNode FindNeighbor(OctreeTreeNode node, Vector3 direction, OctreeTreeNode root)
    {
        // Compute the bounds of the expected neighbor
        Bounds currentBounds = node.Bounds;
        float size = currentBounds.size.x;
        //  Debug.Log(size);
        Bounds neighborBounds = new Bounds(currentBounds.center + (direction * size), currentBounds.size);
        Bounds neighborBounds2 = new Bounds(currentBounds.center + (direction * size * 2), currentBounds.size);




        //// Start the search from the parent node (or root of the tree)
        //return FindNeighborByPosition(root, neighborBounds.center, node.CurrentLevel);
        Vector3Int neighborPostition = new Vector3Int(Mathf.RoundToInt(neighborBounds.center.x), Mathf.RoundToInt(neighborBounds.center.y), Mathf.RoundToInt(neighborBounds.center.z));
        OctreeTreeNode neighbor = nodeLookup.TryGetValue(neighborPostition, out neighbor) ? neighbor : null;

        if (neighbor == null)
        {


            // Try finding a direct neighbor at the same level
            neighbor = FindNeighborByPosition(root, neighborBounds.center, node.CurrentLevel);

        }
        else
        {
            Debug.Log("quick search");
        }



        if (neighbor == null)
        {
            // If no same-level neighbor, check finer resolution (children of a nearby coarser node)
            // neighbor = FindNeighborAtFinerLevel(node, direction);
            if (neighbor != null)
            {
                Debug.Log("finer");

            }
        }

        if (neighbor == null)
        {
            // If still no valid neighbor, move up and check coarser levels (parent level)
            // neighbor = FindNeighborAtCoarserLevel(node, direction);
            if (neighbor != null)
            {
                Debug.Log("coaser");

            }
        }

        return neighbor;

    }

    //Check finer resolution first: search a neighbor's children
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
    //            Debug.Log($"child level: {child.CurrentLevel} node level: {node.CurrentLevel} Finer level");
    //            return child; // Found a finer-level match
    //        }
    //    }

    //    return null; // No fine match found
    //}

    // If no fine-level match, move up to a coarser level and try again
    //private OctreeTreeNode FindNeighborAtCoarserLevel(OctreeTreeNode node, Vector3 direction)
    //{
    //    if (node.Parent == null) return null;

    //    return FindNeighbor(node.Parent, direction, node.Parent);
    //}

    public OctreeTreeNode FindNeighborByPosition(OctreeTreeNode startNode, Vector3 targetPosition, int targetLevel)
    {

        //return null; // Neighbor not found
        OctreeTreeNode currentNode = startNode;
        int maxDepth = 12; // Safety limit to avoid infinite loops

        for (int i = 0; i < maxDepth; i++)
        {
            if (currentNode == null) return null;

            // ✅ If we found a matching node at the correct level
            if (currentNode.CurrentLevel == targetLevel && currentNode.Bounds.Contains(targetPosition))
            {
                return currentNode;
            }

            // If there are children, traverse downwards
            if (currentNode.Children != null)
            {
                foreach (OctreeTreeNode child in currentNode.Children)
                {
                    if (child.Bounds.Contains(targetPosition))
                    {
                        currentNode = child;
                        break; // Continue traversal with the matching child
                    }
                }
            }
            else
            {
                // 🔺 Move up the tree if no match is found
                currentNode = currentNode.Parent;
            }
        }

        return null;


    }


}


//..........................................................>
//debug class
public class VertexDebug
{
    public Vector3 vertex;
    public Vector3 NodePos;
    public float NodeSize;
    public Color Color;

    public VertexDebug(Vector3 vertex, Vector3 nodePos, Color color)
    {
        this.vertex = vertex;
        this.NodePos = nodePos;
        this.Color = color;
    }
}