using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using static VoxelGenerator;

public class Block
{
    public int NumChunks;
    public int Width;
    Chunk_v chunk;
    public int X;
    public int Y;
    public bool Loaded;
    NativeArray<VoxelData_v2> voxelData;


    public Block(int _width, Vector2Int tilepos, int x, int y, NativeArray<Color> heightMap)
    {
        this.Width = _width;
        this.X = x;
        this.Y = y;



        // GenerateMesh(heightMap);


    }

    public Vector2 GetPosition()
    {
        return new Vector2(X, Y);
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

    public JobHandle GenerateMesh(NativeArray<Color> heightMap)
    {

        //if (X > 128 && Y > 128)
        //{
        //    return;
        //}
        

       // Profiler.BeginSample("test_init");
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
       // Profiler.EndSample();
       
        Profiler.BeginSample("test_voxelgenerating");

      
        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.TempJob);

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
        var job = voxelStructure_Job.Schedule(totalVoxels, 64);
        


        job.Complete();
        voxelData.Dispose();




        return job;
    }

    public void DisposeData()
    {
        voxelData.Dispose();
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

            //if (voxelindex >= heightMap.Length)
            //{

            //    // Debug.LogError($"x {x} y {y} z {z}");
            //    // Debug.Log($"offset: {offsetX}:{offsetZ}");
            //    return;
            //}

            // sampledColor = heightMap[voxelindex];
            float sampledHeight = sampledColor.r;
            float scaledHeight = sampledHeight * height;

            float dist = y - scaledHeight;
            // voxelData[index] = new VoxelData_v2((half)dist, 0);

        }
    }

    #endregion
}

