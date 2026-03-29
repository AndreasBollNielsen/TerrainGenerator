using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

public class OctreeChunk
{
    public int Chunkwidth;
    public Vector3 Position;
    private List<OctreeTreeNode> LocalNodes;
    private List<OctreeTreeNode> NeighborNodes;
    private List<Vector3> DebugVertices;
    public Mesh OctreeMesh;
    public GameObject chunkObject;
    private OctreeTreeNode Root;
    private Octree OctreeRef;

    public OctreeChunk(OctreeTreeNode node, Vector3 position)
    {
        this.LocalNodes = new List<OctreeTreeNode> { node };
        this.NeighborNodes = new List<OctreeTreeNode>();
        this.DebugVertices = new List<Vector3>();
        this.Chunkwidth = Mathf.RoundToInt(node.Bounds.size.x);
        this.Position = position;
        chunkObject = new GameObject($"Chunk_{position.x}_{position.y}_{position.z}");
        // chunkObject.transform.position = position;
        chunkObject.AddComponent<BoxCollider>().center = this.Position;
        chunkObject.GetComponent<BoxCollider>().size = node.Bounds.size;
    }

    public void AddNode(OctreeTreeNode node)
    {
        LocalNodes.Add(node);
    }

    public void AddNeighbor(OctreeTreeNode neighbor)
    {
        NeighborNodes.Add(neighbor);
    }

    public void SetRoot(OctreeTreeNode rootNode)
    {
        this.Root = rootNode;
    }

    public void SetOctreeRef(Octree octreeRef)
    {
        this.OctreeRef = octreeRef;
    }

    public List<OctreeTreeNode> GetNodes()
    {
        return LocalNodes;
    }

