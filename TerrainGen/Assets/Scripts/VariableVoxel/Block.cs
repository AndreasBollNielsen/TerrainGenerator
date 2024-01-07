using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
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
    Chunk_v chunk;
    public int X;
    public int Y;
    public bool Loaded;
    NativeArray<VoxelData_v2> voxelData;
    NativeList<Vector3> nativeVertices;
    NativeList<int> nativeTriangles;
    NativeParallelMultiHashMap<Vector3, int> lookupTable;
    //NativeList<Triangle> Triangles;
    // NativeArray<half> cubes;
    // VoxelData_v2[] tempVoxel;
    private int blockId;
    JobHandle combinedHandle;
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

    //public VoxelData_v2[] GetVoxelData()
    //{
    //    return tempVoxel;
    //}

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

    public void testjob(object[] parameters)
    {
        NativeArray<float> heightMap = (NativeArray<float>)parameters[0];
        WorldData.TerrainData terrainData = (WorldData.TerrainData)parameters[1];

        //calc offset
        offsetX = (X - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);
        offsetY = (Y - Width / 2 + Constants.heightmapWidth - 1) % (Constants.heightmapWidth - 1);

        //initialize voxelsize
        float chunkWidth = (float)Width / Constants.minChunkWidth;
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));

        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int yVoxels = Mathf.CeilToInt((float)Constants.height / voxel_Size);
        int zVoxels = xVoxels;
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);

        int totalVertices = (xVoxels * 3) * (zVoxels * 3);



        Profiler.BeginSample("test_initialize memory");
        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.TempJob);
        nativeVertices = new NativeList<Vector3>(allocator: Allocator.TempJob);
        nativeTriangles = new NativeList<int>(allocator: Allocator.TempJob);
        lookupTable = new NativeParallelMultiHashMap<Vector3, int>(totalVertices, allocator: Allocator.Persistent);
        // Triangles = new NativeList<Triangle>(allocator: Allocator.TempJob);

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

        Profiler.BeginSample("test_marchingcubeV2");
        MarchingCube_Job_V2 testjob = new MarchingCube_Job_V2()
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
            triangles = nativeTriangles,
            vertices = nativeVertices,
            lookup = lookupTable


        };
        JobHandle meshhandle2 = testjob.Schedule(totalVoxels, 64, voxeljob);
        // meshhandle2.Complete();
        Profiler.EndSample();



        // Profiler.BeginSample("test_marchingcube");
        //int numThreads = 8;
        //MarchingCube_Job Meshjob = new MarchingCube_Job()
        //{
        //    TerrainData = terrainData,
        //    voxelData = voxelData,
        //    voxelSize = voxel_Size,
        //    voxelWidth = xVoxels,
        //    voxelHeight = yVoxels,
        //    height = Constants.height,
        //    voxelsLength = totalVoxels,
        //    surfaceDensity = WorldData.surfaceDensity,
        //    width = Width,
        //    triangles = triangles,
        //    vertices = vertices,
        //    numThreads = numThreads,

        //};
        //JobHandle meshhandle = Meshjob.Schedule(numThreads, 64, meshhandle2);
        //meshhandle.Complete();
        // Profiler.EndSample();






        JobHandle combinedJobs = JobHandle.CombineDependencies(voxeljob, meshhandle2);
        combinedHandle = combinedJobs;


        //  yield return combinedHandle;
        combinedHandle.Complete();



        //  UnityEditor.EditorApplication.isPaused = true;
        SetMesh();
        Loaded = true;






    }

    public void GenerateMesh(NativeArray<float> heightMap, WorldData.TerrainData terrainData)
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
        int voxel_Size = Mathf.RoundToInt(Mathf.Clamp(chunkWidth * Constants.minVoxelSize, 1, 256));

        //Set voxelSize
        int xVoxels = Mathf.CeilToInt((Width + 1) / voxel_Size);
        int zVoxels = xVoxels;

        Profiler.BeginSample("test_heightCalc");
        //quick job to calculate max voxel height
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

        float maxHeight = heightResult[0] + 64;
        heightResult.Dispose();
        Profiler.EndSample();

        //set total voxel size
        int yVoxels = Mathf.CeilToInt((maxHeight + 1) / voxel_Size);
        int totalVoxels = (xVoxels + 1) * (yVoxels + 1) * (zVoxels + 1);
        int totalVertices = (xVoxels * 3) * (zVoxels * 3);

        Profiler.BeginSample("test_initialize memory");


        voxelData = new NativeArray<VoxelData_v2>(totalVoxels, allocator: Allocator.Persistent);
        nativeVertices = new NativeList<Vector3>(2500, allocator: Allocator.TempJob);
        NativeList<Triangle> TempVertices = new NativeList<Triangle>(9000,allocator: Allocator.TempJob);
        NativeList<Indices> indices = new NativeList<Indices>(allocator: Allocator.TempJob);
        nativeTriangles = new NativeList<int>(1800 * 3, allocator: Allocator.TempJob);
        lookupTable = new NativeParallelMultiHashMap<Vector3, int>(totalVertices, allocator: Allocator.Persistent);

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

        int numThreads = 8;
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
            indices = indices,
            vertices = TempVertices.AsParallelWriter(),
            numThreads = numThreads,
            lookup = lookupTable,


        };

        JobHandle meshhandle = Meshjob.Schedule(totalVoxels, 64, voxeljob);
        meshhandle.Complete();
        //  Debug.Log($"lookup table {lookupTable.Count()} vertices {TempVertices.Length}");
        Profiler.EndSample();

        //test brute force sort
        //for (int i = 0; i < indices.Length; i++)
        //{
        //   Debug.Log($"index: {i} vetIndex1:{indices[i].index1} vetIndex2:{indices[i].index2} vetIndex3:{indices[i].index3} ");
        //}


        Profiler.BeginSample("test_vertexprocess");

        int numTriplets = TempVertices.Length / 3;
        ProcessVertices vertexJob = new ProcessVertices()
        {
            Tempvertices = nativeVertices,
            vertices = TempVertices,
            triangles = nativeTriangles,
            indices = indices,
            lookup = lookupTable
        };

        JobHandle vertexHandle = vertexJob.Schedule(TempVertices.Length, meshhandle);
        Profiler.EndSample();
        JobHandle combinedJobs = JobHandle.CombineDependencies(voxeljob, meshhandle, vertexHandle);
        combinedHandle = combinedJobs;

        combinedJobs.Complete();
       // nativeVertices.CopyFrom(TempVertices);
        TempVertices.Dispose();
        indices.Dispose();
        SetMesh();
        Loaded = true;






    }


    public void SetMesh()
    {






        //set mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = nativeVertices.AsArray().ToArray();
        mesh.triangles = nativeTriangles.AsArray().ToArray();

        //  mesh.colors = colors.ToArray();
        mesh.RecalculateNormals();

        Vector2Int offset = new Vector2Int(offsetX, offsetY);

        // Profiler.EndSample();

        nativeVertices.Dispose();
        nativeTriangles.Dispose();
        voxelData.Dispose();
        lookupTable.Dispose();
        // Debug.Log("nativedata disposed");
        //Triangles.Dispose();



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
        [NativeDisableParallelForRestriction]
        public NativeList<Indices> indices;
        [ReadOnly]
        public WorldData.TerrainData TerrainData;
        [NativeDisableParallelForRestriction]
        public NativeParallelMultiHashMap<Vector3, int> lookup;
        [ReadOnly]
        public float surfaceDensity;
        public int numThreads;

        public void Execute(int index)
        {
            //int chunkSize = voxelsLength / numThreads;
            //int startIndex = iterator * chunkSize;
            //int endIndex = math.min(startIndex + chunkSize, voxelsLength);


            // Debug.Log($"start; {startIndex} end: {endIndex}");

            //for (int index = startIndex; index < endIndex; index++)
            //{

            //}



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

            //sample terrain at each cube corner
            //NativeArray<half> cubes = new NativeArray<half>(8, allocator: Allocator.Temp);

            //for (int j = 0; j < 8; j++)
            //{
            //    //samples terrain data at neigboring cells
            //    Vector3 worldpos = position + (TerrainData.CornerTable[j] * voxelSize);
            //    cubes[j] = GetVoxelSample(worldpos, voxelSize, voxelData);

            //}

            ////get configuration index of the cube
            //int configIndex = GetCubeConfiguration(cubes);


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
                Indices Currentindices = new Indices { index1 = -1, index2 = -1, index3 = -1 };
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

                    //  Vector3 vertexPosition;

                    //get terrain values at either end of the edge
                    //float vert1Sample = cubes[TerrainData.GetEdgeIndexesValue(indice, 0)];
                    //float vert2Sample = cubes[TerrainData.GetEdgeIndexesValue(indice, 1)];

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


                    //add vertices and triangles
                    // int vertIndex = VertForIndice(vertexPosition, position);
                    //triangles.Add(vertIndex);

                  //  bool check = checkUnique(vertexPosition, Currentindices, j);

                    //int newIndex = vertices.Length;
                    //lookup.Add(vertexPosition, newIndex);
                   // Currentindices.SetIndex(j, vertices.Length);
                   // vertices.AddNoResize(vertexPosition);
                    //if (check)
                    //{
                    //    vertices.Add(vertexPosition);

                    //    Currentindices.SetIndex(j, vertices.Length - 1);

                    //}

                    if(j == 0)
                    {
                        triangle.a = vertexPosition;
                    }
                    else if (j == 1)
                    {
                        triangle.b = vertexPosition;
                    }
                    else if (j == 2)
                    {
                        triangle.c = vertexPosition;
                    }

                    edgeIndex++;
                }

                // indices.Add(Currentindices);
                vertices.AddNoResize(triangle);
            }






            // cubes.Dispose();
        }
        //bool checkUnique(Vector3 vert, Indices curIndice, int iter)
        //{
        //    NativeParallelMultiHashMapIterator<Vector3> iterator;

        //    if (lookup.TryGetFirstValue(vert, out int entryIndex, out iterator))
        //    {
        //        do
        //        {

        //            // return true if vertex is unique
        //            return true;
        //        }
        //        while (lookup.TryGetNextValue(out entryIndex, ref iterator));
        //    }
        //    else
        //    {

        //        int newIndex = vertices.Length;
        //       // curIndice.SetIndex(iter, newIndex);
        //        lookup.Add(vert, newIndex);
        //        return false;
        //    }
        //}

        //int VertForIndice(Vector3 vert, Vector3 voxelPos)
        //{


        //    //loop through the vertices
        //    //for (int i = 0; i < vertices.Length; i++)
        //    //{
        //    //    //check if vertex already exists in the list. return it exists
        //    //    if (vertices[i] == vert)
        //    //    {
        //    //        return i;
        //    //    }
        //    //}




        //    //  Debug.Log("adding vert");

        //    // if it does not exist in list, add it to the list
        //    //   vertices.Add(vert);


        //    // return vertices.Length - 1;


        //    NativeParallelMultiHashMapIterator<Vector3> iterator;

        //    if (lookup.TryGetFirstValue(vert, out int entryIndex, out iterator))
        //    {
        //        do
        //        {
        //            // 'entryIndex' is the value associated with the key
        //            // Debug.Log($"index: {entryIndex} belongs to vert: {vert}");
        //            return entryIndex;
        //        }
        //        while (lookup.TryGetNextValue(out entryIndex, ref iterator));
        //    }

        //    // If it does not exist in the map, add it and return the new index
        //    int newIndex = vertices.Length;
        //    lookup.Add(vert, newIndex);
        //    vertices.Add(vert);


        //    return newIndex;
        //}

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


        half GetVoxelSample(Vector3 worldposition, int voxelSize/*, NativeArray<VoxelData_v2> voxelData*/)
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
            // return voxelData[voxelIndex].DistanceToSurface;
            return voxelData[voxelIndex].DistanceToSurface;
        }
    }
    [BurstCompile]
    public struct ProcessVertices : IJobFor
    {
        [NativeDisableParallelForRestriction,ReadOnly]
        public NativeList<Triangle> vertices;
        [NativeDisableParallelForRestriction]
        public NativeList<int> triangles;
        [NativeDisableParallelForRestriction]
        public NativeList<Vector3> Tempvertices;
        [ReadOnly]
        public NativeList<Indices> indices;
        [ReadOnly]
        public NativeParallelMultiHashMap<Vector3, int> lookup;
        int threadId;
        int numIndex;
        public void Execute(int index)
        {

            Tempvertices.Add(vertices[index].a);
            triangles.Add(Tempvertices.Length - 1);
            Tempvertices.Add(vertices[index].b);
            triangles.Add(Tempvertices.Length - 1);
            Tempvertices.Add(vertices[index].c);
            triangles.Add(Tempvertices.Length - 1);


            // Calculate the starting index for the current triplet
            //int vertexIndex0 = index * 3;
            //int vertexIndex1 = vertexIndex0 + 1;
            //int vertexIndex2 = vertexIndex0 + 2;

            //Vector3 v0 = Tempvertices[vertexIndex0];
            //Vector3 v1 = Tempvertices[vertexIndex1];
            //Vector3 v2 = Tempvertices[vertexIndex2];


            //int vertIndice0 = GetOrSetTriangleIndex(v0, vertexIndex0);
            //int vertIndice1 = GetOrSetTriangleIndex(v1, vertexIndex1);
            //int vertIndice2 = GetOrSetTriangleIndex(v2, vertexIndex2);


            //int vertIndice0 = indices[vertexIndex0].GetIndice(0, vertexIndex0);
            //int vertIndice1 = indices[vertexIndex1].GetIndice(1, vertexIndex1);
            //int vertIndice2 = indices[vertexIndex2].GetIndice(2, vertexIndex2);
            //if (vertexIndex0 < Tempvertices.Length && vertexIndex1 < Tempvertices.Length && vertexIndex2 < Tempvertices.Length)
            //{

            //    triangles.AddNoResize(vertexIndex0);
            //    triangles.AddNoResize(vertexIndex1);
            //    triangles.AddNoResize(vertexIndex2);
            //}

            //vertices.AddNoResize(v0);
            //numIndex++;
            //triangles.AddNoResize(numIndex);

            //vertices.AddNoResize(v1);
            //numIndex++;
            //triangles.AddNoResize(numIndex);

            //vertices.AddNoResize(v2);
            //numIndex++;
            //triangles.AddNoResize(numIndex);


            //int vertexIndex = index * 3;

            //if (vertexIndex + 2 < Tempvertices.Length)
            //{
            //    triangles.AddNoResize(vertexIndex);
            //    triangles.AddNoResize(vertexIndex + 1);
            //    triangles.AddNoResize(vertexIndex + 2);
            //}

        }

        //int GetOrSetTriangleIndex(Vector3 vert,int index)
        //{
        //    //NativeParallelMultiHashMapIterator<Vector3> iterator;

        //    //if (lookup.TryGetFirstValue(vert, out int entryIndex, out iterator))
        //    //{
        //    //    do
        //    //    {
        //    //       // Debug.Log("found one");
        //    //        return entryIndex;
        //    //    }
        //    //    while (lookup.TryGetNextValue(out entryIndex, ref iterator));
        //    //}
        //    //else
        //    //{
        //    //   // Debug.Log("nothing here to see");
        //    //   // vertices.AddNoResize(vert);
        //    //    return index;
        //    //}
        //    var data = vertices.ListData;
        //   for (int i = 0; i < vertices.coub; i++)
        //    {

        //    }
        //}
    }

    // [BurstCompile]
    public struct MarchingCube_Job_V2 : IJobParallelFor
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
        [NativeDisableParallelForRestriction]
        public NativeParallelMultiHashMap<Vector3, int> lookup;

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

            //sample terrain at each cube corner
            //NativeArray<half> cubes = new NativeArray<half>(8, allocator: Allocator.Temp);

            //for (int j = 0; j < 8; j++)
            //{
            //    //samples terrain data at neigboring cells
            //    Vector3 worldpos = position + (TerrainData.CornerTable[j] * voxelSize);
            //    cubes[j] = GetVoxelSample(worldpos, voxelSize, voxelData);

            //}

            ////get configuration index of the cube
            //int configIndex = GetCubeConfiguration(cubes);
            int configIndex = 0;
            for (int j = 0; j < 8; j++)
            {
                //samples terrain data at neighboring cells
                Vector3 worldPos = position + (TerrainData.CornerTable[j] * voxelSize);
                half voxelSample = GetVoxelSample(worldPos, voxelSize, voxelData);

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
                //cubes.Dispose();
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
                    float vert1Sample = GetVoxelSample(vert1, voxelSize, voxelData);
                    float vert2Sample = GetVoxelSample(vert2, voxelSize, voxelData);

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

                    //switch (j)
                    //{
                    //    case 0:
                    //        triangle.a = vertexPosition;
                    //        break;
                    //    case 1:
                    //        triangle.b = vertexPosition;
                    //        break;
                    //    case 2:
                    //        triangle.c = vertexPosition;
                    //        break;
                    //    default:
                    //        break;
                    //}

                    ////add triangle to list
                    //if (j == 2)
                    //{
                    //    triangles.Add(triangle);
                    //}

                    ////add vertices and triangles
                    //int vertIndex = VertForIndice(vertexPosition, position);
                    // triangles.Add(1);


                    //add vertices and triangles
                    int vertIndex = VertForIndice(vertexPosition, position);
                    triangles.Add(vertIndex);

                    edgeIndex++;
                }

                //need to add struct
            }







        }

        int VertForIndice(Vector3 vert, Vector3 voxelPos)
        {


            //loop through the vertices
            //for (int i = 0; i < vertices.Length; i++)
            //{
            //    //check if vertex already exists in the list. return it exists
            //    if (vertices[i] == vert)
            //    {
            //        return i;
            //    }
            //}




            //  Debug.Log("adding vert");

            // if it does not exist in list, add it to the list
            // vertices.Add(vert);


            //   return vertices.Length - 1;


            NativeParallelMultiHashMapIterator<Vector3> iterator;

            if (lookup.TryGetFirstValue(vert, out int entryIndex, out iterator))
            {
                do
                {
                    // 'entryIndex' is the value associated with the key
                    // Debug.Log($"index: {entryIndex} belongs to vert: {vert}");
                    return entryIndex;
                }
                while (lookup.TryGetNextValue(out entryIndex, ref iterator));
            }

            // If it does not exist in the map, add it and return the new index
            int newIndex = vertices.Length;
            lookup.Add(vert, newIndex);
            vertices.Add(vert);

            return newIndex;
        }

        //int VertForIndice(Vector3 vert, Vector3 voxelPos)


        //int GetCubeConfiguration(NativeArray<half> cube)
        //{
        //    int configurationIndex = 0;

        //    //iterate each corner
        //    for (int i = 0; i < 8; i++)
        //    {
        //        if (cube[i] > surfaceDensity)
        //        {
        //            //bitmask bool operation
        //            configurationIndex |= 1 << i;
        //        }
        //    }
        //    return configurationIndex;
        //}


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

    // [BurstCompile]
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

    public struct Triangle
    {
        public float3 a;
        public float3 b;
        public float3 c;
    }

    public struct Indices
    {

        public int index1;
        public int index2;
        public int index3;

        public void SetIndex(int index, int value)
        {
            switch (index)
            {
                case 0:
                    index1 = value;
                    break;
                case 1:
                    index2 = value;
                    break;
                case 2:
                    index3 = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), "Index out of range for MyStruct.");
            }
        }

        public int GetIndice(int index, int altValue = -1)
        {
            int selectedVal;
            switch (index)
            {
                case 0:
                    selectedVal = index1;
                    break;

                case 1:
                    selectedVal = index2;
                    break;
                case 2:
                    selectedVal = index3;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index), "Index out of range for MyStruct.");
            }

            if (selectedVal > 0)
            {
                return selectedVal;
            }

            return altValue;
        }

    }
    #endregion
}

