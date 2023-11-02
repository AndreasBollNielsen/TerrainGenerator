using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TerrainUtils;

public class Chunk
{

    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Color> colors = new List<Color>();
    public MeshFilter filter;
    MeshCollider meshcol;
    MeshRenderer meshRender;
    public GameObject chunkObject;
    Vector3Int chunkPos;

    public int Totalmemory;
    public VoxelData[] terrainMap;
    public Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();


    int chunkwidth { get { return WorldData.ChunkWidth; } }
    int chunkHeight { get { return WorldData.ChunkHeight; } }
    float surfaceDensity { get { return WorldData.surfaceDensity; } }

    public Chunk(Vector3Int pos)
    {
        chunkObject = new GameObject();
        chunkPos = pos;
        chunkObject.transform.position = chunkPos;
        chunkObject.name = $"chunk: {pos.x}:{pos.z}";

        filter = chunkObject.AddComponent<MeshFilter>();
        meshcol = chunkObject.AddComponent<MeshCollider>();
        meshRender = chunkObject.AddComponent<MeshRenderer>();
        meshRender.material = Resources.Load<Material>("Terrain");

        chunkObject.tag = "Terrain";
        // InitializeTerrainMap();
        GenerateMeshData();

    }



    void ClearMeshData()
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();

    }

    public void AddTerrain(Vector3 pos)
    {
        Vector3Int vecpos = new Vector3Int(Mathf.CeilToInt(pos.x), Mathf.CeilToInt(pos.y), Mathf.CeilToInt(pos.z)) + chunkPos;
        vecpos -= chunkPos;
        WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(vecpos.x, vecpos.y, vecpos.z)].DistanceToSurface = 0f;
        GenerateMeshData();
    }

    public void RemoveTerrain(Vector3 pos)
    {
        Vector3Int vecpos = new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z)) + chunkPos;
        vecpos -= chunkPos;
        WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(vecpos.x, vecpos.y, vecpos.z)].DistanceToSurface = 1f;
        Debug.Log("removing terrain");
        GenerateMeshData();
    }

    public void RemoveSquare(Vector3 pos, int squareSize)
    {
        Vector3Int vecpos = new Vector3Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y), Mathf.FloorToInt(pos.z));
        vecpos -= chunkPos;
        // Define the 3x3x3 neighborhood around the center position
        int neighborhoodSize = squareSize;
        int neighborhoodOffset = neighborhoodSize / 2;
        List<Chunk> updateChunks = new List<Chunk>() { this };


        // Modify the values of the surrounding cells
        for (int x = vecpos.x - neighborhoodOffset; x <= vecpos.x + neighborhoodOffset; x++)
        {
            for (int y = vecpos.y - neighborhoodOffset; y <= vecpos.y + neighborhoodOffset; y++)
            {
                for (int z = vecpos.z - neighborhoodOffset; z <= vecpos.z + neighborhoodOffset; z++)
                {
                    Debug.Log(WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(x + chunkPos.x, y, z + chunkPos.z)].DistanceToSurface);
                    WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(x + chunkPos.x, y, z + chunkPos.z)].DistanceToSurface = 1; // Modify the value of the cell


                    //get neigbor chunk
                    Vector3 chunkpos = WorldGenerator.Instance.VoxelToChunkPos(new Vector3(x, y, z) + chunkPos);
                    var chunk = WorldGenerator.Instance.GetChunk(chunkpos);
                    if (!updateChunks.Any(c => c.chunkObject.name != chunkObject.name) && updateChunks.Any(c => c.chunkPos != chunk.chunkPos))
                    {
                        updateChunks.Add(chunk);
                    }
                }
            }
        }

        //generate chunk data
        foreach (Chunk neighbor in updateChunks)
        {
            neighbor.GenerateMeshData();
        }
        updateChunks.Clear();
        Debug.Log("removing terrain");

    }

    int WorldToVoxelIndex(int x, int y, int z)
    {
        int voxelIndex = x + y * WorldGenerator.Instance.width + z * (WorldGenerator.Instance.width * WorldGenerator.Instance.height);

        return voxelIndex;
    }

    public void GenerateMeshData()
    {
        ClearMeshData();


        Profiler.BeginSample("marchingcube");

        // voxel marching cube
        for (int x = 0; x < chunkwidth; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                for (int z = 0; z < chunkwidth; z++)
                {
                    MarchCube(new Vector3Int(x, y, z));

                }
            }

        }

        
        Profiler.EndSample();

        GenerateMesh();
    }

    void GenerateMesh()
    {
        Profiler.BeginSample("generate mesh");
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();
        filter.mesh = mesh;
        meshcol.sharedMesh = mesh;
        Profiler.EndSample();
        Totalmemory = MemoryHelper.CalcTotalMemory(mesh);
        // Debug.Log($"chunk memory: {MemoryHelper.ConvertBytes(Totalmemory)}");


    }

    int GetCubeConfiguration(float[] cube)
    {
        int configurationIndex = 0;

        //iterate each corner
        for (int i = 0; i < 8; i++)
        {
            if (cube[i] > surfaceDensity)
            {
                //bitmask bool operation
                configurationIndex |= 1 << i;
            }
        }
        return configurationIndex;
    }

    void MarchCube(Vector3Int position)
    {
        //sample terrain at each cube corner
        float[] cubes = new float[8];
        for (int j = 0; j < 8; j++)
        {
            //samples terrain data at neigboring cells
            cubes[j] = SampleTerrain(position + WorldData.CornerTable[j]);
        }

        //get configuration index of the cube
        int configIndex = GetCubeConfiguration(cubes);


        //if position is outside of cube
        if (configIndex == 0 || configIndex == 255)
        {
            return;
        }

        int edgeIndex = 0;
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                int indice = WorldData.TriangleTable[configIndex, edgeIndex];

                //return if end of indices
                if (indice == -1)
                {
                    return;
                }

                //get top and bottom of cube
                Vector3 vert1 = position + WorldData.CornerTable[WorldData.EdgeIndexes[indice, 0]];
                Vector3 vert2 = position + WorldData.CornerTable[WorldData.EdgeIndexes[indice, 1]];

                Vector3 vertexPosition;

                //get terrain values at either end of the edge
                float vert1Sample = cubes[WorldData.EdgeIndexes[indice, 0]];
                float vert2Sample = cubes[WorldData.EdgeIndexes[indice, 1]];

                //calculate the difference between terrain values
                float diff = vert2Sample - vert1Sample;

                //if difference is 0 terrain passes through the middle
                if (diff == 0)
                {
                    diff = surfaceDensity;
                }
                else
                {
                    diff = (surfaceDensity - vert1Sample) / diff;
                }

                //calculate the point along the edge that passes through
                vertexPosition = vert1 + ((vert2 - vert1) * diff);


                //add vertices and triangles
                triangles.Add(VertForIndice(vertexPosition, position));



                edgeIndex++;
            }
        }
    }

    int VertForIndice(Vector3 vert, Vector3Int voxelPos)
    {
        //loop through the vertices
        for (int i = 0; i < vertices.Count; i++)
        {
            //check if vertex already exists in the list. return it exists
            if (vertices[i] == vert)
            {
                return i;
            }
        }

        // if it does not exist in list, add it to the list
        vertices.Add(vert);
        var colorIndex = WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(voxelPos.x, voxelPos.y, voxelPos.z)].TexIndex;

        colors.Add(GetColorIndex(colorIndex));
        return vertices.Count - 1;
    }

    float SampleTerrain(Vector3Int pos)
    {
        // Debug.Log(pos);
        return WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(pos.x + chunkPos.x, pos.y, pos.z + chunkPos.z)].DistanceToSurface;



        //float terrainHeight = WorldData.GetTerrainHeight(pos.z + chunkPos.z, pos.x + chunkPos.x, WorldGenerator.Instance.HeightMap);
        //return pos.y - terrainHeight;
    }


    //test coloring
    Color GetColorIndex(int index)
    {
        Color[] colors = new Color[]
        {
            new Color(0,1,0),
            new Color(1,0,0),
            new Color(0,0,1),
            new Color(0,1,1),
            new Color(0,0,0)
        };

        return colors[index];
    }



}
