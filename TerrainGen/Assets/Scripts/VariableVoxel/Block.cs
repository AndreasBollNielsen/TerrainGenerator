using Mono.Cecil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using static MeshGenerator;
using static Unity.Collections.AllocatorManager;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using static VoxelGenerator;
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

            return chunkGameObject;
        }

        return null; // No chunk to destroy
    }

    public Chunk_v GetChunk()
    {
        return chunk;
    }
    public void SetBlockId(int blockId)
    {
        chunk.blockId = blockId;
    }
    public JobHandle GenerateMesh(NativeArray<Color> heightMap, WorldData.TerrainData terrainData)
    {

        //if (X > 128 && Y > 128)
        //{
        //    return;
        //}


        //calc offset
        int offsetX = (X - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);
        int offsetY = (Y - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);

        //initialize voxelsize
        int voxel_Size = Mathf.Clamp((Width / Constants.minChunkWidth) * Constants.minVoxelSize, 1, 256);

        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int yVoxels = Mathf.CeilToInt((float)Constants.height / voxel_Size);
        int zVoxels = xVoxels;
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);


        Profiler.BeginSample("test_voxelgenerating");


        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.TempJob);
        vertices = new NativeList<Vector3>(allocator: Allocator.TempJob);
        triangles = new NativeList<int>(allocator: Allocator.TempJob);

        Profiler.EndSample();
        Profiler.BeginSample("test_runningJob");


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
        var job = voxelStructure_Job.Schedule(totalVoxels, 4);
        job.Complete();

        //tempVoxel = new VoxelData_v2[totalVoxels];
        //voxelData.CopyTo(tempVoxel);

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

        };
        JobHandle meshhandle = Meshjob.Schedule(totalVoxels, job);
        JobHandle combinedJobs = JobHandle.CombineDependencies(job, meshhandle);
        combinedJobs.Complete();
        Profiler.EndSample();



        Profiler.BeginSample("test_setmesh");
        //set mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.AsArray().ToArray();
        mesh.triangles = triangles.AsArray().ToArray();
        //  mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();

        Vector2Int offset = new Vector2Int(offsetX, offsetY);
        SetMesh(mesh, offset);
        Profiler.EndSample();

        voxelData.Dispose();
        vertices.Dispose();
        triangles.Dispose();



        return combinedJobs;
    }

    public void DisposeData()
    {
        voxelData.Dispose();
    }

    public void SetMesh(Mesh mesh, Vector2Int offset)
    {
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
        //  _chunk.blockId = blockId;

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

        [ReadOnly] public NativeArray<Color> heightMap;
        // [NativeDisableParallelForRestriction]
        [WriteOnly]
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

            sampledColor = heightMap[voxelindex];
            float sampledHeight = sampledColor.r;
            float scaledHeight = sampledHeight * height;

            float dist = y - scaledHeight;
            voxelData[index] = new VoxelData_v2((half)dist, 0);

            counter++;



        }
    }

    #endregion
}

