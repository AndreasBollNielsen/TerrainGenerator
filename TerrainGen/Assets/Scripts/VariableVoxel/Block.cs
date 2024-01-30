using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
//using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
//using UnityEditor.Rendering.LookDev;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using static Block;

using static WorldData;

public class Block
{
    public int NumChunks;
    public int Width;
    public float MaxHeight;
    Chunk_v chunk;
    public int X;
    public int Y;
    public bool Loaded;
    NativeArray<VoxelData_v2> voxelData;
    NativeList<Vector3> nativeVertices;
    NativeList<int> nativeTriangles;
    NativeList<Color> colors;
    NativeParallelMultiHashMap<Vector3, int> lookupTable;

    private int blockId;
    JobHandle combinedHandle;
    int offsetX;
    int offsetY;
    bool voxelsDisposed = false;
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
        if (voxelData == null)
        {
            return null;
        }

        VoxelData_v2[] voxels = new VoxelData_v2[voxelData.Length];
        voxelData.CopyTo(voxels);
        return voxels;
    }

    public void AddChunk(Chunk_v _chunk)
    {
        chunk = _chunk;

    }

    public Block HideBlock()
    {
        if (chunk != null)
        {
            Block oldBlock = this;
            return oldBlock;
        }
        return null;
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
        // Debug.LogError("chunk reference missing");
        return null; // No chunk to destroy
    }

    public Chunk_v GetChunk()
    {
        return chunk;
    }

    public JobHandle GetJob()
    {
        return combinedHandle;
    }

    public void SetBlockId(int _blockId)
    {
        blockId = _blockId;
    }



    public void GenerateMesh(NativeArray<float> heightMap, WorldData.TerrainData terrainData)
    {

        //if (X > 128 && Y > 128)
        //{
        //    return;
        //}
        Unity.Mathematics.Random randomGen = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(1, 100000));
        float4 randCol = randomGen.NextFloat4(0, 1);

        //calc offset
        offsetX = (X - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);
        offsetY = (Y - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);

        //initialize voxelsize
        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));

        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int zVoxels = xVoxels;



        //set total voxel size
        int yVoxels = Mathf.CeilToInt((MaxHeight + 1) / voxel_Size);
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
        //int totalVertices = (xVoxels * 3) * (zVoxels * 3);

        Profiler.BeginSample("test_initialize memory");


        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.Persistent);
        nativeVertices = new NativeList<Vector3>(2500, allocator: Allocator.TempJob);
        NativeList<Triangle> TempVertices = new NativeList<Triangle>(9000, allocator: Allocator.TempJob);
        nativeTriangles = new NativeList<int>(1800 * 3, allocator: Allocator.TempJob);
        colors = new NativeList<Color>(allocator: Allocator.TempJob);


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
        Profiler.EndSample();





        Profiler.BeginSample("test_marchingcube");


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

            vertices = TempVertices.AsParallelWriter(),
            randcol = randCol


        };

        JobHandle meshhandle = Meshjob.Schedule(totalVoxels, 64, voxeljob);
        meshhandle.Complete();

        Profiler.EndSample();




        Profiler.BeginSample("test_vertexprocess");

        ProcessVertices vertexJob = new ProcessVertices()
        {
            Tempvertices = nativeVertices,
            vertices = TempVertices,
            triangles = nativeTriangles,
            colors = colors

        };

        JobHandle vertexHandle = vertexJob.Schedule(TempVertices.Length, meshhandle);
        Profiler.EndSample();
        JobHandle combinedJobs = JobHandle.CombineDependencies(voxeljob, meshhandle, vertexHandle);
        combinedHandle = combinedJobs;

        combinedJobs.Complete();
        TempVertices.Dispose();



        SetMesh();
        UnloadVoxels();
        Loaded = true;






    }

    public void RebuildVoxels(List<float> heightMap)
    {
        Profiler.BeginSample("test_initVariables");
        voxelsDisposed = false;
        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));

        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int zVoxels = xVoxels;
        int yVoxels = Mathf.CeilToInt((MaxHeight + 1) / voxel_Size);
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
        Profiler.EndSample();

        Profiler.BeginSample("test_init nativearrays");
        NativeArray<float> nativeHeightMap = new NativeArray<float>(heightMap.ToArray(), allocator: Allocator.TempJob);
        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.Persistent);

        Profiler.BeginSample("test_rebuildvoxels");
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
            heightMap = nativeHeightMap,

        };
        var initVoxels = voxelStructure_Job.Schedule(totalVoxels, 64);

        initVoxels.Complete();
        nativeHeightMap.Dispose();
        Profiler.EndSample();
        // Debug.Log($"voxels initialized {voxelData.Length}");
    }

    public void ModifyVoxels(List<Vector3Int> modifiedVoxels, half direction)
    {
        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));

        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int zVoxels = xVoxels;
        int yVoxels = Mathf.CeilToInt((MaxHeight + 1) / voxel_Size);
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);

        NativeArray<Vector3Int> modified = new NativeArray<Vector3Int>(modifiedVoxels.ToArray(), allocator: Allocator.TempJob);

        ModifyVoxelStructure_job modifyJob = new ModifyVoxelStructure_job
        {
            direction = direction,
            voxelData = voxelData,
            modifiedVoxels = modified,
            voxelSize = voxel_Size,
            voxelHeight = yVoxels + 1,
            voxelWidth = xVoxels + 1

        };

        var modifyHandle = modifyJob.Schedule(modified.Length, 64);
        modifyHandle.Complete();
        modified.Dispose();
    }
    public void RebuildMesh(WorldData.TerrainData terrainData,GameObject oldChunk)
    {
        Debug.Log("rebuilding mesh");
        nativeVertices = new NativeList<Vector3>(2500, allocator: Allocator.TempJob);
        NativeList<Triangle> TempVertices = new NativeList<Triangle>(9000, allocator: Allocator.TempJob);
        nativeTriangles = new NativeList<int>(1800 * 3, allocator: Allocator.TempJob);
        colors = new NativeList<Color>(allocator: Allocator.TempJob);

        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int zVoxels = xVoxels;
        int yVoxels = Mathf.CeilToInt((MaxHeight + 1) / voxel_Size);
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);


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

            vertices = TempVertices.AsParallelWriter(),
            randcol = new float4(1, 1, 1, 1),


        };

        JobHandle meshhandle = Meshjob.Schedule(totalVoxels, 64);
        meshhandle.Complete();
        Debug.Log("marching cube done");
        ProcessVertices vertexJob = new ProcessVertices()
        {
            Tempvertices = nativeVertices,
            vertices = TempVertices,
            triangles = nativeTriangles,
            colors = colors

        };

        JobHandle vertexHandle = vertexJob.Schedule(TempVertices.Length, meshhandle);

        JobHandle combinedJobs = JobHandle.CombineDependencies(meshhandle, vertexHandle);
        combinedHandle = combinedJobs;
        combinedJobs.Complete();
        Debug.Log("vertex job complete");
        TempVertices.Dispose();

        SetMesh();
        Loaded = true;
        Debug.Log("mesh generated");
        VoxelGenerator.Instance.DestroyOldChunk(oldChunk);
    }

    public void UnloadVoxels()
    {
        if (!voxelsDisposed)
        {
            voxelData.Dispose();
            voxelsDisposed = true;

        }
       // Debug.Log("voxels disposed");
    }

    public void SetMesh()
    {
        //set mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = nativeVertices.AsArray().ToArray();
        mesh.triangles = nativeTriangles.AsArray().ToArray();
        mesh.colors = colors.AsArray().ToArray();
        mesh.RecalculateNormals();


        Vector2Int offset = new Vector2Int(offsetX, offsetY);


        colors.Dispose();
        nativeVertices.Dispose();
        nativeTriangles.Dispose();




        //add mesh to gameobject
        GameObject go = new GameObject();
        go.AddComponent<MeshFilter>();
        go.GetComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = Resources.Load<Material>("Terrain"); ;
        go.transform.position = new Vector3(offset.x, 0, offset.y);
        go.name = $"chunk_{offset.x}_{offset.y}";
        go.tag = "Terrain";

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

    public void CalculateHeight(NativeArray<float> heightMap)
    {
        offsetX = (X - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);
        offsetY = (Y - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);

        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));

        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);

        NativeArray<float> heightResult = new NativeArray<float>(1, allocator: Allocator.TempJob);
        CalculateHeightJob heightJob = new CalculateHeightJob()
        {
            heightMap = heightMap,
            ResultHeight = heightResult,
            heightmapWidth = Constants.heightmapWidth,
            offsetX = offsetY,
            offsetZ = offsetX,
            height = Constants.height,
            voxelWidth = xVoxels,
            voxelSize = voxel_Size,


        };
        JobHandle deps = new JobHandle();
        JobHandle heightHandle = heightJob.Schedule(Width * Width, deps);
        heightHandle.Complete();

        float maxHeight = heightResult[0] + Width;
        MaxHeight = maxHeight;
        heightResult.Dispose();
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

            // counter++;



        }
    }


    public struct ModifyVoxelStructure_job : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<VoxelData_v2> voxelData;
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3Int> modifiedVoxels;
        public half direction;
        public int voxelSize;
        public int voxelWidth;
        public int voxelHeight;

        public void Execute(int index)
        {
            Vector3Int voxelpos = modifiedVoxels[index];

            int x = Mathf.FloorToInt(voxelpos.x / voxelSize);
            int y = Mathf.FloorToInt(voxelpos.y / voxelSize);
            int z = Mathf.FloorToInt(voxelpos.z / voxelSize);
            int voxelsWidth = voxelWidth;
            int voxelsHeight = voxelHeight;

            int voxelIndex = x + y * voxelsWidth + z * (voxelsWidth * voxelsHeight);
          //  Debug.Log($"index: {voxelIndex} length {voxelData.Length}");
            if (voxelIndex < voxelData.Length)
            {

                VoxelData_v2 modifiedData = voxelData[voxelIndex];
                modifiedData.DistanceToSurface = direction;
                modifiedData.TexIndex = 1;
                voxelData[voxelIndex] = modifiedData;
            }
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
        public NativeList<Triangle>.ParallelWriter vertices;

        [ReadOnly]
        public WorldData.TerrainData TerrainData;

        [ReadOnly]
        public float surfaceDensity;
        public float4 randcol;

        public void Execute(int index)
        {
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

        void MarchCube(Vector3 position, int voxelSize, int width)
        {
            int configIndex = 0;
            Vector3 worldPos;
            for (int j = 0; j < 8; j++)
            {
                //samples terrain data at neighboring cells
                worldPos = position + (TerrainData.CornerTable[j] * voxelSize);
                half voxelSample = GetVoxelSample(worldPos, voxelSize);

                // Update configuration index using bitmask bool operation
                if (voxelSample > surfaceDensity)
                {
                    configIndex |= 1 << j;
                }
            }






            //if position is outside of cube
            if (configIndex == 0 || configIndex == 255)
            {
                // Debug.Log("outside cube");
                return;
            }


            int edgeIndex = 0;
            int indice = 0;
            Vector3 vert1;
            Vector3 vert2;
            Vector3 vertexPosition;
            float vert1Sample;
            float vert2Sample;
            float diff;

            for (int i = 0; i < 5; i++)
            {

                Triangle triangle = new Triangle { };
                for (int j = 0; j < 3; j++)
                {
                    indice = TerrainData.GetTriangleTableValue(configIndex, edgeIndex);
                    // Debug.Log(indice);
                    //return if end of indices
                    if (indice == -1)
                    {
                        return;
                    }

                    //get top and bottom of cube
                    vert1 = position + TerrainData.CornerTable[TerrainData.GetEdgeIndexesValue(indice, 0)] * voxelSize;
                    vert2 = position + TerrainData.CornerTable[TerrainData.GetEdgeIndexesValue(indice, 1)] * voxelSize;



                    //get terrain values at either end of the edge
                    vert1Sample = GetVoxelSample(vert1, voxelSize);
                    vert2Sample = GetVoxelSample(vert2, voxelSize);

                    //calculate the difference between terrain values
                    diff = vert2Sample - vert1Sample;

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



                    if (j == 0)
                    {
                        triangle.a = vertexPosition;
                        triangle.col_a = ColorSample(diff);
                        triangle.col_a = randcol;
                    }
                    else if (j == 1)
                    {
                        triangle.b = vertexPosition;
                        triangle.col_b = ColorSample(diff);
                        triangle.col_b = randcol;
                    }
                    else if (j == 2)
                    {
                        triangle.c = vertexPosition;
                        triangle.col_c = ColorSample(diff);
                        triangle.col_c = randcol;
                    }

                    edgeIndex++;
                }


                vertices.AddNoResize(triangle);
            }

        }

        half GetVoxelSample(Vector3 worldposition, int voxelSize)
        {
            int x = Mathf.FloorToInt(worldposition.x / voxelSize);
            int y = Mathf.FloorToInt(worldposition.y / voxelSize);
            int z = Mathf.FloorToInt(worldposition.z / voxelSize);

            int voxelsWidth = voxelWidth + 1;
            int voxelsHeight = voxelHeight + 1;

            int voxelIndex = x + y * voxelsWidth + z * (voxelsWidth * voxelsHeight);
            if (voxelIndex >= voxelData.Length)
            {
                // Debug.LogError($"worldpos: {worldposition} position: {x}:{y}:{z} voxelwidth: {voxelsWidth} voxelheight: {voxelsHeight} voxelSize: {voxelSize}");
                //  UnityEditor.EditorApplication.isPlaying = false;


                return (half)(-1.0f);
            }

            return voxelData[voxelIndex].DistanceToSurface;
        }

        float4 ColorSample(float sample)
        {

            switch (sample)
            {
                case >= 0f:
                    return new float4(1, 0, 0, 0);

                case < 5.0f:
                    return new float4(0, 1, 0, 0);
                //case < -5f:
                //    return new float4(0, 0, 1, 0);
                default:
                    return new float4(0, 0, 0, 1);

            }
        }
    }
    [BurstCompile]
    public struct ProcessVertices : IJobFor
    {
        [NativeDisableParallelForRestriction, ReadOnly]
        public NativeList<Triangle> vertices;
        [NativeDisableParallelForRestriction]
        public NativeList<int> triangles;
        [NativeDisableParallelForRestriction]
        public NativeList<Vector3> Tempvertices;
        [NativeDisableParallelForRestriction]
        public NativeList<Color> colors;

        public void Execute(int index)
        {


            AddColor(vertices[index].a, vertices[index].col_a);
            triangles.Add(Tempvertices.Length - 1);
            AddColor(vertices[index].b, vertices[index].col_b);
            triangles.Add(Tempvertices.Length - 1);
            AddColor(vertices[index].c, vertices[index].col_c);
            triangles.Add(Tempvertices.Length - 1);





        }

        public void AddColor(float3 vertex, float4 col)
        {

            Color vertexColor = new Color(col.x, col.y, col.z, col.w);
            Tempvertices.Add(vertex);
            colors.Add(vertexColor);
        }
    }

    [BurstCompile]
    public struct CalculateHeightJob : IJobFor
    {
        [ReadOnly] public NativeArray<float> heightMap;
        [NativeDisableParallelForRestriction] public NativeArray<float> ResultHeight;

        [ReadOnly] public int voxelSize;
        [ReadOnly] public int heightmapWidth;
        [ReadOnly] public int offsetX;
        [ReadOnly] public int offsetZ;
        [ReadOnly] public int voxelWidth;
        [ReadOnly] public int height;

        int z;
        int x;
        float lastval;

        public void Execute(int index)
        {
            //get x & z coords
            z = index % voxelWidth;
            x = index / (voxelWidth * voxelWidth);



            // Multiply the voxel positions by the voxel size first
            x *= voxelSize;
            z *= voxelSize;


            // Apply offsets
            x += offsetX;
            z += offsetZ;


            //sample the height map
            int pixelIndex = (x * heightmapWidth) + z;

            if (pixelIndex < heightMap.Length && pixelIndex >= 0)
            {
                float sampledColor = heightMap[pixelIndex];
                float sampledHeight = sampledColor;
                float scaledHeight = sampledHeight * height;

                if (scaledHeight > lastval)
                {
                    ResultHeight[0] = scaledHeight;
                    lastval = scaledHeight;
                    // Debug.Log($"height: {lastval} positions: {x}:{z}");
                }
            }

        }
    }



    public struct Triangle
    {
        public float3 a;
        public float3 b;
        public float3 c;

        public float4 col_a;
        public float4 col_b;
        public float4 col_c;
    }

    //deprecated
    //public struct Indices
    //{

    //    public int index1;
    //    public int index2;
    //    public int index3;

    //    public void SetIndex(int index, int value)
    //    {
    //        switch (index)
    //        {
    //            case 0:
    //                index1 = value;
    //                break;
    //            case 1:
    //                index2 = value;
    //                break;
    //            case 2:
    //                index3 = value;
    //                break;
    //            default:
    //                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range for MyStruct.");
    //        }
    //    }

    //    public int GetIndice(int index, int altValue = -1)
    //    {
    //        int selectedVal;
    //        switch (index)
    //        {
    //            case 0:
    //                selectedVal = index1;
    //                break;

    //            case 1:
    //                selectedVal = index2;
    //                break;
    //            case 2:
    //                selectedVal = index3;
    //                break;
    //            default:
    //                throw new ArgumentOutOfRangeException(nameof(index), "Index out of range for MyStruct.");
    //        }

    //        if (selectedVal > 0)
    //        {
    //            return selectedVal;
    //        }

    //        return altValue;
    //    }

    //}
    #endregion
}

