using Assets.Scripts.VariableVoxel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class GPUvoxels : MonoBehaviour
{
    public ComputeShader MarchingShader;
    public Texture2D Heightfield;
    public int VoxelSize = 1;
    public int ChunkWidth = 16;
    int PointsPerChunk;
    public bool EnableDebug;
    VoxelData_v2[] _weights;


    ComputeBuffer _triangleBuffer;
    ComputeBuffer _trianglesCountBuffer;
    ComputeBuffer _weightsBuffer;

    // Start is called before the first frame update
    void Start()
    {
        PointsPerChunk = ChunkWidth;
        _weights = GenerateVoxels();


        GameObject go = new GameObject();
        Mesh mesh = GenerateMesh();
        go.AddComponent<MeshFilter>();
        go.GetComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Terrain");

        go.transform.position = this.transform.position;
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    void CreateBuffers()
    {
        int pointsPerChunk = (PointsPerChunk + 2) / VoxelSize;
        int height = GridMetrics.height / VoxelSize;
        _triangleBuffer = new ComputeBuffer(5 * (pointsPerChunk * height * pointsPerChunk), Triangle.SizeOf, ComputeBufferType.Append);
        _trianglesCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        _weightsBuffer = new ComputeBuffer(_weights.Length, VoxelData_v2.SizeOf);
    }

    void ReleaseBuffers()
    {
        _triangleBuffer.Release();
        _trianglesCountBuffer.Release();
        _weightsBuffer.Release();
    }

    Mesh GenerateMesh()
    {
        CreateBuffers();

        int xvoxels = Mathf.CeilToInt( (PointsPerChunk +2 ) / VoxelSize);
        int yvoxels = Mathf.CeilToInt((GridMetrics.height +1) / VoxelSize);

        // set buffers
        MarchingShader.SetBuffer(0, "_Triangles", _triangleBuffer);
        MarchingShader.SetBuffer(0, "_Weights", _weightsBuffer);

        //set variables
        MarchingShader.SetInt("_ChunkSize", ChunkWidth);
        MarchingShader.SetFloat("_IsoLevel", 0.5f);
        MarchingShader.SetInt("_VoxelHeight", yvoxels);
        MarchingShader.SetInt("_VoxekWidth", (xvoxels ));
        MarchingShader.SetInt("_voxelSize", VoxelSize);

        //initialize buffers
        _weightsBuffer.SetData(_weights);
        _triangleBuffer.SetCounterValue(0);




        // Number of Thread Groups
        int numGroupsX = Mathf.CeilToInt(xvoxels +1);
        int numGroupsY = Mathf.CeilToInt(yvoxels +1);
        int numGroupsZ = numGroupsX;


        Debug.Log($"xvoxels: {xvoxels } yvoxels: {yvoxels} chunkwidth: {ChunkWidth} voxelsize: {VoxelSize}");

        //dispatch
        MarchingShader.Dispatch(0, numGroupsX, numGroupsY, numGroupsZ);

        //get triangles from computeshader
        Triangle[] triangles = new Triangle[ReadTriangleCount()];
        _triangleBuffer.GetData(triangles);

        ReleaseBuffers();
        Mesh mesh = CreateMesh(triangles);

        return mesh;
    }

    Mesh CreateMesh(Triangle[] triangles)
    {
        //simple mesh generation - hard edges
        //Vector3[] verts = new Vector3[triangles.Length * 3];
        //int[] tris = new int[triangles.Length * 3];

        //for (int i = 0; i < triangles.Length; i++)
        //{
        //    int startIndex = i * 3;
        //    verts[startIndex] = triangles[i].a;
        //    verts[startIndex + 1] = triangles[i].b;
        //    verts[startIndex + 2] = triangles[i].c;

        //    tris[startIndex] = startIndex;
        //    tris[startIndex + 1] = startIndex + 2;
        //    tris[startIndex + 2] = startIndex + 1;
        //}

        //Mesh mesh = new Mesh();
        //mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        //mesh.vertices = verts;
        //mesh.triangles = tris;
        //mesh.RecalculateNormals();

        //return mesh;

        //advanced mesh generation - smooth surface
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        List<Vector3> verts = new List<Vector3>();
        List<Color> cols = new List<Color>();
        List<int> tris = new List<int>();

        for (int i = 0; i < triangles.Length; i++)
        {
            Vector3[] triangleVertices = new Vector3[] { triangles[i].a, triangles[i].b, triangles[i].c };
            Color[] triangleColors = new Color[] { triangles[i].color_a, triangles[i].color_b, triangles[i].color_c };
            int[] triIndices = new int[3];

            for (int j = 0; j < 3; j++)
            {
                Vector3 vertex = triangleVertices[j];
                Color col = triangleColors[j];

                if (vertexMap.ContainsKey(vertex))
                {
                    // If the vertex is already in the map, use the existing index
                    triIndices[j] = vertexMap[vertex];
                }
                else
                {
                    // If it's a new vertex, add it to the verts list and the map
                    verts.Add(vertex);
                    cols.Add(col);
                    int newIndex = verts.Count - 1;
                    vertexMap[vertex] = newIndex;
                    triIndices[j] = newIndex;
                }
            }

            // Add the triangle indices to the tris list
            tris.Add(triIndices[0]);
            tris.Add(triIndices[2]); // Corrected the winding order to ensure proper face orientation
            tris.Add(triIndices[1]);
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.colors = cols.ToArray();
        mesh.RecalculateNormals(); // Ensure smooth shading by recalculating normals

        return mesh;
    }

    int ReadTriangleCount()
    {
        int[] count = { 0 };

        ComputeBuffer.CopyCount(_triangleBuffer, _trianglesCountBuffer, 0);
        _trianglesCountBuffer.GetData(count);
        return count[0];
    }

    VoxelData_v2[] GenerateVoxels()
    {
        int pointsPerChunk = Mathf.CeilToInt((PointsPerChunk + 2) / VoxelSize);
        int height = Mathf.CeilToInt((GridMetrics.height + 1) / VoxelSize);
        float threshold = 0.5f;
        VoxelData_v2[] weights = new VoxelData_v2[(pointsPerChunk) * (height) * (pointsPerChunk)];
        Debug.Log($"voxelwidth: {pointsPerChunk} voxelheight: {height} numvoxels: {weights.Length}");
        for (int x = 0; x < pointsPerChunk; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < pointsPerChunk; z++)
                {
                    // Calculate the 1D index for the 3D grid position
                    int index = x + pointsPerChunk * (y + height * z);

                    // Here you would calculate the actual value at this position
                    float value = CalculateValueAtPosition(x, y, z);

                    ushort texIindex = (ushort)UnityEngine.Random.Range(0, 1);
                    // Assign value based on the threshold
                    if (value < threshold)
                    {
                        VoxelData_v2 voxel = new VoxelData_v2((half)0, texIindex);
                        weights[index] = voxel;
                    }
                    else
                    {
                        VoxelData_v2 voxel = new VoxelData_v2((half)value, texIindex);
                        weights[index] = voxel;
                    }


                }
            }
        }

        return weights;


        float CalculateValueAtPosition(int x, int y, int z)
        {
            // Convert voxel coordinates back to world coordinates if needed
            int worldX = x * VoxelSize;
            int worldZ = z * VoxelSize;
            int worldY = y * VoxelSize;

            // Sample the heightmap at the (x, z) position
            float sampledHeight = SampleHeightmap(worldX, worldZ);


            // Calculate the distance from the y position to the sampled height
            float distance = worldY - sampledHeight;


            // If the distance is positive, the point is above the surface, otherwise below
            return distance;
        }

        float SampleHeightmap(int x, int z)
        {
            float maxHeight = GridMetrics.height;

            if (Heightfield == null)
            {
                Debug.LogError("Heightmap texture is not assigned.");
                return 0f;
            }





            // Sample the heightmap texture at the given (u, v) coordinates
            Color pixelColor = Heightfield.GetPixel(x, z);

            // Assuming the texture is in grayscale where red channel represents height
            float heightValue = pixelColor.r;


            // Scale the height value to the desired maximum height
            float sampledHeight = heightValue * maxHeight;

            // Return the difference in height relative to the y position
            return sampledHeight;
        }
    }


    private void OnDrawGizmos()
    {
        if (_weights == null || _weights.Length == 0 || !EnableDebug)
        {
            return;
        }

        int pointsPerChunk = Mathf.CeilToInt((PointsPerChunk + 2) / VoxelSize);
        int height = Mathf.CeilToInt((GridMetrics.height + 1) / VoxelSize);

        for (int x = 0; x < pointsPerChunk; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < pointsPerChunk; z++)
                {
                    int index = x + pointsPerChunk * (y + height * z); // Calculate index correctly
                    float noiseValue = _weights[index].DistanceToSurface;

                    // Set the color based on the noise value
                    float val = Mathf.Clamp(noiseValue, 0, 1);
                    Gizmos.color = Color.Lerp(Color.black, Color.white, val);

                    // Draw the gizmo at the correct position scaled by voxelSize
                    Vector3 position = new Vector3(x, y, z) * VoxelSize;
                    Gizmos.DrawCube(position, Vector3.one * (VoxelSize * 0.5f));
                }
            }
        }
    }




    //struct decleration
    struct Triangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;

        public Color color_a;
        public Color color_b;
        public Color color_c;

        public static int SizeOf => (sizeof(float) * 3 * 3) + (sizeof(float) * 4 * 3);
    }

    public struct VoxelData_v2
    {
        public float DistanceToSurface;
        public ushort TexIndex;


        public VoxelData_v2(float dist, ushort index)
        {
            DistanceToSurface = dist;
            TexIndex = index;
        }

        public static int SizeOf => Marshal.SizeOf<VoxelData_v2>();
    }
}


