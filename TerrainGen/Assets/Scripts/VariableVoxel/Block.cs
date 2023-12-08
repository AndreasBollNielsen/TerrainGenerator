using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using static WorldData;

public class Block
{
    public int NumChunks;
    public int Width;
    Chunk_v chunk;
    public int X;
    public int Y;
    public bool Loaded;
    NativeArray<VoxelData_v2> voxelData;
    NativeList<Vector3> vertices;
    NativeList<int> triangles;
    VoxelData_v2[] tempVoxel;
    private int blockId;

    int offsetX;
    int offsetY;
    public Block(int _width, Vector2Int tilepos, int x, int y)
    {
        this.Width = _width;
        this.X = x;
        this.Y = y;

    }

    public Vector2 GetPosition()
    {
        return new Vector2(X, Y);
    }

    public VoxelData_v2[] GetVoxelData()
    {
        return tempVoxel;
    }

    public void AddChunk(Chunk_v _chunk)
    {
        chunk = _chunk;

    }

    public GameObject DestroyChunk()
    {
        if (chunk != null)
        {
            chunk.mesh.Clear();

            // Assuming chunkObject is a reference to the GameObject you want to destroy
            GameObject chunkGameObject = chunk.chunkObject;


            // Set the chunk reference to null to avoid further usage
            chunk = null;
           // Debug.Log("removing chunk");
            return chunkGameObject;
        }
        Debug.LogError("chunk reference missing");
        return null; // No chunk to destroy
    }