    public void DrawBoundingBox(bool ShowVertices = false)
    {
        //draw nodes within chunk
        foreach (var node in this.LocalNodes)
        {
            Vector3 center = node.Bounds.center;
            Vector3 size = new Vector3(node.Bounds.size.x, node.Bounds.size.y, node.Bounds.size.z);
            Gizmos.color = Color.blue;
            //  Gizmos.DrawWireCube(center, size);


            if (!node.IsLeaf())
            {
                foreach (var child in node.Children)
                {
                    center = child.Bounds.center;
                    size = new Vector3(child.Bounds.size.x, child.Bounds.size.y, child.Bounds.size.z);
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }

        //draw neighbor nodes
        foreach (var node in this.NeighborNodes)
        {
            Vector3 center = node.Bounds.center;
            Vector3 size = new Vector3(node.Bounds.size.x, node.Bounds.size.y, node.Bounds.size.z);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, size);


            if (!node.IsLeaf())
            {
                foreach (var child in node.Children)
                {
                    center = child.Bounds.center;
                    size = new Vector3(child.Bounds.size.x, child.Bounds.size.y, child.Bounds.size.z);
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }

        if (ShowVertices)
        {
            foreach (var vertex in DebugVertices)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(vertex, 0.2f);
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




        //traverse octree
        int minLevel = 4;

        foreach (var node in this.LocalNodes)
        {
            GenerateVertices(node, minLevel, vertices, indices, VerticIndexMap, VerticeMap);
        }

        foreach (var node in this.NeighborNodes)
        {
            GenerateVertices(node, minLevel, vertices, indices, VerticIndexMap, VerticeMap);
        }

        // Debug.Log(vertices.Count);
        GenerateIndices(vertices, indices, VerticIndexMap, VerticeMap);

        CreateMesh(vertices, indices);
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

                    //check if node exists in dictionary
                    if (!VerticeMap.ContainsKey(neighbor))
                    {
                        OctreeTreeNode neighborAbove = FindNeighbor(neighbor, Vector3.up, Root);
                        OctreeTreeNode neighborbelow = FindNeighbor(neighbor, Vector3.down, Root);

                        OctreeTreeNode[] neighbors = new[]
                        {
                            neighbor,
                            neighborAbove,
                            neighborbelow
                        };

                        //go through all 3 neighbors & check if any of them are valid
                        var Validatedneighbor = GetValidNodeFromVerticesMap(neighbors, VerticeMap);
                        //check if found neighbor is not null
                        if(Validatedneighbor != null)
                        {
                            neighbor = Validatedneighbor;
                        }

                        //check children if neighbor is still null
                        if (neighbor == null)
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


                                //Code responsible for stitching low level and high level nodes.
                                //if (validnodes.Count > 1)
                                //{
                                //    bool isNegativeAxis = (faceindex == 1 || faceindex == 2); // -X or -Z faces
                                //    surroundedByLowlevel = true;

                                //    // Sort based on the relevant axis
                                //    if (faceindex == 0 || faceindex == 2) // Sorting along X-axis for +Z and -Z
                                //    {
                                //        validnodes = validnodes.OrderBy(child => child.Bounds.center.x).ToList();

                                //    }
                                //    else // Sorting along Z-axis for -X and +X
                                //    {
                                //        validnodes = validnodes.OrderBy(child => child.Bounds.center.z).ToList();
                                //        validnodes.Reverse();
                                //    }

                                //    // Flip order if on a negative axis (-X or -Z)
                                //    if (isNegativeAxis)
                                //    {
                                //        validnodes.Reverse();
                                //    }



                                //    childNeighbor = validnodes[0];
                                //    diagonalNeighbor = validnodes[1];
                                //}



                                // Debug.Log("done---------------------------");
                                if (childNeighbor != null)
                                {
                                    // vertexDebuggerlist.Add(new VertexDebug(validVertex, childNode.Bounds.center, 16));
                                    neighbor = childNeighbor;
                                }

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
                                //  vertexDebuggerlist.Add(new VertexDebug(vertex, diagonalNeighbor.Bounds.center, Color.blue));
                                var pos = diagonalNeighbor.Bounds.center + new Vector3(offset * axis.x, 0, offset * axis.z);

                                // Debug.Log($"center: {diagonalNeighbor.Bounds.center} offset: {pos}");
                                Vector3Int neighborpos = new Vector3Int(Mathf.RoundToInt(neighbor.Bounds.center.x), Mathf.RoundToInt(neighbor.Bounds.center.y), Mathf.RoundToInt(neighbor.Bounds.center.z));
                                var adjacentNode = OctreeRef.GetNodeAt(neighborpos);

                                if (adjacentNode != null)
                                {
                                    Debug.Log($"neighbor: {faceDirections[faceindex]} diagonal: {faceDiagonals[faceindex]}");

                                    //  vertexDebuggerlist.Add(new VertexDebug(vertex, adjacentNode.Bounds.center, Color.red));
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
                    else
                    {
                        Debug.Log("no diagonal");
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
            // GameObject go = new GameObject();
            chunkObject.AddComponent<MeshFilter>();
            chunkObject.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Terrain");
            chunkObject.GetComponent<MeshFilter>().mesh = mesh;
            //go.name = heightmap.name;
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

    public OctreeTreeNode GetValidNodeFromVerticesMap(OctreeTreeNode[] neighbors, Dictionary<OctreeTreeNode, int> verticeMap)
    {
        foreach (OctreeTreeNode node in neighbors)
        {
            if(node != null)
            {
                if (verticeMap.ContainsKey(node))
                {
                    return node;
                }
            }
            
        }
        return null;
    }

    public OctreeTreeNode FindNeighbor(OctreeTreeNode node, Vector3 direction, OctreeTreeNode root)
    {
        // Compute the bounds of the expected neighbor
        Bounds currentBounds = node.Bounds;
        float size = currentBounds.size.x;
        //  Debug.Log(size);
        Bounds neighborBounds = new Bounds(currentBounds.center + (direction * size), currentBounds.size);
        //   Bounds neighborBounds2 = new Bounds(currentBounds.center + (direction * size * 2), currentBounds.size);




        //// Start the search from the parent node (or root of the tree)
        //return FindNeighborByPosition(root, neighborBounds.center, node.CurrentLevel);
        Vector3Int neighborPostition = new Vector3Int(Mathf.RoundToInt(neighborBounds.center.x), Mathf.RoundToInt(neighborBounds.center.y), Mathf.RoundToInt(neighborBounds.center.z));
        // OctreeTreeNode neighbor = nodeLookup.TryGetValue(neighborPostition, out neighbor) ? neighbor : null;
        OctreeTreeNode neighbor = OctreeRef.GetNodeAt(neighborPostition);

        if (neighbor == null)
        {


            // Try finding a direct neighbor at the same level
            neighbor = FindNeighborByPosition(root, neighborBounds.center, node.CurrentLevel);

        }
        else
        {
            // Debug.Log("quick search");
            return neighbor;
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

    //helper functions----------------------------------------------
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

}
