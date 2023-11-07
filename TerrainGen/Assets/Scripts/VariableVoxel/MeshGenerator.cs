using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MeshGenerator : MonoBehaviour
{
    List<int> triangles = new List<int>();
    List<Vector3> vertices = new List<Vector3>();
    float surfaceDensity = WorldData.surfaceDensity;
    VoxelGenerator VoxelGenerator;


    [SerializeField] Material material;
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

        // objects.Clear();    

    }

    public void GenerateMesh(int voxelsLength, int voxelWidth, int voxelHeight, int voxelSize, Vector2 worldSize, Vector2Int offset)
    {
        float width = worldSize.x;
        float height = worldSize.y;
        // Debug.Log($" voxellength: {voxelsLength} height {voxelHeight}");
        for (int index = 0; index < voxelsLength; index++)
        {


            // Convert the 1D index to 3D coordinates
            int x = index % voxelWidth;
            int y = (index / voxelWidth) % voxelHeight;
            int z = index / (voxelWidth * voxelHeight);

            // Calculate the position of the voxel in world space
            Vector3 voxelPosition = new Vector3(
                x * voxelSize,
                y * voxelSize,
                z * voxelSize
            );




            //use marchingcube algorithm to generate triangles and vertices
            if (voxelPosition.x < width && voxelPosition.z < width && voxelPosition.y < height)
            {
                MarchCube(voxelPosition, voxelSize);

            }
            //chck edges
            if(x == 0 || x == voxelsLength - 1 || z == 0  || z == voxelsLength-1)
            {
                if(vertices.Count > 0)
                Debug.Log($"edge: {vertices[vertices.Count - 1]}");
            }
        }


        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();

        mesh.RecalculateNormals();



        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>();
        go.GetComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;
        go.transform.position = new Vector3(offset.y, 0, offset.x);
        go.name = $"chunk_{offset.y}_{offset.x}";

        // Debug.Log($"number of vertices: {vertices.Count} chunk: {go.name} voxelSize: {voxelSize}");
        Chunk_v chunk = new Chunk_v();
        chunk.chunkObject = go;
        chunk.mesh = mesh;


        chunks.Add(chunk);

        triangles.Clear();
        vertices.Clear();

    }

    void MarchCube(Vector3 position, int voxelSize)
    {


        //sample terrain at each cube corner
        float[] cubes = new float[8];
        for (int j = 0; j < 8; j++)
        {
            //samples terrain data at neigboring cells
            cubes[j] = VoxelGenerator.GetVoxelSample(position + (WorldData.CornerTable[j] * voxelSize));
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
                Vector3 vert1 = position + WorldData.CornerTable[WorldData.EdgeIndexes[indice, 0]] * voxelSize;
                Vector3 vert2 = position + WorldData.CornerTable[WorldData.EdgeIndexes[indice, 1]] * voxelSize;

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

            // if it does not exist in list, add it to the list
            vertices.Add(vert);
            // var colorIndex = WorldGenerator.Instance.terrainMap[WorldToVoxelIndex(voxelPos.x, voxelPos.y, voxelPos.z)].TexIndex;
            // colors.Add(GetColorIndex(colorIndex));

            return vertices.Count - 1;
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

    public void ClearList()
    {
        chunks.Clear();
    }

    public List<Chunk_v> CopyChunks()
    {
        return chunks;
    }



}