    public Chunk_v GetChunk()
    {
        return chunk;
    }
    public void SetBlockId(int _blockId)
    {
        blockId = _blockId;
    }
    public JobHandle GenerateMesh(NativeArray<float> heightMap, WorldData.TerrainData terrainData)
    {

        //if (X > 128 && Y > 128)
        //{
        //    return;
        //}


        //calc offset
        offsetX = (X - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);
        offsetY = (Y - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);

        //initialize voxelsize
        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt( Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));
       
        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int yVoxels = Mathf.CeilToInt((float)Constants.height / voxel_Size);
        int zVoxels = xVoxels;
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);


        Profiler.BeginSample("test_initialize memory");


        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.Persistent);
        vertices = new NativeList<Vector3>(allocator: Allocator.TempJob);
        triangles = new NativeList<int>(allocator: Allocator.TempJob);
        //  lowresHeightMap = new NativeArray<float>(heightMap.Length, allocator: Allocator.TempJob);

        Profiler.EndSample();


        Profiler.BeginSample("test_generateVoxels");
        GenerateVoxelStructure_Job voxelStructure_Job = new GenerateVoxelStructure_Job()
        {
            offsetX = offsetY,
            offsetZ = offsetX,
            voxelSize = voxel_Size,
            heightmapWidth = Constants.heightmapWidth,
            height = Constants.height,
            voxelHeight = yVoxels + 1,
            voxelWidth = xVoxels + 1,
            voxelData = voxelData,
            heightMap = heightMap,
        };
        var voxeljob = voxelStructure_Job.Schedule(totalVoxels, 64);
        voxeljob.Complete();


        Profiler.EndSample();

        //tempVoxel = new VoxelData_v2[totalVoxels];
        //voxelData.CopyTo(tempVoxel);

        Profiler.BeginSample("test_marchingcube");

        int numThreads = 16;
        MarchingCube_Job Meshjob = new MarchingCube_Job()
        {
            TerrainData = terrainData,
            voxelData = voxelData,
            voxelSize = voxel_Size,
            voxelWidth = xVoxels,
            voxelHeight = yVoxels,
            height = Constants.height,
            voxelsLength = totalVoxels,
            surfaceDensity = WorldData.surfaceDensity,
            width = Width,
            triangles = triangles,
            vertices = vertices,
            numThreads = numThreads,

        };
        JobHandle meshhandle = Meshjob.Schedule(numThreads, 64, voxeljob);




        JobHandle combinedJobs = JobHandle.CombineDependencies(voxeljob, meshhandle);


        
       // voxelData.Dispose();
        Profiler.EndSample();

        return combinedJobs;

    }

   
    public void SetMesh()
    {
        // Debug.Log("set mesh");
        //set mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.AsArray().ToArray();
        mesh.triangles = triangles.AsArray().ToArray();
        //  mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();

        Vector2Int offset = new Vector2Int(offsetX, offsetY);

        // Profiler.EndSample();

        voxelData.Dispose();
        vertices.Dispose();
        triangles.Dispose();



        //add mesh to gameobject
        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>();
        go.GetComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Terrain"); ;
        go.transform.position = new Vector3(offset.x, 0, offset.y);
        go.name = $"chunk_{offset.x}_{offset.y}";

        //create chunk object
        Chunk_v _chunk = new Chunk_v();
        _chunk.chunkObject = go;
        _chunk.mesh = mesh;
        _chunk.blockId = blockId;

        chunk = _chunk;
    }

    private void CalcMemory(int totalVoxels)
    {
        int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(VoxelData_v2));
        int totalSizeBytes = size * totalVoxels;

        float totalSizeMB = totalSizeBytes / (1024f * 1024f);
        float totalSizeGB = totalSizeMB / 1024f;

        Debug.Log($"Number of bytes: {totalSizeBytes}");
        Debug.Log($"Number of megabytes: {totalSizeMB} MB");
        Debug.Log($"Number of gigabytes: {totalSizeGB} GB");
    }



    public struct VoxelData_v2
    {
        public half DistanceToSurface;
        public ushort TexIndex;


        public VoxelData_v2(half dist, ushort index)
        {
            DistanceToSurface = dist;
            TexIndex = index;
        }
    }

    #region Multithread jobs
    [BurstCompile]
    public struct GenerateVoxelStructure_Job : IJobParallelFor
    {

        [ReadOnly] public NativeArray<float> heightMap;
       
        [NativeDisableParallelForRestriction]
        public NativeArray<VoxelData_v2> voxelData;
        public int offsetX;
        public int offsetZ;
        public int voxelSize;
        public int heightmapWidth;
        public int height;
        public int voxelWidth;
        public int voxelHeight;
        int z;
        int y;
        int x;
        int xOffset;
        int zOffset;
        int voxelindex;
        Color sampledColor;
        int counter;
        public void Execute(int index)
        {

            //get x & z coords
            z = index % voxelWidth;
            y = (index / voxelWidth) % voxelHeight;
            x = index / (voxelWidth * voxelHeight);

            // Apply the x and z offsets here
            xOffset = offsetX;
            zOffset = offsetZ;

            // Multiply the voxel positions by the voxel size first
            x *= voxelSize;
            z *= voxelSize;
            y *= voxelSize;

            // Apply offsets
            x += xOffset;
            z += zOffset;


            //sample the height map
            voxelindex = (x * heightmapWidth) + z;

            if (voxelindex >= heightMap.Length)
            {

                // Debug.LogError($"x {x} y {y} z {z}");
                // Debug.Log($"offset: {offsetX}:{offsetZ}");
                return;
            }

            float sampledColor = heightMap[voxelindex];
            float sampledHeight = sampledColor;
            float scaledHeight = sampledHeight * height;

            float dist = y - scaledHeight;
            voxelData[index] = new VoxelData_v2((half)dist, 0);

            counter++;



        }
    }


    [BurstCompile]
    public struct MarchingCube_Job : IJobParallelFor
    {
        public int voxelsLength;
        public int voxelWidth;
        public int voxelHeight;
        public int voxelSize;
        public float width;
        public float height;
        public Vector2Int offset;

        [ReadOnly]
        public NativeArray<VoxelData_v2> voxelData;
        [NativeDisableParallelForRestriction]
        public NativeList<Vector3> vertices;
        [NativeDisableParallelForRestriction]
        public NativeList<int> triangles;
        [ReadOnly]
        public WorldData.TerrainData TerrainData;
        [ReadOnly]
        public float surfaceDensity;
        public int numThreads;
       
        public void Execute(int index)
        {
            int chunkSize = voxelsLength / numThreads;
            int startIndex = index * chunkSize;
            int endIndex = math.min(startIndex + chunkSize, voxelsLength);


            // Debug.Log($"start; {startIndex} end: {endIndex}");



            for (int i = startIndex; i < endIndex; i++)
            {

                int x = Mathf.FloorToInt(i % voxelWidth);
                int y = Mathf.FloorToInt((i / voxelWidth) % voxelHeight);
                int z = Mathf.FloorToInt(i / (voxelWidth * voxelHeight));

                // Calculate the position of the voxel in world space
                Vector3 voxelPosition = new Vector3(
                    x * voxelSize,
                    y * voxelSize,
                    z * voxelSize
                );

                //  Debug.Log($"position: {voxelPosition}");
                //use marchingcube algorithm to generate triangles and vertices
                if (voxelPosition.x < width && voxelPosition.z < width && voxelPosition.y < height)
                {
                    // Debug.Log($"position: {voxelPosition}");
                    MarchCube(voxelPosition, voxelSize, (int)(width));

                }

            }



            


        }

        void MarchCube(Vector3 position, int voxelSize, int width)
        {

            //sample terrain at each cube corner
            NativeArray<half> cubes = new NativeArray<half>(8, allocator: Allocator.Temp);

            for (int j = 0; j < 8; j++)
            {
                //samples terrain data at neigboring cells
                Vector3 worldpos = position + (TerrainData.CornerTable[j] * voxelSize);
                cubes[j] = GetVoxelSample(worldpos, voxelSize, voxelData);

            }

            //get configuration index of the cube
            int configIndex = GetCubeConfiguration(cubes);


            //if position is outside of cube
            if (configIndex == 0 || configIndex == 255)
            {
                // Debug.Log("outside cube");
                return;
            }

            int edgeIndex = 0;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int indice = TerrainData.GetTriangleTableValue(configIndex, edgeIndex);
                    // Debug.Log(indice);
                    //return if end of indices
                    if (indice == -1)
                    {
                        return;
                    }

                    //get top and bottom of cube
                    Vector3 vert1 = position + TerrainData.CornerTable[TerrainData.GetEdgeIndexesValue(indice, 0)] * voxelSize;
                    Vector3 vert2 = position + TerrainData.CornerTable[TerrainData.GetEdgeIndexesValue(indice, 1)] * voxelSize;

                    Vector3 vertexPosition;

                    //get terrain values at either end of the edge
                    float vert1Sample = cubes[TerrainData.GetEdgeIndexesValue(indice, 0)];
                    float vert2Sample = cubes[TerrainData.GetEdgeIndexesValue(indice, 1)];

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
                    triangles.Add(vertIndex);




                    edgeIndex++;
                }
            }






            cubes.Dispose();
        }

        int VertForIndice(Vector3 vert, Vector3 voxelPos)
        {


            //loop through the vertices
            for (int i = 0; i < vertices.Length; i++)
            {
                //check if vertex already exists in the list. return it exists
                if (vertices[i] == vert)
                {
                    return i;
                }
            }




            //  Debug.Log("adding vert");

            // if it does not exist in list, add it to the list
            vertices.Add(vert);


            return vertices.Length - 1;
        }

        int GetCubeConfiguration(NativeArray<half> cube)
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


        half GetVoxelSample(Vector3 worldposition, int voxelSize, NativeArray<VoxelData_v2> voxelData)
        {
            int x = Mathf.FloorToInt(worldposition.x / voxelSize);
            int y = Mathf.FloorToInt(worldposition.y / voxelSize);
            int z = Mathf.FloorToInt(worldposition.z / voxelSize);

            int voxelsWidth = voxelWidth + 1;
            int voxelsHeight = voxelHeight + 1;

            int voxelIndex = x + y * voxelsWidth + z * (voxelsWidth * voxelsHeight);
            if (voxelIndex >= voxelData.Length)
            {
                Debug.LogError($"worldpos: {worldposition} position: {x}:{y}:{z} voxelwidth: {voxelsWidth} voxelheight: {voxelsHeight} voxelSize: {voxelSize}");
                //  UnityEditor.EditorApplication.isPlaying = false;


                return (half)(-1.0f);
            }
            return voxelData[voxelIndex].DistanceToSurface;
        }
    }

    [BurstCompile]
    public struct ConvertHeightMap : IJobParallelFor
    {
        public NativeArray<Color> highresMap;
        public NativeArray<float> lowresMap;
        public void Execute(int index)
        {
            Color value = highresMap[index];
            lowresMap[index] = value.r;
        }
    }

    public struct DisposeData : IJob
    {
        public NativeArray<VoxelData_v2> data;
        public void Execute()
        {
            data.Dispose();
        }
    }
    #endregion
}

