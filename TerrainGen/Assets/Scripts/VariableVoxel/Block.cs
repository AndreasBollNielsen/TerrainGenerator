using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using UnityEngine;
using static VoxelGenerator;

public class Block
{
    public int NumChunks;
    public int Width;
    Chunk_v chunk;
    public int X;
    public int Y;
    public bool Loaded;



    public Block(int _width, Vector2Int tilepos,NativeArray<Color> heightMap)
    {
        this.Width = _width;



        GenerateMesh(heightMap);


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

    private void GenerateMesh(NativeArray<Color> heightMap)
    {
        

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

        NativeArray<VoxelData> voxelData = new NativeArray<VoxelData>(totalVoxels, allocator: Allocator.TempJob);

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
        var job = voxelStructure_Job.Schedule(voxelData.Length, 64);
        job.Complete();
    }
}
