using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.Profiling;
public class OctreeTest : MonoBehaviour
{
    private Octree Octree;
    public Texture2D HeightMap;
    public float MaxHeight;
    public float BoundSize;
    public int MaxDepth;
    public Transform player;
    public static Vector3 playerPos;
    public static Dictionary<int, int> NodeLevels = new Dictionary<int, int>() { { 16, 11 }, { 32, 10 }, { 64, 9 }, { 128, 8 }, { 256, 7 }, { 512, 6 }, { 1024, 5 }, { 2048, 4 }, { 3072, 3 }, { 4096, 2 } };
    Dictionary<Vector2Int, OctreeChunk> chunkLookUp = new Dictionary<Vector2Int, OctreeChunk>();

    List<OctreeTreeNode> debugNodes = new List<OctreeTreeNode>();

    [Range(0, 12)]
    public int MinThreshold;
    [Range(0, 12)]
    public int MaxThreshold;
    public bool EnableDebugView, EnableVertices, EnableBounds;
    [Range(1f, 256)]
    public float debugSize = 100;
    // Start is called before the first frame update
    void Start()
    {
        playerPos = player.transform.position;
        Vector3 origin = new Vector3(1024, 1024, 1024) + this.transform.position;
        // Define the bounds of the terrain (e.g., 256x256)
        Bounds terrainBounds = new Bounds(origin, new Vector3(BoundSize, BoundSize, BoundSize));

        // Create a quadtree with a maximum depth of 4
        Octree = new Octree(terrainBounds, MaxDepth, HeightMap, MaxHeight);

        // Subdivide the root node
        Profiler.BeginSample("debug_Octree generation");
        Octree.Subdivide(Octree.Root);
        Profiler.EndSample();



        //generate chunks
        //test new approach
        int chunkSize = 128;
        int worldSize = 2048;
        int chunkCount = worldSize / chunkSize; // = 16

        for (int x = 0; x < chunkCount; x++)
        {
            for (int z = 0; z < chunkCount; z++)
            {
                Vector3 chunkCenter = new Vector3(x * chunkSize + chunkSize / 2f, 0, z * chunkSize + chunkSize / 2f);
                Bounds currentBound = new Bounds(chunkCenter, new Vector3(chunkSize, 128, chunkSize));

                List<OctreeTreeNode> collectedNodes = new List<OctreeTreeNode>();
                List<OctreeTreeNode> neighborNodes = new List<OctreeTreeNode>();
                Octree.GetNodeWithinChunk(Octree.Root, currentBound, chunkSize, collectedNodes, neighborNodes);

                if (collectedNodes.Count > 0)
                {
                    Vector2Int chunkKey = new Vector2Int(x * chunkSize, z * chunkSize);
                    OctreeChunk chunk = new OctreeChunk(collectedNodes[0], chunkCenter); // or whatever node you want to pass in
                    chunk.SetRoot(Octree.Root);
                    chunk.SetOctreeRef(Octree);

                    foreach (var node in collectedNodes)
                    {
                        chunk.AddNode(node);
                    }

                    foreach (var node in neighborNodes)
                    {
                        chunk.AddNeighbor(node);

                    }

                    chunkLookUp[chunkKey] = chunk;
                }
            }
        }



        // var Localnodes = chunkLookUp[new Vector2Int(0, 0)].GetNodes();
        Debug.Log("nodes: " + debugNodes.Count);
        //    chunkLookUp[new Vector2Int(0, 0)].GenerateMesh();
        // chunkLookUp[new Vector2Int(960+64, 1600+64)].GenerateMesh();
        //   chunkLookUp[new Vector2Int(704 + 64, 1344 + 64)].GenerateMesh();

        // Profiler.BeginSample("debug_terrain generation");
        // Octree.GenerateMesh();
        // Profiler.EndSample();

        var chunks = chunkLookUp.Values;
        foreach (var chunk in chunks)
        {
            chunk.GenerateMesh();
        }

        EditorApplication.isPaused = true;
    }

    private void OnDrawGizmos()
    {
        if (Octree != null)
        {
            if (EnableDebugView)
            {
                Octree.DrawBounds(Octree.Root, 0, MinThreshold, MaxThreshold);

            }

            if (EnableVertices)
            {
                Octree.DrawVertices(debugSize);

            }
            if (EnableBounds)
            {
                Octree.DrawDebugBounds(debugSize);
            }

            // Octree.DrawNeighbors();
        }

        //debug new nodes
        foreach (var node in debugNodes)
        {
            Vector3 center = node.Bounds.center;
            Vector3 size = new Vector3(node.Bounds.size.x, node.Bounds.size.y, node.Bounds.size.z);
            if (center.x == 64 && center.z == 64)
            {
                Gizmos.DrawWireCube(center, size);

            }
        }


        foreach (var chunk in chunkLookUp.Values)
        {
            var nodes = chunk.GetNodes();
            var pos = chunk.Position;
            if (pos.x == 832 && pos.z == 960)
            {
                chunk.DrawBoundingBox(true);

            }
        }
    }


}
