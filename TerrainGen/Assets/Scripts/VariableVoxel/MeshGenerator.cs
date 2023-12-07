using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using static Block;
using static WorldData;

public class MeshGenerator : MonoBehaviour
{
    List<int> triangles = new List<int>();
    List<Vector3> vertices = new List<Vector3>();
    List<Color> colors = new List<Color>();
    float surfaceDensity = WorldData.surfaceDensity;
    VoxelGenerator VoxelGenerator;

    //define chunk edges
    List<int> topEdge = new List<int>();
    List<int> bottomEdge = new List<int>();
    List<int> leftEdge = new List<int>();
    List<int> rightEdge = new List<int>();



    public Material material;
    List<Chunk_v> chunks = new List<Chunk_v>();
    // Start is called before the first frame update
    private void Awake()
    {
        VoxelGenerator = GetComponent<VoxelGenerator>();
        Debug.Log("generator fetched");
    }

    void Start()
    {

    }

    public void DestroyMesh(List<Chunk_v> chunks)
    {


        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].mesh.Clear();
            Destroy(chunks[i].chunkObject);
        }

    }

    public void CreateMesh(Vector2Int offset, int blockId, List<Vector3> vertices, List<int> triangles)
    {
        //set mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        //  mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();


        //add mesh to gameobject
        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>();
        go.GetComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;
        go.transform.position = new Vector3(offset.y, 0, offset.x);
        go.name = $"chunk_{offset.y}_{offset.x}";

        //create chunk object
        Chunk_v chunk = new Chunk_v();
        chunk.chunkObject = go;
        chunk.mesh = mesh;
        chunk.blockId = blockId;
        //  chunk.CopyEdges(topEdge, bottomEdge, leftEdge, rightEdge);
        chunks.Add(chunk);

        //clear lists
        triangles.Clear();
        vertices.Clear();
        //colors.Clear();

        //topEdge.Clear();
        //bottomEdge.Clear();
        //leftEdge.Clear();
        //rightEdge.Clear();
    }

    public JobHandle GenerateMeshJobified(int voxelsLength, int voxelWidth, int voxelHeight, int voxelSize, Vector2 chunkSize, Vector2Int offset, int blockId, NativeArray<VoxelData> voxelData, NativeList<Vector3> vertices, NativeList<int> triangles, JobHandle dependency, WorldData.TerrainData terrainData)
    {


        MarchingCube_Job job = new MarchingCube_Job()
        {

            TerrainData = terrainData,
            //blockId = blockId,
            //  voxelData = voxelData,
            voxelSize = voxelSize,
            voxelWidth = voxelWidth,
            voxelHeight = voxelHeight,
            height = chunkSize.y,
            voxelsLength = voxelsLength,
            surfaceDensity = WorldData.surfaceDensity,
            width = chunkSize.x,
            triangles = triangles,
            vertices = vertices,

        };

        JobHandle handle = job.Schedule(voxelsLength,32, dependency);
        return handle;
    }

    public void GenerateMesh(int voxelsLength, int voxelWidth, int voxelHeight, int voxelSize, Vector2 chunkSize, Vector2Int offset, int blockId, VoxelData[] voxelData, WorldData.TerrainData terrainData)
    {
        float width = chunkSize.x;
        float height = chunkSize.y;
        //   Debug.Log($" height: {height} voxelwidth: {voxelWidth}");
        for (int index = 0; index < voxelsLength; index++)
        {
            // Convert the 1D index to 3D coordinates
            int x = Mathf.FloorToInt(index % voxelWidth);
            int y = Mathf.FloorToInt((index / voxelWidth) % voxelHeight);
            int z = Mathf.FloorToInt(index / (voxelWidth * voxelHeight));

            // Calculate the position of the voxel in world space
            Vector3 voxelPosition = new Vector3(
                x * voxelSize,
                y * voxelSize,
                z * voxelSize
            );




            //use marchingcube algorithm to generate triangles and vertices
            if (voxelPosition.x < width && voxelPosition.z < width && voxelPosition.y < height)
            {
                // Debug.Log($"position: {voxelPosition}");
                MarchCube(voxelPosition, voxelSize, (int)(width));

            }

        }





        //set mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        //  mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();


        //add mesh to gameobject
        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>();
        go.GetComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;
        go.transform.position = new Vector3(offset.y, 0, offset.x);
        go.name = $"chunk_{offset.y}_{offset.x}";

        //create chunk object
        Chunk_v chunk = new Chunk_v();
        chunk.chunkObject = go;
        chunk.mesh = mesh;
        chunk.blockId = blockId;
        //  chunk.CopyEdges(topEdge, bottomEdge, leftEdge, rightEdge);
        chunks.Add(chunk);

        //clear lists
        triangles.Clear();
        vertices.Clear();
        colors.Clear();

        topEdge.Clear();
        bottomEdge.Clear();
        leftEdge.Clear();
        rightEdge.Clear();



        void MarchCube(Vector3 position, int voxelSize, int width)
        {


            //sample terrain at each cube corner
            float[] cubes = new float[8];
            for (int j = 0; j < 8; j++)
            {
                //samples terrain data at neigboring cells
                Vector3 worldpos = position + (terrainData.CornerTable[j] * voxelSize);
                if (worldpos.z == 528)
                {
                    Debug.LogError($"worldpos: {position} corner: {terrainData.CornerTable[j] * voxelSize} voxelSize: {voxelSize}");
                    //break;
                }
                cubes[j] = VoxelGenerator.GetVoxelSample(worldpos, voxelSize, voxelData);
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
                    int indice = terrainData.GetTriangleTableValue(configIndex, edgeIndex);

                    //return if end of indices
                    if (indice == -1)
                    {
                        return;
                    }

                    //get top and bottom of cube
                    Vector3 vert1 = position + terrainData.CornerTable[terrainData.GetEdgeIndexesValue(indice, 0)] * voxelSize;
                    Vector3 vert2 = position + terrainData.CornerTable[terrainData.GetEdgeIndexesValue(indice, 1)] * voxelSize;

                    Vector3 vertexPosition;

                    //get terrain values at either end of the edge
                    float vert1Sample = cubes[terrainData.GetEdgeIndexesValue(indice, 0)];
                    float vert2Sample = cubes[terrainData.GetEdgeIndexesValue(indice, 1)];

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
                    int vertIndex = VertForIndice(vertexPosition, position);
                    DetectEges(vertexPosition, vertIndex);
                    triangles.Add(vertIndex);



                    edgeIndex++;
                }
            }

            int VertForIndice(Vector3 vert, Vector3 voxelPos)
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

                Color color = Color.white;

                //if (vert.z >= width-10)
                //{
                //    Debug.Log($"vert: {vert} width: {width}");
                //}



                //if (color == Color.red)
                //{
                //    vert = new Vector3(vert.x, vert.y - 10, vert.z);
                //}

                // if it does not exist in list, add it to the list
                vertices.Add(vert);
                colors.Add(color);
                // var colorIndex = WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(voxelPos.x, voxelPos.y, voxelPos.z)].TexIndex;
                // colors.Add(GetColorIndex(colorIndex));

                return vertices.Count - 1;
            }

            Color DetectEges(Vector3 vert, int vertIndex)
            {
                Color color = Color.white;

                //detect top edge
                if (vert.z >= width)
                {
                    topEdge.Add(vertIndex);
                    return Color.red;
                }
                //detect bottom edge
                else if (vert.z == 0)
                {
                    bottomEdge.Add(vertIndex);
                    return Color.red;
                }
                //detect left edge
                else if (vert.x >= width)
                {
                    leftEdge.Add(vertIndex);
                    return Color.red;
                }
                //detect right edge
                else if (vert.x == 0)
                {
                    rightEdge.Add(vertIndex);
                    return Color.red;
                }


                return color;


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


        }
    }

    void RemoveTrianglesAlongBorder(Mesh mesh, List<int> borderEdgeVertices)
    {
        // Get the mesh triangles array
        int[] triangles = mesh.triangles;

        // Remove triangles associated with border edge vertices
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int vertexIndex0 = triangles[i];
            int vertexIndex1 = triangles[i + 1];
            int vertexIndex2 = triangles[i + 2];

            // Check if any vertex index is part of the border
            if (borderEdgeVertices.Contains(vertexIndex0) || borderEdgeVertices.Contains(vertexIndex1) || borderEdgeVertices.Contains(vertexIndex2))
            {
                // Set triangle indices to -1 or any sentinel value
                triangles[i] = triangles[i + 1] = triangles[i + 2] = -1;
            }
        }

        // Remove triangles with sentinel indices
        mesh.triangles = triangles.Where(index => index != -1).ToArray();
    }



    public void ClearList()
    {
        chunks.Clear();
    }

    public List<Chunk_v> CopyChunks()
    {
        return chunks;
    }

    


}
